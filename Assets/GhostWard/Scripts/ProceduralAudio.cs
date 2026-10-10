using System.Collections.Generic;
using UnityEngine;

namespace GhostWard
{
    // Все звуки синтезируются при запуске — в прототипе нет аудиофайлов.
    public static class ProceduralAudio
    {
        const int Rate = 44100;
        static readonly Dictionary<string, AudioClip> Cache = new();

        // Зацикленный шёпот: шум через полосовой фильтр с «дыханием» по громкости.
        public static AudioClip Whisper(float pitch)
        {
            string key = "whisper" + pitch.ToString("0.00");
            if (Cache.TryGetValue(key, out var c)) return c;

            int len = Rate * 4;
            var data = new float[len];
            var rng = new System.Random(key.GetHashCode());
            float lp = 0f, bp = 0f;
            float cutoff = 0.08f * pitch;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                lp += cutoff * (noise - lp);
                bp += 0.5f * (lp - bp);
                float breath = Mathf.Pow(Mathf.Sin(t * Mathf.PI * 0.5f * 3f) * 0.5f + 0.5f, 3f);
                float moan = Mathf.Sin(2 * Mathf.PI * (110f * pitch + Mathf.Sin(t * 5f) * 6f) * t) * 0.25f;
                data[i] = ((lp - bp) * 3f * breath + moan * breath) * 0.6f;
            }
            Fade(data, Rate / 20);
            return Cache[key] = Make(key, data);
        }

        public static AudioClip Banish()
        {
            if (Cache.TryGetValue("banish", out var c)) return c;
            int len = (int)(Rate * 0.9f);
            var data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 4f);
                float f = 440f + t * 900f;
                data[i] = (Mathf.Sin(2 * Mathf.PI * f * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * f * 1.5f * t)) * env * 0.35f;
            }
            return Cache["banish"] = Make("banish", data);
        }

        public static AudioClip Hit()
        {
            if (Cache.TryGetValue("hit", out var c)) return c;
            int len = (int)(Rate * 0.6f);
            var data = new float[len];
            var rng = new System.Random(7);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 7f);
                float thump = Mathf.Sin(2 * Mathf.PI * (60f - t * 40f) * t);
                float noise = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 15f);
                data[i] = (thump * 0.8f + noise * 0.5f) * env;
            }
            return Cache["hit"] = Make("hit", data);
        }

        public static AudioClip Fail()
        {
            if (Cache.TryGetValue("fail", out var c)) return c;
            int len = (int)(Rate * 0.35f);
            var data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float sq = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * (180f - t * 200f) * t));
                data[i] = sq * Mathf.Exp(-t * 8f) * 0.15f;
            }
            return Cache["fail"] = Make("fail", data);
        }

        public static AudioClip Spawn()
        {
            if (Cache.TryGetValue("spawn", out var c)) return c;
            int len = (int)(Rate * 1.2f);
            var data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Sin(Mathf.Clamp01(t / 1.2f) * Mathf.PI);
                float f = 300f - t * 150f;
                data[i] = (Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Sin(2 * Mathf.PI * 7f * t)) * env * 0.3f;
            }
            return Cache["spawn"] = Make("spawn", data);
        }

        static void Fade(float[] d, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                d[i] *= k;
                d[d.Length - 1 - i] *= k;
            }
        }

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
