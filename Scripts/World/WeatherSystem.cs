// ============================================================================
//  SUBSISTENCE — World/WeatherSystem.cs
//  Решение 18_weather: «погода 2 раза в сутки» (игровые сутки = Balance.DayLengthSeconds).
//
//  Погода в Backrooms — это не солнце и дождь, а аномалии:
//    L0 «Жёлтые коридоры»  → Конденсат (всё плывёт в тумане) / Просадка сети (свет гаснет)
//    L37 «Бассейны»        → Хлорный туман / Прилив (вода поднимается, шум воды)
//    L3 «Электростанция»   → Перегрев реактора / Прорыв пара
//
//  Всё считает сервер (NetworkBridge.IsServer). Офлайн = мы и есть сервер.
//  В Mirror-сборке событие рассылается клиентам RPC-вызовом (см. //NET ниже) —
//  поэтому клиенты никогда не крутят таймер погоды сами.
//
//  События дают не только картинку, но и механику: туман/пар сужают обзор,
//  монстры начинают слышать дальше, а просадка сети и прилив выбрасывают
//  дополнительных монстров и заставляют шуметь (NoiseSystem) — то есть
//  погода это повод сидеть тихо, как и должно быть в этих коридорах.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Subsistence.Core;
using Subsistence.Net;
using Subsistence.Runtime;

namespace Subsistence.World
{
    public enum WeatherKind : byte
    {
        Clear = 0,          // между событиями
        Condensation = 1,   // L0: конденсат
        Blackout = 2,       // L0: просадка сети
        ChlorineFog = 3,    // L37: хлорный туман
        WaterSurge = 4,     // L37: прилив
        HeatSpike = 5,      // L3: перегрев
        SteamLeak = 6       // L3: прорыв пара
    }

    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }

        [Header("Расписание (18_weather: дважды за игровые сутки)")]
        public int seed = 1337;
        public int eventsPerDay = 2;
        public float minDuration = 110f;
        public float maxDuration = 240f;
        public float warnBefore = 20f;         // предупреждение в HUD за N секунд
        public bool startImmediately = false;  // для отладки: событие сразу

        [Header("Сила")]
        public float fogDensityMin = 0.02f;    // туман во время аномалии
        public float fogDensityMax = 0.075f;
        public float ambientDim = 0.45f;       // во сколько раз тускнеет ambient
        public int extraMonsters = 3;          // сколько монстров «выбрасывает» событие

        /// <summary>Строка для терминала загрузки (ставит RuntimeBootstrap).</summary>
        public System.Action<string> logLine;

        // --- состояние ---
        public WeatherKind Current { get; private set; } = WeatherKind.Clear;
        public bool IsActive => Current != WeatherKind.Clear;
        public float TimeLeft { get; private set; }
        public float NextEventIn => Mathf.Max(0f, _nextEventTime - Time.time);

        /// <summary>Множитель скорости игрока во время аномалии (читает PlayerController).</summary>
        public static float SpeedMultiplier { get; private set; } = 1f;
        /// <summary>0 = «ничего не видно», 1 = чисто. Для UI/ИИ (обзор монстров).</summary>
        public static float Visibility01 { get; private set; } = 1f;

        float _dayTimer;
        float _nextEventTime;
        float _eventEndTime;
        float _pulseTimer;
        float _savedFogDensity;
        Color _savedFogColor;
        float _savedAmbient;
        bool _visualsSaved;

        System.Random _rng;                      // создаётся в Awake с seed (readonly нельзя — поле не в конструкторе)
        Transform[] _water;
        readonly List<Vector3> _waterBase = new List<Vector3>(32);

        void Awake()
        {
            Instance = this;
            _rng = new System.Random(seed);
            _dayTimer = startImmediately ? 0f : Random.Range(0.15f, 0.5f) * Balance.DayLengthSeconds;
            ScheduleNext();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            RestoreVisuals();
            SpeedMultiplier = 1f;
            Visibility01 = 1f;
        }

        // ================== РАСПИСАНИЕ ==================

        void ScheduleNext()
        {
            // два события за сутки: например +0.28 и +0.74 суток от старта таймера
            float slot = Balance.DayLengthSeconds / Mathf.Max(1, eventsPerDay);
            _nextEventTime = Time.time + slot * Random.Range(0.65f, 1.25f);
        }

        void Update()
        {
            if (!NetworkBridge.IsServer) return;    // погоду крутит только сервер

            if (IsActive)
            {
                TimeLeft -= Time.deltaTime;
                TickEffects();
                if (TimeLeft <= 0f) EndEvent();
                return;
            }

            // предупреждение и старт
            if (NextEventIn <= warnBefore && !_warned)
            {
                _warned = true;
                Announce($"<color=#ffd76a>[ПОГОДА]</color> датчики фиксируют аномалию: {NameOf(PickKindFor(CurrentLevelTheme()))} через {(int)NextEventIn} с");
            }
            if (Time.time >= _nextEventTime) StartEvent(PickKindFor(CurrentLevelTheme()));
        }

        bool _warned;

        /// <summary>Принудительный запуск (админ/отладка).</summary>
        public void Force(WeatherKind kind, float duration = 0f)
        {
            StartEvent(kind, duration);
        }

        // ---- для сохранения мира (SaveSystem) ----
        public WeatherKind CurrentKind => Current;
        public float RemainingSeconds => IsActive ? Mathf.Max(0f, TimeLeft) : 0f;

        /// <summary>Восстановить погоду из сохранения: то же событие и тот же остаток времени.</summary>
        public void Restore(string kindName, float left)
        {
            if (left <= 0f || string.IsNullOrEmpty(kindName)) return;
            if (!System.Enum.TryParse(kindName, out WeatherKind kind)) return;
            StartEvent(kind, left);
            Debug.Log($"[Weather] восстановлено событие {kind} на {left:0} с");
        }

        LevelTheme CurrentLevelTheme()
        {
            var boot = RuntimeBootstrap.Instance;
            if (boot != null && boot.Player != null)
            {
                // уровень определяем по высоте: 0 → L0, −40 → L37, −80 → L3
                float y = boot.Player.transform.position.y;
                if (y < -60f) return LevelTheme.PowerStation;
                if (y < -20f) return LevelTheme.Poolrooms;
            }
            return LevelTheme.Corridors;
        }

        WeatherKind PickKindFor(LevelTheme theme)
        {
            switch (theme)
            {
                case LevelTheme.Poolrooms:
                    return _rng.Next(0, 100) < 60 ? WeatherKind.ChlorineFog : WeatherKind.WaterSurge;
                case LevelTheme.PowerStation:
                    return _rng.Next(0, 100) < 55 ? WeatherKind.SteamLeak : WeatherKind.HeatSpike;
                default:
                    return _rng.Next(0, 100) < 55 ? WeatherKind.Condensation : WeatherKind.Blackout;
            }
        }

        // ================== СОБЫТИЕ ==================

        void StartEvent(WeatherKind kind, float duration = 0f)
        {
            _warned = false;
            SaveVisuals();

            Current = kind;
            TimeLeft = duration > 0f ? duration : Random.Range(minDuration, maxDuration);
            _eventEndTime = Time.time + TimeLeft;
            _pulseTimer = 0f;

            ApplyVisuals(kind);
            ApplyGameplay(kind);

            Announce($"<color=#ff9a52>[ПОГОДА]</color> {NameOf(kind)} — {Describe(kind)} (~{(int)TimeLeft} с)");
            logLine?.Invoke($"weather event: {kind} ({LevelName(CurrentLevelTheme())}) duration={(int)TimeLeft}s");
            AnnounceMonsters(kind);
        }

        void EndEvent()
        {
            var was = Current;
            Current = WeatherKind.Clear;
            TimeLeft = 0f;
            RestoreVisuals();
            SpeedMultiplier = 1f;
            Visibility01 = 1f;
            ScheduleNext();
            _dayTimer += Balance.DayLengthSeconds / Mathf.Max(1, eventsPerDay);

            Announce($"<color=#8ef0b4>[ПОГОДА]</color> {NameOf(was)} закончилась — датчики в норме");
            logLine?.Invoke($"weather event ended: {was}, next in {(int)NextEventIn}s");
        }

        // ================== ЭФФЕКТЫ ==================

        void SaveVisuals()
        {
            if (_visualsSaved) return;
            _savedFogDensity = RenderSettings.fogDensity;
            _savedFogColor = RenderSettings.fogColor;
            _savedAmbient = RenderSettings.ambientIntensity;
            _visualsSaved = true;
        }

        void RestoreVisuals()
        {
            if (!_visualsSaved) return;
            RenderSettings.fogDensity = _savedFogDensity;
            RenderSettings.fogColor = _savedFogColor;
            RenderSettings.ambientIntensity = _savedAmbient;
        }

        void ApplyVisuals(WeatherKind kind)
        {
            float density = _savedFogDensity;
            Color fogColor = _savedFogColor;
            float ambient = _savedAmbient;

            switch (kind)
            {
                case WeatherKind.Condensation:
                    density = fogDensityMax; fogColor = new Color(0.55f, 0.52f, 0.34f); ambient = _savedAmbient * ambientDim; break;
                case WeatherKind.Blackout:
                    ambient = _savedAmbient * (ambientDim * 0.4f); fogColor = new Color(0.06f, 0.06f, 0.05f); density = fogDensityMin; break;
                case WeatherKind.ChlorineFog:
                    density = fogDensityMax * 0.85f; fogColor = new Color(0.62f, 0.72f, 0.68f); ambient = _savedAmbient * 0.7f; break;
                case WeatherKind.WaterSurge:
                    density = fogDensityMin * 1.4f; fogColor = new Color(0.42f, 0.52f, 0.5f); break;
                case WeatherKind.HeatSpike:
                    ambient = _savedAmbient * 0.75f; fogColor = new Color(0.52f, 0.30f, 0.20f); density = fogDensityMin * 1.2f; break;
                case WeatherKind.SteamLeak:
                    density = fogDensityMax; fogColor = new Color(0.78f, 0.78f, 0.76f); ambient = _savedAmbient * 0.6f; break;
            }

            RenderSettings.fog = true;
            RenderSettings.fogDensity = density;
            RenderSettings.fogColor = fogColor;
            RenderSettings.ambientIntensity = ambient;

            Visibility01 = Mathf.Clamp01(1f - Mathf.InverseLerp(fogDensityMin, fogDensityMax, density) * 0.75f);
            if (kind == WeatherKind.Blackout) Visibility01 = Mathf.Min(Visibility01, 0.35f);
        }

        void ApplyGameplay(WeatherKind kind)
        {
            SpeedMultiplier = 1f;
            switch (kind)
            {
                // скользкий ковролин/плитка + слепота: медленнее и шумнее
                case WeatherKind.Condensation: SpeedMultiplier = 0.95f; break;
                case WeatherKind.ChlorineFog: SpeedMultiplier = 0.98f; break;
                case WeatherKind.WaterSurge: SpeedMultiplier = 0.85f; RaiseWater(true); break;
                case WeatherKind.SteamLeak: SpeedMultiplier = 0.9f; break;
                case WeatherKind.Blackout: SpeedMultiplier = 1f; break;
                case WeatherKind.HeatSpike: SpeedMultiplier = 0.93f; break;
            }
        }

        void TickEffects()
        {
            // «пульс» аномалии: шум и подсветка каждые несколько секунд
            _pulseTimer -= Time.deltaTime;
            if (_pulseTimer > 0f) return;

            switch (Current)
            {
                case WeatherKind.Blackout:
                    _pulseTimer = 6f;
                    PulseNoise(28f, "где-то щёлкает автомат");
                    break;
                case WeatherKind.WaterSurge:
                    _pulseTimer = 4f;
                    PulseNoise(34f, "вода прибывает");
                    break;
                case WeatherKind.SteamLeak:
                    _pulseTimer = 3f;
                    PulseNoise(40f, "свист пара");
                    break;
                case WeatherKind.HeatSpike:
                    _pulseTimer = 8f;
                    PulseNoise(22f, "гудит реактор");
                    break;
                case WeatherKind.Condensation:
                    _pulseTimer = 9f;
                    PulseNoise(16f, "капли по потолку");
                    break;
                case WeatherKind.ChlorineFog:
                    _pulseTimer = 7f;
                    PulseNoise(20f, "хлор ползёт по плитке");
                    break;
            }
        }

        void PulseNoise(float radius, string hint)
        {
            var player = RuntimeBootstrap.Instance != null ? RuntimeBootstrap.Instance.Player : null;
            if (player == null) return;
            // шум «ниоткуда»: монстры идут на источник, а не на игрока — самое неприятное
            var pos = player.transform.position + Random.insideUnitSphere * 18f;
            pos.y = player.transform.position.y;
            AI.NoiseSystem.Emit(pos, radius, AI.NoiseType.Door);
            if (Random.value < 0.25f) Subsistence.UI.HudRuntime.ShowToast(hint, 2f);
        }

        void AnnounceMonsters(WeatherKind kind)
        {
            if (extraMonsters <= 0) return;
            var spawner = AI.MonsterSpawner.Instance;
            if (spawner == null || spawner.spawnPoints == null || spawner.spawnPoints.Length == 0) return;

            var theme = CurrentLevelTheme();
            AI.MonsterKind kindToSpawn = kind == WeatherKind.Blackout || kind == WeatherKind.Condensation ? AI.MonsterKind.Smiler
                                       : kind == WeatherKind.WaterSurge || kind == WeatherKind.ChlorineFog ? AI.MonsterKind.Hound
                                       : AI.MonsterKind.SkinStealer;

            int spawned = 0;
            for (int i = 0; i < spawner.spawnPoints.Length && spawned < extraMonsters; i++)
            {
                var pt = spawner.spawnPoints[Random.Range(0, spawner.spawnPoints.Length)];
                if (pt == null) continue;
                if (AI.SafeRoom.IsSafe(pt.position)) continue;      // 39_trading: в убежище не лезем
                spawner.Spawn(kindToSpawn, pt.position);
                spawned++;
            }
            if (spawned > 0) logLine?.Invoke($"weather spawned {spawned} x {kindToSpawn} on {LevelName(theme)}");
        }

        // ================== ВОДА (прилив) ==================

        void CacheWater()
        {
            if (_water != null) return;
            var all = Object.FindObjectsOfType<Transform>();
            var list = new List<Transform>(32);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].gameObject.layer == Layers.Water) list.Add(all[i]);

            _water = list.ToArray();
            _waterBase.Clear();
            for (int i = 0; i < _water.Length; i++) _waterBase.Add(_water[i].position);
        }

        void RaiseWater(bool up)
        {
            CacheWater();
            for (int i = 0; i < _water.Length; i++)
            {
                if (_water[i] == null) continue;
                var p = _water[i].position;
                _water[i].position = new Vector3(p.x, _waterBase[i].y + (up ? 0.35f : 0f), p.z);
            }
            if (_water.Length > 0 && up) Subsistence.UI.HudRuntime.ShowToast($"Прилив: вода поднялась на 35 см ({_water.Length} объёмов)", 4f);
        }

        // ================== ТЕКСТЫ ==================

        public static string NameOf(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Condensation: return "КОНДЕНСАТ";
                case WeatherKind.Blackout: return "ПРОСАДКА СЕТИ";
                case WeatherKind.ChlorineFog: return "ХЛОРНЫЙ ТУМАН";
                case WeatherKind.WaterSurge: return "ПРИЛИВ";
                case WeatherKind.HeatSpike: return "ПЕРЕГРЕВ РЕАКТОРА";
                case WeatherKind.SteamLeak: return "ПРОРЫВ ПАРА";
                default: return "ЯСНО";
            }
        }

        static string Describe(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Condensation: return "всё в испарине, дальше 15 метров ничего не видно";
                case WeatherKind.Blackout: return "лампы гаснут по секциям, твари идут на любой звук";
                case WeatherKind.ChlorineFog: return "едкий туман над водой, дышать тяжело";
                case WeatherKind.WaterSurge: return "вода прибывает, плитка скользит";
                case WeatherKind.HeatSpike: return "турбины на пределе, воздух горячий";
                case WeatherKind.SteamLeak: return "магистраль свистит, видимость нулевая";
                default: return "тишина";
            }
        }

        static string LevelName(LevelTheme theme)
        {
            switch (theme)
            {
                case LevelTheme.Poolrooms: return "Level 37";
                case LevelTheme.PowerStation: return "Level 3";
                default: return "Level 0";
            }
        }

        static void Announce(string msg)
        {
            Subsistence.UI.HudRuntime.ShowToast(msg, 5f);
        }
    }
}
