using System.Collections.Generic;
using UnityEngine;

namespace GhostWard
{
    // Текстуры для экранных эффектов: генерируются один раз при запуске.
    public static class Textures
    {
        static Texture2D vignette, glow;
        static readonly Dictionary<int, Texture2D> edges = new();

        static Texture2D noise;

        // зерно для наложения поверх экрана; повторяется плиткой
        public static Texture2D Noise()
        {
            if (noise != null) return noise;
            const int s = 128;
            noise = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var px = new Color32[s * s];
            var rng = new System.Random(11);
            for (int i = 0; i < px.Length; i++)
            {
                byte v = (byte)rng.Next(0, 256);
                px[i] = new Color32(v, v, v, (byte)rng.Next(80, 256));
            }
            noise.SetPixels32(px);
            noise.Apply();
            return noise;
        }

        public static Texture2D Vignette() => vignette ??= Radial(128, d => Mathf.Pow(Mathf.Clamp01((d - 0.35f) / 0.85f), 1.6f));

        // тёплое пятно света фонаря в центре
        public static Texture2D Glow() => glow ??= Radial(128, d => Mathf.Pow(Mathf.Clamp01(1f - d / 0.75f), 2f));

        // градиент инея для края экрана, с мелким «кристаллическим» шумом
        public static Texture2D EdgeGradient(bool vertical = false, bool flip = false)
        {
            int key = (vertical ? 1 : 0) + (flip ? 2 : 0);
            if (edges.TryGetValue(key, out var t)) return t;

            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            var rng = new System.Random(5);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float k = (vertical ? y : x) / (float)(s - 1);
                    if (flip) k = 1f - k;
                    float a = Mathf.Pow(1f - k, 2.2f) * (0.75f + 0.25f * (float)rng.NextDouble());
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return edges[key] = tex;
        }

        static Texture2D Radial(int s, System.Func<float, float> alpha)
        {
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                    float a = alpha(Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }

    // Иконки предметов ломаными с жёсткими гранями.
    public static class Icons
    {
        public static void Draw(RectTransform parent, ItemKind kind, Color color)
        {
            switch (kind)
            {
                case ItemKind.Salt:
                    var mound = new List<Vector2>();
                    for (int i = 0; i <= 10; i++)
                    {
                        float a = i / 10f * Mathf.PI;
                        mound.Add(new Vector2(Mathf.Cos(a) * 58f, Mathf.Sin(a) * 38f - 34f));
                    }
                    Poly(parent, color, mound);
                    foreach (var g in new[] { new Vector2(-30, 30), new Vector2(4, 44), new Vector2(34, 26), new Vector2(-8, 18), new Vector2(18, 58) })
                        Poly(parent, color, Diamond(g, 6f));
                    break;

                case ItemKind.Lantern:
                    Line(parent, color, 6f, true, new Vector2(-28, -44), new Vector2(28, -44), new Vector2(28, 26), new Vector2(-28, 26));
                    Poly(parent, color, new List<Vector2> { new(-36, 26), new(36, 26), new(20, 42), new(-20, 42) });
                    Line(parent, color, 5f, false, new Vector2(-16, 42), new Vector2(-12, 60), new Vector2(12, 60), new Vector2(16, 42));
                    Poly(parent, new Color(1f, 0.75f, 0.4f), Diamond(new Vector2(0, -10), 14f, 22f));
                    break;

                case ItemKind.Iron:
                    Poly(parent, color, new List<Vector2> { new(-9, -8), new(9, -8), new(7, 48), new(0, 64), new(-7, 48) });
                    Line(parent, color, 9f, false, new Vector2(-24, -12), new Vector2(24, -12));
                    Line(parent, color, 13f, false, new Vector2(0, -16), new Vector2(0, -58));
                    break;

                case ItemKind.Bell:
                    Poly(parent, color, new List<Vector2>
                    {
                        new(-14, 40), new(14, 40), new(26, 12), new(32, -22), new(42, -32), new(-42, -32), new(-32, -22), new(-26, 12)
                    });
                    var loop = new List<Vector2>();
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i / 10f * Mathf.PI * 2f;
                        loop.Add(new Vector2(Mathf.Cos(a) * 9f, Mathf.Sin(a) * 9f + 50f));
                    }
                    Line(parent, color, 5f, true, loop.ToArray());
                    Poly(parent, color, Diamond(new Vector2(0, -44), 9f));
                    break;
            }
        }

        public static void Item(RectTransform parent, ItemKind kind, Color color)
        {
            switch (kind)
            {
                case ItemKind.Mirror:
                    var oval = new List<Vector2>();
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i / 16f * Mathf.PI * 2f;
                        oval.Add(new Vector2(Mathf.Cos(a) * 30f, Mathf.Sin(a) * 40f + 14f));
                    }
                    Line(parent, color, 6f, true, oval.ToArray());
                    Line(parent, color, 10f, false, new Vector2(0, -26), new Vector2(0, -62));
                    Line(parent, color, 4f, false, new Vector2(-12, 30), new Vector2(4, 44));
                    break;
                case ItemKind.Wormwood:
                    for (int i = -2; i <= 2; i++)
                    {
                        var top = new Vector2(i * 13f, 58f - Mathf.Abs(i) * 8f);
                        Line(parent, color, 4f, false, new Vector2(0, -60), new Vector2(i * 3f, -20), top);
                        Poly(parent, color, Diamond(top + new Vector2(i * 4f, -14f), 6f, 10f));
                    }
                    Line(parent, color, 8f, false, new Vector2(-12, -34), new Vector2(12, -34));
                    break;
                default:
                    Draw(parent, kind, color);
                    break;
            }
        }

        // как вызвать предмет: схема жеста
        public static void GestureIcon(RectTransform parent, Gesture g, Color color)
        {
            var finger = Diamond(Vector2.zero, 12f);
            switch (g)
            {
                case Gesture.SwipeUp:
                case Gesture.SwipeDown:
                    float s = g == Gesture.SwipeUp ? 1f : -1f;
                    Poly(parent, color, Diamond(new Vector2(0, -40 * s), 12f));
                    Line(parent, color, 6f, false, new Vector2(0, -26 * s), new Vector2(0, 44 * s));
                    Poly(parent, color, new List<Vector2> { new(0, 60 * s), new(-16, 36 * s), new(16, 36 * s) });
                    break;
                case Gesture.Hold:
                    Poly(parent, color, finger);
                    Line(parent, color, 4f, true, Ring(28f, 12));
                    Line(parent, color, 3f, true, Ring(46f, 16));
                    break;
                case Gesture.DoubleTap:
                    Poly(parent, color, Diamond(new Vector2(-22, 0), 11f));
                    Poly(parent, color, Diamond(new Vector2(22, 0), 11f));
                    Line(parent, color, 3f, true, Ring(54f, 16));
                    break;
                case Gesture.Shake:
                    Line(parent, color, 5f, true, new Vector2(-18, -38), new Vector2(18, -38), new Vector2(18, 38), new Vector2(-18, 38));
                    Line(parent, color, 4f, false, new Vector2(-32, 22), new Vector2(-42, 0), new Vector2(-32, -22));
                    Line(parent, color, 4f, false, new Vector2(32, 22), new Vector2(42, 0), new Vector2(32, -22));
                    Line(parent, color, 3f, false, new Vector2(-48, 34), new Vector2(-60, 0), new Vector2(-48, -34));
                    Line(parent, color, 3f, false, new Vector2(48, 34), new Vector2(60, 0), new Vector2(48, -34));
                    break;
                case Gesture.Zigzag:
                    Line(parent, color, 6f, false, new Vector2(-50, -20), new Vector2(-25, 20), new Vector2(0, -20), new Vector2(25, 20), new Vector2(46, -12));
                    Poly(parent, color, new List<Vector2> { new(60, -32), new(34, -20), new(52, -2) });
                    break;
            }
        }

        // приборы поиска
        public static void ToolIcon(RectTransform parent, Tool t, Color color)
        {
            switch (t)
            {
                case Tool.Emf:
                    Line(parent, color, 5f, true, new Vector2(-30, -44), new Vector2(30, -44), new Vector2(30, 34), new Vector2(-30, 34));
                    for (int k = 0; k < 5; k++)
                        Poly(parent, color, new List<Vector2> { new(-22 + k * 11, 22), new(-15 + k * 11, 22), new(-15 + k * 11, 8 - k * 3), new(-22 + k * 11, 8 - k * 3) });
                    Line(parent, color, 4f, false, new Vector2(0, 34), new Vector2(0, 52));
                    break;
                case Tool.Thermo:
                    Line(parent, color, 5f, true, new Vector2(-9, -20), new Vector2(-9, 46), new Vector2(9, 46), new Vector2(9, -20));
                    Poly(parent, color, new List<Vector2>(ShiftRing(new Vector2(0, -32), 17f, 12)));
                    Line(parent, color, 3f, false, new Vector2(12, 30), new Vector2(22, 30));
                    Line(parent, color, 3f, false, new Vector2(12, 14), new Vector2(22, 14));
                    Line(parent, color, 3f, false, new Vector2(12, -2), new Vector2(22, -2));
                    break;
                case Tool.Night:
                    Line(parent, color, 5f, true, new Vector2(-40, -26), new Vector2(30, -26), new Vector2(30, 26), new Vector2(-40, 26));
                    Line(parent, color, 5f, true, new Vector2(30, -12), new Vector2(48, -24), new Vector2(48, 24), new Vector2(30, 12));
                    Line(parent, color, 4f, true, ShiftRing(new Vector2(-6, 0), 13f, 12));
                    Poly(parent, new Color(0.4f, 1f, 0.5f), Diamond(new Vector2(-30, 16), 4f));
                    break;
                case Tool.Radio:
                    Clue(parent, Evidence.Radio, color);
                    break;
            }
        }

        // средства защиты
        public static void ProtectIcon(RectTransform parent, Protect p, Color color)
        {
            switch (p)
            {
                case Protect.Circle:
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i / 12f * Mathf.PI * 2f;
                        Poly(parent, color, Diamond(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 40f, 6f));
                    }
                    break;
                case Protect.Candle:
                    Line(parent, color, 5f, true, new Vector2(-14, -46), new Vector2(14, -46), new Vector2(14, 14), new Vector2(-14, 14));
                    Line(parent, color, 3f, false, new Vector2(0, 14), new Vector2(0, 22));
                    Poly(parent, new Color(1f, 0.75f, 0.4f), Diamond(new Vector2(0, 36), 10f, 16f));
                    break;
                case Protect.Tumar:
                    Line(parent, color, 3f, false, new Vector2(-28, 44), new Vector2(0, 20), new Vector2(28, 44));
                    Line(parent, color, 6f, true, new Vector2(0, 20), new Vector2(34, -36), new Vector2(-34, -36));
                    Poly(parent, color, Diamond(new Vector2(0, -12), 8f));
                    break;
                case Protect.Calm:
                    Line(parent, color, 6f, true, new Vector2(-38, -8), new Vector2(18, -8), new Vector2(30, 0), new Vector2(18, 8), new Vector2(-38, 8));
                    Poly(parent, color, new List<Vector2> { new(-38, -8), new(-6, -8), new(-6, 8), new(-38, 8) });
                    break;
            }
        }

        public static void Bag(RectTransform parent, Color color)
        {
            Line(parent, color, 5f, true, new Vector2(-38, -36), new Vector2(38, -36), new Vector2(30, 22), new Vector2(-30, 22));
            Line(parent, color, 5f, false, new Vector2(-16, 22), new Vector2(-12, 42), new Vector2(12, 42), new Vector2(16, 22));
            Poly(parent, color, Diamond(new Vector2(0, -6), 8f));
        }

        // значки улик для дневника
        public static void Clue(RectTransform parent, Evidence e, Color color)
        {
            switch (e)
            {
                case Evidence.Frost:
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 46f;
                        Line(parent, color, 5f, false, -d, d);
                        var n = new Vector2(-d.y, d.x).normalized * 12f;
                        Line(parent, color, 4f, false, d * 0.6f + n, d * 0.8f, d * 0.6f - n);
                        Line(parent, color, 4f, false, -d * 0.6f + n, -d * 0.8f, -d * 0.6f - n);
                    }
                    break;
                case Evidence.Whisper:
                    Poly(parent, color, Diamond(new Vector2(-34, 0), 9f));
                    for (int k = 1; k <= 3; k++)
                    {
                        float r = 14f + k * 16f;
                        Line(parent, color, 4f, false, new Vector2(-34 + r * 0.5f, r * 0.8f), new Vector2(-34 + r, 0), new Vector2(-34 + r * 0.5f, -r * 0.8f));
                    }
                    break;
                case Evidence.Compass:
                    Line(parent, color, 4f, true, Ring(44f, 16));
                    Poly(parent, new Color(1f, 0.4f, 0.3f), new List<Vector2> { new(0, 36), new(9, 0), new(-9, 0) });
                    Poly(parent, color, new List<Vector2> { new(0, -36), new(-9, 0), new(9, 0) });
                    break;
                case Evidence.Static:
                    Line(parent, color, 5f, true, new Vector2(-46, -34), new Vector2(46, -34), new Vector2(46, 34), new Vector2(-46, 34));
                    Line(parent, color, 4f, false, new Vector2(-36, 0), new Vector2(-22, 18), new Vector2(-8, -16), new Vector2(6, 20), new Vector2(20, -14), new Vector2(36, 6));
                    break;
                case Evidence.Knock:
                    for (int k = -1; k <= 1; k++)
                        Line(parent, color, 9f, false, new Vector2(k * 26, -22), new Vector2(k * 26, 22));
                    Line(parent, color, 3f, false, new Vector2(-50, 30), new Vector2(-40, 22));
                    Line(parent, color, 3f, false, new Vector2(50, 30), new Vector2(40, 22));
                    break;
                case Evidence.Radio:
                    Line(parent, color, 5f, true, new Vector2(-44, -30), new Vector2(44, -30), new Vector2(44, 22), new Vector2(-44, 22));
                    Line(parent, color, 4f, false, new Vector2(20, 22), new Vector2(40, 52));
                    Line(parent, color, 4f, true, ShiftRing(new Vector2(-18, -4), 14f, 12));
                    for (int k = 0; k < 3; k++) Line(parent, color, 3f, false, new Vector2(10, 8 - k * 12), new Vector2(34, 8 - k * 12));
                    break;
                case Evidence.NightVision:
                    Line(parent, color, 5f, false, new Vector2(-48, 0), new Vector2(-24, 22), new Vector2(24, 22), new Vector2(48, 0));
                    Line(parent, color, 5f, false, new Vector2(-48, 0), new Vector2(-24, -22), new Vector2(24, -22), new Vector2(48, 0));
                    Poly(parent, color, Diamond(Vector2.zero, 12f));
                    Line(parent, color, 2f, false, new Vector2(-40, -34), new Vector2(40, -34));
                    Line(parent, color, 2f, false, new Vector2(-40, 34), new Vector2(40, 34));
                    break;
            }
        }

        static Vector2[] ShiftRing(Vector2 c, float r, int n)
        {
            var pts = Ring(r, n);
            for (int i = 0; i < pts.Length; i++) pts[i] += c;
            return pts;
        }

        static Vector2[] Ring(float r, int n)
        {
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return pts;
        }

        // раскрытая книга — кнопка дневника
        public static void Book(RectTransform parent, Color color)
        {
            Line(parent, color, 5f, true, new Vector2(0, -30), new Vector2(-46, -22), new Vector2(-46, 34), new Vector2(0, 26));
            Line(parent, color, 5f, true, new Vector2(0, -30), new Vector2(46, -22), new Vector2(46, 34), new Vector2(0, 26));
            Line(parent, color, 3f, false, new Vector2(-36, 14), new Vector2(-10, 10));
            Line(parent, color, 3f, false, new Vector2(-36, 0), new Vector2(-10, -4));
            Line(parent, color, 3f, false, new Vector2(10, 10), new Vector2(36, 14));
            Line(parent, color, 3f, false, new Vector2(10, -4), new Vector2(36, 0));
        }

        static List<Vector2> Diamond(Vector2 c, float w, float h = -1f)
        {
            if (h < 0f) h = w;
            return new List<Vector2> { c + new Vector2(0, h), c + new Vector2(w, 0), c + new Vector2(0, -h), c + new Vector2(-w, 0) };
        }

        static UILine Make(RectTransform parent, Color color)
        {
            var go = new GameObject("Part", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<UILine>();
            l.color = color;
            l.raycastTarget = false;
            return l;
        }

        static void Poly(RectTransform parent, Color color, List<Vector2> pts)
        {
            var l = Make(parent, color);
            l.Filled = true;
            l.SetPoints(pts);
        }

        static void Line(RectTransform parent, Color color, float thickness, bool closed, params Vector2[] pts)
        {
            var l = Make(parent, color);
            l.Thickness = thickness;
            l.Closed = closed;
            l.SetPoints(pts);
        }
    }
}
