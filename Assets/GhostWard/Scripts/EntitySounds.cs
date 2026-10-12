using System;
using System.Collections.Generic;
using UnityEngine;

namespace GhostWard
{
    // Голоса сущностей: у каждой свой набор.
    //  Loop    — звучит, пока сущность в комнате (дыхание, плач, хрип);
    //  Call    — из засады и когда её разозлили;
    //  Special — особое событие повадки (вой банши, крик кицунэ, хруст костей);
    //  Attack  — крик при нападении.
    public static class EntitySounds
    {
        public static AudioClip Loop(EntityKind k) => k switch
        {
            EntityKind.Ghost => ProceduralAudio.Whisper(),
            EntityKind.Shade => ProceduralAudio.Breath(),
            EntityKind.Upyr => ProceduralAudio.Rasp(),
            EntityKind.Mara => ProceduralAudio.Hum(),
            EntityKind.Banshee => CreatureAudio.Moan(),
            EntityKind.Changeling => CreatureAudio.InfantCry(),
            EntityKind.BlackDog => CreatureAudio.Pant(),
            EntityKind.Rusalka => CreatureAudio.Drip(),
            EntityKind.Llorona => CreatureAudio.Sob(),
            EntityKind.Ghoul => CreatureAudio.Gnaw(),
            EntityKind.CorpoSeco => CreatureAudio.DryRattle(),
            EntityKind.MystanKempir => CreatureAudio.Mutter(),
            EntityKind.Jinn => CreatureAudio.Whirl(),
            _ => null
        };

        public static AudioClip Call(EntityKind k) => k switch
        {
            EntityKind.Albasty => ProceduralAudio.Laugh(),
            EntityKind.Poltergeist => ProceduralAudio.Knock(),
            EntityKind.Kikimora => ProceduralAudio.Scrape(),
            EntityKind.Double => CreatureAudio.MimicLaugh(),
            EntityKind.Shade => CreatureAudio.Hiss(),
            EntityKind.Banshee => CreatureAudio.Moan(),
            EntityKind.Domovoi => CreatureAudio.Grumble(),
            EntityKind.Changeling => CreatureAudio.ChildGiggle(),
            EntityKind.BlackDog => CreatureAudio.Growl(),
            EntityKind.Rusalka => CreatureAudio.ChildGiggle(),
            EntityKind.Zheztyrnak => CreatureAudio.MetalScrape(),
            EntityKind.Llorona => CreatureAudio.Sob(),
            EntityKind.Ghoul => CreatureAudio.Click(),
            EntityKind.Kitsune => CreatureAudio.Yip(),
            EntityKind.CorpoSeco => CreatureAudio.BoneCrack(),
            EntityKind.MystanKempir => CreatureAudio.Cackle(),
            EntityKind.Jinn => CreatureAudio.Whirl(),
            _ => null
        };

        public static AudioClip Special(EntityKind k) => k switch
        {
            EntityKind.Banshee => CreatureAudio.Keen(),
            EntityKind.Domovoi => CreatureAudio.Stomp(),
            EntityKind.Kitsune => CreatureAudio.FoxScream(),
            EntityKind.Zheztyrnak => CreatureAudio.MetalScrape(),
            EntityKind.Changeling => CreatureAudio.InfantShriek(),
            EntityKind.CorpoSeco => CreatureAudio.BoneCrack(),
            EntityKind.Rusalka => CreatureAudio.Gurgle(),
            EntityKind.Jinn => CreatureAudio.Roar(),
            _ => null
        };

        public static AudioClip Attack(EntityKind k) => k switch
        {
            EntityKind.Ghost => CreatureAudio.Scream("scr_ghost", 780f, 0.35f, 0.15f, 0f),
            EntityKind.Shade => CreatureAudio.Scream("scr_shade", 260f, -0.2f, 0.6f, 0.5f),
            EntityKind.Albasty => CreatureAudio.Scream("scr_albasty", 1050f, 0.2f, 0.25f, 0f),
            EntityKind.Kikimora => CreatureAudio.Scream("scr_kiki", 1350f, 0.5f, 0.4f, 0f),
            EntityKind.Upyr => CreatureAudio.Scream("scr_upyr", 150f, -0.1f, 0.7f, 1f),
            EntityKind.Double => CreatureAudio.Scream("scr_double", 420f, -0.5f, 0.3f, 0.3f),
            EntityKind.Mara => CreatureAudio.Hiss(),
            EntityKind.Banshee => CreatureAudio.Keen(),
            EntityKind.Changeling => CreatureAudio.InfantShriek(),
            EntityKind.BlackDog => CreatureAudio.Snarl(),
            EntityKind.Rusalka => CreatureAudio.Scream("scr_rusalka", 900f, -0.3f, 0.5f, 0.2f),
            EntityKind.Zheztyrnak => CreatureAudio.Scream("scr_zhez", 1200f, 0.1f, 0.3f, 0f),
            EntityKind.Llorona => CreatureAudio.Wail(),
            EntityKind.Ghoul => CreatureAudio.Scream("scr_ghoul", 180f, 0.2f, 0.8f, 1f),
            EntityKind.Kitsune => CreatureAudio.FoxScream(),
            EntityKind.CorpoSeco => CreatureAudio.Scream("scr_corpo", 220f, -0.3f, 0.9f, 0.6f),
            EntityKind.MystanKempir => CreatureAudio.Scream("scr_mystan", 700f, 0.6f, 0.45f, 0.1f),
            EntityKind.Jinn => CreatureAudio.Roar(),
            _ => ProceduralAudio.Screech()
        };

        // синтез при первом обращении занимает миллисекунды — делаем это заранее, на загрузке ночи
        public static void Prewarm(EntityKind k)
        {
            Loop(k);
            Call(k);
            Special(k);
            Attack(k);
        }
    }

    // Синтез голосов: голосовой источник (пилообразные импульсы с дрожанием) через резонаторы-форманты,
    // плюс шумы, хрипота и перегруз. Всё считается один раз и кешируется.
    public static class CreatureAudio
    {
        const int Rate = 44100;
        static readonly Dictionary<string, AudioClip> Cache = new();

        static AudioClip Get(string key, float seconds, Func<float, System.Random, float> sample, int fade = 0)
        {
            if (Cache.TryGetValue(key, out var c)) return c;
            int len = (int)(Rate * seconds);
            var data = new float[len];
            var rng = new System.Random(key.GetHashCode());
            float peak = 1e-4f;
            for (int i = 0; i < len; i++)
            {
                float v = sample((float)i / Rate, rng);
                if (float.IsNaN(v) || float.IsInfinity(v)) v = 0f;
                data[i] = v;
                peak = Mathf.Max(peak, Mathf.Abs(v));
            }
            // выравниваем громкость: крики — пик 0.85, зацикленные голоса (с плавными краями) тише
            float g = (fade > 0 ? 0.5f : 0.85f) / peak;
            for (int i = 0; i < len; i++) data[i] *= g;
            int edge = Mathf.Max(fade, Rate / 200);
            for (int i = 0; i < edge && i < len / 2; i++)
            {
                float k = (float)i / edge;
                data[i] *= k;
                data[len - 1 - i] *= k;
            }
            var clip = AudioClip.Create(key, len, 1, Rate, false);
            clip.SetData(data, 0);
            return Cache[key] = clip;
        }

        static float Noise(System.Random r) => (float)(r.NextDouble() * 2 - 1);
        static float Sin(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
        static float Smooth(float a, float b, float x) { x = Mathf.Clamp01((x - a) / (b - a)); return x * x * (3f - 2f * x); }

        // двухполюсный резонатор: форманта голоса или «корпус» звука
        class Reso
        {
            float y1, y2;
            public float Run(float x, float f, float bw)
            {
                float r = Mathf.Exp(-Mathf.PI * bw / Rate);
                float b1 = 2f * r * Mathf.Cos(2f * Mathf.PI * f / Rate), b2 = r * r;
                float y = (1f - r) * x + b1 * y1 - b2 * y2;
                y2 = y1;
                y1 = y;
                return y;
            }
        }

        // голосовой источник: фаза пилы с дрожанием высоты (jitter) и хрипотой (rough — амплитудная модуляция)
        class Glottis
        {
            float phase, jitter;
            public float Run(float f0, float rough, System.Random r)
            {
                jitter += 0.02f * (Noise(r) - jitter);
                phase += f0 * (1f + jitter * 0.08f) / Rate;
                if (phase >= 1f) phase -= 1f;
                float saw = 1f - 2f * phase;
                float am = 1f - rough * 0.5f * (1f + Mathf.Sin(phase * 3.1f + jitter * 40f));
                return saw * am + Noise(r) * rough * 0.4f;
            }
        }

        // голос через три форманты; vowel 0..1 плавно меняет «у» → «а»
        class Voice
        {
            readonly Glottis g = new();
            readonly Reso f1 = new(), f2 = new(), f3 = new();
            public float Run(float f0, float vowel, float rough, float scale, System.Random r)
            {
                float src = g.Run(f0, rough, r);
                float a = Mathf.Lerp(320f, 820f, vowel) * scale, b = Mathf.Lerp(800f, 1250f, vowel) * scale, c = 2600f * scale;
                return f1.Run(src, a, 80f) * 1.0f + f2.Run(src, b, 100f) * 0.6f + f3.Run(src, c, 160f) * 0.3f;
            }
        }

        // ---------- крики ----------

        // f0 — высота, glide — куда уходит тон (доля), rough — хрип, growl — низкий рык под криком
        public static AudioClip Scream(string key, float f0, float glide, float rough, float growl)
        {
            var v = new Voice();
            var low = new Voice();
            return Get(key, 1.3f, (t, r) =>
            {
                float env = Smooth(0f, 0.04f, t) * Mathf.Exp(-t * 1.4f);
                float f = f0 * (1f + glide * t / 1.3f) * (1f + 0.04f * Sin(7f, t));
                float s = v.Run(f, 0.9f, rough, f0 > 600f ? 1.4f : 1f, r);
                if (growl > 0f) s += low.Run(f * 0.5f + 20f * Sin(31f, t), 0.6f, 0.9f, 0.8f, r) * growl;
                s += Noise(r) * 0.15f * rough;
                return (float)Math.Tanh(s * 3f) * env;
            });
        }

        // банши: долгий вой, поднимается, дрожит и опадает; два голоса чуть расстроены
        public static AudioClip Keen()
        {
            var a = new Voice();
            var b = new Voice();
            return Get("keen", 3.4f, (t, r) =>
            {
                float shape = Smooth(0f, 0.9f, t) * (1f - Smooth(2.4f, 3.4f, t));
                float f = 520f + 380f * Smooth(0f, 1.2f, t) - 260f * Smooth(1.8f, 3.4f, t) + 14f * Sin(5.5f, t);
                float vowel = 0.3f + 0.6f * Smooth(0.4f, 1.6f, t);
                float s = a.Run(f, vowel, 0.12f, 1.3f, r) + b.Run(f * 1.012f, vowel, 0.12f, 1.25f, r) * 0.8f;
                return (float)Math.Tanh(s * 2f) * shape + Noise(r) * 0.03f * shape;
            });
        }

        // тихий стон вдалеке
        public static AudioClip Moan()
        {
            var v = new Voice();
            return Get("moan", 4f, (t, r) =>
            {
                float env = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t / 4f * Mathf.PI)), 1.5f);
                float f = 210f + 40f * Sin(0.4f, t) + 4f * Sin(5f, t);
                return v.Run(f, 0.15f + 0.2f * Sin(0.25f, t), 0.1f, 1f, r) * env * 0.6f;
            }, Rate / 4);
        }

        // плакальщица: всхлипы с прерывистым вдохом
        public static AudioClip Sob()
        {
            var v = new Voice();
            float lp = 0f;
            return Get("sob", 4.5f, (t, r) =>
            {
                float cyc = t % 1.5f;
                float cry = Smooth(0f, 0.08f, cyc) * (1f - Smooth(0.6f, 0.95f, cyc));
                float f = 340f - cyc * 90f + 18f * Sin(6f, t);
                float breath = Smooth(1.05f, 1.15f, cyc) * (1f - Smooth(1.3f, 1.45f, cyc));
                lp += 0.15f * (Noise(r) - lp);
                float hic = Mathf.Abs(Sin(9f, t)) > 0.3f ? 1f : 0.3f;
                return v.Run(f, 0.55f, 0.25f, 1.15f, r) * cry * hic + lp * breath * 2.5f;
            }, Rate / 8);
        }

        // плакальщица нападает: «А-а-ай!» — долгий надрывный крик
        public static AudioClip Wail()
        {
            var v = new Voice();
            return Get("wail", 1.8f, (t, r) =>
            {
                float env = Smooth(0f, 0.05f, t) * (1f - Smooth(1.2f, 1.8f, t));
                float f = 620f + 160f * Smooth(0f, 0.3f, t) - 240f * Smooth(0.4f, 1.8f, t) + 25f * Sin(6.5f, t);
                return (float)Math.Tanh(v.Run(f, 0.95f, 0.3f, 1.25f, r) * 3f) * env;
            });
        }

        // подменыш: плач младенца, но слишком низкий и с хрипом на выдохе
        public static AudioClip InfantCry()
        {
            var v = new Voice();
            return Get("infant", 4f, (t, r) =>
            {
                float cyc = t % 1.33f;
                float env = Smooth(0f, 0.06f, cyc) * (1f - Smooth(0.75f, 1.0f, cyc));
                float f = 330f + 90f * Mathf.Sin(cyc / 1f * Mathf.PI) + 20f * Sin(9f, t);
                float rough = 0.2f + 0.6f * Smooth(0.5f, 0.95f, cyc);
                return v.Run(f, 0.85f, rough, 1.35f, r) * env * 0.7f;
            }, Rate / 8);
        }

        public static AudioClip InfantShriek()
        {
            var v = new Voice();
            var low = new Voice();
            return Get("infant_shriek", 1.2f, (t, r) =>
            {
                float env = Smooth(0f, 0.03f, t) * Mathf.Exp(-t * 1.6f);
                float f = 700f + 260f * Smooth(0f, 0.25f, t) + 40f * Sin(11f, t);
                float s = v.Run(f, 1f, 0.35f, 1.6f, r) + low.Run(f * 0.25f, 0.5f, 0.9f, 0.7f, r) * 0.8f;
                return (float)Math.Tanh(s * 3f) * env;
            });
        }

        // детский смешок, растянутый и замедленный — смеётся не ребёнок
        public static AudioClip ChildGiggle()
        {
            var v = new Voice();
            return Get("giggle", 1.8f, (t, r) =>
            {
                float ha = Mathf.Pow(Mathf.Max(0f, Sin(4.2f - t * 1.2f, t)), 5f);
                float f = 520f - t * 140f + 30f * Sin(7f, t);
                return v.Run(f, 0.8f, 0.3f, 1.3f, r) * ha * Mathf.Exp(-t * 0.8f);
            });
        }

        // двойник: смех игрока, только ниже и задом наперёд
        public static AudioClip MimicLaugh()
        {
            var v = new Voice();
            return Get("mimic", 2f, (t, r) =>
            {
                float tr = 2f - t;
                float ha = Mathf.Pow(Mathf.Max(0f, Sin(3f, tr)), 4f) * Smooth(0f, 1.6f, t);
                float f = 150f + tr * 30f + 8f * Sin(5f, t);
                return v.Run(f, 0.7f, 0.5f, 0.85f, r) * ha;
            });
        }

        // шипение — тень и мара
        public static AudioClip Hiss()
        {
            var body = new Reso();
            return Get("hiss", 1.6f, (t, r) =>
            {
                float env = Smooth(0f, 0.25f, t) * (1f - Smooth(1.0f, 1.6f, t));
                float n = Noise(r);
                return (body.Run(n, 3200f + 600f * Sin(0.8f, t), 1800f) * 4f + n * 0.25f) * env;
            });
        }

        // ---------- звери ----------

        // чёрный пёс: частое тяжёлое дыхание
        public static AudioClip Pant()
        {
            var body = new Reso();
            return Get("pant", 2.4f, (t, r) =>
            {
                float cyc = t % 0.4f;
                float env = Mathf.Pow(Mathf.Sin(cyc / 0.4f * Mathf.PI), 2f) * (cyc < 0.2f ? 1f : 0.7f);
                return body.Run(Noise(r), cyc < 0.2f ? 900f : 650f, 500f) * env * 3f;
            }, Rate / 20);
        }

        public static AudioClip Growl()
        {
            var v = new Voice();
            return Get("growl", 2.2f, (t, r) =>
            {
                float env = Smooth(0f, 0.3f, t) * (1f - Smooth(1.6f, 2.2f, t));
                float f = 75f + 10f * Sin(0.7f, t);
                return (float)Math.Tanh(v.Run(f, 0.3f, 0.95f, 0.6f, r) * 1.2f) * env * (0.7f + 0.3f * Mathf.Abs(Sin(2.3f, t)));
            });
        }

        public static AudioClip Snarl()
        {
            var v = new Voice();
            return Get("snarl", 1.1f, (t, r) =>
            {
                float bark = Smooth(0f, 0.02f, t) * Mathf.Exp(-t * 3f);
                float f = 160f + 200f * Mathf.Exp(-t * 6f);
                return (float)Math.Tanh(v.Run(f, 0.9f, 0.9f, 0.9f, r) * 5f) * bark;
            });
        }

        // лисий крик — короткий, рваный, почти человеческий визг
        public static AudioClip FoxScream()
        {
            var v = new Voice();
            return Get("fox", 1.0f, (t, r) =>
            {
                float env = Smooth(0f, 0.015f, t) * (1f - Smooth(0.45f, 0.75f, t));
                float f = 950f + 300f * Mathf.Sin(t / 0.75f * Mathf.PI) + 60f * Sin(23f, t);
                return (float)Math.Tanh(v.Run(f, 0.9f, 0.6f, 1.7f, r) * 4f) * env;
            });
        }

        // кицунэ: тявканье и стрекот
        public static AudioClip Yip()
        {
            var v = new Voice();
            return Get("yip", 1.4f, (t, r) =>
            {
                float y = 0f;
                for (int k = 0; k < 3; k++)
                {
                    float tk = t - k * 0.32f;
                    if (tk > 0f) y += Smooth(0f, 0.01f, tk) * Mathf.Exp(-tk * 18f);
                }
                return v.Run(1100f - t * 200f, 0.85f, 0.4f, 1.6f, r) * y;
            });
        }

        // гуль: мокрое чавканье и хруст
        public static AudioClip Gnaw()
        {
            var wet = new Reso();
            return Get("gnaw", 3f, (t, r) =>
            {
                float cyc = t % 0.55f;
                float chew = Smooth(0f, 0.03f, cyc) * Mathf.Exp(-cyc * 9f);
                float crunch = Mathf.Pow((float)r.NextDouble(), 18f) * Mathf.Sign(Noise(r)) * (cyc < 0.12f ? 1f : 0f);
                return wet.Run(Noise(r), 450f + 200f * Sin(1.3f, t), 300f) * chew * 4f + crunch * 0.8f;
            }, Rate / 20);
        }

        // гортанный щелчок
        public static AudioClip Click()
        {
            var body = new Reso();
            return Get("gclick", 1.2f, (t, r) =>
            {
                float s = 0f;
                for (int k = 0; k < 5; k++)
                {
                    float tk = t - k * 0.11f;
                    if (tk > 0f) s += body.Run(Noise(r), 1400f, 400f) * Mathf.Exp(-tk * 90f);
                }
                return s * 5f;
            });
        }

        // ---------- вещи и тела ----------

        // корпу-секу: сухой хруст костей
        public static AudioClip BoneCrack()
        {
            var body = new Reso();
            return Get("bone", 1.1f, (t, r) =>
            {
                float s = 0f;
                float[] at = { 0f, 0.09f, 0.13f, 0.31f, 0.5f, 0.53f };
                foreach (float a in at)
                {
                    float tk = t - a;
                    if (tk > 0f) s += Noise(r) * Mathf.Exp(-tk * 160f);
                }
                return body.Run(s, 2300f, 1200f) * 3f + s * 0.4f;
            });
        }

        // корпу-секу: сухой треск дыхания, как шелест бумаги
        public static AudioClip DryRattle()
        {
            var body = new Reso();
            return Get("rattle", 3f, (t, r) =>
            {
                float env = Mathf.Pow(Mathf.Max(0f, Sin(0.33f, t)), 2f);
                float crackle = Mathf.Pow((float)r.NextDouble(), 12f) * Mathf.Sign(Noise(r));
                return body.Run(crackle, 1800f, 1500f) * env * 6f + crackle * env * 0.5f;
            }, Rate / 10);
        }

        // жезтырнак: медные когти по металлу
        public static AudioClip MetalScrape()
        {
            var a = new Reso();
            var b = new Reso();
            return Get("metal", 1.6f, (t, r) =>
            {
                float env = Smooth(0f, 0.05f, t) * (1f - Smooth(1.0f, 1.6f, t));
                float stick = Mathf.Abs(Sin(37f + t * 20f, t)) > 0.6f ? 1f : 0.2f;
                float n = Noise(r) * stick;
                return (a.Run(n, 3150f + 200f * Sin(2f, t), 30f) + b.Run(n, 4730f, 40f) * 0.7f) * env * 2f;
            });
        }

        // домовой: тяжёлые удары и дребезг посуды
        public static AudioClip Stomp()
        {
            var plate = new Reso();
            return Get("stomp", 1.6f, (t, r) =>
            {
                float s = 0f;
                for (int k = 0; k < 3; k++)
                {
                    float tk = t - k * 0.38f;
                    if (tk < 0f) continue;
                    s += (Sin(48f - tk * 20f, tk) + Noise(r) * 0.3f) * Mathf.Exp(-tk * 14f);
                    s += plate.Run(Noise(r), 2900f, 60f) * Mathf.Exp(-tk * 6f) * 0.5f;
                }
                return s;
            });
        }

        // домовой: недовольное старческое бормотание за стеной
        public static AudioClip Grumble()
        {
            var v = new Voice();
            var wall = new Reso();
            return Get("grumble", 2.4f, (t, r) =>
            {
                float syl = Mathf.Pow(Mathf.Abs(Sin(3.1f + Sin(0.7f, t), t)), 3f);
                float s = v.Run(95f + 15f * Sin(1.1f, t), 0.4f + 0.3f * Sin(3.3f, t), 0.6f, 0.8f, r) * syl;
                return wall.Run(s, 400f, 500f) * 3f;
            }, Rate / 10);
        }

        // мыстан: бормотание заговора
        public static AudioClip Mutter()
        {
            var v = new Voice();
            return Get("mutter", 4f, (t, r) =>
            {
                float syl = Mathf.Pow(Mathf.Abs(Sin(4.5f + 0.8f * Sin(0.37f, t), t)), 2f);
                float f = 180f + 25f * Sin(0.6f, t) + 10f * Sin(8f, t);
                return v.Run(f, 0.5f + 0.4f * Sin(4.5f, t), 0.55f, 1.05f, r) * syl * (0.6f + 0.4f * Sin(0.25f, t));
            }, Rate / 4);
        }

        // старушечий хохот
        public static AudioClip Cackle()
        {
            var v = new Voice();
            return Get("cackle", 2.2f, (t, r) =>
            {
                float ha = Mathf.Pow(Mathf.Max(0f, Sin(5.5f, t)), 3f) * Mathf.Exp(-t * 0.6f);
                float f = 520f + 120f * Mathf.Exp(-t * 1.5f) + 30f * Sin(9f, t);
                return (float)Math.Tanh(v.Run(f, 0.9f, 0.55f, 1.3f, r) * 3f) * ha;
            });
        }

        // ---------- вода и ветер ----------

        // русалка: капли в тишине, с гулким отзвуком
        public static AudioClip Drip()
        {
            var drop = new Reso();
            var room = new Reso();
            return Get("drip", 4f, (t, r) =>
            {
                float s = 0f;
                float[] at = { 0.2f, 1.1f, 1.45f, 2.6f, 3.3f };
                for (int k = 0; k < at.Length; k++)
                {
                    float tk = t - at[k];
                    if (tk < 0f) continue;
                    float f = 1200f + k * 170f + tk * 2500f;
                    s += Sin(f, tk) * Mathf.Exp(-tk * 30f);
                }
                return s * 0.7f + room.Run(s, 300f, 50f) * 1.5f + drop.Run(Noise(r), 200f, 100f) * 0.02f;
            }, Rate / 10);
        }

        // русалка уходит под воду: бульканье
        public static AudioClip Gurgle()
        {
            var w = new Reso();
            return Get("gurgle", 1.5f, (t, r) =>
            {
                float env = Smooth(0f, 0.1f, t) * (1f - Smooth(0.9f, 1.5f, t));
                float bubbles = Mathf.Pow(Mathf.Abs(Sin(11f + 6f * Sin(1.7f, t), t)), 8f);
                return w.Run(Noise(r), 260f + 400f * bubbles, 120f) * env * (0.3f + bubbles) * 6f;
            });
        }

        // джинн: вихрь, в котором слышатся голоса
        public static AudioClip Whirl()
        {
            var band = new Reso();
            var v1 = new Voice();
            var v2 = new Voice();
            return Get("whirl", 5f, (t, r) =>
            {
                float f = 500f + 350f * Sin(0.31f, t) + 150f * Sin(0.73f, t);
                float wind = band.Run(Noise(r), f, 220f) * 3f;
                float voices = (v1.Run(130f + 20f * Sin(0.9f, t), 0.5f + 0.4f * Sin(2.1f, t), 0.4f, 1f, r)
                               + v2.Run(196f + 15f * Sin(1.3f, t), 0.5f + 0.4f * Sin(1.7f, t), 0.4f, 1.1f, r))
                               * Mathf.Pow(Mathf.Abs(Sin(0.4f, t)), 2f) * 0.35f;
                return wind + voices;
            }, Rate / 4);
        }

        // джинн нападает: рёв пламени и голосов
        public static AudioClip Roar()
        {
            var v = new Voice();
            var fire = new Reso();
            return Get("roar", 1.6f, (t, r) =>
            {
                float env = Smooth(0f, 0.08f, t) * Mathf.Exp(-t * 1.5f);
                float s = v.Run(110f + 40f * Smooth(0f, 0.3f, t), 0.9f, 0.85f, 0.8f, r)
                          + fire.Run(Noise(r), 700f, 900f) * 2f;
                return (float)Math.Tanh(s * 3f) * env;
            });
        }
    }
}
