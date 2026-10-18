using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARCore;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;

namespace GhostWard.EditorTools
{
    // Проект хранится без сгенерированных настроек: при первом открытии всё создаётся здесь.
    // Повторный запуск безопасен — меню GhostWard → Настроить проект.
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string SettingsDir = "Assets/GhostWard/Settings";
        public const string ScenePath = "Assets/GhostWard/Scenes/Main.unity";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath) || GraphicsSettings.defaultRenderPipeline == null) Run();
            };
        }

        [MenuItem("GhostWard/Настроить проект")]
        public static void Run()
        {
            Directory.CreateDirectory(SettingsDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            SetupUrp();
            SetupPlayer();
            SetupXr();
            SetupScene();
            SetupAds();
            AssetDatabase.SaveAssets();
            Debug.Log("[GhostWard] Проект настроен");

            if (SetupInputHandling() && !Application.isBatchMode &&
                EditorUtility.DisplayDialog("GhostWard", "Изменена система ввода. Нужен перезапуск редактора.", "Перезапустить", "Позже"))
                EditorApplication.OpenProject(Directory.GetCurrentDirectory());
        }

        static void SetupUrp()
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(SettingsDir + "/URP.asset");
            if (urp == null)
            {
                var data = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(data, SettingsDir + "/URP_Renderer.asset");

                // без этой фичи URP не рисует видео с камеры ARCore
                var feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
                feature.name = "ARBackgroundRendererFeature";
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                EditorUtility.SetDirty(data);

                urp = UniversalRenderPipelineAsset.Create(data);
                urp.renderScale = 1f;
                urp.msaaSampleCount = 1;
                urp.supportsHDR = false;
                urp.shadowDistance = 0f;
                AssetDatabase.CreateAsset(urp, SettingsDir + "/URP.asset");
            }

            GraphicsSettings.defaultRenderPipeline = urp;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        static void SetupPlayer()
        {
            PlayerSettings.companyName = "TheLightCome";
            PlayerSettings.productName = "Ghost Ward";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.thelightcome.ghostward");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        }

        // true — если настройку пришлось поменять и нужен перезапуск
        static bool SetupInputHandling()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets.Length == 0) return false;
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            // 2 = оба: новый Input System и старый Input.gyro как запасной гироскоп
            if (prop == null || prop.intValue == 2) return false;
            prop.intValue = 2;
            so.ApplyModifiedProperties();
            return true;
        }

        static void SetupXr()
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null)
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                Directory.CreateDirectory("Assets/XR");
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perTarget, true);
            }

            var settings = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                settings.name = "Android Settings";
                AssetDatabase.AddObjectToAsset(settings, perTarget);
                perTarget.SetSettingsForBuildTarget(BuildTargetGroup.Android, settings);
            }
            if (settings.Manager == null)
            {
                var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                manager.name = "Android Providers";
                AssetDatabase.AddObjectToAsset(manager, perTarget);
                settings.Manager = manager;
            }
            // AR не поднимается сам при старте: на телефоне без поддержки ARCore это сразу открывает Маркет.
            // Загрузчик запускает ArRig, и только если сервисы AR уже стоят.
            settings.InitManagerOnStart = false;
            XRPackageMetadataStore.AssignLoader(settings.Manager, "UnityEngine.XR.ARCore.ARCoreLoader", BuildTargetGroup.Android);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(perTarget);

            // ARCore необязателен: игра ставится и на телефоны без него и работает в режиме гироскопа
            var arcore = ARCoreSettings.GetOrCreateSettings();
            arcore.requirement = ARCoreSettings.Requirement.Optional;
            arcore.depth = ARCoreSettings.Requirement.Optional;
            EditorUtility.SetDirty(arcore);
        }

        // ID приложения AdMob. Пока тестовый от Google — заменить на свой перед релизом.
        public const string AdMobAndroidAppId = "ca-app-pub-3940256099942544~3347511713";

        // Класс настроек плагина закрытый (internal), поэтому — через отражение.
        static void SetupAds()
        {
            var type = System.Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
            if (type == null) { Debug.LogWarning("[GhostWard] Плагин AdMob не найден"); return; }
            var load = type.GetMethod("LoadInstance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            var settings = load?.Invoke(null, null) as Object;
            var prop = type.GetProperty("GoogleMobileAdsAndroidAppId");
            if (settings == null || prop == null) { Debug.LogWarning("[GhostWard] Настройки AdMob недоступны"); return; }
            if ((string)prop.GetValue(settings) == AdMobAndroidAppId) return;
            prop.SetValue(settings, AdMobAndroidAppId);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        static void SetupScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Game").AddComponent<Game>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }

    public static class Build
    {
        // Из командной строки:
        // Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod GhostWard.EditorTools.Build.Android
        [MenuItem("GhostWard/Собрать APK")]
        public static void Android()
        {
            ProjectSetup.Run();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = "Builds/GhostWard.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            });

            bool ok = report.summary.result == BuildResult.Succeeded;
            if (TestMode.On) Debug.LogWarning("[GhostWard] ТЕСТОВАЯ СБОРКА (TestMode.On = true): диагностика и тестовые кнопки видны. Перед выкладкой — false.");
            Debug.Log($"[GhostWard] Сборка: {report.summary.result}, {(ok ? new FileInfo("Builds/GhostWard.apk").Length / (1024 * 1024) : 0)} МБ");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
            else if (ok) EditorUtility.RevealInFinder("Builds/GhostWard.apk");
        }
    }
}
