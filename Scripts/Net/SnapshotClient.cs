// ============================================================================
//  SUBSISTENCE — Net/SnapshotClient.cs
//  Приём серверных снапшотов на клиенте: [время float][count ushort] + count ×
//  ([netId uint][данные сущности]). До этого рассылка писалась, но никем не
//  читалась — теперь у пакета есть потребитель.
//
//   • Сущность ищется в реестре NetEntity.Find(netId) и получает данные в свой
//     ReadSnapshot(): игрок и монстр кладут их в SnapshotInterpolator (плавное
//     движение при 20 Гц), ящик/дверь — в своё состояние.
//   • Свой собственный игрок из снапшотов не правится: им управляет
//     предсказание (см. SpawnNet) — серверный пакет для него только подтверждение.
//   • Ведёт статистику приёма (сущностей/с, пакетов/с, КБ/с) — её печатают
//     сервер раз в 10 с и стенд нагрузки (NetLoadTest, команда LOADBOTS).
//   • Неизвестный netId (гонка спавна) обрывает разбор пакета: длина чужого
//     payload неизвестна, продолжать нельзя. Счётчик UnknownEntities это покажет.
// ============================================================================
using UnityEngine;
using Subsistence.Core;

namespace Subsistence.Net
{
    public static class SnapshotClient
    {
        public static bool Enabled = true;

        static bool _inited;
        static readonly BufferReader _reader = new BufferReader(null);
        static float _window;

        static long _winPackets, _winEntities, _winBytes;

        // ---- накопленные счётчики ----
        public static long PacketsTotal { get; private set; }
        public static long EntitiesTotal { get; private set; }
        public static long BytesTotal { get; private set; }
        public static int LastPackets { get; private set; }
        public static int LastEntities { get; private set; }
        public static float LastServerTime { get; private set; }
        public static int UnknownEntities { get; private set; }

        // ---- за окно 1 с (то, что печатается в лог) ----
        public static float PacketsPerSecond { get; private set; }
        public static float EntitiesPerSecond { get; private set; }
        public static float KbPerSecond { get; private set; }

        public static void Init()
        {
            if (_inited) return;
            _inited = true;
            var go = new GameObject("~SnapshotClient");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<SnapshotClientTick>();
            NetworkBridge.HostChanged += Hook;
            Hook();
        }

        static void Hook()
        {
            var host = NetworkBridge.Host;
            if (host == null) return;
            host.OnRpc -= OnRpc;
            host.OnRpc += OnRpc;
        }

        static void OnRpc(NetId target, string method, byte[] payload)
        {
            if (!Enabled || method != "snapshot") return;
            Apply(payload);
        }

        /// <summary>Разбор одного пакета снапшота. Возвращает число применённых сущностей.</summary>
        public static int Apply(byte[] payload)
        {
            if (payload == null || payload.Length < 6) return 0;
            _reader.Reset(payload);
            LastServerTime = _reader.ReadFloat();
            int count = _reader.ReadUShort();
            int applied = 0;
            for (int i = 0; i < count; i++)
            {
                if (_reader.Remaining < 4) break;
                uint id = _reader.ReadUInt();
                var e = NetEntity.Find(new NetId(id));
                if (e == null)
                {
                    UnknownEntities++;   // сущность ещё не создана у клиента — разбор дальше невозможен
                    break;
                }
                // Свой игрок — предикт (SpawnNet); серверный пакет только подтверждает.
                if (e is Subsistence.Player.PlayerController pc && DeployNet.IsLocal(pc)) continue;
                e.ReadSnapshot(_reader);
                applied++;
            }
            LastPackets = 1;
            LastEntities = applied;
            PacketsTotal++; EntitiesTotal += applied; BytesTotal += payload.Length;
            _winPackets++; _winEntities += applied; _winBytes += payload.Length;
            return applied;
        }

        /// <summary>Скользящее окно 1 с — из него считаются «/с» для логов.</summary>
        internal static void Tick()
        {
            _window += Time.unscaledDeltaTime;
            if (_window < 1f) return;
            PacketsPerSecond = _winPackets / _window;
            EntitiesPerSecond = _winEntities / _window;
            KbPerSecond = _winBytes / 1024f / _window;
            _winPackets = 0; _winEntities = 0; _winBytes = 0; _window = 0f;
        }

        public static void ResetStats()
        {
            PacketsTotal = 0; EntitiesTotal = 0; BytesTotal = 0; UnknownEntities = 0;
            LastPackets = 0; LastEntities = 0;
            PacketsPerSecond = 0; EntitiesPerSecond = 0; KbPerSecond = 0;
            _winPackets = 0; _winEntities = 0; _winBytes = 0; _window = 0f;
        }

        /// <summary>Строка для консоли/оверлея (F3) и для стенда нагрузки.</summary>
        public static string Report()
            => $"снапшоты: {PacketsPerSecond:F0} пак/с, {EntitiesPerSecond:F0} сущ/с, {KbPerSecond:F0} КБ/с, " +
               $"всего {PacketsTotal} пакетов / {EntitiesTotal} сущностей" +
               (UnknownEntities > 0 ? $", неизвестных netId {UnknownEntities}" : "");
    }

    /// <summary>Кадровый тик окна статистики (создаёт SnapshotClient.Init).</summary>
    public class SnapshotClientTick : MonoBehaviour
    {
        void Update() => SnapshotClient.Tick();
    }
}
