// ============================================================================
//  SUBSISTENCE — World/Atmosphere.cs
//  Своя палитра на каждый уровень: до этого амбиент и туман были ОДНИ на всю игру,
//  поэтому коридоры, бассейны и станция выглядели одинаково серыми.
//  Теперь при переходе игрока между уровнями атмосфера меняется целиком:
//    L0 «Жёлтые коридоры» — тёплая пыль, желтоватый туман, гудящие лампы;
//    L37 «Бассейны»       — бирюзовая вода, плотный пар, холодный свет;
//    L3  «Электростанция» — холодный серо-зелёный, густая гарь, жёсткие тени.
//  Плюс у каждого уровня своя плотность тумана и яркость солнца (у станции — темнее).
// ============================================================================
using UnityEngine;
using Subsistence.Core;

namespace Subsistence.World
{
    /// <summary>Зона уровня: тема + габариты. Ставится генератором на корень уровня.</summary>
    public class LevelZone : MonoBehaviour
    {
        public LevelTheme theme = LevelTheme.Corridors;
        public Vector3 halfExtents = new Vector3(84f, 12f, 84f);

        static readonly System.Collections.Generic.List<LevelZone> _all
            = new System.Collections.Generic.List<LevelZone>(4);
        public static System.Collections.Generic.IReadOnlyList<LevelZone> All => _all;

        void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        void OnDisable() { _all.Remove(this); }

        /// <summary>В каком уровне находится точка (или null, если вне уровней — лифты/меню).</summary>
        public static LevelZone At(Vector3 pos)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                var z = _all[i];
                if (z == null) continue;
                var d = pos - z.transform.position;
                if (Mathf.Abs(d.x) <= z.halfExtents.x && Mathf.Abs(d.z) <= z.halfExtents.z
                    && Mathf.Abs(d.y) <= z.halfExtents.y) return z;
            }
            return null;
        }
    }

    /// <summary>Палитра уровня: всё, что видит игрок — свет, туман, амбиент.</summary>
    public struct AtmosphereDef
    {
        public Color ambientSky, ambientEquator, ambientGround;
        public Color fogColor;
        public float fogDensity;
        public Color sunColor;
        public float sunIntensity;
        public string note;
    }

    public class LevelAtmosphere : MonoBehaviour
    {
        public static LevelAtmosphere Instance { get; private set; }

        public static readonly System.Collections.Generic.Dictionary<LevelTheme, AtmosphereDef> Palette
            = new System.Collections.Generic.Dictionary<LevelTheme, AtmosphereDef>
        {
            // L0 — жёлтые коридоры: пыль, тепло, лёгкая дымка
            { LevelTheme.Corridors, new AtmosphereDef {
                ambientSky = new Color(0.46f, 0.42f, 0.25f), ambientEquator = new Color(0.34f, 0.31f, 0.18f),
                ambientGround = new Color(0.16f, 0.14f, 0.09f), fogColor = new Color(0.42f, 0.38f, 0.23f),
                fogDensity = 0.014f, sunColor = new Color(1.00f, 0.94f, 0.74f), sunIntensity = 1.05f,
                note = "тёплая пыль, жёлтая дымка" } },
            // L37 — бассейны: бирюза, пар над водой, холодный свет
            { LevelTheme.Poolrooms, new AtmosphereDef {
                ambientSky = new Color(0.28f, 0.44f, 0.46f), ambientEquator = new Color(0.20f, 0.36f, 0.40f),
                ambientGround = new Color(0.08f, 0.18f, 0.22f), fogColor = new Color(0.22f, 0.42f, 0.45f),
                fogDensity = 0.024f, sunColor = new Color(0.78f, 0.94f, 1.00f), sunIntensity = 0.85f,
                note = "бирюза, пар, холодный верхний свет" } },
            // L3 — электростанция: холод, гарь, жёсткий свет
            { LevelTheme.PowerStation, new AtmosphereDef {
                ambientSky = new Color(0.26f, 0.30f, 0.28f), ambientEquator = new Color(0.18f, 0.23f, 0.21f),
                ambientGround = new Color(0.08f, 0.11f, 0.10f), fogColor = new Color(0.18f, 0.24f, 0.21f),
                fogDensity = 0.032f, sunColor = new Color(0.84f, 0.90f, 0.86f), sunIntensity = 0.72f,
                note = "гарь, холодный серо-зелёный, темно" } },
        };

        Light _sun;
        Subsistence.Runtime.RuntimeBootstrap _boot;
        LevelTheme _current = (LevelTheme)255;
        float _switchFade;                 // плавный переход, чтобы скачок не бил по глазам
        AtmosphereDef _from, _to;
        float _fadeT = 1f;

        void Awake()
        {
            Instance = this;
            // Солнце уровня: своя палитра света для коридоров / бассейнов / станции.
            // Свет создаём сами — до этого в сцене вообще не было направленного источника,
            // поэтому «палитра солнца» была не на что вешать.
            if (_sun == null)
            {
                var go = new GameObject("LevelSun");
                go.transform.SetParent(transform.parent);
                go.transform.rotation = Quaternion.Euler(52f, -34f, 0f);   // мягкий верхний свет
                _sun = go.AddComponent<Light>();
                _sun.type = LightType.Directional;
                _sun.shadows = LightShadows.Soft;
                _sun.shadowStrength = 0.75f;
            }
        }

        /// <summary>Солнце сцены: генератор создаёт один directional light на все уровни.</summary>
        public void Bind(Light sun) { _sun = sun; Apply(_current == (LevelTheme)255 ? LevelTheme.Corridors : _current, instant: true); }

        void Update()
        {
            if (_boot == null) _boot = FindObjectOfType<Subsistence.Runtime.RuntimeBootstrap>();
            var player = _boot != null ? _boot.Player : null;
            if (player == null) return;
            var zone = LevelZone.At(player.transform.position);
            if (zone == null) return;
            if (zone.theme != _current) Apply(zone.theme, instant: false);
            TickFade();
        }

        void Apply(LevelTheme theme, bool instant)
        {
            if (!Palette.TryGetValue(theme, out var def)) return;
            _from = _current != (LevelTheme)255 && Palette.TryGetValue(_current, out var old) ? old : def;
            _to = def;
            _current = theme;
            _fadeT = instant ? 1f : 0f;
            if (instant) { RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight; PushLight(def); PushAmbient(def); }
        }

        void TickFade()
        {
            if (_fadeT >= 1f) return;
            _fadeT = Mathf.Min(1f, _fadeT + Time.deltaTime / 1.6f);   // переход ~1.6 с
            var def = Lerp(_from, _to, Mathf.SmoothStep(0f, 1f, _fadeT));
            PushAmbient(def); PushLight(def);
        }

        static AtmosphereDef Lerp(AtmosphereDef a, AtmosphereDef b, float t)
        {
            return new AtmosphereDef
            {
                ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, t),
                ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, t),
                ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, t),
                fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
                fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t),
                sunColor = Color.Lerp(a.sunColor, b.sunColor, t),
                sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
                note = b.note
            };
        }

        static void PushAmbient(AtmosphereDef d)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = d.ambientSky;
            RenderSettings.ambientEquatorColor = d.ambientEquator;
            RenderSettings.ambientGroundColor = d.ambientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = d.fogColor;
            RenderSettings.fogDensity = d.fogDensity;
        }

        void PushLight(AtmosphereDef d)
        {
            if (_sun == null) return;
            _sun.color = d.sunColor;
            _sun.intensity = d.sunIntensity;
        }

        /// <summary>Название палитры — для HUD/дебага («жёлтые коридоры: тёплая пыль»).</summary>
        public static string DescribeFor(LevelTheme theme)
            => Palette.TryGetValue(theme, out var d) ? d.note : "";
    }
}
