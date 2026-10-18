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
        float floorY;    // уровень пола, запомненный в начале ночи (в AR телефон может подниматься и опускаться)
        public float Floor => arRig != null ? floorY : cam.transform.position.y - PhoneHeight;
        // высота телефона над полом: в гироскопе — по карте комнаты (пропорция пола и потолка), иначе обычная
        float PhoneHeight => Room != null ? Room.PhoneHeight : EyeHeight;

        // Высота середины фигуры: по полу ходят — ступни на полу; летающие парят не ниже 25 см над полом
        // и не упираются в потолок; на потолке — макушкой к потолку.
        public float HoverY(EntityDef def, float centerY)
        {
            var f = Entity.FigureOf(def);
            float floor = Floor, top = floor + CeilingHeight - 0.05f;
            float low = floor + f.Feet + (def.Stance is Stance.Float or Stance.Eye ? 0.25f : 0f);
            float high = top - (f.Size.y - f.Feet);
            return high < low ? low : Mathf.Clamp(centerY, low, high);
        }

        float StanceY(EntityDef def, float floatY)
        {
            var f = Entity.FigureOf(def);
            return def.Stance switch
            {
                Stance.Floor => Floor + f.Feet,
                Stance.Ceiling => Floor + CeilingHeight - (f.Size.y - f.Feet),
                // призраки (фигура человека в полный рост) парят чуть над полом, а не висят на уровне глаз
                Stance.Float when IsDrifter(def) => HoverY(def, Floor + Entity.FigureOf(def).Feet + 0.25f),
                _ => HoverY(def, floatY)
            };
        }

        public static bool IsDrifter(EntityDef def) => Entity.ArchOf(def.Kind) == EntityKind.Ghost;

        // Рядом с прежним местом: чуть ближе к игроку (closer — доля дистанции) и немного вбок, не в стене.
        // Для «моргания» призрака: пропал на миг — и стоит почти там же, но ближе.
        public Vector3 NearbyPosition(EntityDef def, Vector3 from, float closer)
        {
            var camPos = cam.transform.position;
            var flat = from - camPos;
            flat.y = 0f;
            float dist = Mathf.Max(1.3f, flat.magnitude * closer);
            float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg + Random.Range(-12f, 12f);
            float limit = RoomLimit(yaw);
            if (limit > 0f) dist = Mathf.Clamp(dist, 1.1f, Mathf.Max(limit, 1.1f));
            var pos = camPos + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * dist;
            pos.y = StanceY(def, from.y);
            return pos;
        }

        // половина горизонтального угла обзора: в портретной ориентации кадр узкий (~15°)
        float HalfFovX => Mathf.Atan(Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect) * Mathf.Rad2Deg;
        const float ScanCoverage = 0.85f;
        // высота потолка: по карте глубины комнаты, если её уже сняли; иначе обычная
        float CeilingHeight => Room != null && Room.CeilingHeight > 0f ? Room.CeilingHeight : 2.6f;
        RoomMap Room => gyroRig != null ? gyroRig.Room : null;
        readonly StepDetector steps = new();
        bool walkHinted;
        float walkCooldown;
        Quaternion lastRoomRot;

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
        float markTime;   // сколько ещё компас показывает метку логова после улики
        static readonly int GwStyle = Shader.PropertyToID("_GW_Style"), GwAlt = Shader.PropertyToID("_GW_Alt"),
                            GwMode = Shader.PropertyToID("_GW_Mode"), GwLight = Shader.PropertyToID("_GW_Light");
        float roomLight = 0.3f;    // средняя яркость комнаты по осмотру
        bool faintedThisNight;     // первый «полный страх» за ночь — обморок, а не смерть
        float wanderTimer;         // когда сущность в следующий раз покажется во время расследования
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

        // модификатор ночи, дело дня, трофеи
        Modifier modifier;
        bool dailyMode, pendingDaily, heardThisHunt;
        float thunderTimer, thunderMask;

        // подсказки и тепловизор
        readonly HashSet<Evidence> cluesShown = new();
        bool radioAnswered, radioHinted, journalHinted, lureUsed, lureHinted;
        float radioHintAt, lookHintAt, lureHintAt;
        GameObject coldSpot;
        Material coldMat;
        bool possessedNight, possessionShown, animalShown;
        bool newCase, lureMatched;   // новая сущность этой ночью; приманка откликнулась

        // подсказки «чем проверить» — пока игрок новичок (первые три пережитые ночи)
        bool Beginner => Progress.NightsSurvived < 3 && !prologue;

        // музыка и обучающая первая ночь (пролог)
        Music music;
        bool prologue, nameMusicPlayed;
        int proStep;
        float proTimer, proIdle;
        const int ProSteps = 8;
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
        float TurnSign;   // куда игрок поворачивается: +1 вправо, −1 влево
        float recaptureUntil;
        Vector3 lastForward;

        // осмотр комнаты
        Vector2[] samplePoints;
        float[] sampleLuma;
        float scanTimer, scanFailTime, scanWait;
        bool ceilingAsked;

        // жесты
        readonly Gestures gestures = new();
        float bellCooldown;

        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.Portrait;

            sfx = gameObject.AddComponent<AudioSource>();
            music = Music.Create(gameObject);
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
            Know.EnsureStarters();
            Ads.Init();
            Trophies.Unlocked += t => ui.TrophyToast(Loc.F("trophy_got", Trophies.Name(t), Trophies.Reward));
            CreateUI();
            if (Loc.Chosen) ui.ShowMenu();
            else ui.ShowLanguage();
            phase = Phase.Menu;
            yield return AskPermissions();
        }

        // Камера и микрофон — сразу при запуске, одним запросом, а не посреди ночи.
        IEnumerator AskPermissions()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var need = new System.Collections.Generic.List<string>();
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
                need.Add(UnityEngine.Android.Permission.Camera);
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
                need.Add(UnityEngine.Android.Permission.Microphone);
            if (need.Count == 0) yield break;
            int answered = 0;
            var cb = new UnityEngine.Android.PermissionCallbacks();
            cb.PermissionGranted += _ => answered++;
            cb.PermissionDenied += _ => answered++;
            UnityEngine.Android.Permission.RequestUserPermissions(need.ToArray(), cb);
            float wait = 60f;
            while (answered < need.Count && wait > 0f) { wait -= Time.unscaledDeltaTime; yield return null; }
#elif UNITY_IOS && !UNITY_EDITOR
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam | UserAuthorization.Microphone);
#else
            yield break;
#endif
        }

        void CreateUI()
        {
            ui = GameUI.Create();
            ui.PreviewCamera = () => gyroRig != null ? gyroRig.Webcam : null;
            ui.StartPressed += () => { Ads.OnRetry(); StartGame(); };
            ui.PlayPressed += () =>
            {
                pendingDaily = false;
                // первая игра — живой пролог: обучение прямо в ночи, без страниц
                if (Settings.TutorialSeen || !Settings.PrologueDone) StartCoroutine(Play());
                else ui.OpenTutorial(true);
            };
            ui.TutorialFinished += () => StartCoroutine(Play());
            ui.DailyPressed += () =>
            {
                pendingDaily = true;
                if (Settings.TutorialSeen) StartCoroutine(Play());
                else ui.OpenTutorial(true);
            };
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
            ui.NextNightPressed += () =>
            {
                // дело дня — одна ночь; после рассвета — в меню
                if (dailyMode) ToMenu();
                else StartNight();
            };
            ui.ToolSelected += t =>
            {
                // ушли с ночной камеры или с тепловизора — вид сразу возвращается, без ожидания случайного эпизода
                if (t != Tool.Night && tool == Tool.Night) nightVisionTarget = 0f;
                if (t != tool) nextVisionSwitch = Mathf.Max(nextVisionSwitch, Random.Range(12f, 20f));
                if (t is Tool.Thermo or Tool.Night) nightVision = t == Tool.Night ? 1f : 0f;   // мгновенно, без растворения
                tool = t;
                ui.SetTool(t);
                sfx.PlayOneShot(ProceduralAudio.Click(), 0.5f);
            };
            ui.ProtectUsed += UseProtection;
            ui.LureUsed += UseLure;
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
                gestures.Reset();
                ui.CanName = open && phase == Phase.Investigate && !named && (!prologue || proStep >= 7);
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
            dailyMode = pendingDaily;
            StartGame();
            ui.Message(Loc.T(arRig != null ? "mode_ar" : "mode_gyro"), 3f);
        }

        void ToMenu()
        {
            ui.SetInstruction("", 0, 0);
            dailyMode = false;
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
            prologue = !Settings.PrologueDone && !dailyMode;
            StartNight();
        }

        void StartNight()
        {
            ClearEntities();
            night++;
            fear = Mathf.Min(fear, 20f);
            gestures.Reset();
            if (photo != null) Destroy(photo);
            photo = null;
            circle = tumar = false;
            candleTime = 0f;
            revivedThisNight = bonusTaken = overXpGiven = faintedThisNight = false;
            nightXp = 0;
            stashesThisNight = 0;
            stashTimer = Random.Range(30f, 50f);
            eventTimer = Random.Range(15f, 25f);
            signatureTimer = Random.Range(25f, 40f);
            wanderTimer = Random.Range(14f, 22f);
            DestroyStash();
            ui.SetTool(tool);
            ui.SetProtection(inv);
            ui.CloseBag();
            target = prologue ? Catalog.Get(EntityKind.Ghost) : dailyMode ? Catalog.Get(Daily.Entity) : PickTarget();
            newCase = Know.LearnName(target.Kind);
            EntitySounds.Prewarm(target.Kind);
            music.SetTheme(target.Kind);
            lureMatched = false;
            modifier = prologue ? Modifier.None : dailyMode ? Daily.Modifier : Modifiers.Roll(night);
            heardThisHunt = false;
            ironNowShown = false;
            // ночь «одержимости»: со второй ночи в трети ночей; в тестовой сборке — всегда, чтобы было что проверять
            possessedNight = !prologue && (Unlocks.TestUnlockAll || night >= 2 && Random.value < 0.35f);
            possession = possessionTimer = 0f;
            possessionCooldown = Random.Range(8f, 15f);
            possessionCount = 0;
            nameMusicPlayed = false;
            proStep = 0;
            proTimer = proIdle = 0f;
            possessionShown = animalShown = false;
            thunderTimer = Random.Range(10f, 18f);
            thunderMask = 0f;
            ui.SetModifier(modifier == Modifier.None ? "" : Modifiers.Name(modifier));
            ui.ShowHud();
            ui.SetNight(night);
            if (prologue) ui.SetNightTitle(Loc.T("prologue"));
            ui.LogNight(prologue ? Loc.T("prologue") : Loc.F("night_n", night));
            ui.SetClock(0f);
            ui.SetPhase("", false);
            ui.ClearEvidence();
            mic.Begin();

            // карта темноты строится заново каждую ночь: свет в комнате мог измениться
            map.Clear();
            Room?.Clear();
            steps.Reset();
            walkHinted = false;
            scanFailTime = scanWait = 0f;
            ceilingAsked = false;
            if (gyroRig != null || arRig != null)
            {
                phase = Phase.Scan;
                ui.SetScan(0f);
                if (prologue)
                {
                    ui.Message(Loc.T("pro_intro"), 7f);
                    Step(1);
                }
                else ui.Message(Loc.T("scan_msg"), 4f);
            }
            else BeginInvestigation();
        }

        // Сущность ночи: из пула, который растёт с каждой ночью; невстреченные — чаще.
        // Сущность ночи: если звание открыло новую и игрок её ещё не встречал — она приходит «новым делом»;
        // иначе случайная из открытых, чаще те, о ком игрок знает меньше.
        EntityDef PickTarget()
        {
            // тест: сущность, выбранная в дневнике кнопкой «Тест: следующей ночью»
            int test = PlayerPrefs.GetInt("test_next", -1);
            if (Unlocks.TestUnlockAll && test >= 0 && test < Catalog.Entities.Length)
            {
                PlayerPrefs.DeleteKey("test_next");
                return Catalog.Entities[test];
            }
            var fresh = Know.NextNewCase();
            if (fresh.HasValue) return Catalog.Get(fresh.Value);
            int pool = Know.UnlockedCount;
            float total = 0f;
            var weights = new float[pool];
            for (int i = 0; i < pool; i++)
                total += weights[i] = 1f + (3 - Know.CluesKnown(Catalog.Entities[i].Kind)) * 0.5f;
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
            if (newCase) named = true;   // кто это — уже известно, называть не нужно
            banishedTime = -1f;
            clueTimer = Random.Range(3f, 5f);
            fakeTimer = Random.Range(8f, 14f);
            frostTime = staticTime = compassTime = silhouetteArmed = 0f;
            radioCooldown = 0f;
            radioAnswerIn = -1f;
            nextVisionSwitch = Random.Range(12f, 20f);
            nightVisionTarget = 0f;

            floorY = arRig != null && arRig.TryFloor(out float planeY) ? planeY : cam.transform.position.y - PhoneHeight;
            markTime = 0f;
            // логово: албасты — за спиной, остальные — по своей привычке на карте темноты
            lair = PickPosition(target, target.Lair == Lair.Behind ? 140f : 0f, -1f);
            if (target.Lair == Lair.Ceiling && target.Stance != Stance.Ceiling) lair.y = Floor + CeilingHeight - 0.5f;

            phase = Phase.Investigate;
            ui.SetPhase(Loc.T("phase_investigate"), false);
            ui.SetCameraOverlay(Settings.Vision);
            string intro = dailyMode ? Loc.T("daily_intro") : night == 1 ? Loc.T("intro_first") : Loc.F("intro_n", night);
            if (modifier != Modifier.None)
                intro += $"\n\n<b>{Modifiers.Name(modifier)}.</b> {Modifiers.Desc(modifier)}\n" + Loc.F("xp_bonus", Modifiers.XpFactor(modifier).ToString("0.0#"));
            if (newCase) intro = Loc.F("new_case", target.Name);
            if (prologue) Step(2);
            else ui.Message(intro, 7f);
            music.Play(Music.Stinger.Swell, newCase ? 0.6f : 0.25f);

            cluesShown.Clear();
            radioAnswered = radioHinted = journalHinted = lureUsed = lureHinted = false;
            radioHintAt = 45f;
            lookHintAt = 25f;
            lureHintAt = 60f;
            if (Beginner) ui.Hint(Loc.T("h.Start"));
            else if (!prologue && !Skills.All) ui.Hint(Loc.T("h.Practice"));
            CreateColdSpot();

            // бонус звания «Чутьё»: одна улика этой ночи отмечена заранее
            if (Progress.Instinct) ui.MarkClue((int)target.Clues[Random.Range(0, target.Clues.Length)]);
        }

        void Update()
        {
            sfx.volume = Settings.Sfx;
            if (TestMode.On)
            {
                ui.SetDiagnostics(DiagnosticsText());
                ui.SetPhoneHeight(PhoneHeightText());
            }
            UpdateRoom();
            if (ui.JournalOpen) return;

            if (cam != null) ui.SetMapStrip(map, cam.transform.forward, phase == Phase.Scan);
            if (phase == Phase.Scan) { music.SetScan(); UpdateScan(); UpdateLook(Time.deltaTime); return; }   // приборы видны и во время осмотра
            if (phase is not (Phase.Investigate or Phase.Hunt))
            {
                if (phase == Phase.Over) music.SetSilence();
                else music.SetMenu();
                drone.volume = Mathf.MoveTowards(drone.volume, 0.15f * Settings.Ambience, Time.deltaTime);
                staticNoise.volume = 0f;
                UpdateLook(Time.deltaTime);
                return;
            }

            float dt = Time.deltaTime;
            entities.RemoveAll(e => e == null);

            var fwd = cam.transform.forward;
            TurnSpeed = Mathf.Lerp(TurnSpeed, Vector3.Angle(fwd, lastForward) / Mathf.Max(dt, 1e-3f), dt * 8f);
            float turnNow = Vector3.Cross(lastForward, fwd).y;
            if (Mathf.Abs(turnNow) > 1e-4f) TurnSign = Mathf.Lerp(TurnSign, Mathf.Sign(turnNow), dt * 6f);
            lastForward = fwd;

            if (!prologue) nightTime += dt;
            ui.SetClock(nightTime / NightSeconds);
            if (nightTime >= NightSeconds) { Dawn(false); return; }

            if (phase == Phase.Investigate) UpdateInvestigation(dt);
            else UpdateHunt(dt);
            if (prologue) UpdatePrologue(dt);
            UpdateMusic();
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

        // ---------- музыка ----------

        void UpdateMusic()
        {
            float near = 0f;
            foreach (var e in entities)
                if (e != null && e.Alive && e.Current != Entity.State.Lurking)
                    near = Mathf.Max(near, Mathf.Clamp01(1f - (e.FlatDistance() - 0.8f) / 3f));
            if (phase == Phase.Investigate) music.SetInvestigation(anger / 100f, fear / 100f);
            else if (phase == Phase.Hunt) music.SetHunt(near, fear / 100f);
        }

        // ---------- пролог: обучение прямо в первой ночи ----------

        void Step(int n)
        {
            proStep = n;
            proTimer = proIdle = 0f;
            ui.SetInstruction(Loc.T(n == 6 && !mic.Available ? "pro_6" : $"pro_{n}"), n, ProSteps);
            if (n > 1) sfx.PlayOneShot(ProceduralAudio.Click(), 0.4f);
        }

        // каждый шаг ждёт действия игрока; проиграть нельзя, злость не растёт до шага «назови её»
        void UpdatePrologue(float dt)
        {
            float angle = AngleToLair();
            bool aimed = angle < 35f;
            proIdle += dt;
            switch (proStep)
            {
                case 2:     // повернуться на шёпот
                    proTimer = aimed ? proTimer + dt : 0f;
                    if (proTimer > 1f) Step(3);
                    break;
                case 3:     // ЭМП
                    proTimer = tool == Tool.Emf && aimed && emf >= 1.8f ? proTimer + dt : 0f;
                    if (proTimer > 1.2f) Step(4);
                    break;
                case 4:     // тепловизор
                    proTimer = tool == Tool.Thermo && aimed ? proTimer + dt : 0f;
                    if (proTimer > 1.5f) Step(5);
                    break;
                case 5:     // отметить «Холод»
                    if (ui.IsMarked(Evidence.Frost)) Step(6);
                    break;
                case 6:     // отметить «Шёпот» и спросить радио
                    // без микрофона радио «ловит голос» по касанию экрана
                    if (tool == Tool.Radio && (!mic.Available) && radioCooldown <= 0f && proIdle > 1f)
                    {
                        ui.Hint(Loc.T("pro_6b"));
                        var p = UnityEngine.InputSystem.Pointer.current;
                        if (p != null && p.press.wasPressedThisFrame && !ui.IsOverButton(p.position.ReadValue()))
                        {
                            radioCooldown = 7f;
                            radioAnswerIn = 1.3f;
                        }
                    }
                    if (radioAnswered) Step(7);
                    break;
                case 7:     // назвать — охота начнётся в NameEntity
                    if (phase == Phase.Hunt) Step(8);
                    break;
            }

            // долго стоит на месте — мягко напомнить, куда смотреть
            if (proStep >= 2 && proStep <= 4 && proIdle > 15f)
            {
                proIdle = 0f;
                ui.Hint(Loc.F("h.Look", Direction(lair)));
            }
        }

        // ---------- расследование ----------

        float AngleToLair()
        {
            var to = lair - cam.transform.position;
            return Vector3.Angle(cam.transform.forward, to);
        }

        void UpdateInvestigation(float dt)
        {
            if (!prologue)
                anger += dt * (0.4f + night * 0.05f) * (tumar ? 0.6f : 1f) * (modifier == Modifier.FullMoon ? 1.5f : 1f);
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
            if (modifier != Modifier.Silence) fakeTimer -= dt;
            if (fakeTimer <= 0f)
            {
                fakeTimer = Random.Range(10f, 18f);
                var dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                PlayAt(ProceduralAudio.Creak(), cam.transform.position + dir * 3f, 0.45f);
            }

            float angle = AngleToLair();
            markTime -= dt;
            ui.SetCompass(cam.transform.forward, 0f, markTime > 0f ? (float?)YawTo(lair) : null);
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

            radioHintAt -= dt;
            if (Beginner && radioHintAt <= 0f && !radioHinted && !radioAnswered && Catalog.Has(target, Evidence.Radio))
            {
                radioHinted = true;
                ui.Hint(Loc.T("h.Radio"));
            }

            if (Beginner)
            {
                // долго не смотрел в сторону логова — подтолкнуть
                lookHintAt = angle < 35f ? 25f : lookHintAt - dt;
                if (lookHintAt <= 0f)
                {
                    lookHintAt = 25f;
                    ui.Hint(Loc.F("h.Look", Direction(lair)));
                }
                lureHintAt -= dt;
                if (lureHintAt <= 0f && !lureUsed && !lureHinted && !journalHinted)
                {
                    lureHinted = true;
                    ui.Hint(Loc.T("h.Lure"));
                }
            }

            UpdateColdSpot(dt);
            UpdatePrints(dt);
            UpdateSignature(dt);
            UpdateWander(dt);
            UpdatePossession(dt);
            UpdateThermalLife();
            if (phase == Phase.Investigate && anger >= 100f) StartHunt(false);
        }

        void EmitClue()
        {
            // радио отвечает только на вопрос, остальные улики сущность подаёт сама
            var options = new List<Evidence>();
            foreach (var c in target.Clues) if (c != Evidence.Radio) options.Add(c);
            var clue = options[Random.Range(0, options.Count)];
            if (prologue) clue = proStep >= 4 && proStep <= 5 ? Evidence.Frost : Evidence.Whisper;
            switch (clue)
            {
                case Evidence.Frost: frostTime = 5f; break;
                case Evidence.Whisper: PlayAt(ProceduralAudio.Whisper(), lair, 0.9f); break;
                case Evidence.Knock: PlayAt(ProceduralAudio.Knock(), lair, 1f); break;
                case Evidence.Static: staticTime = 5f; break;
                case Evidence.Compass: compassTime = 6f; break;
                case Evidence.Prints: SpawnPrints(); break;
                case Evidence.NightVision:
                    silhouetteArmed = 9f;
                    if (nightVisionTarget < 0.5f) nextVisionSwitch = Mathf.Min(nextVisionSwitch, 0.5f);
                    break;
            }
            AddFear(1.5f);
            markTime = Beginner ? 9f : 6f;

            ui.Narrate(Loc.F("n." + clue, Direction(lair)));
            if (cluesShown.Add(clue) && Beginner) ui.Hint(Loc.T("h." + clue));
            CheckJournalHint();
        }

        float YawTo(Vector3 pos)
        {
            var to = pos - cam.transform.position;
            return Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        }

        // где логово относительно взгляда: впереди, слева, справа, за спиной, сверху, снизу
        string Direction(Vector3 pos)
        {
            var to = pos - cam.transform.position;
            float pitch = Mathf.Asin(Mathf.Clamp(to.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (pitch > 25f) return Loc.T("dir_up");
            if (pitch < -25f) return Loc.T("dir_down");
            var fwd = cam.transform.forward;
            float d = Mathf.DeltaAngle(Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            if (Mathf.Abs(d) < 45f) return Loc.T("dir_front");
            if (Mathf.Abs(d) > 135f) return Loc.T("dir_back");
            return Loc.T(d > 0f ? "dir_right" : "dir_left");
        }

        // все самостоятельные улики показаны — подсказать радио или дневник
        void CheckJournalHint()
        {
            if (!Beginner || journalHinted || newCase) return;
            int selfClues = 0;
            foreach (var c in target.Clues) if (c != Evidence.Radio) selfClues++;
            if (cluesShown.Count < selfClues) return;
            if (Catalog.Has(target, Evidence.Radio) && !radioAnswered)
            {
                if (!radioHinted) { radioHinted = true; ui.Hint(Loc.T("h.Radio")); }
                return;
            }
            journalHinted = true;
            ui.Hint(Loc.T("h.Journal"));
        }

        // холодное пятно в логове — видно в тепловизоре, если у сущности есть улика «Холод»
        void CreateColdSpot()
        {
            DestroyColdSpot();
            coldSpot = GameObject.CreatePrimitive(PrimitiveType.Quad);
            coldSpot.name = "ColdSpot";
            Destroy(coldSpot.GetComponent<Collider>());
            coldSpot.transform.position = lair;
            coldSpot.transform.localScale = Vector3.one * 1.4f;
            coldMat = new Material(Shader.Find("GhostWard/ColdSpot"));
            var mr = coldSpot.GetComponent<MeshRenderer>();
            mr.sharedMaterial = coldMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ---------- холодные следы (улика «Следы») ----------
        // Цепочка следов по полу к логову; видна только в тепловизоре, тает через 14 с.
        const float PrintsLife = 14f;
        readonly List<(GameObject go, Material mat, float appear)> prints = new();
        float printsTime;

        void SpawnPrints()
        {
            ClearPrints();
            var camPos = cam.transform.position;
            var toLair = lair - camPos;
            toLair.y = 0f;
            if (toLair.sqrMagnitude < 1e-3f) toLair = Vector3.forward;
            toLair.Normalize();
            var side = Vector3.Cross(Vector3.up, toLair);
            float floor = Floor;
            var start = camPos + toLair * 0.7f + side * Random.Range(-0.7f, 0.7f);
            start.y = floor + 0.01f;
            float yaw = Mathf.Atan2(toLair.x, toLair.z) * Mathf.Rad2Deg;
            var shader = Shader.Find("GhostWard/Footprint");
            for (int k = 0; k < 8; k++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "Print";
                Destroy(go.GetComponent<Collider>());
                go.transform.position = start + toLair * (k * 0.36f) + side * (k % 2 == 0 ? -0.09f : 0.09f);
                go.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
                go.transform.localScale = new Vector3(0.13f, 0.28f, 1f);
                var mat = new Material(shader);
                mat.SetFloat("_Mirror", k % 2);
                mat.SetFloat("_Alpha", 0f);
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                prints.Add((go, mat, k * 0.3f));
            }
            printsTime = PrintsLife;
        }

        void UpdatePrints(float dt)
        {
            if (prints.Count == 0) return;
            printsTime -= dt;
            if (printsTime <= 0f) { ClearPrints(); return; }
            float age = PrintsLife - printsTime;
            bool show = tool == Tool.Thermo;
            foreach (var (_, mat, appear) in prints)
            {
                float target = show && age >= appear ? Mathf.Clamp01(printsTime / 3f) * 0.95f : 0f;
                mat.SetFloat("_Alpha", Mathf.MoveTowards(mat.GetFloat("_Alpha"), target, dt * 3f));
            }
        }

        void ClearPrints()
        {
            foreach (var (go, mat, _) in prints)
            {
                if (go != null) Destroy(go);
                if (mat != null) Destroy(mat);
            }
            prints.Clear();
        }

        void DestroyColdSpot()
        {
            ClearPrints();
            if (coldSpot != null) Destroy(coldSpot);
            if (coldMat != null) Destroy(coldMat);
            coldSpot = null;
        }

        void UpdateColdSpot(float dt)
        {
            if (coldSpot == null) return;
            bool show = tool == Tool.Thermo && phase == Phase.Investigate && Catalog.Has(target, Evidence.Frost);
            float a = Mathf.MoveTowards(coldMat.GetFloat("_Alpha"), show ? 0.9f : 0f, dt * 2f);
            coldMat.SetFloat("_Alpha", a);
            var dir = coldSpot.transform.position - cam.transform.position;
            if (dir.sqrMagnitude > 1e-4f) coldSpot.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        // тепловизор видит живых: животное подсказывает, где логово; «одержимый» человек — холодный
        void UpdateThermalLife()
        {
            if (gyroRig == null || tool != Tool.Thermo) return;
            if (gyroRig.AnimalInView && !animalShown)
            {
                animalShown = true;
                ui.Narrate(Loc.F("n.Animal", Direction(lair)));
            }
            if (possessedNight && gyroRig.PersonInView && !possessionShown)
            {
                possessionShown = true;
                music.Play(Music.Stinger.Strings, 0.35f);
                ui.Narrate(Loc.T("n.Possessed"));
                sfx.PlayOneShot(ProceduralAudio.Whisper(), 0.6f * Settings.Sfx);
                AddFear(5f);
            }
        }

        void RadioAnswer()
        {
            if (Catalog.Has(target, Evidence.Radio))
            {
                sfx.PlayOneShot(ProceduralAudio.RadioVoice(), 1f);
                ui.Message(Loc.F("radio", Loc.T("radio_" + Random.Range(1, RadioAnswerCount + 1))), 3f);
                if (!radioAnswered && Beginner) ui.Hint(Loc.T("h.RadioYes"));
                radioAnswered = true;
                CheckJournalHint();
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
            if (namedRight) Trophies.OnRightName();
            if (namedRight)
            {
                ui.Message(Loc.T("named_right"), 3f);
                StartHunt(true);
            }
            else
            {
                AddFear(15f);
                if (phase != Phase.Investigate) return;   // от страха — конец ночи, охоты уже не будет
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

        // Приманка: «своя» сущность откликается из логова (звук, помехи, улика), но злится сильнее.
        void UseLure(Lure l)
        {
            if (phase != Phase.Investigate) { ui.Message(Loc.T("no_effect"), 1.5f); return; }
            if (!inv.Use(l)) { Empty(Loc.F("no_more", Loc.T($"l.{l}.name"))); return; }
            lureUsed = true;
            ui.SetProtection(inv);
            ui.CloseBag();
            sfx.PlayOneShot(ProceduralAudio.Click(), 0.6f);

            if (target.Lure == l)
            {
                lureMatched = true;
                music.Play(Music.Stinger.Swell, 0.6f);
                var clip = EntitySounds.Special(target.Kind) ?? EntitySounds.Call(target.Kind)
                           ?? EntitySounds.Loop(target.Kind) ?? ProceduralAudio.Whisper();
                PlayAt(clip, lair, 1f);
                glitch = 1f;
                anger += 15f;
                AddFear(4f);
                ui.Narrate(Loc.F("lure_yes", Direction(lair)));
                markTime = 8f;
                if (Beginner) ui.Hint(Loc.T("h.LureYes"));
                clueTimer = Mathf.Min(clueTimer, 1.5f);   // и сразу подаст улику
            }
            else
            {
                anger += 3f;
                ui.Narrate(Loc.T("lure_no"));
            }
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
            DestroyColdSpot();
            phase = Phase.Hunt;
            frostTime = staticTime = compassTime = 0f;
            quietTime = heardTime = 0f;
            float aggression = (1f + (night - 1) * 0.08f) * (weakened ? 0.75f : enraged ? 1.4f : 1f);
            var flat = lair - cam.transform.position;
            flat.y = 0f;
            if (flat.magnitude < 2.2f) lair = PickPosition(target, 60f, -1f);
            hunter = Entity.Create(target, lair, cam, this, aggression, 2.5f);
            hunter.Weakened = weakened;
            entities.Add(hunter);
            ui.SetPhase(Loc.T("phase_hunt"), true);
            if (!named) ui.Message(Loc.T("hunt_start"), 3.5f);
            // верно назвал (или уже изгонял) — расследование окупается: подсказка, как прогнать
            if (namedRight || Know.Defense(target.Kind)) ui.Hint(target.Hint);
            invisibleHinted = false;
            sfx.PlayOneShot(ProceduralAudio.Screech(), 0.35f);
        }

        void UpdateHunt(float dt)
        {
            HandleInput(dt);
            if (phase != Phase.Hunt) return;   // предмет мог закончить ночь (страх 100)

            if (hunter == null || !hunter.Alive)
            {
                if (banishedTime < 0f)
                {
                    banishedTime = 0f;
                    Trophies.OnBanish(!heardThisHunt);
                }
                banishedTime += dt;
                if (banishedTime > 1.6f) Dawn(true);
                return;
            }

            // шум выдаёт игрока, тишина и неподвижность прячут
            heardTime -= dt;
            bool noisy = mic.Loud || modifier == Modifier.Silence && mic.Speaking;
            if (noisy && heardTime <= 0f && thunderMask <= 0f)
            {
                heardTime = 3f;
                heardThisHunt = true;
                ui.Message(Loc.T("heard"), 1.5f);
            }
            bool still = TurnSpeed < 10f && !gestures.Pressing && (!mic.Available || !mic.Speaking);
            quietTime = still ? quietTime + dt : 0f;
            bool hidden = quietTime > 2.5f && heardTime <= 0f;
            hunter.SpeedMul = heardTime > 0f ? 2.2f : hidden ? 0.35f : 1f;
            ui.SetHidden(hidden);

            // боится железа и уже на расстоянии удара — подсказать, что пора бить (раз за охоту)
            if (!ironNowShown && hunter.Def.Bane == ItemKind.Iron && hunter.Visible && hunter.FlatDistance() <= IronRange
                && Vector3.Angle(cam.transform.forward, hunter.transform.position - cam.transform.position) < 35f)
            {
                ironNowShown = true;
                ui.Hint(Loc.T("h.IronNow"));
            }
            ui.SetCompass(cam.transform.forward, 0f, YawTo(hunter.transform.position), true);

            // фото: первый раз, когда сущность близко и в кадре
            if (photo == null && !photoTaking && hunter.Visible && hunter.FlatDistance() < 3.2f &&
                Vector3.Angle(cam.transform.forward, hunter.transform.position - cam.transform.position) < 22f)
            {
                photoTaking = true;
                StartCoroutine(PhotoShare.Capture(tex => { photo = tex; photoTaking = false; nightXp += 25; Trophies.OnPhoto(target.Kind); ui.PhotoFlash(); }));
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
                // сами по себе эпизоды ночного видения — только когда прибор камеру не занимает (ЭМП, радио);
                // иначе они перекрывали тепловизор и не давали выключить ночную камеру
                bool deviceView = tool is Tool.Thermo or Tool.Night;
                if (!deviceView) nextVisionSwitch -= dt;
                if (!deviceView && nextVisionSwitch <= 0f)
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

            // ночная камера держит ночное видение, пока выбрана; тепловизор — без примесей
            if (tool == Tool.Night && phase is Phase.Scan or Phase.Investigate or Phase.Hunt) nightVisionTarget = 1f;
            else if (tool == Tool.Thermo) nightVisionTarget = 0f;

            // переключение режимов не плавное, а через короткий «сбой»
            nightVision = Mathf.MoveTowards(nightVision, nightVisionTarget, dt * 6f);
            staticNoise.volume = Mathf.Max(glitch * 0.35f * Settings.Ambience, Mathf.MoveTowards(staticNoise.volume, 0f, dt * 0.5f));
            // эпизоды ночного режима: зелёное ночное видение, а если оно уже выбрано — ИК-камера
            bool thermal = tool == Tool.Thermo && phase is Phase.Scan or Phase.Investigate or Phase.Hunt;
            var style = thermal ? Vision.Thermal : Settings.Vision;
            var episode = Settings.Vision == Vision.NightGreen ? Vision.Cctv : Vision.NightGreen;
            // в ночь одержимости нейросеть ищет людей и в обычном виде камеры
            bool watchPeople = possessedNight && phase == Phase.Investigate;
            if (gyroRig != null)
            {
                gyroRig.SetThermalSeg(thermal || watchPeople, possessedNight);
                gyroRig.SetPossession(possession, watchPeople && (gyroRig.PersonInView || possession > 0f));
                gyroRig.SetLook(style, episode, nightVision, glitch);
            }
            // сущности «снимаются» той же камерой: тот же вид и свет комнаты
            Shader.SetGlobalFloat(GwStyle, (int)style);
            Shader.SetGlobalFloat(GwAlt, (int)episode);
            Shader.SetGlobalFloat(GwMode, nightVision);
            Shader.SetGlobalFloat(GwLight, roomLight);
            float baseGrain = style switch { Vision.Normal => 0f, Vision.Dark => 0.05f, _ => 0.06f };
            ui.DarkBase = style switch { Vision.Normal => 0.15f, Vision.Dark => 0.55f, Vision.Thermal => 0.2f, _ => 0.38f }
                          + (modifier == Modifier.NewMoon && phase is Phase.Investigate or Phase.Hunt ? 0.15f : 0f);
            ui.SetGrain(baseGrain + nightVision * 0.08f + glitch * 0.25f);
        }

        // высота телефона над полом и откуда она известна
        string PhoneHeightText()
        {
            if (cam == null) return "";
            if (arRig != null)
                return arRig.TryFloor(out float planeY)
                    ? $"Телефон: {cam.transform.position.y - planeY:0.00} м над полом\nпо полу ARCore"
                    : $"Телефон: ~{EyeHeight:0.00} м\nAR ещё не нашёл пол";
            var room = Room;
            if (room == null || !room.Available) return $"Телефон: {EyeHeight:0.00} м\nобычная, карта глубины выключена";
            return room.HeightMeasured
                ? $"Телефон: {room.PhoneHeight:0.00} м над полом\nпо потолку, потолок {room.CeilingHeight:0.0} м"
                : $"Телефон: {room.PhoneHeight:0.00} м (обычная)\nпосмотри на потолок, чтобы измерить";
        }

        string DiagnosticsText()
        {
            string rig = arRig != null ? arRig.Diagnostics() : gyroRig != null ? gyroRig.Diagnostics() : "старт…";
            string micState = mic == null ? "" : mic.Available ? $"\nмикрофон {mic.Level:0.00}" : mic.Denied ? "\nмикрофон запрещён" : "";
            string room = Room != null ? $"\nглубина: {Room.Status}" + (Room.CeilingHeight > 0f ? $", потолок {Room.CeilingHeight:0.0} м" : "") + (Room.LastSpot != "" ? $"\nместо: {Room.LastSpot}" : "") : "";
            // кто пришёл этой ночью и где его логово — чтобы проверять улики и защиту
            string who = "";
            if (phase is Phase.Investigate or Phase.Hunt)
            {
                var flat = lair - cam.transform.position;
                flat.y = 0f;
                who = $"\nсущность: {target.Name} ({target.Kind}), {Direction(lair)}, {flat.magnitude:0.0} м" +
                      $"\nулики: {string.Join(", ", System.Array.ConvertAll(target.Clues, c => c.ToString()))}; изгнать: {target.Bane}" +
                      $"\nфаза {phase}, злость {anger:0}, страх {fear:0}";
            }
            return (map.Empty ? rig : $"{rig}\nкарта {Mathf.RoundToInt(map.Coverage * 100)}%") + room + micState + who;
        }

        // Режим гироскопа: карта глубины снимается, пока идёт ночь (в осмотре — часто, потом — изредка, чтобы
        // поправлять дрейф), и следим за шагами: телефон не знает, что игрок перешёл, и всё «поехало бы» вместе с ним.
        void UpdateRoom()
        {
            // AR: пол уточняется, когда ARCore находит плоскость пола
            if (arRig != null && phase is Phase.Investigate or Phase.Hunt && arRig.TryFloor(out float planeY))
                floorY = planeY;
            var room = Room;
            if (room == null) return;
            bool active = phase is Phase.Scan or Phase.Investigate or Phase.Hunt;
            room.Capturing = active && !ui.JournalOpen;
            // после перехода карта снимается заново — чаще, пока не наберётся
            room.Interval = phase == Phase.Scan || Time.time < recaptureUntil ? 0.3f : 2.5f;

            float dt = Time.deltaTime;
            float turn = Quaternion.Angle(cam.transform.rotation, lastRoomRot) / Mathf.Max(dt, 1e-3f);
            lastRoomRot = cam.transform.rotation;
            walkCooldown -= dt;
            // жесты предметов и резкие повороты тоже трясут телефон — тогда шаги не считаем
            bool suppress = !active || ui.JournalOpen || gestures.Pressing || turn > 90f;
            int stepped = steps.Tick(dt, Time.time, suppress);
            if (stepped > 0) OnSteps(stepped);
        }

        // темнота комнаты по азимуту (карта осмотра) 0..1; -1 — не знаем
        float DarknessAt(float yaw)
        {
            int c = Mathf.Clamp(Mathf.FloorToInt((Mathf.DeltaAngle(0f, yaw) + 180f) / (360f / DarknessMap.YawCells)), 0, DarknessMap.YawCells - 1);
            return map.ColumnDarkness(c);
        }

        // сколько места по азимуту до стены или шкафа (м по горизонтали); -1 — не знаем
        float RoomLimit(float yaw)
        {
            if (arRig != null) return arRig.MaxDistance(cam.transform.position, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward);
            var room = Room;
            return room != null && room.Has ? room.MaxDistance(yaw) : -1f;
        }

        // Игрок идёт (режим гироскопа): каждый шаг — ~0.6 м туда, куда смотрит камера. Сущности, логово, холодное
        // пятно остаются на своих местах в комнате: подошёл — они ближе и крупнее, отошёл — дальше.
        // Карта глубины снята с прежнего места — отошёл больше чем на 0.6 м, снимаем её заново.
        // Во время осмотра комнаты ходить не стоит (карта темноты — по направлениям от одной точки) — подсказка.
        void OnSteps(int count)
        {
            if (gyroRig == null) return;
            if (phase == Phase.Scan)
            {
                if (!walkHinted) { walkHinted = true; ui.Hint(Loc.T("h.StandStill")); }
                return;
            }
            var fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-3f) return;   // телефон смотрит в пол или в потолок — куда шагнул, не понять
            float stride = 0.6f * Mathf.Clamp(PhoneHeight / EyeHeight, 0.8f, 1.15f);
            gyroRig.Walk(fwd.normalized * stride * count);

            var room = Room;
            if (room != null && room.Has)
            {
                var moved = cam.transform.position - room.Origin;
                moved.y = 0f;
                if (moved.magnitude + stride * count > 0.6f)
                {
                    room.Clear(keepHeight: true);   // держит телефон так же — высоту не теряем
                    recaptureUntil = Time.time + 6f;
                }
            }
        }

        void UpdateScan()
        {
            drone.volume = Mathf.MoveTowards(drone.volume, 0.25f * Settings.Ambience, Time.deltaTime * 0.3f);
            scanTimer -= Time.deltaTime;
            if (scanTimer > 0f) return;
            scanTimer = 0.15f;

            bool sampled = arRig != null ? arRig.SampleLuma(samplePoints, sampleLuma) : gyroRig.SampleLuma(samplePoints, sampleLuma);
            if (!sampled)
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
            roomLight = Mathf.Lerp(roomLight, mean, 0.2f);

            for (int i = 0; i < samplePoints.Length; i++)
            {
                if (sampleLuma[i] < 0f) continue;
                var dir = cam.ViewportPointToRay(samplePoints[i]).direction;
                map.Add(dir, sampleLuma[i] / mean);
            }

            ui.SetScan(map.Coverage / ScanCoverage);
            if (map.Coverage < ScanCoverage) return;
            // комната обведена; карте глубины ещё нужен потолок — по нему узнаём высоту телефона и рост фигур.
            // Ждём не дольше 20 с, и только если карта глубины вообще работает
            scanWait += 0.15f;
            var room = Room;
            bool needCeiling = room != null && room.Available && room.Has && !room.HeightMeasured && scanWait < 20f;
            if (needCeiling)
            {
                if (!ceilingAsked) { ceilingAsked = true; ui.Message(Loc.T("scan_ceiling"), 4f); }
                return;
            }
            BeginInvestigation();
        }

        // Место для сущности: сектор карты по привычке её логова, не ближе minAngle к взгляду игрока.
        // Расстояние: если место ниже горизонта — это пол, считаем по наклону и высоте глаз;
        // иначе это проём или дальняя стена — 3–4.5 м. maxDist > 0 ограничивает дальность.
        public Vector3 PickPosition(EntityDef def, float minAngle, float maxDist) => PickPosition(def, minAngle, maxDist, 180f);

        // maxAngle — место не дальше этого угла от взгляда
        public Vector3 PickPosition(EntityDef def, float minAngle, float maxDist, float maxAngle)
        {
            var camPos = cam.transform.position;
            var fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-3f) fwd = Vector3.forward;
            fwd.Normalize();

            // Режим гироскопа с картой комнаты: осмысленное место по повадке — проём, угол, за мебелью (RoomMap.TryPickSpot).
            // Иначе (AR, карта не снята): сектор карты темноты, место без стены или шкафа вплотную;
            // упёрлись — ищем другое (до шести попыток), иначе ставим как получилось
            var room = Room;
            float yaw = 0f, pitch = 0f, dist = 0f;
            float viewYawDeg = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            RoomMap.Spot spot = default;
            bool spotted = arRig == null && room != null
                           && room.TryPickSpot(viewYawDeg, minAngle, maxDist, def.Lair, DarknessAt, out spot, maxAngle);
            if (spotted)
            {
                yaw = spot.Yaw;
                dist = spot.Dist;
                pitch = def.Lair == Lair.Ceiling ? 30f : 0f;
            }
            for (int attempt = 0; attempt < 6 && !spotted; attempt++)
            {
                if (!map.TryPick(fwd, minAngle, def.Lair, out yaw, out pitch))
                {
                    float viewYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                    yaw = viewYaw + Random.Range(minAngle, 180f) * (Random.value < 0.5f ? -1f : 1f);
                    pitch = def.Lair switch { Lair.Floor => -25f, Lair.Ceiling => 30f, _ => 0f };
                }

                if (def.Lair == Lair.Near) dist = Random.Range(1.4f, 2.2f);
                else if (def.Lair == Lair.Far) dist = Random.Range(3.8f, 4.8f);
                else if (pitch < -8f) dist = Mathf.Clamp(PhoneHeight / Mathf.Tan(-pitch * Mathf.Deg2Rad), 2.2f, 4.5f);
                else dist = Random.Range(3.2f, 4.5f);
                if (maxDist > 0f) dist = Mathf.Min(dist, maxDist);

                float limit = RoomLimit(yaw);
                if (limit < 0f) break;                              // туда ещё не смотрели — как без карты
                if (limit < 1.3f && attempt < 5) continue;          // стена или шкаф в шаге — тесно
                // ползающим по полу — на видимый пол, чтобы фигура не «висела» над столом
                if (def.Lair == Lair.Floor && room != null)
                {
                    float reach = room.FloorReach(yaw);
                    if (reach > 1.4f) dist = Mathf.Min(dist, reach);
                }
                dist = Mathf.Clamp(dist, 1.1f, Mathf.Max(limit, 1.1f));
                break;
            }

            var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var pos = camPos + dir * dist;
            pos.y = StanceY(def, def.Stance == Stance.Float
                ? camPos.y + Mathf.Clamp(Mathf.Tan(pitch * Mathf.Deg2Rad) * dist, -0.4f, 0.8f)
                : camPos.y);
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
            music.Play(Music.Stinger.Dawn, 0.8f);

            // опыт за ночь: дожил, изгнал, назвал верно, без ошибок, находки
            int xp = 100 + 20 * night + nightXp
                     + (banished ? 150 : 0)
                     + (namedRight ? 100 : 0)
                     + (banished && namedRight && !wrongItemUsed ? 50 : 0);
            xp = Mathf.RoundToInt(xp * Modifiers.XpFactor(modifier));
            string extra = "";
            if (dailyMode)
            {
                if (Daily.Complete())
                {
                    xp *= 2;
                    Trophies.OnDaily(Daily.Streak);
                    extra = Loc.F("daily_done", Daily.Streak);
                }
                else extra = Loc.T("daily_again");
            }
            int rankBefore = Progress.Rank;
            if (banished && namedRight && !wrongItemUsed) Trophies.OnCleanNight();
            Trophies.OnNight(night);
            int unlockedBefore = Know.UnlockedCount;
            bool rankUp = Progress.AddXp(xp) || Progress.Rank > rankBefore;
            bool newPage = Progress.AddSurvivedNight();
            extra = JoinLines(extra, JournalLine(Know.Record(target.Kind, NightClues(), lureMatched, true, banished)));
            if (banished && newCase) extra = JoinLines(extra, Loc.F("defense_learned", Catalog.Get(target.Bane).Name));
            if (Know.UnlockedCount > unlockedBefore) extra = JoinLines(extra, Loc.T("new_unlocked"));
            if (prologue)
            {
                // пролог пройден: дальше обычные ночи; объясняем, куда расти
                prologue = false;
                Settings.PrologueDone = true;
                Settings.TutorialSeen = true;
                ui.SetInstruction("", 0, 0);
                extra = JoinLines(Loc.T("pro_dawn"), extra);
            }
            ui.ShowNightProgress(xp, rankUp, newPage, extra);
            ui.SetNextNightVisible(!dailyMode);
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
                string journal = JournalLine(Know.Record(target.Kind, NightClues(), lureMatched, false, false));
                ui.ShowNightProgress(xp, Progress.AddXp(xp), false, journal);
            }
        }

        // улики, которые игрок этой ночью видел или услышал
        List<Evidence> NightClues()
        {
            var list = new List<Evidence>(cluesShown);
            if (radioAnswered) list.Add(Evidence.Radio);
            return list;
        }

        string JournalLine(List<string> learned) =>
            learned.Count == 0 ? "" : Loc.F("journal_updated", target.Name, string.Join(", ", learned));

        static string JoinLines(string a, string b) =>
            string.IsNullOrEmpty(a) ? b : string.IsNullOrEmpty(b) ? a : a + "\n" + b;

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
                if (phase != Phase.Investigate || stashesThisNight >= (modifier == Modifier.NewMoon ? 3 : 2)) return;
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
                Trophies.OnStash();
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
            pos.y = Floor + Random.Range(0.3f, 1.1f);
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
            thunderMask -= dt;
            if (modifier == Modifier.Storm)
            {
                thunderTimer -= dt;
                if (thunderTimer <= 0f)
                {
                    thunderTimer = Random.Range(15f, 28f);
                    thunderMask = 3f;
                    ui.Flash(new Color(0.9f, 0.92f, 1f, 0.85f));
                    ui.Shake(0.4f);
                    var dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                    PlayAt(ProceduralAudio.Crash(), cam.transform.position + dir * 6f, 0.9f);
                    PlayAt(ProceduralAudio.Drone(), cam.transform.position + dir * 6f, 0.6f);
                }
            }
            if (modifier == Modifier.Silence || prologue) return;

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

        // ---------- фирменный испуг сущности ----------
        // Раз в 35–60 с расследования сущность пугает по-своему: тихо, без нападения.
        enum ScareStyle { Glimpse, Behind, Overhead, Throw, Reflection, Blackout, Approach, Voice, Handprint }

        float signatureTimer;

        static ScareStyle StyleOf(EntityKind k) => k switch
        {
            EntityKind.Rusalka or EntityKind.Vodyanoy or EntityKind.Pontianak or EntityKind.Llorona => ScareStyle.Handprint,
            EntityKind.Ghost or EntityKind.Shade or EntityKind.Tunda => ScareStyle.Glimpse,
            EntityKind.Albasty or EntityKind.Zheztyrnak or EntityKind.Kitsune or EntityKind.Shurale or EntityKind.Aswang
                or EntityKind.Changeling => ScareStyle.Behind,
            EntityKind.Kikimora or EntityKind.Lechuza or EntityKind.Jinn or EntityKind.Ubyr or EntityKind.Penanggalan => ScareStyle.Overhead,
            EntityKind.Poltergeist or EntityKind.Domovoi or EntityKind.BellWitch => ScareStyle.Throw,
            EntityKind.Double or EntityKind.BloodyMary or EntityKind.Kuchisake => ScareStyle.Reflection,
            EntityKind.Mara or EntityKind.Cucuy or EntityKind.Karakoncolos or EntityKind.Strigoi or EntityKind.BlackDog => ScareStyle.Blackout,
            EntityKind.Upyr or EntityKind.Jiangshi or EntityKind.Abaasy or EntityKind.MulaSemCabeca or EntityKind.Chupacabra
                or EntityKind.CorpoSeco or EntityKind.Ghoul => ScareStyle.Approach,
            _ => ScareStyle.Voice
        };

        void UpdateSignature(float dt)
        {
            if (prologue || modifier == Modifier.Silence || phase != Phase.Investigate) return;
            signatureTimer -= dt;
            if (signatureTimer > 0f) return;
            signatureTimer = Random.Range(35f, 60f);
            SignatureScare();
        }

        void SignatureScare()
        {
            var k = target.Kind;
            var call = EntitySounds.Special(k) ?? EntitySounds.Call(k) ?? EntitySounds.Loop(k) ?? ProceduralAudio.Whisper();
            var camPos = cam.transform.position;
            var fwd = cam.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 1e-3f ? Vector3.forward : fwd.normalized;
            var side = Vector3.Cross(Vector3.up, fwd);
            float floor = Floor;

            switch (StyleOf(k))
            {
                case ScareStyle.Glimpse:
                {
                    // на краю зрения, на миг
                    // у самого края кадра, но в кадре
                    var pos = EdgeOfView(HalfFovX * Random.Range(0.7f, 0.95f) * (Random.value < 0.5f ? -1f : 1f), 3f);
                    Apparition.Show(target, pos, cam.transform, 0.5f);
                    PlayAt(call, pos, 0.6f);
                    music.Play(Music.Stinger.Boom, 0.3f);
                    AddFear(4f);
                    break;
                }
                case ScareStyle.Behind:
                    PlayAt(call, camPos - fwd * 0.6f, 0.85f);
                    PlayAt(ProceduralAudio.Breath(), camPos - fwd * 0.3f, 0.5f);
                    ui.FrostBurst(2.5f);   // дыхание на затылке — иней по краям кадра
                    ui.Shake(0.2f);
                    AddFear(3f);
                    break;
                case ScareStyle.Handprint:
                    StartCoroutine(HandprintScare(call));
                    AddFear(4f);
                    break;
                case ScareStyle.Overhead:
                    PlayAt(call, camPos + Vector3.up * 1.1f - fwd * 0.3f, 0.85f);
                    ui.Shake(0.3f);
                    AddFear(3f);
                    break;
                case ScareStyle.Throw:
                    PlayAt(ProceduralAudio.Crash(), camPos + side * (Random.value < 0.5f ? 2f : -2f), 1f);
                    PlayAt(call, camPos + side * 1.5f, 0.7f);
                    ui.Shake(0.5f);
                    AddFear(4f);
                    break;
                case ScareStyle.Reflection:
                    // лицо на долю секунды проступает на экране — как отражение в стекле
                    ui.Jumpscare(target, Random.value * 10f, 0.3f);
                    PlayAt(call, camPos + fwd * 0.5f, 0.6f);
                    AddFear(5f);
                    break;
                case ScareStyle.Blackout:
                    StartCoroutine(BlackoutScare(call));
                    AddFear(4f);
                    break;
                case ScareStyle.Approach:
                    StartCoroutine(ApproachScare(call));
                    AddFear(4f);
                    break;
                default:
                    PlayAt(call, lair, 1f);
                    AddFear(2f);
                    break;
            }
            ui.Narrate(Loc.F("s." + k, Direction(lair)));
        }

        // ---------- одержимость ----------
        // В ночь одержимости, когда в кадре живой человек, сущность на 3–4 секунды «надевает» его:
        // вены по телу, чёрные глаза с красными точками, растянутый рот. Не больше трёх раз за ночь.
        float possession, possessionTimer, possessionCooldown;
        int possessionCount;
        const float PossessionTime = 3.5f;

        void UpdatePossession(float dt)
        {
            possessionCooldown -= dt;
            if (possessionTimer > 0f)
            {
                possessionTimer -= dt;
                float elapsed = PossessionTime - possessionTimer;
                possession = Mathf.Min(Mathf.Clamp01(elapsed / 0.4f), Mathf.Clamp01(possessionTimer / 0.7f));
                // лёгкое подёргивание силы — как сбой
                if (Random.value < dt * 4f) possession *= 0.5f;
                return;
            }
            possession = 0f;
            if (!possessedNight || gyroRig == null || possessionCount >= 3 || possessionCooldown > 0f) return;
            if (!gyroRig.PersonInView) return;
            possessionTimer = PossessionTime;
            possessionCooldown = Random.Range(25f, 40f);
            possessionCount++;
            sfx.PlayOneShot(ProceduralAudio.Whisper(), 0.7f * Settings.Sfx);
            music.Play(Music.Stinger.Strings, 0.45f);
            glitch = Mathf.Max(glitch, 0.6f);
            ui.Narrate(Loc.T("n.Possession"));
            AddFear(6f);
        }

        // ---------- сущность показывается во время расследования ----------
        // Как в хоррорах: проходит на краю зрения, мерцает и пропадает; или стоит вдалеке и смотрит —
        // отвёл взгляд, вернул — её нет… и она уже ближе. Не нападает: это страх, а не бой.
        Apparition wanderer;

        void UpdateWander(float dt)
        {
            if (prologue || target.Invisible || phase != Phase.Investigate) return;
            wanderTimer -= dt;
            if (wanderTimer > 0f) return;
            wanderTimer = Random.Range(28f, 50f) / (1f + anger / 150f);
            if (Random.value < 0.35f) StartCoroutine(StareScare());
            else StartCoroutine(PassScare());
        }

        // точка на краю зрения: angle — от направления взгляда, dist — от игрока
        Vector3 EdgeOfView(float angle, float dist)
        {
            var fwd = cam.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 1e-3f ? Vector3.forward : fwd.normalized;
            var dir = Quaternion.Euler(0f, angle, 0f) * fwd;
            float limit = RoomLimit(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
            if (limit > 0f) dist = Mathf.Clamp(dist, 1f, Mathf.Max(limit, 1f));   // не за стеной
            var pos = cam.transform.position + dir * dist;
            pos.y = StanceY(target, cam.transform.position.y);
            return pos;
        }

        // проход: медленно плывёт поперёк кадра, мерцает; иногда пропадает и тут же возникает с другой стороны
        IEnumerator PassScare()
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            // входит в кадр с края и плывёт к центру
            var pos = EdgeOfView(side * HalfFovX * Random.Range(0.9f, 1.15f), Random.Range(3.2f, 4.5f));
            var across = Vector3.Cross(Vector3.up, (pos - cam.transform.position).normalized) * -side;
            float seconds = Random.Range(3.5f, 5f);
            wanderer = Apparition.Show(target, pos, cam.transform, seconds, across * Random.Range(0.25f, 0.4f), true, 0.7f);
            var loop = EntitySounds.Loop(target.Kind) ?? EntitySounds.Call(target.Kind);
            if (loop != null) PlayAt(loop, pos, 0.35f);
            AddFear(3f);
            if (Random.value < 0.4f)
            {
                yield return new WaitForSeconds(seconds * 0.6f);
                if (wanderer != null) wanderer.Kill();
                if (phase != Phase.Investigate) yield break;
                var other = EdgeOfView(-side * HalfFovX * Random.Range(0.4f, 0.7f), Random.Range(2.6f, 3.4f));
                Apparition.Show(target, other, cam.transform, 0.7f, default, false, 0.8f);
                music.Play(Music.Stinger.Boom, 0.35f);
                AddFear(4f);
            }
        }

        // наблюдение: стоит вдалеке и смотрит. Отвернулся и посмотрел снова — она ближе, на миг
        // Стоит в комнате — в проёме, в углу, за мебелью (место по карте глубины, прямо в кадре) — и смотрит.
        // Отвёл взгляд — её там уже нет. Повернулся куда угодно — она стоит рядом, в 1.3–1.8 м,
        // чуть впереди по ходу поворота: входит в кадр вместе с поворотом. Стоит, пока на неё не посмотрят, и пропадает.
        IEnumerator StareScare()
        {
            var pos = PickPosition(target, 0f, -1f, HalfFovX * 0.7f);
            var dir = (pos - cam.transform.position).normalized;
            wanderer = Apparition.Show(target, pos, cam.transform, 12f, default, false, 0.6f);
            float wait = 12f;
            while (wait > 0f && phase == Phase.Investigate && Vector3.Angle(cam.transform.forward, dir) < 40f)
            {
                wait -= Time.deltaTime;
                yield return null;
            }
            if (wanderer != null) wanderer.Kill();
            if (wait <= 0f || phase != Phase.Investigate) yield break;

            // отвернулся — её больше нет на том месте. Ждём, когда игрок снова начнёт поворачиваться
            yield return new WaitForSeconds(Random.Range(0.8f, 2f));
            wait = 8f;
            while (wait > 0f && phase == Phase.Investigate && TurnSpeed < 35f)
            {
                wait -= Time.deltaTime;
                yield return null;
            }
            if (wait <= 0f || phase != Phase.Investigate) yield break;

            // рядом, сразу за краем кадра по ходу поворота
            var fwd = cam.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 1e-3f ? Vector3.forward : fwd.normalized;
            float side = TurnSign >= 0f ? 1f : -1f;
            var ahead = Quaternion.Euler(0f, side * (HalfFovX + 10f), 0f) * fwd;
            float yaw = Mathf.Atan2(ahead.x, ahead.z) * Mathf.Rad2Deg;
            float limit = RoomLimit(yaw);
            float dist = Random.Range(1.3f, 1.8f);
            if (limit > 0f) dist = Mathf.Clamp(dist, 0.9f, Mathf.Max(limit, 0.9f));   // не в стене
            var near = cam.transform.position + ahead * dist;
            near.y = StanceY(target, cam.transform.position.y);
            var close = Apparition.Show(target, near, cam.transform, 4f, default, false, 0.95f);

            // пугаем в тот миг, когда она попала в кадр
            wait = 4f;
            while (wait > 0f && close != null && !IsInView(near))
            {
                wait -= Time.deltaTime;
                yield return null;
            }
            if (close == null || wait <= 0f) { if (close != null) close.Kill(); yield break; }
            music.Play(Music.Stinger.Boom, 0.7f);
            var call = EntitySounds.Call(target.Kind);
            if (call != null) PlayAt(call, near, 0.8f);
            ui.Shake(0.45f);
            AddFear(7f);
            yield return new WaitForSeconds(0.9f);
            if (close != null) close.Kill();
            ui.Flash(new Color(0f, 0f, 0f, 0.6f));
        }

        // свет гаснет; когда загорается — она стоит ближе, и снова темно
        IEnumerator BlackoutScare(AudioClip call)
        {
            sfx.PlayOneShot(ProceduralAudio.Click(), 0.7f);
            ui.Flash(new Color(0f, 0f, 0f, 0.97f));
            yield return new WaitForSeconds(0.45f);
            if (phase != Phase.Investigate) yield break;
            var fwd = cam.transform.forward;
            fwd.y = 0f;
            var pos = cam.transform.position + fwd.normalized * 1.8f;
            pos.y = StanceY(target, cam.transform.position.y);
            Apparition.Show(target, pos, cam.transform, 0.35f);
            PlayAt(call, pos, 0.7f);
            yield return new WaitForSeconds(0.3f);
            sfx.PlayOneShot(ProceduralAudio.Click(), 0.5f);
            ui.Flash(new Color(0f, 0f, 0f, 0.95f));
        }

        // мокрые ладони изнутри «стекла» камеры: удар, ещё удар
        IEnumerator HandprintScare(AudioClip call)
        {
            sfx.PlayOneShot(ProceduralAudio.Knock(), 0.5f);
            ui.Handprint();
            ui.Shake(0.25f);
            yield return new WaitForSeconds(0.6f);
            if (phase != Phase.Investigate) yield break;
            ui.Handprint();
            PlayAt(call, cam.transform.position + cam.transform.forward * 0.4f, 0.6f);
        }

        // шаги из темноты: всё ближе — и тишина
        IEnumerator ApproachScare(AudioClip call)
        {
            var back = -cam.transform.forward;
            back.y = 0f;
            back.Normalize();
            for (int i = 0; i < 3; i++)
            {
                if (phase != Phase.Investigate) yield break;
                PlayAt(call, cam.transform.position + back * (4.5f - i * 1.4f), 0.6f + i * 0.15f);
                yield return new WaitForSeconds(0.8f);
            }
            ui.Shake(0.15f);
        }

        IEnumerator SecondFlicker()
        {
            yield return new WaitForSeconds(0.35f);
            sfx.PlayOneShot(ProceduralAudio.Click(), 0.5f);
            ui.Flash(new Color(0f, 0f, 0f, 0.9f));
        }

        void ClearEntities()
        {
            DestroyColdSpot();
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
            if (!nameMusicPlayed)
            {
                nameMusicPlayed = true;
                music.Play(Music.Stinger.Boom, 0.7f);
            }
            if (Catalog.Seen(e.Def.Kind)) return;
            Catalog.MarkSeen(e.Def.Kind);
            Trophies.OnSeen();
            ui.Message(Know.IsStarter(e.Def.Kind) || Know.Defense(e.Def.Kind)
                ? e.Def.Hint + "\n\n" + Loc.T("new_entry")
                : Loc.F("remember_tale", e.Def.Name), 6f);
        }

        // железо бьёт вплотную; в режиме гироскопа к сущности не подойти — дотягиваемся дальше
        float IronRange => arRig != null ? 1.8f : 2.6f;
        bool ironNowShown;

        // ночной режим или тепловизор: видно то, что не видно глазу (чёрный пёс)
        public bool SeesHidden => nightVision > 0.5f || tool == Tool.Thermo;

        // вой банши: страх и помехи, сильнее, если она в кадре
        public void OnWail(Entity e)
        {
            AddFear(IsInView(e.transform.position) ? 9f : 5f);
            glitch = 1f;
            ui.Shake(0.35f);
        }

        bool invisibleHinted;

        public void OnThrow(Entity e)
        {
            // невидимку не увидеть — объяснить один раз за охоту
            if (e.Def.Invisible && !invisibleHinted)
            {
                invisibleHinted = true;
                ui.Hint(Loc.T("h.Invisible"));
            }
            AddFear(7f);
            ui.Shake(0.6f);
        }

        public void OnAttack(Entity e)
        {
            music.Play(Music.Stinger.Strings, e.Def.Invisible ? 0.4f : 0.8f);
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
            AddFear(e.Def.Invisible ? 15f : 22f);
            ui.Jumpscare(e.Def, e.Seed, e.Def.Invisible ? 0.5f : 1f);
            sfx.PlayOneShot(EntitySounds.Attack(e.Def.Kind), 0.8f);
#if UNITY_ANDROID || UNITY_IOS
            if (Settings.Vibration) Handheld.Vibrate();
#endif
        }

        void AddFear(float v)
        {
            fear = Mathf.Clamp(fear + v, 0f, prologue ? 85f : 100f);
            if (fear < 100f || phase is not (Phase.Investigate or Phase.Hunt)) return;
            if (!faintedThisNight) Faint();
            else GameOver();
        }

        // Первый раз за ночь страх не убивает: в глазах темнеет, игрок приходит в себя, а оно всё ещё здесь.
        void Faint()
        {
            faintedThisNight = true;
            fear = 60f;
            ui.Blackout(2.2f);
            heart.volume = 0.9f * Settings.Ambience;
            heart.Play();
            foreach (var e in entities) if (e != null && e.Alive) e.Repel();
            StartCoroutine(WakeUp());
        }

        IEnumerator WakeUp()
        {
            yield return new WaitForSeconds(2.2f);
            if (phase is not (Phase.Investigate or Phase.Hunt)) yield break;
            ui.Message(Loc.T("faint"), 4f);
            ui.FrostBurst(3f);
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
                if (Entity.ArchOf(e.Def.Kind) == EntityKind.Mara && IsInView(e.transform.position)) { AddFear(dt * 9f); continue; }
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
            var g = gestures.Tick(dt, Time.time, ui.IsOverButton);
            UpdateLantern(gestures.Holding, dt);
            if (g == ItemKind.Bell) RingBell();
            else if (g.HasValue) Use(g.Value);
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
            float range = item switch { ItemKind.Iron => IronRange, ItemKind.Wormwood => 4f, ItemKind.Mirror => 5f, _ => 4.5f };

            if (item == ItemKind.Iron)
            {
                if (inv.Iron <= 0) { Empty(Loc.F("no_more", def.Name)); return; }
                if (Target(angle, range) == null)
                {
                    var far = Target(30f, 6f);
                    // те, кто ходит только без взгляда, сами не подойдут, пока на них смотришь — так и скажем
                    string key = far == null ? "nobody" : Entity.ArchOf(far.Def.Kind) == EntityKind.Albasty ? "too_far_look" : "too_far";
                    ui.Message(Loc.T(key), 3f);
                    return;
                }
            }
            if (!prologue && !inv.Use(item)) { Empty(Loc.F("no_more", def.Name)); return; }   // в обучении не тратится

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

            // мару звон будит всегда; остальных, кто боится колокольчика, — если повернуться в их сторону
            bool hit = false;
            foreach (var e in entities)
            {
                if (!e.Alive || e.Current != Entity.State.Manifest) continue;
                if (e.Def.Kind == EntityKind.Mara) { e.Banish(); hit = true; continue; }
                if (e.Def.Bane != ItemKind.Bell) continue;
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
                inv.Battery = Mathf.Max(0f, inv.Battery - 9f * (Progress.SlowLantern ? 0.75f : 1f) * (modifier == Modifier.NewMoon ? 2f : 1f) * dt);
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
