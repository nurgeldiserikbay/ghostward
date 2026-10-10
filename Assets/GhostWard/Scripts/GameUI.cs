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
        static readonly Color Ink = new(0.04f, 0.03f, 0.06f, 0.82f);
        static readonly Color Bone = new(0.93f, 0.9f, 0.84f);

        public RectTransform Root { get; private set; }

        Font font;
        Text scoreText, waveText, messageText, fpsText, comboText;
        float messageTimer;
        readonly List<UILine> hearts = new();
        UILine stroke;
        float strokeFade;
        RawImage damage;
        float damageAlpha;
        RectTransform indicatorRoot;
        readonly List<UILine> indicators = new();
        GameObject hud, menu, over, loading;
        Text menuMode, overScore;
        readonly Dictionary<GhostKind, GameObject> legend = new();
        float fpsTime;
        int fpsFrames;

        public event Action StartPressed;

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
            ui.Root = go.GetComponent<RectTransform>();
            ui.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ui.Build();
            return ui;
        }

        void Build()
        {
            // урон — самый нижний слой интерфейса, поверх сцены
            damage = Rect(Root, "Damage", Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            damage.texture = Effects.Vignette();
            damage.color = new Color(0.8f, 0f, 0.05f, 0f);
            damage.raycastTarget = false;

            indicatorRoot = Rect(Root, "Indicators", Vector2.zero, Vector2.one);

            stroke = Rect(Root, "Stroke", Vector2.zero, Vector2.one).gameObject.AddComponent<UILine>();
            stroke.Thickness = 14f;
            stroke.raycastTarget = false;

            BuildHud();
            BuildMenu();
            BuildOver();

            loading = Panel(Root, "Loading");
            Label(loading.transform, "Открываю врата…", 54, new Vector2(0.5f, 0.5f), Vector2.zero, Bone);

            messageText = Label(Root, "", 58, new Vector2(0.5f, 0.62f), Vector2.zero, Bone);
            messageText.rectTransform.sizeDelta = new Vector2(1000, 220);
            Outline(messageText);

            fpsText = Label(Root, "", 26, new Vector2(0f, 0f), new Vector2(90, 40), new Color(1, 1, 1, 0.5f));

            ShowOnly(loading);
        }

        void BuildHud()
        {
            hud = new GameObject("HUD", typeof(RectTransform));
            hud.transform.SetParent(Root, false);
            Stretch(hud.GetComponent<RectTransform>());

            scoreText = Label(hud.transform, "0", 64, new Vector2(0f, 1f), new Vector2(200, -110), Bone);
            scoreText.alignment = TextAnchor.MiddleLeft;
            scoreText.rectTransform.pivot = new Vector2(0f, 0.5f);
            scoreText.rectTransform.anchoredPosition = new Vector2(56, -110);
            Outline(scoreText);

            comboText = Label(hud.transform, "", 36, new Vector2(0f, 1f), new Vector2(56, -175), new Color(1f, 0.85f, 0.3f));
            comboText.alignment = TextAnchor.MiddleLeft;
            comboText.rectTransform.pivot = new Vector2(0f, 0.5f);
            comboText.rectTransform.anchoredPosition = new Vector2(56, -175);

            waveText = Label(hud.transform, "", 40, new Vector2(0.5f, 1f), new Vector2(0, -110), new Color(Bone.r, Bone.g, Bone.b, 0.75f));
            Outline(waveText);

            for (int i = 0; i < 3; i++)
            {
                var h = Rect(hud.transform, "Heart", new Vector2(1f, 1f), new Vector2(1f, 1f)).gameObject.AddComponent<UILine>();
                h.rectTransform.anchoredPosition = new Vector2(-70 - i * 80, -110);
                h.Filled = true;
                h.color = new Color(0.95f, 0.15f, 0.2f);
                h.raycastTarget = false;
                h.SetPoints(new[] { new Vector2(0, 30), new Vector2(26, 0), new Vector2(0, -30), new Vector2(-26, 0) });
                hearts.Add(h);
            }

            // нижняя памятка: какой знак против какого призрака
            var bar = Rect(hud.transform, "Legend", new Vector2(0f, 0f), new Vector2(1f, 0f));
            bar.sizeDelta = new Vector2(0, 230);
            bar.anchoredPosition = new Vector2(0, 150);
            int n = GhostCatalog.All.Length;
            for (int i = 0; i < n; i++)
            {
                var def = GhostCatalog.All[i];
                var card = Rect(bar, def.Name, new Vector2((i + 0.5f) / n, 0.5f), new Vector2((i + 0.5f) / n, 0.5f));
                card.sizeDelta = new Vector2(300, 220);
                var bg = card.gameObject.AddComponent<Image>();
                bg.color = Ink;
                bg.raycastTarget = false;
                Frame(card, 300, 220, def.Color, 4f);

                var dot = Rect(card, "Dot", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f)).gameObject.AddComponent<UILine>();
                dot.rectTransform.anchoredPosition = new Vector2(-95, -40);
                dot.Filled = true;
                dot.color = def.Color;
                dot.SetPoints(new[] { new Vector2(0, 14), new Vector2(14, 0), new Vector2(0, -14), new Vector2(-14, 0) });

                var name = Label(card, def.Name, 32, new Vector2(0.5f, 1f), new Vector2(20, -40), Bone);
                name.rectTransform.sizeDelta = new Vector2(220, 50);

                var icon = Rect(card, "Sigil", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).gameObject.AddComponent<UILine>();
                icon.rectTransform.anchoredPosition = new Vector2(0, 80);
                icon.Thickness = 7f;
                icon.color = def.Color;
                icon.raycastTarget = false;
                SetSigilShape(icon, def.Ward, 52f);

                legend[def.Kind] = card.gameObject;
            }
        }

        void BuildMenu()
        {
            menu = Panel(Root, "Menu");
            var title = Label(menu.transform, "GHOST WARD", 120, new Vector2(0.5f, 0.72f), Vector2.zero, Bone);
            title.rectTransform.sizeDelta = new Vector2(1000, 160);
            title.fontStyle = FontStyle.Bold;
            Outline(title);

            var sub = Label(menu.transform,
                "Призраки приходят со всех сторон.\nСлушай шёпот, поворачивайся\nи рисуй нужный знак пальцем.",
                40, new Vector2(0.5f, 0.56f), Vector2.zero, Bone);
            sub.rectTransform.sizeDelta = new Vector2(960, 220);

            menuMode = Label(menu.transform, "", 30, new Vector2(0.5f, 0.45f), Vector2.zero, new Color(1, 1, 1, 0.55f));
            menuMode.rectTransform.sizeDelta = new Vector2(960, 100);

            Button(menu.transform, "НАЧАТЬ ОХОТУ", new Vector2(0.5f, 0.3f), () => StartPressed?.Invoke());

            var tip = Label(menu.transform, "Лучше в наушниках", 30, new Vector2(0.5f, 0.18f), Vector2.zero, new Color(1, 1, 1, 0.45f));
            tip.rectTransform.sizeDelta = new Vector2(900, 60);
        }

        void BuildOver()
        {
            over = Panel(Root, "GameOver");
            var t = Label(over.transform, "ТЕБЯ ЗАБРАЛИ", 96, new Vector2(0.5f, 0.66f), Vector2.zero, new Color(1f, 0.3f, 0.3f));
            t.rectTransform.sizeDelta = new Vector2(1000, 140);
            t.fontStyle = FontStyle.Bold;
            Outline(t);
            overScore = Label(over.transform, "", 48, new Vector2(0.5f, 0.52f), Vector2.zero, Bone);
            overScore.rectTransform.sizeDelta = new Vector2(1000, 200);
            Button(over.transform, "ЕЩЁ РАЗ", new Vector2(0.5f, 0.32f), () => StartPressed?.Invoke());
        }

        // ---------- состояние экранов ----------

        public void ShowMenu(string modeInfo)
        {
            menuMode.text = modeInfo;
            ShowOnly(menu);
        }

        public void ShowHud() => ShowOnly(hud);

        public void ShowGameOver(int score, int best, int wave)
        {
            overScore.text = $"Счёт: {score}\nВолна: {wave}   Рекорд: {best}";
            ShowOnly(over);
            ClearIndicators();
        }

        void ShowOnly(GameObject panel)
        {
            loading.SetActive(panel == loading);
            menu.SetActive(panel == menu);
            over.SetActive(panel == over);
            hud.SetActive(panel == hud);
        }

        public void SetLegendVisible(GhostKind kind, bool visible) => legend[kind].SetActive(visible);

        public void SetScore(int score, int combo)
        {
            scoreText.text = score.ToString();
            comboText.text = combo > 1 ? $"серия ×{combo}" : "";
        }

        public void SetWave(int wave) => waveText.text = $"ВОЛНА {wave}";

        public void SetHp(int hp)
        {
            for (int i = 0; i < hearts.Count; i++)
                hearts[i].color = i < hp ? new Color(0.95f, 0.15f, 0.2f) : new Color(1f, 1f, 1f, 0.15f);
        }

        public void Message(string text, float seconds = 1.6f)
        {
            messageText.text = text;
            messageTimer = seconds;
        }

        public void Damage() => damageAlpha = 0.9f;

        // ---------- росчерк ----------

        public Vector2 ScreenToLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(stroke.rectTransform, screen, null, out var local);
            return local;
        }

        public void SetStroke(List<Vector2> screenPoints, Color color)
        {
            var local = new List<Vector2>(screenPoints.Count);
            foreach (var p in screenPoints) local.Add(ScreenToLocal(p));
            stroke.color = color;
            stroke.SetPoints(local);
            strokeFade = 0f;
        }

        public void ReleaseStroke(Color color)
        {
            stroke.color = color;
            strokeFade = 0.5f;
        }

        // ---------- указатели на призраков за краем экрана ----------

        public void UpdateIndicators(Camera cam, List<Ghost> ghosts)
        {
            int used = 0;
            var size = indicatorRoot.rect.size;
            foreach (var g in ghosts)
            {
                if (g == null || !g.Alive) continue;
                var vp = cam.WorldToViewportPoint(g.transform.position);
                bool onScreen = vp.z > 0f && vp.x > 0.02f && vp.x < 0.98f && vp.y > 0.02f && vp.y < 0.98f;
                if (onScreen) continue;

                var dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                if (vp.z < 0f) dir = -dir;
                if (dir.sqrMagnitude < 1e-5f) dir = Vector2.down;
                dir.Normalize();

                // точка на границе прямоугольника экрана с отступом
                var half = size * 0.5f - new Vector2(70f, 160f);
                float k = Mathf.Min(half.x / Mathf.Max(Mathf.Abs(dir.x), 1e-4f), half.y / Mathf.Max(Mathf.Abs(dir.y), 1e-4f));
                var pos = dir * k;

                var arrow = GetIndicator(used++);
                arrow.rectTransform.anchoredPosition = pos;
                arrow.rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                float dist = Vector3.Distance(cam.transform.position, g.transform.position);
                float pulse = dist < 1.6f ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 10f)) : 1f;
                var c = g.Def.Color;
                c.a = pulse;
                arrow.color = c;
                arrow.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.4f, 0.8f, Mathf.InverseLerp(1f, 4f, dist));
            }
            for (int i = used; i < indicators.Count; i++) indicators[i].gameObject.SetActive(false);
        }

        void ClearIndicators()
        {
            foreach (var i in indicators) i.gameObject.SetActive(false);
        }

        UILine GetIndicator(int i)
        {
            while (indicators.Count <= i)
            {
                var a = Rect(indicatorRoot, "Arrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<UILine>();
                a.Filled = true;
                a.raycastTarget = false;
                a.SetPoints(new[] { new Vector2(34, 0), new Vector2(-18, 26), new Vector2(-8, 0), new Vector2(-18, -26) });
                indicators.Add(a);
            }
            indicators[i].gameObject.SetActive(true);
            return indicators[i];
        }

        // ---------- кадр ----------

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (messageTimer > 0f)
            {
                messageTimer -= dt;
                var c = messageText.color;
                c.a = Mathf.Clamp01(messageTimer / 0.3f);
                messageText.color = c;
            }

            damageAlpha = Mathf.MoveTowards(damageAlpha, 0f, dt * 1.5f);
            var dc = damage.color;
            dc.a = damageAlpha;
            damage.color = dc;
            // лёгкая тряска интерфейса при уроне
            hud.transform.localPosition = damageAlpha > 0.3f ? (Vector3)(UnityEngine.Random.insideUnitCircle * 14f * damageAlpha) : Vector3.zero;

            if (strokeFade > 0f)
            {
                strokeFade -= dt;
                var sc = stroke.color;
                sc.a = Mathf.Clamp01(strokeFade / 0.5f);
                stroke.color = sc;
                if (strokeFade <= 0f) stroke.SetPoints(Array.Empty<Vector2>());
            }

            fpsFrames++;
            fpsTime += dt;
            if (fpsTime >= 0.5f)
            {
                fpsText.text = Mathf.RoundToInt(fpsFrames / fpsTime) + " fps";
                fpsFrames = 0;
                fpsTime = 0f;
            }
        }

        // ---------- построители ----------

        public static void SetSigilShape(UILine line, Sigil sigil, float r)
        {
            var pts = new List<Vector2>();
            switch (sigil)
            {
                case Sigil.Circle:
                    for (int i = 0; i < 20; i++)
                    {
                        float a = i / 20f * Mathf.PI * 2f;
                        pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                    }
                    line.Closed = true;
                    break;
                case Sigil.Triangle:
                    pts.Add(new Vector2(0, r));
                    pts.Add(new Vector2(r * 0.87f, -r * 0.5f));
                    pts.Add(new Vector2(-r * 0.87f, -r * 0.5f));
                    line.Closed = true;
                    break;
                case Sigil.Zigzag:
                    for (int i = 0; i < 5; i++) pts.Add(new Vector2(-r + i * r * 0.5f, i % 2 == 0 ? -r * 0.45f : r * 0.45f));
                    line.Closed = false;
                    break;
            }
            line.SetPoints(pts);
        }

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

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        GameObject Panel(Transform parent, string name)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.one);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.02f, 0.01f, 0.04f, 0.72f);
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
            o.effectColor = new Color(0, 0, 0, 0.85f);
            o.effectDistance = new Vector2(3, -3);
        }

        static void Frame(RectTransform parent, float w, float h, Color color, float thickness)
        {
            var f = Rect(parent, "Frame", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).gameObject.AddComponent<UILine>();
            f.Closed = true;
            f.Thickness = thickness;
            f.color = color;
            f.raycastTarget = false;
            float x = w * 0.5f, y = h * 0.5f;
            f.SetPoints(new[] { new Vector2(-x, y), new Vector2(x, y), new Vector2(x, -y), new Vector2(-x, -y) });
        }

        void Button(Transform parent, string text, Vector2 anchor, Action onClick)
        {
            var rt = Rect(parent, text, anchor, anchor);
            rt.sizeDelta = new Vector2(640, 150);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.75f, 0.1f, 0.15f);
            var btn = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            Frame(rt, 640, 150, Bone, 5f);
            var label = Label(rt, text, 54, new Vector2(0.5f, 0.5f), Vector2.zero, Bone);
            label.rectTransform.sizeDelta = new Vector2(620, 140);
            label.fontStyle = FontStyle.Bold;
        }
    }
}
