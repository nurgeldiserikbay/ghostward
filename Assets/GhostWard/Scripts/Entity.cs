using UnityEngine;

namespace GhostWard
{
    // Одна сущность. Поведение зависит от вида:
    //  призрак — плывёт и «мерцает», исчезая и возникая ближе;
    //  тень — быстро идёт в темноте, на свету замирает;
    //  албасты — двигается только когда игрок на неё не смотрит;
    //  полтергейст — невидим, кидается вещами, выдаёт себя звуком;
    //  двойник — подходит, только пока игрок поворачивается;
    //  кикимора — ползёт по потолку и падает сверху;
    //  упырь — рывки: замер, бросок, замер;
    //  мара — появляется рядом и сидит; страх растёт, пока на неё смотрят.
    // Остальные ходят по одной из этих повадок (Arch) со своей особенностью:
    //  банши — как призрак, но воет, возникая на новом месте; плакальщица — как призрак, но по полу;
    //  чёрный пёс и гуль — как тень (пса видно только в ночном режиме или тепловизоре);
    //  подменыш, жезтырнак, кицунэ — как албасты; корпу-секу — как упырь; мыстан — как двойник;
    //  домовой — как полтергейст; джинн — кружит вокруг, сужая круги;
    //  русалка — под взглядом уходит «под воду» и всплывает ближе.
    public class Entity : MonoBehaviour
    {
        public enum State { Lurking, Manifest, Attacking, Retreating, Banished }

        const float AttackDistance = 0.65f;

        public EntityDef Def { get; private set; }
        public State Current { get; private set; }
        public bool Alive => Current != State.Banished;
        public bool Visible => Current == State.Manifest && !Def.Invisible;
        public bool Lit { get; set; }
        public float Exposure { get; private set; }
        public float ExposureNeeded => (Def.Kind == EntityKind.Upyr ? 2.6f : 1.6f) * (Weakened ? 0.5f : 1f);
        public bool Weakened { get; set; }          // игрок верно назвал её имя
        public float SpeedMul { get; set; } = 1f;   // затаился — медленнее, зашумел — быстрее
        public float Seed { get; private set; }

        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        static readonly int FlipId = Shader.PropertyToID("_Flip");
        static readonly int WorldId = Shader.PropertyToID("_World");
        static readonly int DimId = Shader.PropertyToID("_Dim");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        GameObject shadow;
        Material shadowMat;
        float blink;   // призраки иногда «моргают»: исчезают на долю секунды

        Game game;
        Camera cam;
        Material mat;
        MeshRenderer rend;
        AudioSource voice;
        ParticleSystem sparks;
        float stateTime, speed, flash, fade, nextEvent, lurkTime, phaseTime;
        int throwsLeft;
        bool dashing, dropped;
        float orbit, watchedTime;
        float shiftTime, shiftMax;  // игрок пошёл: фигура рассыпается и переносится   // джинн: угол на круге; русалка: сколько на неё уже смотрят
        Vector3 baseScale;

        // повадка, по которой ходит сущность
        public static EntityKind ArchOf(EntityKind k) => k switch
        {
            EntityKind.Banshee or EntityKind.Llorona or EntityKind.Tunda or EntityKind.Pontianak or EntityKind.Karakoncolos
                => EntityKind.Ghost,
            EntityKind.BlackDog or EntityKind.Ghoul or EntityKind.Cucuy or EntityKind.ZhalmauyzKempir or EntityKind.BabaYaga
                => EntityKind.Shade,
            EntityKind.Changeling or EntityKind.Zheztyrnak or EntityKind.Kitsune or EntityKind.Aswang or EntityKind.Shurale
                => EntityKind.Albasty,
            EntityKind.CorpoSeco or EntityKind.Chupacabra or EntityKind.MulaSemCabeca or EntityKind.Jiangshi
                or EntityKind.Abaasy or EntityKind.Strigoi => EntityKind.Upyr,
            EntityKind.MystanKempir => EntityKind.Double,
            EntityKind.Domovoi or EntityKind.BellWitch => EntityKind.Poltergeist,
            EntityKind.Lechuza => EntityKind.Kikimora,
            EntityKind.Kuchisake => EntityKind.Mara,
            EntityKind.Penanggalan or EntityKind.Ubyr => EntityKind.Jinn,
            EntityKind.BloodyMary or EntityKind.Vodyanoy => EntityKind.Rusalka,
            _ => k
        };

        EntityKind Arch => ArchOf(Def.Kind);

        // Облик: фотореалистичная картинка Resources/Entities/<Kind> (шейдер EntityPhoto с эффектами плёнки),
        // а пока её нет — процедурный силуэт. У новых видов без своего силуэта — похожий из первых двадцати.
        public static Material CreateMaterial(EntityDef def, float seed)
        {
            var picture = Picture(def.Kind);
            if (picture != null)
            {
                var pm = new Material(Shader.Find("GhostWard/EntityPhoto"));
                pm.SetTexture("_MainTex", picture);
                // PNG с прозрачностью — фон уже вырезан; иначе вырезаем почти чистый чёрный
                pm.SetFloat("_Key", UnityEngine.Experimental.Rendering.GraphicsFormatUtility.HasAlphaChannel(picture.graphicsFormat) ? 0f : 1f);
                pm.SetColor("_Color", def.Accent);
                pm.SetFloat("_Seed", seed);
                return pm;
            }

            var m = new Material(Shader.Find("GhostWard/Entity"));
            m.SetColor("_Body", def.Body);
            m.SetColor("_Color", def.Accent);
            m.SetFloat("_Shape", Silhouette(def.Kind));
            m.SetFloat("_Seed", seed);
            return m;
        }

        static readonly System.Collections.Generic.Dictionary<EntityKind, Texture2D> Pictures = new();

        public static Texture2D Picture(EntityKind k)
        {
            if (!Pictures.TryGetValue(k, out var tex))
                Pictures[k] = tex = Resources.Load<Texture2D>("Entities/" + k);
            return tex;
        }

        // номер процедурного силуэта (у албасты и полтергейста переставлены относительно EntityKind)
        static int Silhouette(EntityKind k) => k switch
        {
            EntityKind.Albasty => 3,
            EntityKind.Poltergeist => 2,
            EntityKind.Lechuza => 5,
            EntityKind.Pontianak => 12,
            EntityKind.BabaYaga => 18,
            EntityKind.Shurale => 1,
            EntityKind.Cucuy => 1,
            EntityKind.Kuchisake => 3,
            EntityKind.BloodyMary => 14,
            EntityKind.Ubyr => 19,
            EntityKind.Chupacabra => 15,
            EntityKind.Jiangshi => 17,
            EntityKind.Strigoi => 6,
            EntityKind.Karakoncolos => 9,
            EntityKind.Tunda => 0,
            EntityKind.Aswang => 15,
            EntityKind.Vodyanoy => 9,
            EntityKind.Abaasy => 6,
            EntityKind.MulaSemCabeca => 11,
            EntityKind.Penanggalan => 8,
            EntityKind.BellWitch => 14,
            EntityKind.ZhalmauyzKempir => 18,
            _ => (int)k
        };

        public static Entity Create(EntityDef def, Vector3 position, Camera cam, Game game, float aggression, float lurk = -1f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = def.Name;
            Destroy(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.localScale = new Vector3(def.Height * 0.8f, def.Height, 1f);

            var e = go.AddComponent<Entity>();
            e.Def = def;
            e.cam = cam;
            e.game = game;
            e.baseScale = go.transform.localScale;
            e.Seed = Random.value * 10f;
            e.speed = aggression * def.Kind switch
            {
                EntityKind.Ghost => 0.28f,
                EntityKind.Shade => 0.55f,
                EntityKind.Albasty => 1.0f,
                EntityKind.Double => 1.4f,
                EntityKind.Kikimora => 0.45f,
                EntityKind.Upyr => 2.2f,
                EntityKind.Banshee => 0.22f,
                EntityKind.Llorona => 0.35f,
                EntityKind.BlackDog => 0.75f,
                EntityKind.Ghoul => 0.6f,
                EntityKind.Changeling => 0.8f,
                EntityKind.Zheztyrnak => 1.25f,
                EntityKind.Kitsune => 1.1f,
                EntityKind.CorpoSeco => 1.9f,
                EntityKind.MystanKempir => 1.2f,
                EntityKind.Jinn => 0.35f,
                EntityKind.Rusalka => 0.4f,
                EntityKind.Lechuza => 0.55f,
                EntityKind.Pontianak => 0.3f,
                EntityKind.BabaYaga => 0.5f,
                EntityKind.Shurale => 1.3f,
                EntityKind.Cucuy => 0.65f,
                EntityKind.BloodyMary => 0.45f,
                EntityKind.Ubyr => 0.45f,
                EntityKind.Chupacabra => 2.4f,
                EntityKind.Jiangshi => 1.6f,
                EntityKind.Strigoi => 2.0f,
                EntityKind.Karakoncolos => 0.3f,
                EntityKind.Tunda => 0.32f,
                EntityKind.Aswang => 1.15f,
                EntityKind.Vodyanoy => 0.38f,
                EntityKind.Abaasy => 1.8f,
                EntityKind.MulaSemCabeca => 2.6f,
                EntityKind.Penanggalan => 0.5f,
                EntityKind.ZhalmauyzKempir => 0.55f,
                _ => 0f
            };

            e.mat = CreateMaterial(def, e.Seed);
            e.mat.SetFloat(FadeId, 0f);
            e.mat.SetFloat(WorldId, 1f);   // в мире — та же «камера», что и комната

            // тень на полу под ногами
            if (def.Stance == Stance.Floor)
            {
                e.shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                e.shadow.name = "Shadow";
                Destroy(e.shadow.GetComponent<Collider>());
                e.shadow.transform.localScale = new Vector3(def.Height * 0.75f, def.Height * 0.32f, 1f);
                e.shadowMat = new Material(Shader.Find("GhostWard/Shadow"));
                var sr = e.shadow.GetComponent<MeshRenderer>();
                sr.sharedMaterial = e.shadowMat;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            e.mat.SetFloat(FlipId, ArchOf(def.Kind) == EntityKind.Kikimora ? 1f : 0f);
            e.rend = go.GetComponent<MeshRenderer>();
            e.rend.sharedMaterial = e.mat;
            e.rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            e.rend.receiveShadows = false;
            e.rend.enabled = false;

            e.voice = go.AddComponent<AudioSource>();
            e.voice.spatialBlend = 1f;
            e.voice.rolloffMode = AudioRolloffMode.Linear;
            e.voice.minDistance = 0.7f;
            e.voice.maxDistance = 9f;
            e.voice.dopplerLevel = 0f;
            e.voice.volume = Settings.Sfx;
            e.voice.clip = EntitySounds.Loop(def.Kind);
            if (e.voice.clip != null)
            {
                e.voice.loop = true;
                e.voice.time = Random.Range(0f, 2f);
                e.voice.Play();
            }

            e.sparks = Effects.CreateSparks(go.transform, def.Accent);
            // мара не подкрадывается — она просто оказывается рядом
            e.Lurk(lurk > 0f ? lurk : ArchOf(def.Kind) == EntityKind.Mara ? Random.Range(2f, 3f) : Random.Range(4f, 7f));
            return e;
        }

        // ---------- состояния ----------

        void Lurk(float seconds)
        {
            SetState(State.Lurking);
            lurkTime = seconds;
            rend.enabled = false;
            fade = 0f;
            dropped = false;
            nextEvent = Random.Range(0.5f, 1.5f);
            if (Arch == EntityKind.Kikimora) mat.SetFloat(FlipId, 1f);
        }

        void SetState(State s)
        {
            Current = s;
            stateTime = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            stateTime += dt;
            flash = Mathf.MoveTowards(flash, 0f, dt * 3f);

            if (shiftTime > 0f)
            {
                shiftTime -= dt;
                mat.SetFloat(DissolveId, 1f - Mathf.Clamp01(shiftTime / 0.45f));
                if (shiftTime > 0f) return;
                mat.SetFloat(DissolveId, 0f);
                transform.localScale = baseScale;
                Relocate(60f, shiftMax);
                return;
            }

            switch (Current)
            {
                case State.Lurking: TickLurking(); break;
                case State.Manifest: TickManifest(dt); break;
                case State.Attacking: TickAttacking(); break;
                case State.Retreating:
                    if (stateTime > 0.3f) Relocate(100f, -1f);
                    break;
                case State.Banished:
                    mat.SetFloat(DissolveId, Mathf.Clamp01(stateTime / 0.9f));
                    transform.position += Vector3.up * dt * 0.3f;
                    voice.volume = Mathf.Max(0f, 1f - stateTime * 2f) * Settings.Sfx;
                    if (stateTime > 1.4f) Destroy(gameObject);
                    break;
            }

            if (Current != State.Banished) Exposure = Mathf.MoveTowards(Exposure, 0f, dt * (Lit ? 0f : 0.4f));
            mat.SetFloat(FlashId, flash);
            mat.SetFloat(FadeId, fade);
            // вдали — темнее, как в настоящей комнате
            mat.SetFloat(DimId, Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(1.5f, 6f, FlatDistance())));
        }

        void TickLurking()
        {
            // в засаде сущность только слышна
            nextEvent -= Time.deltaTime;
            if (nextEvent <= 0f)
            {
                nextEvent = Random.Range(1.5f, 2.5f);
                var call = EntitySounds.Call(Def.Kind);
                if (call != null) voice.PlayOneShot(call, 0.85f);
            }
            if (stateTime < lurkTime) return;

            SetState(State.Manifest);
            nextEvent = Arch switch
            {
                EntityKind.Poltergeist => Random.Range(3f, 5f),
                EntityKind.Mara => 14f,                 // столько она сидит, прежде чем напасть
                _ => Random.Range(5f, 8f)
            };
            throwsLeft = 3;
            phaseTime = Random.Range(1f, 2f);
            rend.enabled = !Def.Invisible;
            watchedTime = 0f;
            game.OnManifest(this);
        }

        void TickManifest(float dt)
        {
            // чёрного пса видно только в ночном режиме или тепловизоре — иначе едва заметная тень
            float maxFade = Def.Kind == EntityKind.BlackDog && !game.SeesHidden ? 0.12f : 1f;
            if (!Def.Invisible) fade = Mathf.MoveTowards(fade, maxFade, dt * (Arch == EntityKind.Mara ? 3f : 1f));
            bool watched = game.IsInView(transform.position);

            // фонарь жжёт тех, кто боится света, и останавливает их
            if (Lit && Def.Bane == ItemKind.Lantern) { Burn(dt); dashing = false; return; }

            if (Arch == EntityKind.Jinn) { TickJinn(dt); return; }
            if (Arch == EntityKind.Rusalka) { TickRusalka(dt, watched); return; }

            switch (Arch)
            {
                case EntityKind.Ghost:
                    Approach(speed);
                    // медленно плывёт из стороны в сторону
                    var toCam = cam.transform.position - transform.position;
                    toCam.y = 0f;
                    if (toCam.sqrMagnitude > 1e-4f)
                        transform.position += Vector3.Cross(Vector3.up, toCam.normalized) * (Mathf.Sin(Time.time * 0.45f + Seed) * 0.5f * dt * SpeedMul);
                    // и иногда «моргает» — пропадает на миг
                    if (blink > 0f) { blink -= dt; fade = Mathf.MoveTowards(fade, 0f, dt * 10f); }
                    else if (Random.value < dt * 0.3f) blink = Random.Range(0.15f, 0.5f);
                    nextEvent -= dt;
                    if (nextEvent < 0.8f) fade = Mathf.Clamp01(nextEvent / 0.8f);
                    if (nextEvent <= 0f)
                    {
                        Relocate(50f, Mathf.Max(1.4f, FlatDistance() * 0.85f), keepState: true);
                        nextEvent = Random.Range(5f, 8f);
                        // банши воет, возникнув на новом месте
                        if (Def.Kind == EntityKind.Banshee) { voice.PlayOneShot(EntitySounds.Special(Def.Kind), 1f); game.OnWail(this); }
                    }
                    break;

                case EntityKind.Shade:
                    Approach(speed);
                    break;

                case EntityKind.Albasty:
                    if (!watched)
                    {
                        Approach(speed);
                        nextEvent -= dt;
                        if (nextEvent <= 0f && FlatDistance() > 1.6f)
                        {
                            Relocate(140f, FlatDistance() * 0.8f, keepState: true);
                            nextEvent = Random.Range(6f, 10f);
                            var jump = EntitySounds.Special(Def.Kind);   // кицунэ кричит, оказавшись за спиной
                            if (jump != null) voice.PlayOneShot(jump, 1f);
                        }
                    }
                    else if (Random.value < 0.04f) transform.position += Jitter(0.03f);
                    break;

                case EntityKind.Poltergeist:
                    nextEvent -= dt * SpeedMul;
                    if (nextEvent > 0f) break;
                    if (throwsLeft-- > 0)
                    {
                        voice.PlayOneShot(EntitySounds.Special(Def.Kind) ?? ProceduralAudio.Crash(), 1f);
                        game.OnThrow(this);
                        nextEvent = Random.Range(4f, 6.5f);
                    }
                    else
                    {
                        game.OnAttack(this);
                        Relocate(60f, -1f, keepState: true);
                        throwsLeft = 3;
                        nextEvent = Random.Range(4f, 6f);
                    }
                    break;

                case EntityKind.Double:
                    // повторяет движения игрока: идёт, только пока тот поворачивается
                    float turn = Mathf.Clamp01(game.TurnSpeed / 60f);
                    if (turn > 0.05f) Approach(speed * turn);
                    if (Random.value < 0.002f) voice.PlayOneShot(EntitySounds.Call(Def.Kind) ?? ProceduralAudio.Laugh(), 0.5f);
                    break;

                case EntityKind.Kikimora:
                    if (!dropped)
                    {
                        Approach(speed);
                        nextEvent -= dt;
                        if (nextEvent <= 0f) { voice.PlayOneShot(EntitySounds.Call(Def.Kind) ?? ProceduralAudio.Scrape(), 0.8f); nextEvent = Random.Range(2f, 4f); }
                        if (FlatDistance() < 1.4f)
                        {
                            // падает с потолка прямо перед игроком
                            dropped = true;
                            phaseTime = 0.9f;
                            mat.SetFloat(FlipId, 0f);
                            flash = 0.6f;
                            voice.PlayOneShot(EntitySounds.Attack(Def.Kind), 0.5f);
                        }
                    }
                    else
                    {
                        var p = transform.position;
                        p.y = Mathf.MoveTowards(p.y, game.Floor + Def.Height * 0.5f, dt * 6f);   // падает на пол перед игроком
                        transform.position = p;
                        phaseTime -= dt;
                        if (phaseTime <= 0f) Approach(speed * 2f);
                    }
                    break;

                case EntityKind.Upyr:
                    phaseTime -= dt;
                    if (phaseTime <= 0f)
                    {
                        dashing = !dashing;
                        phaseTime = dashing ? 0.5f : Random.Range(1.2f, 2.2f);
                        if (dashing)
                        {
                            flash = 0.3f;
                            var snap = EntitySounds.Special(Def.Kind);   // корпу-секу: хруст сухих костей
                            if (snap != null) voice.PlayOneShot(snap, 0.9f);
                        }
                    }
                    if (dashing) Approach(speed);
                    break;

                case EntityKind.Mara:
                    nextEvent -= dt;
                    if (watched && Random.value < 0.03f) transform.position += Jitter(0.02f);
                    if (nextEvent <= 0f) { Strike(); return; }
                    break;
            }

            if (Arch is EntityKind.Poltergeist or EntityKind.Mara) return;
            if (FlatDistance() < AttackDistance && !(Arch == EntityKind.Albasty && watched)) Strike();
        }

        // джинн: вихрь кружит вокруг игрока, с каждым кругом ближе
        void TickJinn(float dt)
        {
            var c = cam.transform.position;
            var off = transform.position - c;
            float r = new Vector2(off.x, off.z).magnitude;
            // угол берём из текущего места: после отступления джинн продолжает круг оттуда, где оказался
            orbit = Mathf.Atan2(off.x, off.z) * Mathf.Rad2Deg + dt * (35f + 30f / Mathf.Max(r, 0.5f)) * SpeedMul;
            r = Mathf.Max(0f, r - speed * SpeedMul * dt);
            var p = c + Quaternion.Euler(0f, orbit, 0f) * Vector3.forward * r;
            p.y = Mathf.Lerp(transform.position.y, c.y + Mathf.Sin(Time.time * 1.7f + Seed) * 0.25f, dt * 2f);
            transform.position = p;
            nextEvent -= dt;
            if (nextEvent <= 0f) { voice.PlayOneShot(EntitySounds.Special(Def.Kind), 0.9f); nextEvent = Random.Range(4f, 7f); }
            if (FlatDistance() < AttackDistance) Strike();
        }

        // русалка: под долгим взглядом уходит «под воду» и всплывает ближе, с другой стороны
        void TickRusalka(float dt, bool watched)
        {
            watchedTime = watched ? watchedTime + dt : Mathf.Max(0f, watchedTime - dt);
            if (watchedTime > 1.4f)
            {
                fade = Mathf.MoveTowards(fade, 0f, dt * 2f);
                if (fade <= 0.01f)
                {
                    voice.PlayOneShot(EntitySounds.Special(Def.Kind), 0.9f);
                    Relocate(70f, Mathf.Max(1.0f, FlatDistance() * 0.7f), keepState: true);
                    watchedTime = 0f;
                }
                return;
            }
            Approach(speed);
            if (FlatDistance() < AttackDistance) Strike();
        }

        // дрожь на месте: у стоящих на полу — без вертикали, иначе фигура и тень «уплывают» с пола
        Vector3 Jitter(float r)
        {
            var j = Random.insideUnitSphere * r;
            if (Planar) j.y = 0f;
            return j;
        }

        void Burn(float dt)
        {
            Exposure += dt;
            flash = Mathf.Max(flash, 0.25f + 0.25f * Mathf.Sin(Time.time * 40f));
        }

        void Strike()
        {
            SetState(State.Attacking);
            rend.enabled = true;
            fade = 1f;
            flash = 0.8f;
            mat.SetFloat(FlipId, 0f);
            voice.PlayOneShot(EntitySounds.Attack(Def.Kind), 1f);
            game.OnAttack(this);
        }

        void TickAttacking()
        {
            // бросок в лицо
            var target = cam.transform.position + cam.transform.forward * 0.42f;
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * 14f);
            transform.localScale = baseScale * (1f + stateTime * 0.6f);
            if (stateTime > 0.7f)
            {
                transform.localScale = baseScale;
                rend.enabled = false;
                SetState(State.Retreating);
            }
        }

        void LateUpdate()
        {
            var dir = transform.position - cam.transform.position;
            if (dir.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            if (shadow != null)
            {
                var p = transform.position;
                p.y -= baseScale.y * 0.5f - 0.01f;
                shadow.transform.position = p;
                shadow.transform.rotation = Quaternion.Euler(90f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f);
                bool visible = rend.enabled && Current is not (State.Attacking or State.Banished) && !(Arch == EntityKind.Kikimora && !dropped);
                shadowMat.SetFloat(AlphaId, visible ? fade * 0.55f : 0f);
            }
        }

        // ---------- движение ----------

        // по полу и по потолку — без вертикального движения, по воздуху — плавно к лицу
        bool Planar => Def.Stance == Stance.Floor || Arch == EntityKind.Kikimora && !dropped;

        void Approach(float v)
        {
            var dir = cam.transform.position - transform.position;
            dir.y = Planar ? 0f : dir.y * 0.4f;
            var step = dir.normalized * (v * SpeedMul * Time.deltaTime);
            if (Arch != EntityKind.Upyr)
            {
                var side = Vector3.Cross(Vector3.up, dir.normalized);
                step += side * (Mathf.Sin(Time.time * 1.3f + Seed) * 0.15f * Time.deltaTime);
            }
            transform.position += step;
        }

        // переставить в тёмное место комнаты не ближе minAngle к направлению взгляда
        void Relocate(float minAngle, float maxDist, bool keepState = false)
        {
            transform.position = game.PickPosition(Def, minAngle, maxDist);
            if (keepState) { fade = 0f; return; }
            Lurk(Random.Range(3f, 5f));
        }

        public float FlatDistance()
        {
            var d = transform.position - cam.transform.position;
            d.y *= 0.4f;
            return d.magnitude;
        }

        // ---------- реакция на предметы ----------

        public void Banish()
        {
            if (!Alive) return;
            rend.enabled = true;
            fade = 1f;
            flash = 1f;
            sparks.Emit(40);
            AudioSource.PlayClipAtPoint(ProceduralAudio.Banish(), transform.position, Settings.Sfx);
            SetState(State.Banished);
        }

        // неверный предмет: сущность злится и становится быстрее
        public void Provoke()
        {
            if (!Alive) return;
            speed *= 1.35f;
            flash = 0.7f;
            sparks.Emit(8);
            voice.PlayOneShot(EntitySounds.Call(Def.Kind) ?? ProceduralAudio.Laugh(), 0.9f);
            if (Arch is EntityKind.Poltergeist or EntityKind.Mara) nextEvent = Mathf.Min(nextEvent, 1.5f);
        }

        public void Vanish() => Destroy(gameObject);

        // игрок пошёл (режим гироскопа): видимая фигура рассыпается и через несколько секунд ждёт в другом месте,
        // не дальше maxDist (-1 — где угодно); затаившуюся не трогаем
        public void Shift(float maxDist)
        {
            if (!Alive || shiftTime > 0f || !rend.enabled) return;
            shiftTime = 0.45f;
            shiftMax = maxDist;
            voice.PlayOneShot(EntitySounds.Call(Def.Kind) ?? ProceduralAudio.Laugh(), 0.5f);
        }

        // «второй шанс»: сущность отступает и снова прячется
        public void Repel()
        {
            if (!Alive) return;
            rend.enabled = false;
            transform.localScale = baseScale;
            SetState(State.Retreating);
        }

        void OnDestroy()
        {
            if (shadow != null) Destroy(shadow);
            if (shadowMat != null) Destroy(shadowMat);
            if (mat != null) Destroy(mat);
        }
    }
}
