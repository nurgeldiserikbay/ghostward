namespace GhostWard
{
    // Всё, что нужно заменить перед выкладкой в Google Play. Сборка AAB (меню GhostWard → Собрать AAB для Google Play)
    // откажется собираться, пока здесь тестовые ID Google или пустая ссылка на политику.
    public static class ReleaseConfig
    {
        // ID приложения AdMob (с «~») и рекламных блоков (с «/»). Сейчас — тестовые ID Google.
        public const string AdMobAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string RewardedId = "ca-app-pub-3940256099942544/5224354917";
        public const string InterstitialId = "ca-app-pub-3940256099942544/1033173712";

        // Опубликованная политика конфиденциальности (Google Docs → Файл → Поделиться → Опубликовать в интернете).
        // Та же ссылка указывается в Play Console и в AdMob.
        public const string PrivacyPolicyUrl = "";

        const string GoogleTestPublisher = "ca-app-pub-3940256099942544";

        public static bool UsesTestAds =>
            AdMobAppId.StartsWith(GoogleTestPublisher) || RewardedId.StartsWith(GoogleTestPublisher) ||
            InterstitialId.StartsWith(GoogleTestPublisher);

        public static bool HasPolicyUrl => PrivacyPolicyUrl.StartsWith("https://");
    }
}
