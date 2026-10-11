using UnityEngine;

namespace GhostWard
{
    public enum EntityKind { Ghost, Shade, Albasty, Poltergeist }

    public enum ItemKind { Salt, Lantern, Iron, Bell }

    public struct EntityDef
    {
        public EntityKind Kind;
        public string Name;
        public ItemKind Bane;     // чего сущность боится
        public Color Body;        // цвет тела в шейдере
        public Color Accent;      // глаза и контур
        public float Shape;       // 0 призрак, 1 тень, 2 полтергейст, 3 албасты
        public string Signs;      // признаки для дневника
        public string Hint;       // подсказка при первой встрече
    }

    public struct ItemDef
    {
        public ItemKind Kind;
        public string Name;
        public string Usage;
    }

    public static class Catalog
    {
        public static readonly EntityDef[] Entities =
        {
            new EntityDef
            {
                Kind = EntityKind.Ghost, Name = "Призрак", Bane = ItemKind.Salt, Shape = 0,
                Body = new Color(0.82f, 0.88f, 0.95f), Accent = new Color(0.55f, 0.75f, 1f),
                Signs = "Шёпот и холод. Тает и возникает в другом месте.",
                Hint = "Это призрак. Соль: выбери её и смахни пальцем вверх, глядя на него."
            },
            new EntityDef
            {
                Kind = EntityKind.Shade, Name = "Тень", Bane = ItemKind.Lantern, Shape = 1,
                Body = new Color(0.03f, 0.02f, 0.04f), Accent = new Color(1f, 0.25f, 0.15f),
                Signs = "Тяжёлое дыхание. Быстро движется в темноте, замирает на свету.",
                Hint = "Это тень. Фонарь: держи палец на экране и не выпускай её из луча."
            },
            new EntityDef
            {
                Kind = EntityKind.Albasty, Name = "Албасты", Bane = ItemKind.Iron, Shape = 3,
                Body = new Color(0.72f, 0.74f, 0.66f), Accent = new Color(0.9f, 0.85f, 0.4f),
                Signs = "Тихий смех. Приближается, только когда ты на неё не смотришь.",
                Hint = "Это албасты. Железо бьёт только вблизи: подпусти её и коснись экрана."
            },
            new EntityDef
            {
                Kind = EntityKind.Poltergeist, Name = "Полтергейст", Bane = ItemKind.Bell, Shape = 2,
                Body = new Color(0.6f, 0.7f, 0.6f), Accent = new Color(0.4f, 1f, 0.5f),
                Signs = "Стук и грохот. Его не видно, только слышно.",
                Hint = "Это полтергейст. Повернись на стук и встряхни телефон с колокольчиком."
            },
        };

        public static readonly ItemDef[] Items =
        {
            new ItemDef { Kind = ItemKind.Salt,    Name = "Соль",       Usage = "смахни вверх" },
            new ItemDef { Kind = ItemKind.Lantern, Name = "Фонарь",     Usage = "держи палец" },
            new ItemDef { Kind = ItemKind.Iron,    Name = "Железо",     Usage = "коснись вблизи" },
            new ItemDef { Kind = ItemKind.Bell,    Name = "Колокольчик", Usage = "встряхни" },
        };

        public static EntityDef Get(EntityKind k) => Entities[(int)k];
        public static ItemDef Get(ItemKind k) => Items[(int)k];

        // какие сущности приходят в какую ночь
        public static int KindsForNight(int night) => Mathf.Clamp(night + 1, 2, Entities.Length);
    }
}
