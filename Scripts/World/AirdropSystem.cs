// ============================================================================
//  SUBSISTENCE — World/AirdropSystem.cs
//  Решение 19_airdrops: «аирдропы есть; в бассейнах — приманка».
//
//  Как это работает в world:
//    • раз в 15–28 минут «борт» сбрасывает ящик (LootTables → loot.airdrop, Tier3);
//    • за 30 секунд до сброса приходит предупреждение — в HUD и в терминал;
//    • ящик падает на парашюте (медленно, с мигающим маяком) и шумит, так что
//      на него идут все, у кого есть уши: и игроки, и монстры;
//    • в Level 37 (бассейны) примерно каждый второй сброс — ПРИМАНКА: ящик
//      приземляется тихо, а потом начинает «кричать» (NoiseSystem.Scream),
//      стягивая волну монстров. Лут в нём настоящий — ровно до того момента,
//      как ты решишь, что успел.
//
//  Всё считает сервер (NetworkBridge.IsServer); в Mirror-сборке запуск и
//  приземление рассылаются клиентам RPC (//NET).
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;
using Subsistence.Net;
using Subsistence.AI;
using Subsistence.Runtime;

namespace Subsistence.World
{
    public class AirdropSystem : MonoBehaviour
    {
        public static AirdropSystem Instance { get; private set; }

        [Header("Расписание")]
        public int seed = 1337;
        public float minInterval = 900f;      // 15 минут
        public float maxInterval = 1680f;     // 28 минут
        public float firstDropIn = 180f;      // первый сброс — через 3 минуты после старта
        public float warnLead = 30f;

        [Header("Приманка (Level 37)")]
        public float baitChance = 0.5f;       // доля приманок среди сбросов в бассейнах
        public float baitScreamRadius = 70f;
        public int baitMonsters = 4;

        /// <summary>Секунд до следующего сброса (для HUD).</summary>
        public float NextDropIn => Mathf.Max(0f, _nextDropTime - Time.time);

        [Header("Полёт")]
        public float dropHeight = 42f;
        public float fallSpeed = 7.5f;

        /// <summary>Строка в терминал загрузки (ставит RuntimeBootstrap).</summary>
        public System.Action<string> logLine;

        float _nextDropTime;
        bool _warned;

        void Awake()
        {
            Instance = this;
            _nextDropTime = Time.time + firstDropIn;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (!NetworkBridge.IsServer) return;

            float left = _nextDropTime - Time.time;
            if (!_warned && left <= warnLead && left > 0f)
            {
                _warned = true;
                Announce($"<color=#ffd76a>[АИРДРОП]</color> борт на подходе, сброс через {(int)left} с — иди на шум");
                logLine?.Invoke($"airdrop inbound in {(int)left}s");
            }
            if (left <= 0f)
            {
                _warned = false;
                Launch();
                ScheduleNext();
            }
        }

        void ScheduleNext()
        {
            var rng = new System.Random(seed ^ Mathf.RoundToInt(Time.time));
            _nextDropTime = Time.time + Mathf.Lerp(minInterval, maxInterval, (float)rng.NextDouble());
        }

        /// <summary>Запустить сброс вручную (админ/отладка).</summary>
        public void Launch(bool bait = false, LevelTheme? forceLevel = null)
        {
            var level = forceLevel.HasValue ? FindLevel(forceLevel.Value) : PickLevel();
            if (level == null)
            {
                logLine?.Invoke("airdrop: нет сгенерированных уровней, сброс отменён");
                return;
            }

            Vector3 point;
            if (!PickLandingPoint(level, out point))
            {
                logLine?.Invoke("airdrop: не нашёл свободную площадку, сброс отменён");
                return;
            }

            bool isBait = bait || (level.theme == LevelTheme.Poolrooms && Random.value < baitChance);

            var go = new GameObject(isBait ? "Airdrop_BAIT" : "Airdrop");
            go.transform.position = new Vector3(point.x, point.y + dropHeight, point.z);

            // --- визуал: ящик + парашют + мигающий маяк ---
            var crate = ModelLibrary.AttachFitted(ModelLibrary.Names.AirdropCrate, go.transform, 0.95f, Random.Range(0f, 360f));
            if (crate == null) MakePrimitiveCrate(go.transform, isBait);
            else ModelLibrary.Tint(crate, isBait ? new Color(0.62f, 0.28f, 0.26f) : new Color(0.72f, 0.60f, 0.34f));

            var chute = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            chute.name = "Parachute";
            chute.transform.SetParent(go.transform, false);
            chute.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            chute.transform.localScale = new Vector3(2.5f, 0.03f, 2.5f);
            Object.Destroy(chute.GetComponent<Collider>());

            var beacon = new GameObject("Beacon");
            beacon.transform.SetParent(go.transform, false);
            beacon.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            var beaconLight = beacon.AddComponent<Light>();
            beaconLight.type = LightType.Point;
            beaconLight.color = isBait ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.85f, 0.35f);
            beaconLight.range = 26f;
            beaconLight.intensity = 2.4f;

            var land = go.AddComponent<AirdropCrate>();
            land.bait = isBait;
            land.tableId = "loot.airdrop";
            land.theme = level.theme;
            land.fallSpeed = fallSpeed;
            land.screamRadius = baitScreamRadius;
            land.monsters = baitMonsters;
            land.logLine = logLine;

            Announce(isBait
                ? "<color=#ff6b5e>[АИРДРОП]</color> ящик ушёл вниз… и почему-то очень тихо"
                : "<color=#ffd76a>[АИРДРОП]</color> ящик пошёл вниз — смотри на маяк");
            logLine?.Invoke($"airdrop launched: {(isBait ? "BAIT" : "loot")} on {LevelName(level.theme)} at {point}");
        }

        // ================== ВЫБОР МЕСТА ==================

        GeneratedLevel PickLevel()
        {
            var boot = RuntimeBootstrap.Instance;
            if (boot == null || boot.Generator == null) return null;
            var levels = boot.Generator.Generated;
            if (levels == null || levels.Count == 0) return null;

            // бассейны и станция интереснее: там лут тира 2–3
            int roll = Random.Range(0, 100);
            int index = roll < 45 ? 0 : roll < 80 ? 1 : 2;
            if (index >= levels.Count) index = Random.Range(0, levels.Count);
            return levels[index];
        }

        GeneratedLevel FindLevel(LevelTheme theme)
        {
            var boot = RuntimeBootstrap.Instance;
            if (boot == null || boot.Generator == null) return null;
            var levels = boot.Generator.Generated;
            for (int i = 0; i < levels.Count; i++) if (levels[i].theme == theme) return levels[i];
            return null;
        }

        bool PickLandingPoint(GeneratedLevel level, out Vector3 point)
        {
            point = Vector3.zero;
            var nodes = level.lootNodes;
            if (nodes == null || nodes.Count == 0) return false;

            for (int attempt = 0; attempt < 24; attempt++)
            {
                var candidate = nodes[Random.Range(0, nodes.Count)];
                if (SafeRoom.IsSafe(candidate)) continue;       // 39_trading: в убежище не падает

                RaycastHit hit;
                var from = candidate + Vector3.up * (dropHeight + 5f);
                if (!Physics.Raycast(from, Vector3.down, out hit, dropHeight + 40f,
                                     Layers.Mask(Layers.LevelGeometry, Layers.Buildable), QueryTriggerInteraction.Ignore))
                    continue;

                point = hit.point + Vector3.up * 0.25f;
                return true;
            }
            return false;
        }

        static void MakePrimitiveCrate(Transform parent, bool bait)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Crate";
            box.transform.SetParent(parent, false);
            box.transform.localScale = new Vector3(0.9f, 0.75f, 0.9f);
            box.transform.localPosition = new Vector3(0f, 0.38f, 0f);
            var r = box.GetComponent<Renderer>();
            if (r != null) r.material.color = bait ? new Color(0.62f, 0.28f, 0.26f) : new Color(0.72f, 0.60f, 0.34f);
            Object.Destroy(box.GetComponent<Collider>());
        }

        static void Announce(string msg) => Subsistence.UI.HudRuntime.ShowToast(msg, 5f);

        static string LevelName(LevelTheme theme)
        {
            switch (theme)
            {
                case LevelTheme.Poolrooms: return "Level 37";
                case LevelTheme.PowerStation: return "Level 3";
                default: return "Level 0";
            }
        }
    }

    /// <summary>Падающий ящик: парашют, маяк, приземление → лут (и, если приманка, — волна монстров).</summary>
    public class AirdropCrate : MonoBehaviour
    {
        public bool bait;
        public string tableId = "loot.airdrop";
        public LevelTheme theme = LevelTheme.Corridors;
        public float fallSpeed = 7.5f;
        public float screamRadius = 70f;
        public int monsters = 4;
        public System.Action<string> logLine;

        bool _landed;
        float _beaconTimer;

        void Update()
        {
            if (_landed) return;

            // мигаем маяком, пока летим
            _beaconTimer += Time.deltaTime;
            var light = GetComponentInChildren<Light>();
            if (light != null) light.intensity = 1.6f + Mathf.Sin(_beaconTimer * 9f) * 1.1f;

            transform.position += Vector3.down * fallSpeed * Time.deltaTime;

            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out hit, 1.2f,
                                 Layers.Mask(Layers.LevelGeometry, Layers.Buildable), QueryTriggerInteraction.Ignore))
                Land(hit.point);
        }

        void Land(Vector3 ground)
        {
            _landed = true;
            transform.position = ground + Vector3.up * 0.05f;

            // парашют больше не нужен
            var chute = transform.Find("Parachute");
            if (chute != null) Destroy(chute.gameObject);

            var spawner = LootSpawner.Instance;
            if (spawner != null)
            {
                var container = spawner.SpawnContainer(ground + Vector3.up * 0.05f, tableId, theme);
                if (container != null)
                {
                    container.kind = LootContainerKind.Airdrop;   // «птица», как в Rust
                    container.tier = LootTier.Tier3;
                    logLine?.Invoke($"airdrop landed on {theme} at {ground}");
                }
            }

            // шум приземления слышно далеко
            NoiseSystem.Emit(ground, bait ? 45f : 95f, NoiseType.Explosion);
            Subsistence.UI.HudRuntime.ShowToast(bait
                ? "<color=#ff6b5e>Ящик сел. Слишком тихо. Слишком вкусно пахнет.</color>"
                : "<color=#ffd76a>Аирдроп на земле — маяк ещё горит</color>", 5f);

            if (bait) StartCoroutine(BaitRoutine(ground));
            else Destroy(gameObject, 6f);      // маяк гаснет, ящик-контейнер остаётся
        }

        /// <summary>Приманка: ящик «кричит» и стягивает монстров на конкретную точку.</summary>
        IEnumerator BaitRoutine(Vector3 point)
        {
            var kinds = theme == LevelTheme.Poolrooms
                ? new[] { MonsterKind.Hound, MonsterKind.Partygoer }
                : new[] { MonsterKind.Smiler, MonsterKind.SkinStealer };

            int spawned = 0;
            for (int tick = 0; tick < 18; tick++)      // ~36 секунд шума
            {
                NoiseSystem.Emit(point, screamRadius, NoiseType.Scream);
                if (tick % 4 == 1 && spawned < monsters)
                {
                    var spawner = MonsterSpawner.Instance;
                    if (spawner != null)
                    {
                        var offset = Random.insideUnitSphere * 14f;
                        offset.y = 0f;
                        var kind = kinds[Random.Range(0, kinds.Length)];
                        spawner.Spawn(kind, point + offset);
                        spawned++;
                        logLine?.Invoke($"bait attracted {kind} ({spawned}/{monsters})");
                    }
                }
                if (tick == 0) Subsistence.UI.HudRuntime.ShowToast("<color=#ff6b5e>Что-то идёт на ящик. Много.</color>", 4f);
                yield return new WaitForSeconds(2f);
            }
            Destroy(gameObject, 1f);
        }
    }
}
