using UnityEngine;
using UnityEngine.UI;

namespace GhostWard
{
    /// <summary>
    /// Procedural branding emblem for the main menu. No SVG import package required.
    /// Matching editable vector master: docs/branding/GhostWard_Logo.svg
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

        static Texture2D GetTexture()
        {
            if (cached != null) return cached;
            const int n = 320;
            cached = new Texture2D(n, n, TextureFormat.RGBA32, false, true);
            cached.name = "GhostWardEmblemTexture";
            cached.wrapMode = TextureWrapMode.Clamp;
            cached.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[n * n];
            var a = new Vector2(-0.64f, 0.5f);
            var b = new Vector2(-0.40f, -0.53f);
            var c = new Vector2(-0.07f, 0.15f);
            var d = new Vector2(0.28f, -0.53f);
            var e = new Vector2(0.68f, 0.5f);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2((x + 0.5f - n / 2f) / (n * 0.49f),
                                    (y + 0.5f - n / 2f) / (n * 0.49f));
                float radius = p.magnitude;
                float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                // Missing pieces make the ward look fractured, not like a clean cartoon badge.
                bool fracture = (angle > 74f && angle < 94f) ||
                                (angle > -57f && angle < -42f) ||
                                (angle > -166f && angle < -150f);
                float ring = fracture ? 0 : Mathf.Clamp01((0.052f - Mathf.Abs(radius - 0.84f)) * 115f);
                float ward = Mathf.Min(Mathf.Min(Segment(p, a, b), Segment(p, b, c)),
                                       Mathf.Min(Segment(p, c, d), Segment(p, d, e)));
                float mark = Mathf.Clamp01((0.064f - ward) * 95f);
                float cut = Mathf.Clamp01((0.055f - Segment(p, new Vector2(0.01f, 0.56f),
                                                          new Vector2(0.09f, 0.22f))) * 85f);
                float texture = 0.83f + 0.17f * Mathf.Sin(x * 0.37f + y * 0.61f) *
                                                    Mathf.Sin(y * 0.17f - x * 0.29f);
                float silver = Mathf.Max(ring * 0.92f, mark) * texture;
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
    }
}
