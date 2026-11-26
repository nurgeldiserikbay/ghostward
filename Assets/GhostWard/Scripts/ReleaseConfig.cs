namespace GhostWard
{
    // Всё, что нужно заменить перед выкладкой в Google Play. Сборка AAB (меню GhostWard → Собрать AAB для Google Play)
    // откажется собираться, пока здесь тестовые ID Google или пустая ссылка на политику.
    public static class ReleaseConfig
    {
        // ID приложения AdMob (с «~») и рекламных блоков (с «/»). Боевые ID аккаунта.
        public const string AdMobAppId = "ca-app-pub-9702825788968948~5910076762";
        public const string RewardedId = "ca-app-pub-9702825788968948/9051245420";
        public const string InterstitialId = "ca-app-pub-9702825788968948/3744233376";

        // Блоки, которые реально запрашивает игра: в тестовой сборке (APK) — тестовые блоки Google, они помечены
        // «Test Ad» и не грозят блокировкой аккаунта; в сборке для Google Play (GHOSTWARD_RELEASE) — свои.
#if GHOSTWARD_RELEASE
        public const string ActiveRewardedId = RewardedId;
        public const string ActiveInterstitialId = InterstitialId;
#else
        public const string ActiveRewardedId = "ca-app-pub-3940256099942544/5224354917";
        public const string ActiveInterstitialId = "ca-app-pub-3940256099942544/1033173712";
#endif

        // Опубликованная политика конфиденциальности (Google Docs → Файл → Поделиться → Опубликовать в интернете).
        // Та же ссылка указывается в Play Console и в AdMob.
        public const string PrivacyPolicyUrl = "https://docs.google.com/document/d/e/2PACX-1vQA_3QsTLu4_N_SarT-LKJo2j7SOflbBFYWf8gWR-DKmo4TIgpZUr9vLrAJEOoPMGNbnWV3Hrbuos4C/pub";

        const string GoogleTestPublisher = "ca-app-pub-3940256099942544";

        public static bool UsesTestAds =>
            AdMobAppId.StartsWith(GoogleTestPublisher) || RewardedId.StartsWith(GoogleTestPublisher) ||
            InterstitialId.StartsWith(GoogleTestPublisher);

        public static bool HasPolicyUrl => PrivacyPolicyUrl.StartsWith("https://");
    }
}
