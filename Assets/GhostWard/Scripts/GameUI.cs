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

        public event Action StartPressed;
        public event Action NextNightPressed;
        public event Action<bool> JournalToggled;

        public bool JournalOpen => journal.activeSelf;

        RectTransform root, shakeRoot, journalButton;
        Font font;
        GameObject hud, menu, dawn, over, loading, journal, scan, strip;
        Text nightText, clockText, messageText, supplyText, diagText, menuMode, dawnText, overText, journalSupply, scanText;
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

            loading = Panel(root, "Loading");
            Label(loading.transform, "Темнеет…", 54, new Vector2(0.5f, 0.5f), Vector2.zero, Bone);

            messageText = Label(root, "", 44, new Vector2(0.5f, 0.6f), Vector2.zero, Bone);
            messageText.rectTransform.sizeDelta = new Vector2(940, 300);
            messageText.fontStyle = FontStyle.Italic;
            Outline(messageText);

            supplyText = Label(root, "", 32, new Vector2(0.5f, 0f), new Vector2(0, 160), Dim);
            supplyText.rectTransform.sizeDelta = new Vector2(800, 60);

            flash = Overlay(root, "Flash", Texture2D.whiteTexture, new Color(0, 0, 0, 0));

            diagText = Label(root, "", 22, new Vector2(1f, 1f), new Vector2(-20, -40), new Color(1, 1, 1, 0.45f));
            diagText.alignment = TextAnchor.UpperRight;
            diagText.rectTransform.pivot = new Vector2(1f, 1f);
            diagText.rectTransform.sizeDelta = new Vector2(600, 140);

            journal.transform.SetAsLastSibling();
            ShowOnly(loading);
        }

        void BuildHud()
        {
            hud = Rect(shakeRoot, "HUD", Vector2.zero, Vector2.one).gameObject;

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
            var fearLabel = Label(bar, "СТРАХ", 24, new Vector2(0.5f, 0f), new Vector2(0, -26), Dim);
            fearLabel.rectTransform.sizeDelta = new Vector2(300, 40);

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
        }

        void BuildScan()
        {
            scan = Rect(shakeRoot, "Scan", Vector2.zero, Vector2.one).gameObject;
            scanText = Label(scan.transform, "ОСМОТРИСЬ", 60, new Vector2(0.5f, 0.42f), Vector2.zero, Bone);
            scanText.rectTransform.sizeDelta = new Vector2(900, 100);
            Outline(scanText);
            var bar = Rect(scan.transform, "Progress", new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f));
            bar.anchoredPosition = new Vector2(0, -70);
            bar.sizeDelta = new Vector2(520, 10);
            bar.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.15f);
            scanFill = Rect(bar, "Fill", Vector2.zero, new Vector2(0f, 1f));
            scanFill.gameObject.AddComponent<Image>().color = Bone;
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
            var title = Label(menu.transform, "GHOST WARD", 110, new Vector2(0.5f, 0.72f), Vector2.zero, Bone);
            title.rectTransform.sizeDelta = new Vector2(1000, 150);
            title.fontStyle = FontStyle.Bold;

            var sub = Label(menu.transform, "Доживи до рассвета.\nУзнай, кто пришёл, и защитись тем, чего он боится.",
                36, new Vector2(0.5f, 0.6f), Vector2.zero, Dim);
            sub.rectTransform.sizeDelta = new Vector2(900, 140);

            menuMode = Label(menu.transform, "", 28, new Vector2(0.5f, 0.48f), Vector2.zero, Dim);
            menuMode.rectTransform.sizeDelta = new Vector2(900, 80);
            Button(menu.transform, "НАЧАТЬ НОЧЬ", new Vector2(0.5f, 0.36f), () => StartPressed?.Invoke(), true);
            Button(menu.transform, "ДНЕВНИК", new Vector2(0.5f, 0.26f), () => SetJournal(true), false);
            var tip = Label(menu.transform, "Играй в темноте и в наушниках", 28, new Vector2(0.5f, 0.12f), Vector2.zero, Dim);
            tip.rectTransform.sizeDelta = new Vector2(900, 50);
        }

        void BuildDawn()
        {
            dawn = Panel(root, "Dawn");
            var t = Label(dawn.transform, "РАССВЕТ", 110, new Vector2(0.5f, 0.66f), Vector2.zero, new Color(1f, 0.85f, 0.6f));
            t.rectTransform.sizeDelta = new Vector2(1000, 150);
            t.fontStyle = FontStyle.Bold;
            dawnText = Label(dawn.transform, "", 36, new Vector2(0.5f, 0.5f), Vector2.zero, Bone);
            dawnText.rectTransform.sizeDelta = new Vector2(940, 260);
            Button(dawn.transform, "СЛЕДУЮЩАЯ НОЧЬ", new Vector2(0.5f, 0.3f), () => NextNightPressed?.Invoke(), true);
        }

        void BuildOver()
        {
            over = Panel(root, "GameOver");
            var t = Label(over.transform, "ТЕБЯ ЗАБРАЛИ", 96, new Vector2(0.5f, 0.64f), Vector2.zero, new Color(0.8f, 0.15f, 0.15f));
            t.rectTransform.sizeDelta = new Vector2(1000, 140);
            t.fontStyle = FontStyle.Bold;
            overText = Label(over.transform, "", 40, new Vector2(0.5f, 0.5f), Vector2.zero, Bone);
            overText.rectTransform.sizeDelta = new Vector2(1000, 200);
            Button(over.transform, "ЕЩЁ РАЗ", new Vector2(0.5f, 0.3f), () => StartPressed?.Invoke(), true);
        }

        // дневник: кто приходит, по каким признакам узнать и как защититься
        void BuildJournal()
        {
            journal = Panel(root, "Journal");
            journal.GetComponent<Image>().color = new Color(0.02f, 0.015f, 0.02f, 0.96f);
            var title = Label(journal.transform, "ДНЕВНИК", 72, new Vector2(0.5f, 0.92f), Vector2.zero, Bone);
            title.rectTransform.sizeDelta = new Vector2(900, 110);
            title.fontStyle = FontStyle.Bold;

            float y = 0.81f;
            foreach (var e in Catalog.Entities)
            {
                var item = Catalog.Get(e.Bane);
                var head = Label(journal.transform, e.Name.ToUpper(), 42, new Vector2(0.5f, y), Vector2.zero, Bone);
                head.rectTransform.sizeDelta = new Vector2(900, 60);
                head.alignment = TextAnchor.MiddleLeft;
                head.fontStyle = FontStyle.Bold;
                var body = Label(journal.transform,
                    $"{e.Signs}\nБоится: {item.Name.ToLower()} — {item.Usage}.",
                    30, new Vector2(0.5f, y - 0.055f), Vector2.zero, Dim);
                body.rectTransform.sizeDelta = new Vector2(900, 120);
                body.alignment = TextAnchor.UpperLeft;
                body.color = new Color(Bone.r, Bone.g, Bone.b, 0.7f);
                y -= 0.15f;
            }

            journalSupply = Label(journal.transform, "", 32, new Vector2(0.5f, 0.17f), Vector2.zero, Bone);
            journalSupply.rectTransform.sizeDelta = new Vector2(900, 60);
            Button(journal.transform, "ЗАКРЫТЬ", new Vector2(0.5f, 0.08f), () => SetJournal(false), false);
            journal.SetActive(false);
        }

        void SetJournal(bool open)
        {
            journal.SetActive(open);
            JournalToggled?.Invoke(open);
        }

        // ---------- экраны ----------

        public void ShowMenu(string modeInfo) { menuMode.text = modeInfo; ShowOnly(menu); }
        public void ShowHud() => ShowOnly(hud);

        public void ShowJournal(Inventory inv)
        {
            journalSupply.text = inv == null ? "" :
                $"С собой: соль {inv.Salt} · железо {inv.Iron} · колокольчик {inv.Bell} · фонарь {Mathf.CeilToInt(inv.Battery)}%";
        }

        public void ShowDawn(int night, string supply)
        {
            dawnText.text = $"Ночь {night} пережита.\n\nНашлось в доме:\n{supply}";
            ShowOnly(dawn);
            ClearEffects();
        }

        public void ShowGameOver(int night, float progress, int best)
        {
            overText.text = $"Ночь {night}, {Clock(progress)}\nДольше всего: ночь {best}";
            ShowOnly(over);
            ClearEffects();
        }

        void ShowOnly(GameObject panel)
        {
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

        public void SetNight(int night) => nightText.text = $"НОЧЬ {night}";
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
            hud.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(journalButton, screen, null);

        public void SetDiagnostics(string text) => diagText.text = text;

        public void Message(string text, float seconds = 1.6f)
        {
            messageText.text = text;
            messageTimer = seconds;
        }

        // короткая строка об остатке предмета после применения
        public void Supply(string text)
        {
            supplyText.text = text;
            supplyTimer = 1.8f;
        }

        public void SetLantern(bool on, float battery)
        {
            if (on && !lanternOn) Supply($"фонарь: {Mathf.CeilToInt(battery)}%");
            if (on) { supplyText.text = $"фонарь: {Mathf.CeilToInt(battery)}%"; supplyTimer = Mathf.Max(supplyTimer, 0.8f); }
            lanternOn = on;
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);
        public void Flash(Color c) { flashColor = c; flashAlpha = c.a; }

        public void Jumpscare(float strength)
        {
            Flash(new Color(0.35f, 0f, 0f, 0.85f * strength));
            Shake(strength);
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
        public void UpdateCues(Camera cam, List<Entity> entities)
        {
            for (int i = 0; i < 4; i++) edgeTarget[i] = 0f;
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

            bool inNight = hud.activeSelf;
            float dark = inNight ? 0.55f + fear * 0.35f : 0f;
            // редкое «моргание» света при высоком страхе
            if (inNight && fear > 0.5f && UnityEngine.Random.value < 0.01f) dark = 0.95f;
            SetAlpha(darkness, Mathf.MoveTowards(darkness.color.a, dark, dt * 2f));
            SetAlpha(lanternDark, Mathf.MoveTowards(lanternDark.color.a, lanternOn && inNight ? 0.6f : 0f, dt * 6f));
            float glow = lanternOn && inNight ? 0.22f + UnityEngine.Random.Range(-0.02f, 0.02f) : 0f;
            SetAlpha(lanternGlow, Mathf.MoveTowards(lanternGlow.color.a, glow, dt * 4f));

            for (int i = 0; i < 4; i++)
            {
                float target = inNight ? edgeTarget[i] * 0.75f : 0f;
                SetAlpha(edges[i], Mathf.MoveTowards(edges[i].color.a, target, dt * 1.5f));
            }

            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 1.6f);
            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);

            shake = Mathf.MoveTowards(shake, 0f, dt * 1.5f);
            shakeRoot.anchoredPosition = shake > 0.01f ? UnityEngine.Random.insideUnitCircle * 30f * shake : Vector2.zero;

            for (int i = grains.Count - 1; i >= 0; i--)
            {
                var (rt, vel, life) = grains[i];
                life -= dt;
                if (life <= 0f) { Destroy(rt.gameObject); grains.RemoveAt(i); continue; }
                vel.y -= 2200f * dt;
                rt.anchoredPosition += vel * dt;
                rt.localScale = Vector3.one * Mathf.Clamp01(life / 0.6f);
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

        void Button(Transform parent, string text, Vector2 anchor, Action onClick, bool primary)
        {
            var rt = Rect(parent, text, anchor, anchor);
            rt.sizeDelta = new Vector2(640, 130);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = primary ? Blood : Ink;
            var btn = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            Frame(rt, 640, 130, primary ? Bone : Dim, 4f);
            var label = Label(rt, text, 46, new Vector2(0.5f, 0.5f), Vector2.zero, Bone);
            label.rectTransform.sizeDelta = new Vector2(620, 120);
            label.fontStyle = FontStyle.Bold;
        }
    }
}
