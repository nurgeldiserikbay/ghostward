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
            EntityKind.Lechuza => CreatureAudio.OwlHoot(),
            EntityKind.Pontianak => CreatureAudio.FarCry(),
            EntityKind.BabaYaga => CreatureAudio.Mortar(),
            EntityKind.Shurale => CreatureAudio.WoodCreak(),
            EntityKind.Cucuy => CreatureAudio.UnderBed(),
            EntityKind.Kuchisake => CreatureAudio.Snip(),
            EntityKind.BloodyMary => CreatureAudio.Chant(),
            EntityKind.Ubyr => CreatureAudio.FireCrackle(),
            EntityKind.Chupacabra => CreatureAudio.Chitter(),
            EntityKind.Strigoi => CreatureAudio.Hiss(),
            EntityKind.Karakoncolos => CreatureAudio.IcyWind(),
            EntityKind.Tunda => CreatureAudio.MotherHum(),
            EntityKind.Aswang => CreatureAudio.TikTik(),
            EntityKind.Vodyanoy => CreatureAudio.Bubbles(),
            EntityKind.Abaasy => CreatureAudio.IronGrind(),
            EntityKind.MulaSemCabeca => CreatureAudio.FireCrackle(),
            EntityKind.Penanggalan => CreatureAudio.WetDrip(),
            EntityKind.BellWitch => CreatureAudio.Hymn(),
            EntityKind.ZhalmauyzKempir => CreatureAudio.ManyVoices(),
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
            EntityKind.Lechuza => CreatureAudio.InfantCry(),
            EntityKind.Pontianak => CreatureAudio.HighLaugh(),
            EntityKind.BabaYaga => CreatureAudio.Cackle(),
            EntityKind.Shurale => CreatureAudio.Tickle(),
            EntityKind.Cucuy => CreatureAudio.DeepWhisper(),
            EntityKind.Kuchisake => CreatureAudio.Question(),
            EntityKind.BloodyMary => CreatureAudio.GlassCrack(),
            EntityKind.Ubyr => CreatureAudio.Hiss(),
            EntityKind.Chupacabra => CreatureAudio.Chitter(),
            EntityKind.Jiangshi => CreatureAudio.Hop(),
            EntityKind.Strigoi => CreatureAudio.WingFlap(),
            EntityKind.Karakoncolos => CreatureAudio.NameCall(),
            EntityKind.Tunda => CreatureAudio.PegStep(),
            EntityKind.Aswang => CreatureAudio.TikTik(),
            EntityKind.Vodyanoy => CreatureAudio.Croak(),
            EntityKind.Abaasy => CreatureAudio.JawHarp(),
            EntityKind.MulaSemCabeca => CreatureAudio.Neigh(),
            EntityKind.Penanggalan => CreatureAudio.WetFlap(),
            EntityKind.BellWitch => CreatureAudio.Slap(),
            EntityKind.ZhalmauyzKempir => CreatureAudio.ManyVoices(),
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
            EntityKind.Lechuza => CreatureAudio.OwlScreech(),
            EntityKind.Pontianak => CreatureAudio.HighLaugh(),
            EntityKind.BabaYaga => CreatureAudio.IronTeeth(),
            EntityKind.Shurale => CreatureAudio.BoneCrack(),
            EntityKind.Cucuy => CreatureAudio.DeepWhisper(),
            EntityKind.Kuchisake => CreatureAudio.Snip(),
            EntityKind.BloodyMary => CreatureAudio.GlassCrack(),
            EntityKind.Ubyr => CreatureAudio.FireWhoosh(),
            EntityKind.Chupacabra => CreatureAudio.SpineRattle(),
            EntityKind.Jiangshi => CreatureAudio.Hop(),
            EntityKind.Strigoi => CreatureAudio.WingFlap(),
            EntityKind.Karakoncolos => CreatureAudio.NameCall(),
            EntityKind.Tunda => CreatureAudio.PegStep(),
            EntityKind.Aswang => CreatureAudio.WingFlap(),
            EntityKind.Vodyanoy => CreatureAudio.Gurgle(),
            EntityKind.Abaasy => CreatureAudio.OneLegHop(),
            EntityKind.MulaSemCabeca => CreatureAudio.Gallop(),
            EntityKind.Penanggalan => CreatureAudio.WetFlap(),
            EntityKind.BellWitch => CreatureAudio.Slap(),
            EntityKind.ZhalmauyzKempir => CreatureAudio.Chomp(),
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
            EntityKind.Lechuza => CreatureAudio.OwlScreech(),
            EntityKind.Pontianak => CreatureAudio.Scream("scr_ponti", 1300f, 0.3f, 0.3f, 0f),
            EntityKind.BabaYaga => CreatureAudio.Scream("scr_yaga", 560f, 0.5f, 0.6f, 0.3f),
            EntityKind.Shurale => CreatureAudio.Scream("scr_shurale", 880f, 0.8f, 0.4f, 0.2f),
            EntityKind.Cucuy => CreatureAudio.Scream("scr_cucuy", 120f, 0.1f, 0.9f, 1f),
            EntityKind.Kuchisake => CreatureAudio.Scream("scr_kuchi", 1000f, -0.2f, 0.35f, 0f),
            EntityKind.BloodyMary => CreatureAudio.Scream("scr_mary", 960f, -0.4f, 0.45f, 0.1f),
            EntityKind.Ubyr => CreatureAudio.FireWhoosh(),
            EntityKind.Chupacabra => CreatureAudio.Scream("scr_chupa", 700f, 0.3f, 0.9f, 0.5f),
            EntityKind.Jiangshi => CreatureAudio.Scream("scr_jiang", 300f, -0.3f, 0.7f, 0.6f),
            EntityKind.Strigoi => CreatureAudio.Scream("scr_strigoi", 380f, 0.2f, 0.7f, 0.6f),
            EntityKind.Karakoncolos => CreatureAudio.Roar(),
            EntityKind.Tunda => CreatureAudio.Scream("scr_tunda", 820f, -0.5f, 0.4f, 0.2f),
            EntityKind.Aswang => CreatureAudio.Scream("scr_aswang", 1150f, 0.4f, 0.6f, 0.2f),
            EntityKind.Vodyanoy => CreatureAudio.Scream("scr_vodyanoy", 140f, 0.2f, 0.8f, 1f),
            EntityKind.Abaasy => CreatureAudio.Roar(),
            EntityKind.MulaSemCabeca => CreatureAudio.Neigh(),
            EntityKind.Penanggalan => CreatureAudio.Scream("scr_penang", 1250f, 0.2f, 0.5f, 0f),
            EntityKind.BellWitch => CreatureAudio.Cackle(),
            EntityKind.ZhalmauyzKempir => CreatureAudio.Scream("scr_zhalm", 240f, 0.1f, 0.9f, 1f),
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
        // ---------- голоса новых сущностей ----------

        // сова: глухое «у-ху» с придыханием
        public static AudioClip OwlHoot()
        {
            var v = new Voice();
            return Get("owlhoot", 4f, (t, r) =>
            {
                float s = 0f;
                float[] at = { 0.3f, 0.75f, 2.2f };
                foreach (float a in at)
                {
                    float tk = t - a;
                    if (tk < 0f || tk > 0.5f) continue;
                    s += v.Run(330f - tk * 60f, 0.05f, 0.25f, 0.9f, r) * Mathf.Sin(tk / 0.5f * Mathf.PI);
                }
                return s;
            }, Rate / 10);
        }

        // сипуха: шипящий визг — жуткий и почти человеческий
        public static AudioClip OwlScreech()
        {
            var body = new Reso();
            var v = new Voice();
            return Get("owlscreech", 1.4f, (t, r) =>
            {
                float env = Smooth(0f, 0.04f, t) * (1f - Smooth(0.8f, 1.4f, t));
                float n = body.Run(Noise(r), 3200f + 800f * Sin(0.9f, t), 1400f) * 4f;
                return ((float)Math.Tanh(n * 2f) + v.Run(1400f, 0.9f, 0.8f, 1.7f, r) * 0.4f) * env;
            });
        }

        // понтианак: детский плач, будто издалека — чем тише, тем она ближе
        public static AudioClip FarCry()
        {
            var v = new Voice();
            var far = new Reso();
            return Get("farcry", 5f, (t, r) =>
            {
                float cyc = t % 1.7f;
                float env = Smooth(0f, 0.1f, cyc) * (1f - Smooth(0.9f, 1.3f, cyc));
                float s = v.Run(420f + 60f * Mathf.Sin(cyc * 2.4f), 0.85f, 0.3f, 1.4f, r) * env;
                return far.Run(s, 900f, 1200f) * 3f;
            }, Rate / 8);
        }

        // высокий женский смешок «хи-хи-хи», тянется
        public static AudioClip HighLaugh()
        {
            var v = new Voice();
            return Get("highlaugh", 2.6f, (t, r) =>
            {
                float hi = Mathf.Pow(Mathf.Max(0f, Sin(4.8f - t * 0.6f, t)), 4f) * (1f - Smooth(1.8f, 2.6f, t));
                return v.Run(880f + 120f * Mathf.Exp(-t), 1f, 0.35f, 1.6f, r) * hi;
            });
        }

        // баба-яга: ступа бьёт об землю, метла заметает след
        public static AudioClip Mortar()
        {
            var broom = new Reso();
            return Get("mortar", 3.2f, (t, r) =>
            {
                float c = t % 1.6f;
                float thump = (Sin(52f - c * 25f, c) + Noise(r) * 0.25f) * Mathf.Exp(-c * 10f);
                float sweep = broom.Run(Noise(r), 2500f, 2000f) * Mathf.Pow(Mathf.Max(0f, Mathf.Sin((c - 0.5f) / 0.9f * Mathf.PI)), 2f) * (c > 0.5f && c < 1.4f ? 1f : 0f);
                return thump + sweep * 1.5f;
            }, Rate / 20);
        }

        // железные зубы: скрежет и щелчок челюстей
        public static AudioClip IronTeeth()
        {
            var a = new Reso();
            return Get("ironteeth", 1.2f, (t, r) =>
            {
                float s = 0f;
                for (int k = 0; k < 3; k++)
                {
                    float tk = t - k * 0.28f;
                    if (tk > 0f) s += a.Run(Noise(r), 2600f, 60f) * Mathf.Exp(-tk * 25f) * 4f;
                }
                return s + Noise(r) * 0.05f * (1f - Smooth(0.8f, 1.2f, t));
            });
        }

        // шурале: скрип деревьев в лесу
        public static AudioClip WoodCreak()
        {
            var body = new Reso();
            return Get("woodcreak", 5f, (t, r) =>
            {
                float f = 140f + 50f * Sin(0.21f, t) + 15f * Sin(2.3f, t);
                float stick = Mathf.Abs(Sin(f * 0.25f, t)) > 0.92f ? 1f : 0.15f;
                float creak = body.Run(Noise(r) * stick, f * 3f, 80f) * 6f;
                return creak * Mathf.Pow(Mathf.Abs(Sin(0.18f, t)), 1.5f);
            }, Rate / 4);
        }

        // шурале: щекочущий быстрый смех
        public static AudioClip Tickle()
        {
            var v = new Voice();
            return Get("tickle", 1.8f, (t, r) =>
            {
                float he = Mathf.Pow(Mathf.Max(0f, Sin(9f, t)), 3f) * (1f - Smooth(1.2f, 1.8f, t));
                return v.Run(640f + 80f * Sin(1.3f, t), 0.75f, 0.4f, 1.3f, r) * he;
            });
        }

        // эль кукуй: скребётся под кроватью и тяжело дышит
        public static AudioClip UnderBed()
        {
            var wood = new Reso();
            float lp = 0f;
            return Get("underbed", 4f, (t, r) =>
            {
                float scratch = Mathf.Pow(Mathf.Abs(Sin(7f + 3f * Sin(0.5f, t), t)), 6f) * (t % 2f < 1.1f ? 1f : 0f);
                lp += 0.03f * (Noise(r) - lp);
                float breath = lp * 4f * Mathf.Pow(Mathf.Max(0f, Sin(0.25f, t)), 2f);
                return wood.Run(Noise(r), 1800f, 900f) * scratch * 3f + breath;
            }, Rate / 8);
        }

        // очень низкий шёпот
        public static AudioClip DeepWhisper()
        {
            float lp = 0f, bp = 0f;
            var chest = new Reso();
            return Get("deepwhisper", 2.4f, (t, r) =>
            {
                lp += 0.05f * (Noise(r) - lp);
                bp += 0.4f * (lp - bp);
                float syl = Mathf.Pow(Mathf.Abs(Sin(2.6f, t)), 3f) * (1f - Smooth(1.9f, 2.4f, t));
                return (chest.Run(lp - bp, 420f, 300f) * 8f + (lp - bp) * 2f) * syl;
            });
        }

        // кучисакэ: щёлканье ножниц
        public static AudioClip Snip()
        {
            var blade = new Reso();
            return Get("snip", 2f, (t, r) =>
            {
                float s = 0f;
                float[] at = { 0.1f, 0.42f, 1.3f };
                foreach (float a in at)
                {
                    float tk = t - a;
                    if (tk > 0f) s += blade.Run(Noise(r), 5200f, 300f) * Mathf.Exp(-tk * 45f) * 5f + Noise(r) * Mathf.Exp(-tk * 200f) * 0.3f;
                }
                return s;
            });
        }

        // «Я красивая?» — три слога с вопросительной интонацией
        public static AudioClip Question()
        {
            var v = new Voice();
            return Get("question", 1.8f, (t, r) =>
            {
                float syl = 0f, f = 260f, vow = 0.5f;
                if (t < 0.32f) { syl = Mathf.Sin(t / 0.32f * Mathf.PI); vow = 0.9f; f = 240f; }
                else if (t > 0.4f && t < 0.7f) { syl = Mathf.Sin((t - 0.4f) / 0.3f * Mathf.PI); vow = 0.2f; f = 250f; }
                else if (t > 0.8f && t < 1.5f) { syl = Mathf.Sin((t - 0.8f) / 0.7f * Mathf.PI); vow = 0.6f; f = 250f + (t - 0.8f) * 200f; }
                return v.Run(f, vow, 0.35f, 1.2f, r) * syl;
            });
        }

        // кровавая мэри: шёпот имени трижды, с эхом зеркала
        public static AudioClip Chant()
        {
            float lp = 0f, bp = 0f;
            var glass = new Reso();
            return Get("chant", 5f, (t, r) =>
            {
                lp += 0.1f * (Noise(r) - lp);
                bp += 0.5f * (lp - bp);
                float phrase = t % 1.6f;
                float syl = phrase < 1.1f ? Mathf.Pow(Mathf.Abs(Sin(2.8f, phrase)), 3f) : 0f;
                float w = (lp - bp) * 3.5f * syl;
                return w + glass.Run(w, 2200f, 120f) * 0.6f;
            }, Rate / 8);
        }

        // треск стекла
        public static AudioClip GlassCrack()
        {
            var a = new Reso();
            var b = new Reso();
            return Get("glass", 1.6f, (t, r) =>
            {
                float s = 0f;
                for (int k = 0; k < 6; k++)
                {
                    float tk = t - k * 0.05f - (k > 3 ? 0.3f : 0f);
                    if (tk > 0f) s += Noise(r) * Mathf.Exp(-tk * 120f);
                }
                return a.Run(s, 3400f, 90f) * 3f + b.Run(s, 5100f, 120f) * 2f + s * 0.3f;
            });
        }

        // убыр, мула: треск огня
        public static AudioClip FireCrackle()
        {
            var roar = new Reso();
            return Get("fire", 4f, (t, r) =>
            {
                float pop = Mathf.Pow((float)r.NextDouble(), 25f) * Mathf.Sign(Noise(r));
                return roar.Run(Noise(r), 300f + 100f * Sin(0.6f, t), 400f) * 1.5f + pop * 0.7f;
            }, Rate / 4);
        }

        public static AudioClip FireWhoosh()
        {
            var b = new Reso();
            return Get("firewhoosh", 1.4f, (t, r) =>
            {
                float env = Smooth(0f, 0.15f, t) * Mathf.Exp(-t * 2f);
                return (float)Math.Tanh(b.Run(Noise(r), 400f + 1200f * Smooth(0f, 0.4f, t), 800f) * 6f) * env;
            });
        }

        // чупакабра: стрекот и шипение
        public static AudioClip Chitter()
        {
            var b = new Reso();
            return Get("chitter", 2f, (t, r) =>
            {
                float clicks = Mathf.Pow(Mathf.Abs(Sin(23f + 8f * Sin(1.1f, t), t)), 12f);
                return b.Run(Noise(r), 2900f, 500f) * clicks * 5f + Noise(r) * 0.05f * Mathf.Abs(Sin(0.5f, t));
            }, Rate / 10);
        }

        public static AudioClip SpineRattle()
        {
            var b = new Reso();
            return Get("spines", 1.2f, (t, r) =>
            {
                float env = Smooth(0f, 0.02f, t) * Mathf.Exp(-t * 2.5f);
                float rattle = Mathf.Pow(Mathf.Abs(Sin(38f, t)), 8f);
                return b.Run(Noise(r), 4200f, 900f) * rattle * env * 6f;
            });
        }

        // цзянши: прыжок — глухой удар и колокольчик на поясе
        public static AudioClip Hop()
        {
            var bell = new Reso();
            return Get("hop", 2.4f, (t, r) =>
            {
                float s = 0f;
                for (int k = 0; k < 3; k++)
                {
                    float tk = t - k * 0.75f;
                    if (tk < 0f) continue;
                    s += (Sin(60f - tk * 30f, tk) + Noise(r) * 0.2f) * Mathf.Exp(-tk * 14f);
                    s += bell.Run(Noise(r) * Mathf.Exp(-tk * 80f), 2350f, 15f) * 3f;
                }
                return s;
            });
        }

        // стригой, асванг: хлопанье крыльев
        public static AudioClip WingFlap()
        {
            var b = new Reso();
            return Get("wings", 1.4f, (t, r) =>
            {
                float flap = Mathf.Pow(Mathf.Max(0f, Sin(5f, t)), 4f) * (1f - Smooth(1f, 1.4f, t));
                return b.Run(Noise(r), 700f, 900f) * flap * 5f;
            });
        }

        // каракончолос: ледяной вой ветра
        public static AudioClip IcyWind()
        {
            var a = new Reso();
            var b = new Reso();
            return Get("icywind", 6f, (t, r) =>
            {
                float n = Noise(r);
                float f = 700f + 400f * Sin(0.17f, t) + 150f * Sin(0.61f, t);
                return a.Run(n, f, 40f) * 2.5f + b.Run(n, f * 1.5f, 60f) * 1.5f;
            }, Rate / 4);
        }

        // зовёт по имени: два слога издалека, голосом близкого
        public static AudioClip NameCall()
        {
            var v = new Voice();
            var far = new Reso();
            return Get("namecall", 2f, (t, r) =>
            {
                float syl = 0f, f = 300f, vow = 0.7f;
                if (t < 0.35f) { syl = Mathf.Sin(t / 0.35f * Mathf.PI); f = 320f; vow = 0.9f; }
                else if (t > 0.45f && t < 1.2f) { syl = Mathf.Sin((t - 0.45f) / 0.75f * Mathf.PI); f = 300f - (t - 0.45f) * 80f; vow = 0.3f; }
                return far.Run(v.Run(f, vow, 0.15f, 1.1f, r) * syl, 800f, 1500f) * 3f;
            });
        }

        // тунда: колыбельная, которую напевает «мама» — чуть мимо нот
        public static AudioClip MotherHum()
        {
            var v = new Voice();
            int[] tune = { 0, 3, 5, 3, 0, -2, 0, 1 };
            return Get("motherhum", 6f, (t, r) =>
            {
                int i = Mathf.Min(tune.Length - 1, (int)(t / 0.7f));
                float nt = (t % 0.7f) / 0.7f;
                float f = 260f * Mathf.Pow(2f, (tune[i] + 0.35f * Sin(0.3f, t)) / 12f);
                float env = Mathf.Sin(nt * Mathf.PI) * (t < 5.6f ? 1f : 0f);
                return v.Run(f, 0.1f, 0.1f, 1.1f, r) * env;
            }, Rate / 8);
        }

        // тунда: шаг ногой — и стук деревянной культи
        public static AudioClip PegStep()
        {
            var peg = new Reso();
            return Get("pegstep", 2.4f, (t, r) =>
            {
                float s = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float tk = t - k * 0.55f;
                    if (tk < 0f) continue;
                    if (k % 2 == 0) s += (Sin(70f, tk) + Noise(r) * 0.3f) * Mathf.Exp(-tk * 20f);
                    else s += peg.Run(Noise(r), 900f, 120f) * Mathf.Exp(-tk * 30f) * 8f;
                }
                return s;
            });
        }

        // асванг: «тик-тик» — чем тише, тем ближе
        public static AudioClip TikTik()
        {
            var b = new Reso();
            return Get("tiktik", 3f, (t, r) =>
            {
                float s = 0f;
                for (int k = 0; k < 6; k++)
                {
                    float tk = t - k * 0.42f - (k > 2 ? 0.6f : 0f);
                    if (tk > 0f) s += Noise(r) * Mathf.Exp(-tk * 300f);
                }
                return b.Run(s, 2100f, 200f) * 4f;
            }, Rate / 20);
        }

        // водяной: пузыри со дна
        public static AudioClip Bubbles()
        {
            var w = new Reso();
            return Get("bubbles", 4f, (t, r) =>
            {
                float s = 0f;
                for (int k = 0; k < 9; k++)
                {
                    float tk = t - k * 0.43f - 0.1f * (k % 3);
                    if (tk < 0f) continue;
                    s += Sin(300f + k * 40f + tk * 900f, tk) * Mathf.Exp(-tk * 25f);
                }
                return s * 0.6f + w.Run(Noise(r), 120f, 60f) * 0.6f;
            }, Rate / 10);
        }

        // водяной: густое кваканье
        public static AudioClip Croak()
        {
            var v = new Voice();
            return Get("croak", 1.4f, (t, r) =>
            {
                float s = 0f;
                for (int k = 0; k < 2; k++)
                {
                    float tk = t - k * 0.55f;
                    if (tk > 0f && tk < 0.4f) s += v.Run(70f, 0.4f, 1f, 0.6f, r) * Mathf.Sin(tk / 0.4f * Mathf.PI);
                }
                return (float)Math.Tanh(s * 3f);
            });
        }

        // абаасы: скрежет железных зубов
        public static AudioClip IronGrind()
        {
            var a = new Reso();
            var b = new Reso();
            return Get("irongrind", 4f, (t, r) =>
            {
                float grind = Mathf.Pow(Mathf.Abs(Sin(0.5f, t)), 2f) * (Mathf.Abs(Sin(41f, t)) > 0.5f ? 1f : 0.3f);
                float n = Noise(r) * grind;
                return a.Run(n, 1700f, 50f) * 2f + b.Run(n, 2900f, 70f) * 1.5f;
            }, Rate / 4);
        }

        // абаасы: якутский хомус — гнусавый звенящий тон с меняющимся «гласным»
        public static AudioClip JawHarp()
        {
            var f1 = new Reso();
            return Get("khomus", 2.5f, (t, r) =>
            {
                float pulse = (t * 98f) % 1f < 0.12f ? 1f : -0.1f;
                float env = Smooth(0f, 0.02f, t) * (1f - Smooth(1.8f, 2.5f, t));
                float formant = 600f + 900f * Mathf.Abs(Sin(1.5f, t));
                return f1.Run(pulse, formant, 60f) * env * 3f;
            });
        }

        // абаасы: удары единственной ноги
        public static AudioClip OneLegHop()
        {
            return Get("onelegh", 2.4f, (t, r) =>
            {
                float s = 0f;
                for (int k = 0; k < 3; k++)
                {
                    float tk = t - k * 0.7f;
                    if (tk > 0f) s += (Sin(38f - tk * 15f, tk) * 1.2f + Noise(r) * 0.4f) * Mathf.Exp(-tk * 9f);
                }
                return s;
            });
        }

        // мула-сем-кабеса: искажённое ржание
        public static AudioClip Neigh()
        {
            var v = new Voice();
            return Get("neigh", 1.8f, (t, r) =>
            {
                float env = Smooth(0f, 0.05f, t) * (1f - Smooth(1.1f, 1.8f, t));
                float f = 900f - 500f * Smooth(0f, 1.4f, t) + 120f * Sin(11f, t);
                return (float)Math.Tanh(v.Run(f, 0.9f, 0.7f, 1.4f, r) * 3f) * env;
            });
        }

        // мула: галоп подков по камню
        public static AudioClip Gallop()
        {
            var shoe = new Reso();
            return Get("gallop", 2f, (t, r) =>
            {
                float s = 0f;
                for (int k = 0; k < 8; k++)
                {
                    float tk = t - (k / 4) * 0.6f - (k % 4) * 0.09f;
                    if (tk > 0f) s += (shoe.Run(Noise(r), 2400f, 300f) * 4f + Sin(90f, tk)) * Mathf.Exp(-tk * 40f);
                }
                return s;
            });
        }

        // пенанггалан: капает с висящих внутренностей
        public static AudioClip WetDrip()
        {
            var wet = new Reso();
            return Get("wetdrip", 4f, (t, r) =>
            {
                float s = 0f;
                float[] at = { 0.3f, 0.9f, 1.4f, 2.5f, 3.1f };
                foreach (float a in at)
                {
                    float tk = t - a;
                    if (tk > 0f) s += wet.Run(Noise(r), 700f + tk * 300f, 200f) * Mathf.Exp(-tk * 18f) * 5f;
                }
                return s;
            }, Rate / 10);
        }

        public static AudioClip WetFlap()
        {
            var b = new Reso();
            return Get("wetflap", 1.4f, (t, r) =>
            {
                float flap = Mathf.Pow(Mathf.Max(0f, Sin(4f, t)), 3f) * (1f - Smooth(1f, 1.4f, t));
                return b.Run(Noise(r), 450f, 500f) * flap * 6f + Mathf.Pow((float)r.NextDouble(), 30f) * flap;
            });
        }

        // ведьма Белла: дрожащий старушечий голос поёт гимн
        public static AudioClip Hymn()
        {
            var v = new Voice();
            int[] tune = { 0, 0, 2, 4, 4, 2, 0, -1, 0 };
            return Get("hymn", 7f, (t, r) =>
            {
                int i = Mathf.Min(tune.Length - 1, (int)(t / 0.75f));
                float nt = (t % 0.75f) / 0.75f;
                float f = 330f * Mathf.Pow(2f, tune[i] / 12f) * (1f + 0.02f * Sin(6.5f, t));
                return v.Run(f, 0.55f, 0.3f, 1.2f, r) * Mathf.Sin(nt * Mathf.PI) * 0.7f;
            }, Rate / 8);
        }

        // звонкая пощёчина
        public static AudioClip Slap()
        {
            var b = new Reso();
            return Get("slap", 0.6f, (t, r) =>
                b.Run(Noise(r), 1500f, 2500f) * Mathf.Exp(-t * 60f) * 6f + Noise(r) * Mathf.Exp(-t * 120f) * 0.6f);
        }

        // жалмауыз: семь голов бормочут наперебой
        public static AudioClip ManyVoices()
        {
            var vs = new Voice[5];
            for (int i = 0; i < vs.Length; i++) vs[i] = new Voice();
            return Get("manyvoices", 5f, (t, r) =>
            {
                float s = 0f;
                for (int i = 0; i < vs.Length; i++)
                {
                    float syl = Mathf.Pow(Mathf.Abs(Sin(3.3f + i * 0.71f, t + i * 0.37f)), 3f);
                    s += vs[i].Run(110f + i * 37f + 10f * Sin(0.5f + i * 0.1f, t), 0.3f + 0.5f * Mathf.Abs(Sin(2f + i, t)), 0.5f, 0.9f + i * 0.06f, r) * syl;
                }
                return s * 0.4f;
            }, Rate / 4);
        }

        // щелчок огромной челюсти
        public static AudioClip Chomp()
        {
            var b = new Reso();
            return Get("chomp", 1f, (t, r) =>
            {
                float s = 0f;
                for (int k = 0; k < 2; k++)
                {
                    float tk = t - k * 0.35f;
                    if (tk > 0f) s += (b.Run(Noise(r), 900f, 700f) * 5f + Sin(80f, tk)) * Mathf.Exp(-tk * 35f);
                }
                return s;
            });
        }
    }
}
