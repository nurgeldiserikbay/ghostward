using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace GhostWard.EditorTools
{
    // Android 8+ рисует адаптивную иконку (слои из ProjectSetup.SetupIcon), но установщик APK,
    // файловые менеджеры и часть лаунчеров берут обычную картинку mipmap/app_icon.png.
    // Unity кладёт туда свою заглушку, поэтому после генерации Gradle-проекта подменяем её
    // на нашу иконку (192×192 из docs/branding/GhostWard_Icon.svg).
    public class LegacyIcon : IPostGenerateGradleAndroidProject
    {
        const string Source = "Assets/GhostWard/Editor/Branding/AppIconLegacy.png";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            if (!File.Exists(Source)) { Debug.LogWarning("[GhostWard] Нет иконки " + Source); return; }
            string res = Path.Combine(unityLibraryPath, "..", "launcher", "src", "main", "res");
            if (!Directory.Exists(res)) { Debug.LogWarning("[GhostWard] Не найдена папка ресурсов: " + res); return; }

            var bytes = File.ReadAllBytes(Source);
            int n = 0;
            foreach (var dpi in new[] { "mdpi", "hdpi", "xhdpi", "xxhdpi", "xxxhdpi" })
            {
                string dir = Path.Combine(res, "mipmap-" + dpi);
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, "app_icon.png"), bytes);
                n++;
            }
            Debug.Log($"[GhostWard] Обычная иконка app_icon.png заменена ({n} плотностей)");
        }
    }
}
