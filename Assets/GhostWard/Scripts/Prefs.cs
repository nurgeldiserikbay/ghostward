using System;
using UnityEngine;

namespace GhostWard
{
    // Вид камеры. Normal и Dark бесплатны, остальные открываются покупкой или за рекламу.
    public enum Vision { Normal, Dark, NightGreen, Cctv, Vhs }

    // Настройки игрока: хранятся на устройстве.
    public static class Settings
    {
        public static Vision Vision
        {
            get => (Vision)PlayerPrefs.GetInt("vision", (int)Vision.Dark);
            set { PlayerPrefs.SetInt("vision", (int)value); PlayerPrefs.Save(); }
        }

        // 0..1 шагами по 0.1
        public static float Volume
        {
            get => PlayerPrefs.GetFloat("volume", 1f);
            set { PlayerPrefs.SetFloat("volume", Mathf.Clamp01(value)); PlayerPrefs.Save(); AudioListener.volume = Mathf.Clamp01(value); }
        }

        // 0 — низкая, 1 — средняя, 2 — высокая
        public static int MicSensitivity
        {
            get => PlayerPrefs.GetInt("micSens", 1);
            set { PlayerPrefs.SetInt("micSens", Mathf.Clamp(value, 0, 2)); PlayerPrefs.Save(); }
        }

        public static float MicFactor => MicSensitivity switch { 0 => 0.6f, 2 => 1.7f, _ => 1f };

        public static bool Vibration
        {
            get => PlayerPrefs.GetInt("vibration", 1) == 1;
            set { PlayerPrefs.SetInt("vibration", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool TutorialSeen
        {
            get => PlayerPrefs.GetInt("tutorialSeen", 0) == 1;
            set { PlayerPrefs.SetInt("tutorialSeen", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static void Apply() => AudioListener.volume = Volume;
    }

    // Открытые платные вещи. Без своего сервера: состояние — на устройстве,
    // покупки подтверждает Google Play (при подключении биллинга), реклама — AdMob.
    public static class Unlocks
    {
        // На время тестов всё открыто. Перед релизом — false.
        public const bool TestUnlockAll = true;

        public static bool IsFree(Vision v) => v is Vision.Normal or Vision.Dark;

        public static bool Has(Vision v) => TestUnlockAll || IsFree(v) || PlayerPrefs.GetInt("unlock_" + v, 0) == 1;

        public static void Grant(Vision v)
        {
            PlayerPrefs.SetInt("unlock_" + v, 1);
            PlayerPrefs.Save();
        }
    }

    // Реклама: пока заглушка с той же логикой, что будет у AdMob.
    // Межстраничная — после каждого третьего «Ещё раз»; наградная — за режимы камеры и припасы.
    public static class Ads
    {
        const int RetriesPerInterstitial = 3;
        static int retries;

        public static bool RewardedReady => true;

        public static void ShowRewarded(Action onReward)
        {
            Debug.Log("[GhostWard] Наградная реклама (заглушка)");
            onReward?.Invoke();
        }

        // вызывать при «Ещё раз» после поражения
        public static void OnRetry()
        {
            retries++;
            if (retries % RetriesPerInterstitial != 0) return;
            Debug.Log("[GhostWard] Межстраничная реклама (заглушка)");
        }
    }
}
