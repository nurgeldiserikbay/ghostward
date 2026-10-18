using System;
using UnityEngine;

namespace GhostWard
{
    // Вид камеры. Normal и Dark бесплатны, остальные открываются покупкой или за рекламу.
    // Thermal — только у прибора, не в настройках; NightShot — ночная ИК-съёмка любительской камерой
    public enum Vision { Normal, Dark, NightGreen, Cctv, Vhs, Thermal, NightShot }

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

        // 0 — авто (AR, только если сервисы AR уже установлены), 1 — выключен
        public static int ArMode
        {
            get => PlayerPrefs.GetInt("arMode", 0);
            set { PlayerPrefs.SetInt("arMode", value); PlayerPrefs.Save(); }
        }

        // игрок сам попросил установить сервисы AR — предложим при следующей инициализации камеры
        public static bool ArInstallRequested
        {
            get => PlayerPrefs.GetInt("arInstall", 0) == 1;
            set { PlayerPrefs.SetInt("arInstall", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool TutorialSeen
        {
            get => PlayerPrefs.GetInt("tutorialSeen", 0) == 1;
            set { PlayerPrefs.SetInt("tutorialSeen", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // громкость атмосферы (гул, помехи, сердце) и эффектов (голоса сущностей, предметы)
        public static float Ambience
        {
            get => PlayerPrefs.GetFloat("ambience", 1f);
            set { PlayerPrefs.SetFloat("ambience", Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        public static float Music
        {
            get => PlayerPrefs.GetFloat("music", 0.8f);
            set { PlayerPrefs.SetFloat("music", Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        // обучающая первая ночь пройдена
        public static bool PrologueDone
        {
            get => PlayerPrefs.GetInt("prologueDone", 0) == 1;
            set { PlayerPrefs.SetInt("prologueDone", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static float Sfx
        {
            get => PlayerPrefs.GetFloat("sfx", 1f);
            set { PlayerPrefs.SetFloat("sfx", Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        // Качество: 0 — экономное, 1 — среднее, 2 — высокое. По умолчанию — по объёму памяти телефона.
        public static int Quality
        {
            get => PlayerPrefs.GetInt("quality", SystemInfo.systemMemorySize < 3500 ? 0 : SystemInfo.systemMemorySize < 6000 ? 1 : 2);
            set { PlayerPrefs.SetInt("quality", Mathf.Clamp(value, 0, 2)); PlayerPrefs.Save(); Apply(); }
        }

        public static void Apply()
        {
            AudioListener.volume = Volume;
            Application.targetFrameRate = Quality == 0 ? 30 : 60;
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
                urp.renderScale = Quality switch { 0 => 0.6f, 1 => 0.8f, _ => 1f };
        }
    }

    // Открытые платные вещи. Без своего сервера: состояние — на устройстве,
    // покупки подтверждает Google Play (при подключении биллинга), реклама — AdMob.
    public static class Unlocks
    {
        // в тестовой сборке всё открыто (TestMode.On)
        public static bool TestUnlockAll => TestMode.On;

        public static bool IsFree(Vision v) => v is Vision.Normal or Vision.Dark;

        public static bool Has(Vision v) => TestUnlockAll || IsFree(v) || PlayerPrefs.GetInt("unlock_" + v, 0) == 1;

        public static void Grant(Vision v)
        {
            PlayerPrefs.SetInt("unlock_" + v, 1);
            PlayerPrefs.Save();
        }
    }
}
