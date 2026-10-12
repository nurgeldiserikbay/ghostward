using UnityEngine;

namespace GhostWard
{
    // Приборы поиска — переключаются панелью внизу экрана.
    public enum Tool { Emf, Thermo, Night, Radio }

    // Средства защиты — в сумке; тратятся сразу при выборе.
    public enum Protect { Circle, Candle, Tumar, Calm }

    // Приманки: каждая привлекает две сущности (Lure в EntityDef). Тратятся при использовании.
    public enum Lure { Photo, Yarn, Meat, MusicBox }

    // Запас предметов игрока. Фонарь меряется зарядом в процентах, остальное — штуками.
    public class Inventory
    {
        public int Salt, Iron, Bell, Mirror, Wormwood;
        public float Battery;
        public int Circle, Candle, Tumar, Calm;
        public readonly int[] Lures = { 1, 1, 1, 1 };

        public int Count(Lure l) => Lures[(int)l];

        public bool Use(Lure l)
        {
            if (Lures[(int)l] <= 0) return false;
            Lures[(int)l]--;
            return true;
        }

        // стартовый запас с бонусами званий
        public static Inventory Starting() => new()
        {
            Salt = 4 + (Progress.ExtraSalt ? 1 : 0), Battery = 100f, Iron = 2, Bell = 3, Mirror = 2, Wormwood = 2,
            Circle = 1 + (Progress.ExtraCircle ? 1 : 0),
            Candle = 2 + (Progress.ExtraCandle ? 1 : 0),
            Tumar = 1 + (Progress.ExtraTumar ? 1 : 0),
            Calm = 1
        };

        public int Count(Protect p) => p switch
        {
            Protect.Circle => Circle,
            Protect.Candle => Candle,
            Protect.Tumar => Tumar,
            _ => Calm
        };

        public bool Use(Protect p)
        {
            switch (p)
            {
                case Protect.Circle: if (Circle <= 0) return false; Circle--; return true;
                case Protect.Candle: if (Candle <= 0) return false; Candle--; return true;
                case Protect.Tumar: if (Tumar <= 0) return false; Tumar--; return true;
                default: if (Calm <= 0) return false; Calm--; return true;
            }
        }

        // припасы за наградную рекламу на рассвете
        public string Bonus()
        {
            Salt += 2;
            Iron += 1;
            Bell += 1;
            Mirror += 1;
            Wormwood += 1;
            Battery = Mathf.Min(100f, Battery + 30f);
            Circle += 1;
            Calm += 1;
            static string N(ItemKind k) => Catalog.Get(k).Name;
            static string P(Protect k) => Loc.T($"p.{k}.name");
            return $"{N(ItemKind.Salt)} +2 · {N(ItemKind.Iron)} +1 · {N(ItemKind.Bell)} +1 · {N(ItemKind.Mirror)} +1\n" +
                   $"{N(ItemKind.Wormwood)} +1 · {N(ItemKind.Lantern)} +30% · {P(Protect.Circle)} +1 · {P(Protect.Calm)} +1";
        }

        // находка в тайнике: одна случайная вещь; возвращает её название
        public string Stash()
        {
            switch (Random.Range(0, 7))
            {
                case 0: Salt++; return Catalog.Get(ItemKind.Salt).Name;
                case 1: Iron++; return Catalog.Get(ItemKind.Iron).Name;
                case 2: Wormwood++; return Catalog.Get(ItemKind.Wormwood).Name;
                case 3: Battery = Mathf.Min(100f, Battery + 25f); return Catalog.Get(ItemKind.Lantern).Name + " +25%";
                case 4: Candle++; return Loc.T("p.Candle.name");
                case 5: Circle++; return Loc.T("p.Circle.name");
                default: Calm++; return Loc.T("p.Calm.name");
            }
        }

        // пополнение на рассвете; возвращает строку для экрана
        public string Resupply()
        {
            float battery = Mathf.Min(40f, 100f - Battery);
            Salt += 3;
            Iron += 1;
            Bell += 2;
            Mirror += 1;
            Wormwood += 2;
            Battery += battery;
            Circle += 1;
            Candle += 1;
            Calm += 1;
            for (int i = 0; i < Lures.Length; i++) Lures[i] = System.Math.Max(Lures[i], 1);
            static string N(ItemKind k) => Catalog.Get(k).Name;
            static string P(Protect k) => Loc.T($"p.{k}.name");
            return $"{N(ItemKind.Salt)} +3 · {N(ItemKind.Iron)} +1 · {N(ItemKind.Bell)} +2\n" +
                   $"{N(ItemKind.Mirror)} +1 · {N(ItemKind.Wormwood)} +2 · {N(ItemKind.Lantern)} +{Mathf.RoundToInt(battery)}%\n" +
                   $"{P(Protect.Circle)} +1 · {P(Protect.Candle)} +1 · {P(Protect.Calm)} +1";
        }

        public int Count(ItemKind k) => k switch
        {
            ItemKind.Salt => Salt,
            ItemKind.Lantern => Mathf.CeilToInt(Battery),
            ItemKind.Iron => Iron,
            ItemKind.Bell => Bell,
            ItemKind.Mirror => Mirror,
            ItemKind.Wormwood => Wormwood,
            _ => 0
        };

        // потратить одну штуку; false — если закончилось
        public bool Use(ItemKind k)
        {
            switch (k)
            {
                case ItemKind.Salt: if (Salt <= 0) return false; Salt--; return true;
                case ItemKind.Iron: if (Iron <= 0) return false; Iron--; return true;
                case ItemKind.Bell: if (Bell <= 0) return false; Bell--; return true;
                case ItemKind.Mirror: if (Mirror <= 0) return false; Mirror--; return true;
                case ItemKind.Wormwood: if (Wormwood <= 0) return false; Wormwood--; return true;
                default: return Battery > 0f;
            }
        }

        public string Label(ItemKind k) => k == ItemKind.Lantern ? Count(k) + "%" : Count(k).ToString();
    }
}
