using System.Collections.Generic;
using UnityEngine;

namespace GhostWard
{
    // Распознаватель одиночных росчерков по алгоритму $1 Unistroke (Wobbrock и др., 2007).
    public static class SigilRecognizer
    {
        const int N = 64;
        const float Size = 250f;
        static readonly float HalfDiagonal = 0.5f * Mathf.Sqrt(2f * Size * Size);
        const float AngleRange = 45f * Mathf.Deg2Rad;
        const float AnglePrecision = 2f * Mathf.Deg2Rad;
        static readonly float Phi = 0.5f * (-1f + Mathf.Sqrt(5f));

        static readonly List<(Sigil sigil, Vector2[] points)> Templates = new();

        static SigilRecognizer()
        {
            var circle = new List<Vector2>();
            for (int i = 0; i <= 48; i++)
            {
                float a = Mathf.PI * 0.5f - i / 48f * Mathf.PI * 2f;
                circle.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
            }
            AddBothWays(Sigil.Circle, circle);

            AddBothWays(Sigil.Triangle, new List<Vector2>
            {
                new(0f, 1f), new(0.87f, -0.5f), new(-0.87f, -0.5f), new(0f, 1f)
            });

            AddBothWays(Sigil.Zigzag, new List<Vector2>
            {
                new(0f, 0f), new(1f, 1f), new(2f, 0f), new(3f, 1f), new(4f, 0f)
            });
            AddBothWays(Sigil.Zigzag, new List<Vector2>
            {
                new(0f, 0f), new(1f, 1f), new(2f, 0f), new(3f, 1f)
            });
        }

        static void AddBothWays(Sigil sigil, List<Vector2> pts)
        {
            Templates.Add((sigil, Normalize(Densify(pts))));
            var rev = new List<Vector2>(pts);
            rev.Reverse();
            Templates.Add((sigil, Normalize(Densify(rev))));
        }

        // Шаблоны заданы вершинами — перед ресэмплингом добавляем промежуточные точки.
        static List<Vector2> Densify(List<Vector2> pts)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < pts.Count - 1; i++)
                for (int k = 0; k < 16; k++)
                    result.Add(Vector2.Lerp(pts[i], pts[i + 1], k / 16f));
            result.Add(pts[^1]);
            return result;
        }

        public static Sigil Recognize(List<Vector2> stroke, out float score)
        {
            score = 0f;
            if (stroke.Count < 8) return Sigil.None;

            var candidate = Normalize(stroke);
            float best = float.MaxValue;
            Sigil bestSigil = Sigil.None;
            foreach (var (sigil, points) in Templates)
            {
                float d = DistanceAtBestAngle(candidate, points, -AngleRange, AngleRange, AnglePrecision);
                if (d < best) { best = d; bestSigil = sigil; }
            }
            score = 1f - best / HalfDiagonal;
            return bestSigil;
        }

        static Vector2[] Normalize(List<Vector2> pts)
        {
            var p = Resample(pts, N);
            float angle = IndicativeAngle(p);
            p = RotateBy(p, -angle);
            p = ScaleTo(p, Size);
            p = TranslateTo(p, Vector2.zero);
            return p;
        }

        static Vector2[] Resample(List<Vector2> input, int n)
        {
            var pts = new List<Vector2>(input);
            float interval = PathLength(pts) / (n - 1);
            float acc = 0f;
            var result = new List<Vector2> { pts[0] };
            for (int i = 1; i < pts.Count; i++)
            {
                float d = Vector2.Distance(pts[i - 1], pts[i]);
                if (acc + d >= interval && d > 0f)
                {
                    var q = pts[i - 1] + (interval - acc) / d * (pts[i] - pts[i - 1]);
                    result.Add(q);
                    pts.Insert(i, q);
                    acc = 0f;
                }
                else acc += d;
            }
            while (result.Count < n) result.Add(pts[^1]);
            if (result.Count > n) result.RemoveRange(n, result.Count - n);
            return result.ToArray();
        }

        static float IndicativeAngle(Vector2[] p)
        {
            var c = Centroid(p);
            return Mathf.Atan2(c.y - p[0].y, c.x - p[0].x);
        }

        static Vector2[] RotateBy(Vector2[] p, float radians)
        {
            var c = Centroid(p);
            float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            var r = new Vector2[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                var d = p[i] - c;
                r[i] = new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos) + c;
            }
            return r;
        }

        static Vector2[] ScaleTo(Vector2[] p, float size)
        {
            Vector2 min = p[0], max = p[0];
            foreach (var v in p) { min = Vector2.Min(min, v); max = Vector2.Max(max, v); }
            float sx = size / Mathf.Max(max.x - min.x, 1e-3f);
            float sy = size / Mathf.Max(max.y - min.y, 1e-3f);
            var r = new Vector2[p.Length];
            for (int i = 0; i < p.Length; i++) r[i] = new Vector2(p[i].x * sx, p[i].y * sy);
            return r;
        }

        static Vector2[] TranslateTo(Vector2[] p, Vector2 to)
        {
            var c = Centroid(p);
            var r = new Vector2[p.Length];
            for (int i = 0; i < p.Length; i++) r[i] = p[i] + to - c;
            return r;
        }

        static float DistanceAtBestAngle(Vector2[] pts, Vector2[] t, float a, float b, float threshold)
        {
            float x1 = Phi * a + (1f - Phi) * b;
            float f1 = DistanceAtAngle(pts, t, x1);
            float x2 = (1f - Phi) * a + Phi * b;
            float f2 = DistanceAtAngle(pts, t, x2);
            while (Mathf.Abs(b - a) > threshold)
            {
                if (f1 < f2) { b = x2; x2 = x1; f2 = f1; x1 = Phi * a + (1f - Phi) * b; f1 = DistanceAtAngle(pts, t, x1); }
                else { a = x1; x1 = x2; f1 = f2; x2 = (1f - Phi) * a + Phi * b; f2 = DistanceAtAngle(pts, t, x2); }
            }
            return Mathf.Min(f1, f2);
        }

        static float DistanceAtAngle(Vector2[] pts, Vector2[] t, float radians)
        {
            var r = RotateBy(pts, radians);
            float d = 0f;
            for (int i = 0; i < r.Length; i++) d += Vector2.Distance(r[i], t[i]);
            return d / r.Length;
        }

        static Vector2 Centroid(Vector2[] p)
        {
            var c = Vector2.zero;
            foreach (var v in p) c += v;
            return c / p.Length;
        }

        public static float PathLength(List<Vector2> p)
        {
            float d = 0f;
            for (int i = 1; i < p.Count; i++) d += Vector2.Distance(p[i - 1], p[i]);
            return d;
        }
    }
}
