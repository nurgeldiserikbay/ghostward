using System.Collections.Generic;
using UnityEngine;

namespace GhostWard
{
    // Что игрок знает о сущностях — бестиарий заполняется по ходу игры.
    //  • Заранее не известна ни одна: сущность попадает сюда, когда игрок распознал её по уликам (Game.Identify).
    //  • Остальные открываются по мере роста опыта (UnlockXp; первые шесть совпадают со званиями).
    //    Новая сущность приходит отдельным «делом»:
    //    игрок сразу узнаёт имя, суть и легенду (в легенде — намёк, чего она боится), а способ ищет сам.
    //  • Повадки — после пережитой с ней ночи; улики — каждая замеченная; приманка — если откликнулась;
    //    «Как защититься» — после изгнания.
    public static class Know
    {
        public static readonly EntityKind[] Starters = { EntityKind.Ghost, EntityKind.Shade };

        public static bool IsStarter(EntityKind k) => System.Array.IndexOf(Starters, k) >= 0;

        static string Key(EntityKind k, string what) => $"know_{k}_{what}";
        static bool Get(string key) => PlayerPrefs.GetInt(key, 0) == 1;

        // true — если знание новое
        static bool Learn(string key)
        {
            if (Get(key)) return false;
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
            return true;
        }

        // Старые сохранения (до распознавания по уликам) знали сущностей заранее — бестиарий у них не пустой.
        // Один раз стираем эти знания: список сущностей у всех начинается пустым
        public static void MigrateOnce()
        {
            if (PlayerPrefs.GetInt("know_ver", 0) >= 2) return;
            ResetAll();
            PlayerPrefs.SetInt("know_ver", 2);
            PlayerPrefs.Save();
        }

        public static bool AnyRecognized()
        {
            foreach (var def in Catalog.Entities) if (Name(def.Kind)) return true;
            return false;
        }

        // тестовая сборка: забыть всё о сущностях — бестиарий снова пуст, как у нового игрока
        public static void ResetAll()
        {
            foreach (var def in Catalog.Entities)
            {
                foreach (var what in new[] { "name", "habits", "defense", "lure", "banished" })
                    PlayerPrefs.DeleteKey(Key(def.Kind, what));
                foreach (Evidence e in System.Enum.GetValues(typeof(Evidence)))
                    PlayerPrefs.DeleteKey(Key(def.Kind, "clue_" + e));
                PlayerPrefs.DeleteKey("seen_" + def.Kind);
            }
            PlayerPrefs.Save();
        }

        // (больше не вызывается: заранее известных сущностей нет — бестиарий заполняется распознанными)
        // учебные сущности известны полностью с первого запуска
        public static void EnsureStarters()
        {
            foreach (var k in Starters)
            {
                var def = Catalog.Get(k);
                Learn(Key(k, "name"));
                Learn(Key(k, "habits"));
                Learn(Key(k, "defense"));
                Learn(Key(k, "lure"));
                foreach (var c in def.Clues) Learn(Key(k, "clue_" + c));
            }
        }

        // сколько опыта нужно, чтобы сущность начала приходить (по порядку EntityKind)
        public static readonly int[] UnlockXp =
        {
            0, 0, 300, 800, 1600, 2800, 4500, 7000,
            8500, 10000, 11500, 13000, 14700, 16400, 18200, 20000, 22000, 24000, 26000, 28000,
            30000, 32000, 34000, 36000, 38000, 40000, 42000, 44000, 46000, 48000,
            50000, 52000, 54000, 56000, 58000, 60000, 62000, 64000, 66000, 68000
        };

        public static int UnlockedCount
        {
            get
            {
                int xp = Progress.Xp, n = 0;
                for (int i = 0; i < Catalog.Entities.Length && i < UnlockXp.Length; i++) if (xp >= UnlockXp[i]) n = i + 1;
                return Mathf.Max(Starters.Length, n);
            }
        }

        public static bool Unlocked(EntityKind k) => (int)k < UnlockedCount;
        public static int XpFor(EntityKind k) => UnlockXp[Mathf.Min((int)k, UnlockXp.Length - 1)];

        // порог следующей сущности или -1, если открыты все
        public static int NextUnlockXp => UnlockedCount < Catalog.Entities.Length ? XpFor((EntityKind)UnlockedCount) : -1;

        public static bool Name(EntityKind k) => Get(Key(k, "name"));
        public static bool Habits(EntityKind k) => Get(Key(k, "habits"));
        public static bool Defense(EntityKind k) => Get(Key(k, "defense"));
        public static bool Lure(EntityKind k) => Get(Key(k, "lure"));
        public static bool Clue(EntityKind k, Evidence e) => Get(Key(k, "clue_" + e));
        public static bool Banished(EntityKind k) => Get(Key(k, "banished"));

        public static int CluesKnown(EntityKind k)
        {
            int n = 0;
            foreach (var c in Catalog.Get(k).Clues) if (Clue(k, c)) n++;
            return n;
        }

        public static bool AllCluesKnown(EntityKind k) => CluesKnown(k) >= Catalog.Get(k).Clues.Length;

        public static bool LearnName(EntityKind k) => Learn(Key(k, "name"));
        public static bool LearnHabits(EntityKind k) => Learn(Key(k, "habits"));
        public static bool LearnDefense(EntityKind k) => Learn(Key(k, "defense"));
        public static bool LearnLure(EntityKind k) => Learn(Key(k, "lure"));
        public static bool LearnClue(EntityKind k, Evidence e) => Learn(Key(k, "clue_" + e));
        public static void MarkBanished(EntityKind k) => Learn(Key(k, "banished"));

        public static bool AllBanished
        {
            get
            {
                foreach (var e in Catalog.Entities) if (!Banished(e.Kind)) return false;
                return true;
            }
        }

        // первая открытая, но ещё не встреченная сущность — следующее «новое дело»
        public static EntityKind? NextNewCase()
        {
            for (int i = 0; i < UnlockedCount; i++)
                if (!Name(Catalog.Entities[i].Kind)) return Catalog.Entities[i].Kind;
            return null;
        }

        // записать итог ночи в дневник; возвращает список новых знаний для экрана рассвета
        public static List<string> Record(EntityKind k, IEnumerable<Evidence> clues, bool lure, bool survived, bool banished)
        {
            var learned = new List<string>();
            int newClues = 0;
            foreach (var c in clues) if (LearnClue(k, c)) newClues++;
            if (newClues > 0) learned.Add(Loc.F("jl_clues", newClues));
            if (lure && LearnLure(k)) learned.Add(Loc.T("jl_lure"));
            if (survived && LearnHabits(k)) learned.Add(Loc.T("jl_habits"));
            if (banished)
            {
                MarkBanished(k);
                if (LearnDefense(k)) learned.Add(Loc.T("jl_defense"));
            }
            return learned;
        }
    }

    // Какие жесты игрок отработал на тренировке в дневнике.
    public static class Skills
    {
        public static bool Learned(ItemKind k) => PlayerPrefs.GetInt("learned_" + k, 0) == 1;

        public static void Learn(ItemKind k)
        {
            PlayerPrefs.SetInt("learned_" + k, 1);
            PlayerPrefs.Save();
        }

        public static int Count
        {
            get
            {
                int n = 0;
                foreach (var i in Catalog.Items) if (Learned(i.Kind)) n++;
                return n;
            }
        }

        public static bool All => Count >= Catalog.Items.Length;
    }
}
