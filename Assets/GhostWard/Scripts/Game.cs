using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GhostWard
{
    // Точка входа: лежит на единственном объекте сцены Main и строит всё остальное из кода.
    //
    // Перед ночью игрок осматривает комнату — игра строит карту темноты.
    // Ночью «режиссёр» чередует затишье (с ложными звуками) и встречи; сущности выходят из тёмных мест.
    // Предметы применяются жестами, без выбора на экране:
    //   соль — смахнуть вверх, фонарь — зажать палец, железо — двойное касание, колокольчик — встряхнуть.
    // Страх растёт, когда сущность рядом или нападает; на 100% игра окончена. Дожил до 04:00 — рассвет.
    public class Game : MonoBehaviour
    {
        enum Phase { Loading, Menu, Scan, Night, Dawn, Over }

        const float NightSeconds = 180f;
        const float FearDecay = 2.5f;
        const float EyeHeight = 1.4f;
        const float ScanCoverage = 0.85f;

        Phase phase;
        GameUI ui;
        Camera cam;
        ArRig arRig;
        GyroRig gyroRig;
        AudioSource sfx, drone, heart;
        float heartTimer;

        readonly List<Entity> entities = new();
        readonly HashSet<EntityKind> introduced = new();
        readonly DarknessMap map = new();
        Inventory inv;
        int night;
        float nightTime, fear, nextEncounter, nextFakeCue;

        // осмотр комнаты
        Vector2[] samplePoints;
        float[] sampleLuma;
        float scanTimer, scanFailTime;

        // жесты
        bool pressing, pressIgnored, moved, lanternHeld;
        Vector2 pressStart;
        float pressTime, lastTapTime = -10f, bellCooldown;
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

            // сетка точек экрана, по которым меряем яркость при осмотре
            const int gx = 8, gy = 12;
            samplePoints = new Vector2[gx * gy];
            sampleLuma = new float[gx * gy];
            for (int y = 0; y < gy; y++)
                for (int x = 0; x < gx; x++)
                    samplePoints[y * gx + x] = new Vector2((x + 0.5f) / gx, (y + 0.5f) / gy);

            ui = GameUI.Create();
            ui.StartPressed += StartGame;
            ui.NextNightPressed += StartNight;
            ui.JournalToggled += open =>
            {
                Time.timeScale = open ? 0f : 1f;
                AudioListener.pause = open;
                pressing = false;
                if (open) ui.ShowJournal(inv);
            };

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
            fear = Mathf.Min(fear, 20f);
            pressing = false;
            ui.ShowHud();
            ui.SetNight(night);
            ui.SetClock(0f);

            // карта темноты строится заново каждую ночь: свет в комнате мог измениться
            map.Clear();
            scanFailTime = 0f;
            if (gyroRig != null)
            {
                phase = Phase.Scan;
                ui.SetScan(0f);
                ui.Message("Медленно обведи камерой комнату вокруг себя", 4f);
            }
            else BeginNight();
        }

        void BeginNight()
        {
            ui.SetScan(-1f);
            nightTime = 0f;
            nextEncounter = Random.Range(8f, 12f);
            nextFakeCue = Random.Range(4f, 7f);
            ui.Message($"Ночь {night}", 3f);
            phase = Phase.Night;
        }

        void Update()
        {
            ui.SetDiagnostics(DiagnosticsText());
            if (ui.JournalOpen) return;

            if (cam != null) ui.SetMapStrip(map, cam.transform.forward, phase is Phase.Scan or Phase.Night);
            if (phase == Phase.Scan) { UpdateScan(); return; }
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
            ui.UpdateCues(cam, entities);
        }

        string DiagnosticsText()
        {
            string rig = arRig != null ? arRig.Diagnostics() : gyroRig != null ? gyroRig.Diagnostics() : "старт…";
            return map.Empty ? rig : $"{rig}\nкарта {Mathf.RoundToInt(map.Coverage * 100)}%";
        }

        void UpdateScan()
        {
            drone.volume = Mathf.MoveTowards(drone.volume, 0.25f, Time.deltaTime * 0.3f);
            scanTimer -= Time.deltaTime;
            if (scanTimer > 0f) return;
            scanTimer = 0.15f;

            if (!gyroRig.SampleLuma(samplePoints, sampleLuma))
            {
                // камеры нет — играем без карты, сущности будут выбирать место случайно
                scanFailTime += 0.15f;
                if (scanFailTime > 4f) BeginNight();
                return;
            }

            float mean = 0f;
            int n = 0;
            foreach (var l in sampleLuma) if (l >= 0f) { mean += l; n++; }
            if (n == 0) return;
            mean = Mathf.Max(mean / n, 0.02f);

            for (int i = 0; i < samplePoints.Length; i++)
            {
                if (sampleLuma[i] < 0f) continue;
                var dir = cam.ViewportPointToRay(samplePoints[i]).direction;
                map.Add(dir, sampleLuma[i] / mean);
            }

            ui.SetScan(map.Coverage / ScanCoverage);
            if (map.Coverage >= ScanCoverage) BeginNight();
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

            // ложные звуки из тёмных мест — чтобы игрок не расслаблялся
            nextFakeCue -= dt;
            if (nextFakeCue <= 0f)
            {
                nextFakeCue = Random.Range(9f, 16f);
                var clip = Random.value < 0.5f ? ProceduralAudio.Knock() : ProceduralAudio.Laugh();
                var pos = PickPosition(Catalog.Get(EntityKind.Poltergeist), 40f, 3f);
                AudioSource.PlayClipAtPoint(clip, pos, 0.35f);
            }
        }

        void Spawn()
        {
            var def = Catalog.Entities[Random.Range(0, Catalog.KindsForNight(night))];
            // приходят из темноты сбоку и со спины
            var pos = PickPosition(def, 60f, -1f);
            entities.Add(Entity.Create(def, pos, cam, this, 1f + (night - 1) * 0.1f));
        }

        public static float EntityHeight(EntityDef def) =>
            def.Kind is EntityKind.Albasty or EntityKind.Shade ? 1.7f : 1.15f;

        // Место для сущности: тёмный сектор карты, не ближе minAngle к взгляду игрока.
        // Расстояние: если тёмное место ниже горизонта — это пол, считаем по наклону и высоте глаз;
        // иначе это проём или дальняя стена — 3–4.5 м. maxDist > 0 ограничивает дальность.
        public Vector3 PickPosition(EntityDef def, float minAngle, float maxDist)
        {
            var camPos = cam.transform.position;
            var fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-3f) fwd = Vector3.forward;
            fwd.Normalize();

            if (!map.TryPick(fwd, minAngle, out float yaw, out float pitch))
            {
                float viewYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                yaw = viewYaw + Random.Range(minAngle, 180f) * (Random.value < 0.5f ? -1f : 1f);
                pitch = 0f;
            }

            float dist;
            if (def.Kind == EntityKind.Poltergeist) dist = Random.Range(1.8f, 2.6f);
            else if (pitch < -8f) dist = Mathf.Clamp(EyeHeight / Mathf.Tan(-pitch * Mathf.Deg2Rad), 2.2f, 4.5f);
            else dist = Random.Range(3.2f, 4.5f);
            if (maxDist > 0f) dist = Mathf.Min(dist, maxDist);

            var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var pos = camPos + dir * dist;
            float floor = camPos.y - EyeHeight;
            pos.y = def.Kind switch
            {
                EntityKind.Shade or EntityKind.Albasty => floor + EntityHeight(def) * 0.5f,
                EntityKind.Ghost => camPos.y + Mathf.Clamp(Mathf.Tan(pitch * Mathf.Deg2Rad) * dist, -0.4f, 0.8f),
                _ => camPos.y
            };
            return pos;
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

        // ---------- жесты ----------

        void HandleInput(float dt)
        {
            bellCooldown -= dt;
            float h = Screen.height;

            var pointer = Pointer.current;
            if (pointer != null)
            {
                bool pressed = pointer.press.isPressed;
                var pos = pointer.position.ReadValue();

                if (pressed && !pressing)
                {
                    pressing = true;
                    pressIgnored = ui.IsOverButton(pos);
                    moved = false;
                    lanternHeld = false;
                    pressStart = pos;
                    pressTime = 0f;
                }
                else if (pressed && pressing && !pressIgnored)
                {
                    pressTime += dt;
                    if ((pos - pressStart).magnitude > h * 0.03f) moved = true;
                    // зажатый неподвижный палец — фонарь
                    if (!moved && pressTime > 0.3f) lanternHeld = true;
                }
                else if (!pressed && pressing)
                {
                    pressing = false;
                    if (!pressIgnored && !lanternHeld) OnRelease(pos - pressStart);
                    lanternHeld = false;
                }
            }

            UpdateLantern(lanternHeld && pressing, dt);
            if (DetectShake()) RingBell();
        }

        void OnRelease(Vector2 delta)
        {
            float h = Screen.height;
            if (pressTime < 0.6f && delta.y > h * 0.1f && Mathf.Abs(delta.x) < delta.y)
            {
                ThrowSalt();
                return;
            }
            if (!moved && pressTime < 0.3f)
            {
                if (Time.time - lastTapTime < 0.35f)
                {
                    lastTapTime = -10f;
                    StrikeIron();
                }
                else lastTapTime = Time.time;
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
            ui.Supply($"соль: {inv.Salt}");

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
            ui.Supply($"железо: {inv.Iron}");
            if (t.Def.Bane == ItemKind.Iron) t.Banish();
            else WrongItem(t);
        }

        void RingBell()
        {
            if (bellCooldown > 0f) return;
            bellCooldown = 1.2f;
            if (inv.Bell <= 0) { Empty("Колокольчик треснул"); return; }
            inv.Bell--;
            sfx.PlayOneShot(ProceduralAudio.BellRing(), 1f);
            ui.Supply($"колокольчик: {inv.Bell}");

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
            if (on)
            {
                inv.Battery = Mathf.Max(0f, inv.Battery - 9f * dt);
                if (inv.Battery <= 0f) Empty("Фонарь погас");
            }
            ui.SetLantern(on, inv.Battery);

            var lit = on ? Target(16f, 6f) : null;
            foreach (var e in entities)
            {
                e.Lit = e == lit;
                if (e == lit && e.Def.Bane == ItemKind.Lantern && e.Exposure > 1.6f) e.Banish();
            }
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
