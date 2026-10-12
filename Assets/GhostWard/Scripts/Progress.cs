using UnityEngine;

namespace GhostWard
{
    // Долгий прогресс игрока между играми: опыт, звание с бонусами, история дома.
    // Хранится на устройстве (PlayerPrefs).
    public static class Progress
    {
        // порог опыта для каждого звания; бонусы — rank_N / perk_N в Strings.cs
        public static readonly int[] RankXp = { 0, 300, 800, 1600, 2800, 4500, 7000 };
        public const int StoryNightPages = 20;   // по странице за пережитую ночь
        public const int StoryPages = 21;        // + эпилог

        // эпилог — после звания «Страж дома» и изгнания всех сущностей
        public static bool EpilogueUnlocked => Rank >= RankXp.Length - 1 && Know.AllBanished;

        public static int Xp => PlayerPrefs.GetInt("xp", 0);
        public static int NightsSurvived => PlayerPrefs.GetInt("nightsSurvived", 0);
        public static int StoryUnlocked => Mathf.Min(StoryNightPages, NightsSurvived);

        public static int Rank
        {
            get
            {
                int xp = Xp, rank = 0;
                for (int i = 0; i < RankXp.Length; i++) if (xp >= RankXp[i]) rank = i;
                return rank;
            }
        }

        public static string RankName(int rank) => Loc.T($"rank_{rank}");
        // бонус звания; со второго звания — ещё и новая сущность в доме
        public static string Perk(int rank) =>
            Loc.T($"perk_{rank}") + (rank >= 1 ? " · " + Loc.T("perk_new") : "");

        // бонусы званий
        public static bool ExtraSalt => Rank >= 1;
        public static bool ExtraCandle => Rank >= 2;
        public static bool ExtraTumar => Rank >= 3;
        public static bool SlowLantern => Rank >= 4;
        public static bool Instinct => Rank >= 5;
        public static bool ExtraCircle => Rank >= 6;

        // начислить опыт; true — если получено новое звание
        public static bool AddXp(int amount)
        {
            int before = Rank;
            PlayerPrefs.SetInt("xp", Xp + Mathf.Max(0, amount));
            PlayerPrefs.Save();
            return Rank > before;
        }

        // пережита ночь; true — если открылась новая страница истории
        public static bool AddSurvivedNight()
        {
            int before = StoryUnlocked;
            PlayerPrefs.SetInt("nightsSurvived", NightsSurvived + 1);
            PlayerPrefs.Save();
            return StoryUnlocked > before;
        }

        // доля пути к следующему званию 0..1
        public static float RankProgress
        {
            get
            {
                int r = Rank;
                if (r >= RankXp.Length - 1)
                {
                    // высшее звание — полоска показывает путь к следующей сущности
                    int next = Know.NextUnlockXp;
                    if (next < 0) return 1f;
                    int prev = Know.XpFor((EntityKind)(Know.UnlockedCount - 1));
                    return Mathf.InverseLerp(prev, next, Xp);
                }
                return Mathf.InverseLerp(RankXp[r], RankXp[r + 1], Xp);
            }
        }

        public static string XpLine()
        {
            int r = Rank;
            if (r < RankXp.Length - 1) return Loc.F("xp_of", Xp, RankXp[r + 1]);
            int next = Know.NextUnlockXp;
            return next < 0 ? Loc.T("rank_max") : Loc.F("xp_next_case", Xp, next);
        }
    }
}
