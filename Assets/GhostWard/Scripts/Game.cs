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
    //   соль — смахнуть вверх, зеркало — вниз, полынь — зигзаг, фонарь — зажать палец,
    //   железо — двойное касание, колокольчик — встряхнуть.
    // Новые виды сущностей открываются по мере того, сколько игрок продержался за игру.
    // Страх растёт, когда сущность рядом или нападает; на 100% игра окончена. Дожил до 04:00 — рассвет.
    public class Game : MonoBehaviour
    {
        enum Phase { Loading, Menu, Scan, Night, Dawn, Over }

        const float NightSeconds = 180f;
        const float FearDecay = 2.5f;
        const float EyeHeight = 1.4f;
        const float ScanCoverage = 0.85f;
        const float CeilingHeight = 2.6f;

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
        int night, unlocked;
        float nightTime, survived, fear, nextEncounter, nextFakeCue;
        EntityKind? forcedNext;

        // вид камеры: ночной режим и помехи
        AudioSource staticNoise;
        float nightVision, nightVisionTarget, nextVisionSwitch, glitch;

        // скорость поворота игрока, °/с — по ней ходит двойник
        public float TurnSpeed { get; private set; }
        Vector3 lastForward;

        // осмотр комнаты
        Vector2[] samplePoints;
        float[] sampleLuma;
        float scanTimer, scanFailTime;

        // жесты
        bool pressing, pressIgnored, moved, lanternHeld;
        Vector2 pressStart, lastExtreme;
        int reversals, lastDirX;
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
            staticNoise = gameObject.AddComponent<AudioSource>();
            staticNoise.clip = ProceduralAudio.Static();
            staticNoise.loop = true;
            staticNoise.volume = 0f;
            staticNoise.Play();

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
            survived = 0f;
            unlocked = Catalog.Unlocked(0f);
            forcedNext = null;
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
            nextVisionSwitch = Random.Range(20f, 40f);
            nightVisionTarget = 0f;
            ui.Message($"Ночь {night}", 3f);
            phase = Phase.Night;
        }

        void Update()
        {
            ui.SetDiagnostics(DiagnosticsText());
            if (ui.JournalOpen) return;

            if (cam != null) ui.SetMapStrip(map, cam.transform.forward, phase is Phase.Scan or Phase.Night);
            if (phase == Phase.Scan) { UpdateScan(); return; }
            if (phase != Phase.Night)
            {
                drone.volume = Mathf.MoveTowards(drone.volume, 0.15f, Time.deltaTime);
                staticNoise.volume = 0f;
                UpdateLook(Time.deltaTime);
                return;
            }

            float dt = Time.deltaTime;
            entities.RemoveAll(e => e == null);

            var fwd = cam.transform.forward;
            TurnSpeed = Mathf.Lerp(TurnSpeed, Vector3.Angle(fwd, lastForward) / Mathf.Max(dt, 1e-3f), dt * 8f);
            lastForward = fwd;

            nightTime += dt;
            survived += dt;
            int nowUnlocked = Catalog.Unlocked(survived);
            if (nowUnlocked > unlocked)
            {
                // открылся новый вид — он придёт следующим
                unlocked = nowUnlocked;
                forcedNext = Catalog.Entities[unlocked - 1].Kind;
            }

            ui.SetClock(nightTime / NightSeconds);
            if (nightTime >= NightSeconds) { Dawn(); return; }

            Direct(dt);
            HandleInput(dt);
            UpdateFear(dt);
            UpdateAtmosphere(dt);
            UpdateLook(dt);
            ui.UpdateCues(cam, entities);
        }

        // Камера: тёмная, иногда переключается в ночной режим; рядом с сущностью — помехи.
        void UpdateLook(float dt)
        {
            if (phase == Phase.Night)
            {
                nextVisionSwitch -= dt;
                if (nextVisionSwitch <= 0f)
                {
                    bool on = nightVisionTarget < 0.5f;
                    nightVisionTarget = on ? 1f : 0f;
                    nextVisionSwitch = on ? Random.Range(10f, 18f) : Random.Range(25f, 50f);
                    sfx.PlayOneShot(ProceduralAudio.Click(), 0.8f);
                    glitch = 1f;
                }

                float near = 0f;
                foreach (var e in entities)
                    if (e.Alive && e.Current != Entity.State.Lurking)
                        near = Mathf.Max(near, Mathf.Clamp01(1f - (e.FlatDistance() - 0.8f) / 2.2f));
                glitch = Mathf.Max(Mathf.MoveTowards(glitch, 0f, dt * 2f), near * 0.6f);
            }
            else
            {
                nightVisionTarget = 0f;
                glitch = Mathf.MoveTowards(glitch, 0f, dt * 2f);
            }

            // переключение режимов не плавное, а через короткий «сбой»
            nightVision = Mathf.MoveTowards(nightVision, nightVisionTarget, dt * 6f);
            staticNoise.volume = glitch * 0.35f;
            if (gyroRig != null) gyroRig.SetLook(nightVision, glitch);
            ui.SetGrain(0.05f + nightVision * 0.08f + glitch * 0.25f);
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
            int maxAlive = survived > 600f ? 3 : survived > 300f ? 2 : 1;

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
            EntityDef def;
            if (forcedNext.HasValue)
            {
                def = Catalog.Get(forcedNext.Value);
                forcedNext = null;
            }
            else
            {
                // свежие виды приходят чуть чаще старых
                float total = 0f;
                for (int i = 0; i < unlocked; i++) total += 1f + i * 0.15f;
                float r = Random.value * total;
                int pick = 0;
                for (; pick < unlocked - 1; pick++)
                {
                    r -= 1f + pick * 0.15f;
                    if (r <= 0f) break;
                }
                def = Catalog.Entities[pick];
            }
            // приходят из темноты сбоку и со спины
            var pos = PickPosition(def, 60f, -1f);
            entities.Add(Entity.Create(def, pos, cam, this, 1f + (night - 1) * 0.1f));
        }

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
            else if (def.Kind == EntityKind.Mara) dist = Random.Range(1.2f, 1.6f);
            else if (pitch < -8f) dist = Mathf.Clamp(EyeHeight / Mathf.Tan(-pitch * Mathf.Deg2Rad), 2.2f, 4.5f);
            else dist = Random.Range(3.2f, 4.5f);
            if (maxDist > 0f) dist = Mathf.Min(dist, maxDist);

            var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var pos = camPos + dir * dist;
            float floor = camPos.y - EyeHeight;
            pos.y = def.Kind switch
            {
                EntityKind.Shade or EntityKind.Albasty or EntityKind.Double or EntityKind.Upyr or EntityKind.Mara
                    => floor + def.Height * 0.5f,
                EntityKind.Kikimora => floor + CeilingHeight - def.Height * 0.5f,
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
            // при самой первой встрече — подсказка и новая запись в дневнике; дальше игрок опознаёт сам
            if (!Catalog.Seen(e.Def.Kind))
            {
                Catalog.MarkSeen(e.Def.Kind);
                ui.Message(e.Def.Hint + "\n\nНовая запись в дневнике", 6f);
            }
            else if (introduced.Add(e.Def.Kind) && night == 1)
                ui.Message(e.Def.Hint, 4f);
        }

        public void OnThrow(Entity e)
        {
            AddFear(7f);
            ui.Shake(0.6f);
        }

        public void OnAttack(Entity e)
        {
            AddFear(e.Def.Kind == EntityKind.Poltergeist ? 22f : 32f);
            ui.Jumpscare(e.Def, e.Seed, e.Def.Kind == EntityKind.Poltergeist ? 0.5f : 1f);
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
                // на мару нельзя смотреть — страх растёт втрое быстрее
                if (e.Def.Kind == EntityKind.Mara && IsInView(e.transform.position)) { AddFear(dt * 9f); continue; }
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
            float h = Screen.height, w = Screen.width;

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
                    pressStart = lastExtreme = pos;
                    reversals = 0;
                    lastDirX = 0;
                    pressTime = 0f;
                }
                else if (pressed && pressing && !pressIgnored)
                {
                    pressTime += dt;
                    if ((pos - pressStart).magnitude > h * 0.03f) moved = true;
                    // зажатый неподвижный палец — фонарь
                    if (!moved && pressTime > 0.3f) lanternHeld = true;

                    // зигзаг: считаем развороты по горизонтали с заметным размахом
                    float dx = pos.x - lastExtreme.x;
                    int dir = dx > 0f ? 1 : -1;
                    if (lastDirX == 0)
                    {
                        if (Mathf.Abs(dx) > w * 0.08f) { lastDirX = dir; lastExtreme = pos; }
                    }
                    else if (dir == lastDirX) lastExtreme = pos;          // тянем дальше в ту же сторону
                    else if (Mathf.Abs(dx) > w * 0.08f)
                    {
                        reversals++;
                        lastDirX = dir;
                        lastExtreme = pos;
                    }
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
            if (reversals >= 2) { Use(ItemKind.Wormwood); return; }
            if (pressTime < 0.6f && Mathf.Abs(delta.y) > h * 0.1f && Mathf.Abs(delta.x) < Mathf.Abs(delta.y))
            {
                Use(delta.y > 0f ? ItemKind.Salt : ItemKind.Mirror);
                return;
            }
            if (!moved && pressTime < 0.3f)
            {
                if (Time.time - lastTapTime < 0.35f)
                {
                    lastTapTime = -10f;
                    Use(ItemKind.Iron);
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

        // Применить предмет «в цель» — в сущность у центра экрана.
        void Use(ItemKind item)
        {
            var def = Catalog.Get(item);
            float angle = item switch { ItemKind.Iron => 35f, ItemKind.Wormwood => 35f, ItemKind.Mirror => 25f, _ => 20f };
            float range = item switch { ItemKind.Iron => 1.5f, ItemKind.Wormwood => 4f, ItemKind.Mirror => 5f, _ => 4.5f };

            if (item == ItemKind.Iron)
            {
                if (inv.Iron <= 0) { Empty("Железа не осталось"); return; }
                if (Target(angle, range) == null)
                {
                    ui.Message(Target(30f, 6f) != null ? "Слишком далеко. Подпусти ближе…" : "Рядом никого", 1.5f);
                    return;
                }
            }
            if (!inv.Use(item)) { Empty($"Больше нет: {def.Name.ToLower()}"); return; }

            switch (item)
            {
                case ItemKind.Salt: sfx.PlayOneShot(ProceduralAudio.SaltThrow(), 1f); ui.SaltBurst(); break;
                case ItemKind.Iron: sfx.PlayOneShot(ProceduralAudio.IronStrike(), 1f); ui.Flash(new Color(1f, 1f, 1f, 0.5f)); break;
                case ItemKind.Mirror: sfx.PlayOneShot(ProceduralAudio.Shimmer(), 1f); ui.Flash(new Color(0.8f, 0.9f, 1f, 0.6f)); break;
                case ItemKind.Wormwood: sfx.PlayOneShot(ProceduralAudio.Whoosh(), 1f); ui.Smoke(); break;
            }
            ui.Supply($"{def.Name.ToLower()}: {inv.Label(item)}");

            var t = Target(angle, range);
            if (t == null) { ui.Message("Впустую…", 1.2f); return; }
            if (t.Def.Bane == item) t.Banish();
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

            // мару звон будит всегда; полтергейста — если повернуться в его сторону
            bool hit = false;
            foreach (var e in entities)
            {
                if (!e.Alive || e.Current != Entity.State.Manifest) continue;
                if (e.Def.Kind == EntityKind.Mara) { e.Banish(); hit = true; continue; }
                if (e.Def.Kind != EntityKind.Poltergeist) continue;
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
                if (e == lit && e.Def.Bane == ItemKind.Lantern && e.Exposure > e.ExposureNeeded) e.Banish();
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
