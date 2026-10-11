using UnityEngine;

namespace GhostWard
{
    // Запас предметов игрока. Фонарь меряется зарядом в процентах, остальное — штуками.
    public class Inventory
    {
        public int Salt;
        public float Battery;
        public int Iron;
        public int Bell;

        public static Inventory Starting() => new() { Salt = 4, Battery = 100f, Iron = 2, Bell = 3 };

        // пополнение на рассвете; возвращает строку для экрана
        public string Resupply()
        {
            int salt = 3, iron = 1, bell = 2;
            float battery = Mathf.Min(40f, 100f - Battery);
            Salt += salt;
            Iron += iron;
            Bell += bell;
            Battery += battery;
            return $"Соль +{salt}   Железо +{iron}   Колокольчик +{bell}   Фонарь +{Mathf.RoundToInt(battery)}%";
        }

        public bool Has(ItemKind k) => k switch
        {
            ItemKind.Salt => Salt > 0,
            ItemKind.Lantern => Battery > 0.5f,
            ItemKind.Iron => Iron > 0,
            ItemKind.Bell => Bell > 0,
            _ => false
        };

        public string Label(ItemKind k) => k switch
        {
            ItemKind.Salt => Salt.ToString(),
            ItemKind.Lantern => Mathf.CeilToInt(Battery) + "%",
            ItemKind.Iron => Iron.ToString(),
            ItemKind.Bell => Bell.ToString(),
            _ => ""
        };
    }
}
