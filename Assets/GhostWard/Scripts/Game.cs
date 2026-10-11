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
        }

        void Update()
        {
            ui.SetDiagnostics(DiagnosticsText());
            if (ui.JournalOpen) return;

            if (cam != null) ui.SetMapStrip(map, cam.transform.forward, phase == Phase.Scan);
            if (phase == Phase.Scan) { UpdateScan(); return; }
            if (phase is not (Phase.Investigate or Phase.Hunt))
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
            ui.SetClock(nightTime / NightSeconds);
            if (nightTime >= NightSeconds) { Dawn(false); return; }

            if (phase == Phase.Investigate) UpdateInvestigation(dt);
            else UpdateHunt(dt);

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
            anger += dt * (0.4f + night * 0.05f);
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
                AudioSource.PlayClipAtPoint(ProceduralAudio.Creak(), cam.transform.position + dir * 3f, 0.45f);
            }

            float angle = AngleToLair();
            ui.SetCompass(cam.transform.forward, compassTime > 0f && angle < 40f ? 1f : 0f);
            if (staticTime > 0f && angle < 35f) glitch = Mathf.Max(glitch, 0.85f);
            if (silhouetteArmed > 0f && nightVision > 0.9f && angle < 30f && silhouetteCooldown <= 0f)
            {
                Apparition.Show(target, lair, cam.transform, 0.45f);
                silhouetteArmed = 0f;
                silhouetteCooldown = 3f;
            }

            // радио духов: игрок задаёт вопрос вслух
            if (mic.Speaking && radioCooldown <= 0f)
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
                case Evidence.Whisper: AudioSource.PlayClipAtPoint(ProceduralAudio.Whisper(), lair, 0.9f); break;
                case Evidence.Knock: AudioSource.PlayClipAtPoint(ProceduralAudio.Knock(), lair, 1f); break;
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
                staticNoise.volume = 0.3f;
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
                StartCoroutine(PhotoShare.Capture(tex => { photo = tex; photoTaking = false; ui.PhotoFlash(); }));
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

            // переключение режимов не плавное, а через короткий «сбой»
            nightVision = Mathf.MoveTowards(nightVision, nightVisionTarget, dt * 6f);
            staticNoise.volume = Mathf.Max(glitch * 0.35f, Mathf.MoveTowards(staticNoise.volume, 0f, dt * 0.5f));
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
            drone.volume = Mathf.MoveTowards(drone.volume, 0.25f, Time.deltaTime * 0.3f);
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
        }

        void GameOver()
        {
            phase = Phase.Over;
            ClearEntities();
            ui.SetHidden(false);
            int best = Mathf.Max(night, PlayerPrefs.GetInt("bestNight", 0));
            PlayerPrefs.SetInt("bestNight", best);
            PlayerPrefs.Save();
            ui.ShowGameOver(night, nightTime / NightSeconds, best, target.Name, photo);
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
                inv.Battery = Mathf.Max(0f, inv.Battery - 9f * dt);
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
