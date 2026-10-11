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
    public class Entity : MonoBehaviour
    {
        public enum State { Lurking, Manifest, Attacking, Retreating, Banished }

        const float AttackDistance = 0.65f;

        public EntityDef Def { get; private set; }
        public State Current { get; private set; }
        public bool Alive => Current != State.Banished;
        public bool Visible => Current == State.Manifest && Def.Kind != EntityKind.Poltergeist;
        public bool Lit { get; set; }
        public float Exposure { get; private set; }
        public float ExposureNeeded => Def.Kind == EntityKind.Upyr ? 2.6f : 1.6f;
        public float Seed { get; private set; }

        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        static readonly int FlipId = Shader.PropertyToID("_Flip");

        Game game;
        Camera cam;
        Material mat;
        MeshRenderer rend;
        AudioSource voice;
        ParticleSystem sparks;
        float stateTime, speed, flash, fade, nextEvent, lurkTime, phaseTime;
        int throwsLeft;
        bool dashing, dropped;
        Vector3 baseScale;

        public static Material CreateMaterial(EntityDef def, float seed)
        {
            var m = new Material(Shader.Find("GhostWard/Entity"));
            m.SetColor("_Body", def.Body);
            m.SetColor("_Color", def.Accent);
            // номера силуэтов в шейдере: у албасты и полтергейста они переставлены относительно EntityKind
            int shape = def.Kind switch { EntityKind.Albasty => 3, EntityKind.Poltergeist => 2, _ => (int)def.Kind };
            m.SetFloat("_Shape", shape);
            m.SetFloat("_Seed", seed);
            return m;
        }

        public static Entity Create(EntityDef def, Vector3 position, Camera cam, Game game, float aggression)
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
                _ => 0f
            };

            e.mat = CreateMaterial(def, e.Seed);
            e.mat.SetFloat(FadeId, 0f);
            e.mat.SetFloat(FlipId, def.Kind == EntityKind.Kikimora ? 1f : 0f);
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
            e.voice.clip = def.Kind switch
            {
                EntityKind.Ghost => ProceduralAudio.Whisper(),
                EntityKind.Shade => ProceduralAudio.Breath(),
                EntityKind.Upyr => ProceduralAudio.Rasp(),
                EntityKind.Mara => ProceduralAudio.Hum(),
                _ => null
            };
            if (e.voice.clip != null)
            {
                e.voice.loop = true;
                e.voice.time = Random.Range(0f, 2f);
                e.voice.Play();
            }

            e.sparks = Effects.CreateSparks(go.transform, def.Accent);
            // мара не подкрадывается — она просто оказывается рядом
            e.Lurk(def.Kind == EntityKind.Mara ? Random.Range(2f, 3f) : Random.Range(4f, 7f));
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
            if (Def.Kind == EntityKind.Kikimora) mat.SetFloat(FlipId, 1f);
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
                    voice.volume = Mathf.Max(0f, 1f - stateTime * 2f);
                    if (stateTime > 1.4f) Destroy(gameObject);
                    break;
            }

            if (Current != State.Banished) Exposure = Mathf.MoveTowards(Exposure, 0f, dt * (Lit ? 0f : 0.4f));
            mat.SetFloat(FlashId, flash);
            mat.SetFloat(FadeId, fade);
        }

        void TickLurking()
        {
            // в засаде сущность только слышна
            nextEvent -= Time.deltaTime;
            if (nextEvent <= 0f)
            {
                nextEvent = Random.Range(1.5f, 2.5f);
                switch (Def.Kind)
                {
                    case EntityKind.Albasty: voice.PlayOneShot(ProceduralAudio.Laugh(), 0.7f); break;
                    case EntityKind.Poltergeist: voice.PlayOneShot(ProceduralAudio.Knock(), 1f); break;
                    case EntityKind.Kikimora: voice.PlayOneShot(ProceduralAudio.Scrape(), 0.9f); break;
                }
            }
            if (stateTime < lurkTime) return;

            SetState(State.Manifest);
            nextEvent = Def.Kind switch
            {
                EntityKind.Poltergeist => Random.Range(3f, 5f),
                EntityKind.Mara => 14f,                 // столько она сидит, прежде чем напасть
                _ => Random.Range(5f, 8f)
            };
            throwsLeft = 3;
            phaseTime = Random.Range(1f, 2f);
            rend.enabled = Def.Kind != EntityKind.Poltergeist;
            game.OnManifest(this);
        }

        void TickManifest(float dt)
        {
            if (Def.Kind != EntityKind.Poltergeist) fade = Mathf.MoveTowards(fade, 1f, dt * (Def.Kind == EntityKind.Mara ? 3f : 1f));
            bool watched = game.IsInView(transform.position);

            switch (Def.Kind)
            {
                case EntityKind.Ghost:
                    Approach(speed);
                    nextEvent -= dt;
                    if (nextEvent < 0.8f) fade = Mathf.Clamp01(nextEvent / 0.8f);
                    if (nextEvent <= 0f)
                    {
                        Relocate(50f, Mathf.Max(1.4f, FlatDistance() * 0.85f), keepState: true);
                        nextEvent = Random.Range(5f, 8f);
                    }
                    break;

                case EntityKind.Shade:
                    if (Lit) Burn(dt);
                    else Approach(speed);
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
                        }
                    }
                    else if (Random.value < 0.04f) transform.position += Random.insideUnitSphere * 0.03f;
                    break;

                case EntityKind.Poltergeist:
                    nextEvent -= dt;
                    if (nextEvent > 0f) break;
                    if (throwsLeft-- > 0)
                    {
                        voice.PlayOneShot(ProceduralAudio.Crash(), 1f);
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
                    if (Random.value < 0.002f) voice.PlayOneShot(ProceduralAudio.Laugh(), 0.4f);
                    break;

                case EntityKind.Kikimora:
                    if (!dropped)
                    {
                        Approach(speed);
                        nextEvent -= dt;
                        if (nextEvent <= 0f) { voice.PlayOneShot(ProceduralAudio.Scrape(), 0.8f); nextEvent = Random.Range(2f, 4f); }
                        if (FlatDistance() < 1.4f)
                        {
                            // падает с потолка прямо перед игроком
                            dropped = true;
                            phaseTime = 0.9f;
                            mat.SetFloat(FlipId, 0f);
                            flash = 0.6f;
                            voice.PlayOneShot(ProceduralAudio.Screech(), 0.5f);
                        }
                    }
                    else
                    {
                        var p = transform.position;
                        p.y = Mathf.MoveTowards(p.y, cam.transform.position.y - 0.2f, dt * 6f);
                        transform.position = p;
                        phaseTime -= dt;
                        if (phaseTime <= 0f) Approach(speed * 2f);
                    }
                    break;

                case EntityKind.Upyr:
                    if (Lit) { Burn(dt); dashing = false; break; }
                    phaseTime -= dt;
                    if (phaseTime <= 0f)
                    {
                        dashing = !dashing;
                        phaseTime = dashing ? 0.5f : Random.Range(1.2f, 2.2f);
                        if (dashing) flash = 0.3f;
                    }
                    if (dashing) Approach(speed);
                    break;

                case EntityKind.Mara:
                    nextEvent -= dt;
                    if (watched && Random.value < 0.03f) transform.position += Random.insideUnitSphere * 0.02f;
                    if (nextEvent <= 0f) { Strike(); return; }
                    break;
            }

            if (Def.Kind is EntityKind.Poltergeist or EntityKind.Mara) return;
            if (FlatDistance() < AttackDistance && !(Def.Kind == EntityKind.Albasty && watched)) Strike();
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
            voice.PlayOneShot(ProceduralAudio.Screech(), 1f);
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
        }

        // ---------- движение ----------

        // по полу и по потолку — без вертикального движения, по воздуху — плавно к лицу
        bool Planar => Def.Kind is EntityKind.Shade or EntityKind.Albasty or EntityKind.Double or EntityKind.Upyr
            || Def.Kind == EntityKind.Kikimora && !dropped;

        void Approach(float v)
        {
            var dir = cam.transform.position - transform.position;
            dir.y = Planar ? 0f : dir.y * 0.4f;
            var step = dir.normalized * (v * Time.deltaTime);
            if (Def.Kind != EntityKind.Upyr)
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
            AudioSource.PlayClipAtPoint(ProceduralAudio.Banish(), transform.position, 1f);
            SetState(State.Banished);
        }

        // неверный предмет: сущность злится и становится быстрее
        public void Provoke()
        {
            if (!Alive) return;
            speed *= 1.35f;
            flash = 0.7f;
            sparks.Emit(8);
            voice.PlayOneShot(ProceduralAudio.Laugh(), 0.8f);
            if (Def.Kind is EntityKind.Poltergeist or EntityKind.Mara) nextEvent = Mathf.Min(nextEvent, 1.5f);
        }

        public void Vanish() => Destroy(gameObject);

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
