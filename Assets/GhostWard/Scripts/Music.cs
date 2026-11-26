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

        AudioSource menu, pad, tension, pulse, stingers, theme;
        float menuTarget, padTarget, tensionTarget, pulseTarget, themeTarget;
        EntityKind? themeKind;
        static readonly Dictionary<Stinger, AudioClip> StingerClips = new();

        public static Music Create(GameObject host)
        {
            var m = host.AddComponent<Music>();
            m.menu = m.Loop(MenuTheme());
            m.pad = m.Loop(NightPad());
            m.tension = m.Loop(Tension());
            m.pulse = m.Loop(HuntPulse());
            m.stingers = host.AddComponent<AudioSource>();
            m.theme = m.Loop(null);
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
        public void SetMenu() { menuTarget = 0.5f; padTarget = 0f; tensionTarget = 0f; pulseTarget = 0f; themeTarget = 0f; }
        public void SetSilence() { menuTarget = 0f; padTarget = 0.15f; tensionTarget = 0f; pulseTarget = 0f; themeTarget = 0f; }
        public void SetScan() { menuTarget = 0f; padTarget = 0.35f; tensionTarget = 0f; pulseTarget = 0f; themeTarget = 0f; }

        // Тема сущности этой ночи: трек Resources/Music/<Kind> (например, из Suno), а если его нет — синтезированный мотив.
        // Синтез занимает заметное время, поэтому считается в фоновом потоке; звучит, как только готов.
        public void SetTheme(EntityKind k)
        {
            if (themeKind == k) return;
            themeKind = k;
            theme.Stop();
            theme.volume = 0f;
            var file = Resources.Load<AudioClip>("Music/" + k);
            if (file != null) { StartTheme(file); return; }
            themeTask = System.Threading.Tasks.Task.Run(() => EntityThemeData(k));
            themeTaskKind = k;
        }

        // Тема молчит, пока сущность не распознана: по мотиву её узнавали бы без улик,
        // а смена мотива выдавала бы, что кто-то пришёл.
        public void ClearTheme()
        {
            themeKind = null;
            themeTask = null;
            theme.Stop();
            theme.volume = 0f;
            theme.clip = null;
            if (ownedTheme != null) { Destroy(ownedTheme); ownedTheme = null; }
        }

        System.Threading.Tasks.Task<float[]> themeTask;
        EntityKind themeTaskKind;

        AudioClip ownedTheme;   // синтезированная тема (не файл из Resources) — удаляем, когда сменилась

        void StartTheme(AudioClip clip, bool owned = false)
        {
            if (ownedTheme != null && ownedTheme != clip) Destroy(ownedTheme);
            ownedTheme = owned ? clip : null;
            theme.clip = clip;
            theme.Play();
        }

        public void SetInvestigation(float anger, float fear)
        {
            menuTarget = 0f;
            padTarget = 0.3f;
            tensionTarget = Mathf.Clamp01(anger * anger * 0.3f + fear * 0.2f);
            pulseTarget = 0f;
            themeTarget = 0.4f * (1f - anger * 0.4f);   // со злостью мотив тонет в напряжении
        }

        public void SetHunt(float near, float fear)
        {
            menuTarget = 0f;
            padTarget = 0.3f;
            tensionTarget = Mathf.Clamp01(0.12f + near * 0.45f + fear * 0.15f);
            pulseTarget = 0.5f;
            themeTarget = 0.22f * (1f - near);
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
            if (themeTask != null && themeTask.IsCompleted)
            {
                if (themeTask.Status == System.Threading.Tasks.TaskStatus.RanToCompletion && themeKind == themeTaskKind)
                    StartTheme(Make("theme_" + themeTaskKind, themeTask.Result), true);
                themeTask = null;
            }
            float dt = Time.unscaledDeltaTime;
            float k = Settings.Music;
            menu.volume = Mathf.MoveTowards(menu.volume, menuTarget * k, dt * 0.4f);
            pad.volume = Mathf.MoveTowards(pad.volume, padTarget * k, dt * 0.25f);
            tension.volume = Mathf.MoveTowards(tension.volume, tensionTarget * k, dt * 0.35f);
            pulse.volume = Mathf.MoveTowards(pulse.volume, pulseTarget * k, dt * 0.5f);
            theme.volume = Mathf.MoveTowards(theme.volume, themeTarget * k, dt * 0.2f);
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

        // ---------- темы сущностей ----------

        enum Voice { MusicBox, Bell, Pluck, Flute, Choir, Organ, Cello, Metal, Piano, Theremin }

        static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 }, Phrygian = { 0, 1, 3, 5, 7, 8, 10 },
            Locrian = { 0, 1, 3, 5, 6, 8, 10 }, Penta = { 0, 3, 5, 7, 10 }, Whole = { 0, 2, 4, 6, 8, 10 },
            Hijaz = { 0, 1, 4, 5, 7, 8, 10 }, Hirajoshi = { 0, 2, 3, 7, 8 }, Pelog = { 0, 1, 3, 7, 8 },
            Dorian = { 0, 2, 3, 5, 7, 9, 10 }, Romanian = { 0, 2, 3, 6, 7, 9, 10 }, Chinese = { 0, 2, 4, 7, 9 };

        // инструмент, лад, темп (удары в минуту), сдвиг тоники от A3 в полутонах
        static (Voice voice, int[] scale, float bpm, int root) ThemeOf(EntityKind k) => k switch
        {
            EntityKind.Ghost => (Voice.MusicBox, Minor, 60, 0),
            EntityKind.Shade => (Voice.Cello, Locrian, 48, -12),
            EntityKind.Albasty => (Voice.Pluck, Penta, 66, -5),
            EntityKind.Poltergeist => (Voice.Piano, Whole, 72, 2),
            EntityKind.Double => (Voice.MusicBox, Locrian, 54, -2),
            EntityKind.Kikimora => (Voice.Flute, Phrygian, 70, 3),
            EntityKind.Upyr => (Voice.Cello, Phrygian, 50, -10),
            EntityKind.Mara => (Voice.Choir, Dorian, 44, -7),
            EntityKind.Banshee => (Voice.Choir, Dorian, 50, 0),
            EntityKind.Domovoi => (Voice.Organ, Minor, 58, -5),
            EntityKind.Changeling => (Voice.MusicBox, Dorian, 64, 5),
            EntityKind.BlackDog => (Voice.Bell, Locrian, 40, -12),
            EntityKind.Rusalka => (Voice.Choir, Minor, 52, 3),
            EntityKind.Zheztyrnak => (Voice.Pluck, Hijaz, 70, -3),
            EntityKind.Llorona => (Voice.Pluck, Minor, 56, -2),
            EntityKind.Ghoul => (Voice.Flute, Hijaz, 52, -5),
            EntityKind.Kitsune => (Voice.Flute, Hirajoshi, 48, 2),
            EntityKind.CorpoSeco => (Voice.Pluck, Phrygian, 62, -7),
            EntityKind.MystanKempir => (Voice.Cello, Penta, 46, -9),
            EntityKind.Jinn => (Voice.Theremin, Hijaz, 50, -4),
            EntityKind.Lechuza => (Voice.Flute, Minor, 58, 0),
            EntityKind.Pontianak => (Voice.Metal, Pelog, 50, 0),
            EntityKind.BabaYaga => (Voice.Organ, Phrygian, 64, -8),
            EntityKind.Shurale => (Voice.Flute, Penta, 72, -2),
            EntityKind.Cucuy => (Voice.MusicBox, Whole, 50, -3),
            EntityKind.Kuchisake => (Voice.Pluck, Hirajoshi, 54, 4),
            EntityKind.BloodyMary => (Voice.Piano, Minor, 52, -4),
            EntityKind.Ubyr => (Voice.Theremin, Penta, 46, -6),
            EntityKind.Chupacabra => (Voice.Pluck, Phrygian, 76, -9),
            EntityKind.Jiangshi => (Voice.Bell, Chinese, 60, 1),
            EntityKind.Strigoi => (Voice.Organ, Romanian, 52, -6),
            EntityKind.Karakoncolos => (Voice.Choir, Locrian, 40, -10),
            EntityKind.Tunda => (Voice.Flute, Dorian, 56, -1),
            EntityKind.Aswang => (Voice.Metal, Pelog, 66, -3),
            EntityKind.Vodyanoy => (Voice.Cello, Minor, 44, -14),
            EntityKind.Abaasy => (Voice.Theremin, Penta, 42, -12),
            EntityKind.MulaSemCabeca => (Voice.Pluck, Phrygian, 80, -5),
            EntityKind.Penanggalan => (Voice.Metal, Pelog, 46, 5),
            EntityKind.BellWitch => (Voice.Organ, Dorian, 50, 0),
            EntityKind.ZhalmauyzKempir => (Voice.Choir, Penta, 46, -9),
            _ => (Voice.MusicBox, Minor, 60, 0)
        };

        // одна нота инструмента: dt — время от начала ноты
        static float Tone(Voice v, float f, float dt, float t)
        {
            switch (v)
            {
                case Voice.MusicBox:
                    return (Sin(f, dt) + 0.4f * Sin(f * 2.01f, dt) + 0.25f * Sin(f * 4.18f, dt) * Mathf.Exp(-dt * 6f)) * Mathf.Exp(-dt * 2.2f);
                case Voice.Bell:
                    return (Sin(f, dt) + 0.6f * Sin(f * 2.76f, dt) + 0.4f * Sin(f * 5.4f, dt) * Mathf.Exp(-dt * 2f) + 0.3f * Sin(f * 0.5f, dt)) * Mathf.Exp(-dt * 0.9f);
                case Voice.Pluck:
                {
                    float s = 0f;
                    for (int h = 1; h <= 6; h++) s += Sin(f * h * (1f + 0.002f * h), dt) * Mathf.Exp(-dt * (2f + h * 1.4f)) / h;
                    return s * 1.4f;
                }
                case Voice.Flute:
                {
                    float att = Mathf.Min(1f, dt * 6f), rel = Mathf.Exp(-Mathf.Max(0f, dt - 0.9f) * 3f);
                    float vib = 1f + 0.008f * Sin(5f, dt) * Mathf.Min(1f, dt * 2f);
                    return (Sin(f * vib, dt) + 0.15f * Sin(f * 2f * vib, dt) + 0.08f * Sin(f * 3f, dt)) * att * rel;
                }
                case Voice.Choir:
                {
                    float att = Mathf.Min(1f, dt * 1.2f), rel = Mathf.Exp(-Mathf.Max(0f, dt - 1.6f) * 1.5f);
                    float s = 0f;
                    for (int c = -1; c <= 1; c++)
                    {
                        float fc = f * (1f + c * 0.006f) * (1f + 0.005f * Sin(4.6f + c, dt));
                        s += Sin(fc, dt) + 0.5f * Sin(fc * 2f, dt) + 0.35f * Sin(fc * 3f, dt) * (0.6f + 0.4f * Sin(0.7f, t));
                    }
                    return s * 0.3f * att * rel;
                }
                case Voice.Organ:
                {
                    float att = Mathf.Min(1f, dt * 10f), rel = Mathf.Exp(-Mathf.Max(0f, dt - 1.2f) * 2.5f);
                    float s = Sin(f, dt) + 0.7f * Sin(f * 2f, dt) + 0.5f * Sin(f * 3f, dt) + 0.3f * Sin(f * 4f, dt) + 0.4f * Sin(f * 0.5f, dt);
                    return s * 0.45f * att * rel * (0.9f + 0.1f * Sin(6f, dt));
                }
                case Voice.Cello:
                {
                    float att = Mathf.Min(1f, dt * 2.5f), rel = Mathf.Exp(-Mathf.Max(0f, dt - 1.8f) * 1.6f);
                    float vib = 1f + 0.007f * Sin(5.2f, dt);
                    float s = 0f;
                    for (int h = 1; h <= 7; h++) s += Sin(f * h * vib, dt) / (h * h * 0.6f + 0.4f);
                    return s * 0.5f * att * rel;
                }
                case Voice.Metal:
                    return (Sin(f, dt) + 0.7f * Sin(f * 2.4f, dt) + 0.5f * Sin(f * 3.9f, dt) * Mathf.Exp(-dt * 3f) + 0.3f * Sin(f * 5.4f, dt) * Mathf.Exp(-dt * 6f))
                           * Mathf.Exp(-dt * 1.4f) * (1f + 0.25f * Sin(3.5f, dt));
                case Voice.Piano:
                {
                    float s = Sin(f, dt) + 0.5f * Sin(f * 2.003f, dt) + 0.3f * Sin(f * 3.01f, dt) + 0.2f * Sin(f * 1.007f, dt);
                    return s * Mathf.Exp(-dt * 1.6f) * Mathf.Min(1f, dt * 300f) * 0.8f;
                }
                default:   // терменвокс: чистый тон, скользящий к ноте
                {
                    float att = Mathf.Min(1f, dt * 3f), rel = Mathf.Exp(-Mathf.Max(0f, dt - 1.5f) * 2f);
                    float glide = 1f - 0.06f * Mathf.Exp(-dt * 6f);
                    return (Sin(f * glide * (1f + 0.012f * Sin(6.5f, dt)), dt) + 0.1f * Sin(f * 2f, dt)) * att * rel;
                }
            }
        }

        // Мотив: 8–12 нот из лада с паузами, повтор с вариацией, под ним — тихий бурдон на тонике.
        static AudioClip EntityTheme(EntityKind k) => Make("theme_" + k, EntityThemeData(k));

        static float[] EntityThemeData(EntityKind k)
        {
            var (voice, scale, bpm, root) = ThemeOf(k);
            var rng = new System.Random(1000 + (int)k * 7919);
            float beat = 60f / bpm;
            int count = 8 + rng.Next(5);
            var notes = new List<(float start, int semi, float vel)>();
            float time = 0f;
            int degree = rng.Next(scale.Length);
            for (int rep = 0; rep < 2; rep++)
            {
                var r2 = new System.Random(1000 + (int)k * 7919);   // второй проход — тот же мотив...
                for (int i = 0; i < count; i++)
                {
                    int step = r2.Next(-2, 3);
                    degree = Mathf.Clamp(degree + step, 0, scale.Length * 2 - 1);
                    int semi = root + scale[degree % scale.Length] + 12 * (degree / scale.Length);
                    if (rep == 1 && i == count - 1) semi += 1;            // ...но в конце нота «не туда»
                    float dur = beat * (r2.NextDouble() < 0.3 ? 2f : 1f);
                    if (r2.NextDouble() < 0.2) time += beat;               // пауза
                    notes.Add((time, semi, 0.7f + 0.3f * (float)r2.NextDouble()));
                    time += dur;
                }
                time += beat * 4f;                                          // тишина между фразами
            }
            float len = Mathf.Max(16f, time + beat * 4f);
            float droneF = Note(root - 12);
            int first = 0;
            var d = Render(len, (t, r) =>
            {
                float wow = 1f + 0.004f * Sin(0.23f, t);
                float s = 0f;
                // ноты идут по порядку: считаем только те, что звучат сейчас (не дольше 4 с)
                while (first < notes.Count && t - notes[first].start > 4f) first++;
                for (int n = first; n < notes.Count && notes[n].start <= t; n++)
                {
                    var (start, semi, vel) = notes[n];
                    s += Tone(voice, Note(semi) * wow, t - start, t) * vel;
                }
                float drone = (Sin(droneF, t) + 0.3f * Sin(droneF * 1.5f, t)) * 0.12f * (0.6f + 0.4f * Sin(1f / len, t));
                return s * 0.3f + drone + ((float)r.NextDouble() * 2f - 1f) * 0.006f;
            }, 2000 + (int)k);
            d = Reverb(d, 0.5f, 0.86f, true);
            Normalize(d, 0.5f);
            return d;
        }

        // Тема меню: тихий тёмный гул. Два низких тона в полтона «бьются» друг о друга, сквозь них — ветер;
        // изредка далёкая расстроенная шкатулка и низкий стон. Без мелодии — тревога, а не песня.
        static AudioClip MenuTheme()
        {
            const float len = 48f;
            // частоты подогнаны под длину петли — цикл замыкается без щелчка
            float Loopf(float f) => Mathf.Round(f * len) / len;
            float a = Loopf(55f), b = Loopf(58.27f), c = Loopf(82.4f);
            // Динамик телефона почти не играет ниже ~250 Гц, поэтому у гула есть верхний слой (220–250 Гц),
            // а у ветра — шипящая полоса; низ остаётся для наушников.
            float ha = Loopf(220f), hb = Loopf(221.2f), hc = Loopf(247.2f);
            // редкие ноты шкатулки: (время, полутон от A3); последняя затихает до конца петли
            float[] at = { 2.5f, 4f, 11f, 12.6f, 20.5f, 27f, 31f, 35.5f, 40f };
            int[] notes = { 20, 15, 14, 8, 22, 19, 17, 13, 10 };
            float wind = 0f, wind2 = 0f, hiss = 0f, hissLow = 0f;
            var d = Render(len, (t, r) =>
            {
                float swell = 0.65f + 0.35f * Sin(1f / len, t);
                float drone = (Sin(a, t) + 0.8f * Sin(b, t) + 0.25f * Sin(c, t)) * 0.22f * swell;
                // верхний слой: два тона в 1.2 Гц друг от друга медленно «плывут», под ними септима
                float upper = (Sin(ha, t) + 0.8f * Sin(hb, t) + 0.35f * Sin(hc, t)) * 0.11f * (1.3f - swell * 0.5f);

                // ветер: шум через два фильтра, громкость медленно ходит
                float n = (float)r.NextDouble() * 2f - 1f;
                wind += (n - wind) * 0.02f;
                wind2 += (wind - wind2) * 0.05f;
                float gust = 0.5f + 0.5f * Sin(2f / len, t + 7f);
                // шипение ветра: полоса примерно 150–1000 Гц, слышна и в динамике
                hiss += (n - hiss) * 0.25f;
                hissLow += (n - hissLow) * 0.04f;
                float air = wind2 * 1.6f * gust + (hiss - hissLow) * 0.22f * gust * gust;

                // шкатулка: высоко, тихо, расстроено, долго тает
                float box = 0f;
                for (int i = 0; i < at.Length; i++)
                {
                    float dt = t - at[i];
                    if (dt < 0f || dt > 6f) continue;
                    float f = Note(notes[i]);
                    float env = Mathf.Exp(-dt * 1.1f) * Mathf.Min(1f, dt * 150f);
                    box += (Sin(f, dt) + 0.5f * Sin(f * 1.013f, dt) + 0.2f * Sin(f * 3.97f, dt) * Mathf.Exp(-dt * 4f)) * env;
                }

                // низкий стон: тон медленно сползает вниз
                float groan = 0f;
                float gt = t - 19f;
                if (gt > 0f && gt < 5f)
                {
                    float gf = Mathf.Lerp(98f, 69f, gt / 5f);
                    float genv = Mathf.Sin(Mathf.PI * gt / 5f);
                    groan = Mathf.Sin(2f * Mathf.PI * (98f * gt - 2.9f * gt * gt)) * genv * 0.18f * (1f + 0.3f * Sin(gf * 0.5f, gt));
                }
                return drone + upper + air + box * 0.22f + groan;
            }, 13);
            d = Reverb(d, 0.55f, 0.88f, true);
            Normalize(d, 0.5f);
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
