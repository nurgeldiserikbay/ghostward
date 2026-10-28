using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GhostWard
{
    public class GameUI : MonoBehaviour
    {
        static readonly Color Ink = new(0.03f, 0.025f, 0.035f, 0.85f);
        static readonly Color Bone = new(0.86f, 0.83f, 0.76f);
        static readonly Color Dim = new(0.86f, 0.83f, 0.76f, 0.4f);
        static readonly Color Blood = new(0.55f, 0.06f, 0.08f);
        static readonly Color Frost = new(0.75f, 0.88f, 1f);

        public event Action StartPressed;      // «Ещё раз» после поражения
        public event Action PlayPressed;       // «Играть» в главном меню
        public event Action MenuPressed;
        public event Action TutorialFinished;
        public event Action<Lang> LanguageChosen;
        public event Action<Tool> ToolSelected;
        public event Action<Protect> ProtectUsed;
        public event Action<Lure> LureUsed;
        public event Action SecondChancePressed;
        public event Action BonusSupplyPressed;
        public event Action DailyPressed;
        public event Action NextNightPressed;
        public event Action<bool> JournalToggled;
        public event Action<EntityKind> NamePressed;
        public event Action SharePressed;

        // можно ли сейчас назвать сущность (идёт расследование и имя ещё не названо)
        // можно ли сейчас определить сущность (идёт расследование, ещё не распознана)
        bool canName;
        public bool CanName
        {
            get => canName;
            set { canName = value; UpdateIdentify(); }
        }
        public event Action<Evidence[]> IdentifyPressed;
        GameObject identifyButton;
        Text listEmpty;

        // базовое затемнение экрана ночью — зависит от вида камеры
        public float DarkBase { get; set; } = 0.55f;

        public bool JournalOpen => journal.activeSelf;

        RectTransform root, shakeRoot, journalButton;
        Font font;
        GameObject hud, menu, dawn, over, loading, journal, scan, strip;
        GameObject journalList, journalEntry, journalItems, journalClues, journalHowTo, nameButton, shareButton, micRoot;
        Text phaseText, recText, hiddenText, suspectsText, dawnSupply;
        RawImage dawnPhoto;
        UILine needle, compassMark;
        bool huntEdges;
        readonly List<(RectTransform rt, int yaw)> compassTicks = new();
        readonly List<(Text label, int yaw)> compassLabels = new();
        readonly Image[] micBars = new Image[5];
        readonly bool[] marks = new bool[System.Enum.GetValues(typeof(Evidence)).Length];
        readonly List<(UILine box, UILine check)> clueRows = new();
        EntityKind openedEntry;
        float recBlink;
        GameObject language, settings, tutorial;
        Text tutTitle, tutBody, tutNext, camTop, camBottom;
        RectTransform tutIcon;
        readonly List<Image> tutDots = new();
        int tutPage;
        bool tutFromPlay;
        Material menuMat;
        Vision overlayStyle;
        readonly List<(Image img, Func<bool> selected)> settingButtons = new();
        RectTransform toolBar, bagButton, bagPanel;
        Text readout;
        RawImage candleGlow;
        GameObject journalTools, secondChance, bonusButton, settingsGeneral, settingsSound;
        readonly List<(Text text, Func<float> value)> volumeTexts = new();
        readonly List<(Image img, bool general)> settingTabs = new();
        GameObject ranks, story, readButton;
        GameObject storyReturn;
        Text rankName, rankXp, dawnXp, overXp, storyPageLabel, storyBody;
        RectTransform rankFill;
        int storyPage;
        GameObject trophies, nextNightButton;
        Text trophyText, narrateText, hintText, instructionText, instructionStep;
        GameObject instruction;
        float trophyTimer, narrateTimer, hintTimer;
        string modifierName = "";
        readonly List<(Tool tool, Image bg)> toolButtons = new();
        readonly List<(Protect kind, Text label)> bagRows = new();
        readonly List<(Lure kind, Text label)> lureRows = new();
        Text nightText, clockText, messageText, supplyText, diagText, dawnText, overText, scanText, phoneText;
        Text entryName, entryOrigin, entryText, entryItem, entryCount;
        RectTransform entryItemIcon, entryGestureIcon;
        RawImage entryPicture, grain, jumpscare;
        Material entryMat, jumpMat;
        float jumpTimer;
        // карточки списка сущностей: картинка — готовое превью (Thumb), а не живой шейдер — список не тормозит
        readonly List<(EntityKind kind, RawImage pic, Text name)> cards = new();
        readonly Dictionary<(EntityKind, bool), RenderTexture> thumbs = new();
        Coroutine thumbFill;

        // записи ночи: всё, что игра сказала, можно перечитать
        readonly List<string> nightLog = new();
        GameObject journalLog;
        Text logText;
        RectTransform logContent, logButton;
        ScrollRect logScroll;

        // превью вида камеры в настройках
        GameObject visionPreview;
        RawImage previewImage;
        Material previewMat;
        Text previewTitle;
        WebCamTexture ownPreviewCam;
        public Func<WebCamTexture> PreviewCamera;   // камера игры, если уже запущена
        Text journalCount;
        ScrollRect journalScroll;
        readonly List<(ItemKind kind, Text count)> itemRows = new();
        readonly List<(ItemKind kind, Image bg, Text label)> practiceButtons = new();

        // тренировка жестов в дневнике
        GameObject journalPractice;
        Text practiceTitle, practiceText, practiceStatus, learnedCount;
        RectTransform practiceGesture, practiceBack;
        RawImage practiceTarget;
        Material practiceMat;
        ItemKind practiceItem;
        float practiceDone = -1f, practiceHold;
        readonly Gestures practiceGestures = new();
        AudioSource uiAudio;
        static readonly Color Warm = new(1f, 0.75f, 0.45f);
        Inventory shownInventory;
        RectTransform fearFill, scanFill, stripMarker;
        RawImage darkness, lanternDark, lanternGlow, flash;
        readonly RawImage[] edges = new RawImage[4]; // лево, право, верх, низ
        readonly float[] edgeTarget = new float[4];
        readonly Image[] stripCells = new Image[DarknessMap.YawCells];
        readonly List<(RectTransform rt, Vector2 vel, float life)> grains = new();
        float messageTimer, supplyTimer, shake, flashAlpha, fear;
        Color flashColor;
        bool lanternOn;

        public static GameUI Create()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var go = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var ui = go.AddComponent<GameUI>();
            ui.root = go.GetComponent<RectTransform>();
            ui.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ui.Build();
            return ui;
        }

        void Build()
        {
            shakeRoot = Rect(root, "Shake", Vector2.zero, Vector2.one);

            // атмосфера: постоянная тёмная виньетка, сгущается со страхом
            var vignette = Textures.Vignette();
            darkness = Overlay(shakeRoot, "Darkness", vignette, new Color(0f, 0f, 0f, 0.6f));
            lanternDark = Overlay(shakeRoot, "LanternDark", vignette, new Color(0f, 0f, 0f, 0f));
            lanternGlow = Overlay(shakeRoot, "LanternGlow", Textures.Glow(), new Color(1f, 0.8f, 0.5f, 0f));

            // иней по краю экрана — с той стороны, где сущность
            for (int i = 0; i < 4; i++)
            {
                var tex = i switch
                {
                    2 => Textures.EdgeGradient(vertical: true, flip: true),
                    3 => Textures.EdgeGradient(vertical: true),
                    _ => Textures.EdgeGradient()
                };
                var img = Overlay(shakeRoot, "Edge" + i, tex, new Color(Frost.r, Frost.g, Frost.b, 0f));
                var rt = img.rectTransform;
                switch (i)
                {
                    case 0: rt.anchorMax = new Vector2(0.22f, 1f); break;
                    case 1: rt.anchorMin = new Vector2(0.78f, 0f); rt.localScale = new Vector3(-1, 1, 1); break;
                    case 2: rt.anchorMin = new Vector2(0f, 0.85f); break;
                    case 3: rt.anchorMax = new Vector2(1f, 0.15f); break;
                }
                edges[i] = img;
            }

            BuildHud();
            BuildScan();
            BuildStrip();
            BuildMenu();
            BuildDawn();
            BuildOver();
            BuildJournal();
            BuildLanguage();
            BuildSettings();
            BuildTutorial();
            BuildRanks();
            BuildStory();

            loading = Panel(root, "Loading");
            Label(loading.transform, Loc.T("loading"), 54, new Vector2(0.5f, 0.5f), Vector2.zero, Bone);

            messageText = Label(root, "", 44, new Vector2(0.5f, 0.6f), Vector2.zero, Bone);
            messageText.rectTransform.sizeDelta = new Vector2(940, 300);
            messageText.fontStyle = FontStyle.Italic;
            Outline(messageText);

            supplyText = Label(root, "", 32, new Vector2(0.5f, 0f), new Vector2(0, 400), Dim);
            supplyText.rectTransform.sizeDelta = new Vector2(800, 60);

            jumpscare = Rect(root, "Jumpscare", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<RawImage>();
            jumpscare.rectTransform.sizeDelta = new Vector2(1100, 1380);
            jumpscare.raycastTarget = false;
            jumpscare.gameObject.SetActive(false);

            // инструкция пролога: шаг обучения, пока не выполнен
            var ins = Rect(root, "Instruction", new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f));
            ins.sizeDelta = new Vector2(980, 190);
            ins.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.015f, 0.02f, 0.78f);
            Frame(ins, 980, 190, new Color(1f, 0.75f, 0.45f, 0.6f), 3f);
            instructionStep = Label(ins, "", 24, new Vector2(1f, 1f), new Vector2(-70, -24), new Color(1f, 0.75f, 0.45f, 0.8f));
            instructionStep.rectTransform.sizeDelta = new Vector2(120, 36);
            instructionText = Label(ins, "", 31, new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Color(1f, 0.85f, 0.6f));
            instructionText.rectTransform.sizeDelta = new Vector2(920, 160);
            instruction = ins.gameObject;
            instruction.SetActive(false);

            // ощущение при улике и подсказка новичку — над приборами
            narrateText = Label(root, "", 36, new Vector2(0.5f, 0.37f), Vector2.zero, Bone);
            narrateText.rectTransform.sizeDelta = new Vector2(960, 70);
            narrateText.fontStyle = FontStyle.Italic;
            Outline(narrateText);
            hintText = Label(root, "", 27, new Vector2(0.5f, 0.32f), Vector2.zero, new Color(1f, 0.75f, 0.45f));
            hintText.rectTransform.sizeDelta = new Vector2(940, 90);
            Outline(hintText);

            trophyText = Label(root, "", 34, new Vector2(0.5f, 0.78f), Vector2.zero, new Color(1f, 0.8f, 0.4f));
            trophyText.rectTransform.sizeDelta = new Vector2(980, 120);
            trophyText.fontStyle = FontStyle.Bold;
            Outline(trophyText);

            flash = Overlay(root, "Flash", Texture2D.whiteTexture, new Color(0, 0, 0, 0));
            grain = Overlay(root, "Grain", Textures.Noise(), new Color(1, 1, 1, 0.05f));

            diagText = Label(root, "", 22, new Vector2(1f, 1f), new Vector2(-20, -40), new Color(1, 1, 1, 0.45f));
            diagText.alignment = TextAnchor.UpperRight;
            diagText.rectTransform.pivot = new Vector2(1f, 1f);
            diagText.rectTransform.sizeDelta = new Vector2(600, 260);
            diagText.gameObject.SetActive(TestMode.On);   // диагностика — только в тестовой сборке

            journal.transform.SetAsLastSibling();
            tutorial.transform.SetAsLastSibling();
            ShowOnly(loading);
        }

        void BuildHud()
        {
            hud = Rect(shakeRoot, "HUD", Vector2.zero, Vector2.one).gameObject;
            // тестовая сборка: карта глубины поверх камеры (под остальным интерфейсом)
            depthView = Rect(hud.transform, "DepthView", Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            depthView.material = new Material(Shader.Find("GhostWard/DepthView"));
            depthView.raycastTarget = false;
            depthView.gameObject.SetActive(false);

            nightText = Label(hud.transform, "", 34, new Vector2(0.5f, 1f), new Vector2(0, -80), Dim);

            clockText = Label(hud.transform, "00:00", 64, new Vector2(0.5f, 1f), new Vector2(0, -140), Bone);
            Outline(clockText);

            // шкала страха
            var bar = Rect(hud.transform, "Fear", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            bar.anchoredPosition = new Vector2(0, -205);
            bar.sizeDelta = new Vector2(520, 14);
            bar.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.12f);
            fearFill = Rect(bar, "Fill", Vector2.zero, new Vector2(0f, 1f));
            fearFill.gameObject.AddComponent<Image>().color = Blood;
            var fearLabel = Label(bar, Loc.T("fear"), 24, new Vector2(0.5f, 0f), new Vector2(0, -26), Dim);
            fearLabel.rectTransform.sizeDelta = new Vector2(300, 40);

            phaseText = Label(hud.transform, "", 30, new Vector2(0.5f, 1f), new Vector2(0, -275), Dim);
            phaseText.rectTransform.sizeDelta = new Vector2(600, 44);
            // тестовая сборка: на какой высоте держат телефон (от неё — пол, рост и места сущностей)
            phoneText = Label(hud.transform, "", 28, new Vector2(0.5f, 1f), new Vector2(0, -500), new Color(1f, 0.75f, 0.45f));
            phoneText.rectTransform.sizeDelta = new Vector2(640, 150);   // под компасом, не задевая сумку справа
            phoneText.rectTransform.pivot = new Vector2(0.5f, 1f);
            phoneText.alignment = TextAnchor.UpperCenter;
            phoneText.gameObject.SetActive(TestMode.On);
            // прицел: расстояние до того, на что он наведён, — в строке выше (проверка глубины рулеткой)
            var cross = Label(hud.transform, "+", 56, new Vector2(0.5f, 0.5f), Vector2.zero, new Color(1f, 0.75f, 0.45f, 0.8f));
            cross.rectTransform.sizeDelta = new Vector2(80, 80);
            cross.raycastTarget = false;
            cross.gameObject.SetActive(TestMode.On);
            BuildCompass();

            recText = Label(hud.transform, "● REC", 30, new Vector2(0f, 1f), new Vector2(40, -240), new Color(0.9f, 0.1f, 0.1f));
            recText.alignment = TextAnchor.MiddleLeft;
            recText.rectTransform.pivot = new Vector2(0f, 0.5f);
            recText.gameObject.SetActive(false);

            // шум с микрофона — чтобы игрок учился быть тихим
            micRoot = Rect(hud.transform, "Mic", new Vector2(0f, 0f), new Vector2(0f, 0f)).gameObject;
            ((RectTransform)micRoot.transform).anchoredPosition = new Vector2(60, 330);
            var micLabel = Label(micRoot.transform, Loc.T("noise"), 24, new Vector2(0.5f, 0.5f), new Vector2(60, 40), Dim);
            micLabel.rectTransform.sizeDelta = new Vector2(160, 36);
            for (int i = 0; i < micBars.Length; i++)
            {
                var b = Rect(micRoot.transform, "Bar", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                b.pivot = new Vector2(0.5f, 0f);
                b.anchoredPosition = new Vector2(i * 22, 0);
                b.sizeDelta = new Vector2(14, 12 + i * 8);
                micBars[i] = b.gameObject.AddComponent<Image>();
                micBars[i].raycastTarget = false;
            }
            micRoot.SetActive(false);

            camTop = Label(hud.transform, "", 34, new Vector2(0f, 1f), new Vector2(40, -310), new Color(1f, 1f, 1f, 0.8f));
            camTop.alignment = TextAnchor.MiddleLeft;
            camTop.rectTransform.pivot = new Vector2(0f, 0.5f);
            Outline(camTop);
            camBottom = Label(hud.transform, "", 30, new Vector2(1f, 0f), new Vector2(-40, 430), new Color(1f, 1f, 1f, 0.8f));
            camBottom.alignment = TextAnchor.LowerRight;
            camBottom.rectTransform.pivot = new Vector2(1f, 0f);
            Outline(camBottom);

            hiddenText = Label(hud.transform, Loc.T("hidden"), 32, new Vector2(0.5f, 0.3f), Vector2.zero, Dim);
            hiddenText.fontStyle = FontStyle.Italic;
            hiddenText.gameObject.SetActive(false);

            BuildTools();

            // дневник — книжка в левом верхнем углу
            journalButton = Rect(hud.transform, "JournalButton", new Vector2(0f, 1f), new Vector2(0f, 1f));
            journalButton.sizeDelta = new Vector2(150, 150);
            journalButton.anchoredPosition = new Vector2(100, -130);
            var bg = journalButton.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.01f);
            var btn = journalButton.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => SetJournal(true));
            Icons.Book(journalButton, Dim);

            // тестовая сборка: кнопка «глубина» — показать, что видит нейросеть
            if (TestMode.On)
            {
                var dv = Button(hud.transform, "ГЛУБ.", new Vector2(0f, 1f), () =>
                {
                    DepthViewOn = !DepthViewOn;
                    depthView.gameObject.SetActive(DepthViewOn);
                }, false, 150, 0);
                var dvr = (RectTransform)dv.transform;
                dvr.anchorMin = dvr.anchorMax = new Vector2(0f, 1f);
                dvr.sizeDelta = new Vector2(150, 80);
                dvr.anchoredPosition = new Vector2(100, -400);
                dv.GetComponentInChildren<Text>().fontSize = 26;
            }

            // записи ночи — три строки под дневником
            logButton = Rect(hud.transform, "LogButton", new Vector2(0f, 1f), new Vector2(0f, 1f));
            logButton.sizeDelta = new Vector2(130, 120);
            logButton.anchoredPosition = new Vector2(100, -280);
            var lbg = logButton.gameObject.AddComponent<Image>();
            lbg.color = new Color(0, 0, 0, 0.01f);
            var lbtn = logButton.gameObject.AddComponent<UnityEngine.UI.Button>();
            lbtn.targetGraphic = lbg;
            lbtn.transition = Selectable.Transition.None;
            lbtn.onClick.AddListener(OpenLog);
            for (int i = 0; i < 3; i++)
            {
                var line = Rect(logButton, "Line", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<UILine>();
                line.Thickness = 6f;
                line.color = Dim;
                line.raycastTarget = false;
                float y = 22f - i * 22f;
                line.SetPoints(new[] { new Vector2(-34, y), new Vector2(34, y) });
            }
        }

        void OpenLog()
        {
            SetJournal(true);
            ShowJournalPage(journalLog);
        }

        void BuildTools()
        {
            // тёплый свет свечи — снизу экрана
            candleGlow = Rect(hud.transform, "Candle", new Vector2(0f, 0f), new Vector2(1f, 0.45f)).gameObject.AddComponent<RawImage>();
            candleGlow.texture = Textures.EdgeGradient(vertical: true);
            candleGlow.color = new Color(1f, 0.7f, 0.35f, 0f);
            candleGlow.raycastTarget = false;

            toolBar = Rect(hud.transform, "Tools", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            toolBar.anchoredPosition = new Vector2(0, 110);
            toolBar.sizeDelta = new Vector2(1000, 170);
            var tools = new[] { Tool.Emf, Tool.Thermo, Tool.Night, Tool.Radio };
            for (int i = 0; i < tools.Length; i++)
            {
                var t = tools[i];
                var b = Rect(toolBar, t.ToString(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                b.sizeDelta = new Vector2(205, 160);
                b.anchoredPosition = new Vector2((i - 1.5f) * 220f, 0f);
                var bg = b.gameObject.AddComponent<Image>();
                bg.color = Ink;
                var btn = b.gameObject.AddComponent<UnityEngine.UI.Button>();
                btn.targetGraphic = bg;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => ToolSelected?.Invoke(t));
                Frame(b, 205, 160, Dim, 3f);
                var icon = Rect(b, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                icon.anchoredPosition = new Vector2(0, 18);
                icon.localScale = Vector3.one * 0.75f;
                Icons.ToolIcon(icon, t, Bone);
                var name = Label(b, Loc.T($"t.{t}.name"), 26, new Vector2(0.5f, 0f), new Vector2(0, 26), Bone);
                name.rectTransform.sizeDelta = new Vector2(220, 40);
                toolButtons.Add((t, bg));
            }

            readout = Label(hud.transform, "", 46, new Vector2(0.5f, 0f), new Vector2(0, 250), Bone);
            readout.rectTransform.sizeDelta = new Vector2(900, 70);
            readout.fontStyle = FontStyle.Bold;
            Outline(readout);

            // сумка с защитой — справа
            bagButton = Rect(hud.transform, "Bag", new Vector2(1f, 1f), new Vector2(1f, 1f));
            bagButton.sizeDelta = new Vector2(150, 150);
            bagButton.anchoredPosition = new Vector2(-100, -430);
            var bbg = bagButton.gameObject.AddComponent<Image>();
            bbg.color = new Color(0, 0, 0, 0.01f);
            var bbtn = bagButton.gameObject.AddComponent<UnityEngine.UI.Button>();
            bbtn.targetGraphic = bbg;
            bbtn.transition = Selectable.Transition.None;
            bbtn.onClick.AddListener(() => bagPanel.gameObject.SetActive(!bagPanel.gameObject.activeSelf));
            Icons.Bag(bagButton, Dim);

            bagPanel = Rect(hud.transform, "BagPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            bagPanel.sizeDelta = new Vector2(820, 1300);
            bagPanel.anchoredPosition = new Vector2(0, 60);
            bagPanel.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.015f, 0.02f, 0.93f);
            Frame(bagPanel, 820, 1300, Dim, 3f);
            // крестик: панель перекрывает кнопку сумки, закрывать надо отсюда
            var close = Rect(bagPanel, "Close", new Vector2(1f, 1f), new Vector2(1f, 1f));
            close.sizeDelta = new Vector2(110, 110);
            close.anchoredPosition = new Vector2(-55, -55);
            var cbg = close.gameObject.AddComponent<Image>();
            cbg.color = new Color(0, 0, 0, 0.01f);
            var cbtn = close.gameObject.AddComponent<UnityEngine.UI.Button>();
            cbtn.targetGraphic = cbg;
            cbtn.transition = Selectable.Transition.None;
            cbtn.onClick.AddListener(CloseBag);
            var x1 = Rect(close, "X1", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<UILine>();
            x1.Thickness = 6f;
            x1.color = Bone;
            x1.raycastTarget = false;
            x1.SetPoints(new[] { new Vector2(-20, -20), new Vector2(20, 20) });
            var x2 = Rect(close, "X2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<UILine>();
            x2.Thickness = 6f;
            x2.color = Bone;
            x2.raycastTarget = false;
            x2.SetPoints(new[] { new Vector2(-20, 20), new Vector2(20, -20) });

            var bt = Label(bagPanel, Loc.T("bag"), 40, new Vector2(0.5f, 1f), new Vector2(0, -50), Bone);
            bt.rectTransform.sizeDelta = new Vector2(700, 60);
            bt.fontStyle = FontStyle.Bold;
            var kinds = new[] { Protect.Circle, Protect.Candle, Protect.Tumar, Protect.Calm };
            for (int i = 0; i < kinds.Length; i++)
            {
                var k = kinds[i];
                var row = Rect(bagPanel, k.ToString(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                row.sizeDelta = new Vector2(760, 125);
                row.anchoredPosition = new Vector2(0, -160 - i * 135);
                var rbg = row.gameObject.AddComponent<Image>();
                rbg.color = Ink;
                var rbtn = row.gameObject.AddComponent<UnityEngine.UI.Button>();
                rbtn.targetGraphic = rbg;
                rbtn.onClick.AddListener(() => ProtectUsed?.Invoke(k));
                Frame(row, 760, 125, Dim, 2f);
                var icon = Rect(row, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
                icon.anchoredPosition = new Vector2(80, 0);
                icon.localScale = Vector3.one * 0.8f;
                Icons.ProtectIcon(icon, k, Bone);
                var label = Label(row, "", 30, new Vector2(0f, 0.5f), new Vector2(170, 0), Bone);
                label.rectTransform.pivot = new Vector2(0f, 0.5f);
                label.rectTransform.sizeDelta = new Vector2(570, 130);
                label.alignment = TextAnchor.MiddleLeft;
                bagRows.Add((k, label));
            }

            // приманки: проверить догадку — «своя» сущность откликнется
            var lt = Label(bagPanel, Loc.T("lures"), 36, new Vector2(0.5f, 1f), new Vector2(0, -735), Bone);
            lt.rectTransform.sizeDelta = new Vector2(700, 56);
            lt.fontStyle = FontStyle.Bold;
            var lures = new[] { Lure.Photo, Lure.Yarn, Lure.Meat, Lure.MusicBox };
            for (int i = 0; i < lures.Length; i++)
            {
                var k = lures[i];
                var row = Rect(bagPanel, k.ToString(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                row.sizeDelta = new Vector2(760, 125);
                row.anchoredPosition = new Vector2(0, -830 - i * 135);
                var rbg = row.gameObject.AddComponent<Image>();
                rbg.color = Ink;
                var rbtn = row.gameObject.AddComponent<UnityEngine.UI.Button>();
                rbtn.targetGraphic = rbg;
                rbtn.onClick.AddListener(() => LureUsed?.Invoke(k));
                Frame(row, 760, 125, Dim, 2f);
                var icon = Rect(row, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
                icon.anchoredPosition = new Vector2(80, 0);
                icon.localScale = Vector3.one * 0.8f;
                Icons.LureIcon(icon, k, Bone);
                var label = Label(row, "", 28, new Vector2(0f, 0.5f), new Vector2(170, 0), Bone);
                label.rectTransform.pivot = new Vector2(0f, 0.5f);
                label.rectTransform.sizeDelta = new Vector2(570, 120);
                label.alignment = TextAnchor.MiddleLeft;
                lureRows.Add((k, label));
            }
            bagPanel.gameObject.SetActive(false);
        }

        public void SetTool(Tool tool)
        {
            foreach (var (t, bg) in toolButtons) bg.color = t == tool ? Blood : Ink;
        }

        public void SetToolReadout(string text, Color color)
        {
            readout.text = text;
            readout.color = color;
        }

        public void SetProtection(Inventory inv)
        {
            if (inv == null) return;
            foreach (var (k, label) in bagRows)
                label.text = $"<b>{Loc.T($"p.{k}.name")}</b>  ×{inv.Count(k)}\n<size=24>{Loc.T($"p.{k}.desc")}</size>";
            foreach (var (k, label) in lureRows)
                label.text = $"<b>{Loc.T($"l.{k}.name")}</b>  ×{inv.Count(k)}\n<size=22>{Loc.T($"l.{k}.desc")}</size>";
        }

        public void CloseBag() => bagPanel.gameObject.SetActive(false);

        // для превью из редактора: экран ночи с открытой сумкой
        public void DebugHud(Inventory inv, bool bag)
        {
            ShowHud();
            SetNight(2);
            SetClock(0.4f);
            SetPhase(Loc.T("phase_investigate"), false);
            SetTool(Tool.Emf);
            SetToolReadout("EMF  ■■■□□", new Color(1f, 0.8f, 0.4f));
            SetFear(0.35f);
            SetProtection(inv);
            bagPanel.gameObject.SetActive(bag);
        }

        public void SetCandle(bool lit) => candleGlow.color = new Color(1f, 0.7f, 0.35f, lit ? 0.18f + UnityEngine.Random.Range(-0.02f, 0.02f) : 0f);

        // компас: деления сторон света двигаются с поворотом; стрелка дрожит у логова (улика «Компас»)
        void BuildCompass()
        {
            var rt = Rect(hud.transform, "Compass", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            rt.anchoredPosition = new Vector2(0, -345);
            for (int yaw = 0; yaw < 360; yaw += 15)
            {
                var tick = Rect(rt, "Tick", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                bool major = yaw % 90 == 0;
                tick.sizeDelta = new Vector2(3, major ? 26 : 14);
                var img = tick.gameObject.AddComponent<Image>();
                img.color = major ? Bone : Dim;
                img.raycastTarget = false;
                compassTicks.Add((tick, yaw));
                if (major)
                {
                    string n = Loc.T("compass").Split(',')[yaw / 90];
                    var l = Label(rt, n, 26, new Vector2(0.5f, 0.5f), new Vector2(0, 32), Dim);
                    l.rectTransform.sizeDelta = new Vector2(60, 36);
                    compassLabels.Add((l, yaw));
                }
            }
            needle = Rect(rt, "Needle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<UILine>();
            needle.Filled = true;
            needle.raycastTarget = false;
            needle.color = Bone;
            needle.SetPoints(new[] { new Vector2(0, 8), new Vector2(10, -22), new Vector2(-10, -22) });
            needle.rectTransform.anchoredPosition = new Vector2(0, -26);

            // метка «оно там»: после улики — к логову, на охоте — к сущности
            compassMark = Rect(rt, "Mark", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<UILine>();
            compassMark.Filled = true;
            compassMark.raycastTarget = false;
            compassMark.SetPoints(new[] { new Vector2(0, -14), new Vector2(14, 10), new Vector2(-14, 10) });
            compassMark.gameObject.SetActive(false);
        }

        void BuildScan()
        {
            scan = Rect(shakeRoot, "Scan", Vector2.zero, Vector2.one).gameObject;
            scanText = Label(scan.transform, Loc.T("scan_title"), 60, new Vector2(0.5f, 0.42f), Vector2.zero, Bone);
            scanText.rectTransform.sizeDelta = new Vector2(900, 100);
            Outline(scanText);
            var bar = Rect(scan.transform, "Progress", new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f));
            bar.anchoredPosition = new Vector2(0, -70);
            bar.sizeDelta = new Vector2(520, 10);
            bar.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.15f);
            scanFill = Rect(bar, "Fill", Vector2.zero, new Vector2(0f, 1f));
            scanFill.gameObject.AddComponent<Image>().color = Bone;

            // как держишь телефон: от этого высота над полом и рост сущностей (в AR не нужно — ARCore знает сам)
            posture = Rect(scan.transform, "Posture", Vector2.zero, Vector2.one).gameObject;
            var pt = Label(posture.transform, Loc.T("posture_title"), 32, new Vector2(0.5f, 0.3f), Vector2.zero, Bone);
            pt.rectTransform.sizeDelta = new Vector2(900, 50);
            Outline(pt);
            string[] pk = { "posture_stand", "posture_sit", "posture_lie" };
            for (int i = 0; i < 3; i++)
            {
                int p = i;
                var b = Button(posture.transform, Loc.T(pk[i]), new Vector2(0.5f, 0.245f), () =>
                {
                    Settings.Posture = p;
                    RefreshPosture();
                    PostureChanged?.Invoke();
                }, false, 300, (i - 1) * 320f);
                postureButtons.Add((b.GetComponent<Image>(), p));
            }
            RefreshPosture();
            scan.SetActive(false);
        }

        // полоска отладки: темнота по 24 направлениям вокруг игрока и текущий взгляд
        void BuildStrip()
        {
            var rt = Rect(root, "MapStrip", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            rt.anchoredPosition = new Vector2(0, 60);
            rt.sizeDelta = new Vector2(720, 26);
            strip = rt.gameObject;
            float w = 720f / DarknessMap.YawCells;
            for (int i = 0; i < DarknessMap.YawCells; i++)
            {
                var c = Rect(rt, "Cell", new Vector2(0f, 0f), new Vector2(0f, 1f));
                c.pivot = new Vector2(0f, 0.5f);
                c.anchoredPosition = new Vector2(i * w, 0);
                c.sizeDelta = new Vector2(w - 2, 0);
                stripCells[i] = c.gameObject.AddComponent<Image>();
                stripCells[i].raycastTarget = false;
            }
            stripMarker = Rect(rt, "Marker", new Vector2(0f, 0f), new Vector2(0f, 1f));
            stripMarker.sizeDelta = new Vector2(4, 16);
            var m = stripMarker.gameObject.AddComponent<Image>();
            m.color = new Color(1f, 0.3f, 0.3f);
            m.raycastTarget = false;
            strip.SetActive(false);
        }

        void BuildMenu()
        {
            menu = Panel(root, "Menu");
            menu.GetComponent<Image>().color = new Color(0.008f, 0.006f, 0.01f, 1f);

            // на фоне — едва заметная фигура албасты
            var bg = Rect(menu.transform, "Figure", new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f)).gameObject.AddComponent<RawImage>();
            bg.rectTransform.sizeDelta = new Vector2(900, 1125);
            bg.raycastTarget = false;
            menuMat = Entity.CreateMaterial(Catalog.Get(EntityKind.Albasty), 4.2f);
            menuMat.SetFloat("_Fade", 0.22f);
            bg.material = menuMat;

            var title = Label(menu.transform, Loc.Brand.ToUpper(), 110, new Vector2(0.5f, 0.8f), Vector2.zero, Bone);
            title.rectTransform.sizeDelta = new Vector2(1060, 140);
            title.fontStyle = FontStyle.Bold;
            Outline(title);
            var sub = Label(menu.transform, Loc.T("title_sub").ToUpper(), 54, new Vector2(0.5f, 0.74f), Vector2.zero, new Color(0.8f, 0.15f, 0.15f));
            sub.rectTransform.sizeDelta = new Vector2(1000, 80);
            sub.fontStyle = FontStyle.Bold;
            var tag = Label(menu.transform, Loc.T("tagline"), 32, new Vector2(0.5f, 0.66f), Vector2.zero, Dim);
            tag.rectTransform.sizeDelta = new Vector2(880, 120);

            // звание и опыт — касание открывает список званий
            var rank = Rect(menu.transform, "Rank", new Vector2(0.5f, 0.575f), new Vector2(0.5f, 0.575f));
            rank.sizeDelta = new Vector2(640, 110);
            var rbg = rank.gameObject.AddComponent<Image>();
            rbg.color = new Color(0, 0, 0, 0.01f);
            var rbtn = rank.gameObject.AddComponent<UnityEngine.UI.Button>();
            rbtn.targetGraphic = rbg;
            rbtn.transition = Selectable.Transition.None;
            rbtn.onClick.AddListener(OpenRanks);
            rankName = Label(rank, "", 38, new Vector2(0.5f, 1f), new Vector2(0, -24), Bone);
            rankName.rectTransform.sizeDelta = new Vector2(640, 50);
            rankName.fontStyle = FontStyle.Bold;
            var bar = Rect(rank, "Bar", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            bar.anchoredPosition = new Vector2(0, -10);
            bar.sizeDelta = new Vector2(520, 10);
            bar.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.12f);
            rankFill = Rect(bar, "Fill", Vector2.zero, new Vector2(0f, 1f));
            rankFill.gameObject.AddComponent<Image>().color = new Color(1f, 0.75f, 0.45f);
            rankXp = Label(rank, "", 24, new Vector2(0.5f, 0f), new Vector2(0, 14), Dim);
            rankXp.rectTransform.sizeDelta = new Vector2(640, 36);

            Button(menu.transform, Loc.T("play"), new Vector2(0.5f, 0.49f), () => PlayPressed?.Invoke(), true);
            Button(menu.transform, Loc.T("daily"), new Vector2(0.5f, 0.415f), () => DailyPressed?.Invoke(), false, 310, -165);
            Button(menu.transform, Loc.T("story"), new Vector2(0.5f, 0.415f), () => OpenStory(menu, Progress.StoryUnlocked), false, 310, 165);
            Button(menu.transform, Loc.T("journal"), new Vector2(0.5f, 0.34f), () => SetJournal(true), false, 310, -165);
            Button(menu.transform, Loc.T("howto"), new Vector2(0.5f, 0.34f), () => OpenTutorial(false), false, 310, 165);
            Button(menu.transform, Loc.T("settings"), new Vector2(0.5f, 0.265f), () => ShowOnly(settings), false);
            BuildMenuItems();
            var tip = Label(menu.transform, Loc.T("tip"), 26, new Vector2(0.5f, 0.045f), Vector2.zero, Dim);
            tip.rectTransform.sizeDelta = new Vector2(900, 50);
        }

        // Чем прогонять: шесть предметов с жестами прямо в меню; касание — тренировка жеста.
        readonly List<(ItemKind kind, Text check)> menuItems = new();

        void BuildMenuItems()
        {
            var cap = Label(menu.transform, Loc.T("menu_items"), 26, new Vector2(0.5f, 0.205f), Vector2.zero, Dim);
            cap.rectTransform.sizeDelta = new Vector2(980, 44);
            const float cw = 154f;
            for (int i = 0; i < Catalog.Items.Length; i++)
            {
                var def = Catalog.Items[i];
                var kind = def.Kind;
                var cell = Rect(menu.transform, "Item", new Vector2(0.5f, 0.13f), new Vector2(0.5f, 0.13f));
                cell.sizeDelta = new Vector2(cw - 6f, 200f);
                cell.anchoredPosition = new Vector2((i - 2.5f) * cw, 0f);
                var bg = cell.gameObject.AddComponent<Image>();
                bg.color = Ink;
                var btn = cell.gameObject.AddComponent<UnityEngine.UI.Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() =>
                {
                    SetJournal(true);
                    ShowJournalPage(journalItems);
                    OpenPractice(kind);
                });
                Frame(cell, cw - 6f, 200f, Dim, 2f);

                var icon = Rect(cell, "Icon", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                icon.anchoredPosition = new Vector2(-32, -50);
                icon.localScale = Vector3.one * 0.42f;
                Icons.Item(icon, kind, Bone);
                var gesture = Rect(cell, "Gesture", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                gesture.anchoredPosition = new Vector2(34, -50);
                gesture.localScale = Vector3.one * 0.42f;
                Icons.GestureIcon(gesture, def.Gesture, Warm);

                var name = Label(cell, def.Name, 24, new Vector2(0.5f, 0f), new Vector2(0, 74), Bone);
                name.rectTransform.sizeDelta = new Vector2(cw - 14f, 36);
                name.fontStyle = FontStyle.Bold;
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 14;
                name.resizeTextMaxSize = 24;
                var how = Label(cell, Loc.T($"g.{kind}"), 22, new Vector2(0.5f, 0f), new Vector2(0, 32), Warm);
                how.rectTransform.sizeDelta = new Vector2(cw - 12f, 52);
                how.resizeTextForBestFit = true;
                how.resizeTextMinSize = 17;
                how.resizeTextMaxSize = 22;

                var check = Label(cell, "✓", 26, new Vector2(1f, 0f), new Vector2(-14, 14), Warm);
                check.rectTransform.sizeDelta = new Vector2(30, 30);
                check.fontStyle = FontStyle.Bold;
                menuItems.Add((kind, check));
            }
        }

        void RefreshMenuItems()
        {
            foreach (var (kind, check) in menuItems) check.gameObject.SetActive(Skills.Learned(kind));
        }

        // Выбор языка при первом запуске.
        void BuildLanguage()
        {
            language = Panel(root, "Language");
            language.GetComponent<Image>().color = new Color(0.008f, 0.006f, 0.01f, 1f);
            var title = Label(language.transform, "Language · Язык · Тіл · Idioma", 40, new Vector2(0.5f, 0.78f), Vector2.zero, Bone);
            title.rectTransform.sizeDelta = new Vector2(1000, 80);
            for (int i = 0; i < Loc.LangNames.Length; i++)
            {
                var lang = (Lang)i;
                Button(language.transform, Loc.LangNames[i], new Vector2(0.5f, 0.64f - i * 0.085f), () => LanguageChosen?.Invoke(lang), lang == Lang.En);
            }
        }

        // Настройки на двух вкладках: «Основное» (язык, вид камеры, качество) и «Звук» (громкость, микрофон, вибрация).
        void BuildSettings()
        {
            BuildVisionPreview();
            settings = Panel(root, "Settings");
            settings.GetComponent<Image>().color = new Color(0.01f, 0.008f, 0.012f, 0.98f);
            var title = Label(settings.transform, Loc.T("settings"), 64, new Vector2(0.5f, 0.95f), Vector2.zero, Bone);
            title.rectTransform.sizeDelta = new Vector2(900, 100);
            title.fontStyle = FontStyle.Bold;

            var tg = Button(settings.transform, Loc.T("tab_general"), new Vector2(0.5f, 0.885f), () => SettingsTab(true), false, 400, -210);
            var ts = Button(settings.transform, Loc.T("tab_sound"), new Vector2(0.5f, 0.885f), () => SettingsTab(false), false, 400, 210);
            settingTabs.Add((tg.GetComponent<Image>(), true));
            settingTabs.Add((ts.GetComponent<Image>(), false));

            settingsGeneral = Rect(settings.transform, "General", Vector2.zero, Vector2.one).gameObject;
            settingsSound = Rect(settings.transform, "Sound", Vector2.zero, Vector2.one).gameObject;
            var g = settingsGeneral.transform;
            var snd = settingsSound.transform;

            // язык
            Caption(g, Loc.T("choose_lang"), 0.8f);
            for (int i = 0; i < Loc.LangNames.Length; i++)
            {
                var lang = (Lang)i;
                int row = i / 3, col = i % 3, inRow = row == 0 ? 3 : 2;
                float x = (col - (inRow - 1) * 0.5f) * 320f;
                var b = Button(g, Loc.LangNames[i], new Vector2(0.5f, 0.755f - row * 0.07f), () => LanguageChosen?.Invoke(lang), false, 300, x);
                b.GetComponentInChildren<Text>().fontSize = 32;
                settingButtons.Add((b.GetComponent<Image>(), () => Loc.Current == lang));
            }

            // вид камеры
            Caption(g, Loc.T("vision"), 0.625f);
            var visions = new[] { Vision.Normal, Vision.Dark, Vision.NightGreen, Vision.NightShot, Vision.Cctv, Vision.Vhs };
            string[] keys = { "vision_normal", "vision_dark", "vision_night", "vision_nightshot", "vision_cctv", "vision_vhs" };
            for (int i = 0; i < visions.Length; i++)
            {
                var v = visions[i];
                int row = i / 3, col = i % 3, inRow = Mathf.Min(3, visions.Length - row * 3);
                float x = (col - (inRow - 1) * 0.5f) * 320f;
                string label = Loc.T(keys[i]) + (Unlocks.Has(v) ? "" : "  ▶");
                var b = Button(g, label, new Vector2(0.5f, 0.575f - row * 0.07f), () => PickVision(v), false, 300, x);
                b.GetComponentInChildren<Text>().fontSize = 30;
                settingButtons.Add((b.GetComponent<Image>(), () => Settings.Vision == v));
            }
            var note = Label(g, Loc.T("vision_note"), 25, new Vector2(0.5f, 0.45f), Vector2.zero, Dim);
            note.rectTransform.sizeDelta = new Vector2(940, 70);

            // качество графики
            Caption(g, Loc.T("quality"), 0.39f);
            string[] q = { "low", "medium", "high" };
            for (int i = 0; i < 3; i++)
            {
                int level = i;
                var b = Button(g, Loc.T(q[i]), new Vector2(0.5f, 0.34f), () => { Settings.Quality = level; RefreshSettings(); }, false, 300, (i - 1) * 320f);
                settingButtons.Add((b.GetComponent<Image>(), () => Settings.Quality == level));
            }
            var qn = Label(g, Loc.T("quality_note"), 25, new Vector2(0.5f, 0.285f), Vector2.zero, Dim);
            qn.rectTransform.sizeDelta = new Vector2(940, 60);

            // режим AR: авто без Маркета, выкл, или явная установка сервисов AR
            Caption(g, Loc.T("ar"), 0.235f);
            for (int i = 0; i < 2; i++)
            {
                int mode = i;
                var b = Button(g, Loc.T(mode == 0 ? "ar_auto" : "off"), new Vector2(0.5f, 0.185f), () => { Settings.ArMode = mode; RefreshSettings(); }, false, 300, (i - 1) * 320f);
                settingButtons.Add((b.GetComponent<Image>(), () => Settings.ArMode == mode));
            }
            var install = Button(g, Loc.T("ar_install"), new Vector2(0.5f, 0.185f), () =>
            {
                Settings.ArMode = 0;
                Settings.ArInstallRequested = true;
                RefreshSettings();
                Message(Loc.T("ar_restart"), 3f);
            }, false, 300, 320f);
            install.GetComponentInChildren<Text>().fontSize = 30;
            var an = Label(g, Loc.T("ar_note"), 23, new Vector2(0.5f, 0.135f), Vector2.zero, Dim);
            an.rectTransform.sizeDelta = new Vector2(940, 60);

            // звук
            VolumeRow(snd, Loc.T("master"), 0.82f, () => Settings.Volume, v => Settings.Volume = v);
            VolumeRow(snd, Loc.T("music_vol"), 0.71f, () => Settings.Music, v => Settings.Music = v);
            VolumeRow(snd, Loc.T("ambience_vol"), 0.6f, () => Settings.Ambience, v => Settings.Ambience = v);
            VolumeRow(snd, Loc.T("sfx_vol"), 0.49f, () => Settings.Sfx, v => Settings.Sfx = v);

            Caption(snd, Loc.T("mic"), 0.385f);
            string[] sens = { "low", "medium", "high" };
            for (int i = 0; i < 3; i++)
            {
                int level = i;
                var b = Button(snd, Loc.T(sens[i]), new Vector2(0.5f, 0.335f), () => { Settings.MicSensitivity = level; RefreshSettings(); }, false, 300, (i - 1) * 320f);
                settingButtons.Add((b.GetComponent<Image>(), () => Settings.MicSensitivity == level));
            }

            Caption(snd, Loc.T("vibration"), 0.265f);
            for (int i = 0; i < 2; i++)
            {
                bool on = i == 0;
                var b = Button(snd, Loc.T(on ? "on" : "off"), new Vector2(0.5f, 0.215f), () => { Settings.Vibration = on; RefreshSettings(); }, false, 300, (i - 0.5f) * 320f);
                settingButtons.Add((b.GetComponent<Image>(), () => Settings.Vibration == on));
            }

            Button(settings.transform, Loc.T("back"), new Vector2(0.5f, 0.08f), () => ShowOnly(menu), false);
            SettingsTab(true);
        }

        void SettingsTab(bool general)
        {
            settingsGeneral.SetActive(general);
            settingsSound.SetActive(!general);
            foreach (var (img, isGeneral) in settingTabs) img.color = isGeneral == general ? Blood : Ink;
            RefreshSettings();
        }

        void VolumeRow(Transform parent, string caption, float y, Func<float> get, Action<float> set)
        {
            Caption(parent, caption, y);
            Button(parent, "−", new Vector2(0.5f, y - 0.05f), () => { set(get() - 0.1f); RefreshSettings(); }, false, 160, -250);
            Button(parent, "+", new Vector2(0.5f, y - 0.05f), () => { set(get() + 0.1f); RefreshSettings(); }, false, 160, 250);
            var t = Label(parent, "", 44, new Vector2(0.5f, y - 0.05f), Vector2.zero, Bone);
            t.rectTransform.sizeDelta = new Vector2(300, 80);
            volumeTexts.Add((t, get));
        }

        void Caption(Transform parent, string text, float y)
        {
            var c = Label(parent, text, 32, new Vector2(0.5f, y), Vector2.zero, Dim);
            c.rectTransform.sizeDelta = new Vector2(940, 50);
        }

        void PickVision(Vision v)
        {
            if (Unlocks.Has(v)) { Settings.Vision = v; RefreshSettings(); ShowVisionPreview(v); return; }
            // закрытый режим открывается за наградную рекламу (позже — и покупкой)
            Ads.ShowRewarded(() =>
            {
                Unlocks.Grant(v);
                Settings.Vision = v;
                RefreshSettings();
                ShowVisionPreview(v);
            });
        }

        // Живое превью: в меню камера игры не видна, поэтому выбранный вид показываем сразу, на весь экран.
        void BuildVisionPreview()
        {
            visionPreview = Panel(root, "VisionPreview");
            visionPreview.GetComponent<Image>().color = Color.black;
            previewImage = Rect(visionPreview.transform, "Feed", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<RawImage>();
            previewImage.raycastTarget = false;
            previewMat = new Material(Shader.Find("GhostWard/Feed"));
            previewImage.material = previewMat;
            previewTitle = Label(visionPreview.transform, "", 52, new Vector2(0.5f, 0.92f), Vector2.zero, Bone);
            previewTitle.rectTransform.sizeDelta = new Vector2(960, 160);
            previewTitle.fontStyle = FontStyle.Bold;
            Outline(previewTitle);
            var hint = Label(visionPreview.transform, Loc.T("preview_tap"), 30, new Vector2(0.5f, 0.07f), Vector2.zero, Bone);
            hint.rectTransform.sizeDelta = new Vector2(900, 60);
            Outline(hint);
            var btn = visionPreview.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = visionPreview.GetComponent<Image>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(CloseVisionPreview);
            visionPreview.SetActive(false);
        }

        void ShowVisionPreview(Vision v)
        {
            var cam = PreviewCamera?.Invoke();
            if (cam == null || !cam.isPlaying)
            {
                bool allowed = true;
#if UNITY_ANDROID && !UNITY_EDITOR
                allowed = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera);
#endif
                if (allowed && ownPreviewCam == null && WebCamTexture.devices.Length > 0)
                {
                    string device = WebCamTexture.devices[0].name;
                    foreach (var d in WebCamTexture.devices) if (!d.isFrontFacing) { device = d.name; break; }
                    ownPreviewCam = new WebCamTexture(device, 1280, 720, 30);
                }
                if (ownPreviewCam != null) ownPreviewCam.Play();
                cam = ownPreviewCam;
            }
            previewImage.texture = cam;
            previewImage.color = cam != null ? Color.white : Color.black;
            previewMat.SetFloat("_Style", (int)v);
            previewMat.SetFloat("_Mode", 0f);
            previewMat.SetFloat("_Glitch", 0f);
            previewTitle.text = Loc.T(VisionKey(v)) + (cam == null ? "\n<size=30>" + Loc.T("preview_no_camera") + "</size>" : "");
            visionPreview.SetActive(true);
            visionPreview.transform.SetAsLastSibling();
        }

        void CloseVisionPreview()
        {
            visionPreview.SetActive(false);
            if (ownPreviewCam != null) ownPreviewCam.Stop();
        }

        static string VisionKey(Vision v) => v switch
        {
            Vision.Normal => "vision_normal",
            Vision.Dark => "vision_dark",
            Vision.NightGreen => "vision_night",
            Vision.NightShot => "vision_nightshot",
            Vision.Cctv => "vision_cctv",
            _ => "vision_vhs"
        };

        // кадр камеры: повернуть и растянуть на весь экран, как фон игры
        void FitPreview()
        {
            if (previewImage.texture is not WebCamTexture cam || cam.width < 32) return;
            int angle = cam.videoRotationAngle;
            bool sideways = angle % 180 != 0;
            float texW = sideways ? cam.height : cam.width, texH = sideways ? cam.width : cam.height;
            var size = root.rect.size;
            float scale = Mathf.Max(size.x / texW, size.y / texH);
            previewImage.rectTransform.sizeDelta = new Vector2(cam.width * scale, cam.height * scale);
            previewImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -angle);
            previewImage.uvRect = cam.videoVerticallyMirrored ? new Rect(0f, 1f, 1f, -1f) : new Rect(0f, 0f, 1f, 1f);
        }

        void RefreshSettings()
        {
            foreach (var (img, selected) in settingButtons) img.color = selected() ? Blood : Ink;
            foreach (var (text, value) in volumeTexts) text.text = Mathf.RoundToInt(value() * 100f) + "%";
        }

        public void ShowSettings() => ShowOnly(settings);

        public void SetSecondChance(bool available) => secondChance.SetActive(available);

        // опыт за ночь, новое звание, найденная страница истории
        public void SetNextNightVisible(bool visible) => nextNightButton.SetActive(visible);

        public void ShowNightProgress(int xp, bool rankUp, bool newPage, string extra = "")
        {
            string text = Loc.F("xp_gain", xp);
            if (!string.IsNullOrEmpty(extra)) text = extra + "\n" + text;
            if (rankUp)
            {
                int r = Progress.Rank;
                text += "\n" + Loc.F("rank_up", Progress.RankName(r), Progress.Perk(r));
            }
            if (dawn.activeSelf)
            {
                if (newPage) text += "\n" + Loc.T("story_found");
                dawnXp.text = text;
                readButton.SetActive(newPage);
            }
            else overXp.text = text;
        }

        public void ShowBonus(string what)
        {
            bonusButton.SetActive(false);
            dawnSupply.text += "\n" + Loc.F("bonus_got", what);
        }

        // для превью из редактора
        public void DebugTutorial(int page)
        {
            OpenTutorial(true);
            tutPage = page;
            ShowTutorialPage();
        }
        public void ShowLanguage() => ShowOnly(language);

        // Обучение: пять страниц со схемами.
        void BuildTutorial()
        {
            tutorial = Panel(root, "Tutorial");
            tutorial.GetComponent<Image>().color = new Color(0.01f, 0.008f, 0.012f, 0.98f);
            tutTitle = Label(tutorial.transform, "", 58, new Vector2(0.5f, 0.84f), Vector2.zero, Bone);
            tutTitle.rectTransform.sizeDelta = new Vector2(1000, 90);
            tutTitle.fontStyle = FontStyle.Bold;
            tutIcon = Rect(tutorial.transform, "Icon", new Vector2(0.5f, 0.67f), new Vector2(0.5f, 0.67f));
            tutBody = Label(tutorial.transform, "", 34, new Vector2(0.5f, 0.52f), Vector2.zero, Bone);
            tutBody.rectTransform.pivot = new Vector2(0.5f, 1f);
            tutBody.rectTransform.sizeDelta = new Vector2(900, 600);
            tutBody.alignment = TextAnchor.UpperCenter;
            for (int i = 0; i < 5; i++)
            {
                var d = Rect(tutorial.transform, "Dot", new Vector2(0.5f, 0.17f), new Vector2(0.5f, 0.17f));
                d.sizeDelta = new Vector2(18, 18);
                d.anchoredPosition = new Vector2((i - 2) * 40f, 0f);
                tutDots.Add(d.gameObject.AddComponent<Image>());
            }
            Button(tutorial.transform, Loc.T("back"), new Vector2(0.5f, 0.08f), () => TutorialStep(-1), false, 310, -165);
            var next = Button(tutorial.transform, Loc.T("next"), new Vector2(0.5f, 0.08f), () => TutorialStep(1), true, 310, 165);
            tutNext = next.GetComponentInChildren<Text>();
            tutorial.SetActive(false);
        }

        public void OpenTutorial(bool fromPlay)
        {
            tutFromPlay = fromPlay;
            tutPage = 0;
            tutorial.SetActive(true);
            ShowTutorialPage();
        }

        void TutorialStep(int delta)
        {
            int page = tutPage + delta;
            if (page < 0) { tutorial.SetActive(false); return; }
            if (page > 4)
            {
                tutorial.SetActive(false);
                Settings.TutorialSeen = true;
                if (tutFromPlay) TutorialFinished?.Invoke();
                return;
            }
            tutPage = page;
            ShowTutorialPage();
        }

        void ShowTutorialPage()
        {
            tutTitle.text = Loc.T($"tut{tutPage + 1}_t");
            tutBody.text = Loc.T($"tut{tutPage + 1}_b");
            for (int i = 0; i < tutDots.Count; i++) tutDots[i].color = i == tutPage ? Bone : Dim;
            tutNext.text = tutPage < 4 ? Loc.T("next") : tutFromPlay ? Loc.T("play") : Loc.T("close");

            Clear(tutIcon);
            var amber = new Color(1f, 0.75f, 0.45f);
            switch (tutPage)
            {
                case 0: Icon(tutIcon, Vector2.zero, 2.2f, r => Icons.Book(r, Bone)); break;
                case 1: Icon(tutIcon, Vector2.zero, 2f, r => Icons.Clue(r, Evidence.NightVision, Bone)); break;
                case 2:
                    var clues = new[] { Evidence.Frost, Evidence.Whisper, Evidence.Knock, Evidence.Radio };
                    for (int i = 0; i < clues.Length; i++)
                    {
                        var e = clues[i];
                        Icon(tutIcon, new Vector2((i - 1.5f) * 190f, 0f), 1.2f, r => Icons.Clue(r, e, Bone));
                    }
                    break;
                case 3:
                    Icon(tutIcon, Vector2.zero, 2.2f, r =>
                    {
                        var l = r.gameObject.AddComponent<UILine>();
                        l.Thickness = 10f;
                        l.color = amber;
                        l.SetPoints(new[] { new Vector2(-40, 0), new Vector2(-10, -30), new Vector2(45, 35) });
                    });
                    break;
                case 4:
                    var gestures = new[] { Gesture.SwipeUp, Gesture.Hold, Gesture.DoubleTap, Gesture.Shake };
                    for (int i = 0; i < gestures.Length; i++)
                    {
                        var g = gestures[i];
                        Icon(tutIcon, new Vector2((i - 1.5f) * 190f, 0f), 1.1f, r => Icons.GestureIcon(r, g, amber));
                    }
                    break;
            }
        }

        static void Icon(RectTransform parent, Vector2 pos, float scale, Action<RectTransform> draw)
        {
            var rt = Rect(parent, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.anchoredPosition = pos;
            rt.localScale = Vector3.one * scale;
            draw(rt);
        }

        // подписи поверх кадра для видов «камера наблюдения» и «VHS»
        public void SetCameraOverlay(Vision v)
        {
            overlayStyle = v;
            if (camTop == null) return;
            var now = DateTime.Now;
            switch (v)
            {
                case Vision.Cctv:
                    camTop.text = "CAM 03";
                    camBottom.text = now.ToString("yyyy-MM-dd  HH:mm:ss");
                    break;
                case Vision.Vhs:
                    camTop.text = "▶ PLAY";
                    camBottom.text = now.ToString("tt h:mm", System.Globalization.CultureInfo.InvariantCulture) + "\n" +
                                     now.ToString("MMM. dd yyyy", System.Globalization.CultureInfo.InvariantCulture).ToUpper();
                    break;
                case Vision.NightGreen:
                    camTop.text = "NV ●";
                    camBottom.text = "";
                    break;
                case Vision.NightShot:
                    camTop.text = "● REC   IR";
                    camBottom.text = "0 LUX   " + now.ToString("HH:mm:ss");
                    break;
                default:
                    camTop.text = camBottom.text = "";
                    break;
            }
        }

        void BuildDawn()
        {
            dawn = Panel(root, "Dawn");
            var t = Label(dawn.transform, Loc.T("dawn"), 96, new Vector2(0.5f, 0.92f), Vector2.zero, new Color(1f, 0.85f, 0.6f));
            t.rectTransform.sizeDelta = new Vector2(1000, 130);
            t.fontStyle = FontStyle.Bold;
            dawnPhoto = Rect(dawn.transform, "Photo", new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f)).gameObject.AddComponent<RawImage>();
            dawnPhoto.rectTransform.sizeDelta = new Vector2(200, 420);
            dawnPhoto.raycastTarget = false;
            dawnText = Label(dawn.transform, "", 32, new Vector2(0.5f, 0.5f), Vector2.zero, Bone);
            dawnText.rectTransform.sizeDelta = new Vector2(940, 200);
            dawnSupply = Label(dawn.transform, "", 25, new Vector2(0.5f, 0.405f), Vector2.zero, Dim);
            dawnSupply.rectTransform.sizeDelta = new Vector2(940, 130);
            dawnXp = Label(dawn.transform, "", 30, new Vector2(0.5f, 0.31f), Vector2.zero, new Color(1f, 0.75f, 0.45f));
            dawnXp.rectTransform.sizeDelta = new Vector2(940, 170);
            dawnXp.fontStyle = FontStyle.Bold;
            readButton = Button(dawn.transform, Loc.T("read"), new Vector2(0.5f, 0.215f), () => OpenStory(dawn, Progress.StoryUnlocked), false, 400);
            shareButton = Button(dawn.transform, Loc.T("share_photo"), new Vector2(0.5f, 0.142f), () => SharePressed?.Invoke(), false, 330, -175);
            shareButton.GetComponentInChildren<Text>().fontSize = 30;
            bonusButton = Button(dawn.transform, Loc.T("bonus_supply"), new Vector2(0.5f, 0.142f), () => BonusSupplyPressed?.Invoke(), true, 330, 175);
            bonusButton.GetComponentInChildren<Text>().fontSize = 30;
            nextNightButton = Button(dawn.transform, Loc.T("next_night"), new Vector2(0.5f, 0.065f), () => NextNightPressed?.Invoke(), true, 470, 120);
            Button(dawn.transform, Loc.T("menu"), new Vector2(0.5f, 0.065f), () => MenuPressed?.Invoke(), false, 220, -255);
        }

        void BuildOver()
        {
            over = Panel(root, "GameOver");
            var t = Label(over.transform, Loc.T("taken"), 96, new Vector2(0.5f, 0.64f), Vector2.zero, new Color(0.8f, 0.15f, 0.15f));
            t.rectTransform.sizeDelta = new Vector2(1000, 140);
            t.fontStyle = FontStyle.Bold;
            overXp = Label(over.transform, "", 30, new Vector2(0.5f, 0.565f), Vector2.zero, new Color(1f, 0.75f, 0.45f));
            overXp.rectTransform.sizeDelta = new Vector2(940, 90);
            overText = Label(over.transform, "", 40, new Vector2(0.5f, 0.48f), Vector2.zero, Bone);
            overText.rectTransform.sizeDelta = new Vector2(1000, 200);
            secondChance = Button(over.transform, Loc.T("second_chance"), new Vector2(0.5f, 0.39f), () => SecondChancePressed?.Invoke(), true);
            Button(over.transform, Loc.T("again"), new Vector2(0.5f, 0.3f), () => StartPressed?.Invoke(), false);
            Button(over.transform, Loc.T("menu"), new Vector2(0.5f, 0.21f), () => MenuPressed?.Invoke(), false);
        }

        // Дневник — энциклопедия: список сущностей, статья о каждой, страница предметов и жестов.
        void BuildJournal()
        {
            journal = Panel(root, "Journal");
            journal.GetComponent<Image>().color = new Color(0.02f, 0.015f, 0.02f, 0.97f);
            BuildJournalList();
            BuildJournalEntry();
            BuildJournalItems();
            BuildJournalClues();
            BuildJournalHowTo();
            BuildJournalTools();
            BuildJournalPractice();
            BuildJournalLog();
            journal.SetActive(false);
        }

        void BuildJournalList()
        {
            journalList = Rect(journal.transform, "List", Vector2.zero, Vector2.one).gameObject;
            var title = Label(journalList.transform, Loc.T("journal"), 72, new Vector2(0.5f, 0.93f), Vector2.zero, Bone);
            title.rectTransform.sizeDelta = new Vector2(900, 110);
            title.fontStyle = FontStyle.Bold;

            journalCount = Label(journalList.transform, "", 30, new Vector2(0.5f, 0.93f), new Vector2(0, -72), Dim);
            journalCount.rectTransform.sizeDelta = new Vector2(900, 50);

            // список прокручивается: сущностей много, открываются постепенно
            var view = Rect(journalList.transform, "View", new Vector2(0f, 0.175f), new Vector2(1f, 1f));
            view.offsetMax = new Vector2(0, -250);
            // маска по трафарету; прозрачная подложка ловит перетаскивание и между карточками
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            view.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = Rect(view, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
            content.pivot = new Vector2(0.5f, 1f);
            const int cols = 3, cw = 296, ch = 330, gap = 14;
            int rows = (Catalog.Entities.Length + cols - 1) / cols;
            content.sizeDelta = new Vector2(0, rows * (ch + gap) + gap);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
            journalScroll = scroll;

            for (int i = 0; i < Catalog.Entities.Length; i++)
            {
                var def = Catalog.Entities[i];
                int col = i % cols, row = i / cols;
                var card = Rect(content, def.Kind.ToString(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                card.sizeDelta = new Vector2(cw, ch);
                card.anchoredPosition = new Vector2((col - 1) * (cw + gap), -gap - ch * 0.5f - row * (ch + gap));
                var bg = card.gameObject.AddComponent<Image>();
                bg.color = Ink;
                var btn = card.gameObject.AddComponent<UnityEngine.UI.Button>();
                btn.targetGraphic = bg;
                var kind = def.Kind;
                btn.onClick.AddListener(() => OpenEntry(kind));
                Frame(card, cw, ch, Dim, 3f);

                var pic = Rect(card, "Picture", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f)).gameObject.AddComponent<RawImage>();
                pic.rectTransform.sizeDelta = new Vector2(200, 245);
                pic.rectTransform.anchoredPosition = new Vector2(0, -135);
                pic.raycastTarget = false;
                pic.enabled = false;   // превью подгружается, когда список открыт (FillThumbs)

                var name = Label(card, "", 28, new Vector2(0.5f, 0f), new Vector2(0, 40), Bone);
                name.rectTransform.sizeDelta = new Vector2(cw - 20, 70);
                name.fontStyle = FontStyle.Bold;
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 18;
                name.resizeTextMaxSize = 28;
                cards.Add((kind, pic, name));
            }

            Button(journalList.transform, Loc.T("back"), new Vector2(0.5f, 0.06f), () => ShowJournalPage(journalClues), false);
            listEmpty = Label(journalList.transform, Loc.T("list_empty"), 30, new Vector2(0.5f, 0.6f), Vector2.zero, Dim);
            listEmpty.rectTransform.sizeDelta = new Vector2(860, 200);
            listContent = content;
        }

        RectTransform listContent;

        // В списке — только распознанные сущности (в тестовой сборке — все): остальные скрыты целиком,
        // без силуэтов и счётчика «из 40» — сколько их всего, игрок не знает.
        // showAll — только для тестовой кнопки «ТЕСТ: ВСЕ» (выбрать, кто придёт); у игрока список — только распознанные
        void RefreshCards(bool showAll)
        {
            const int cols = 3, cw = 296, ch = 330, gap = 14;
            int shown = 0;
            foreach (var c in cards)
            {
                bool visible = showAll || Know.Name(c.kind);
                var card = (RectTransform)c.pic.transform.parent;
                card.gameObject.SetActive(visible);
                if (!visible) continue;
                int col = shown % cols, row = shown / cols;
                card.anchoredPosition = new Vector2((col - 1) * (cw + gap), -gap - ch * 0.5f - row * (ch + gap));
                bool known = Know.Name(c.kind);
                c.name.text = known ? Catalog.Get(c.kind).Name : "???";
                c.name.color = Catalog.Seen(c.kind) ? Bone : Dim;
                shown++;
            }
            int rows = (shown + cols - 1) / cols;
            listContent.sizeDelta = new Vector2(0, rows * (ch + gap) + gap);
            listEmpty.gameObject.SetActive(shown == 0);
            int found = 0;
            foreach (var c in cards) if (Know.Name(c.kind)) found++;
            journalCount.text = showAll ? "ТЕСТ: все сущности" : Loc.F("journal_found", found);
        }

        void OpenList(bool showAll)
        {
            RefreshCards(showAll);   // до показа: превью подгружаются только у видимых карточек
            ShowJournalPage(journalList);
        }

        // Превью сущности для списка: шейдер рисуется один раз в текстуру (фон прозрачный) и запоминается.
        // Живых шейдеров в списке больше нет (их было 40 — по шейдеру на карточку каждый кадр).
        RenderTexture Thumb(EntityDef def, bool locked)
        {
            if (thumbs.TryGetValue((def.Kind, locked), out var rt) && rt != null) return rt;
            rt = new RenderTexture(200, 245, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Thumb " + def.Kind };   // sRGB — цвета как у живого шейдера
            var mat = Entity.CreateMaterial(def, 1.7f + (int)def.Kind);
            mat.SetFloat("_Locked", locked ? 1f : 0f);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.clear);   // прозрачный фон — под картинкой видна сама карточка
            RenderTexture.active = prev;
            // у картинок шейдер читает _MainTex — её Blit и передаёт; процедурному силуэту источник не нужен
            Graphics.Blit(Entity.Picture(def.Kind) ?? (Texture)Texture2D.blackTexture, rt, mat);
            if (Application.isPlaying) Destroy(mat); else DestroyImmediate(mat);
            thumbs[(def.Kind, locked)] = rt;
            return rt;
        }

        // подгружаем превью постепенно — по две карточки за кадр, сверху вниз
        System.Collections.IEnumerator FillThumbs()
        {
            int done = 0;
            foreach (var c in cards)
            {
                if (!c.pic.transform.parent.gameObject.activeSelf) continue;
                var def = Catalog.Get(c.kind);
                bool locked = !Catalog.Seen(c.kind);
                bool cached = thumbs.ContainsKey((c.kind, locked));
                c.pic.texture = Thumb(def, locked);
                c.pic.enabled = true;
                if (!cached && ++done % 2 == 0) yield return null;
            }
            thumbFill = null;
        }

        void BuildJournalEntry()
        {
            journalEntry = Rect(journal.transform, "Entry", Vector2.zero, Vector2.one).gameObject;
            entryPicture = Rect(journalEntry.transform, "Picture", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f)).gameObject.AddComponent<RawImage>();
            entryPicture.rectTransform.sizeDelta = new Vector2(420, 520);
            entryPicture.rectTransform.anchoredPosition = new Vector2(-250, -340);
            entryPicture.raycastTarget = false;

            entryName = Label(journalEntry.transform, "", 64, new Vector2(0.5f, 1f), new Vector2(230, -230), Bone);
            entryName.rectTransform.sizeDelta = new Vector2(500, 100);
            entryName.fontStyle = FontStyle.Bold;
            entryName.alignment = TextAnchor.MiddleLeft;
            entryOrigin = Label(journalEntry.transform, "", 28, new Vector2(0.5f, 1f), new Vector2(230, -300), Dim);
            entryOrigin.rectTransform.sizeDelta = new Vector2(500, 80);
            entryOrigin.alignment = TextAnchor.UpperLeft;

            // чем защищаться: иконка предмета, жест, остаток
            entryItemIcon = Rect(journalEntry.transform, "ItemIcon", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            entryItemIcon.anchoredPosition = new Vector2(80, -470);
            entryGestureIcon = Rect(journalEntry.transform, "GestureIcon", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            entryGestureIcon.anchoredPosition = new Vector2(380, -470);
            entryItem = Label(journalEntry.transform, "", 30, new Vector2(0.5f, 1f), new Vector2(230, -590), Bone);
            entryItem.rectTransform.sizeDelta = new Vector2(500, 90);
            entryCount = Label(journalEntry.transform, "", 26, new Vector2(0.5f, 1f), new Vector2(230, -640), Dim);
            entryCount.rectTransform.sizeDelta = new Vector2(500, 40);

            entryText = Label(journalEntry.transform, "", 29, new Vector2(0.5f, 1f), new Vector2(0, -690), Bone);
            entryText.rectTransform.pivot = new Vector2(0.5f, 1f);
            entryText.rectTransform.sizeDelta = new Vector2(940, 860);
            entryText.alignment = TextAnchor.UpperLeft;
            entryText.supportRichText = true;

            nameButton = Button(journalEntry.transform, Loc.T("this_is_it"), new Vector2(0.5f, 0.135f), () =>
            {
                SetJournal(false);
                NamePressed?.Invoke(openedEntry);
            }, true);
            Button(journalEntry.transform, Loc.T("back"), new Vector2(0.5f, 0.06f), () => ShowJournalPage(journalList), false);

            // только в тестовой сборке: выбрать, кто придёт следующей ночью
            if (TestMode.On)
            {
                var test = Button(journalEntry.transform, Loc.T("test_next"), new Vector2(0.5f, 0.21f), () =>
                {
                    PlayerPrefs.SetInt("test_next", (int)openedEntry);
                    PlayerPrefs.Save();
                    Message(Loc.F("test_next_set", Catalog.Get(openedEntry).Name), 2.5f);
                }, false, 520);
                test.GetComponentInChildren<Text>().fontSize = 30;
            }
        }

        // Улики: отмечай замеченное — ниже остаются только подходящие сущности.
        void BuildJournalClues()
        {
            journalClues = Rect(journal.transform, "Clues", Vector2.zero, Vector2.one).gameObject;
            var title = Label(journalClues.transform, Loc.T("clues"), 64, new Vector2(0.5f, 0.95f), Vector2.zero, Bone);
            title.rectTransform.sizeDelta = new Vector2(900, 100);
            title.fontStyle = FontStyle.Bold;
            var sub = Label(journalClues.transform, Loc.T("clues_sub"), 28, new Vector2(0.5f, 0.91f), Vector2.zero, Dim);
            sub.rectTransform.sizeDelta = new Vector2(900, 44);

            for (int i = 0; i < Catalog.Clues.Length; i++)
            {
                var def = Catalog.Clues[i];
                int index = i;
                var row = Rect(journalClues.transform, def.Name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                row.sizeDelta = new Vector2(1000, 124);
                row.anchoredPosition = new Vector2(0, -262 - i * 130);
                var bg = row.gameObject.AddComponent<Image>();
                bg.color = new Color(0, 0, 0, 0.01f);
                var btn = row.gameObject.AddComponent<UnityEngine.UI.Button>();
                btn.targetGraphic = bg;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => ToggleClue(index));

                var box = Rect(row, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<UILine>();
                box.rectTransform.anchoredPosition = new Vector2(-450, 0);
                box.Closed = true;
                box.Thickness = 4f;
                box.color = Bone;
                box.raycastTarget = false;
                box.SetPoints(new[] { new Vector2(-24, 24), new Vector2(24, 24), new Vector2(24, -24), new Vector2(-24, -24) });
                var check = Rect(row, "Check", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<UILine>();
                check.rectTransform.anchoredPosition = new Vector2(-450, 0);
                check.Thickness = 7f;
                check.color = new Color(1f, 0.75f, 0.45f);
                check.raycastTarget = false;
                check.SetPoints(new[] { new Vector2(-14, 0), new Vector2(-4, -12), new Vector2(16, 14) });
                check.gameObject.SetActive(false);

                var icon = Rect(row, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                icon.anchoredPosition = new Vector2(-350, 0);
                icon.localScale = Vector3.one * 0.7f;
                Icons.Clue(icon, def.Kind, Bone);

                var text = Label(row, $"<b>{def.Name}</b>\n{def.HowTo}", 27, new Vector2(0.5f, 0.5f), new Vector2(110, 0), Bone);
                text.rectTransform.sizeDelta = new Vector2(700, 122);
                text.alignment = TextAnchor.MiddleLeft;
                clueRows.Add((box, check));
            }

            suspectsText = Label(journalClues.transform, "", 28, new Vector2(0.5f, 1f), new Vector2(0, -1300), new Color(1f, 0.75f, 0.45f));
            suspectsText.rectTransform.sizeDelta = new Vector2(940, 120);
            // три отмеченные улики → «Определить»: сошлось — узнаёшь, кто это; нет — она злится
            identifyButton = Button(journalClues.transform, Loc.T("identify"), new Vector2(0.5f, 0.235f), () =>
            {
                var picked = new List<Evidence>();
                for (int i = 0; i < marks.Length; i++) if (marks[i]) picked.Add((Evidence)i);
                IdentifyPressed?.Invoke(picked.ToArray());
            }, true, 520);
            suspectsText.resizeTextForBestFit = true;
            suspectsText.resizeTextMinSize = 18;
            suspectsText.resizeTextMaxSize = 28;
            // улики — первая страница журнала: отсюда — список сущностей и остальные разделы
            Button(journalClues.transform, Loc.T("entities"), new Vector2(0.5f, 0.135f), () => OpenList(false), true, 300, -320);
            // тестовая сборка: полный список (выбрать, кто придёт следующей ночью) и сброс знаний — проверить игру с нуля
            if (TestMode.On)
            {
                var all = Button(journalClues.transform, "ТЕСТ: ВСЕ", new Vector2(0.5f, 0.95f), () => OpenList(true), false, 200, -340);
                all.GetComponentInChildren<Text>().fontSize = 24;
                var reset = Button(journalClues.transform, "ТЕСТ: СБРОС", new Vector2(0.5f, 0.95f), () =>
                {
                    Know.ResetAll();
                    RefreshCards(false);
                    Message("ТЕСТ: знания о сущностях стёрты — бестиарий пуст", 3f);
                }, false, 200, 340);
                reset.GetComponentInChildren<Text>().fontSize = 24;
            }
            Button(journalClues.transform, Loc.T("items"), new Vector2(0.5f, 0.135f), () => ShowJournalPage(journalItems), false, 300, 0);
            Button(journalClues.transform, Loc.T("tools"), new Vector2(0.5f, 0.135f), () => ShowJournalPage(journalTools), false, 300, 320);
            Button(journalClues.transform, Loc.T("howto"), new Vector2(0.5f, 0.06f), () => ShowJournalPage(journalHowTo), false, 300, -320);
            Button(journalClues.transform, Loc.T("night_log"), new Vector2(0.5f, 0.06f), () => ShowJournalPage(journalLog), false, 300, 0);
            Button(journalClues.transform, Loc.T("close"), new Vector2(0.5f, 0.06f), () => SetJournal(false), false, 300, 320);
            UpdateIdentify();
        }

        // Приборы поиска и средства защиты.
        void BuildJournalTools()
        {
            journalTools = Rect(journal.transform, "Tools", Vector2.zero, Vector2.one).gameObject;
            var title = Label(journalTools.transform, Loc.T("tools"), 64, new Vector2(0.5f, 0.95f), Vector2.zero, Bone);
            title.rectTransform.sizeDelta = new Vector2(900, 100);
            title.fontStyle = FontStyle.Bold;

            int row = 0;
            foreach (var t in new[] { Tool.Emf, Tool.Thermo, Tool.Night, Tool.Radio })
                ToolRow(row++, r => Icons.ToolIcon(r, t, Bone), Loc.T($"t.{t}.desc"));
            var bagTitle = Label(journalTools.transform, Loc.T("bag"), 40, new Vector2(0.5f, 1f), new Vector2(0, -240 - row * 150 + 30), Bone);
            bagTitle.rectTransform.sizeDelta = new Vector2(900, 60);
            bagTitle.fontStyle = FontStyle.Bold;
            row++;
            foreach (var k in new[] { Protect.Circle, Protect.Candle, Protect.Tumar, Protect.Calm })
                ToolRow(row++, r => Icons.ProtectIcon(r, k, Bone), $"<b>{Loc.T($"p.{k}.name")}.</b> {Loc.T($"p.{k}.desc")}");
            Button(journalTools.transform, Loc.T("back"), new Vector2(0.5f, 0.06f), () => ShowJournalPage(journalClues), false);
        }

        void ToolRow(int row, Action<RectTransform> icon, string text)
        {
            float y = -230 - row * 140;
            var ic = Rect(journalTools.transform, "Icon", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            ic.anchoredPosition = new Vector2(-430, y);
            ic.localScale = Vector3.one * 0.8f;
            icon(ic);
            var l = Label(journalTools.transform, text, 27, new Vector2(0.5f, 1f), new Vector2(70, y), Bone);
            l.rectTransform.sizeDelta = new Vector2(780, 130);
            l.alignment = TextAnchor.MiddleLeft;
        }

        void ToggleClue(int i)
        {
            marks[i] = !marks[i];
            clueRows[i].check.gameObject.SetActive(marks[i]);
            idWrongText = null;
            UpdateIdentify();
        }

        // Подозреваемых дневник не подсказывает: только сколько улик отмечено и можно ли определять.
        string idWrongText;

        void UpdateIdentify()
        {
            if (suspectsText == null) return;
            int marked = 0;
            foreach (var m in marks) if (m) marked++;
            identifyButton.SetActive(canName && marked == 3);
            suspectsText.text = idWrongText ?? (!canName ? "" : marked == 3 ? Loc.T("id_ready")
                : marked > 3 ? Loc.T("id_too_many") : Loc.F("id_count", marked));
        }

        // не сошлось: сказать об этом (и сколько попыток осталось)
        public void IdentifyWrong(string text)
        {
            idWrongText = text;
            Shake(0.3f);
            UpdateIdentify();
        }

        // сошлось: статья о ней открывается целиком — прочесть, кто это и как защищаться
        public void ShowIdentified(EntityKind kind)
        {
            canName = false;
            idWrongText = null;
            UpdateIdentify();
            RefreshCards(false);
            OpenEntry(kind);
            Message(Loc.F("id_right", Catalog.Get(kind).Name), 4f);
        }

        // шаг пролога; пустой текст — спрятать
        public void SetInstruction(string text, int step, int total)
        {
            instruction.SetActive(!string.IsNullOrEmpty(text));
            instructionText.text = text;
            instructionStep.text = total > 0 ? Loc.F("step_n", step, total) : "";
        }

        public void SetNightTitle(string title) => nightText.text = title;

        public bool IsMarked(Evidence e) => marks[(int)e];

        public void Narrate(string text)
        {
            narrateText.text = text;
            narrateTimer = 4f + text.Length * 0.04f;   // длинное — дольше
            Log(text, false);
        }

        public void Hint(string text)
        {
            hintText.text = text;
            hintTimer = 6f + text.Length * 0.05f;
            Log(text, true);
        }

        // запись в журнал ночи: время по часам ночи, подсказки — янтарным
        void Log(string text, bool hint)
        {
            if (string.IsNullOrEmpty(text)) return;
            string line = text.Replace("\n\n", " ").Replace("\n", " ");
            string time = hud.activeSelf ? clockText.text + "  " : "";
            string entry = hint ? $"{time}<color=#ffc073>{line}</color>" : time + line;
            if (nightLog.Count > 0 && nightLog[^1] == entry) return;
            nightLog.Add(entry);
            if (nightLog.Count > 80) nightLog.RemoveAt(0);
        }

        // разделитель между ночами
        public void LogNight(string title)
        {
            nightLog.Add($"<b>— {title} —</b>");
            if (nightLog.Count > 80) nightLog.RemoveAt(0);
        }

        public void TrophyToast(string text)
        {
            trophyText.text = text;
            trophyTimer = 4f;
        }

        public void MarkClue(int i)
        {
            if (i >= 0 && i < marks.Length && !marks[i]) ToggleClue(i);
        }

        // список званий пересобирается при открытии — опыт мог вырасти
        void OpenRanks()
        {
            if (trophies != null) trophies.SetActive(false);
            Kill(ranks);
            BuildRanks();
            ShowOnly(ranks);
        }

        // для превью из редактора
        public void DebugPanel(string name)
        {
            switch (name)
            {
                case "ranks": OpenRanks(); break;
                case "trophies": OpenRanks(); OpenTrophies(); break;
                case "story": OpenStory(menu, 1); break;
                case "story19": OpenStory(menu, 19); break;
                case "dawn":
                    ShowDawn(3, Loc.F("verdict_banished", Catalog.Get(EntityKind.Albasty).Name) + "\n" + Loc.T("verdict_named_right"),
                        Inventory.Starting().Resupply(), null);
                    ShowNightProgress(410, true, true);
                    break;
            }
        }

        // Трофеи: выполненные — яркие, остальные — тусклые; за каждый — опыт.
        void OpenTrophies()
        {
            if (trophies != null) Kill(trophies);
            ranks.SetActive(false);
            trophies = Panel(root, "Trophies");
            trophies.GetComponent<Image>().color = new Color(0.01f, 0.008f, 0.012f, 0.99f);
            var title = Label(trophies.transform, Loc.T("trophies"), 64, new Vector2(0.5f, 0.95f), Vector2.zero, Bone);
            title.rectTransform.sizeDelta = new Vector2(900, 100);
            title.fontStyle = FontStyle.Bold;
            var all = (Trophy[])Enum.GetValues(typeof(Trophy));
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                bool got = Trophies.Has(t);
                float y = -220 - i * 135;
                var mark = Rect(trophies.transform, "Mark", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f)).gameObject.AddComponent<UILine>();
                mark.rectTransform.anchoredPosition = new Vector2(-450, y - 10);
                mark.Filled = true;
                mark.color = got ? new Color(1f, 0.75f, 0.4f) : new Color(1f, 1f, 1f, 0.12f);
                mark.raycastTarget = false;
                mark.SetPoints(new[] { new Vector2(0, 22), new Vector2(22, 0), new Vector2(0, -22), new Vector2(-22, 0) });
                var name = Label(trophies.transform, Trophies.Name(t), 34, new Vector2(0.5f, 1f), new Vector2(40, y + 12), got ? Bone : Dim);
                name.rectTransform.sizeDelta = new Vector2(860, 50);
                name.alignment = TextAnchor.MiddleLeft;
                name.fontStyle = FontStyle.Bold;
                var desc = Label(trophies.transform, Trophies.Desc(t) + $"  +{Trophies.Reward} XP", 26, new Vector2(0.5f, 1f), new Vector2(40, y - 34), Dim);
                desc.rectTransform.sizeDelta = new Vector2(860, 44);
                desc.alignment = TextAnchor.MiddleLeft;
            }
            Button(trophies.transform, Loc.T("back"), new Vector2(0.5f, 0.06f), () => { trophies.SetActive(false); ShowOnly(ranks); }, false);
            trophies.transform.SetAsLastSibling();
            trophies.SetActive(true);
        }

        // Звания: порог опыта и бонус каждого.
        void BuildRanks()
        {
            ranks = Panel(root, "Ranks");
            ranks.GetComponent<Image>().color = new Color(0.01f, 0.008f, 0.012f, 0.98f);
            var title = Label(ranks.transform, Loc.T("ranks"), 64, new Vector2(0.5f, 0.95f), Vector2.zero, Bone);
            title.rectTransform.sizeDelta = new Vector2(900, 100);
            title.fontStyle = FontStyle.Bold;
            int current = Progress.Rank;
            for (int i = 0; i < Progress.RankXp.Length; i++)
            {
                bool reached = i <= current;
                float y = -230 - i * 185;
                var name = Label(ranks.transform, $"{Progress.RankName(i)}", 38, new Vector2(0.5f, 1f), new Vector2(-60, y), reached ? Bone : Dim);
                name.rectTransform.sizeDelta = new Vector2(820, 56);
                name.alignment = TextAnchor.MiddleLeft;
                name.fontStyle = FontStyle.Bold;
                var xp = Label(ranks.transform, Progress.RankXp[i] + " XP", 28, new Vector2(0.5f, 1f), new Vector2(380, y), reached ? new Color(1f, 0.75f, 0.45f) : Dim);
                xp.rectTransform.sizeDelta = new Vector2(200, 56);
                xp.alignment = TextAnchor.MiddleRight;
                var perk = Label(ranks.transform, Progress.Perk(i), 28, new Vector2(0.5f, 1f), new Vector2(0, y - 60), reached ? new Color(Bone.r, Bone.g, Bone.b, 0.8f) : Dim);
                perk.rectTransform.sizeDelta = new Vector2(940, 70);
                perk.alignment = TextAnchor.UpperLeft;
            }
            Button(ranks.transform, Loc.T("trophies"), new Vector2(0.5f, 0.06f), OpenTrophies, true, 310, -165);
            Button(ranks.transform, Loc.T("back"), new Vector2(0.5f, 0.06f), () => ShowMenu(), false, 310, 165);
        }

        // История дома: страницы дневника прежних жильцов, по одной за пережитую ночь.
        void BuildStory()
        {
            story = Panel(root, "Story");
            story.GetComponent<Image>().color = new Color(0.04f, 0.035f, 0.03f, 0.99f);
            var title = Label(story.transform, Loc.T("story"), 64, new Vector2(0.5f, 0.93f), Vector2.zero, Bone);
            title.rectTransform.sizeDelta = new Vector2(900, 100);
            title.fontStyle = FontStyle.Bold;
            storyPageLabel = Label(story.transform, "", 28, new Vector2(0.5f, 0.87f), Vector2.zero, Dim);
            storyPageLabel.rectTransform.sizeDelta = new Vector2(600, 50);
            Frame(Rect(story.transform, "Paper", new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f)), 920, 1000, Dim, 3f);
            storyBody = Label(story.transform, "", 40, new Vector2(0.5f, 0.55f), Vector2.zero, new Color(0.9f, 0.86f, 0.78f));
            storyBody.rectTransform.sizeDelta = new Vector2(840, 920);
            storyBody.fontStyle = FontStyle.Italic;
            Button(story.transform, Loc.T("prev"), new Vector2(0.5f, 0.15f), () => StoryStep(-1), false, 200, -300);
            Button(story.transform, Loc.T("next_page"), new Vector2(0.5f, 0.15f), () => StoryStep(1), false, 200, 300);
            Button(story.transform, Loc.T("back"), new Vector2(0.5f, 0.06f), () =>
            {
                if (storyReturn == menu) ShowMenu();
                else ShowOnly(storyReturn);
            }, false);
        }

        void OpenStory(GameObject returnTo, int page)
        {
            storyReturn = returnTo;
            storyPage = Mathf.Clamp(page, 1, Progress.StoryPages);
            ShowOnly(story);
            ShowStoryPage();
        }

        void StoryStep(int delta)
        {
            storyPage = Mathf.Clamp(storyPage + delta, 1, Progress.StoryPages);
            ShowStoryPage();
        }

        void ShowStoryPage()
        {
            storyPageLabel.text = Loc.F("page_n", storyPage, Progress.StoryPages);
            if (storyPage > Progress.StoryNightPages)
                storyBody.text = Progress.EpilogueUnlocked ? Loc.T($"story_{storyPage}") : Loc.T("epilogue_locked");
            else
                storyBody.text = storyPage <= Progress.StoryUnlocked ? Loc.T($"story_{storyPage}") : Loc.T("story_locked");
        }

        public void ClearEvidence()
        {
            for (int i = 0; i < marks.Length; i++)
            {
                marks[i] = false;
                if (i < clueRows.Count) clueRows[i].check.gameObject.SetActive(false);
            }
            idWrongText = null;
            UpdateIdentify();
        }

        void BuildJournalHowTo()
        {
            journalHowTo = Rect(journal.transform, "HowTo", Vector2.zero, Vector2.one).gameObject;
            var title = Label(journalHowTo.transform, Loc.T("howto"), 64, new Vector2(0.5f, 0.95f), Vector2.zero, Bone);
            title.rectTransform.sizeDelta = new Vector2(900, 100);
            title.fontStyle = FontStyle.Bold;
            var body = new System.Text.StringBuilder();
            for (int i = 2; i <= 5; i++) body.Append($"<b>{Loc.T($"tut{i}_t")}</b>\n{Loc.T($"tut{i}_b")}\n\n");
            var text = Label(journalHowTo.transform, body.ToString(),
                30, new Vector2(0.5f, 1f), new Vector2(0, -200), Bone);
            text.rectTransform.pivot = new Vector2(0.5f, 1f);
            text.rectTransform.sizeDelta = new Vector2(940, 1400);
            text.fontSize = 28;
            text.alignment = TextAnchor.UpperLeft;
            Button(journalHowTo.transform, Loc.T("back"), new Vector2(0.5f, 0.06f), () => ShowJournalPage(journalClues), false);
        }

        void BuildJournalItems()
        {
            journalItems = Rect(journal.transform, "Items", Vector2.zero, Vector2.one).gameObject;
            var title = Label(journalItems.transform, Loc.T("items"), 64, new Vector2(0.5f, 0.94f), Vector2.zero, Bone);
            title.rectTransform.sizeDelta = new Vector2(900, 100);
            title.fontStyle = FontStyle.Bold;

            for (int i = 0; i < Catalog.Items.Length; i++)
            {
                var def = Catalog.Items[i];
                float y = -290 - i * 238;
                var icon = Rect(journalItems.transform, "Icon", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                icon.anchoredPosition = new Vector2(-420, y);
                icon.localScale = Vector3.one * 0.9f;
                Icons.Item(icon, def.Kind, Bone);
                var gesture = Rect(journalItems.transform, "Gesture", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                gesture.anchoredPosition = new Vector2(-280, y);
                gesture.localScale = Vector3.one * 0.85f;
                Icons.GestureIcon(gesture, def.Gesture, new Color(1f, 0.75f, 0.45f));

                var name = Label(journalItems.transform, def.Name.ToUpper(), 36, new Vector2(0.5f, 1f), new Vector2(110, y + 60), Bone);
                name.rectTransform.sizeDelta = new Vector2(600, 50);
                name.alignment = TextAnchor.MiddleLeft;
                name.fontStyle = FontStyle.Bold;
                var body = Label(journalItems.transform, $"<color=#ffc073>{def.Usage}</color>\n{def.Lore}", 28,
                    new Vector2(0.5f, 1f), new Vector2(50, y - 20), Dim);
                body.rectTransform.sizeDelta = new Vector2(480, 110);
                body.alignment = TextAnchor.UpperLeft;
                body.color = new Color(Bone.r, Bone.g, Bone.b, 0.75f);
                var count = Label(journalItems.transform, "", 34, new Vector2(0.5f, 1f), new Vector2(410, y + 60), Bone);
                count.rectTransform.sizeDelta = new Vector2(120, 50);
                count.alignment = TextAnchor.MiddleRight;
                itemRows.Add((def.Kind, count));

                // попробовать жест
                var kind = def.Kind;
                var tb = Rect(journalItems.transform, "Practice", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                tb.sizeDelta = new Vector2(170, 74);
                tb.anchoredPosition = new Vector2(390, y - 40);
                var tbg = tb.gameObject.AddComponent<Image>();
                tbg.color = Ink;
                var tbtn = tb.gameObject.AddComponent<UnityEngine.UI.Button>();
                tbtn.targetGraphic = tbg;
                tbtn.onClick.AddListener(() => OpenPractice(kind));
                Frame(tb, 170, 74, Dim, 3f);
                var tl = Label(tb, "", 26, new Vector2(0.5f, 0.5f), Vector2.zero, Bone);
                tl.rectTransform.sizeDelta = new Vector2(160, 70);
                tl.fontStyle = FontStyle.Bold;
                tl.resizeTextForBestFit = true;
                tl.resizeTextMinSize = 16;
                tl.resizeTextMaxSize = 26;
                practiceButtons.Add((kind, tbg, tl));
            }

            learnedCount = Label(journalItems.transform, "", 28, new Vector2(0.5f, 0.94f), new Vector2(0, -58), Dim);
            learnedCount.rectTransform.sizeDelta = new Vector2(900, 50);

            Button(journalItems.transform, Loc.T("back"), new Vector2(0.5f, 0.06f), () => ShowJournalPage(journalClues), false);
        }

        // Тренировка: на экране сущность, которая боится предмета; верный жест — она тает, жест выучен.
        void BuildJournalPractice()
        {
            journalPractice = Rect(journal.transform, "Practice", Vector2.zero, Vector2.one).gameObject;
            practiceTitle = Label(journalPractice.transform, "", 58, new Vector2(0.5f, 0.93f), Vector2.zero, Bone);
            practiceTitle.rectTransform.sizeDelta = new Vector2(1000, 100);
            practiceTitle.fontStyle = FontStyle.Bold;
            practiceTarget = Rect(journalPractice.transform, "Target", new Vector2(0.5f, 0.64f), new Vector2(0.5f, 0.64f)).gameObject.AddComponent<RawImage>();
            practiceTarget.rectTransform.sizeDelta = new Vector2(400, 500);
            practiceTarget.raycastTarget = false;
            practiceGesture = Rect(journalPractice.transform, "Gesture", new Vector2(0.5f, 0.37f), new Vector2(0.5f, 0.37f));
            practiceGesture.localScale = Vector3.one * 1.5f;
            practiceText = Label(journalPractice.transform, "", 44, new Vector2(0.5f, 0.27f), Vector2.zero, Warm);
            practiceText.rectTransform.sizeDelta = new Vector2(960, 110);
            practiceText.fontStyle = FontStyle.Bold;
            practiceStatus = Label(journalPractice.transform, "", 34, new Vector2(0.5f, 0.19f), Vector2.zero, Bone);
            practiceStatus.rectTransform.sizeDelta = new Vector2(960, 110);
            practiceBack = Button(journalPractice.transform, Loc.T("back"), new Vector2(0.5f, 0.06f), () => ShowJournalPage(journalItems), false)
                .GetComponent<RectTransform>();
            journalPractice.SetActive(false);

            uiAudio = gameObject.AddComponent<AudioSource>();
            uiAudio.ignoreListenerPause = true;   // в дневнике игра на паузе, а звук тренировки нужен
            uiAudio.playOnAwake = false;
        }

        void OpenPractice(ItemKind kind)
        {
            var def = Catalog.Get(kind);
            practiceItem = kind;
            practiceDone = -1f;
            practiceHold = 0f;
            practiceGestures.Reset();
            practiceTitle.text = Loc.F("practice_title", def.Name.ToUpper());
            Clear(practiceGesture);
            Icons.GestureIcon(practiceGesture, def.Gesture, Warm);
            practiceText.text = def.Usage;
            practiceStatus.text = Loc.T("practice_try");
            practiceStatus.color = Dim;

            // мишень — первая сущность, которая этого боится
            var target = Catalog.Entities[0];
            foreach (var e in Catalog.Entities) if (e.Bane == kind) { target = e; break; }
            if (practiceMat != null) Destroy(practiceMat);
            practiceMat = Entity.CreateMaterial(target, 5.5f);
            practiceMat.SetFloat("_Locked", 1f);   // облик не раскрываем — просто силуэт
            practiceTarget.material = practiceMat;
            ShowJournalPage(journalPractice);
        }

        void TickPractice(float dt)
        {
            if (practiceDone >= 0f)
            {
                practiceDone += dt;
                practiceMat.SetFloat("_Dissolve", Mathf.Clamp01(practiceDone / 0.9f));
                if (practiceDone > 1.9f) { RefreshLearned(); ShowJournalPage(journalItems); }
                return;
            }

            var g = practiceGestures.Tick(dt, Time.unscaledTime,
                pos => RectTransformUtility.RectangleContainsScreenPoint(practiceBack, pos, null));

            if (practiceItem == ItemKind.Lantern)
            {
                if (practiceGestures.Holding)
                {
                    practiceHold += dt;
                    practiceMat.SetFloat("_Flash", 0.25f + 0.25f * Mathf.Sin(Time.unscaledTime * 40f));
                    practiceStatus.text = Loc.T("practice_hold");
                    practiceStatus.color = Warm;
                    if (practiceHold > 1.2f) PracticeSucceeded();
                    return;
                }
                practiceHold = 0f;
                practiceMat.SetFloat("_Flash", 0f);
            }

            if (!g.HasValue) return;
            if (g.Value == practiceItem) { PracticeSucceeded(); return; }
            practiceStatus.text = Loc.F("practice_wrong", Catalog.Get(g.Value).Name);
            practiceStatus.color = new Color(1f, 0.45f, 0.4f);
            uiAudio.PlayOneShot(ProceduralAudio.Fail(), 0.6f * Settings.Sfx);
        }

        void PracticeSucceeded()
        {
            practiceDone = 0f;
            practiceMat.SetFloat("_Flash", 0.6f);
            Skills.Learn(practiceItem);
            practiceStatus.text = Loc.F("practice_done", Catalog.Get(practiceItem).Name);
            practiceStatus.color = Warm;
            var clip = practiceItem switch
            {
                ItemKind.Salt => ProceduralAudio.SaltThrow(),
                ItemKind.Iron => ProceduralAudio.IronStrike(),
                ItemKind.Bell => ProceduralAudio.BellRing(),
                ItemKind.Mirror => ProceduralAudio.Shimmer(),
                ItemKind.Wormwood => ProceduralAudio.Whoosh(),
                _ => ProceduralAudio.Banish()
            };
            uiAudio.PlayOneShot(clip, Settings.Sfx);
            uiAudio.PlayOneShot(ProceduralAudio.Banish(), 0.5f * Settings.Sfx);
        }

        void RefreshLearned()
        {
            RefreshMenuItems();
            foreach (var (kind, bg, label) in practiceButtons)
            {
                bool learned = Skills.Learned(kind);
                label.text = Loc.T(learned ? "learned" : "practice");
                label.color = learned ? Warm : Bone;
                bg.color = learned ? Ink : Blood;
            }
            learnedCount.text = Loc.F("learned_count", Skills.Count, Catalog.Items.Length);
        }

        void BuildJournalLog()
        {
            journalLog = Rect(journal.transform, "Log", Vector2.zero, Vector2.one).gameObject;
            var title = Label(journalLog.transform, Loc.T("night_log"), 64, new Vector2(0.5f, 0.94f), Vector2.zero, Bone);
            title.rectTransform.sizeDelta = new Vector2(900, 100);
            title.fontStyle = FontStyle.Bold;
            var view = Rect(journalLog.transform, "View", new Vector2(0f, 0.12f), new Vector2(1f, 1f));
            view.offsetMin = new Vector2(60, 0);
            view.offsetMax = new Vector2(-60, -200);
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            view.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            logContent = Rect(view, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
            logContent.pivot = new Vector2(0.5f, 1f);
            logText = Label(logContent, "", 30, new Vector2(0.5f, 1f), Vector2.zero, Bone);
            logText.rectTransform.anchorMin = new Vector2(0f, 1f);
            logText.rectTransform.anchorMax = new Vector2(1f, 1f);
            logText.rectTransform.pivot = new Vector2(0.5f, 1f);
            logText.rectTransform.offsetMin = logText.rectTransform.offsetMax = Vector2.zero;
            logText.alignment = TextAnchor.UpperLeft;
            logText.supportRichText = true;
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.verticalOverflow = VerticalWrapMode.Overflow;
            logText.lineSpacing = 1.1f;
            logScroll = view.gameObject.AddComponent<ScrollRect>();
            logScroll.content = logContent;
            logScroll.horizontal = false;
            logScroll.scrollSensitivity = 40f;
            Button(journalLog.transform, Loc.T("back"), new Vector2(0.5f, 0.06f), () => ShowJournalPage(journalClues), false);
        }

        void RefreshLog()
        {
            // новое — сверху
            var sb = new System.Text.StringBuilder();
            for (int i = nightLog.Count - 1; i >= 0; i--) sb.Append(nightLog[i]).Append("\n\n");
            logText.text = nightLog.Count == 0 ? $"<i>{Loc.T("log_empty")}</i>" : sb.ToString();
            Canvas.ForceUpdateCanvases();
            float h = logText.preferredHeight + 40f;
            logText.rectTransform.sizeDelta = new Vector2(0f, h);
            logContent.sizeDelta = new Vector2(0f, h);
            logScroll.verticalNormalizedPosition = 1f;
        }

        void ShowJournalPage(GameObject page)
        {
            journalLog.SetActive(page == journalLog);
            if (page == journalLog) RefreshLog();
            journalPractice.SetActive(page == journalPractice);
            if (page == journalItems) RefreshLearned();
            journalList.SetActive(page == journalList);
            if (page == journalList && thumbFill == null && Application.isPlaying) thumbFill = StartCoroutine(FillThumbs());
            journalEntry.SetActive(page == journalEntry);
            journalItems.SetActive(page == journalItems);
            journalClues.SetActive(page == journalClues);
            journalHowTo.SetActive(page == journalHowTo);
            journalTools.SetActive(page == journalTools);
        }

        void OpenEntry(EntityKind kind)
        {
            var def = Catalog.Get(kind);
            var item = Catalog.Get(def.Bane);
            bool seen = Catalog.Seen(kind);
            bool known = Know.Name(kind);
            bool defense = Know.Defense(kind);
            openedEntry = kind;

            if (entryMat != null) Destroy(entryMat);
            entryMat = Entity.CreateMaterial(def, 3.3f);
            entryMat.SetFloat("_Locked", seen ? 0f : 1f);
            entryPicture.material = entryMat;

            Clear(entryItemIcon);
            Clear(entryGestureIcon);
            nameButton.SetActive(false);   // имя определяется по уликам на странице улик

            if (!known)
            {
                // ещё не встречалась: ничего, кроме того, когда она появится
                entryName.text = "???";
                entryOrigin.text = "";
                entryText.text = $"<i>{Loc.T("unknown_entity")}</i>" +
                    (Know.Unlocked(kind) ? "" : "\n\n" + Loc.F("unlock_xp", Know.XpFor(kind))) + TestInfo(def);
                entryItem.text = entryCount.text = "";
                ShowJournalPage(journalEntry);
                return;
            }

            var clues = new List<string>();
            foreach (var c in def.Clues) clues.Add(Know.Clue(kind, c) ? Catalog.Get(c).Name : "?");
            string Locked(string key) => $"<color=#8a8378><i>{Loc.T(key)}</i></color>";
            entryName.text = def.Name.ToUpper();
            entryOrigin.text = def.Origin;
            entryText.text =
                $"{def.Lore}\n\n" +
                $"<b>{Loc.T("clues_label")}</b> <color=#ffc073>{string.Join(" · ", clues)}</color>   " +
                $"<b>{Loc.T("lure_label")}</b> {(Know.Lure(kind) ? Loc.T($"l.{def.Lure}.name") : "?")}\n\n" +
                (Know.Habits(kind)
                    ? $"<b>{Loc.T("hides")}</b> {def.LairText}\n<b>{Loc.T("on_hunt")}</b> {def.Signs}\n\n"
                    : Locked("locked_habits") + "\n\n") +
                $"<b>{Loc.T("sec_tale")}</b> <i>{Loc.T($"e.{kind}.tale")}</i>\n\n" +
                (defense ? $"<b>{Loc.T("defense")}</b> {def.Defense}" : Locked("locked_defense")) + TestInfo(def);

            if (defense)
            {
                Icons.Item(entryItemIcon, item.Kind, Bone);
                Icons.GestureIcon(entryGestureIcon, item.Gesture, new Color(1f, 0.75f, 0.45f));
                entryItem.text = $"{item.Name} — {item.Usage.ToLower()}";
                entryCount.text = shownInventory != null ? Loc.F("carry", shownInventory.Label(item.Kind)) : "";
            }
            else entryItem.text = entryCount.text = "";
            ShowJournalPage(journalEntry);
        }

        // тестовая сборка: всё о сущности сразу, даже если игрок её ещё не встречал
        static string TestInfo(EntityDef def)
        {
            if (!TestMode.On) return "";
            var item = Catalog.Get(def.Bane);
            var clues = new List<string>();
            foreach (var c in def.Clues) clues.Add(Catalog.Get(c).Name);
            return $"\n\n<size=24><color=#7fe0c0><b>ТЕСТ</b> {def.Kind} · рост {def.Height:0.0} м · {def.Stance} · логово {def.Lair}\n" +
                   $"<b>Улики:</b> {string.Join(" · ", clues)}\n" +
                   $"<b>Приманка:</b> {Loc.T($"l.{def.Lure}.name")}   <b>Изгнать:</b> {item.Name} — {item.Usage.ToLower()}\n" +
                   $"<b>Прячется:</b> {def.LairText}\n<b>Охота:</b> {def.Signs}\n<b>Защита:</b> {def.Defense}</color></size>";
        }

        // для превью из редактора: открыть дневник на нужной странице
        public void DebugJournal(string page, EntityKind entry = EntityKind.Ghost)
        {
            SetJournal(true);
            switch (page)
            {
                case "entry": OpenEntry(entry); break;
                case "items": ShowJournalPage(journalItems); break;
                case "clues": ToggleClue(0); ToggleClue(1); ShowJournalPage(journalClues); break;
                case "identify": CanName = true; ToggleClue(0); ToggleClue(1); ToggleClue(5); ShowJournalPage(journalClues); break;
                case "identify_wrong": CanName = true; ToggleClue(0); ToggleClue(2); ToggleClue(5); ShowJournalPage(journalClues);
                    IdentifyWrong(Loc.F("id_wrong", 2)); break;
                case "list_player": ShowJournalPage(journalList); RefreshCards(false); FillThumbsNow(); break;
                case "howto": ShowJournalPage(journalHowTo); break;
                case "practice": ShowJournalPage(journalItems); OpenPractice(ItemKind.Mirror); break;
                case "list": ShowJournalPage(journalList); FillThumbsNow(); break;
                case "end": ShowJournalPage(journalList); FillThumbsNow(); journalScroll.verticalNormalizedPosition = 0f; break;
                case "log":
                    LogNight(Loc.F("night_n", 3));
                    Narrate(Loc.F("n.Frost", Loc.T("dir_left")));
                    Hint(Loc.T("h.Frost"));
                    Message(Loc.T("circle_saved"));
                    Narrate(Loc.T("s.Kuchisake"));
                    ShowJournalPage(journalLog);
                    break;
            }
        }

        void FillThumbsNow()
        {
            var it = FillThumbs();
            while (it.MoveNext()) { }
        }

        // удаление панели: в игре — обычное, в превью из редактора — сразу
        static void Kill(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);   // превью из редактора
            }
        }

        void SetJournal(bool open)
        {
            journal.SetActive(open);
            if (open)
            {
                RefreshCards(false);
                journalScroll.verticalNormalizedPosition = 1f;
                ShowJournalPage(journalClues);
            }
            JournalToggled?.Invoke(open);
        }

        // ---------- экраны ----------

        public void ShowMenu()
        {
            ShowOnly(menu);
            RefreshMenuItems();
            RefreshRank();
        }

        void RefreshRank()
        {
            int r = Progress.Rank;
            rankName.text = $"{Loc.T("rank_" + r)}";
            rankFill.anchorMax = new Vector2(Progress.RankProgress, 1f);
            rankXp.text = Progress.XpLine() + (Daily.Streak > 0 ? "  ·  " + Loc.F("streak", Daily.Streak) : "");
        }
        public void ShowLoading() => ShowOnly(loading);
        public void ShowHud() => ShowOnly(hud);

        public void ShowJournal(Inventory inv)
        {
            shownInventory = inv;
            foreach (var (kind, count) in itemRows) count.text = inv != null ? inv.Label(kind) : "";
        }

        public void ShowDawn(int night, string verdict, string supply, Texture2D photo)
        {
            dawnText.text = Loc.F("dawn_night", night) + "\n" + verdict;
            dawnSupply.text = Loc.F("found", supply);
            dawnPhoto.texture = photo;
            dawnPhoto.gameObject.SetActive(photo != null);
            if (photo != null)
                dawnPhoto.rectTransform.sizeDelta = new Vector2(420f * photo.width / photo.height, 420f);
            shareButton.SetActive(photo != null);
            bonusButton.SetActive(true);
            nextNightButton.SetActive(true);
            readButton.SetActive(false);
            dawnXp.text = "";
            ShowOnly(dawn);
            ClearEffects();
        }

        public void ShowGameOver(int night, float progress, int best, string who, Texture2D photo)
        {
            overText.text = Loc.F("over_text", night, Clock(progress), who, best);
            ShowOnly(over);
            ClearEffects();
        }

        void ShowOnly(GameObject panel)
        {
            language.SetActive(panel == language);
            settings.SetActive(panel == settings);
            ranks.SetActive(panel == ranks);
            story.SetActive(panel == story);
            loading.SetActive(panel == loading);
            menu.SetActive(panel == menu);
            dawn.SetActive(panel == dawn);
            over.SetActive(panel == over);
            hud.SetActive(panel == hud);
            if (panel != hud) { scan.SetActive(false); strip.SetActive(false); }
        }

        void ClearEffects()
        {
            for (int i = 0; i < 4; i++) edgeTarget[i] = 0f;
            lanternOn = false;
        }

        // ---------- HUD ----------

        public void SetNight(int night) => nightText.text = Loc.F("night_n", night);
        public void SetClock(float progress) => clockText.text = Clock(progress);
        static string Clock(float progress)
        {
            int minutes = Mathf.FloorToInt(Mathf.Clamp01(progress) * 240f);
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }

        public void SetFear(float v)
        {
            fear = v;
            fearFill.anchorMax = new Vector2(Mathf.Clamp01(v), 1f);
        }

        // прогресс осмотра 0..1; отрицательное значение прячет экран осмотра
        public void SetScan(float progress)
        {
            scan.SetActive(progress >= 0f);
            if (progress >= 0f) scanFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
        }

        public void SetMapStrip(DarknessMap map, Vector3 forward, bool visible)
        {
            strip.SetActive(visible && !map.Empty);
            if (!strip.activeSelf) return;
            for (int i = 0; i < DarknessMap.YawCells; i++)
            {
                float d = map.ColumnDarkness(i);
                stripCells[i].color = d < 0f ? new Color(0.3f, 0.1f, 0.1f, 0.5f) : Color.Lerp(new Color(0.9f, 0.9f, 0.85f), Color.black, d);
            }
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            stripMarker.anchoredPosition = new Vector2((yaw + 180f) / 360f * 720f, 0f);
        }

        public bool IsOverButton(Vector2 screen) =>
            hud.activeSelf && (RectTransformUtility.RectangleContainsScreenPoint(journalButton, screen, null)
                               || RectTransformUtility.RectangleContainsScreenPoint(toolBar, screen, null)
                               || RectTransformUtility.RectangleContainsScreenPoint(bagButton, screen, null)
                               || RectTransformUtility.RectangleContainsScreenPoint(logButton, screen, null)
                               || bagPanel.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(bagPanel, screen, null));

        public event System.Action PostureChanged;
        public bool DepthViewOn { get; private set; }
        RawImage depthView;
        GameObject posture;
        readonly List<(Image bg, int value)> postureButtons = new();

        void RefreshPosture()
        {
            foreach (var (bg, value) in postureButtons) bg.color = value == Settings.Posture ? Blood : Ink;
        }

        // выбор позы — только в режиме гироскопа
        public void ShowPosture(bool on)
        {
            posture.SetActive(on);
            RefreshPosture();
        }

        public void SetPhoneHeight(string text)
        {
            if (TestMode.On) phoneText.text = text;
        }

        public void SetDiagnostics(string text)
        {
            if (TestMode.On) diagText.text = text;
        }

        public void SetModifier(string name) => modifierName = name;

        public void SetPhase(string text, bool hunt)
        {
            phaseText.text = string.IsNullOrEmpty(text) || string.IsNullOrEmpty(modifierName) ? text : $"{text}  ·  {modifierName}";
            phaseText.color = hunt ? new Color(0.85f, 0.15f, 0.15f) : Dim;
            recText.gameObject.SetActive(hunt);
            huntEdges = hunt;
        }

        // markYaw — направление на цель (градусы); за краем шкалы метка прижимается к краю
        public void SetCompass(Vector3 forward, float agitation, float? markYaw = null, bool hunt = false)
        {
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            compassMark.gameObject.SetActive(markYaw.HasValue);
            if (markYaw.HasValue)
            {
                float d = Mathf.DeltaAngle(yaw, markYaw.Value);
                bool edge = Mathf.Abs(d) > 90f;
                compassMark.rectTransform.anchoredPosition = new Vector2(Mathf.Clamp(d, -90f, 90f) / 90f * 280f, 20f);
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * (hunt ? 9f : 4f));
                var c = hunt ? new Color(0.95f, 0.15f, 0.12f) : Frost;
                c.a = edge ? pulse * 0.6f : pulse;
                compassMark.color = c;
            }
            foreach (var (rt, tickYaw) in compassTicks)
            {
                float d = Mathf.DeltaAngle(yaw, tickYaw);
                rt.gameObject.SetActive(Mathf.Abs(d) < 90f);
                rt.anchoredPosition = new Vector2(d / 90f * 280f, 0f);
            }
            foreach (var (label, labelYaw) in compassLabels)
            {
                float d = Mathf.DeltaAngle(yaw, labelYaw);
                label.gameObject.SetActive(Mathf.Abs(d) < 80f);
                label.rectTransform.anchoredPosition = new Vector2(d / 90f * 280f, 32f);
            }
            needle.rectTransform.localEulerAngles = new Vector3(0, 0, agitation > 0f ? UnityEngine.Random.Range(-35f, 35f) : 0f);
            needle.color = agitation > 0f ? new Color(1f, 0.4f, 0.3f) : Bone;
        }

        // уровень шума 0..1; отрицательный — микрофона нет
        public void SetMic(float level, bool visible)
        {
            micRoot.SetActive(visible && level >= 0f);
            if (!micRoot.activeSelf) return;
            for (int i = 0; i < micBars.Length; i++)
            {
                bool lit = level * micBars.Length > i + 0.2f;
                micBars[i].color = !lit ? new Color(1, 1, 1, 0.12f) : level > 0.35f ? new Color(0.9f, 0.15f, 0.1f) : Bone;
            }
        }

        public void SetHidden(bool hidden) => hiddenText.gameObject.SetActive(hidden && hud.activeSelf);

        public void PhotoFlash()
        {
            Flash(new Color(1f, 1f, 1f, 0.55f));
            Supply(Loc.T("photo_saved"));
        }

        public void Message(string text, float seconds = 1.6f)
        {
            messageText.text = text;
            messageTimer = Mathf.Max(seconds, 2.2f + text.Length * 0.035f);   // успеть прочитать
            Log(text, false);
        }

        // короткая строка об остатке предмета после применения
        public void Supply(string text)
        {
            supplyText.text = text;
            supplyTimer = 1.8f;
        }

        public void SetLantern(bool on, float battery)
        {
            if (on && !lanternOn) Supply($"{Catalog.Get(ItemKind.Lantern).Name}: {Mathf.CeilToInt(battery)}%");
            if (on) { supplyText.text = $"{Catalog.Get(ItemKind.Lantern).Name}: {Mathf.CeilToInt(battery)}%"; supplyTimer = Mathf.Max(supplyTimer, 0.8f); }
            lanternOn = on;
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);
        public void Flash(Color c) { flashColor = c; flashAlpha = c.a; }

        // экран держится чёрным (обморок)
        float blackout;
        public void Blackout(float seconds) { blackout = seconds; flashColor = Color.black; flashAlpha = 1f; }

        // дыхание на «стекле» камеры: иней по всем краям на несколько секунд
        float frostBurst;
        public void FrostBurst(float seconds = 3f) => frostBurst = Mathf.Max(frostBurst, seconds);

        // мокрая ладонь, прижатая к «стеклу» камеры изнутри
        readonly List<(RawImage img, float life)> hands = new();
        public void Handprint()
        {
            var img = Overlay(shakeRoot, "Hand", Textures.Hand(), new Color(0.85f, 0.9f, 0.92f, 0f));
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(UnityEngine.Random.Range(0.25f, 0.75f), UnityEngine.Random.Range(0.3f, 0.75f));
            rt.sizeDelta = new Vector2(330, 420) * UnityEngine.Random.Range(0.9f, 1.2f);
            rt.localEulerAngles = new Vector3(0f, 0f, UnityEngine.Random.Range(-25f, 25f));
            rt.localScale = new Vector3(UnityEngine.Random.value < 0.5f ? -1f : 1f, 1f, 1f);
            hands.Add((img, 5f));
        }

        // скример: лицо сущности на весь экран на долю секунды
        public void Jumpscare(EntityDef def, float seed, float strength)
        {
            Flash(new Color(0.35f, 0f, 0f, 0.7f * strength));
            Shake(strength);
            if (def.Invisible) return;
            if (jumpMat != null) Destroy(jumpMat);
            jumpMat = Entity.CreateMaterial(def, seed);
            jumpMat.SetFloat("_Flash", 0.15f);
            jumpscare.material = jumpMat;
            jumpscare.gameObject.SetActive(true);
            jumpTimer = 0.45f;
        }

        public void SetGrain(float amount) => grain.color = new Color(1f, 1f, 1f, amount);

        // дым полыни: серо-зелёные клубы снизу вверх
        public void Smoke()
        {
            for (int i = 0; i < 16; i++)
            {
                var g = Rect(shakeRoot, "Smoke", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).gameObject.AddComponent<UILine>();
                g.Filled = true;
                g.raycastTarget = false;
                g.color = new Color(0.55f, 0.6f, 0.5f, 0.35f);
                float r = UnityEngine.Random.Range(40f, 90f);
                var pts = new Vector2[8];
                for (int k = 0; k < 8; k++)
                {
                    float a = k / 8f * Mathf.PI * 2f;
                    pts[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                }
                g.SetPoints(pts);
                g.rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(-300f, 300f), 150f);
                var vel = new Vector2(UnityEngine.Random.Range(-150f, 150f), UnityEngine.Random.Range(900f, 1500f));
                grains.Add((g.rectTransform, vel, 0.9f));
            }
        }

        public void SaltBurst()
        {
            for (int i = 0; i < 26; i++)
            {
                var g = Rect(shakeRoot, "Grain", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).gameObject.AddComponent<UILine>();
                g.Filled = true;
                g.raycastTarget = false;
                g.color = new Color(1f, 1f, 0.95f, 0.9f);
                float s = UnityEngine.Random.Range(4f, 9f);
                g.SetPoints(new[] { new Vector2(0, s), new Vector2(s, 0), new Vector2(0, -s), new Vector2(-s, 0) });
                g.rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(-60f, 60f), 200f);
                var vel = new Vector2(UnityEngine.Random.Range(-500f, 500f), UnityEngine.Random.Range(1600f, 2600f));
                grains.Add((g.rectTransform, vel, 0.6f));
            }
        }

        // иней с той стороны, где сущность (если её не видно на экране)
        public void UpdateCues(Camera cam, List<Entity> entities, Vector3? lairFrost = null)
        {
            for (int i = 0; i < 4; i++) edgeTarget[i] = frostBurst > 0f ? 0.8f : 0f;
            if (lairFrost.HasValue)
            {
                var vp = cam.WorldToViewportPoint(lairFrost.Value);
                var dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                if (vp.z < 0f) dir = -dir;
                bool facing = vp.z > 0f && Mathf.Abs(dir.x) < 0.5f && Mathf.Abs(dir.y) < 0.5f;
                if (facing)
                    for (int i = 0; i < 4; i++) edgeTarget[i] = 0.7f;   // смотришь прямо на логово — иней со всех сторон
                else
                {
                    dir.Normalize();
                    edgeTarget[0] = Mathf.Max(0f, -dir.x);
                    edgeTarget[1] = Mathf.Max(0f, dir.x);
                    edgeTarget[2] = Mathf.Max(0f, dir.y);
                    edgeTarget[3] = Mathf.Max(0f, -dir.y);
                }
            }
            foreach (var e in entities)
            {
                if (e == null || !e.Alive) continue;
                var vp = cam.WorldToViewportPoint(e.transform.position);
                bool onScreen = vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
                if (onScreen && e.Visible) continue;

                var dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                if (vp.z < 0f) dir = -dir;
                if (dir.sqrMagnitude < 1e-5f) dir = Vector2.down;
                dir.Normalize();

                float strength = Mathf.Clamp01(1.2f - e.FlatDistance() / 4.5f) * (e.Current == Entity.State.Lurking ? 0.5f : 1f);
                if (huntEdges) strength = Mathf.Max(strength, 0.4f);   // на охоте сторона видна всегда
                edgeTarget[0] = Mathf.Max(edgeTarget[0], Mathf.Max(0f, -dir.x) * strength);
                edgeTarget[1] = Mathf.Max(edgeTarget[1], Mathf.Max(0f, dir.x) * strength);
                edgeTarget[2] = Mathf.Max(edgeTarget[2], Mathf.Max(0f, dir.y) * strength);
                edgeTarget[3] = Mathf.Max(edgeTarget[3], Mathf.Max(0f, -dir.y) * strength);
            }
        }

        // ---------- кадр ----------

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (visionPreview.activeSelf) FitPreview();
            if (journal.activeSelf && journalPractice.activeSelf) TickPractice(dt);

            if (messageTimer > 0f)
            {
                messageTimer -= dt;
                SetAlpha(messageText, Mathf.Clamp01(messageTimer / 0.5f));
            }
            if (supplyTimer > 0f)
            {
                supplyTimer -= dt;
                SetAlpha(supplyText, Mathf.Clamp01(supplyTimer / 0.4f) * 0.7f);
            }

            bool night = hud.activeSelf;
            narrateTimer -= dt;
            hintTimer -= dt;
            SetAlpha(narrateText, night ? Mathf.Clamp01(narrateTimer / 0.8f) : 0f);
            SetAlpha(hintText, night ? Mathf.Clamp01(hintTimer / 0.8f) : 0f);

            if (trophyTimer > 0f)
            {
                trophyTimer -= dt;
                SetAlpha(trophyText, Mathf.Clamp01(trophyTimer / 0.6f));
            }

            recBlink += dt;
            if (hud.activeSelf && overlayStyle is Vision.Cctv or Vision.Vhs or Vision.NightShot) SetCameraOverlay(overlayStyle);
            if (recText.gameObject.activeSelf) SetAlpha(recText, recBlink % 1f < 0.6f ? 1f : 0.2f);

            bool inNight = hud.activeSelf;
            float dark = inNight ? DarkBase + fear * 0.35f : 0f;
            // редкое «моргание» света при высоком страхе
            if (inNight && fear > 0.5f && UnityEngine.Random.value < 0.01f) dark = 0.95f;
            SetAlpha(darkness, Mathf.MoveTowards(darkness.color.a, dark, dt * 2f));
            SetAlpha(lanternDark, Mathf.MoveTowards(lanternDark.color.a, lanternOn && inNight ? 0.6f : 0f, dt * 6f));
            float glow = lanternOn && inNight ? 0.22f + UnityEngine.Random.Range(-0.02f, 0.02f) : 0f;
            SetAlpha(lanternGlow, Mathf.MoveTowards(lanternGlow.color.a, glow, dt * 4f));

            for (int i = 0; i < 4; i++)
            {
                // на охоте кромка красная и пульсирует, тем чаще, чем ближе сущность
                float target = inNight ? edgeTarget[i] * 0.75f : 0f;
                if (huntEdges) target *= 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * (3f + edgeTarget[i] * 6f));
                var ec = huntEdges ? new Color(0.8f, 0.05f, 0.05f) : Frost;
                edges[i].color = new Color(ec.r, ec.g, ec.b, Mathf.MoveTowards(edges[i].color.a, target, dt * (huntEdges ? 4f : 1.5f)));
            }

            grain.uvRect = new Rect(UnityEngine.Random.value, UnityEngine.Random.value, 6f, 10f);
            if (jumpTimer > 0f)
            {
                jumpTimer -= dt;
                float k = 1f - jumpTimer / 0.45f;
                jumpscare.rectTransform.localScale = Vector3.one * (1.05f + k * 0.35f);
                jumpscare.rectTransform.anchoredPosition = UnityEngine.Random.insideUnitCircle * 25f;
                if (jumpTimer <= 0f) jumpscare.gameObject.SetActive(false);
            }

            if (blackout > 0f) { blackout -= dt; flashAlpha = 1f; }
            else flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 1.6f);
            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);
            frostBurst -= dt;

            // ладони: резко проступают, медленно стекают и тают
            for (int i = hands.Count - 1; i >= 0; i--)
            {
                var (img, life) = hands[i];
                life -= dt;
                float a = Mathf.Min(Mathf.Clamp01((5f - life) / 0.12f), Mathf.Clamp01(life / 3f)) * 0.55f;
                img.color = new Color(img.color.r, img.color.g, img.color.b, hud.activeSelf ? a : 0f);
                img.rectTransform.anchoredPosition += new Vector2(0f, -dt * 6f);
                if (life <= 0f) { Destroy(img.gameObject); hands.RemoveAt(i); continue; }
                hands[i] = (img, life);
            }

            shake = Mathf.MoveTowards(shake, 0f, dt * 1.5f);
            shakeRoot.anchoredPosition = shake > 0.01f ? UnityEngine.Random.insideUnitCircle * 30f * shake : Vector2.zero;

            for (int i = grains.Count - 1; i >= 0; i--)
            {
                var (rt, vel, life) = grains[i];
                life -= dt;
                if (life <= 0f) { Destroy(rt.gameObject); grains.RemoveAt(i); continue; }
                if (rt.name == "Grain") vel.y -= 2200f * dt;
                else
                {
                    vel *= 1f - dt * 2.5f;
                    rt.localScale = Vector3.one * (1f + (0.9f - life));
                    var puff = rt.GetComponent<UILine>();
                    var pc = puff.color;
                    pc.a = 0.35f * Mathf.Clamp01(life / 0.9f);
                    puff.color = pc;
                }
                rt.anchoredPosition += vel * dt;
                if (rt.name == "Grain") rt.localScale = Vector3.one * Mathf.Clamp01(life / 0.6f);
                grains[i] = (rt, vel, life);
            }
        }

        static void SetAlpha(Graphic g, float a)
        {
            var c = g.color;
            c.a = a;
            g.color = c;
        }

        // ---------- построители ----------

        static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static RawImage Overlay(Transform parent, string name, Texture tex, Color color)
        {
            var img = Rect(parent, name, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        GameObject Panel(Transform parent, string name)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.one);
            rt.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.008f, 0.012f, 0.9f);
            return rt.gameObject;
        }

        Text Label(Transform parent, string text, int size, Vector2 anchor, Vector2 pos, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(600, size * 1.6f);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static void Outline(Text t)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.9f);
            o.effectDistance = new Vector2(3, -3);
        }

        static UILine Frame(RectTransform parent, float w, float h, Color color, float thickness)
        {
            var f = Rect(parent, "Frame", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<UILine>();
            f.Closed = true;
            f.Thickness = thickness;
            f.color = color;
            f.raycastTarget = false;
            float x = w * 0.5f, y = h * 0.5f;
            f.SetPoints(new[] { new Vector2(-x, y), new Vector2(x, y), new Vector2(x, -y), new Vector2(-x, -y) });
            return f;
        }

        GameObject Button(Transform parent, string text, Vector2 anchor, Action onClick, bool primary, float width = 640f, float offsetX = 0f)
        {
            var rt = Rect(parent, text, anchor, anchor);
            rt.sizeDelta = new Vector2(width, 130);
            rt.anchoredPosition = new Vector2(offsetX, 0f);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = primary ? Blood : Ink;
            var btn = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            Frame(rt, width, 130, primary ? Bone : Dim, 4f);
            var label = Label(rt, text, width < 400f ? 38 : 46, new Vector2(0.5f, 0.5f), Vector2.zero, Bone);
            label.rectTransform.sizeDelta = new Vector2(width - 20f, 120);
            label.fontStyle = FontStyle.Bold;
            return rt.gameObject;
        }
    }
}
