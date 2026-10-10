using UnityEngine;

namespace GhostWard
{
    public enum GhostKind { Wraith, Shade, Poltergeist }

    public enum Sigil { None, Circle, Triangle, Zigzag }

    public struct GhostDef
    {
        public GhostKind Kind;
        public string Name;
        public Color Color;
        public Sigil Ward;
        public float Shape;      // форма силуэта в шейдере: 0 капюшон, 1 шар с одним глазом, 2 рваный
        public float Speed;      // м/с
        public float Pitch;      // высота голоса
    }

    public static class GhostCatalog
    {
        public static readonly GhostDef[] All =
        {
            new GhostDef { Kind = GhostKind.Wraith,      Name = "Призрак",     Color = new Color(1.00f, 0.22f, 0.25f), Ward = Sigil.Circle,   Shape = 0, Speed = 0.45f, Pitch = 0.8f },
            new GhostDef { Kind = GhostKind.Shade,       Name = "Тень",        Color = new Color(0.45f, 0.40f, 1.00f), Ward = Sigil.Triangle, Shape = 1, Speed = 0.32f, Pitch = 0.6f },
            new GhostDef { Kind = GhostKind.Poltergeist, Name = "Полтергейст", Color = new Color(0.30f, 1.00f, 0.55f), Ward = Sigil.Zigzag,   Shape = 2, Speed = 0.38f, Pitch = 1.3f },
        };

        public static GhostDef Get(GhostKind kind) => All[(int)kind];

        public static string SigilName(Sigil s) => s switch
        {
            Sigil.Circle => "Круг",
            Sigil.Triangle => "Треугольник",
            Sigil.Zigzag => "Молния",
            _ => "?"
        };
    }
}
