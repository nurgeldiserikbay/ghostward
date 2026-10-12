using System;
using UnityEngine;

namespace GhostWard
{
    // Модификатор ночи: меняет правила и даёт больше опыта.
    public enum Modifier { None, NewMoon, Storm, FullMoon, Silence }

    public static class Modifiers
    {
        public static string Name(Modifier m) => Loc.T($"m.{m}.name");
        public static string Desc(Modifier m) => Loc.T($"m.{m}.desc");

        public static float XpFactor(Modifier m) => m switch
        {
            Modifier.NewMoon => 1.3f,
            Modifier.Storm => 1.2f,
            Modifier.FullMoon => 1.4f,
            Modifier.Silence => 1.3f,
            _ => 1f
        };

        // со второй ночи — примерно в половине ночей
        public static Modifier Roll(int night) =>
            night >= 2 && UnityEngine.Random.value < 0.5f ? (Modifier)UnityEngine.Random.Range(1, 5) : Modifier.None;
    }

    // Дело дня: одна и та же сущность и модификатор у всех игроков в этот день (зерно — дата).
    public static class Daily
    {
        static int Seed
        {
            get
            {
                var d = DateTime.Now.Date;
                return d.Year * 10000 + d.Month * 100 + d.Day;
            }
        }

        static string Today => DateTime.Now.ToString("yyyy-MM-dd");
        static string Yesterday => DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");

        public static EntityKind Entity => (EntityKind)(new System.Random(Seed).Next(Catalog.Entities.Length));
        public static Modifier Modifier => (Modifier)(1 + new System.Random(Seed * 7 + 3).Next(4));

        public static bool SolvedToday => PlayerPrefs.GetString("dailyLast", "") == Today;

        // серия считается живой, если последнее дело раскрыто сегодня или вчера
        public static int Streak
        {
            get
            {
                string last = PlayerPrefs.GetString("dailyLast", "");
                return last == Today || last == Yesterday ? PlayerPrefs.GetInt("dailyStreak", 0) : 0;
            }
        }

        // отметить раскрытие; false — если сегодня уже раскрыто (награда не повторяется)
        public static bool Complete()
        {
            if (SolvedToday) return false;
            int streak = PlayerPrefs.GetString("dailyLast", "") == Yesterday ? PlayerPrefs.GetInt("dailyStreak", 0) + 1 : 1;
            PlayerPrefs.SetInt("dailyStreak", streak);
            PlayerPrefs.SetString("dailyLast", Today);
            PlayerPrefs.Save();
            return true;
        }
    }

    public enum Trophy { FirstBanish, Names10, Clean5, Silent, Photo5, AllMet, Night5, Night10, Stash10, Streak7 }

    // Трофеи: локальные достижения со счётчиками; каждый даёт опыт.
    public static class Trophies
    {
        public const int Reward = 200;
        public static event Action<Trophy> Unlocked;

        public static bool Has(Trophy t) => PlayerPrefs.GetInt("tr_" + t, 0) == 1;
        public static string Name(Trophy t) => Loc.T($"tr.{t}.name");
        public static string Desc(Trophy t) => Loc.T($"tr.{t}.desc");

        public static void Give(Trophy t)
        {
            if (Has(t)) return;
            PlayerPrefs.SetInt("tr_" + t, 1);
            PlayerPrefs.Save();
            Progress.AddXp(Reward);
            Unlocked?.Invoke(t);
        }

        static int Count(string key, int add)
        {
            int v = PlayerPrefs.GetInt(key, 0) + add;
            PlayerPrefs.SetInt(key, v);
            PlayerPrefs.Save();
            return v;
        }

        public static void OnBanish(bool silent)
        {
            Give(Trophy.FirstBanish);
            if (silent) Give(Trophy.Silent);
        }

        public static void OnRightName() { if (Count("cnt_names", 1) >= 10) Give(Trophy.Names10); }
        public static void OnCleanNight() { if (Count("cnt_clean", 1) >= 5) Give(Trophy.Clean5); }
        public static void OnStash() { if (Count("cnt_stash", 1) >= 10) Give(Trophy.Stash10); }

        public static void OnPhoto(EntityKind k)
        {
            PlayerPrefs.SetInt("photo_" + k, 1);
            int n = 0;
            foreach (var e in Catalog.Entities) if (PlayerPrefs.GetInt("photo_" + e.Kind, 0) == 1) n++;
            if (n >= 5) Give(Trophy.Photo5);
        }

        public static void OnSeen()
        {
            foreach (var e in Catalog.Entities) if (!Catalog.Seen(e.Kind)) return;
            Give(Trophy.AllMet);
        }

        public static void OnNight(int night)
        {
            if (night >= 5) Give(Trophy.Night5);
            if (night >= 10) Give(Trophy.Night10);
        }

        public static void OnDaily(int streak) { if (streak >= 7) Give(Trophy.Streak7); }
    }
}
