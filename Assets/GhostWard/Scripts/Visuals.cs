using System.Collections.Generic;
using UnityEngine;

namespace GhostWard
{
    // Текстуры для экранных эффектов: генерируются один раз при запуске.
    public static class Textures
    {
        static Texture2D vignette, glow;
        static readonly Dictionary<int, Texture2D> edges = new();

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
