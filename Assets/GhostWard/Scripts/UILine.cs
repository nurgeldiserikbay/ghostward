using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GhostWard
{
    // Ломаная с жёсткими гранями для UI: росчерк игрока, иконки знаков, стрелки-указатели.
    [RequireComponent(typeof(CanvasRenderer))]
    public class UILine : MaskableGraphic
    {
        public readonly List<Vector2> Points = new();
        public float Thickness = 10f;
        public bool Closed;
        public bool Filled;   // заливка выпуклого многоугольника вместо контура

        public void SetPoints(IEnumerable<Vector2> pts)
        {
            Points.Clear();
            Points.AddRange(pts);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Points.Count < 2) return;

            if (Filled)
            {
                var c = Vector2.zero;
                foreach (var p in Points) c += p;
                c /= Points.Count;
                vh.AddVert(c, color, Vector2.zero);
                foreach (var p in Points) vh.AddVert(p, color, Vector2.zero);
                for (int i = 0; i < Points.Count; i++)
                    vh.AddTriangle(0, 1 + i, 1 + (i + 1) % Points.Count);
                return;
            }

            int count = Closed ? Points.Count : Points.Count - 1;
            float h = Thickness * 0.5f;
            for (int i = 0; i < count; i++)
            {
                var a = Points[i];
                var b = Points[(i + 1) % Points.Count];
                var dir = b - a;
                if (dir.sqrMagnitude < 1e-4f) continue;
                dir.Normalize();
                var n = new Vector2(-dir.y, dir.x) * h;
                // продлеваем сегмент на полтолщины, чтобы стыки закрывались без скруглений
                var a2 = a - dir * h;
                var b2 = b + dir * h;
                int idx = vh.currentVertCount;
                vh.AddVert(a2 - n, color, Vector2.zero);
                vh.AddVert(a2 + n, color, Vector2.zero);
                vh.AddVert(b2 + n, color, Vector2.zero);
                vh.AddVert(b2 - n, color, Vector2.zero);
                vh.AddTriangle(idx, idx + 1, idx + 2);
                vh.AddTriangle(idx, idx + 2, idx + 3);
            }
        }
    }
}
