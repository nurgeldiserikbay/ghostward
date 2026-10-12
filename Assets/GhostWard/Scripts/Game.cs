using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GhostWard
{
    // Точка входа: лежит на единственном объекте сцены Main и строит всё остальное из кода.
    //
    // Ночь — одно «дело»: в дом приходит одна сущность.
    //  1. Осмотр: игрок обводит комнату камерой, игра строит карту темноты.
    //  2. Расследование: сущность невидима в логове; оттуда идут улики (иней, шёпот, стук, помехи,
    //     дрожь компаса, силуэт в ночном режиме, ответ радио на вопрос вслух). Игрок отмечает их в дневнике
    //     и может назвать сущность: угадал — она слабее, ошибся — охота начинается в ярости.
    //  3. Охота: сущность выходит. Шум в микрофоне выдаёт игрока, тишина и неподвижность прячут.
    //     Изгнать можно только тем, чего она боится. Предметы — жестами:
    //     соль — смахнуть вверх, зеркало — вниз, полынь — зигзаг, фонарь — зажать палец,
    //     железо — двойное касание, колокольчик — встряхнуть.
    //  4. Рассвет: итог, фото сущности, пополнение запаса.
    // Страх растёт от улик, близости и нападений; на 100% игра окончена.
    public class Game : MonoBehaviour
    {
        enum Phase { Loading, Menu, Scan, Investigate, Hunt, Dawn, Over }

        const float NightSeconds = 300f;
        const float FearDecay = 2.5f;
        const float EyeHeight = 1.4f;
        const float ScanCoverage = 0.85f;
        const float CeilingHeight = 2.6f;

        const int RadioAnswerCount = 6;   // ключи radio_1 … radio_6

        Phase phase;
        GameUI ui;
        Camera cam;
        ArRig arRig;
        GyroRig gyroRig;
        MicListener mic;
        AudioSource sfx, drone, heart;
        float heartTimer;

        readonly List<Entity> entities = new();
        readonly DarknessMap map = new();
        Inventory inv;
        int night;
        float nightTime, fear;

        // дело этой ночи
        EntityDef target;
        Vector3 lair;
        Entity hunter;
        float anger, clueTimer, fakeTimer, frostTime, staticTime, compassTime, silhouetteArmed, silhouetteCooldown;
        float radioCooldown, radioAnswerIn = -1f, banishedTime = -1f;
        bool named, namedRight, wrongItemUsed;

        // приборы и защита
        Tool tool = Tool.Emf;
        float emf, thermo = 20f, beepTimer, candleTime;
        bool circle, tumar;

        // второй шанс, припасы за рекламу, тайники, пугающие события
        Phase lostIn;
        bool revivedThisNight, bonusTaken, overXpGiven;
        int nightXp;   // опыт за находки этой ночи: тайники, фото
        GameObject stash;
        float stashTimer, stashGaze, stashLife, stashSound, eventTimer;
        int stashesThisNight;
        bool stashHintShown;

        // охота: слух и затаивание
        float quietTime, heardTime;

        // фото сущности
        Texture2D photo;
        bool photoTaking;

        // вид камеры: ночной режим и помехи
        AudioSource staticNoise;
        float nightVision, nightVisionTarget, nextVisionSwitch, glitch;

        // скорость поворота игрока, °/с — по ней ходит двойник и по ней понятно, замер ли игрок
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
            mic = gameObject.AddComponent<MicListener>();

            // сетка точек экрана, по которым меряем яркость при осмотре
            const int gx = 8, gy = 12;
            samplePoints = new Vector2[gx * gy];
            sampleLuma = new float[gx * gy];
            for (int y = 0; y < gy; y++)
                for (int x = 0; x < gx; x++)
                    samplePoints[y * gx + x] = new Vector2((x + 0.5f) / gx, (y + 0.5f) / gy);

            Settings.Apply();
            Ads.Init();
            CreateUI();
            if (Loc.Chosen) ui.ShowMenu();
            else ui.ShowLanguage();
            phase = Phase.Menu;
            yield break;
        }

        void CreateUI()
        {
            ui = GameUI.Create();
            ui.StartPressed += () => { Ads.OnRetry(); StartGame(); };
            ui.PlayPressed += () =>
            {
                if (Settings.TutorialSeen) StartCoroutine(Play());
                else ui.OpenTutorial(true);
            };
            ui.TutorialFinished += () => StartCoroutine(Play());
            ui.MenuPressed += ToMenu;
            ui.LanguageChosen += lang =>
            {
                bool firstTime = !Loc.Chosen;
                Loc.Set(lang);
                // тексты строятся при сборке интерфейса — проще пересобрать его целиком
                Destroy(ui.gameObject);
                CreateUI();
                if (firstTime) ui.ShowMenu();
                else ui.ShowSettings();
            };
            ui.NextNightPressed += StartNight;
            ui.ToolSelected += t =>
            {
                tool = t;
                ui.SetTool(t);
                sfx.PlayOneShot(ProceduralAudio.Click(), 0.5f);
            };
            ui.ProtectUsed += UseProtection;
            ui.SecondChancePressed += () => Ads.ShowRewarded(Revive, () => ui.Message(Loc.T("ad_unavailable"), 2f));
            ui.BonusSupplyPressed += () => Ads.ShowRewarded(() =>
            {
                if (bonusTaken) return;
                bonusTaken = true;
                ui.ShowBonus(inv.Bonus());
            }, () => ui.Message(Loc.T("ad_unavailable"), 2f));
            ui.NamePressed += NameEntity;
            ui.SharePressed += SharePhoto;
            ui.JournalToggled += open =>
            {
                Time.timeScale = open ? 0f : 1f;
                AudioListener.pause = open;
                pressing = false;
                ui.CanName = open && phase == Phase.Investigate && !named;
                if (open) ui.ShowJournal(inv);
            };
        }

        // Камера включается только когда игрок нажал «Играть»: AR, если есть, иначе гироскоп.
        IEnumerator Play()
        {
            if (cam == null)
            {
                ui.ShowLoading();
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
            }
            StartGame();
            ui.Message(Loc.T(arRig != null ? "mode_ar" : "mode_gyro"), 3f);
        }

        void ToMenu()
        {
            DestroyStash();
            ClearEntities();
            phase = Phase.Menu;
            ui.SetHidden(false);
            ui.ShowMenu();
        }

        // ---------- ход игры ----------

        void StartGame()
        {
            inv = Inventory.Starting();
            night = 0;
            fear = 0f;
            StartNight();
        }

        void StartNight()
        {
            ClearEntities();
            night++;
            fear = Mathf.Min(fear, 20f);
            pressing = false;
            photo = null;
            circle = tumar = false;
            candleTime = 0f;
            revivedThisNight = bonusTaken = overXpGiven = false;
            nightXp = 0;
            stashesThisNight = 0;
            stashTimer = Random.Range(30f, 50f);
            eventTimer = Random.Range(15f, 25f);
            DestroyStash();
            ui.SetTool(tool);
            ui.SetProtection(inv);
            ui.CloseBag();
            target = PickTarget();
            ui.ShowHud();
            ui.SetNight(night);
            ui.SetClock(0f);
            ui.SetPhase("", false);
            ui.ClearEvidence();
            mic.Begin();

            // карта темноты строится заново каждую ночь: свет в комнате мог измениться
            map.Clear();
            scanFailTime = 0f;
            if (gyroRig != null)
            {
                phase = Phase.Scan;
                ui.SetScan(0f);
                ui.Message(Loc.T("scan_msg"), 4f);
            }
            else BeginInvestigation();
        }

        // Сущность ночи: из пула, который растёт с каждой ночью; невстреченные — чаще.
        EntityDef PickTarget()
        {
            int pool = Catalog.PoolForNight(night);
            float total = 0f;
            var weights = new float[pool];
            for (int i = 0; i < pool; i++) total += weights[i] = Catalog.Seen(Catalog.Entities[i].Kind) ? 1f : 3f;
            float r = Random.value * total;
            for (int i = 0; i < pool; i++)
            {
                r -= weights[i];
                if (r <= 0f) return Catalog.Entities[i];
            }
            return Catalog.Entities[pool - 1];
        }

        void BeginInvestigation()
        {
            ui.SetScan(-1f);
            nightTime = 0f;
            anger = 0f;
            named = namedRight = wrongItemUsed = false;
            banishedTime = -1f;
            clueTimer = Random.Range(3f, 5f);
            fakeTimer = Random.Range(8f, 14f);
            frostTime = staticTime = compassTime = silhouetteArmed = 0f;
            radioCooldown = 0f;
            radioAnswerIn = -1f;
            nextVisionSwitch = Random.Range(12f, 20f);
            nightVisionTarget = 0f;

            // логово: албасты — за спиной, остальные — по своей привычке на карте темноты
            lair = PickPosition(target, target.Lair == Lair.Behind ? 140f : 0f, -1f);
            if (target.Lair == Lair.Ceiling) lair.y = cam.transform.position.y - EyeHeight + CeilingHeight - 0.5f;

            phase = Phase.Investigate;
            ui.SetPhase(Loc.T("phase_investigate"), false);
            ui.SetCameraOverlay(Settings.Vision);
            ui.Message(night == 1 ? Loc.T("intro_first") : Loc.F("intro_n", night), 6f);

            // бонус звания «Чутьё»: одна улика этой ночи отмечена заранее
            if (Progress.Instinct) ui.MarkClue((int)target.Clues[Random.Range(0, target.Clues.Length)]);
        }

        void Update()
        {
            sfx.volume = Settings.Sfx;
            ui.SetDiagnostics(DiagnosticsText());
            if (ui.JournalOpen) return;

            if (cam != null) ui.SetMapStrip(map, cam.transform.forward, phase == Phase.Scan);
            if (phase == Phase.Scan) { UpdateScan(); return; }
            if (phase is not (Phase.Investigate or Phase.Hunt))
            {
                drone.volume = Mathf.MoveTowards(drone.volume, 0.15f * Settings.Ambience, Time.deltaTime);
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
            ui.SetClock(nightTime / NightSeconds);
            if (nightTime >= NightSeconds) { Dawn(false); return; }

            if (phase == Phase.Investigate) UpdateInvestigation(dt);
            else UpdateHunt(dt);
            UpdateTools(dt);
            UpdateProtection(dt);
            UpdateStash(dt);
            UpdateScares(dt);

            UpdateFear(dt);
            UpdateAtmosphere(dt);
            UpdateLook(dt);
            ui.SetMic(mic.Available ? mic.Level : -1f, phase == Phase.Hunt);
            ui.UpdateCues(cam, entities, frostTime > 0f ? lair : (Vector3?)null);
        }

        // ---------- расследование ----------

        float AngleToLair()
        {
            var to = lair - cam.transform.position;
            return Vector3.Angle(cam.transform.forward, to);
        }

        void UpdateInvestigation(float dt)
        {
            anger += dt * (0.4f + night * 0.05f) * (tumar ? 0.6f : 1f);
            frostTime -= dt;
            staticTime -= dt;
            compassTime -= dt;
            silhouetteArmed -= dt;
            silhouetteCooldown -= dt;
            radioCooldown -= dt;

            clueTimer -= dt;
            if (clueTimer <= 0f)
            {
                clueTimer = Random.Range(5f, 9f);
                EmitClue();
            }

            // скрипы — ложная тревога, не улика
            fakeTimer -= dt;
            if (fakeTimer <= 0f)
            {
                fakeTimer = Random.Range(10f, 18f);
                var dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                PlayAt(ProceduralAudio.Creak(), cam.transform.position + dir * 3f, 0.45f);
            }

            float angle = AngleToLair();
            ui.SetCompass(cam.transform.forward, 0f);
            if (staticTime > 0f && angle < 35f) glitch = Mathf.Max(glitch, 0.85f);
            if (silhouetteArmed > 0f && nightVision > 0.9f && angle < 30f && silhouetteCooldown <= 0f)
            {
                Apparition.Show(target, lair, cam.transform, 0.45f);
                silhouetteArmed = 0f;
                silhouetteCooldown = 3f;
            }

            // радио духов: игрок задаёт вопрос вслух
            if (tool == Tool.Radio && mic.Speaking && radioCooldown <= 0f)
            {
                radioCooldown = 7f;
                radioAnswerIn = 1.3f;
                anger += 8f;
                sfx.PlayOneShot(ProceduralAudio.Click(), 0.6f);
            }
            if (radioAnswerIn > 0f)
            {
                radioAnswerIn -= dt;
                if (radioAnswerIn <= 0f) RadioAnswer();
            }

            if (anger >= 100f) StartHunt(false);
        }

        void EmitClue()
        {
            // радио отвечает только на вопрос, остальные улики сущность подаёт сама
            var options = new List<Evidence>();
            foreach (var c in target.Clues) if (c != Evidence.Radio) options.Add(c);
            var clue = options[Random.Range(0, options.Count)];
            switch (clue)
            {
                case Evidence.Frost: frostTime = 5f; break;
                case Evidence.Whisper: PlayAt(ProceduralAudio.Whisper(), lair, 0.9f); break;
                case Evidence.Knock: PlayAt(ProceduralAudio.Knock(), lair, 1f); break;
                case Evidence.Static: staticTime = 5f; break;
                case Evidence.Compass: compassTime = 6f; break;
                case Evidence.NightVision:
                    silhouetteArmed = 9f;
                    if (nightVisionTarget < 0.5f) nextVisionSwitch = Mathf.Min(nextVisionSwitch, 0.5f);
                    break;
            }
            AddFear(1.5f);
        }

        void RadioAnswer()
        {
            if (Catalog.Has(target, Evidence.Radio))
            {
                sfx.PlayOneShot(ProceduralAudio.RadioVoice(), 1f);
                ui.Message(Loc.F("radio", Loc.T("radio_" + Random.Range(1, RadioAnswerCount + 1))), 3f);
                glitch = 1f;
                AddFear(4f);
            }
            else
            {
                staticNoise.volume = 0.3f * Settings.Ambience;
                ui.Message(Loc.T("radio_silence"), 2f);
            }
        }

        // игрок назвал сущность в дневнике
        void NameEntity(EntityKind kind)
        {
            if (phase != Phase.Investigate || named) return;
            named = true;
            namedRight = kind == target.Kind;
            if (namedRight)
            {
                ui.Message(Loc.T("named_right"), 3f);
                StartHunt(true);
            }
            else
            {
                AddFear(15f);
                ui.Message(Loc.T("named_wrong"), 3f);
                StartHunt(false, enraged: true);
            }
        }

        // ---------- приборы ----------

        // Показания прибора. Цель — логово во время расследования и сама сущность на охоте.
        void UpdateTools(float dt)
        {
            var aim = hunter != null && hunter.Alive ? hunter.transform.position : lair;
            float angle = Vector3.Angle(cam.transform.forward, aim - cam.transform.position);
            bool aimed = angle < 35f;

            // ЭМП: фон 1, у логова 2–3, с уликой «ЭМП 5» в момент всплеска — 5; на охоте рядом с ней — 4
            float emfTarget = 1f + Mathf.PerlinNoise(Time.time * 0.7f, 0f) * 0.6f;
            if (aimed) emfTarget = phase == Phase.Hunt ? 4f : 2.4f + Mathf.PerlinNoise(Time.time * 2f, 3f);
            if (aimed && phase == Phase.Investigate && compassTime > 0f && Catalog.Has(target, Evidence.Compass)) emfTarget = 5f;
            emf = Mathf.MoveTowards(emf, emfTarget, dt * 6f);

            // термометр: в комнате ~20 °C, у логова холоднее; с уликой «Холод» — ниже нуля
            float thermoTarget = 19.5f + Mathf.PerlinNoise(Time.time * 0.3f, 7f);
            if (aimed) thermoTarget = Catalog.Has(target, Evidence.Frost) ? -2.5f : 13f;
            thermo = Mathf.MoveTowards(thermo, thermoTarget, dt * 4f);

            switch (tool)
            {
                case Tool.Emf:
                    int level = Mathf.Clamp(Mathf.RoundToInt(emf), 1, 5);
                    ui.SetToolReadout("EMF  " + new string('■', level) + new string('□', 5 - level), level >= 5 ? new Color(1f, 0.3f, 0.25f) : level >= 3 ? new Color(1f, 0.8f, 0.4f) : new Color(0.86f, 0.83f, 0.76f));
                    beepTimer -= dt;
                    if (level >= 2 && beepTimer <= 0f)
                    {
                        sfx.PlayOneShot(ProceduralAudio.Beep(), 0.25f + level * 0.1f);
                        beepTimer = 1.4f / level;
                    }
                    break;
                case Tool.Thermo:
                    ui.SetToolReadout($"{thermo:0.0} °C", thermo < 0f ? new Color(0.6f, 0.85f, 1f) : new Color(0.86f, 0.83f, 0.76f));
                    break;
                case Tool.Night:
                    ui.SetToolReadout(Loc.T("night_on"), new Color(0.4f, 1f, 0.55f));
                    break;
                case Tool.Radio:
                    ui.SetToolReadout(Loc.T("radio_listen"), new Color(0.86f, 0.83f, 0.76f));
                    staticNoise.volume = Mathf.Max(staticNoise.volume, 0.08f * Settings.Ambience);
                    break;
            }
        }

        // ---------- защита ----------

        void UseProtection(Protect p)
        {
            if (phase is not (Phase.Investigate or Phase.Hunt)) return;
            bool active = p switch { Protect.Circle => circle, Protect.Candle => candleTime > 0f, Protect.Tumar => tumar, _ => false };
            if (active) { ui.Message(Loc.T("already"), 1.5f); return; }
            if (!inv.Use(p)) { Empty(Loc.F("no_more", Loc.T($"p.{p}.name"))); return; }

            switch (p)
            {
                case Protect.Circle:
                    circle = true;
                    ui.SaltBurst();
                    sfx.PlayOneShot(ProceduralAudio.SaltThrow(), 1f);
                    ui.Message(Loc.T("circle_set"), 2f);
                    break;
                case Protect.Candle:
                    candleTime = 60f;
                    sfx.PlayOneShot(ProceduralAudio.Click(), 0.6f);
                    ui.Message(Loc.T("candle_lit"), 2f);
                    break;
                case Protect.Tumar:
                    tumar = true;
                    sfx.PlayOneShot(ProceduralAudio.BellRing(), 0.3f);
                    ui.Message(Loc.T("tumar_on"), 2f);
                    break;
                case Protect.Calm:
                    fear = Mathf.Max(0f, fear - 40f);
                    ui.Message(Loc.T("calm_used"), 2f);
                    break;
            }
            ui.SetProtection(inv);
            ui.CloseBag();
        }

        void UpdateProtection(float dt)
        {
            if (candleTime > 0f)
            {
                candleTime -= dt;
                if (candleTime <= 0f) ui.Message(Loc.T("candle_out"), 1.5f);
            }
            ui.SetCandle(candleTime > 0f);

            // свеча замедляет тех, кто боится света
            if (hunter != null && candleTime > 0f && hunter.Def.Bane == ItemKind.Lantern) hunter.SpeedMul *= 0.6f;
        }

        // ---------- охота ----------

        void StartHunt(bool weakened, bool enraged = false)
        {
            phase = Phase.Hunt;
            frostTime = staticTime = compassTime = 0f;
            quietTime = heardTime = 0f;
            float aggression = (1f + (night - 1) * 0.08f) * (weakened ? 0.75f : enraged ? 1.4f : 1f);
            hunter = Entity.Create(target, lair, cam, this, aggression, 2.5f);
            hunter.Weakened = weakened;
            entities.Add(hunter);
            ui.SetPhase(Loc.T("phase_hunt"), true);
            if (!named) ui.Message(Loc.T("hunt_start"), 3.5f);
            sfx.PlayOneShot(ProceduralAudio.Screech(), 0.35f);
        }

        void UpdateHunt(float dt)
        {
            HandleInput(dt);

            if (hunter == null || !hunter.Alive)
            {
                if (banishedTime < 0f) banishedTime = 0f;
                banishedTime += dt;
                if (banishedTime > 1.6f) Dawn(true);
                return;
            }

            // шум выдаёт игрока, тишина и неподвижность прячут
            heardTime -= dt;
            if (mic.Loud && heardTime <= 0f)
            {
                heardTime = 3f;
                ui.Message(Loc.T("heard"), 1.5f);
            }
            bool still = TurnSpeed < 10f && !pressing && (!mic.Available || !mic.Speaking);
            quietTime = still ? quietTime + dt : 0f;
            bool hidden = quietTime > 2.5f && heardTime <= 0f;
            hunter.SpeedMul = heardTime > 0f ? 2.2f : hidden ? 0.35f : 1f;
            ui.SetHidden(hidden);

            // фото: первый раз, когда сущность близко и в кадре
            if (photo == null && !photoTaking && hunter.Visible && hunter.FlatDistance() < 3.2f &&
                Vector3.Angle(cam.transform.forward, hunter.transform.position - cam.transform.position) < 22f)
            {
                photoTaking = true;
                StartCoroutine(PhotoShare.Capture(tex => { photo = tex; photoTaking = false; nightXp += 25; ui.PhotoFlash(); }));
            }
        }

        void SharePhoto()
        {
            if (photo == null) return;
            if (!PhotoShare.Share(photo, Loc.F("share_text", night))) ui.Message(Loc.T("share_fail"), 2f);
        }

        // ---------- камера и атмосфера ----------

        // Камера: тёмная, иногда переключается в ночной режим; рядом с сущностью и у логова — помехи.
        void UpdateLook(float dt)
        {
            if (phase is Phase.Investigate or Phase.Hunt)
            {
                nextVisionSwitch -= dt;
                if (nextVisionSwitch <= 0f)
                {
                    bool on = nightVisionTarget < 0.5f;
                    nightVisionTarget = on ? 1f : 0f;
                    nextVisionSwitch = on ? Random.Range(10f, 16f) : Random.Range(18f, 32f);
                    sfx.PlayOneShot(ProceduralAudio.Click(), 0.8f);
                    glitch = Mathf.Max(glitch, 0.7f);
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

            // ночная камера держит ночное видение, пока выбрана
            if (tool == Tool.Night && phase is Phase.Investigate or Phase.Hunt) nightVisionTarget = 1f;

            // переключение режимов не плавное, а через короткий «сбой»
            nightVision = Mathf.MoveTowards(nightVision, nightVisionTarget, dt * 6f);
            staticNoise.volume = Mathf.Max(glitch * 0.35f * Settings.Ambience, Mathf.MoveTowards(staticNoise.volume, 0f, dt * 0.5f));
            // эпизоды ночного режима: зелёное ночное видение, а если оно уже выбрано — ИК-камера
            var style = Settings.Vision;
            var episode = style == Vision.NightGreen ? Vision.Cctv : Vision.NightGreen;
            if (gyroRig != null) gyroRig.SetLook(style, episode, nightVision, glitch);
            float baseGrain = style switch { Vision.Normal => 0f, Vision.Dark => 0.05f, _ => 0.06f };
            ui.DarkBase = style switch { Vision.Normal => 0.15f, Vision.Dark => 0.55f, _ => 0.38f };
            ui.SetGrain(baseGrain + nightVision * 0.08f + glitch * 0.25f);
        }

        string DiagnosticsText()
        {
            string rig = arRig != null ? arRig.Diagnostics() : gyroRig != null ? gyroRig.Diagnostics() : "старт…";
            string micState = mic == null ? "" : mic.Available ? $"\nмикрофон {mic.Level:0.00}" : mic.Denied ? "\nмикрофон запрещён" : "";
            return (map.Empty ? rig : $"{rig}\nкарта {Mathf.RoundToInt(map.Coverage * 100)}%") + micState;
        }

        void UpdateScan()
        {
            drone.volume = Mathf.MoveTowards(drone.volume, 0.25f * Settings.Ambience, Time.deltaTime * 0.3f);
            scanTimer -= Time.deltaTime;
            if (scanTimer > 0f) return;
            scanTimer = 0.15f;

            if (!gyroRig.SampleLuma(samplePoints, sampleLuma))
            {
                // камеры нет — играем без карты, логово выбирается случайно
                scanFailTime += 0.15f;
                if (scanFailTime > 4f) BeginInvestigation();
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
            if (map.Coverage >= ScanCoverage) BeginInvestigation();
        }

        // Место для сущности: сектор карты по привычке её логова, не ближе minAngle к взгляду игрока.
        // Расстояние: если место ниже горизонта — это пол, считаем по наклону и высоте глаз;
        // иначе это проём или дальняя стена — 3–4.5 м. maxDist > 0 ограничивает дальность.
        public Vector3 PickPosition(EntityDef def, float minAngle, float maxDist)
        {
            var camPos = cam.transform.position;
            var fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-3f) fwd = Vector3.forward;
            fwd.Normalize();

            if (!map.TryPick(fwd, minAngle, def.Lair, out float yaw, out float pitch))
            {
                float viewYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                yaw = viewYaw + Random.Range(minAngle, 180f) * (Random.value < 0.5f ? -1f : 1f);
                pitch = 0f;
            }

            float dist;
            if (def.Lair == Lair.Near) dist = Random.Range(1.4f, 2.2f);
            else if (def.Lair == Lair.Far) dist = Random.Range(3.8f, 4.8f);
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

        void Dawn(bool banished)
        {
            phase = Phase.Dawn;
            ClearEntities();
            ui.SetHidden(false);
            string supply = inv.Resupply();
            string verdict = banished
                ? Loc.F("verdict_banished", target.Name) + (namedRight ? "\n" + Loc.T("verdict_named_right") : "")
                : Loc.F("verdict_survived", target.Name);
            if (named && !namedRight) verdict += "\n" + Loc.T("verdict_named_wrong");
            else if (banished && namedRight && !wrongItemUsed) verdict += "\n" + Loc.T("verdict_clean");
            ui.ShowDawn(night, verdict, supply, photo);

            // опыт за ночь: дожил, изгнал, назвал верно, без ошибок, находки
            int xp = 100 + 20 * night + nightXp
                     + (banished ? 150 : 0)
                     + (namedRight ? 100 : 0)
                     + (banished && namedRight && !wrongItemUsed ? 50 : 0);
            bool rankUp = Progress.AddXp(xp);
            bool newPage = Progress.AddSurvivedNight();
            ui.ShowNightProgress(xp, rankUp, newPage);
        }

        void GameOver()
        {
            lostIn = phase;
            phase = Phase.Over;
            foreach (var e in entities) if (e != null) e.gameObject.SetActive(false);
            DestroyStash();
            ui.SetHidden(false);
            ui.SetSecondChance(!revivedThisNight);
            int best = Mathf.Max(night, PlayerPrefs.GetInt("bestNight", 0));
            PlayerPrefs.SetInt("bestNight", best);
            PlayerPrefs.Save();
            ui.ShowGameOver(night, nightTime / NightSeconds, best, target.Name, photo);

            // за проигранную ночь — немного опыта за пройденное время (один раз за ночь)
            if (!overXpGiven)
            {
                overXpGiven = true;
                int xp = Mathf.RoundToInt(nightTime / NightSeconds * 60f) + nightXp;
                ui.ShowNightProgress(xp, Progress.AddXp(xp), false);
            }
        }

        // «Второй шанс» за наградную рекламу: игрок приходит в себя, сущность отступает. Раз за ночь.
        void Revive()
        {
            if (phase != Phase.Over || revivedThisNight) return;
            revivedThisNight = true;
            phase = lostIn;
            fear = 45f;
            foreach (var e in entities)
            {
                if (e == null) continue;
                e.gameObject.SetActive(true);
                e.Repel();
            }
            ui.ShowHud();
            ui.Message(Loc.T("revived"), 3f);
        }

        void PlayAt(AudioClip clip, Vector3 pos, float volume) =>
            AudioSource.PlayClipAtPoint(clip, pos, volume * Settings.Sfx);

        // ---------- тайники ----------

        // Иногда в тёмном месте комнаты появляется блик: посмотри на него секунду — и найдёшь припасы.
        void UpdateStash(float dt)
        {
            if (stash == null)
            {
                if (phase != Phase.Investigate || stashesThisNight >= 2) return;
                stashTimer -= dt;
                if (stashTimer > 0f) return;
                SpawnStash();
                return;
            }

            stashLife -= dt;
            if (stashLife <= 0f) { DestroyStash(); return; }

            var to = stash.transform.position - cam.transform.position;
            float angle = Vector3.Angle(cam.transform.forward, to);
            stashGaze = angle < 9f ? stashGaze + dt : Mathf.Max(0f, stashGaze - dt * 0.5f);
            float pulse = 0.12f + 0.05f * Mathf.Sin(Time.time * 6f) + stashGaze * 0.12f;
            stash.transform.localScale = Vector3.one * pulse;
            stash.transform.rotation = Quaternion.LookRotation(to, Vector3.up);

            stashSound -= dt;
            if (stashSound <= 0f)
            {
                stashSound = 4f;
                PlayAt(ProceduralAudio.Shimmer(), stash.transform.position, 0.25f);
            }

            if (stashGaze >= 1f)
            {
                string what = inv.Stash();
                nightXp += 25;
                sfx.PlayOneShot(ProceduralAudio.BellRing(), 0.3f);
                ui.Message(Loc.F("found_item", what), 2f);
                ui.SetProtection(inv);
                DestroyStash();
            }
        }

        void SpawnStash()
        {
            stashesThisNight++;
            stashTimer = Random.Range(45f, 70f);
            stashLife = 30f;
            stashGaze = 0f;
            stashSound = 0f;

            // место — тёмный угол на карте, на высоте мебели
            var pos = PickPosition(Catalog.Get(EntityKind.Shade), 50f, 3f);
            pos.y = cam.transform.position.y - EyeHeight + Random.Range(0.3f, 1.1f);
            stash = GameObject.CreatePrimitive(PrimitiveType.Quad);
            stash.name = "Stash";
            Destroy(stash.GetComponent<Collider>());
            stash.transform.position = pos;
            var mr = stash.GetComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Shader.Find("GhostWard/Spark"));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mf = stash.GetComponent<MeshFilter>();
            var colors = new Color[mf.mesh.vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color(1f, 0.8f, 0.45f, 1f);
            mf.mesh.colors = colors;

            if (!stashHintShown)
            {
                stashHintShown = true;
                ui.Message(Loc.T("stash_hint"), 4f);
            }
        }

        void DestroyStash()
        {
            if (stash == null) return;
            Destroy(stash.GetComponent<MeshRenderer>().sharedMaterial);
            Destroy(stash);
            stash = null;
        }

        // ---------- пугающие события ----------

        // Время от времени: шаги за спиной, хлопок двери, дыхание у самого уха, мигание света, шёпот.
        void UpdateScares(float dt)
        {
            eventTimer -= dt;
            if (eventTimer > 0f) return;
            eventTimer = Random.Range(16f, 32f) / (1f + fear / 100f);

            var camPos = cam.transform.position;
            var back = -cam.transform.forward;
            back.y = 0f;
            back.Normalize();
            var side = Vector3.Cross(Vector3.up, back);
            switch (Random.Range(0, 5))
            {
                case 0:
                    PlayAt(ProceduralAudio.Footsteps(), camPos + back * 2.5f + side * Random.Range(-1.5f, 1.5f), 0.8f);
                    break;
                case 1:
                    PlayAt(ProceduralAudio.Crash(), camPos + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * 4.5f, 0.45f);
                    ui.Shake(0.25f);
                    break;
                case 2:
                    PlayAt(ProceduralAudio.Breath(), camPos + back * 0.5f, 0.5f);
                    AddFear(2f);
                    break;
                case 3:
                    // мигание света: экран дважды гаснет
                    sfx.PlayOneShot(ProceduralAudio.Click(), 0.7f);
                    ui.Flash(new Color(0f, 0f, 0f, 0.95f));
                    StartCoroutine(SecondFlicker());
                    break;
                default:
                    PlayAt(ProceduralAudio.Whisper(), camPos + side * (Random.value < 0.5f ? 0.4f : -0.4f), 0.45f);
                    break;
            }
        }

        IEnumerator SecondFlicker()
        {
            yield return new WaitForSeconds(0.35f);
            sfx.PlayOneShot(ProceduralAudio.Click(), 0.5f);
            ui.Flash(new Color(0f, 0f, 0f, 0.9f));
        }

        void ClearEntities()
        {
            foreach (var e in entities) if (e != null) e.Vanish();
            entities.Clear();
            hunter = null;
        }

        // ---------- события от сущностей ----------

        public bool IsInView(Vector3 pos)
        {
            var vp = cam.WorldToViewportPoint(pos);
            return vp.z > 0f && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
        }

        public void OnManifest(Entity e)
        {
            // при самой первой встрече — подсказка и картинка в дневнике; дальше игрок опознаёт сам
            if (Catalog.Seen(e.Def.Kind)) return;
            Catalog.MarkSeen(e.Def.Kind);
            ui.Message(e.Def.Hint + "\n\n" + Loc.T("new_entry"), 6f);
        }

        public void OnThrow(Entity e)
        {
            AddFear(7f);
            ui.Shake(0.6f);
        }

        public void OnAttack(Entity e)
        {
            if (circle)
            {
                circle = false;
                ui.SaltBurst();
                ui.Flash(new Color(1f, 1f, 1f, 0.4f));
                ui.Message(Loc.T("circle_saved"), 2f);
                sfx.PlayOneShot(ProceduralAudio.Banish(), 0.6f);
                ui.SetProtection(inv);
                return;
            }
            AddFear(e.Def.Kind == EntityKind.Poltergeist ? 22f : 32f);
            ui.Jumpscare(e.Def, e.Seed, e.Def.Kind == EntityKind.Poltergeist ? 0.5f : 1f);
            sfx.PlayOneShot(ProceduralAudio.Screech(), 0.8f);
#if UNITY_ANDROID || UNITY_IOS
            if (Settings.Vibration) Handheld.Vibrate();
#endif
        }

        void AddFear(float v)
        {
            fear = Mathf.Clamp(fear + v, 0f, 100f);
            if (fear >= 100f && phase is Phase.Investigate or Phase.Hunt) GameOver();
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
                if (d < 3f) AddFear(dt * 7f * (1f - d / 3f) * (candleTime > 0f ? 0.5f : 1f));
            }
            if (calm) fear = Mathf.Max(0f, fear - FearDecay * (candleTime > 0f ? 2f : 1f) * dt);
            ui.SetFear(fear / 100f);
        }

        void UpdateAtmosphere(float dt)
        {
            drone.volume = Mathf.MoveTowards(drone.volume, (0.35f + fear / 100f * 0.4f) * Settings.Ambience, dt * 0.3f);

            // сердце бьётся чаще со страхом
            heartTimer -= dt;
            if (fear > 15f && heartTimer <= 0f)
            {
                heart.volume = Mathf.Lerp(0.2f, 0.9f, fear / 100f) * Settings.Ambience;
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
                if (inv.Iron <= 0) { Empty(Loc.F("no_more", def.Name)); return; }
                if (Target(angle, range) == null)
                {
                    ui.Message(Loc.T(Target(30f, 6f) != null ? "too_far" : "nobody"), 1.5f);
                    return;
                }
            }
            if (!inv.Use(item)) { Empty(Loc.F("no_more", def.Name)); return; }

            switch (item)
            {
                case ItemKind.Salt: sfx.PlayOneShot(ProceduralAudio.SaltThrow(), 1f); ui.SaltBurst(); break;
                case ItemKind.Iron: sfx.PlayOneShot(ProceduralAudio.IronStrike(), 1f); ui.Flash(new Color(1f, 1f, 1f, 0.5f)); break;
                case ItemKind.Mirror: sfx.PlayOneShot(ProceduralAudio.Shimmer(), 1f); ui.Flash(new Color(0.8f, 0.9f, 1f, 0.6f)); break;
                case ItemKind.Wormwood: sfx.PlayOneShot(ProceduralAudio.Whoosh(), 1f); ui.Smoke(); break;
            }
            ui.Supply($"{def.Name}: {inv.Label(item)}");

            var t = Target(angle, range);
            if (t == null) { ui.Message(Loc.T("wasted"), 1.2f); return; }
            if (t.Def.Bane == item) t.Banish();
            else WrongItem(t);
        }

        void RingBell()
        {
            if (bellCooldown > 0f) return;
            bellCooldown = 1.2f;
            if (inv.Bell <= 0) { Empty(Loc.T("bell_cracked")); return; }
            inv.Bell--;
            sfx.PlayOneShot(ProceduralAudio.BellRing(), 1f);
            ui.Supply($"{Catalog.Get(ItemKind.Bell).Name}: {inv.Bell}");

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
                inv.Battery = Mathf.Max(0f, inv.Battery - 9f * (Progress.SlowLantern ? 0.75f : 1f) * dt);
                if (inv.Battery <= 0f) Empty(Loc.T("lantern_out"));
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
            wrongItemUsed = true;
            e.Provoke();
            AddFear(8f);
            sfx.PlayOneShot(ProceduralAudio.Fail(), 0.6f);
            ui.Message(Loc.T("no_effect"), 1.5f);
        }

        void Empty(string text)
        {
            sfx.PlayOneShot(ProceduralAudio.Fail(), 0.5f);
            ui.Message(text, 1.5f);
        }
    }
}
