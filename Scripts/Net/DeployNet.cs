// ============================================================================
//  SUBSISTENCE — Net/DeployNet.cs
//  16_doors / 30–31: сетевой путь для деплоев-интерактива (двери и замки).
//  Один и тот же код работает офлайн (LocalHost) и в Mirror:
//    • клиент шлёт ClientCommand.UseDeployable / SetCode с позицией двери;
//    • сервер валидирует (дистанция, парный ключ, код) и рассылает RPC;
//    • клиенты применяют состояние молча — без шума и без повторных проверок.
//  Код замка НИКОГДА не рассылается: у клиента его просто нет (как в Rust).
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Building;

namespace Subsistence.Net
{
    public static class DeployNet
    {
        public enum DoorAction : byte { Toggle = 0, Key = 1, Code = 2 }

        /// <summary>Сервер ищет игрока по id: офлайн — локальный, Mirror подставит свой резолвер.</summary>
        public static Func<ulong, Subsistence.Player.PlayerController> ServerPlayerOf;

        // Кто есть кто на сервере (заполняет MirrorPlayer при подключении): connectionId → игрок.
        static readonly Dictionary<ulong, Subsistence.Player.PlayerController> _serverPlayers
            = new Dictionary<ulong, Subsistence.Player.PlayerController>(128);

        // Кого мы знаем по id (сервер — всех, клиент — тех, кого видит через MirrorPlayer):
        // по этой таблице находится id цели для урона (CombatNet).
        static readonly Dictionary<ulong, Subsistence.Player.PlayerController> _known
            = new Dictionary<ulong, Subsistence.Player.PlayerController>(128);

        public static void RegisterServerPlayer(ulong id, Subsistence.Player.PlayerController pc)
        { if (pc != null) { _serverPlayers[id] = pc; _known[id] = pc; } }

        public static void UnregisterServerPlayer(ulong id)
        { _serverPlayers.Remove(id); _known.Remove(id); }

        /// <summary>Клиент: запомнить игрока (вызывает MirrorPlayer на своей стороне).</summary>
        public static void RegisterKnownPlayer(ulong id, Subsistence.Player.PlayerController pc)
        { if (pc != null) _known[id] = pc; }

        public static void UnregisterKnownPlayer(ulong id) => _known.Remove(id);

        /// <summary>Наш ли это игрок (объект, которым управляем мы, а не сетевой двойник).</summary>
        public static bool IsLocal(Subsistence.Player.PlayerController pc) => pc != null && pc == LocalPlayer();

        /// <summary>Сетевой id игрока (0 — не знаем такого).</summary>
        public static ulong IdOf(Subsistence.Player.PlayerController pc)
        {
            if (pc == null) return 0UL;
            var host = NetworkBridge.Host;
            if (host != null && pc == LocalPlayer()) return host.LocalPlayerId;
            foreach (var kv in _known) if (kv.Value == pc) return kv.Key;
            foreach (var kv in _serverPlayers) if (kv.Value == pc) return kv.Key;
            return 0UL;
        }

        static bool _inited;

        public static void Init()
        {
            if (_inited) return;
            _inited = true;
            Hook();
            NetworkBridge.HostChanged += Hook;
            Debug.Log("[Net] DeployNet: сетевой путь дверей/замков активен");
        }

        static void Hook()
        {
            var host = NetworkBridge.Host;
            if (host == null) return;

            // Командный цикл: офлайн замыкается сам на себя (LocalHost),
            // в Mirror команды уже роутит MirrorPlayer — иначе они обработались бы дважды.
            host.OnCommand -= OnCommand;
            if (host is LocalHost) host.OnCommand += OnCommand;

            host.OnRpc -= OnRpc;
            host.OnRpc += OnRpc;
        }

        public static Subsistence.Player.PlayerController LocalPlayer()
            => Subsistence.Runtime.RuntimeBootstrap.Instance != null
               ? Subsistence.Runtime.RuntimeBootstrap.Instance.Player
               : null;

        /// <summary>Сервер: игрок по id (офлайн — локальный, Mirror — зарегистрированный).</summary>
        public static Subsistence.Player.PlayerController PlayerOf(ulong id)
        {
            if (ServerPlayerOf != null) return ServerPlayerOf(id);

            // Хост-клиент: объект, который адаптер заспавнил под нашим же netId, никто не двигает —
            // «свой» игрок это тот, кем реально управляют из RuntimeBootstrap.
            var host = NetworkBridge.Host;
            if (host != null && host.IsClient && id == host.LocalPlayerId)
            {
                var local = LocalPlayer();
                if (local != null) return local;
            }

            if (_serverPlayers.TryGetValue(id, out var pc)) return pc;   // Mirror: сервер знает всех
            return LocalPlayer();                                        // офлайн: сервер и клиент — один игрок
        }

        // ================== КЛИЕНТ → СЕРВЕР ==================

        /// <summary>«E» по двери: сервер проверит замок и применит состояние.</summary>
        public static void RequestToggle(DoorDeployable door)
            => SendUse(door, DoorAction.Toggle, null);

        /// <summary>Дверь под ключевым замком: сервер сам посмотрит инвентарь игрока.</summary>
        public static void RequestKey(DoorDeployable door)
            => SendUse(door, DoorAction.Key, null);

        /// <summary>Код введён в терминал: проверяет сервер (у клиента кода нет).</summary>
        public static void RequestCode(DoorDeployable door, string code)
            => SendUse(door, DoorAction.Code, code);

        /// <summary>Смена кода владельцем двери (ClientCommand.SetCode).</summary>
        public static void RequestSetCode(DoorDeployable door, string newCode)
        {
            if (door == null || string.IsNullOrEmpty(newCode)) return;
            var w = BufferWriter.Rent();
            w.WriteByte((byte)DoorAction.Code);
            w.WritePosition(door.transform.position, -100f, 1000f);
            w.WriteString(newCode);
            NetworkBridge.Command(ClientCommand.SetCode, w.ToArray());
            BufferWriter.Return(w);
        }

        static void SendUse(DoorDeployable door, DoorAction action, string code)
        {
            if (door == null) return;
            var w = BufferWriter.Rent();
            w.WriteByte((byte)action);
            w.WritePosition(door.transform.position, -100f, 1000f);
            if (action == DoorAction.Code) w.WriteString(code);
            NetworkBridge.Command(ClientCommand.UseDeployable, w.ToArray());
            BufferWriter.Return(w);
        }

        // ================== СЕРВЕР ==================

        /// <summary>Вызывается из MirrorPlayer (и локально через LocalHost).</summary>
        public static void HandleServerCommand(ulong from, ClientCommand cmd, byte[] payload)
        {
            if (!NetworkBridge.IsServer) return;
            if (cmd == ClientCommand.UseDeployable) ServerUse(from, payload);
            else if (cmd == ClientCommand.SetCode) ServerSetCode(from, payload);
        }

        static void OnCommand(ulong from, ClientCommand cmd, byte[] payload)
            => HandleServerCommand(from, cmd, payload);

        static void ServerUse(ulong from, byte[] payload)
        {
            var r = new BufferReader(payload);
            var action = (DoorAction)r.ReadByte();
            var pos = r.ReadPosition(-100f, 1000f);
            string code = action == DoorAction.Code ? r.ReadString() : null;

            var door = DoorDeployable.FindAt(pos, 1.5f);
            var player = PlayerOf(from);
            if (door == null || player == null) return;

            // Античит: игрок должен стоять рядом с дверью (продолжение спринта 6).
            if (Vector3.Distance(player.transform.position, door.transform.position) > 9f) return;

            door.ServerUse(action, code, from, player);
        }

        static void ServerSetCode(ulong from, byte[] payload)
        {
            var r = new BufferReader(payload);
            r.ReadByte();
            var pos = r.ReadPosition(-100f, 1000f);
            string newCode = r.ReadString();

            var door = DoorDeployable.FindAt(pos, 1.5f);
            if (door == null || door.doorLock == null) return;
            if (newCode.Length != 4 || !int.TryParse(newCode, out _)) return;

            // Код меняет тот, у кого уже есть доступ (в Rust — владелец с кодом).
            if (!door.doorLock.granted.Contains(from)) return;
            door.doorLock.code = newCode;
            door.doorLock.locked = true;
            BroadcastLock(door);
            Debug.Log($"[Net] код замка у {door.transform.position} изменён игроком {from}");
        }

        // ================== СЕРВЕР → ВСЕ ==================

        static void OnRpc(NetId target, string method, byte[] payload)
        {
            if (string.IsNullOrEmpty(method)) return;
            switch (method)
            {
                case "door.state": ApplyDoorState(payload); break;
                case "door.lock": ApplyDoorLock(payload); break;
                case "door.grant": ApplyDoorGrant(payload); break;
                case "door.deny": ApplyDoorDeny(payload); break;
                case "net.msg": ApplyNotify(payload); break;
            }
        }

        /// <summary>Состояние створки: pos + open + locked + kind.</summary>
        public static void BroadcastState(DoorDeployable door)
        {
            if (door == null || !NetworkBridge.IsServer) return;
            var w = BufferWriter.Rent();
            w.WritePosition(door.transform.position, -100f, 1000f);
            w.WriteBool(door.IsOpen);
            w.WriteBool(door.doorLock != null && door.doorLock.locked);
            w.WriteByte(door.doorLock != null ? (byte)door.doorLock.kind : (byte)255);
            NetworkBridge.Host?.SendRpc(NetId.None, "door.state", w.ToArray());
            BufferWriter.Return(w);
        }

        /// <summary>Появился замок (модель + «заперто» у всех клиентов).</summary>
        public static void BroadcastLock(DoorDeployable door)
        {
            if (door == null || !NetworkBridge.IsServer) return;
            var w = BufferWriter.Rent();
            w.WritePosition(door.transform.position, -100f, 1000f);
            w.WriteByte(door.doorLock != null ? (byte)door.doorLock.kind : (byte)255);
            w.WriteBool(door.doorLock != null && door.doorLock.locked);
            NetworkBridge.Host?.SendRpc(NetId.None, "door.lock", w.ToArray());
            BufferWriter.Return(w);
        }

        /// <summary>Доступ выдан конкретному игроку (код верный / ключ подошёл).</summary>
        public static void BroadcastGrant(DoorDeployable door, ulong player)
        {
            if (door == null || !NetworkBridge.IsServer) return;
            var w = BufferWriter.Rent();
            w.WritePosition(door.transform.position, -100f, 1000f);
            w.WriteULong(player);
            NetworkBridge.Host?.SendRpc(NetId.None, "door.grant", w.ToArray());
            BufferWriter.Return(w);
        }

        /// <summary>Сервер → конкретному игроку: короткое сообщение в HUD (причина отказа и т.п.).</summary>
        public static void Notify(ulong player, string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            var w = BufferWriter.Rent();
            w.WriteULong(player);
            w.WriteString(message);
            PlayerRpc.Send(player, "net.msg", w.ToArray());      // только адресату (в Mirror — по netId)
            BufferWriter.Return(w);
        }

        /// <summary>Отказ (неверный код / нет ключа) — реагирует только тот, кто открыл терминал.</summary>
        public static void BroadcastDeny(DoorDeployable door, string reason)
        {
            if (door == null) return;
            var w = BufferWriter.Rent();
            w.WritePosition(door.transform.position, -100f, 1000f);
            w.WriteString(reason);
            NetworkBridge.Host?.SendRpc(NetId.None, "door.deny", w.ToArray());
            BufferWriter.Return(w);
        }

        // ================== ПРИЁМ НА КЛИЕНТЕ ==================

        static void ApplyDoorState(byte[] payload)
        {
            var r = new BufferReader(payload);
            var pos = r.ReadPosition(-100f, 1000f);
            bool open = r.ReadBool();
            bool locked = r.ReadBool();
            byte kind = r.ReadByte();

            var door = DoorDeployable.FindAt(pos, 1.5f);
            if (door == null) return;
            door.ApplyRemote(open);

            if (kind != 255 && door.doorLock == null) door.ApplyRemoteLock(kind, locked);
            else if (door.doorLock != null) door.doorLock.locked = locked;
        }

        static void ApplyDoorLock(byte[] payload)
        {
            var r = new BufferReader(payload);
            var pos = r.ReadPosition(-100f, 1000f);
            byte kind = r.ReadByte();
            bool locked = r.ReadBool();

            var door = DoorDeployable.FindAt(pos, 1.5f);
            if (door == null) return;
            if (kind == 255) return;
            door.ApplyRemoteLock(kind, locked);
        }

        static void ApplyDoorGrant(byte[] payload)
        {
            var r = new BufferReader(payload);
            var pos = r.ReadPosition(-100f, 1000f);
            ulong player = r.ReadULong();

            ulong me = NetworkBridge.Host?.LocalPlayerId ?? 0UL;
            if (player != me) return;                       // чужой доступ нам знать не нужно
            var door = DoorDeployable.FindAt(pos, 1.5f);
            if (door == null || door.doorLock == null) return;
            door.doorLock.granted.Add(me);
            Subsistence.UI.CodeLockUI.OnGranted(door);      // терминал: «ДОСТУП РАЗРЕШЁН»
        }

        static void ApplyNotify(byte[] payload)
        {
            var r = new BufferReader(payload);
            ulong to = r.ReadULong();
            string msg = r.ReadString();
            ulong me = NetworkBridge.Host?.LocalPlayerId ?? 0UL;
            if (to != me) return;                       // сообщение не нам
            Subsistence.UI.HudRuntime.ShowToast(msg, 2.5f);
        }

        static void ApplyDoorDeny(byte[] payload)
        {
            var r = new BufferReader(payload);
            var pos = r.ReadPosition(-100f, 1000f);
            string reason = r.ReadString();

            var door = DoorDeployable.FindAt(pos, 1.5f);
            if (door == null) return;
            Subsistence.UI.CodeLockUI.OnDenied(door, reason);   // терминал открыт → красная строка
        }
    }
}
