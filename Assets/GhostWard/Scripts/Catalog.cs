using UnityEngine;

namespace GhostWard
{
    // Порядок важен: в этом порядке сущности открываются по мере роста опыта (Know.UnlockXp).
    public enum EntityKind
    {
        Ghost, Shade, Albasty, Poltergeist, Double, Kikimora, Upyr, Mara,
        Banshee, Domovoi, Changeling, BlackDog, Rusalka, Zheztyrnak, Llorona, Ghoul, Kitsune, CorpoSeco, MystanKempir, Jinn
    }

    // Как сущность держится в комнате: стоит на полу, висит под потолком, парит или держится на уровне глаз.
    public enum Stance { Floor, Ceiling, Float, Eye }

    public enum ItemKind { Salt, Lantern, Iron, Bell, Mirror, Wormwood }

    public enum Gesture { SwipeUp, Hold, DoubleTap, Shake, SwipeDown, Zigzag }

    // Улики: у каждой сущности ровно три, комбинации не повторяются.
    public enum Evidence { Frost, Whisper, Compass, Static, Knock, Radio, NightVision }

    // Где сущность устраивает логово — откуда идут улики и откуда она выходит на охоту.
    public enum Lair { DarkCorner, Floor, Behind, Ceiling, Bright, Near, Far }

    // Тексты берутся из Strings.cs по ключам вида e.<Kind>.<поле> — на текущем языке.
    public struct EntityDef
    {
        public EntityKind Kind;
        public ItemKind Bane;     // чего сущность боится
        public Color Body;        // цвет тела в шейдере
        public Color Accent;      // глаза и контур
        public float Height;      // м
        public Evidence[] Clues;  // три улики
        public Lair Lair;
        public Lure Lure;          // на какую приманку откликается
        public Stance Stance;

        // невидимые: выдают себя только звуком и брошенными вещами
        public bool Invisible => Kind is EntityKind.Poltergeist or EntityKind.Domovoi;

        string Key(string field) => Loc.T($"e.{Kind}.{field}");
        public string Name => Key("name");
        public string Origin => Key("origin");
        public string Lore => Key("lore");
        public string Signs => Key("signs");      // поведение на охоте
        public string Defense => Key("defense");
        public string Hint => Key("hint");        // подсказка при самой первой встрече
        public string LairText => Key("lair");    // где прячется
    }

    public struct ItemDef
    {
        public ItemKind Kind;
        public Gesture Gesture;
        public string Name => Loc.T($"i.{Kind}.name");
        public string Usage => Loc.T($"i.{Kind}.usage");
        public string Lore => Loc.T($"i.{Kind}.lore");
    }

    public struct EvidenceDef
    {
        public Evidence Kind;
        public string Name => Loc.T($"c.{Kind}.name");
        public string HowTo => Loc.T($"c.{Kind}.howto");
    }

    public static class Catalog
    {
        public static readonly EntityDef[] Entities =
        {
            new EntityDef { Kind = EntityKind.Ghost, Lure = Lure.Photo, Bane = ItemKind.Salt, Height = 1.2f, Stance = Stance.Float,
                Body = new Color(0.78f, 0.84f, 0.9f), Accent = new Color(0.55f, 0.75f, 1f),
                Clues = new[] { Evidence.Frost, Evidence.Whisper, Evidence.Radio }, Lair = Lair.Far },
            new EntityDef { Kind = EntityKind.Shade, Lure = Lure.MusicBox, Bane = ItemKind.Lantern, Height = 1.8f, Stance = Stance.Floor,
                Body = new Color(0.02f, 0.015f, 0.03f), Accent = new Color(1f, 0.2f, 0.12f),
                Clues = new[] { Evidence.NightVision, Evidence.Static, Evidence.Frost }, Lair = Lair.DarkCorner },
            new EntityDef { Kind = EntityKind.Albasty, Lure = Lure.Yarn, Bane = ItemKind.Iron, Height = 1.8f, Stance = Stance.Floor,
                Body = new Color(0.66f, 0.68f, 0.6f), Accent = new Color(0.95f, 0.85f, 0.35f),
                Clues = new[] { Evidence.Whisper, Evidence.Compass, Evidence.NightVision }, Lair = Lair.Behind },
            new EntityDef { Kind = EntityKind.Poltergeist, Lure = Lure.Meat, Bane = ItemKind.Bell, Height = 1.2f, Stance = Stance.Eye,
                Body = new Color(0.55f, 0.65f, 0.55f), Accent = new Color(0.4f, 1f, 0.5f),
                Clues = new[] { Evidence.Knock, Evidence.Static, Evidence.Compass }, Lair = Lair.Near },
            new EntityDef { Kind = EntityKind.Double, Lure = Lure.Photo, Bane = ItemKind.Mirror, Height = 1.75f, Stance = Stance.Floor,
                Body = new Color(0.08f, 0.07f, 0.08f), Accent = new Color(0.95f, 0.95f, 0.9f),
                Clues = new[] { Evidence.Radio, Evidence.NightVision, Evidence.Compass }, Lair = Lair.Bright },
            new EntityDef { Kind = EntityKind.Kikimora, Lure = Lure.Yarn, Bane = ItemKind.Wormwood, Height = 1.1f, Stance = Stance.Ceiling,
                Body = new Color(0.4f, 0.42f, 0.32f), Accent = new Color(0.85f, 1f, 0.3f),
                Clues = new[] { Evidence.Knock, Evidence.Frost, Evidence.Whisper }, Lair = Lair.Ceiling },
            new EntityDef { Kind = EntityKind.Upyr, Lure = Lure.Meat, Bane = ItemKind.Lantern, Height = 1.7f, Stance = Stance.Floor,
                Body = new Color(0.55f, 0.5f, 0.52f), Accent = new Color(1f, 0.1f, 0.1f),
                Clues = new[] { Evidence.Knock, Evidence.Static, Evidence.NightVision }, Lair = Lair.Floor },
            new EntityDef { Kind = EntityKind.Mara, Lure = Lure.MusicBox, Bane = ItemKind.Bell, Height = 1.3f, Stance = Stance.Floor,
                Body = new Color(0.25f, 0.24f, 0.3f), Accent = new Color(0.7f, 0.6f, 1f),
                Clues = new[] { Evidence.Radio, Evidence.Frost, Evidence.Compass }, Lair = Lair.Near },
            new EntityDef { Kind = EntityKind.Banshee, Lure = Lure.MusicBox, Bane = ItemKind.Iron, Height = 1.6f, Stance = Stance.Float,
                Body = new Color(0.8f, 0.82f, 0.85f), Accent = new Color(0.7f, 0.88f, 1f),
                Clues = new[] { Evidence.Whisper, Evidence.Radio, Evidence.Static }, Lair = Lair.Far },
            new EntityDef { Kind = EntityKind.Domovoi, Lure = Lure.Photo, Bane = ItemKind.Bell, Height = 1.0f, Stance = Stance.Floor,
                Body = new Color(0.36f, 0.3f, 0.24f), Accent = new Color(1f, 0.62f, 0.2f),
                Clues = new[] { Evidence.Knock, Evidence.Radio, Evidence.Compass }, Lair = Lair.Behind },
            new EntityDef { Kind = EntityKind.Changeling, Lure = Lure.MusicBox, Bane = ItemKind.Iron, Height = 0.9f, Stance = Stance.Floor,
                Body = new Color(0.6f, 0.6f, 0.5f), Accent = new Color(0.9f, 0.95f, 0.55f),
                Clues = new[] { Evidence.Whisper, Evidence.Knock, Evidence.Compass }, Lair = Lair.Near },
            new EntityDef { Kind = EntityKind.BlackDog, Lure = Lure.Meat, Bane = ItemKind.Lantern, Height = 1.0f, Stance = Stance.Floor,
                Body = new Color(0.03f, 0.028f, 0.032f), Accent = new Color(1f, 0.35f, 0.08f),
                Clues = new[] { Evidence.Knock, Evidence.NightVision, Evidence.Frost }, Lair = Lair.Floor },
            new EntityDef { Kind = EntityKind.Rusalka, Lure = Lure.Yarn, Bane = ItemKind.Wormwood, Height = 1.65f, Stance = Stance.Floor,
                Body = new Color(0.55f, 0.68f, 0.66f), Accent = new Color(0.4f, 0.95f, 0.85f),
                Clues = new[] { Evidence.Frost, Evidence.Radio, Evidence.Knock }, Lair = Lair.Bright },
            new EntityDef { Kind = EntityKind.Zheztyrnak, Lure = Lure.Yarn, Bane = ItemKind.Mirror, Height = 1.75f, Stance = Stance.Floor,
                Body = new Color(0.7f, 0.6f, 0.54f), Accent = new Color(1f, 0.55f, 0.22f),
                Clues = new[] { Evidence.Static, Evidence.Compass, Evidence.Frost }, Lair = Lair.Behind },
            new EntityDef { Kind = EntityKind.Llorona, Lure = Lure.Photo, Bane = ItemKind.Bell, Height = 1.7f, Stance = Stance.Floor,
                Body = new Color(0.86f, 0.84f, 0.8f), Accent = new Color(0.95f, 0.3f, 0.3f),
                Clues = new[] { Evidence.Whisper, Evidence.Frost, Evidence.NightVision }, Lair = Lair.Far },
            new EntityDef { Kind = EntityKind.Ghoul, Lure = Lure.Meat, Bane = ItemKind.Lantern, Height = 1.1f, Stance = Stance.Floor,
                Body = new Color(0.4f, 0.37f, 0.3f), Accent = new Color(0.95f, 0.8f, 0.2f),
                Clues = new[] { Evidence.Static, Evidence.NightVision, Evidence.Compass }, Lair = Lair.Floor },
            new EntityDef { Kind = EntityKind.Kitsune, Lure = Lure.Meat, Bane = ItemKind.Mirror, Height = 1.2f, Stance = Stance.Floor,
                Body = new Color(0.55f, 0.33f, 0.22f), Accent = new Color(0.45f, 0.8f, 1f),
                Clues = new[] { Evidence.Radio, Evidence.NightVision, Evidence.Whisper }, Lair = Lair.Near },
            new EntityDef { Kind = EntityKind.CorpoSeco, Lure = Lure.Meat, Bane = ItemKind.Salt, Height = 1.7f, Stance = Stance.Floor,
                Body = new Color(0.42f, 0.33f, 0.25f), Accent = new Color(0.95f, 0.92f, 0.65f),
                Clues = new[] { Evidence.Knock, Evidence.Compass, Evidence.NightVision }, Lair = Lair.DarkCorner },
            new EntityDef { Kind = EntityKind.MystanKempir, Lure = Lure.Yarn, Bane = ItemKind.Wormwood, Height = 1.4f, Stance = Stance.Floor,
                Body = new Color(0.3f, 0.28f, 0.25f), Accent = new Color(0.6f, 1f, 0.4f),
                Clues = new[] { Evidence.Whisper, Evidence.Static, Evidence.Knock }, Lair = Lair.DarkCorner },
            new EntityDef { Kind = EntityKind.Jinn, Lure = Lure.MusicBox, Bane = ItemKind.Salt, Height = 1.9f, Stance = Stance.Float,
                Body = new Color(0.24f, 0.2f, 0.32f), Accent = new Color(1f, 0.45f, 0.1f),
                Clues = new[] { Evidence.Compass, Evidence.Static, Evidence.Radio }, Lair = Lair.Ceiling },
        };

        public static readonly ItemDef[] Items =
        {
            new ItemDef { Kind = ItemKind.Salt, Gesture = Gesture.SwipeUp },
            new ItemDef { Kind = ItemKind.Lantern, Gesture = Gesture.Hold },
            new ItemDef { Kind = ItemKind.Iron, Gesture = Gesture.DoubleTap },
            new ItemDef { Kind = ItemKind.Bell, Gesture = Gesture.Shake },
            new ItemDef { Kind = ItemKind.Mirror, Gesture = Gesture.SwipeDown },
            new ItemDef { Kind = ItemKind.Wormwood, Gesture = Gesture.Zigzag },
        };

        public static readonly EvidenceDef[] Clues =
        {
            new EvidenceDef { Kind = Evidence.Frost },
            new EvidenceDef { Kind = Evidence.Whisper },
            new EvidenceDef { Kind = Evidence.Compass },
            new EvidenceDef { Kind = Evidence.Static },
            new EvidenceDef { Kind = Evidence.Knock },
            new EvidenceDef { Kind = Evidence.Radio },
            new EvidenceDef { Kind = Evidence.NightVision },
        };

        public static EntityDef Get(EntityKind k) => Entities[(int)k];
        public static ItemDef Get(ItemKind k) => Items[(int)k];
        public static EvidenceDef Get(Evidence e) => Clues[(int)e];

        public static bool Has(EntityDef def, Evidence e) => System.Array.IndexOf(def.Clues, e) >= 0;

        // запись в энциклопедии открывается после первой встречи и сохраняется между играми
        public static bool Seen(EntityKind k) => PlayerPrefs.GetInt("seen_" + k, 0) == 1;
        public static void MarkSeen(EntityKind k)
        {
            PlayerPrefs.SetInt("seen_" + k, 1);
            PlayerPrefs.Save();
        }
    }
}
