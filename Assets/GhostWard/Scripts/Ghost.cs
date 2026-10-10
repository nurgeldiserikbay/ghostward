using System;
using UnityEngine;

namespace GhostWard
{
    public class Ghost : MonoBehaviour
    {
        enum State { Appearing, Hunting, Banished, Striking }

        const float Height = 1.1f;
        const float StrikeDistance = 0.7f;

        public GhostDef Def { get; private set; }
        public bool Alive => state is State.Appearing or State.Hunting;

        public event Action<Ghost> Struck;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int ShapeId = Shader.PropertyToID("_Shape");
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        static readonly int SeedId = Shader.PropertyToID("_Seed");

        State state;
        float stateTime;
        float speed;
        float flash;
        float nextBlink;
        float seed;
        Transform cam;
        Material mat;
        AudioSource voice;
        ParticleSystem sparks;

        public static Ghost Create(GhostDef def, Vector3 position, Transform cam, float speedMul)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Ghost " + def.Name;
            Destroy(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.localScale = new Vector3(Height * 0.85f, Height, 1f);

            var g = go.AddComponent<Ghost>();
            g.Def = def;
            g.cam = cam;
            g.speed = def.Speed * speedMul;
            g.seed = UnityEngine.Random.value * 10f;

            g.mat = new Material(Shader.Find("GhostWard/Ghost"));
            g.mat.SetColor(ColorId, def.Color);
            g.mat.SetFloat(ShapeId, def.Shape);
            g.mat.SetFloat(SeedId, g.seed);
            g.mat.SetFloat(FadeId, 0f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = g.mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            g.voice = go.AddComponent<AudioSource>();
            g.voice.clip = ProceduralAudio.Whisper(def.Pitch);
            g.voice.loop = true;
            g.voice.spatialBlend = 1f;
            g.voice.rolloffMode = AudioRolloffMode.Linear;
            g.voice.minDistance = 0.8f;
            g.voice.maxDistance = 9f;
            g.voice.dopplerLevel = 0f;
            g.voice.volume = 0.9f;
            g.voice.time = UnityEngine.Random.Range(0f, 3f);
            g.voice.Play();
            g.voice.PlayOneShot(ProceduralAudio.Spawn(), 0.7f);

            g.sparks = Effects.CreateSparks(go.transform, def.Color);
            g.nextBlink = UnityEngine.Random.Range(2.5f, 4f);
            return g;
        }

        void Update()
        {
            stateTime += Time.deltaTime;
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 4f);

            switch (state)
            {
                case State.Appearing:
                    mat.SetFloat(FadeId, Mathf.Clamp01(stateTime / 1.2f));
                    Approach(0.3f);
                    if (stateTime > 1.2f) SetState(State.Hunting);
                    break;

                case State.Hunting:
                    Approach(1f);
                    if (Def.Kind == GhostKind.Poltergeist) Blink();
                    if (FlatDistance() < StrikeDistance) Strike();
                    break;

                case State.Banished:
                    mat.SetFloat(DissolveId, Mathf.Clamp01(stateTime / 0.7f));
                    transform.position += Vector3.up * Time.deltaTime * 0.4f;
                    voice.volume = Mathf.Max(0f, 0.9f - stateTime * 2f);
                    if (stateTime > 1.2f) Destroy(gameObject);
                    break;

                case State.Striking:
                    // бросок в лицо и исчезновение
                    transform.position = Vector3.Lerp(transform.position, cam.position + cam.forward * 0.25f, Time.deltaTime * 12f);
                    mat.SetFloat(FadeId, Mathf.Clamp01(1f - stateTime / 0.35f));
                    if (stateTime > 0.5f) Destroy(gameObject);
                    break;
            }

            mat.SetFloat(FlashId, flash);
        }

        void LateUpdate()
        {
            var dir = transform.position - cam.position;
            if (dir.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        void Approach(float k)
        {
            var target = cam.position;
            var dir = target - transform.position;
            dir.y *= 0.5f;
            var step = dir.normalized * (speed * k * Time.deltaTime);
            // покачивание по сторонам, чтобы движение не было линейным
            var side = Vector3.Cross(Vector3.up, dir.normalized);
            step += side * (Mathf.Sin(Time.time * 1.7f + seed) * 0.25f * Time.deltaTime);
            step.y += Mathf.Sin(Time.time * 2.3f + seed) * 0.08f * Time.deltaTime;
            transform.position += step;
        }

        void Blink()
        {
            if (stateTime < nextBlink) return;
            nextBlink = stateTime + UnityEngine.Random.Range(2.5f, 4f);
            var offset = transform.position - cam.position;
            float yaw = UnityEngine.Random.Range(35f, 70f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            offset = Quaternion.Euler(0f, yaw, 0f) * offset;
            transform.position = cam.position + offset;
            flash = 1f;
            sparks.Emit(12);
        }

        float FlatDistance()
        {
            var d = transform.position - cam.position;
            d.y *= 0.5f;
            return d.magnitude;
        }

        void Strike()
        {
            SetState(State.Striking);
            Struck?.Invoke(this);
        }

        void SetState(State s)
        {
            state = s;
            stateTime = 0f;
        }

        public void Banish()
        {
            if (!Alive) return;
            mat.SetFloat(FadeId, 1f);
            flash = 1f;
            sparks.Emit(40);
            // отдельным источником: голос призрака сейчас затухнет вместе с ним
            AudioSource.PlayClipAtPoint(ProceduralAudio.Banish(), transform.position, 1f);
            SetState(State.Banished);
        }

        // неверный знак: призрак злится и ускоряется
        public void Enrage()
        {
            if (!Alive) return;
            speed *= 1.5f;
            flash = 1f;
            sparks.Emit(8);
            if (state == State.Appearing) SetState(State.Hunting);
        }

        public void Vanish()
        {
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
