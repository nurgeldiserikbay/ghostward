using System;
using System.Collections.Generic;
using UnityEngine;

namespace GhostWard
{
    // Все звуки синтезируются при запуске — в прототипе нет аудиофайлов.
    public static class ProceduralAudio
    {
        const int Rate = 44100;
        static readonly Dictionary<string, AudioClip> Cache = new();

        static AudioClip Get(string key, float seconds, Func<float, System.Random, float> sample, int fade = 0)
        {
            if (Cache.TryGetValue(key, out var c)) return c;
            int len = (int)(Rate * seconds);
            var data = new float[len];
            var rng = new System.Random(key.GetHashCode());
            for (int i = 0; i < len; i++) data[i] = sample((float)i / Rate, rng);
            if (fade > 0) Fade(data, fade);
            var clip = AudioClip.Create(key, len, 1, Rate, false);
            clip.SetData(data, 0);
            return Cache[key] = clip;
        }

        static float Noise(System.Random r) => (float)(r.NextDouble() * 2 - 1);
        static float Sin(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);

        // Фоновый гул комнаты: низкий шум с медленными биениями. Зацикливается.
        public static AudioClip Drone()
        {
            float lp = 0f;
            return Get("drone", 8f, (t, r) =>
            {
                lp += 0.01f * (Noise(r) - lp);
                float beat = Sin(55f, t) * 0.3f + Sin(55.7f, t) * 0.3f + Sin(82.4f, t) * 0.12f;
                return (lp * 4f + beat * (0.6f + 0.4f * Sin(0.125f, t))) * 0.35f;
            }, Rate / 4);
        }

        // Один удар сердца «тук-тук», темп задаётся паузой между запусками.
        public static AudioClip Heartbeat() => Get("heart", 0.45f, (t, r) =>
        {
            float a = Mathf.Exp(-t * 30f) * Sin(55f - t * 30f, t);
            float t2 = t - 0.18f;
            float b = t2 > 0 ? Mathf.Exp(-t2 * 35f) * Sin(48f, t2) * 0.7f : 0f;
            return (a + b) * 0.9f;
        });

        // Зацикленный шёпот призрака.
        public static AudioClip Whisper()
        {
            float lp = 0f, bp = 0f;
            return Get("whisper", 4f, (t, r) =>
            {
                lp += 0.09f * (Noise(r) - lp);
                bp += 0.5f * (lp - bp);
                float syl = Mathf.Pow(Mathf.Abs(Sin(2.2f + Sin(0.3f, t), t)), 4f);
                return (lp - bp) * 3.5f * syl;
            }, Rate / 10);
        }

        // Тяжёлое дыхание тени.
        public static AudioClip Breath()
        {
            float lp = 0f;
            return Get("breath", 3f, (t, r) =>
            {
                lp += 0.03f * (Noise(r) - lp);
                float env = Mathf.Pow(Mathf.Max(0f, Sin(0.333f, t)), 2f) + 0.6f * Mathf.Pow(Mathf.Max(0f, -Sin(0.333f, t)), 3f);
                return (lp * 5f + Sin(70f, t) * 0.1f) * env;
            }, Rate / 10);
        }

        // Тихий смех/напев албасты: дрожащий голос с ритмичными «ха».
        public static AudioClip Laugh() => Get("laugh", 2.2f, (t, r) =>
        {
            float f = 420f + Sin(6f, t) * 25f - t * 60f;
            float ha = Mathf.Pow(Mathf.Max(0f, Sin(3.5f, t)), 6f);
            float voice = Sin(f, t) * 0.5f + Sin(f * 2.01f, t) * 0.25f + Sin(f * 3.03f, t) * 0.12f;
            return (voice * 0.6f + Noise(r) * 0.25f) * ha * Mathf.Exp(-t * 0.7f) * 0.6f;
        });

        public static AudioClip Knock() => Get("knock", 0.9f, (t, r) =>
        {
            float s = 0f;
            for (int k = 0; k < 3; k++)
            {
                float tk = t - k * 0.22f;
                if (tk < 0) continue;
                s += (Sin(140f, tk) * 0.8f + Noise(r) * 0.5f) * Mathf.Exp(-tk * 45f);
            }
            return s;
        });

        public static AudioClip Crash() => Get("crash", 1.2f, (t, r) =>
        {
            float n = Noise(r) * Mathf.Exp(-t * 5f);
            float ring = (Sin(1830f, t) + Sin(2710f, t) * 0.6f) * Mathf.Exp(-t * 6f) * 0.2f;
            float thud = Sin(70f - t * 30f, t) * Mathf.Exp(-t * 12f);
            return (n * 0.6f + ring + thud) * 0.8f;
        });

        // Визг при нападении.
        public static AudioClip Screech() => Get("screech", 0.9f, (t, r) =>
        {
            float f = 900f + Sin(23f, t) * 180f + t * 400f;
            float s = Sin(f, t) * 0.4f + Sin(f * 1.47f, t) * 0.3f + Noise(r) * 0.5f;
            return Mathf.Clamp(s * 1.6f, -1f, 1f) * Mathf.Min(1f, t * 40f) * Mathf.Exp(-t * 2f) * 0.9f;
        });

        public static AudioClip SaltThrow() => Get("salt", 0.6f, (t, r) =>
            Noise(r) * Mathf.Exp(-t * 7f) * (0.5f + 0.5f * Mathf.Abs(Sin(37f, t))) * 0.5f);

        public static AudioClip BellRing() => Get("bell", 2.2f, (t, r) =>
        {
            float env = Mathf.Exp(-t * 2.2f);
            return (Sin(1046f, t) + Sin(1046f * 2.76f, t) * 0.5f + Sin(1046f * 5.4f, t) * 0.25f) * env * 0.25f;
        });

        public static AudioClip IronStrike() => Get("iron", 0.8f, (t, r) =>
            (Sin(2400f, t) * 0.4f + Sin(3720f, t) * 0.3f + Noise(r) * Mathf.Exp(-t * 60f)) * Mathf.Exp(-t * 5f) * 0.5f);

        // Вой при изгнании: нисходящий тон, тающий в шум.
        public static AudioClip Banish() => Get("banish", 1.4f, (t, r) =>
        {
            float f = 520f - t * 300f;
            return (Sin(f, t) * 0.5f + Sin(f * 1.5f, t) * 0.25f + Noise(r) * 0.2f * t) * Mathf.Exp(-t * 2.5f) * 0.5f;
        });

        // Треск помех, зацикленный: громкость зависит от близости сущности.
        public static AudioClip Static() => Get("static", 2f, (t, r) =>
        {
            float crackle = Mathf.Pow((float)r.NextDouble(), 30f) * Mathf.Sign(Noise(r));
            return (Noise(r) * 0.25f + crackle * 0.8f) * (0.7f + 0.3f * Sin(50f, t));
        }, Rate / 20);

        // Скрежет когтей кикиморы.
        public static AudioClip Scrape() => Get("scrape", 1.1f, (t, r) =>
        {
            float bursts = Mathf.Pow(Mathf.Abs(Sin(9f + t * 6f, t)), 3f);
            return (Noise(r) * 0.5f + Sin(2300f + Sin(13f, t) * 300f, t) * 0.2f) * bursts * Mathf.Exp(-t * 1.2f) * 0.7f;
        });

        // Хрип упыря, зацикленный.
        public static AudioClip Rasp()
        {
            float lp = 0f;
            return Get("rasp", 2.6f, (t, r) =>
            {
                lp += 0.2f * (Noise(r) - lp);
                float env = Mathf.Pow(Mathf.Max(0f, Sin(0.385f, t)), 1.5f);
                float growl = Mathf.Sign(Sin(48f + Sin(3f, t) * 6f, t)) * 0.15f;
                return (lp * 1.4f + growl) * env * 0.7f;
            }, Rate / 10);
        }

        // Низкий гул-напев мары, зацикленный.
        public static AudioClip Hum() => Get("hum", 4f, (t, r) =>
        {
            float f = 98f + Sin(0.25f, t) * 4f;
            return (Sin(f, t) * 0.4f + Sin(f * 2f, t) * 0.2f + Sin(f * 3.01f, t) * 0.1f) * (0.6f + 0.4f * Sin(0.5f, t)) * 0.5f;
        }, Rate / 4);

        // Щелчок переключения камеры.
        public static AudioClip Click() => Get("click", 0.08f, (t, r) => Noise(r) * Mathf.Exp(-t * 120f) * 0.6f);

        // Звон зеркала: стеклянный перелив.
        public static AudioClip Shimmer() => Get("shimmer", 1.4f, (t, r) =>
        {
            float s = 0f;
            for (int k = 0; k < 5; k++) s += Sin(1800f + k * 470f + t * 300f, t) * Mathf.Exp(-(t - k * 0.06f) * (t - k * 0.06f) * 20f);
            return s * Mathf.Exp(-t * 2f) * 0.18f;
        });

        // Свист пучка полыни.
        public static AudioClip Whoosh()
        {
            float lp = 0f;
            return Get("whoosh", 0.8f, (t, r) =>
            {
                lp += (0.05f + 0.25f * Mathf.Sin(t / 0.8f * Mathf.PI)) * (Noise(r) - lp);
                return lp * Mathf.Sin(t / 0.8f * Mathf.PI) * 1.2f;
            });
        }

        // «Радио духов»: искажённый голос сквозь эфир — слоги с плавающей высотой.
        public static AudioClip RadioVoice() => Get("radiovoice", 1.8f, (t, r) =>
        {
            float syl = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 2.4f)), 2f) * (t < 1.5f ? 1f : 0f);
            float f0 = 140f - t * 30f + Sin(5f, t) * 10f;
            float voice = 0f;
            for (int k = 1; k <= 6; k++)
                voice += Sin(f0 * k, t) * Mathf.Exp(-Mathf.Pow((f0 * k - (500f + Sin(3f, t) * 300f)) / 350f, 2f));
            float hiss = Noise(r) * 0.25f;
            float chop = Mathf.Sin(t * 70f) > -0.6f ? 1f : 0.2f;
            return (voice * syl * 0.7f + hiss) * chop * 0.6f;
        });

        // Скрип пола — ложная тревога, не улика.
        public static AudioClip Creak() => Get("creak", 1.2f, (t, r) =>
        {
            float f = 180f + Sin(1.3f, t) * 60f + Sin(17f, t) * 12f;
            float saw = (t * f) % 1f * 2f - 1f;
            float env = Mathf.Sin(Mathf.Clamp01(t / 1.2f) * Mathf.PI);
            return (saw * 0.3f + Noise(r) * 0.08f) * env * Mathf.Abs(Sin(23f, t)) * 0.5f;
        });

        // Шаги: четыре тяжёлых шага по деревянному полу.
        public static AudioClip Footsteps() => Get("steps", 2.4f, (t, r) =>
        {
            float s = 0f;
            for (int k = 0; k < 4; k++)
            {
                float tk = t - k * 0.55f;
                if (tk < 0f) continue;
                s += (Sin(65f - tk * 30f, tk) * 0.9f + Noise(r) * 0.35f) * Mathf.Exp(-tk * 18f);
                float creak = tk - 0.05f;
                if (creak > 0f) s += Sin(310f + Sin(9f, creak) * 40f, creak) * Mathf.Exp(-creak * 9f) * 0.12f;
            }
            return s * 0.8f;
        });

        // писк ЭМП-детектора
        public static AudioClip Beep() => Get("beep", 0.07f, (t, r) => Mathf.Sign(Sin(1900f, t)) * Mathf.Min(1f, (0.07f - t) * 60f) * 0.12f);

        public static AudioClip Fail() => Get("fail", 0.35f, (t, r) =>
            Mathf.Sign(Sin(180f - t * 200f, t)) * Mathf.Exp(-t * 8f) * 0.12f);

        static void Fade(float[] d, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                d[i] *= k;
                d[d.Length - 1 - i] *= k;
            }
        }
    }
}
