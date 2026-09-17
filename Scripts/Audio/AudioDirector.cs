// ============================================================================
//  SUBSISTENCE — Audio/AudioDirector.cs
//  Менеджер звука: пул позиционных источников, 2D-канал для интерфейса,
//  шаги игрока, «голоса» монстров и фоновый гул каждого уровня.
//  Все клипы — процедурные (Audio/ProcAudio.cs), файлов нет.
// ============================================================================
using UnityEngine;
using Subsistence.Core;
using Subsistence.AI;

namespace Subsistence.Audio
{
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        const int PoolSize = 20;
        AudioSource[] _pool;
        int _next;
        AudioSource _ui;

        void Awake()
        {
            Instance = this;
            ProcAudio.Prebuild();
            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"sfx_{i:00}");
                go.transform.SetParent(transform);
                var a = go.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = 1f;                       // 3D
                a.rolloffMode = AudioRolloffMode.Linear;
                a.minDistance = 2f;
                a.maxDistance = 55f;
                a.dopplerLevel = 0f;
                _pool[i] = a;
            }
            var uiGo = new GameObject("sfx_ui");
            uiGo.transform.SetParent(transform);
            _ui = uiGo.AddComponent<AudioSource>();
            _ui.playOnAwake = false;
            _ui.spatialBlend = 0f;                          // 2D: крафт, интерфейс
        }

        AudioSource Take(Vector3 pos)
        {
            var a = _pool[_next];
            _next = (_next + 1) % PoolSize;
            a.transform.position = pos;
            return a;
        }

        /// <summary>Звук в мире (выстрел, шаг, удар, шёпот).</summary>
        public static void PlayAt(string clip, Vector3 pos, float volume = 1f, float pitch = 1f, float pitchJitter = 0.08f)
        {
            if (Instance == null) return;
            var c = ProcAudio.Get(clip);
            if (c == null) return;
            var a = Instance.Take(pos);
            a.clip = c;
            a.volume = Mathf.Clamp01(volume);
            a.pitch = Mathf.Clamp(pitch + Random.Range(-pitchJitter, pitchJitter), 0.5f, 1.6f);
            a.Play();
        }

        /// <summary>Звук «в голове» игрока (крафт, подбор, интерфейс).</summary>
        public static void Play2D(string clip, float volume = 1f, float pitch = 1f)
        {
            if (Instance == null) return;
            var c = ProcAudio.Get(clip);
            if (c == null) return;
            Instance._ui.pitch = pitch;
            Instance._ui.PlayOneShot(c, Mathf.Clamp01(volume));
        }

        /// <summary>Долгий звук с затуханием — обвал, крик.</summary>
        public static AudioSource PlayLong(string clip, Vector3 pos, float volume = 1f)
        {
            if (Instance == null) return null;
            var c = ProcAudio.Get(clip);
            if (c == null) return null;
            var a = Instance.Take(pos);
            a.clip = c;
            a.volume = volume;
            a.pitch = 1f;
            a.loop = false;
            a.Play();
            return a;
        }
    }

    /// <summary>Шаги игрока: материал — по уровню и воде, темп — по скорости.</summary>
    public class PlayerFootsteps : MonoBehaviour
    {
        public CharacterController controller;
        float _accum;
        float _stepDistance = 0.85f;

        void Update()
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            if (controller == null) return;

            var flat = controller.velocity; flat.y = 0f;
            float speed = flat.magnitude;
            if (!controller.isGrounded || speed < 0.4f) return;

            _accum += speed * Time.deltaTime;
            float need = speed > 5.2f ? _stepDistance * 1.25f : _stepDistance;    // на бегу шаг длиннее
            if (_accum < need) return;
            _accum = 0f;

            string clip = "step_carpet";
            var zone = World.LevelZone.At(transform.position);
            if (zone != null)
            {
                if (zone.theme == LevelTheme.Poolrooms) clip = "step_tile";
                else if (zone.theme == LevelTheme.PowerStation) clip = "step_tile";
            }
            // по воде — мокрый шаг (затопленные залы L37, лужи станции)
            if (World.WorldEnvironment.IsUnderwater(transform.position + Vector3.up * 0.25f)) clip = "step_wet";

            AudioDirector.PlayAt(clip, transform.position, 0.35f, 1f, 0.14f);
        }
    }

    /// <summary>
    /// «Голос» монстра на клиенте: шёпот Шептуна, мокрые шаги Утопленника,
    /// треск Искровика, рык при погоне, крик при смерти.
    /// (Flavor() на сервере рассылает шум для ИИ — здесь именно слышимая часть.)
    /// </summary>
    public class MonsterAudio : MonoBehaviour
    {
        MonsterRuntime _rt;
        float _idleTimer, _stepAccum, _growlTimer;
        Vector3 _last;
        bool _died;

        void Update()
        {
            if (_rt == null)
            {
                _rt = GetComponent<MonsterRuntime>();
                if (_rt == null) return;
                _last = transform.position;
            }

            if (!_rt.IsAlive)
            {
                if (!_died)
                {
                    _died = true;
                    AudioDirector.PlayAt("scream", transform.position, 0.7f, 0.85f);
                }
                return;
            }

            float dt = Time.deltaTime;
            float moved = Vector3.Distance(transform.position, _last);
            _last = transform.position;

            // шаги: по пройденному пути, у Утопленника — мокрые
            _stepAccum += moved;
            float stepLen = _rt.kind == MonsterKind.Drowned ? 1.0f : 1.3f;
            if (_stepAccum >= stepLen)
            {
                _stepAccum = 0f;
                string clip = _rt.kind == MonsterKind.Drowned ? "step_wet" : "step_carpet";
                AudioDirector.PlayAt(clip, transform.position, 0.4f, 0.85f, 0.1f);
            }

            // характерные звуки вида
            _idleTimer -= dt;
            if (_idleTimer <= 0f)
            {
                switch (_rt.kind)
                {
                    case MonsterKind.Whisperer: AudioDirector.PlayAt("whisper", transform.position, 0.55f); _idleTimer = 16f; break;
                    case MonsterKind.Drowned: AudioDirector.PlayAt("water_drip", transform.position, 0.5f); _idleTimer = 6f; break;
                    case MonsterKind.Spark: AudioDirector.PlayAt("crackle", transform.position, 0.45f); _idleTimer = 8f; break;
                    case MonsterKind.Smiler: AudioDirector.PlayAt("crackle", transform.position, 0.18f); _idleTimer = 21f; break;
                    default: _idleTimer = 25f; break;
                }
            }

            // рычание при погоне
            if (_rt.State == MonsterState.Chase)
            {
                _growlTimer -= dt;
                if (_growlTimer <= 0f)
                {
                    AudioDirector.PlayAt("growl", transform.position, 0.6f, _rt.kind == MonsterKind.Spark ? 1.25f : 0.9f);
                    _growlTimer = _rt.kind == MonsterKind.Hound ? 2.6f : 4.2f;
                }
                if (_rt.kind == MonsterKind.Spark)
                    AudioDirector.PlayAt("electric", transform.position, 0.35f, 1.1f, 0.2f);
            }
        }
    }

    /// <summary>Фон уровня: гул ламп (L0), вода и пар (L37), трансформатор (L3).</summary>
    public class LevelAmbience : MonoBehaviour
    {
        AudioSource _a, _b;
        LevelTheme _current = (LevelTheme)255;

        void Start()
        {
            _a = gameObject.AddComponent<AudioSource>();
            _b = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { _a, _b })
            {
                s.loop = true;
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.volume = 0f;
            }
            _a.clip = ProcAudio.Get("hum_lamp");
            _b.clip = ProcAudio.Get("hum_transformer");
            _a.Play(); _b.Play();
        }

        void Update()
        {
            var zone = World.LevelZone.At(transform.position);
            var theme = zone != null ? zone.theme : LevelTheme.Corridors;
            if (theme != _current)
            {
                _current = theme;
                switch (theme)
                {
                    case LevelTheme.Corridors: SetTargets(0.22f, 0.0f); break;   // гудят лампы
                    case LevelTheme.Poolrooms: SetTargets(0.06f, 0.05f); break;  // эхо залов, пар
                    case LevelTheme.PowerStation: SetTargets(0.04f, 0.30f); break; // трансформатор
                }
            }
            _a.volume = Mathf.MoveTowards(_a.volume, _targetA, Time.deltaTime * 0.25f);
            _b.volume = Mathf.MoveTowards(_b.volume, _targetB, Time.deltaTime * 0.25f);
        }

        float _targetA, _targetB;
        void SetTargets(float a, float b) { _targetA = a; _targetB = b; }
    }
}
