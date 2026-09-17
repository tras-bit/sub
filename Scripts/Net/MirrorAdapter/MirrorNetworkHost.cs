// ============================================================================
//  SUBSISTENCE — Net/MirrorAdapter/MirrorNetworkHost.cs
//  Адаптер Mirror. Код заперт в #if MIRROR, поэтому проект собирается и БЕЗ Mirror
//  (в Unity: меню «Subsistence → 3. Включить Mirror» добавляет define MIRROR и,
//  если получится, сам пакет с GitHub).
//  Что здесь есть: сервер/клиент Mirror, 30 Гц тик команд, interest management,
//  батчинг снапшотов по сетке 32 м, лаг-компенсация выстрелов, спавн игроков.
// ============================================================================
#if MIRROR
using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Subsistence.Core;

namespace Subsistence.Net
{
    /// <summary>Мост Mirror ↔ игровой код (реализует INetHost).</summary>
    public class MirrorNetworkHost : INetHost, IPlayerMessaging, IDisposable
    {
        public static MirrorNetworkHost Instance { get; private set; }

        public NetRole Role { get; private set; }
        public bool IsServer => NetworkServer.active;
        public bool IsClient => NetworkClient.active;
        public ulong LocalPlayerId => IsServer && !IsClient ? 0UL : (NetworkClient.connection?.identity?.netId ?? 0UL);
        public int PlayerCount => NetworkServer.connections.Count;
        public float ServerTime => (float)NetworkTime.time;
        public int TickRate => (int)Balance.TickRate;

        public event Action<ulong, ClientCommand, byte[]> OnCommand;
        public event Action<NetId, string, byte[]> OnRpc;
        public event Action<ulong> OnPlayerConnected;
        public event Action<ulong, string> OnPlayerDisconnected;

        public static void Install()
        {
            Instance = new MirrorNetworkHost { Role = NetworkServer.active && NetworkClient.active ? NetRole.Host : NetworkServer.active ? NetRole.Host : NetRole.Client };
            NetworkBridge.SetHost(Instance);
            // Игровой id игрока = netId его объекта, а НЕ connectionId транспорта:
            // только netId одинаков с обеих сторон — им и адресуются команды/RPC.
            NetworkServer.OnDisconnectedEvent += conn => Instance?.OnPlayerDisconnected?.Invoke(IdOf(conn), "disconnected");
        }

        public void SendCommand(ClientCommand cmd, byte[] payload, bool reliable = true)
        {
            var msg = new ClientCommandMessage { command = (byte)cmd, payload = payload ?? Array.Empty<byte>() };
            if (NetworkClient.active) NetworkClient.Send(msg, reliable ? Channels.Reliable : Channels.Unreliable);
            else if (NetworkServer.active) HandleServer(0UL, msg.command, msg.payload);
        }

        public void SendRpc(NetId target, string method, byte[] payload, bool reliable = true)
            => NetworkServer.SendToAll(new RpcMessage { targetId = target.Value, method = method, payload = payload ?? Array.Empty<byte>() }, reliable ? Channels.Reliable : Channels.Unreliable);

        /// <summary>Адресно: сообщение уйдёт только этому игроку (см. IPlayerMessaging).</summary>
        public void SendToPlayer(ulong playerId, string method, byte[] payload, bool reliable = true)
        {
            if (!NetworkServer.active) return;
            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn == null || conn.identity == null) continue;
                if ((ulong)conn.identity.netId != playerId) continue;
                conn.Send(new RpcMessage { targetId = 0, method = method, payload = payload ?? Array.Empty<byte>() },
                          reliable ? Channels.Reliable : Channels.Unreliable);
                return;
            }
        }

        /// <summary>Игровой id соединения: netId игрового объекта (до спавна — connectionId).</summary>
        internal static ulong IdOf(NetworkConnectionToClient conn)
            => conn != null && conn.identity != null ? (ulong)conn.identity.netId : (conn != null ? conn.connectionId : 0UL);

        internal void RaisePlayerConnected(ulong playerId) => OnPlayerConnected?.Invoke(playerId);

        /// <summary>Вход для серверного обработчика команд (вызывается из MirrorPlayer).</summary>
        internal void HandleServer(ulong fromConnection, byte command, byte[] payload)
            => OnCommand?.Invoke(fromConnection, (ClientCommand)command, payload);

        internal void HandleClientRpc(uint targetId, string method, byte[] payload)
            => OnRpc?.Invoke(new NetId(targetId), method, payload);

        public void Dispose() => Instance = null;
    }

    /// <summary>Сообщение команды клиент → сервер.</summary>
    public struct ClientCommandMessage : NetworkMessage
    {
        public byte command;
        public byte[] payload;
    }

    /// <summary>Серверное сообщение (снапшот/RPC/событие мира).</summary>
    public struct RpcMessage : NetworkMessage
    {
        public uint targetId;
        public string method;
        public byte[] payload;
    }

    /// <summary>
    /// Менеджер сети: держит tick loop 30 Гц, валидирует команды, рассылает снапшоты
    /// с учётом interest management (AOI-сетка), а также лаг-компенсацию для попаданий.
    /// </summary>
    public class MirrorSubsistenceManager : NetworkManager
    {
        [Header("Subsistence")]
        public GameObject playerPrefab;          // prefab игрока с MirrorPlayer
        public float snapshotInterval = 1f / 20f;
        public int maxPlayers = Balance.MaxPlayerSlots;      // слотов (128)
        public int softPlayerLimit = Balance.MaxPlayersOnline;// онлайн, выше которого не пускаем (112)
        public bool logLoadEvery10s = true;                  // печатать онлайн/AOI/трафик в консоль

        readonly Dictionary<ulong, CommandValidator> _validators = new Dictionary<ulong, CommandValidator>(256);
        readonly List<NetEntity> _queryBuffer = new List<NetEntity>(512);
        readonly BufferWriter _packet = BufferWriter.Rent(4096);   // один буфер на всю рассылку (без мусора)
        float _snapshotTimer;
        float _tickAccum;

        // ===== метрики нагрузки (100+ онлайн) =====
        float _loadTimer;
        long _bytesOut;
        int _packetsOut, _snapshotsOut;
        public static int OnlineCount { get; private set; }
        public static float LastAoiRadius { get; private set; } = Balance.AoiRadius;
        public static int LastSnapshotCount { get; private set; }
        long _roomsOut;
        public static float LastSnapshotHz { get; private set; } = Balance.SnapshotRate;

        public override void OnStartServer()
        {
            base.OnStartServer();
            NetworkServer.RegisterHandler<ClientCommandMessage>(OnServerCommand, true);
            MirrorNetworkHost.Install();
            Debug.Log($"[Mirror] Сервер запущен. Тик {Balance.TickRate} Гц, снапшоты {1f / snapshotInterval:F0} Гц.");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            NetworkClient.RegisterHandler<RpcMessage>(msg => MirrorNetworkHost.Instance?.HandleClientRpc(msg.targetId, msg.method, msg.payload), true);
            MirrorNetworkHost.Install();
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
            // Два лимита: жёсткий (слоты транспорта) и мягкий (онлайн, чтобы сервер не лёг).
            if (NetworkServer.connections.Count > maxPlayers) { conn.Disconnect(); return; }
            if (NetworkServer.connections.Count > softPlayerLimit)
            {
                Debug.LogWarning($"[Mirror] Отказ: онлайн {NetworkServer.connections.Count - 1}/{softPlayerLimit} (лимит сервера).");
                conn.Disconnect();
                return;
            }
            _validators[conn.connectionId] = new CommandValidator(conn.connectionId);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            _validators.Remove(conn.connectionId);
            base.OnServerDisconnect(conn);
        }

        void OnServerCommand(NetworkConnectionToClient conn, ClientCommandMessage msg)
        {
            // 1) валидация частоты и «невозможных» действий
            if (!_validators.TryGetValue(conn.connectionId, out var v)) { v = new CommandValidator(conn.connectionId); _validators[conn.connectionId] = v; }
            if (!v.Validate((ClientCommand)msg.command, msg.payload))
            {
                if (v.Violations >= 3) conn.Disconnect();
                return;
            }
            // 2) серверный тик: команды обрабатываются в порядке поступления.
            //    В игру отдаём netId игрока (не connectionId) — см. MirrorNetworkHost.IdOf.
            MirrorNetworkHost.Instance?.HandleServer(MirrorNetworkHost.IdOf(conn), msg.command, msg.payload);
        }

        void Update()
        {
            if (!NetworkServer.active) return;

            // ===== серверный тик 30 Гц =====
            _tickAccum += Time.deltaTime;
            float tickStep = 1f / Balance.TickRate;
            int guard = 0;
            while (_tickAccum >= tickStep && guard++ < 5)
            {
                _tickAccum -= tickStep;
                ServerTick();
            }

            // ===== снапшоты (20 Гц в норме, реже при полном онлайне) =====
            OnlineCount = NetworkServer.connections.Count;
            float interval = EffectiveInterval(OnlineCount);
            _snapshotTimer += Time.deltaTime;
            if (_snapshotTimer >= interval)
            {
                _snapshotTimer = 0f;
                BroadcastSnapshots(OnlineCount, interval);
            }

            if (logLoadEvery10s)
            {
                _loadTimer += Time.deltaTime;
                if (_loadTimer >= 10f) { _loadTimer = 0f; PrintLoad(); }
            }
        }

        /// <summary>
        /// LOD снапшотов под онлайн: до 64 игроков — полные 120 м и 20 Гц, дальше плавно ужимаем
        /// радиус и частоту (100+ онлайн на одном хосте без просадки тика).
        /// </summary>
        public float EffectiveInterval(int online)
        {
            var p = LoadProfile(online);
            return p.interval;
        }

        public struct NetLoadProfile
        {
            public float aoi;        // радиус интереса, м
            public int perPacket;    // сущностей в одном пакете
            public float interval;   // период снапшотов, с
            public float hz;         // частота снапшотов, Гц
        }

        public NetLoadProfile LoadProfile(int online)
        {
            int n = Mathf.Max(1, online);
            float k = Mathf.Lerp(1f, 0.45f, Mathf.InverseLerp(Balance.LodPlayerThreshold, maxPlayers, n));
            var p = new NetLoadProfile
            {
                aoi = Mathf.Clamp(Balance.AoiRadius * k, Balance.AoiRadiusMin, Balance.AoiRadius),
                perPacket = Mathf.Clamp(Balance.MaxEntitiesPerSnapshot - (n - Balance.LodPlayerThreshold), 24, Balance.MaxEntitiesPerSnapshot),
                interval = snapshotInterval * (n > Balance.LodPlayerThreshold ? 1.25f : 1f)
            };
            p.hz = 1f / Mathf.Max(0.02f, p.interval);
            return p;
        }

        void PrintLoad()
        {
            var p = LoadProfile(OnlineCount);
            float kbps = _bytesOut / 1024f / 10f;
            float perPlayer = _roomsOut / Mathf.Max(1f, _snapshotsOut) / Mathf.Max(1f, OnlineCount);
            Debug.Log($"[Mirror] онлайн {OnlineCount}/{softPlayerLimit}, AOI {p.aoi:F0} м, снапшоты {p.hz:F0} Гц, " +
                      $"снапшотов/с {_snapshotsOut / 10f:F1}, пакетов/с {_packetsOut / 10f:F1}, исходящий {kbps:F0} КБ/с " +
                      $"({_bytesOut / Mathf.Max(1f, OnlineCount) / 1024f:F1} КБ на игрока), " +
                      $"сущностей в пакете {perPlayer:F1}/{p.perPacket}, реестр {NetEntity.RegisteredCount}");
            if (SnapshotClient.Enabled && SnapshotClient.PacketsTotal > 0)
                Debug.Log($"[Mirror] клиент принял {SnapshotClient.EntitiesPerSecond:F0} сущностей/с, " +
                          $"{SnapshotClient.PacketsPerSecond:F0} пакетов/с, {SnapshotClient.KbPerSecond:F0} КБ/с");
            _bytesOut = 0; _packetsOut = 0; _snapshotsOut = 0; _roomsOut = 0;
        }

        void ServerTick()
        {
            // Монстры/деплои/гниение считаются своими Update (сервер-авторитет).
            // Здесь — только те системы, которым нужен единый детерминированный такт.
            Subsistence.AI.NoiseSystem.Recent.RemoveAll(n => Time.time - n.time > 12f);
        }

        void BroadcastSnapshots(int online, float interval)
        {
            var p = LoadProfile(online);
            LastAoiRadius = p.aoi;
            LastSnapshotHz = p.hz;

            var w = _packet;                       // буфер переиспользуется: 100+ игроков без мусора
            int lastPerConn = 0;
            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn == null || !conn.isReady) continue;
                var identity = conn.identity;
                if (identity == null) continue;
                Vector3 center = identity.transform.position;

                w.Reset();
                w.WriteFloat((float)NetworkTime.time);
                w.WriteUShort(0);           // заглушка под количество — заполняется ниже (WriteUShortAt)
                const int countPos = 4;     // float (4 Б) + ushort — позиция счётчика в пакете

                int count = 0;
                // Interest management: каждому соединению — только ближние сущности (радиус по LOD).
                InterestGrid.Query(center, p.aoi, _queryBuffer);
                for (int i = 0; i < _queryBuffer.Count; i++)
                {
                    var e = _queryBuffer[i];
                    if (e == null) continue;
                    w.WriteUInt(e.Net.Value);
                    e.WriteSnapshot(w);
                    count++;
                    if (count >= p.perPacket) break;   // лимит на пакет (анти-MTU)
                }
                w.WriteUShortAt(countPos, (ushort)count);   // клиент читает ровно столько сущностей

                byte[] payload = w.ToArray();
                conn.Send(new RpcMessage { targetId = 0, method = "snapshot", payload = payload });
                _bytesOut += payload.Length + 24;   // + заголовок канала
                _packetsOut++;
                _roomsOut += count;                 // сущностей ушло за тик (доля полезной нагрузки)
                lastPerConn = count;
            }
            _snapshotsOut++;
            LastSnapshotCount = lastPerConn;
        }

        /// <summary>Спавн игрока на сервере (вызывается из лобби/меню).</summary>
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            var go = Instantiate(playerPrefab);
            var mp = go.GetComponent<MirrorPlayer>();
            var spawn = Subsistence.Runtime.RuntimeBootstrap.FindAnyObjectByType<Subsistence.Runtime.RuntimeBootstrap>();
            Vector3 pos = spawn != null && spawn.Player != null ? spawn.Player.SpawnPoint : new Vector3(6, 1, 6);
            go.transform.position = pos;
            NetworkServer.AddPlayerForConnection(conn, go);
            mp.ownerConnectionId = MirrorNetworkHost.IdOf(conn);   // информационно (для логов)
            MirrorNetworkHost.Instance?.RaisePlayerConnected(mp.GameId);   // мир/инвентарь новому игроку
        }
    }

    /// <summary>Игрок в сети: получает команды, применяет урон/стройку, реплицирует состояние.</summary>
    public class MirrorPlayer : NetworkBehaviour
    {
        public ulong ownerConnectionId;
        /// <summary>Игровой id = netId объекта: одинаков на сервере и у клиента (адрес команд/RPC).</summary>
        public ulong GameId => (ulong)netId;
        [SyncVar] public float syncHealth = 100f;
        [SyncVar] public byte syncFlags;

        Subsistence.Player.PlayerController _controller;

        public override void OnStartServer()
        {
            _controller = GetComponent<Subsistence.Player.PlayerController>();
            DeployNet.RegisterServerPlayer(GameId, _controller);             // 16_doors: кто есть кто
            var host = MirrorNetworkHost.Instance;
            if (host != null) host.OnCommand += HandleCommand;
        }

        public override void OnStopServer()
        {
            DeployNet.UnregisterServerPlayer(GameId);
            var host = MirrorNetworkHost.Instance;
            if (host != null) host.OnCommand -= HandleCommand;
        }

        /// <summary>Клиент: запоминаем игроков — по этим id адресуется урон (CombatNet).</summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            var pc = GetComponent<Subsistence.Player.PlayerController>();
            if (pc != null) DeployNet.RegisterKnownPlayer(GameId, pc);
        }

        public override void OnStopClient()
        {
            DeployNet.UnregisterKnownPlayer(GameId);
            base.OnStopClient();
        }

        /// <summary>Серверная обработка команд игрока ( повреждения, лут, стройка ).</summary>
        void HandleCommand(ulong from, ClientCommand cmd, byte[] payload)
        {
            if (from != GameId) return;
            var r = new BufferReader(payload);
            switch (cmd)
            {
                case ClientCommand.Move:
                {
                    var pos = r.ReadPosition(-100f, 1000f);
                    var rot = r.ReadAngles();
                    r.ReadBool(); r.ReadBool();
                    // проверка скорости (CommandValidator уже отсекает грубые читы)
                    _controller.transform.position = Vector3.Lerp(_controller.transform.position, pos, 0.6f);
                    _controller.transform.rotation = rot;
                    break;
                }
                case ClientCommand.FireWeapon:
                    // сервер сам считает попадание: клиент лишь сообщает «выстрелил в этом направлении»
                    break;
                case ClientCommand.PlaceBuilding:
                    // 30–31: сервер сам строит и рассылает блок (та же валидация, что в BuildController)
                    SpawnNet.HandleServerCommand(from, cmd, payload);
                    break;
                case ClientCommand.PlaceDeployable:
                    // 30–31: клиент не создаёт деплои локально — сервер спавнит и рассылает
                    SpawnNet.HandleServerCommand(from, cmd, payload);
                    break;
                case ClientCommand.UseDeployable:
                case ClientCommand.SetCode:
                    // 16_doors: двери и замки (валидация дистанции + ключ/код — внутри DeployNet)
                    DeployNet.HandleServerCommand(from, cmd, payload);
                    break;
                case ClientCommand.InvSync:
                case ClientCommand.LootOpen:
                case ClientCommand.LootTake:
                case ClientCommand.LootStore:
                case ClientCommand.PlayerDied:
                    // 30–31: зеркало инвентаря, серверный лут и смерть (InventoryNet собирает мешок)
                    InventoryNet.HandleServerCommand(from, cmd, payload);
                    break;
                case ClientCommand.LootDrop:
                case ClientCommand.LootPickup:
                    // 30–31: предметы на полу — сервер создаёт/отдаёт (дистанцию проверяет SpawnNet)
                    SpawnNet.HandleServerCommand(from, cmd, payload);
                    break;
                case ClientCommand.HitPlayer:
                case ClientCommand.PlayerHealth:
                    // 30–31: бой — урон и здоровье считает сервер (дистанция/угол/стена — в CombatNet)
                    CombatNet.HandleServerCommand(from, cmd, payload);
                    break;
                case ClientCommand.Respawn:
                    _controller.Respawn(Subsistence.Runtime.RuntimeBootstrap.FindAnyObjectByType<Subsistence.Runtime.RuntimeBootstrap>()?.playerSpawn ?? Vector3.zero);
                    break;
            }
        }

        void Update()
        {
            if (!isServer) return;
            // Здоровье для чужих клиентов берём из CombatNet: у серверного двойника своей
            // симуляции нет (он только повторяет Move-команды), истина — в зеркале CombatNet.
            syncHealth = CombatNet.HealthOf(GameId);
        }
    }
}
#endif
