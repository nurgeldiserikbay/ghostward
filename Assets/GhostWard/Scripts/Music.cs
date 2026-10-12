using System;
using System.Collections.Generic;
using UnityEngine;

namespace GhostWard
{
    // Музыка и «пугающие» акценты — синтезируются при запуске, аудиофайлов нет.
    // Слои играют одновременно и смешиваются по состоянию игры: меню — шкатулка; ночь — гул;
    // напряжение растёт со злостью и близостью сущности; охота — медленный пульс.
    // Акценты редкие: хоррор держится на тишине и ожидании, а не на постоянных ударах.
    public class Music : MonoBehaviour
    {
        public enum Stinger { Swell, Boom, Strings, Dawn }

        const int Rate = 22050;

        AudioSource menu, pad, tension, pulse, stingers;
        float menuTarget, padTarget, tensionTarget, pulseTarget;
        static readonly Dictionary<Stinger, AudioClip> StingerClips = new();

        public static Music Create(GameObject host)
        {
            var m = host.AddComponent<Music>();
            m.menu = m.Loop(MenuTheme());
            m.pad = m.Loop(NightPad());
            m.tension = m.Loop(Tension());
            m.pulse = m.Loop(HuntPulse());
            m.stingers = host.AddComponent<AudioSource>();
            return m;
        }

        AudioSource Loop(AudioClip clip)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.volume = 0f;
            s.Play();
            return s;
        }

        // уровни 0..1: страх, близость сущности, злость
        public void SetMenu() { menuTarget = 0.55f; padTarget = 0f; tensionTarget = 0f; pulseTarget = 0f; }
        public void SetSilence() { menuTarget = 0f; padTarget = 0.15f; tensionTarget = 0f; pulseTarget = 0f; }
        public void SetScan() { menuTarget = 0f; padTarget = 0.35f; tensionTarget = 0f; pulseTarget = 0f; }

        public void SetInvestigation(float anger, float fear)
        {
            menuTarget = 0f;
            padTarget = 0.45f;
            tensionTarget = Mathf.Clamp01(anger * anger * 0.3f + fear * 0.2f);
            pulseTarget = 0f;
        }

        public void SetHunt(float near, float fear)
        {
            menuTarget = 0f;
            padTarget = 0.3f;
            tensionTarget = Mathf.Clamp01(0.12f + near * 0.45f + fear * 0.15f);
            pulseTarget = 0.5f;
        }

        public void Play(Stinger s, float volume = 1f)
        {
            if (!StingerClips.TryGetValue(s, out var clip))
            {
                clip = s switch
                {
                    Stinger.Swell => ReverseSwell(),
                    Stinger.Boom => Boom(),
                    Stinger.Strings => StringStab(),
                    _ => DawnChime()
                };
                StingerClips[s] = clip;
            }
            stingers.PlayOneShot(clip, volume * Settings.Music);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float k = Settings.Music;
            menu.volume = Mathf.MoveTowards(menu.volume, menuTarget * k, dt * 0.4f);
            pad.volume = Mathf.MoveTowards(pad.volume, padTarget * k, dt * 0.25f);
            tension.volume = Mathf.MoveTowards(tension.volume, tensionTarget * k, dt * 0.35f);
            pulse.volume = Mathf.MoveTowards(pulse.volume, pulseTarget * k, dt * 0.5f);
        }

        // ---------- синтез ----------

        static float Sin(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
        static float Note(int semitone) => 220f * Mathf.Pow(2f, semitone / 12f);   // от A3

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float[] Render(float seconds, Func<float, System.Random, float> f, int seed)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            var r = new System.Random(seed);
            for (int i = 0; i < n; i++) d[i] = f((float)i / Rate, r);
            return d;
        }

        // Ревербация Шрёдера (4 гребенчатых + 2 всепропускающих фильтра) — «пустой дом».
        // loop: хвост переносится через начало, чтобы петля не щёлкала.
        static float[] Reverb(float[] dry, float wet, float room, bool loop)
        {
            int n = dry.Length;
            int passes = loop ? 2 : 1;
            var outp = new float[n];
            int[] combLen = { 1116, 1188, 1277, 1356 };
            int[] apLen = { 556, 441 };
            var combs = new float[4][];
            var ci = new int[4];
            var lp = new float[4];
            for (int c = 0; c < 4; c++) combs[c] = new float[combLen[c] / 2];
            var aps = new float[2][];
            var ai = new int[2];
            for (int a = 0; a < 2; a++) aps[a] = new float[apLen[a] / 2];

            for (int pass = 0; pass < passes; pass++)
                for (int i = 0; i < n; i++)
                {
                    float x = dry[i] * 0.15f, y = 0f;
                    for (int c = 0; c < 4; c++)
                    {
                        float o = combs[c][ci[c]];
                        lp[c] = o * 0.7f + lp[c] * 0.3f;
                        combs[c][ci[c]] = x + lp[c] * room;
                        ci[c] = (ci[c] + 1) % combs[c].Length;
                        y += o;
                    }
                    for (int a = 0; a < 2; a++)
                    {
                        float b = aps[a][ai[a]];
                        aps[a][ai[a]] = y + b * 0.5f;
                        ai[a] = (ai[a] + 1) % aps[a].Length;
                        y = b - y * 0.5f;
                    }
                    if (pass == passes - 1) outp[i] = dry[i] * (1f - wet) + y * wet * 2.2f;
                }
            return outp;
        }

        static void Normalize(float[] d, float peak)
        {
            float m = 1e-4f;
            foreach (var v in d) m = Mathf.Max(m, Mathf.Abs(v));
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        // Тема меню: колыбельная на расстроенной музыкальной шкатулке, плёнка чуть «плывёт».
        static AudioClip MenuTheme()
        {
            // ля минор, медленно: (полутон от A3, начало в долях, длительность)
            int[] notes = { 12, 15, 19, 17, 15, 14, 12, 7, 10, 12, 14, 10, 12, 15, 12, 11 };
            float beat = 0.75f;
            float len = notes.Length * beat * 2f;
            var d = Render(len, (t, r) =>
            {
                float wow = 1f + 0.012f * Sin(0.31f, t) + 0.004f * Sin(5.3f, t);
                float s = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    float start = i * beat * 2f + (i % 2 == 1 ? beat * 0.5f : 0f);
                    float dtn = t - start;
                    if (dtn < 0f || dtn > 3f) continue;
                    float f = Note(notes[i]) * wow;
                    float env = Mathf.Exp(-dtn * 2.2f) * Mathf.Min(1f, dtn * 200f);
                    s += (Sin(f, dtn) + 0.4f * Sin(f * 2.01f, dtn) + 0.25f * Sin(f * 4.18f, dtn) * Mathf.Exp(-dtn * 6f)) * env;
                }
                float bass = Sin(55f * wow, t) * 0.25f * (0.6f + 0.4f * Sin(1f / len, t));
                float hiss = ((float)r.NextDouble() * 2f - 1f) * 0.01f;
                return s * 0.35f + bass * 0.3f + hiss;
            }, 11);
            d = Reverb(d, 0.45f, 0.84f, true);
            Normalize(d, 0.6f);
            return Make("menuTheme", d);
        }

        // Ночной гул: две низкие ноты в полутон, медленно дышат; сквозь них ветер.
        static AudioClip NightPad()
        {
            float len = 32f;
            float wind = 0f, wind2 = 0f;
            var d = Render(len, (t, r) =>
            {
                float breathe = 0.6f + 0.4f * Sin(1f / 16f, t);
                float a = Sin(55f, t) + 0.5f * Sin(110.3f, t) + 0.2f * Sin(165.2f, t);
                float b = Sin(58.27f, t) + 0.4f * Sin(116.4f, t);
                float n = (float)r.NextDouble() * 2f - 1f;
                wind += 0.02f * (n - wind);
                wind2 += 0.2f * (wind - wind2);
                float gust = 0.5f + 0.5f * Sin(1f / 8f, t + 2f);
                return (a * 0.35f + b * 0.25f * (1f - breathe * 0.5f)) * breathe * 0.4f + (wind - wind2) * 6f * gust;
            }, 12);
            d = Reverb(d, 0.4f, 0.86f, true);
            Normalize(d, 0.55f);
            return Make("nightPad", d);
        }

        // Напряжение: высокий кластер «струн» с вибрато, режущий и неустойчивый.
        static AudioClip Tension()
        {
            float len = 16f;
            float[] freqs = { 1174.7f, 1244.5f, 1318.5f, 1396.9f };
            var d = Render(len, (t, r) =>
            {
                float s = 0f;
                for (int i = 0; i < freqs.Length; i++)
                {
                    float vib = 1f + 0.006f * Sin(5.5f + i * 0.7f, t);
                    float bow = 0.6f + 0.4f * Sin(1f / 8f + i * 0.03f, t + i);
                    float f = freqs[i] * vib;
                    // пилообразная волна смягчена — похоже на смычок
                    float saw = 0f;
                    for (int h = 1; h <= 4; h++) saw += Sin(f * h, t) / h;
                    s += saw * bow;
                }
                float scrape = ((float)r.NextDouble() * 2f - 1f) * 0.05f;
                return s * 0.12f + scrape;
            }, 13);
            d = Reverb(d, 0.5f, 0.8f, true);
            Normalize(d, 0.4f);
            return Make("tension", d);
        }

        // Пульс охоты: низкий удар раз в секунду и тиканье часов между ними.
        static AudioClip HuntPulse()
        {
            float len = 8f;
            var d = Render(len, (t, r) =>
            {
                float beat = t % 1f;
                float thump = Sin(42f - beat * 20f, beat) * Mathf.Exp(-beat * 9f);
                float tickT = (t + 0.5f) % 1f;
                float tick = ((float)r.NextDouble() * 2f - 1f) * Mathf.Exp(-tickT * 400f) * 0.4f;
                return thump * 0.9f + tick;
            }, 14);
            d = Reverb(d, 0.3f, 0.75f, true);
            Normalize(d, 0.6f);
            return Make("huntPulse", d);
        }

        // Обратный шорох: шум и тон нарастают и обрываются — «сейчас что-то будет».
        static AudioClip ReverseSwell()
        {
            float len = 1.8f;
            var d = Render(len, (t, r) =>
            {
                float k = t / len;
                float env = Mathf.Pow(k, 3f) * (k < 0.97f ? 1f : 0f);
                float noise = (float)r.NextDouble() * 2f - 1f;
                return (noise * 0.6f + Sin(180f + k * 240f, t) * 0.4f) * env;
            }, 15);
            Normalize(d, 0.8f);
            return Make("swell", d);
        }

        // Низкий удар с хвостом — появление.
        static AudioClip Boom()
        {
            float len = 3f;
            var d = Render(len, (t, r) =>
            {
                float sub = Sin(38f - t * 8f, t) * Mathf.Exp(-t * 1.6f);
                float hit = ((float)r.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 25f);
                float metal = (Sin(311f, t) + Sin(467f, t) * 0.6f) * Mathf.Exp(-t * 3f) * 0.15f;
                return sub + hit * 0.5f + metal;
            }, 16);
            d = Reverb(d, 0.45f, 0.88f, false);
            Normalize(d, 0.9f);
            return Make("boom", d);
        }

        // Скрип струн — нападение.
        static AudioClip StringStab()
        {
            float len = 2.2f;
            var d = Render(len, (t, r) =>
            {
                float env = Mathf.Min(1f, t * 60f) * Mathf.Exp(-t * 1.8f);
                float s = 0f;
                float[] f = { 1567.98f, 1661.2f, 1760f, 2349.3f };
                foreach (var fr in f)
                {
                    float gliss = fr * (1f - t * 0.04f);
                    for (int h = 1; h <= 5; h++) s += Sin(gliss * h * (1f + 0.003f * Sin(7f, t)), t) / h;
                }
                return (s * 0.1f + ((float)r.NextDouble() * 2f - 1f) * 0.15f * Mathf.Exp(-t * 6f)) * env;
            }, 17);
            d = Reverb(d, 0.4f, 0.85f, false);
            Normalize(d, 0.85f);
            return Make("strings", d);
        }

        // Рассвет: тихий колокол и мажорная (наконец-то) терция.
        static AudioClip DawnChime()
        {
            float len = 5f;
            var d = Render(len, (t, r) =>
            {
                float env = Mathf.Exp(-t * 0.9f);
                float bell = Sin(Note(12), t) + 0.5f * Sin(Note(16), t) + 0.4f * Sin(Note(19), t) + 0.2f * Sin(Note(12) * 2.76f, t) * Mathf.Exp(-t * 3f);
                return bell * env * 0.3f;
            }, 18);
            d = Reverb(d, 0.5f, 0.88f, false);
            Normalize(d, 0.55f);
            return Make("dawn", d);
        }
    }
}
