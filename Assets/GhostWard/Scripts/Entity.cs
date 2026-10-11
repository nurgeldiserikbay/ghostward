using UnityEngine;

namespace GhostWard
{
    // Одна сущность. Поведение зависит от вида:
    //  призрак — медленно плывёт и «мерцает», исчезая и возникая в другом месте;
    //  тень — быстро идёт в темноте, на свету фонаря замирает;
    //  албасты — двигается только когда игрок на неё не смотрит;
    //  полтергейст — невидим, кидается вещами издалека, его выдаёт только звук.
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

        static readonly int BodyId = Shader.PropertyToID("_Body");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int ShapeId = Shader.PropertyToID("_Shape");
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        static readonly int SeedId = Shader.PropertyToID("_Seed");

        Game game;
        Camera cam;
        Material mat;
        MeshRenderer rend;
        AudioSource voice;
        ParticleSystem sparks;
        float stateTime, speed, seed, flash, fade, nextEvent, lurkTime;
        int throwsLeft;
        Vector3 baseScale;

        public static Entity Create(EntityDef def, Vector3 position, Camera cam, Game game, float aggression)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = def.Name;
            Destroy(go.GetComponent<Collider>());
            go.transform.position = position;
            float h = def.Kind == EntityKind.Albasty || def.Kind == EntityKind.Shade ? 1.7f : 1.15f;
            go.transform.localScale = new Vector3(h * 0.8f, h, 1f);

            var e = go.AddComponent<Entity>();
            e.Def = def;
            e.cam = cam;
            e.game = game;
            e.baseScale = go.transform.localScale;
            e.seed = Random.value * 10f;
            e.speed = aggression * def.Kind switch
            {
                EntityKind.Ghost => 0.28f,
                EntityKind.Shade => 0.55f,
                EntityKind.Albasty => 1.0f,
                _ => 0f
            };

            e.mat = new Material(Shader.Find("GhostWard/Entity"));
            e.mat.SetColor(BodyId, def.Body);
            e.mat.SetColor(ColorId, def.Accent);
            e.mat.SetFloat(ShapeId, def.Shape);
            e.mat.SetFloat(SeedId, e.seed);
            e.mat.SetFloat(FadeId, 0f);
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
                _ => null
            };
            if (e.voice.clip != null)
            {
                e.voice.loop = true;
                e.voice.time = Random.Range(0f, 2f);
                e.voice.Play();
            }

            e.sparks = Effects.CreateSparks(go.transform, def.Accent);
            e.Lurk(Random.Range(4f, 7f));
            return e;
        }

        // ---------- состояния ----------

        void Lurk(float seconds)
        {
            SetState(State.Lurking);
            lurkTime = seconds;
            rend.enabled = false;
            fade = 0f;
            nextEvent = Random.Range(0.5f, 1.5f);
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
            // в засаде сущность только слышна: стук, смех
            nextEvent -= Time.deltaTime;
            if (nextEvent <= 0f)
            {
                nextEvent = Random.Range(1.5f, 2.5f);
                if (Def.Kind == EntityKind.Albasty) voice.PlayOneShot(ProceduralAudio.Laugh(), 0.7f);
                if (Def.Kind == EntityKind.Poltergeist) voice.PlayOneShot(ProceduralAudio.Knock(), 1f);
            }
            if (stateTime < lurkTime) return;

            SetState(State.Manifest);
            nextEvent = Def.Kind == EntityKind.Poltergeist ? Random.Range(3f, 5f) : Random.Range(5f, 8f);
            throwsLeft = 3;
            rend.enabled = Def.Kind != EntityKind.Poltergeist;
            game.OnManifest(this);
        }

        void TickManifest(float dt)
        {
            if (Def.Kind != EntityKind.Poltergeist) fade = Mathf.MoveTowards(fade, 1f, dt);

            switch (Def.Kind)
            {
                case EntityKind.Ghost:
                    Approach(speed);
                    nextEvent -= dt;
                    if (nextEvent < 0.8f) fade = Mathf.Clamp01(nextEvent / 0.8f);
                    if (nextEvent <= 0f)
                    {
                        // растаял и возник в другом месте, чуть ближе
                        Relocate(50f, Mathf.Max(1.4f, FlatDistance() * 0.85f), keepState: true);
                        nextEvent = Random.Range(5f, 8f);
                    }
                    break;

                case EntityKind.Shade:
                    if (Lit)
                    {
                        Exposure += dt;
                        flash = Mathf.Max(flash, 0.25f + 0.25f * Mathf.Sin(Time.time * 40f));
                    }
                    else Approach(speed);
                    break;

                case EntityKind.Albasty:
                    if (!game.IsInView(transform.position))
                    {
                        Approach(speed);
                        nextEvent -= dt;
                        // пока не смотрят, может зайти за спину
                        if (nextEvent <= 0f && FlatDistance() > 1.6f)
                        {
                            Relocate(140f, FlatDistance() * 0.8f, keepState: true);
                            nextEvent = Random.Range(6f, 10f);
                        }
                    }
                    else if (Random.value < 0.04f)
                    {
                        // замерла под взглядом, но подёргивается
                        transform.position += Random.insideUnitSphere * 0.03f;
                    }
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
            }

            if (Def.Kind == EntityKind.Poltergeist) return;
            bool watched = game.IsInView(transform.position);
            if (FlatDistance() < AttackDistance && !(Def.Kind == EntityKind.Albasty && watched))
            {
                SetState(State.Attacking);
                fade = 1f;
                flash = 0.8f;
                voice.PlayOneShot(ProceduralAudio.Screech(), 1f);
                game.OnAttack(this);
            }
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

        // тень и албасты ходят по полу, призрак плывёт по воздуху
        bool Grounded => Def.Kind is EntityKind.Shade or EntityKind.Albasty;

        void Approach(float v)
        {
            var dir = cam.transform.position - transform.position;
            dir.y = Grounded ? 0f : dir.y * 0.4f;
            var step = dir.normalized * (v * Time.deltaTime);
            var side = Vector3.Cross(Vector3.up, dir.normalized);
            step += side * (Mathf.Sin(Time.time * 1.3f + seed) * 0.15f * Time.deltaTime);
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
            if (Def.Kind == EntityKind.Poltergeist) nextEvent = Mathf.Min(nextEvent, 1f);
        }

        public void Vanish() => Destroy(gameObject);

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
