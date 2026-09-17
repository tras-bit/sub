// ============================================================================
//  SUBSISTENCE — Net/NetLoadTest.cs
//  Стенд нагрузки под «сеть 100+»: серверные боты-прокси + холостой прогон
//  рассылки снапшотов. Позволяет измерить цену сервера (AOI-запросы, сборка
//  пакетов, трафик) на 100+ сущностях БЕЗ 100 реальных машин.
//
//  Как запустить (редактор):
//      Play → в консоли меню команда LOADBOTS 112
//  Как запустить (билд):
//      Subsistence.exe -batchmode -nographics --load-bots 112
//
//  Что печатает: онлайн-профиль (AOI/Гц/лимит на пакет), среднее число
//  сущностей в радиусе, байты снапшота, расчётный трафик на игрока и суммарно,
//  худшее время AOI-запроса и время холостого прогона (мс на кадр).
//
//  Честная граница: транспорт не задействован (сокеты не пишутся). Это замер
//  СЕРВЕРНОЙ части — ровно то, что упирается в CPU при 100+ игроках.
// ============================================================================
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Subsistence.Core;

namespace Subsistence.Net
{
    /// <summary>Лёгкий сетевой двойник игрока: только позиция и поворот (для замера нагрузки).</summary>
    public class LoadBot : NetEntity
    {
        public float radius = 40f;
        public float phase;
        public float speed = 4.2f;         // м/с — бег как у игрока, чтобы AOI «дышал», как в бою
        public float yaw;

        Vector3 _center;

        public void SetUp(uint id, Vector3 start, Vector3 center, float r, float ph)
        {
            transform.position = start;
            _center = center;
            radius = r; phase = ph;
            AssignNetId(new NetId(id));
        }

        void Update()
        {
            if (!NetworkBridge.IsServer) return;                 // боты живут только на сервере
            float t = Time.time * speed / Mathf.Max(1f, radius) + phase;
            Vector3 p = _center + new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
            yaw = Mathf.Atan2(p.z - transform.position.z, p.x - transform.position.x) * Mathf.Rad2Deg;
            transform.position = p;
            InterestGrid.Update(this);                            // как у настоящего NetEntity
        }

        public override void WriteSnapshot(BufferWriter w)
        {
            var p = transform.position;
            w.WritePosition(p);                                   // 6 Б: 0.01 м в диапазоне 500 м
            w.WriteUShort((ushort)Mathf.Clamp(Mathf.RoundToInt(yaw * 2f), 0, 65535));   // 2 Б: 0.5°
        }

        public override void ReadSnapshot(BufferReader r)
        {
            Vector3 p = r.ReadPosition();
            yaw = r.ReadUShort() * 0.5f;
            transform.position = p;
        }
    }

    /// <summary>
    /// Серверный стенд нагрузки. BotCount ботов ходят по кругу и попадают в AOI друг друга,
    /// а раз в 1/20 с считается тот же снапшот, что ушёл бы клиенту, и измеряется его размер.
    /// </summary>
    public class NetLoadTest : MonoBehaviour
    {
        [Tooltip("Сколько ботов поднять (цель — 112, мягкий лимит сервера)")]
        public int bots = 112;
        [Tooltip("Частота холостого прогона снапшотов (совпадает с сетевой)")]
        public float snapshotHz = 20f;
        [Tooltip("Размер площадки: мягкий лимит сервера = 128, мир 168×168 м")]
        public Vector2 area = new Vector2(150f, 150f);
        public bool logEvery10s = true;

        readonly List<LoadBot> _list = new List<LoadBot>(160);
        readonly List<NetEntity> _query = new List<NetEntity>(256);
        readonly List<int> _entitiesInAoi = new List<int>(160);
        float _timer, _reportTimer, _worstQueryMs, _reserve;
        long _bytesPerRun;
        int _runs;
        bool _spawned;

        public int BotCount => _list.Count;

        void Update()
        {
            if (!_spawned)
            {
                // Ждём, пока бутстрап сгенерирует мир (иначе боты окажутся в пустоте).
                var boot = Runtime.RuntimeBootstrap.Instance;
                if (boot != null && boot.Generator != null) Spawn();
            }
            if (!_spawned) return;

            _timer += Time.deltaTime;
            float step = 1f / Mathf.Max(1f, snapshotHz);
            if (_timer >= step) { _timer = 0f; DryRun(); }

            if (logEvery10s)
            {
                _reportTimer += Time.deltaTime;
                if (_reportTimer >= 10f) { _reportTimer = 0f; Report(); }
            }
        }

        void Spawn()
        {
            _spawned = true;
            var center = Vector3.zero;
            var boot = Runtime.RuntimeBootstrap.Instance;
            if (boot != null && boot.Player != null) center = boot.Player.SpawnPoint;
            center.y = 0f;

            // Бots: 100+ сущностей всего — это и есть «худший кадр» для сервера.
            for (int i = 0; i < bots; i++)
            {
                var go = new GameObject($"LoadBot_{i:000}");
                go.transform.SetParent(transform, false);
                var b = go.AddComponent<LoadBot>();
                float a = i * 2.399963f;                                   // золотое сечение — равномерно по кругу
                float r = Mathf.Lerp(6f, Mathf.Min(area.x, area.y) * 0.5f, (i % 32) / 31f);
                float x = Mathf.Clamp(Mathf.Cos(a) * r, -area.x * 0.5f, area.x * 0.5f);
                float z = Mathf.Clamp(Mathf.Sin(a) * r, -area.y * 0.5f, area.y * 0.5f);
                b.SetUp(900000u + (uint)i, center + new Vector3(x, 0.1f, z), center + new Vector3(x * 0.25f, 0f, z * 0.25f), r, a);
                _list.Add(b);
            }
            Debug.Log($"[netload] стенд запущен: {_list.Count} ботов, площадка {area.x:F0}×{area.y:F0} м, прогон {snapshotHz:F0} Гц");
        }

        /// <summary>Тот же путь, что и в рассылке: AOI-запрос → сборка пакета. Сокеты не трогаем.</summary>
        void DryRun()
        {
            int online = _list.Count;
            var p = Profile(online);
            var w = BufferWriter.Rent(2048);
            _entitiesInAoi.Clear();

            float t0 = Time.realtimeSinceStartup;
            long bytes = 0;
            float worst = 0f;
            for (int i = 0; i < _list.Count; i++)
            {
                var b = _list[i];
                if (b == null) continue;

                float q0 = Time.realtimeSinceStartup;
                InterestGrid.Query(b.transform.position, p.aoi, _query);
                float q = (Time.realtimeSinceStartup - q0) * 1000f;
                if (q > worst) worst = q;

                w.Reset();
                w.WriteFloat(Time.time);
                w.WriteUShort(0);
                const int countPos = 4;            // тот же формат, что у боевой рассылки
                int count = 0;
                for (int k = 0; k < _query.Count; k++)
                {
                    var e = _query[k];
                    if (e == null || e == b) continue;
                    w.WriteUInt(e.Net.Value);
                    e.WriteSnapshot(w);
                    count++;
                    if (count >= p.perPacket) break;
                }
                w.WriteUShortAt(countPos, (ushort)count);   // пакет стенда повторяет боевой байт в байт
                bytes += w.Length;
                _entitiesInAoi.Add(count);
            }
            BufferWriter.Return(w);

            _bytesPerRun += bytes;
            _runs++;
            _reserve = (Time.realtimeSinceStartup - t0) * 1000f;         // бюджет времени на один прогон
            if (worst > _worstQueryMs) _worstQueryMs = worst;
        }

        MirrorScale Profile(int online)
        {
            int n = Mathf.Max(1, online);
            float k = Mathf.Lerp(1f, 0.45f, Mathf.InverseLerp(Balance.LodPlayerThreshold, Balance.MaxPlayerSlots, n));
            return new MirrorScale
            {
                aoi = Mathf.Clamp(Balance.AoiRadius * k, Balance.AoiRadiusMin, Balance.AoiRadius),
                perPacket = Mathf.Clamp(Balance.MaxEntitiesPerSnapshot - (n - Balance.LodPlayerThreshold), 24, Balance.MaxEntitiesPerSnapshot),
                hz = snapshotHz
            };
        }

        public struct MirrorScale
        {
            public float aoi;       // радиус интереса
            public int perPacket;   // лимит сущностей в пакете
            public float hz;        // частота снапшотов
        }

        void Report()
        {
            var p = Profile(_list.Count);
            float avgEnt = 0f;
            for (int i = 0; i < _entitiesInAoi.Count; i++) avgEnt += _entitiesInAoi[i];
            if (_entitiesInAoi.Count > 0) avgEnt /= _entitiesInAoi.Count;

            float avgBytes = _runs > 0 ? (float)_bytesPerRun / _runs / Mathf.Max(1, _list.Count) : 0f;
            float perPlayerKB = avgBytes * p.hz / 1024f;
            float totalMB = perPlayerKB * _list.Count / 1024f;

            bool kbOk = perPlayerKB <= Balance.NetKbPerPlayerCap;
            bool mbpsOk = totalMB * 8f <= Balance.NetMbpsTotalCap;
            bool timeOk = _reserve <= Balance.NetTickBudgetMs;
            Debug.Log($"[netload] ботов {_list.Count}, AOI {p.aoi:F0} м, {p.hz:F0} Гц, лимит пакета {p.perPacket} | " +
                      $"в среднем в кадре {avgEnt:F1} сущностей, снапшот {avgBytes:F0} Б | " +
                      $"трафик {perPlayerKB:F1} КБ/с на игрока, суммарно {totalMB:F2} МБ/с ({totalMB * 8f:F1} Мбит/с) | " +
                      $"прогон {_reserve:F2} мс, худший AOI {_worstQueryMs:F3} мс");
            Debug.Log($"[netload] приёмка: {(kbOk ? "ОК" : "ПЕРЕБОР")} трафик/игрок {perPlayerKB:F1}/" +
                      $"{Balance.NetKbPerPlayerCap:F0} КБ/с | {(mbpsOk ? "ОК" : "ПЕРЕБОР")} суммарно " +
                      $"{totalMB * 8f:F1}/{Balance.NetMbpsTotalCap:F0} Мбит/с | {(timeOk ? "ОК" : "ПЕРЕБОР")} прогон " +
                      $"{_reserve:F2}/{Balance.NetTickBudgetMs:F1} мс | цель онлайн {Balance.TargetOnline}+");
            if (SnapshotClient.Enabled)
                Debug.Log($"[netload] клиентский разбор снапшотов: {SnapshotClient.Report()}");

            _bytesPerRun = 0; _runs = 0; _worstQueryMs = 0f;
        }

        /// <summary>Итог для теста: можно дёрнуть из консоли (LOADBOTS REPORT).</summary>
        public string Summary()
        {
            var p = Profile(_list.Count);
            float avgEnt = 0f;
            for (int i = 0; i < _entitiesInAoi.Count; i++) avgEnt += _entitiesInAoi[i];
            if (_entitiesInAoi.Count > 0) avgEnt /= _entitiesInAoi.Count;
            float avgBytes = _runs > 0 ? (float)_bytesPerRun / _runs / Mathf.Max(1, _list.Count) : 0f;
            return $"ботов {_list.Count} / AOI {p.aoi:F0} м / {p.hz:F0} Гц / сущностей в кадре {avgEnt:F1} / " +
                   $"снапшот {avgBytes:F0} Б / {avgBytes * p.hz / 1024f:F1} КБ/с на игрока / прогон {_reserve:F2} мс / " +
                   $"приёмка {(avgBytes * p.hz / 1024f <= Balance.NetKbPerPlayerCap ? "ОК" : "ПЕРЕБОР")}";
        }
    }
}
