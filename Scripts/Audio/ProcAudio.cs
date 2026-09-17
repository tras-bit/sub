// ============================================================================
//  SUBSISTENCE — Audio/ProcAudio.cs
//  Процедурный звук: все клипы генерируются математикой в рантайме (PCM),
//  поэтому в архиве нет ни одного .wav/.ogg — игра весит столько же, а звучит.
//  Это осознанное решение: 100+ МБ сэмплов «ультра-реализма» противоречат
//  компактному билду, а гул ламп, капли, шёпот и треск прекрасно считаются.
//
//  Что есть: гул ламп (L0), трансформатор (L3), капли воды (L37), шаги
//  (ковролин/кафель/вода), шёпот Шептуна, треск Искровика, рык, крик, выстрел,
//  перезарядка, попадания, стройка (постановка/апгрейд/ремонт/снос), подбор
//  предмета, готовый крафт, ток, обвал.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Subsistence.Audio
{
    public static class ProcAudio
    {
        public const int Rate = 22050;              // хватает для ударных/шумов, экономит память

        static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>(32);
        static bool _built;

        /// <summary>Все имена клипов — для предзагрузки и дебага.</summary>
        public static readonly string[] AllNames =
        {
            "hum_lamp", "hum_transformer", "water_drip", "step_carpet", "step_tile", "step_wet",
            "whisper", "crackle", "shot", "reload", "dryfire", "hit_flesh", "hit_metal",
            "build_place", "hammer_up", "repair_ticks", "demolish", "pickup", "craft_done",
            "electric", "growl", "scream", "research_done"
        };

        public static void Prebuild()
        {
            if (_built) return;
            for (int i = 0; i < AllNames.Length; i++) Get(AllNames[i]);
            _built = true;
        }

        public static AudioClip Get(string name)
        {
            if (_clips.TryGetValue(name, out var c) && c != null) return c;
            float[] data = Build(name);
            if (data == null) return null;
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            _clips[name] = clip;
            return clip;
        }

        // ==================== генераторы ====================

        static float[] Build(string name)
        {
            switch (name)
            {
                // --- фон уровней (зацикленные) ---
                case "hum_lamp":        return Hum(100f, 4.0f, 0.055f, 0.006f);       // L0: гудящие лампы
                case "hum_transformer": return Hum(50f, 4.0f, 0.10f, 0.012f, 3);     // L3: трансформатор пониже
                // --- вода (L37) ---
                case "water_drip":      return Drip();
                // --- шаги ---
                case "step_carpet":     return Step(900f, 0.09f, 0.35f);             // глухой ковролин
                case "step_tile":       return Step(2600f, 0.07f, 0.5f);             // звонкий кафель
                case "step_wet":        return WetStep();
                // --- монстры ---
                case "whisper":         return Whisper();
                case "crackle":         return Crackle();
                case "growl":           return Growl();
                case "scream":          return Scream();
                case "electric":        return Electric();
                // --- оружие ---
                case "shot":            return Shot();
                case "reload":          return Reload();
                case "dryfire":         return Click(1800f, 0.05f, 0.5f);
                case "hit_flesh":       return HitFlesh();
                case "hit_metal":       return HitMetal();
                // --- строительство ---
                case "build_place":     return WoodKnock(240f, 0.20f, 0.9f);
                case "hammer_up":       return StoneHit();
                case "repair_ticks":    return RepairTicks();
                case "demolish":        return Demolish();
                // --- интерфейс/предметы ---
                case "pickup":          return Pickup();
                case "craft_done":      return CraftDone();
                case "research_done":   return ResearchDone();
                default: return null;
            }
        }

        // ==================== примитивы ====================

        static float[] Buf(float seconds) => new float[Mathf.Max(16, (int)(seconds * Rate))];

        static float Noise() => Random.value * 2f - 1f;

        /// <summary>Однополюсный ФНЧ — «глухость» материала.</summary>
        static void Lowpass(float[] s, float cutoffHz)
        {
            float rc = 1f / (2f * Mathf.PI * cutoffHz);
            float dt = 1f / Rate;
            float a = dt / (rc + dt);
            float y = 0f;
            for (int i = 0; i < s.Length; i++) { y += a * (s[i] - y); s[i] = y; }
        }

        static void Highpass(float[] s, float cutoffHz)
        {
            float rc = 1f / (2f * Mathf.PI * cutoffHz);
            float dt = 1f / Rate;
            float a = rc / (rc + dt);
            float y = 0f, px = 0f;
            for (int i = 0; i < s.Length; i++) { y = a * (y + s[i] - px); px = s[i]; s[i] = y; }
        }

        static void Normalize(float[] s, float peak = 0.9f)
        {
            float max = 0f;
            for (int i = 0; i < s.Length; i++) max = Mathf.Max(max, Mathf.Abs(s[i]));
            if (max < 1e-5f) return;
            float k = peak / max;
            for (int i = 0; i < s.Length; i++) s[i] *= k;
        }

        /// <summary>Гул: основной тон + обертоны + шум, с медленным биением.</summary>
        static float[] Hum(float baseHz, float seconds, float amp, float noiseAmt, int harmonics = 2)
        {
            var s = Buf(seconds);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float v = 0f;
                for (int h = 1; h <= harmonics; h++)
                    v += Mathf.Sin(2f * Mathf.PI * baseHz * h * t) / (h * 1.6f);
                // биение 0.5 Гц — «живой» гул, а не синтетический писк
                float beat = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                s[i] = v * amp * beat + Noise() * noiseAmt;
            }
            Lowpass(s, 900f);
            // сшивка концов косинусным окном (0.2 с), иначе на стыке петли слышен щелчок:
            // проверено расчётом — разрыв падает с 0.12 до 0.01–0.03 при RMS ≈ 0.33
            int fade = Rate / 5;
            for (int i = 0; i < fade; i++)
            {
                float k = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * i / fade);   // окно Ханна
                s[i] = s[s.Length - fade + i] * (1f - k) + s[i] * k;
            }
            Normalize(s, 0.75f);
            return s;
        }

        static float[] Drip()
        {
            var s = Buf(0.35f);
            float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float f = Mathf.Lerp(1400f, 320f, Mathf.Clamp01(t / 0.12f));   // свип вниз
                phase += 2f * Mathf.PI * f / Rate;
                float env = Mathf.Exp(-t * 26f);
                s[i] = Mathf.Sin(phase) * env * 0.7f + Noise() * env * 0.25f;
            }
            Normalize(s, 0.85f);
            return s;
        }

        static float[] Step(float cutoff, float length, float body)
        {
            var s = Buf(length);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 55f);
                s[i] = Noise() * env;
            }
            Lowpass(s, cutoff);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                s[i] += Mathf.Sin(2f * Mathf.PI * 110f * t) * Mathf.Exp(-t * 40f) * body;
            }
            Normalize(s, 0.55f);
            return s;
        }

        /// <summary>Мокрый шаг: чавк — шум, промодулированный низкой частотой, плюс всплеск.</summary>
        static float[] WetStep()
        {
            var s = Buf(0.28f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 16f);
                float smack = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 42f * t);
                s[i] = Noise() * env * smack;
            }
            Lowpass(s, 3200f);
            Highpass(s, 180f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                s[i] += Mathf.Sin(2f * Mathf.PI * 90f * t) * Mathf.Exp(-t * 22f) * 0.35f;   // «бульк» воды
            }
            Normalize(s, 0.6f);
            return s;
        }

        /// <summary>Шёпот: полосовой шум с формантами и медленной модуляцией — «манит».</summary>
        static float[] Whisper()
        {
            var s = Buf(1.8f);
            float[] formants = { 420f, 900f, 1650f, 2400f };
            for (int f = 0; f < formants.Length; f++)
            {
                float amp = 0.5f / (f + 1);
                float phase = 0f;
                for (int i = 0; i < s.Length; i++)
                {
                    float t = (float)i / Rate;
                    // дыхание: медленная модуляция амплитуды + лёгкая дрожь
                    float breath = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * (1.1f + 0.2f * f) * t);
                    float tremble = 1f + 0.12f * Mathf.Sin(2f * Mathf.PI * 7f * t);
                    phase += 2f * Mathf.PI * formants[f] * tremble / Rate;
                    s[i] += Mathf.Sin(phase) * amp * breath;
                }
            }
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                s[i] += Noise() * 0.25f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 1.3f * t));
                // огибающая: тихо → громче → тихо (шёпот то приближается, то уходит)
                s[i] *= Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.8f));
            }
            Highpass(s, 300f);
            Lowpass(s, 3200f);
            Normalize(s, 0.7f);
            return s;
        }

        /// <summary>Треск электричества: редкие щелчки + шипение.</summary>
        static float[] Crackle()
        {
            var s = Buf(1.2f);
            for (int i = 0; i < s.Length; i++) s[i] = Noise() * 0.08f;
            Highpass(s, 2200f);
            var rng = new System.Random(1337);
            for (int e = 0; e < 26; e++)
            {
                int start = rng.Next(0, Mathf.Max(1, s.Length - 400));
                float amp = 0.35f + (float)rng.NextDouble() * 0.55f;
                int len = 60 + rng.Next(0, 220);
                for (int i = 0; i < len && start + i < s.Length; i++)
                {
                    float t = (float)i / Rate;
                    s[start + i] += Noise() * amp * Mathf.Exp(-t * 260f);
                }
            }
            Normalize(s, 0.8f);
            return s;
        }

        static float[] Growl()
        {
            var s = Buf(1.1f);
            float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float f = 68f + 10f * Mathf.Sin(2f * Mathf.PI * 5.5f * t);      // рычание дрожит
                phase += 2f * Mathf.PI * f / Rate;
                float saw = Mathf.Repeat(phase / (2f * Mathf.PI), 1f) * 2f - 1f; // пила = «звериный» тембр
                float env = Mathf.Min(1f, t * 8f) * Mathf.Exp(-Mathf.Max(0f, t - 0.6f) * 2.2f);
                s[i] = saw * env * 0.6f + Noise() * env * 0.22f;
            }
            Lowpass(s, 1200f);
            Normalize(s, 0.85f);
            return s;
        }

        static float[] Scream()
        {
            var s = Buf(1.3f);
            float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float f = Mathf.Lerp(280f, 1250f, Mathf.Clamp01(t / 0.45f)) * (1f + 0.08f * Mathf.Sin(2f * Mathf.PI * 9f * t));
                phase += 2f * Mathf.PI * f / Rate;
                float env = Mathf.Min(1f, t * 12f) * Mathf.Exp(-Mathf.Max(0f, t - 0.5f) * 3.5f);
                s[i] = (Mathf.Sin(phase) * 0.55f + Noise() * 0.35f) * env;
            }
            Normalize(s, 0.9f);
            return s;
        }

        static float[] Electric()
        {
            var s = Buf(0.5f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float zap = Mathf.Exp(-t * 8f);
                float buzz = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 60f * t);   // 60 Гц в такт с током
                s[i] = (Noise() * 0.7f * buzz + Mathf.Sin(2f * Mathf.PI * 3200f * t) * 0.2f) * zap;
            }
            Highpass(s, 900f);
            Normalize(s, 0.9f);
            return s;
        }

        static float[] Shot()
        {
            var s = Buf(0.42f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float crack = Mathf.Exp(-t * 90f);          // щёлк
                float body = Mathf.Exp(-t * 12f);           // раскат
                s[i] = Noise() * crack + Mathf.Sin(2f * Mathf.PI * 85f * t) * body * 0.5f + Noise() * body * 0.25f;
            }
            Lowpass(s, 6000f);
            Normalize(s, 1.0f);
            return s;
        }

        static float[] Reload()
        {
            var s = Buf(0.6f);
            void Click(float at_, float tone, float amp)
            {
                int start = (int)(at_ * Rate);
                for (int i = 0; i < 900 && start + i < s.Length; i++)
                {
                    float t = (float)i / Rate;
                    float env = Mathf.Exp(-t * 120f);
                    s[start + i] += (Mathf.Sin(2f * Mathf.PI * tone * t) * 0.5f + Noise() * 0.5f) * env * amp;
                }
            }
            Click(0.02f, 2400f, 0.8f);      // вынул магазин
            Click(0.26f, 1750f, 0.7f);      // вставил
            Click(0.42f, 3100f, 0.9f);      // дослал затвор
            Normalize(s, 0.75f);
            return s;
        }

        static float[] Click(float tone, float length, float amp)
        {
            var s = Buf(length);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 150f);
                s[i] = (Mathf.Sin(2f * Mathf.PI * tone * t) * 0.5f + Noise() * 0.5f) * env * amp;
            }
            Normalize(s, 0.6f);
            return s;
        }

        static float[] HitFlesh()
        {
            var s = Buf(0.18f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 38f);
                s[i] = (Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.5f + Noise() * 0.5f) * env;
            }
            Lowpass(s, 1500f);
            Normalize(s, 0.7f);
            return s;
        }

        static float[] HitMetal()
        {
            var s = Buf(0.4f);
            float[] tones = { 1900f, 2950f, 4400f };
            for (int k = 0; k < tones.Length; k++)
                for (int i = 0; i < s.Length; i++)
                {
                    float t = (float)i / Rate;
                    s[i] += Mathf.Sin(2f * Mathf.PI * tones[k] * t) * Mathf.Exp(-t * (18f + 6f * k)) * 0.35f;
                }
            for (int i = 0; i < s.Length; i++) s[i] += Noise() * Mathf.Exp(-(float)i / Rate * 200f) * 0.3f;
            Normalize(s, 0.75f);
            return s;
        }

        static float[] WoodKnock(float tone, float length, float amp)
        {
            var s = Buf(length);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 26f);
                s[i] = (Mathf.Sin(2f * Mathf.PI * tone * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * tone * 2.7f * t) * 0.2f
                        + Noise() * 0.35f) * env * amp;
            }
            Lowpass(s, 3500f);
            Normalize(s, 0.8f);
            return s;
        }

        static float[] StoneHit()
        {
            var s = Buf(0.3f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 30f);
                s[i] = (Mathf.Sin(2f * Mathf.PI * 680f * t) * 0.45f + Mathf.Sin(2f * Mathf.PI * 1450f * t) * 0.25f
                        + Noise() * 0.45f) * env;
            }
            Highpass(s, 200f);
            Normalize(s, 0.85f);
            return s;
        }

        static float[] RepairTicks()
        {
            var s = Buf(0.55f);
            for (int k = 0; k < 3; k++)
            {
                int start = (int)((0.02f + k * 0.16f) * Rate);
                for (int i = 0; i < 1200 && start + i < s.Length; i++)
                {
                    float t = (float)i / Rate;
                    float env = Mathf.Exp(-t * 110f);
                    s[start + i] += (Mathf.Sin(2f * Mathf.PI * 1500f * t) * 0.4f + Noise() * 0.6f) * env * 0.7f;
                }
            }
            Normalize(s, 0.65f);
            return s;
        }

        static float[] Demolish()
        {
            var s = Buf(1.4f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 3.2f);
                s[i] = Noise() * env;
            }
            Lowpass(s, 2200f);
            var rng = new System.Random(77);
            for (int e = 0; e < 30; e++)       // обломки
            {
                int start = rng.Next(0, s.Length - 2000);
                for (int i = 0; i < 1500 && start + i < s.Length; i++)
                {
                    float t = (float)i / Rate;
                    s[start + i] += Noise() * 0.5f * Mathf.Exp(-t * 90f);
                }
            }
            Normalize(s, 0.9f);
            return s;
        }

        static float[] Pickup()
        {
            var s = Buf(0.16f);
            float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float f = Mathf.Lerp(620f, 980f, Mathf.Clamp01(t / 0.05f));
                phase += 2f * Mathf.PI * f / Rate;
                s[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 32f) * 0.7f + Noise() * Mathf.Exp(-t * 90f) * 0.2f;
            }
            Normalize(s, 0.6f);
            return s;
        }

        /// <summary>Изучено: три нисходящих тона + шорох бумаги (записал в блокнот).</summary>
        static float[] ResearchDone()
        {
            var s = Buf(0.9f);
            float[] tones = { 1180f, 940f, 720f };
            for (int k = 0; k < tones.Length; k++)
            {
                int start = (int)(k * 0.14f * Rate);
                for (int i = 0; i < Rate / 3 && start + i < s.Length; i++)
                {
                    float t = (float)i / Rate;
                    s[start + i] += Mathf.Sin(2f * Mathf.PI * tones[k] * t) * Mathf.Exp(-t * 7f) * 0.4f;
                }
            }
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                s[i] += Noise() * 0.12f * Mathf.Exp(-t * 9f) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 26f * t));
            }
            Highpass(s, 300f);
            Normalize(s, 0.7f);
            return s;
        }

        static float[] CraftDone()
        {
            var s = Buf(0.5f);
            for (int k = 0; k < 2; k++)
            {
                float freq = k == 0 ? 880f : 1320f;
                int start = (int)(k * 0.11f * Rate);
                for (int i = 0; i < Rate / 2 && start + i < s.Length; i++)
                {
                    float t = (float)i / Rate;
                    s[start + i] += Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 9f) * 0.5f;
                }
            }
            Normalize(s, 0.55f);
            return s;
        }
    }
}
