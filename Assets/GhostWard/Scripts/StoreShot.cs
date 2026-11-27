#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GhostWard
{
    // Только в редакторе: F12 в режиме Play сохраняет кадр окна Game в Builds/store-art/raw/N.png
    // (для скриншотов Google Play). Окно Game поставить в 1080x1920 (портрет). В сборку не попадает.
    public class StoreShot : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            var go = new GameObject("StoreShot (editor)");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            go.AddComponent<StoreShot>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.f12Key.wasPressedThisFrame) return;

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "store-art", "raw"));
            Directory.CreateDirectory(dir);
            int n = 1;
            while (File.Exists(Path.Combine(dir, n + ".png"))) n++;
            string path = Path.Combine(dir, n + ".png");
            ScreenCapture.CaptureScreenshot(path);

            if (Screen.width > Screen.height)
                Debug.LogWarning($"[StoreShot] Окно Game горизонтальное ({Screen.width}x{Screen.height}) — для Google Play поставьте 1080x1920.");
            Debug.Log($"[StoreShot] Сохранён {path} ({Screen.width}x{Screen.height})");
        }
    }
}
#endif
