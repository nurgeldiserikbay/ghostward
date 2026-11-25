using UnityEngine;
using UnityEngine.UI;

namespace GhostWard
{
    /// <summary>
    /// Procedural branding emblem for the main menu. No SVG import package required.
    /// Matching editable vector master: docs/branding/GhostWard_Logo.svg
    /// The same emblem, in night-vision green, is the app icon (RenderIcon, used by ProjectSetup).
    /// </summary>
    public static class WardLogo
    {
        static Texture2D cached;

        public static void AddTo(Transform parent)
        {
            var go = new GameObject("GhostWardEmblem", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.91f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(198f, 198f);
            var image = go.GetComponent<RawImage>();
            image.texture = GetTexture();
            image.raycastTarget = false;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ba = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ba) / Vector2.Dot(ba, ba));
            return Vector2.Distance(p, a + t * ba);
        }

        static readonly Vector2 A = new Vector2(-0.64f, 0.5f), B = new Vector2(-0.40f, -0.53f),
                                C = new Vector2(-0.07f, 0.15f), D = new Vector2(0.28f, -0.53f),
                                E = new Vector2(0.68f, 0.5f);

        // p — точка в координатах эмблемы (радиус кольца 0.84). shape — кольцо и знак «W», cut — красная черта.
        static void Emblem(Vector2 p, int x, int y, out float shape, out float cut)
        {
            float radius = p.magnitude;
            float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
            // Missing pieces make the ward look fractured, not like a clean cartoon badge.
            bool fracture = (angle > 74f && angle < 94f) ||
                            (angle > -57f && angle < -42f) ||
                            (angle > -166f && angle < -150f);
            float ring = fracture ? 0 : Mathf.Clamp01((0.052f - Mathf.Abs(radius - 0.84f)) * 115f);
            float ward = Mathf.Min(Mathf.Min(Segment(p, A, B), Segment(p, B, C)),
                                   Mathf.Min(Segment(p, C, D), Segment(p, D, E)));
            float mark = Mathf.Clamp01((0.064f - ward) * 95f);
            cut = Mathf.Clamp01((0.055f - Segment(p, new Vector2(0.01f, 0.56f),
                                                    new Vector2(0.09f, 0.22f))) * 85f);
            float texture = 0.83f + 0.17f * Mathf.Sin(x * 0.37f + y * 0.61f) *
                                                Mathf.Sin(y * 0.17f - x * 0.29f);
            shape = Mathf.Max(ring * 0.92f, mark) * texture;
        }

        static Texture2D GetTexture()
        {
            if (cached != null) return cached;
            const int n = 320;
            cached = new Texture2D(n, n, TextureFormat.RGBA32, false, true);
            cached.name = "GhostWardEmblemTexture";
            cached.wrapMode = TextureWrapMode.Clamp;
            cached.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2((x + 0.5f - n / 2f) / (n * 0.49f),
                                    (y + 0.5f - n / 2f) / (n * 0.49f));
                Emblem(p, x, y, out float silver, out float cut);
                float red = cut * 0.85f;
                float alpha = Mathf.Clamp01(silver + red);
                byte al = (byte)(alpha * 255);
                pixels[y * n + x] = new Color32(
                    (byte)(Mathf.Clamp01((silver * 0.72f + red * 0.64f) / Mathf.Max(alpha, 0.001f)) * 255),
                    (byte)(Mathf.Clamp01((silver * 0.80f + red * 0.10f) / Mathf.Max(alpha, 0.001f)) * 255),
                    (byte)(Mathf.Clamp01((silver * 0.78f + red * 0.10f) / Mathf.Max(alpha, 0.001f)) * 255),
                    al);
            }
            cached.SetPixels32(pixels);
            cached.Apply(false, true);
            return cached;
        }

        // Иконка приложения: эмблема в зелёном ночном видоискателе, непрозрачная, n×n.
        // Всё важное — в центральных ~66%, чтобы круглая маска лаунчера ничего не срезала.
        public static Texture2D RenderIcon(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            var bgDeep = new Color(0.010f, 0.045f, 0.022f);
            var bgGlow = new Color(0.045f, 0.165f, 0.080f);
            var phosphor = new Color(0.49f, 1f, 0.54f);
            var bone = new Color(0.72f, 0.93f, 0.72f);
            var rec = new Color(0.92f, 0.12f, 0.10f);
            float s = n / 1024f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // -1..1 по иконке
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                // фон: свечение в центре, виньетка, строки развёртки и зерно
                var c = Color.Lerp(bgGlow, bgDeep, Mathf.Clamp01(r * 0.95f));
                float scan = (y / Mathf.Max(1, (int)(4 * s))) % 2 == 0 ? 1f : 0.86f;
                float grain = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
                grain = (grain - Mathf.Floor(grain)) * 0.06f - 0.03f;
                c = c * scan + new Color(grain, grain * 1.4f, grain);

                // уголки видоискателя
                float inset = 0.56f, len = 0.17f, w = 0.022f;
                float au = Mathf.Abs(u), av = Mathf.Abs(v);
                bool hor = Mathf.Abs(av - inset) < w && au > inset - len && au < inset + w;
                bool ver = Mathf.Abs(au - inset) < w && av > inset - len && av < inset + w;
                if (hor || ver) c = Color.Lerp(c, bone, 0.85f);

                // точка REC слева сверху, внутри рамки
                float dx = u + 0.40f, dy = v - 0.40f;
                float dot = Mathf.Clamp01((0.045f - Mathf.Sqrt(dx * dx + dy * dy)) * 200f / Mathf.Max(s, 0.25f));
                c = Color.Lerp(c, rec, dot);

                // эмблема: радиус кольца 0.84 → 0.40 иконки
                var p = new Vector2(u, v) / 0.48f;
                Emblem(p, (int)(x / s), (int)(y / s), out float shape, out float cut);
                // мягкое свечение вокруг знака
                float halo = Mathf.Clamp01(0.10f - Mathf.Abs(p.magnitude - 0.84f)) * 1.4f;
                c += phosphor * halo * 0.25f;
                c = Color.Lerp(c, phosphor, shape);
                c = Color.Lerp(c, rec, cut * 0.9f);

                c.a = 1f;
                px[y * n + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }
    }
}
