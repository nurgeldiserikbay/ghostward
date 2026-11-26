using System;
using System.Collections.Concurrent;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace GhostWard
{
    // Реклама AdMob. ID приложения и блоков — в ReleaseConfig (пока тестовые Google).
    // Хоррор не участвует в программе Families: детских флагов нет, рейтинг объявлений — до 12+ (T).
    // Согласие (GDPR и законы штатов США) — через Google UMP: сначала запрос согласия,
    // и только если CanRequestAds — запуск AdMob. Форма настраивается в AdMob → Конфиденциальность и сообщения.
    //  • Наградная — за припасы на рассвете, «второй шанс» и закрытые виды камеры.
    //  • Межстраничная — после каждого третьего «Ещё раз», не чаще раза в 2 минуты.
    public static class Ads
    {
        const string RewardedId = ReleaseConfig.RewardedId;
        const string InterstitialId = ReleaseConfig.InterstitialId;
        const int RetriesPerInterstitial = 3;
        const float InterstitialCooldown = 120f;

        static RewardedAd rewarded;
        static InterstitialAd interstitial;
        static bool initialized;
        static int retries;
        static float lastInterstitial = -999f;
        static readonly ConcurrentQueue<Action> mainThread = new();

        static bool sdkStarted;

        // Вызывать после системных запросов разрешений, чтобы окно согласия не наложилось на них.
        public static void Init()
        {
            if (initialized) return;
            initialized = true;
            new GameObject("Ads Dispatcher").AddComponent<Dispatcher>();
            // согласие, полученное в прошлый запуск, действует сразу
            if (ConsentInformation.CanRequestAds()) StartSdk();
            ConsentInformation.Update(new ConsentRequestParameters { TagForUnderAgeOfConsent = false }, updateError => Post(() =>
            {
                if (updateError != null) { Debug.LogWarning("[GhostWard] Согласие: " + updateError.Message); StartIfAllowed(); return; }
                ConsentForm.LoadAndShowConsentFormIfRequired(showError => Post(() =>
                {
                    if (showError != null) Debug.LogWarning("[GhostWard] Форма согласия: " + showError.Message);
                    StartIfAllowed();
                }));
            }));
        }

        // Кнопка «Настройки рекламы» нужна только там, где этого требует закон (Европа и т. п.)
        public static bool PrivacyOptionsRequired =>
            initialized && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public static void ShowPrivacyOptions()
        {
            ConsentForm.ShowPrivacyOptionsForm(error => Post(() =>
            {
                if (error != null) Debug.LogWarning("[GhostWard] Настройки рекламы: " + error.Message);
                StartIfAllowed();
            }));
        }

        static void StartIfAllowed()
        {
            if (ConsentInformation.CanRequestAds()) StartSdk();
        }

        static void StartSdk()
        {
            if (sdkStarted) return;
            sdkStarted = true;
            MobileAds.SetRequestConfiguration(new RequestConfiguration
            {
                MaxAdContentRating = MaxAdContentRating.T,
            });
            MobileAds.Initialize(_ => Post(() =>
            {
                LoadRewarded();
                LoadInterstitial();
            }));
        }

        public static bool RewardedReady
        {
            get
            {
#if UNITY_EDITOR
                return true;
#else
                return rewarded != null && rewarded.CanShowAd();
#endif
            }
        }

        // onReward вызывается, только если игрок досмотрел; если рекламы нет — onUnavailable
        public static void ShowRewarded(Action onReward, Action onUnavailable = null)
        {
#if UNITY_EDITOR
            onReward?.Invoke();
#else
            if (rewarded == null || !rewarded.CanShowAd())
            {
                onUnavailable?.Invoke();
                LoadRewarded();
                return;
            }
            var ad = rewarded;
            rewarded = null;
            ad.Show(_ => Post(onReward));
#endif
        }

        // вызывать при «Ещё раз» после поражения
        public static void OnRetry()
        {
            retries++;
            if (retries % RetriesPerInterstitial != 0) return;
            if (Time.realtimeSinceStartup - lastInterstitial < InterstitialCooldown) return;
            if (interstitial == null || !interstitial.CanShowAd()) { LoadInterstitial(); return; }
            lastInterstitial = Time.realtimeSinceStartup;
            var ad = interstitial;
            interstitial = null;
            ad.Show();
        }

        static void LoadRewarded()
        {
            RewardedAd.Load(RewardedId, new AdRequest(), (ad, error) => Post(() =>
            {
                if (error != null || ad == null) return;
                rewarded = ad;
                ad.OnAdFullScreenContentClosed += () => Post(LoadRewarded);
                ad.OnAdFullScreenContentFailed += _ => Post(LoadRewarded);
            }));
        }

        static void LoadInterstitial()
        {
            InterstitialAd.Load(InterstitialId, new AdRequest(), (ad, error) => Post(() =>
            {
                if (error != null || ad == null) return;
                interstitial = ad;
                ad.OnAdFullScreenContentClosed += () => Post(LoadInterstitial);
                ad.OnAdFullScreenContentFailed += _ => Post(LoadInterstitial);
            }));
        }

        // колбэки рекламы могут прийти не из главного потока — выполняем их в Update
        static void Post(Action a)
        {
            if (a != null) mainThread.Enqueue(a);
        }

        class Dispatcher : MonoBehaviour
        {
            void Awake() => DontDestroyOnLoad(gameObject);

            void Update()
            {
                while (mainThread.TryDequeue(out var a)) a();
            }
        }
    }
}
