using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GhostWard
{
    // Точка входа: лежит на единственном объекте сцены Main и строит всё остальное из кода.
    //
    // Петля ночи: «режиссёр» чередует затишье (с ложными звуками) и встречи с сущностями.
    // Игрок определяет сущность по признакам и применяет нужный предмет из ограниченного запаса.
    // Страх растёт, когда сущность рядом или нападает; на 100% игра окончена. Дожил до 04:00 — рассвет.
    public class Game : MonoBehaviour
    {
        enum Phase { Loading, Menu, Night, Dawn, Over }

        const float NightSeconds = 180f;
        const float FearDecay = 2.5f;

        Phase phase;
        GameUI ui;
        Camera cam;
        ArRig arRig;
        GyroRig gyroRig;
        AudioSource sfx, drone, heart;
        float heartTimer;

        readonly List<Entity> entities = new();
        readonly HashSet<EntityKind> introduced = new();
        Inventory inv;
        ItemKind selected = ItemKind.Salt;
        int night;
        float nightTime, fear, nextEncounter, nextFakeCue;

        // жесты
        bool pressing, overBar;
        Vector2 pressStart;
        float pressTime;
        float bellCooldown;
        Vector3 lastAccel;

        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.Portrait;

            sfx = gameObject.AddComponent<AudioSource>();
            drone = gameObject.AddComponent<AudioSource>();
            drone.clip = ProceduralAudio.Drone();
            drone.loop = true;
            drone.volume = 0f;
            drone.Play();
            heart = gameObject.AddComponent<AudioSource>();
            heart.clip = ProceduralAudio.Heartbeat();

            ui = GameUI.Create();
            ui.StartPressed += StartGame;
            ui.NextNightPressed += StartNight;
            ui.ItemSelected += k => { selected = k; ui.SetSelected(k); };

            yield return ArRig.TryCreate(r => arRig = r, ui.SetDiagnostics);
            if (arRig != null)
            {
                cam = arRig.Cam;
                // если за 8 секунд с камеры AR не пришло ни одного кадра — уходим в режим гироскопа
                float wait = 8f;
                while (wait > 0f && !arRig.ReceivingFrames)
                {
                    wait -= Time.unscaledDeltaTime;
                    ui.SetDiagnostics(arRig.Diagnostics());
                    yield return null;
                }
                if (!arRig.ReceivingFrames)
                {
                    arRig.Shutdown();
                    arRig = null;
                }
            }
            if (arRig == null)
            {
                gyroRig = GyroRig.Create();
                cam = gyroRig.Cam;
            }

            ui.ShowMenu(arRig != null
                ? "Режим AR: сущности живут в комнате, можно ходить."
                : "Режим без AR: стой на месте и поворачивайся всем телом.");
            phase = Phase.Menu;
        }

        // ---------- ход игры ----------

        void StartGame()
        {
            inv = Inventory.Starting();
            night = 0;
            fear = 0f;
            introduced.Clear();
            StartNight();
        }

        void StartNight()
        {
            ClearEntities();
            night++;
            nightTime = 0f;
            nextEncounter = Random.Range(8f, 12f);
            nextFakeCue = Random.Range(4f, 7f);
            fear = Mathf.Min(fear, 20f);
            pressing = false;
            ui.ShowHud();
            ui.SetNight(night);
            ui.SetSelected(selected);
            ui.Message($"Ночь {night}", 3f);
            phase = Phase.Night;
        }

        void Update()
        {
            ui.SetDiagnostics(arRig != null ? arRig.Diagnostics() : gyroRig != null ? gyroRig.Diagnostics() : "старт…");
            if (phase != Phase.Night) { drone.volume = Mathf.MoveTowards(drone.volume, 0.15f, Time.deltaTime); return; }

            float dt = Time.deltaTime;
            entities.RemoveAll(e => e == null);

            nightTime += dt;
            ui.SetClock(nightTime / NightSeconds);
            if (nightTime >= NightSeconds) { Dawn(); return; }

            Direct(dt);
            HandleInput(dt);
            UpdateFear(dt);
            UpdateAtmosphere(dt);
            ui.SetInventory(inv);
            ui.UpdateCues(cam, entities);
        }

        // «Режиссёр»: сколько сущностей одновременно и когда приходит следующая.
        void Direct(float dt)
        {
            int alive = 0;
            foreach (var e in entities) if (e.Alive) alive++;
            int maxAlive = night >= 3 && nightTime > NightSeconds * 0.4f ? 2 : 1;

            if (alive < maxAlive)
            {
                nextEncounter -= dt;
                if (nextEncounter <= 0f)
                {
                    Spawn();
                    nextEncounter = Random.Range(10f, 18f) / (1f + night * 0.1f);
                }
            }

            // ложные звуки в тишине — чтобы игрок не расслаблялся
            nextFakeCue -= dt;
            if (nextFakeCue <= 0f)
            {
                nextFakeCue = Random.Range(9f, 16f);
                var clip = Random.value < 0.5f ? ProceduralAudio.Knock() : ProceduralAudio.Laugh();
                var dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                AudioSource.PlayClipAtPoint(clip, cam.transform.position + dir * 3f, 0.35f);
            }
        }

        void Spawn()
        {
            int kinds = Catalog.KindsForNight(night);
            var def = Catalog.Entities[Random.Range(0, kinds)];

            // приходят чаще сбоку и со спины
            var fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-3f) fwd = Vector3.forward;
            float yaw = Random.Range(60f, 180f) * (Random.value < 0.5f ? -1f : 1f);
            var dir = Quaternion.Euler(0f, yaw, 0f) * fwd.normalized;
            float dist = def.Kind == EntityKind.Poltergeist ? Random.Range(1.8f, 2.6f) : Random.Range(3f, 4.2f);
            var pos = cam.transform.position + dir * dist + Vector3.up * Random.Range(-0.2f, 0.15f);

            entities.Add(Entity.Create(def, pos, cam, this, 1f + (night - 1) * 0.1f));
        }

        void Dawn()
        {
            phase = Phase.Dawn;
            ClearEntities();
            string supply = inv.Resupply();
            ui.ShowDawn(night, supply);
        }

        void GameOver()
        {
            phase = Phase.Over;
            ClearEntities();
            int best = Mathf.Max(night, PlayerPrefs.GetInt("bestNight", 0));
            PlayerPrefs.SetInt("bestNight", best);
            PlayerPrefs.Save();
            ui.ShowGameOver(night, nightTime / NightSeconds, best);
        }

        void ClearEntities()
        {
            foreach (var e in entities) if (e != null) e.Vanish();
            entities.Clear();
        }

        // ---------- события от сущностей ----------

        public bool IsInView(Vector3 pos)
        {
            var vp = cam.WorldToViewportPoint(pos);
            return vp.z > 0f && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
        }

        public void OnManifest(Entity e)
        {
            // подсказка при первой встрече за игру — дальше игрок опознаёт сам
            if (introduced.Add(e.Def.Kind) && night <= 2)
                ui.Message(e.Def.Hint, 5f);
        }

        public void OnThrow(Entity e)
        {
            AddFear(7f);
            ui.Shake(0.6f);
        }

        public void OnAttack(Entity e)
        {
            AddFear(e.Def.Kind == EntityKind.Poltergeist ? 22f : 32f);
            ui.Jumpscare(e.Def.Kind == EntityKind.Poltergeist ? 0.5f : 1f);
            sfx.PlayOneShot(ProceduralAudio.Screech(), 0.8f);
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        void AddFear(float v)
        {
            fear = Mathf.Clamp(fear + v, 0f, 100f);
            if (fear >= 100f && phase == Phase.Night) GameOver();
        }

        void UpdateFear(float dt)
        {
            bool calm = true;
            foreach (var e in entities)
            {
                if (!e.Alive || e.Current == Entity.State.Lurking) continue;
                calm = false;
                if (!e.Visible) continue;
                float d = e.FlatDistance();
                if (d < 3f) AddFear(dt * 7f * (1f - d / 3f));
            }
            if (calm) fear = Mathf.Max(0f, fear - FearDecay * dt);
            ui.SetFear(fear / 100f);
        }

        void UpdateAtmosphere(float dt)
        {
            drone.volume = Mathf.MoveTowards(drone.volume, 0.35f + fear / 100f * 0.4f, dt * 0.3f);

            // сердце бьётся чаще со страхом
            heartTimer -= dt;
            if (fear > 15f && heartTimer <= 0f)
            {
                heart.volume = Mathf.Lerp(0.2f, 0.9f, fear / 100f);
                heart.Play();
                heartTimer = Mathf.Lerp(1.1f, 0.38f, fear / 100f);
            }
        }

        // ---------- предметы ----------

        void HandleInput(float dt)
        {
            bellCooldown -= dt;
            bool lightOn = false;

            var pointer = Pointer.current;
            if (pointer != null)
            {
                bool pressed = pointer.press.isPressed;
                var pos = pointer.position.ReadValue();

                if (pressed && !pressing)
                {
                    pressing = true;
                    overBar = ui.IsOverItemBar(pos);
                    pressStart = pos;
                    pressTime = 0f;
                }
                else if (pressed && pressing)
                {
                    pressTime += dt;
                    if (!overBar && selected == ItemKind.Lantern) lightOn = true;
                }
                else if (!pressed && pressing)
                {
                    pressing = false;
                    if (!overBar) OnRelease(pos - pressStart);
                }
            }

            UpdateLantern(lightOn, dt);
            if (selected == ItemKind.Bell && DetectShake()) RingBell();
        }

        void OnRelease(Vector2 delta)
        {
            float h = Screen.height;
            bool tap = pressTime < 0.35f && delta.magnitude < h * 0.03f;
            bool flickUp = pressTime < 0.6f && delta.y > h * 0.1f && Mathf.Abs(delta.x) < delta.y;

            switch (selected)
            {
                case ItemKind.Salt when flickUp: ThrowSalt(); break;
                case ItemKind.Salt when tap: ui.Message("Смахни пальцем вверх, чтобы бросить соль", 2f); break;
                case ItemKind.Iron when tap: StrikeIron(); break;
                case ItemKind.Bell when tap: RingBell(); break;
            }
        }

        // сущность ближе всего к центру экрана в пределах угла и дистанции
        Entity Target(float maxAngle, float maxDist)
        {
            Entity best = null;
            float bestAngle = maxAngle;
            foreach (var e in entities)
            {
                if (!e.Visible) continue;
                var to = e.transform.position - cam.transform.position;
                float a = Vector3.Angle(cam.transform.forward, to);
                if (a < bestAngle && e.FlatDistance() <= maxDist) { bestAngle = a; best = e; }
            }
            return best;
        }

        void ThrowSalt()
        {
            if (inv.Salt <= 0) { Empty("Соль закончилась"); return; }
            inv.Salt--;
            sfx.PlayOneShot(ProceduralAudio.SaltThrow(), 1f);
            ui.SaltBurst();

            var t = Target(20f, 4.5f);
            if (t == null) { ui.Message("Соль рассыпалась впустую", 1.5f); return; }
            if (t.Def.Bane == ItemKind.Salt) t.Banish();
            else WrongItem(t);
        }

        void StrikeIron()
        {
            if (inv.Iron <= 0) { Empty("Железа не осталось"); return; }
            var t = Target(35f, 1.5f);
            if (t == null)
            {
                var far = Target(30f, 6f);
                ui.Message(far != null ? "Слишком далеко. Подпусти ближе…" : "Рядом никого", 1.5f);
                return;
            }
            inv.Iron--;
            sfx.PlayOneShot(ProceduralAudio.IronStrike(), 1f);
            ui.Flash(new Color(1f, 1f, 1f, 0.5f));
            if (t.Def.Bane == ItemKind.Iron) t.Banish();
            else WrongItem(t);
        }

        void RingBell()
        {
            if (bellCooldown > 0f) return;
            if (inv.Bell <= 0) { Empty("Колокольчик треснул"); bellCooldown = 1f; return; }
            inv.Bell--;
            bellCooldown = 1.2f;
            sfx.PlayOneShot(ProceduralAudio.BellRing(), 1f);

            // колокольчик действует на полтергейста, если повернуться в его сторону
            bool hit = false;
            foreach (var e in entities)
            {
                if (!e.Alive || e.Current != Entity.State.Manifest || e.Def.Kind != EntityKind.Poltergeist) continue;
                var to = e.transform.position - cam.transform.position;
                to.y = 0f;
                var fwd = cam.transform.forward;
                fwd.y = 0f;
                if (Vector3.Angle(fwd, to) < 45f) { e.Banish(); hit = true; }
                else e.Provoke();
            }
            if (!hit)
            {
                var visible = Target(30f, 6f);
                if (visible != null) WrongItem(visible);
            }
        }

        void UpdateLantern(bool on, float dt)
        {
            on &= inv.Battery > 0f;
            if (on) inv.Battery = Mathf.Max(0f, inv.Battery - 9f * dt);
            ui.SetLantern(on);

            var lit = on ? Target(16f, 6f) : null;
            foreach (var e in entities)
            {
                e.Lit = e == lit;
                if (e != lit) continue;
                if (e.Def.Bane == ItemKind.Lantern)
                {
                    if (e.Exposure > 1.6f) e.Banish();
                }
            }
            if (on && inv.Battery <= 0f) Empty("Фонарь погас");
        }

        bool DetectShake()
        {
            var acc = Accelerometer.current;
            Vector3 a;
            if (acc != null)
            {
                if (!acc.enabled) InputSystem.EnableDevice(acc);
                a = acc.acceleration.ReadValue();
                if (a.magnitude > 4f) a /= 9.81f; // некоторые устройства отдают м/с², приводим к g
            }
            else a = Input.acceleration;

            float jerk = (a - lastAccel).magnitude;
            lastAccel = a;
            return jerk > 1.4f;
        }

        void WrongItem(Entity e)
        {
            e.Provoke();
            AddFear(8f);
            sfx.PlayOneShot(ProceduralAudio.Fail(), 0.6f);
            ui.Message("Не действует…", 1.5f);
        }

        void Empty(string text)
        {
            sfx.PlayOneShot(ProceduralAudio.Fail(), 0.5f);
            ui.Message(text, 1.5f);
        }
    }
}
