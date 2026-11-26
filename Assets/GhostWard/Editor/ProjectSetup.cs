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
            SetupIcon();
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

        // Иконка приложения. Файла нет — рисуется из эмблемы (WardLogo.RenderIcon).
        // Чтобы поставить свою (например, из GPT): заменить PNG 1024×1024 по этому пути.
        public const string IconPath = "Assets/GhostWard/Branding/AppIcon.png";
        // слои адаптивной иконки Android (docs/branding/GhostWard_Icon_fg.svg / _bg.svg)
        const string IconFgPath = "Assets/GhostWard/Branding/AppIconFg.png";
        const string IconBgPath = "Assets/GhostWard/Branding/AppIconBg.png";

        // иконке нужна картинка без сжатия и мипмапов; спрайт — чтобы её же показать на заставке
        static Texture2D LoadIconTexture(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) return null;
            if (imp.textureType != TextureImporterType.Sprite || imp.mipmapEnabled ||
                imp.textureCompression != TextureImporterCompression.Uncompressed || imp.npotScale != TextureImporterNPOTScale.None)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void SetupIcon()
        {
            if (!File.Exists(IconPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
                var gen = WardLogo.RenderIcon(1024);
                File.WriteAllBytes(IconPath, gen.EncodeToPNG());
                Object.DestroyImmediate(gen);
                AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            }
            var icon = LoadIconTexture(IconPath);
            if (icon == null) { Debug.LogWarning("[GhostWard] Иконка не найдена: " + IconPath); return; }
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            // Android берёт иконки из своих слотов, а не из общей — заполняем их явно.
            // Адаптивная (Android 8+): фон + эмблема в безопасной зоне; без своих слоёв — вся иконка на фоне.
            var fg = LoadIconTexture(IconFgPath) ?? icon;
            var bg = LoadIconTexture(IconBgPath) ?? icon;
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var slot in slots)
                {
                    if (slot.maxLayerCount >= 2) slot.SetTextures(bg, fg);
                    else slot.SetTexture(icon);
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
            }

            // заставка при запуске: тёмный фон как у иконки и эмблема
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.03f, 0.04f, 0.045f);
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            if (sprite != null)
            {
                PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2f, sprite) };
                PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
            }
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

        // ID приложения AdMob — в ReleaseConfig (там же блоки и ссылка на политику).
        public const string AdMobAndroidAppId = ReleaseConfig.AdMobAppId;

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

        const string ReleaseDefine = "GHOSTWARD_RELEASE";
        const string AabPath = "Builds/GhostWard-release.aab";

        // Сборка для Google Play: подписанный AAB, без тестового режима, с настоящей рекламой.
        // Ключ загрузки (upload keystore) и пароли задаются только локально в Player Settings → Publishing Settings,
        // в репозиторий не попадают. Каждый новый AAB — с бóльшим versionCode (Player Settings → Bundle Version Code).
        // Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod GhostWard.EditorTools.Build.AndroidRelease
        [MenuItem("GhostWard/Собрать AAB для Google Play")]
        public static void AndroidRelease()
        {
            var problems = new System.Collections.Generic.List<string>();
            if (ReleaseConfig.UsesTestAds) problems.Add("в ReleaseConfig тестовые ID AdMob — поставить свои");
            if (!ReleaseConfig.HasPolicyUrl) problems.Add("в ReleaseConfig.PrivacyPolicyUrl нет https-ссылки на опубликованную политику");
            if (!PlayerSettings.Android.useCustomKeystore ||
                string.IsNullOrWhiteSpace(PlayerSettings.Android.keystoreName) ||
                string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName) ||
                string.IsNullOrWhiteSpace(PlayerSettings.Android.keystorePass) ||
                string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasPass))
                problems.Add("не задан свой ключ загрузки (Player Settings → Publishing Settings → Custom Keystore, алиас и пароли)");
            if (problems.Count > 0)
            {
                string msg = "[GhostWard] AAB не собран:\n • " + string.Join("\n • ", problems);
                Debug.LogError(msg);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else EditorUtility.DisplayDialog("GhostWard", msg, "OK");
                return;
            }

            ProjectSetup.Run();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            bool oldBundle = EditorUserBuildSettings.buildAppBundle;
            BuildReport report;
            try
            {
                EditorUserBuildSettings.buildAppBundle = true;
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ProjectSetup.ScenePath },
                    locationPathName = AabPath,
                    target = BuildTarget.Android,
                    extraScriptingDefines = new[] { ReleaseDefine }, // TestMode.On = false
                    options = BuildOptions.None
                });
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = oldBundle;
            }

            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log($"[GhostWard] AAB: {report.summary.result}, versionCode {PlayerSettings.Android.bundleVersionCode}, " +
                      $"{(ok ? new FileInfo(AabPath).Length / (1024 * 1024) : 0)} МБ → {AabPath}");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
            else if (ok) EditorUtility.RevealInFinder(AabPath);
        }
    }
}
