using System.Collections.Generic;
using UnityEngine;

namespace GhostWard
{
    // Что игрок знает о сущностях — бестиарий заполняется по ходу игры.
    //  • Призрак и Тень — учебные: известны полностью с самого начала, при первой встрече подсказка, как справиться.
    //  • Остальные открываются званиями (по одной на звание). Новая сущность приходит отдельным «делом»:
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

        // сколько видов открыто: две учебные и по одной за каждое звание
        public static int UnlockedCount => Mathf.Clamp(Starters.Length + Progress.Rank, Starters.Length, Catalog.Entities.Length);
        public static bool Unlocked(EntityKind k) => (int)k < UnlockedCount;
        public static int RankFor(EntityKind k) => Mathf.Max(0, (int)k - (Starters.Length - 1));

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
}
