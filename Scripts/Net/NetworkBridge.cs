// ============================================================================
//  SUBSISTENCE — Net/NetworkBridge.cs
//  Абстракция сети. Вся геймплейная логика пишется ТОЛЬКО через этот интерфейс,
//  поэтому переезд на Mirror (или на что угодно) не трогает геймплей.
//  Сейчас доступны:
//    LocalHost        — офлайн/сингл, «сервер» живёт в том же процессе (тесты, редактор);
//    MirrorNetworkHost — включается через меню Subsistence → 3. Включить Mirror
//                        (файл в Assets/Subsistence/Net/MirrorAdapter~/).
//  Архитектура рассчитана на 100+ игроков: серверный авторитет, interest management,
//  батчинг репликации, клиентская интерполяция, авторитет по зонам (см. docs/).
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;

namespace Subsistence.Net
{
    /// <summary>Идентификатор сетевого объекта (уникален на сервере, 0 = нет).</summary>
    public readonly struct NetId : IEquatable<NetId>
    {
        public readonly uint Value;
        public NetId(uint v) { Value = v; }
        public static readonly NetId None = new NetId(0);
        public bool IsValid => Value != 0;
        public bool Equals(NetId o) => Value == o.Value;
        public override bool Equals(object o) => o is NetId n && Equals(n);
        public override int GetHashCode() => (int)Value;
        public override string ToString() => "N" + Value;
        public static bool operator ==(NetId a, NetId b) => a.Value == b.Value;
        public static bool operator !=(NetId a, NetId b) => a.Value != b.Value;
    }

    /// <summary>Кто мы: сервер считает урон/стройку/лут, клиент только предсказывает и просит.</summary>
    public enum NetRole : byte { Offline, Host, Client }

    /// <summary>Команды клиент→сервер. Всё проходит валидацию (см. CommandValidator).</summary>
    public enum ClientCommand : byte
    {
        Move = 1, Look = 2, FireWeapon = 3, Reload = 4, SwapHotbar = 5,
        InteractLoot = 6, QuickMoveItem = 7, DropItem = 8, Consume = 9,
        PlaceBuilding = 10, UpgradeBuilding = 11, RotateBuilding = 12,
        PlaceDeployable = 13, UseDeployable = 14, Craft = 15, Respawn = 16,
        MeleeAttack = 17, ThrowExplosive = 18, Chat = 19, AuthToolCupboard = 20,
        SetCode = 21, Repair = 22, SkinItem = 23, VoiceData = 24,
        InvSync = 25,                    // клиент → сервер: дельта слотов инвентаря
        LootOpen = 26, LootTake = 27, LootStore = 28,  // лут: сервер владеет содержимым
        LootDrop = 29, LootPickup = 30,                // предметы на полу: выбросить / подобрать
        PlayerDied = 31,                               // смерть: мешок с лутом собирает сервер
        PlayerHealth = 32, HitPlayer = 33              // бой: своё здоровье и попадание по игроку
    }

    /// <summary>Транспорт. Реализации: LocalHost (офлайн) и MirrorNetworkHost (~адаптер).</summary>
    public interface INetHost
    {
        NetRole Role { get; }
        bool IsServer { get; }
        bool IsClient { get; }
        ulong LocalPlayerId { get; }
        int PlayerCount { get; }
        float ServerTime { get; }              // авторитетное время сервера (для интерполяции)
        int TickRate { get; }

        void SendCommand(ClientCommand cmd, byte[] payload, bool reliable = true);
        void SendRpc(NetId target, string method, byte[] payload, bool reliable = true);

        event Action<ulong, ClientCommand, byte[]> OnCommand;      // сервер
        event Action<NetId, string, byte[]> OnRpc;                 // обе стороны
        event Action<ulong> OnPlayerConnected;
        event Action<ulong, string> OnPlayerDisconnected;
    }

    /// <summary>
    /// Необязательная возможность хоста: доставить RPC ОДНОМУ игроку.
    /// Mirror умеет (по netId соединения); LocalHost — нет, но офлайн игрок один,
    /// и там хватает обычной рассылки.
    /// </summary>
    public interface IPlayerMessaging
    {
        void SendToPlayer(ulong playerId, string method, byte[] payload, bool reliable = true);
    }

    /// <summary>Адресное сообщение игроку: «это только тебе» (тосты, отказы, выдача предметов).</summary>
    public static class PlayerRpc
    {
        public static void Send(ulong playerId, string method, byte[] payload)
        {
            var host = NetworkBridge.Host;
            if (host == null) return;
            if (host is IPlayerMessaging pm) pm.SendToPlayer(playerId, method, payload);
            else host.SendRpc(NetId.None, method, payload);      // офлайн: игрок один
        }
    }

    /// <summary>
    /// Мост: единая точка входа для геймплея. Если сети нет — включается LocalHost,
    /// и игра работает в одиночном режиме с точно той же логикой серверного авторитета.
    /// </summary>
    public static class NetworkBridge
    {
        public static INetHost Host { get; private set; }
        public static bool Ready => Host != null;
        public static bool IsServer => Host == null || Host.IsServer;   // офлайн = мы сервер
        public static bool IsClient => Host != null && Host.IsClient;

        public static event Action HostChanged;

        /// <summary>Подключает транспорт (вызывает MirrorNetworkHost или LocalHost).</summary>
        public static void SetHost(INetHost host)
        {
            if (Host != null && Host is IDisposable d) d.Dispose();
            Host = host;
            HostChanged?.Invoke();
            Debug.Log($"[Net] Транспорт: {host?.GetType().Name ?? "none"} role={(host != null ? host.Role.ToString() : "offline")}");
        }

        public static void UseLocalHost() => SetHost(new LocalHost());

        /// <summary>Отправить команду на сервер (в офлайне — обрабатывается сразу локально).</summary>
        public static void Command(ClientCommand cmd, byte[] payload = null, bool reliable = true)
        {
            if (Host != null) Host.SendCommand(cmd, payload, reliable);
            else LocalHost.DispatchLocal(cmd, payload);
        }

        /// <summary>Выполнить код только на сервере-владельце логики.</summary>
        public static void ServerOnly(Action a)
        {
            if (IsServer) a();
        }
    }

    /// <summary>Офлайн-транспорт: команды уходят «самому себе» без задержки.</summary>
    public sealed class LocalHost : INetHost
    {
        public NetRole Role => NetRole.Offline;
        public bool IsServer => true;
        public bool IsClient => true;
        public ulong LocalPlayerId => 0UL;
        public int PlayerCount => 1;
        public float ServerTime => Time.time;
        public int TickRate => (int)Balance.TickRate;

        public event Action<ulong, ClientCommand, byte[]> OnCommand;
        public event Action<NetId, string, byte[]> OnRpc;
#pragma warning disable 67
        // События заведены под будущий Mirror-хост: офлайн их никто не слушает (CS0067 — норма).
        public event Action<ulong> OnPlayerConnected;
        public event Action<ulong, string> OnPlayerDisconnected;
#pragma warning restore 67

        public LocalHost()
        {
            OnCommand?.Invoke(0UL, ClientCommand.Respawn, null);
        }

        public void SendCommand(ClientCommand cmd, byte[] payload, bool reliable = true) => DispatchLocal(cmd, payload);

        public void SendRpc(NetId target, string method, byte[] payload, bool reliable = true) => OnRpc?.Invoke(target, method, payload);

        /// <summary>Локальная обработка: подписчики (системы мира) сами решают, что делать.</summary>
        public static void DispatchLocal(ClientCommand cmd, byte[] payload)
        {
            LocalCommandQueue.Enqueue(cmd, payload);
        }
    }

    /// <summary>Очередь локальных команд — обрабатывается в Update (детерминированный порядок).</summary>
    public static class LocalCommandQueue
    {
        struct Entry { public ClientCommand cmd; public byte[] payload; }
        static readonly Queue<Entry> _q = new Queue<Entry>(32);

        public static void Enqueue(ClientCommand cmd, byte[] payload)
        {
            _q.Enqueue(new Entry { cmd = cmd, payload = payload });
        }

        public static bool TryDequeue(out ClientCommand cmd, out byte[] payload)
        {
            if (_q.Count == 0) { cmd = 0; payload = null; return false; }
            var e = _q.Dequeue(); cmd = e.cmd; payload = e.payload; return true;
        }
    }

    /// <summary>
    /// Сериализация в байты без аллокаций (BufferWriter/BufferReader в пуле).
    /// 100+ игроков требуют минимума GC — переиспользуем буферы.
    /// </summary>
    public sealed class BufferWriter
    {
        byte[] _buf; int _pos;
        static readonly Stack<BufferWriter> _pool = new Stack<BufferWriter>(64);
        public static BufferWriter Rent(int capacity = 256)
        {
            var w = _pool.Count > 0 ? _pool.Pop() : new BufferWriter();
            if (w._buf == null || w._buf.Length < capacity) w._buf = new byte[capacity];
            w._pos = 0; return w;
        }
        public static void Return(BufferWriter w) { if (w != null && _pool.Count < 256) _pool.Push(w); }

        public int Length => _pos;
        public byte[] Raw => _buf;

        /// <summary>Сброс без возврата в пул — один писатель на всю рассылку снапшотов (100+ игроков).</summary>
        public void Reset() { _pos = 0; }

        void Ensure(int n) { if (_pos + n > _buf.Length) Array.Resize(ref _buf, Mathf.NextPowerOfTwo(_pos + n)); }
        public void WriteByte(byte v) { Ensure(1); _buf[_pos++] = v; }
        public void WriteBool(bool v) => WriteByte(v ? (byte)1 : (byte)0);
        public void WriteUShort(ushort v) { Ensure(2); _buf[_pos++] = (byte)(v & 0xFF); _buf[_pos++] = (byte)(v >> 8); }
        /// <summary>
        /// Правка уже записанного ushort (снапшот пишет заглушку под количество сущностей,
        /// а заполняет её после обхода AOI — иначе пришлось бы писать пакет дважды).
        /// </summary>
        public void WriteUShortAt(int pos, ushort v)
        {
            if (pos < 0 || pos + 1 >= _buf.Length) return;
            _buf[pos] = (byte)(v & 0xFF); _buf[pos + 1] = (byte)(v >> 8);
        }
        public void WriteUInt(uint v) { Ensure(4); _buf[_pos++] = (byte)v; _buf[_pos++] = (byte)(v >> 8); _buf[_pos++] = (byte)(v >> 16); _buf[_pos++] = (byte)(v >> 24); }
        public void WriteULong(ulong v) { WriteUInt((uint)v); WriteUInt((uint)(v >> 32)); }
        public void WriteFloat(float v) => WriteUInt(BitConverter.ToUInt32(BitConverter.GetBytes(v), 0));
        /// <summary>Квантованные трансформы (как в FPS-сетевухах): позиция 0.01 м, углы 0.5°.</summary>
        public void WritePosition(Vector3 p, float origin = 0f, float range = 500f)
        {
            Ensure(6);
            ushort x = (ushort)Mathf.Clamp(Mathf.RoundToInt((p.x - origin) / range * 65535f), 0, 65535);
            ushort y = (ushort)Mathf.Clamp(Mathf.RoundToInt((p.y - origin) / range * 65535f), 0, 65535);
            ushort z = (ushort)Mathf.Clamp(Mathf.RoundToInt((p.z - origin) / range * 65535f), 0, 65535);
            WriteUShort(x); WriteUShort(y); WriteUShort(z);
        }
        public void WriteAngles(Quaternion q)
        {
            var e = q.eulerAngles;
            WriteUShort((ushort)Mathf.RoundToInt(Mathf.Repeat(e.x, 360f) / 360f * 65535f));
            WriteUShort((ushort)Mathf.RoundToInt(Mathf.Repeat(e.y, 360f) / 360f * 65535f));
        }
        public void WriteString(string s)
        {
            if (string.IsNullOrEmpty(s)) { WriteUShort(0); return; }
            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            WriteUShort((ushort)Mathf.Min(bytes.Length, 512));
            Ensure(bytes.Length); Buffer.BlockCopy(bytes, 0, _buf, _pos, Mathf.Min(bytes.Length, 512)); _pos += Mathf.Min(bytes.Length, 512);
        }
        public byte[] ToArray()
        {
            var copy = new byte[_pos];
            Buffer.BlockCopy(_buf, 0, copy, 0, _pos);
            return copy;
        }
    }

    public sealed class BufferReader
    {
        byte[] _buf; int _pos;
        public BufferReader(byte[] buf) { _buf = buf ?? Array.Empty<byte>(); }
        /// <summary>Переиспользование одного читателя: 100+ игроков × 20 Гц не должны мусорить.</summary>
        public void Reset(byte[] buf) { _buf = buf ?? Array.Empty<byte>(); _pos = 0; }
        public int Position => _pos;
        public int Remaining => _buf.Length - _pos;
        public byte ReadByte() => _pos < _buf.Length ? _buf[_pos++] : (byte)0;
        public bool ReadBool() => ReadByte() != 0;
        public ushort ReadUShort() { if (_pos + 2 > _buf.Length) return 0; ushort v = (ushort)(_buf[_pos] | (_buf[_pos + 1] << 8)); _pos += 2; return v; }
        public uint ReadUInt() { if (_pos + 4 > _buf.Length) return 0; uint v = (uint)(_buf[_pos] | (_buf[_pos + 1] << 8) | (_buf[_pos + 2] << 16) | (_buf[_pos + 3] << 24)); _pos += 4; return v; }
        public ulong ReadULong() { ulong lo = ReadUInt(); ulong hi = ReadUInt(); return lo | (hi << 32); }
        public float ReadFloat() => BitConverter.ToSingle(BitConverter.GetBytes(ReadUInt()), 0);
        public Vector3 ReadPosition(float origin = 0f, float range = 500f)
            => new Vector3(origin + ReadUShort() / 65535f * range, origin + ReadUShort() / 65535f * range, origin + ReadUShort() / 65535f * range);
        public Quaternion ReadAngles()
        {
            float x = ReadUShort() / 65535f * 360f, y = ReadUShort() / 65535f * 360f;
            return Quaternion.Euler(x, y, 0f);
        }
        public string ReadString()
        {
            int len = ReadUShort();
            if (len <= 0 || _pos + len > _buf.Length) return string.Empty;
            var s = System.Text.Encoding.UTF8.GetString(_buf, _pos, len);
            _pos += len; return s;
        }
    }

    /// <summary>Клиентская интерполяция удалённых игроков/монстров (для 100+ игроков обязательна).</summary>
    public class SnapshotInterpolator
    {
        public struct Snap { public float serverTime; public Vector3 pos; public Quaternion rot; public Vector3 velocity; }
        readonly Snap[] _buffer = new Snap[16];
        int _count, _head;

        public void Push(float serverTime, Vector3 pos, Quaternion rot, Vector3 velocity)
        {
            _buffer[_head] = new Snap { serverTime = serverTime, pos = pos, rot = rot, velocity = velocity };
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length) _count++;
        }

        /// <summary>Интерполяция с задержкой в 2 снапшота (Rust-подобная сглаженность).</summary>
        public bool Sample(float now, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero; rot = Quaternion.identity;
            if (_count == 0) return false;
            if (_count == 1) { pos = _buffer[(_head - 1 + _buffer.Length) % _buffer.Length].pos; rot = _buffer[(_head - 1 + _buffer.Length) % _buffer.Length].rot; return true; }

            float target = now - (1f / Balance.SnapshotRate) * 2f;
            for (int i = 0; i < _count - 1; i++)
            {
                int a = (_head - _count + i + _buffer.Length * 2) % _buffer.Length;
                int b = (_head - _count + i + 1 + _buffer.Length * 2) % _buffer.Length;
                var sa = _buffer[a]; var sb = _buffer[b];
                if (target >= sa.serverTime && target <= sb.serverTime)
                {
                    float t = Mathf.InverseLerp(sa.serverTime, sb.serverTime, target);
                    pos = Vector3.Lerp(sa.pos, sb.pos, t);
                    rot = Quaternion.Slerp(sa.rot, sb.rot, t);
                    return true;
                }
            }
            int last = (_head - 1 + _buffer.Length) % _buffer.Length;
            // экстраполяция на короткий срок (анти-рывок при лагах)
            float dt = Mathf.Clamp(target - _buffer[last].serverTime, 0f, 0.15f);
            pos = _buffer[last].pos + _buffer[last].velocity * dt;
            rot = _buffer[last].rot;
            return true;
        }
    }

    /// <summary>
    /// Interest management: сетка 32×32 м. Каждый игрок получает сущности из своих и
    /// соседних ячеек + «важные» (двери своего билда, монстры ближе 40 м). Это то, что
    /// позволяет держать 100+ игроков: O(игроки × ближние сущности), а не O(N²).
    /// </summary>
    public static class InterestGrid
    {
        public const float CellSize = 32f;
        static readonly Dictionary<long, List<NetEntity>> _cells = new Dictionary<long, List<NetEntity>>(1024);
        static readonly Dictionary<NetEntity, long> _cellOf = new Dictionary<NetEntity, long>(4096);

        public static long Key(Vector3 p) => Key(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize));
        public static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

        public static void Register(NetEntity e)
        {
            if (e == null || e.Net == NetId.None) return;
            Unregister(e);
            long k = Key(e.transform.position);
            if (!_cells.TryGetValue(k, out var list)) { list = new List<NetEntity>(32); _cells[k] = list; }
            list.Add(e); _cellOf[e] = k;
        }

        public static void Unregister(NetEntity e)
        {
            if (e == null || !_cellOf.TryGetValue(e, out long k)) return;
            if (_cells.TryGetValue(k, out var list)) list.Remove(e);
            _cellOf.Remove(e);
        }

        public static void Update(NetEntity e)
        {
            if (e == null) return;
            long k = Key(e.transform.position);
            if (!_cellOf.TryGetValue(e, out long old) || old != k) Register(e);
        }

        /// <summary>Сущности в радиусе (использует и репликация, и ИИ, и клиентский фейд).</summary>
        public static void Query(Vector3 pos, float radius, List<NetEntity> result)
        {
            result.Clear();
            int r = Mathf.CeilToInt(radius / CellSize);
            int cx = Mathf.FloorToInt(pos.x / CellSize), cz = Mathf.FloorToInt(pos.z / CellSize);
            float sqr = radius * radius;
            for (int x = cx - r; x <= cx + r; x++)
                for (int z = cz - r; z <= cz + r; z++)
                {
                    if (!_cells.TryGetValue(Key(x, z), out var list)) continue;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var e = list[i];
                        if (e == null) continue;
                        if ((e.transform.position - pos).sqrMagnitude <= sqr) result.Add(e);
                    }
                }
        }
        public static void Clear() { _cells.Clear(); _cellOf.Clear(); }
    }

    /// <summary>База для всего, что реплицируется по сети (игрок, монстр, ящик, дверь, турель).</summary>
    public abstract class NetEntity : MonoBehaviour
    {
        public NetId Net { get; protected set; }
        [Tooltip("Радиус репликации: дальше — обновления реже (LOD сети)")]
        public float cullRadius = 120f;
        [Tooltip("Обновлять только при превышении порога (позиция в метрах)")]
        public float positionThreshold = 0.05f;

        static readonly Dictionary<uint, NetEntity> ByNetId = new Dictionary<uint, NetEntity>(512);

        /// <summary>Поиск сущности по сетевому id — приём снапшотов на клиенте (100+ объектов).</summary>
        public static NetEntity Find(NetId id)
            => id.Value != 0 && ByNetId.TryGetValue(id.Value, out var e) ? e : null;
        public static int RegisteredCount => ByNetId.Count;
        public static void Forget(NetId id) { if (id.Value != 0) ByNetId.Remove(id.Value); }
        public static void ForgetAll() => ByNetId.Clear();

        protected virtual void OnEnable()
        {
            InterestGrid.Register(this);
            if (Net.Value != 0) ByNetId[Net.Value] = this;
        }
        protected virtual void OnDisable()
        {
            InterestGrid.Unregister(this);
            if (Net.Value != 0) ByNetId.Remove(Net.Value);
        }
        public void AssignNetId(NetId id)
        {
            Net = id;
            if (id.Value != 0) ByNetId[id.Value] = this;
            InterestGrid.Register(this);
        }
        public abstract void WriteSnapshot(BufferWriter w);
        public abstract void ReadSnapshot(BufferReader r);
    }

    /// <summary>
    /// Валидация команд (античит на сервере): скорость, частота выстрелов, дистанция,
    /// невозможные состояния. Каждый игрок имеет бюджет «нарушений» — 3 и кик.
    /// </summary>
    public class CommandValidator
    {
        readonly ulong _player;
        float _lastMoveTime;
        Vector3 _lastPos;
        int _violations;
        readonly Dictionary<ClientCommand, float> _lastCmdTime = new Dictionary<ClientCommand, float>(16);

        // лимиты частоты (в секунду)
        static readonly Dictionary<ClientCommand, float> MinInterval = new Dictionary<ClientCommand, float>
        {
            { ClientCommand.FireWeapon, 0.033f }, { ClientCommand.MeleeAttack, 0.2f },
            { ClientCommand.Reload, 0.3f }, { ClientCommand.PlaceBuilding, 0.08f },
            { ClientCommand.Craft, 0.1f }, { ClientCommand.Consume, 0.5f },
            { ClientCommand.QuickMoveItem, 0.03f }, { ClientCommand.Respawn, 1.0f },
            { ClientCommand.PlaceDeployable, 0.2f },
            { ClientCommand.InvSync, 0.15f }, { ClientCommand.LootOpen, 0.2f },
            { ClientCommand.LootTake, 0.05f }, { ClientCommand.LootStore, 0.05f },
            { ClientCommand.LootDrop, 0.12f }, { ClientCommand.LootPickup, 0.1f },
            { ClientCommand.PlayerDied, 5f }, { ClientCommand.PlayerHealth, 0.4f },
            { ClientCommand.HitPlayer, 0.05f }
        };

        public CommandValidator(ulong playerId) { _player = playerId; }

        public bool Validate(ClientCommand cmd, byte[] payload)
        {
            float now = Time.time;
            if (MinInterval.TryGetValue(cmd, out float min))
            {
                if (_lastCmdTime.TryGetValue(cmd, out float last) && now - last < min * 0.9f)
                    return Punish($"rate {cmd} {(now - last):F3}s");
                _lastCmdTime[cmd] = now;
            }
            return true;
        }

        /// <summary>Проверка телепорта/спидхака — вызывать при приёме позиции.</summary>
        public bool ValidateMovement(Vector3 reported, float maxSpeed)
        {
            float now = Time.time;
            float dt = Mathf.Max(0.0001f, now - _lastMoveTime);
            if (_lastMoveTime > 0f)
            {
                float dist = Vector3.Distance(reported, _lastPos);
                float allowed = maxSpeed * dt * 1.35f + 0.5f;   // допуск на лаг/лаг-компенсацию
                if (dist > allowed) return Punish($"speed {dist / dt:F1} m/s");
            }
            _lastMoveTime = now; _lastPos = reported;
            return true;
        }

        bool Punish(string reason)
        {
            _violations++;
            Debug.LogWarning($"[AntiCheat] игрок {_player}: {reason} (нарушение #{_violations})");
            return false;   // сервер просто игнорирует команду; при 3+ — кик (в Mirror-адаптере)
        }
        public int Violations => _violations;
    }
}
