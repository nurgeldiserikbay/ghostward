using UnityEngine;

namespace GhostWard
{
    // Порядок важен: сущности открываются по мере того, сколько игрок продержался.
    public enum EntityKind { Ghost, Shade, Albasty, Poltergeist, Double, Kikimora, Upyr, Mara }

    public enum ItemKind { Salt, Lantern, Iron, Bell, Mirror, Wormwood }

    public enum Gesture { SwipeUp, Hold, DoubleTap, Shake, SwipeDown, Zigzag }

    public struct EntityDef
    {
        public EntityKind Kind;
        public string Name;
        public ItemKind Bane;     // чего сущность боится
        public Color Body;        // цвет тела в шейдере
        public Color Accent;      // глаза и контур
        public float Height;      // м
        public string Origin;     // откуда легенда
        public string Lore;       // статья энциклопедии
        public string Signs;      // как узнать
        public string Defense;    // как защититься
        public string Hint;       // подсказка при самой первой встрече
    }

    public struct ItemDef
    {
        public ItemKind Kind;
        public string Name;
        public Gesture Gesture;
        public string Usage;      // описание жеста
        public string Lore;
    }

    public static class Catalog
    {
        public static readonly EntityDef[] Entities =
        {
            new EntityDef
            {
                Kind = EntityKind.Ghost, Name = "Призрак", Bane = ItemKind.Salt, Height = 1.2f,
                Body = new Color(0.78f, 0.84f, 0.9f), Accent = new Color(0.55f, 0.75f, 1f),
                Origin = "Повсеместно",
                Lore = "Душа, которая не ушла. Привязана к месту, где умерла, и не понимает, что её время кончилось. Тянется к живым, чтобы согреться, — и забирает тепло.",
                Signs = "Шёпот, внезапный холод. Тает и возникает ближе, в другом месте.",
                Defense = "Соль — граница, которую мёртвые не переступают. Брось горсть прямо в него.",
                Hint = "Это призрак. Он боится соли: смахни пальцем вверх, глядя на него."
            },
            new EntityDef
            {
                Kind = EntityKind.Shade, Name = "Тень", Bane = ItemKind.Lantern, Height = 1.8f,
                Body = new Color(0.02f, 0.015f, 0.03f), Accent = new Color(1f, 0.2f, 0.12f),
                Origin = "Свидетельства по всему миру",
                Lore = "Человеческий силуэт без лица, который видят краем глаза. Живёт в темноте между мебелью и стеной. Свет для неё — как огонь.",
                Signs = "Тяжёлое дыхание. В темноте движется быстро, на свету замирает.",
                Defense = "Свет. Включи фонарь и держи её в луче, пока не сгорит.",
                Hint = "Это тень. Она боится света: зажми палец — фонарь — и держи её в луче."
            },
            new EntityDef
            {
                Kind = EntityKind.Albasty, Name = "Албасты", Bane = ItemKind.Iron, Height = 1.8f,
                Body = new Color(0.66f, 0.68f, 0.6f), Accent = new Color(0.95f, 0.85f, 0.35f),
                Origin = "Казахский и тюркский фольклор",
                Lore = "Женщина с длинными спутанными волосами и вывернутыми ступнями. Приходит к спящим и к роженицам, садится на грудь и душит. Издавна от неё кладут под подушку нож или иглу.",
                Signs = "Тихий смех. Подходит, только когда на неё не смотрят. Под взглядом замирает.",
                Defense = "Железо, но только вплотную. Не отводи взгляд, дай ей подойти и ударь.",
                Hint = "Это албасты. Она боится железа: подпусти вплотную и коснись экрана дважды."
            },
            new EntityDef
            {
                Kind = EntityKind.Poltergeist, Name = "Полтергейст", Bane = ItemKind.Bell, Height = 1.2f,
                Body = new Color(0.55f, 0.65f, 0.55f), Accent = new Color(0.4f, 1f, 0.5f),
                Origin = "Европейские хроники",
                Lore = "«Шумный дух». Его никто не видел — только слышали стук, видели летящую посуду и сдвинутую мебель. С каждым разом бьёт сильнее.",
                Signs = "Стук и грохот с одной стороны. Невидим.",
                Defense = "Колокольчик. Повернись туда, откуда стучит, и позвони.",
                Hint = "Это полтергейст. Он боится колокольчика: повернись на стук и встряхни телефон."
            },
            new EntityDef
            {
                Kind = EntityKind.Double, Name = "Двойник", Bane = ItemKind.Mirror, Height = 1.75f,
                Body = new Color(0.08f, 0.07f, 0.08f), Accent = new Color(0.95f, 0.95f, 0.9f),
                Origin = "Германский и славянский фольклор",
                Lore = "Встретить своего двойника — к беде. Он повторяет каждое твоё движение и ждёт, когда ты ошибёшься, чтобы занять твоё место. Своего отражения не выносит.",
                Signs = "Тёмная фигура с широкой улыбкой. Подходит, только когда ты двигаешься. Стой — и он стоит.",
                Defense = "Зеркало. Повернись к нему и покажи отражение.",
                Hint = "Это двойник. Он движется, пока движешься ты. Боится зеркала: смахни пальцем вниз."
            },
            new EntityDef
            {
                Kind = EntityKind.Kikimora, Name = "Кикимора", Bane = ItemKind.Wormwood, Height = 1.1f,
                Body = new Color(0.4f, 0.42f, 0.32f), Accent = new Color(0.85f, 1f, 0.3f),
                Origin = "Славянский фольклор",
                Lore = "Маленькая, тощая, с длинными пальцами. Ночью бегает по потолку и стенам, прядёт, путает волосы спящим. Не выносит запаха полыни и чертополоха.",
                Signs = "Скрежет сверху. Ползёт по потолку и падает сверху, когда рядом.",
                Defense = "Полынь. Помаши ею перед кикиморой — дым её выгонит.",
                Hint = "Это кикимора. Смотри наверх! Боится полыни: проведи пальцем зигзагом влево-вправо."
            },
            new EntityDef
            {
                Kind = EntityKind.Upyr, Name = "Упырь", Bane = ItemKind.Lantern, Height = 1.7f,
                Body = new Color(0.55f, 0.5f, 0.52f), Accent = new Color(1f, 0.1f, 0.1f),
                Origin = "Славянский фольклор",
                Lore = "Мертвец, вставший из могилы за кровью. Ходит рывками: замер — бросок — замер. Боится света, как первого луча солнца.",
                Signs = "Хрип. Двигается рывками, бросается и замирает.",
                Defense = "Свет. Держи в луче фонаря дольше, чем тень, — упырь крепче.",
                Hint = "Это упырь. Бросается рывками. Боится света: зажми палец и держи его в луче."
            },
            new EntityDef
            {
                Kind = EntityKind.Mara, Name = "Мара", Bane = ItemKind.Bell, Height = 1.3f,
                Body = new Color(0.25f, 0.24f, 0.3f), Accent = new Color(0.7f, 0.6f, 1f),
                Origin = "Славянский и скандинавский фольклор",
                Lore = "Ночной морок, от которого не пошевелиться. Сгорбленная старуха садится совсем рядом и смотрит. Чем дольше смотришь на неё в ответ, тем сильнее страх.",
                Signs = "Появляется вплотную. Страх растёт, пока ты на неё смотришь.",
                Defense = "Звон колокольчика будит от морока. Не смотри на неё — позвони.",
                Hint = "Это мара. Не смотри на неё! Встряхни телефон — колокольчик разбудит."
            },
        };

        public static readonly ItemDef[] Items =
        {
            new ItemDef { Kind = ItemKind.Salt, Name = "Соль", Gesture = Gesture.SwipeUp,
                Usage = "Смахни пальцем вверх", Lore = "Чистая соль. Мёртвые не переступают соляную черту." },
            new ItemDef { Kind = ItemKind.Lantern, Name = "Фонарь", Gesture = Gesture.Hold,
                Usage = "Зажми палец на экране", Lore = "Свет горит, пока держишь палец. Заряд тратится." },
            new ItemDef { Kind = ItemKind.Iron, Name = "Железо", Gesture = Gesture.DoubleTap,
                Usage = "Коснись экрана дважды", Lore = "Старый нож. Бьёт только вплотную, но наверняка." },
            new ItemDef { Kind = ItemKind.Bell, Name = "Колокольчик", Gesture = Gesture.Shake,
                Usage = "Встряхни телефон", Lore = "Медный колокольчик. Звон отгоняет духов и будит от морока." },
            new ItemDef { Kind = ItemKind.Mirror, Name = "Зеркало", Gesture = Gesture.SwipeDown,
                Usage = "Смахни пальцем вниз", Lore = "Маленькое зеркальце. Показывает истинное лицо." },
            new ItemDef { Kind = ItemKind.Wormwood, Name = "Полынь", Gesture = Gesture.Zigzag,
                Usage = "Проведи пальцем зигзагом влево-вправо", Lore = "Пучок сухой полыни. Горький дым гонит нечисть." },
        };

        public static EntityDef Get(EntityKind k) => Entities[(int)k];
        public static ItemDef Get(ItemKind k) => Items[(int)k];

        // Сколько видов сущностей открыто: две с начала и ещё по одной за каждые 2 минуты выживания.
        public const float UnlockSeconds = 120f;
        public static int Unlocked(float survivedSeconds) =>
            Mathf.Clamp(2 + Mathf.FloorToInt(survivedSeconds / UnlockSeconds), 2, Entities.Length);

        // запись в энциклопедии открывается после первой встречи и сохраняется между играми
        public static bool Seen(EntityKind k) => PlayerPrefs.GetInt("seen_" + k, 0) == 1;
        public static void MarkSeen(EntityKind k)
        {
            PlayerPrefs.SetInt("seen_" + k, 1);
            PlayerPrefs.Save();
        }
    }
}
