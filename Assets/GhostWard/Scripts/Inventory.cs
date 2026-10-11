using UnityEngine;

namespace GhostWard
{
    // Запас предметов игрока. Фонарь меряется зарядом в процентах, остальное — штуками.
    public class Inventory
    {
        public int Salt, Iron, Bell, Mirror, Wormwood;
        public float Battery;

        public static Inventory Starting() => new() { Salt = 4, Battery = 100f, Iron = 2, Bell = 3, Mirror = 2, Wormwood = 2 };

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
            static string N(ItemKind k) => Catalog.Get(k).Name;
            return $"{N(ItemKind.Salt)} +3 · {N(ItemKind.Iron)} +1 · {N(ItemKind.Bell)} +2\n" +
                   $"{N(ItemKind.Mirror)} +1 · {N(ItemKind.Wormwood)} +2 · {N(ItemKind.Lantern)} +{Mathf.RoundToInt(battery)}%";
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
