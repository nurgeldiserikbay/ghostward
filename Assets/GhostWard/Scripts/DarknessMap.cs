using UnityEngine;

namespace GhostWard
{
    // Карта темноты вокруг игрока: сектора по азимуту и высоте.
    // Камера телефона сама подстраивает экспозицию, поэтому храним не абсолютную яркость,
    // а яркость относительно среднего по тому же кадру: тёмный угол остаётся тёмным,
    // даже если камера его «высветлила».
    public class DarknessMap
    {
        public const int YawCells = 24;   // по 15°
        public const int PitchCells = 5;
        static readonly float[] PitchEdges = { -60f, -30f, -10f, 10f, 30f, 60f };

        readonly float[,] sum = new float[YawCells, PitchCells];
        readonly int[,] count = new int[YawCells, PitchCells];

        public bool Empty { get; private set; } = true;

        public void Clear()
        {
            System.Array.Clear(sum, 0, sum.Length);
            System.Array.Clear(count, 0, count.Length);
            Empty = true;
        }

        public void Add(Vector3 dir, float relativeLuma)
        {
            if (!Cell(dir, out int y, out int p)) return;
            sum[y, p] += relativeLuma;
            count[y, p]++;
            Empty = false;
        }

        // доля азимутов, увиденных хотя бы на уровне глаз
        public float Coverage
        {
            get
            {
                int seen = 0;
                for (int y = 0; y < YawCells; y++)
                    if (count[y, 1] + count[y, 2] + count[y, 3] > 0) seen++;
                return (float)seen / YawCells;
            }
        }

        // темнота сектора 0..1 относительно всей комнаты; -1 — не видели
        public float Darkness(int y, int p)
        {
            if (count[y, p] == 0) return -1f;
            MinMax(out float min, out float max);
            float l = sum[y, p] / count[y, p];
            return 1f - Mathf.Clamp01((l - min) / Mathf.Max(max - min, 1e-3f));
        }

        // самая тёмная высота в столбце азимута — для полоски отладки
        public float ColumnDarkness(int y)
        {
            float best = -1f;
            for (int p = 0; p < PitchCells; p++) best = Mathf.Max(best, Darkness(y, p));
            return best;
        }

        // вес полос высоты (снизу вверх) для каждого логова
        static float[] Bands(Lair lair) => lair switch
        {
            Lair.Floor => new[] { 1f, 1f, 0.2f, 0f, 0f },
            Lair.Ceiling => new[] { 0f, 0f, 0.1f, 0.6f, 1f },
            Lair.DarkCorner => new[] { 0.5f, 1f, 1f, 0.3f, 0.1f },
            Lair.Far => new[] { 0.1f, 0.6f, 1f, 0.5f, 0.1f },
            _ => new[] { 0.25f, 1f, 1f, 0.6f, 0.25f }
        };

        // Случайный сектор с перевесом в сторону самых тёмных (для Lair.Bright — самых светлых),
        // не ближе minAngle к направлению взгляда, с учётом высоты логова.
        public bool TryPick(Vector3 viewForward, float minAngle, Lair lair, out float yawDeg, out float pitchDeg)
        {
            yawDeg = pitchDeg = 0f;
            if (Empty) return false;
            float viewYaw = Mathf.Atan2(viewForward.x, viewForward.z) * Mathf.Rad2Deg;

            float total = 0f;
            var weights = new float[YawCells, PitchCells];
            var bands = Bands(lair);
            for (int y = 0; y < YawCells; y++)
            {
                float cy = CellYaw(y);
                if (Mathf.Abs(Mathf.DeltaAngle(cy, viewYaw)) < minAngle) continue;
                for (int p = 0; p < PitchCells; p++)
                {
                    float d = Darkness(y, p);
                    if (d < 0f) continue;
                    if (lair == Lair.Bright) d = 1f - d;
                    float w = (0.05f + d * d * d) * bands[p];
                    weights[y, p] = w;
                    total += w;
                }
            }
            if (total <= 0f) return false;

            float r = Random.value * total;
            for (int y = 0; y < YawCells; y++)
                for (int p = 0; p < PitchCells; p++)
                {
                    r -= weights[y, p];
                    if (r > 0f) continue;
                    yawDeg = CellYaw(y) + Random.Range(-5f, 5f);
                    pitchDeg = (PitchEdges[p] + PitchEdges[p + 1]) * 0.5f;
                    return true;
                }
            return false;
        }

        public static float CellYaw(int y) => (y + 0.5f) * (360f / YawCells) - 180f;

        static bool Cell(Vector3 dir, out int y, out int p)
        {
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            y = Mathf.Clamp(Mathf.FloorToInt((yaw + 180f) / (360f / YawCells)), 0, YawCells - 1);
            p = -1;
            for (int i = 0; i < PitchCells; i++)
                if (pitch >= PitchEdges[i] && pitch < PitchEdges[i + 1]) { p = i; break; }
            return p >= 0;
        }

        void MinMax(out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            for (int y = 0; y < YawCells; y++)
                for (int p = 0; p < PitchCells; p++)
                {
                    if (count[y, p] == 0) continue;
                    float l = sum[y, p] / count[y, p];
                    min = Mathf.Min(min, l);
                    max = Mathf.Max(max, l);
                }
        }
    }
}
