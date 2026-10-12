using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace GhostWard
{
    // «Фото сущности»: снимок экрана в момент встречи, сохранение в галерею и «Поделиться».
    public static class PhotoShare
    {
        public static IEnumerator Capture(Action<Texture2D> done)
        {
            yield return new WaitForEndOfFrame();
            done(ScreenCapture.CaptureScreenshotAsTexture());
        }

        public static string SaveLocal(Texture2D tex)
        {
            string path = Path.Combine(Application.persistentDataPath, $"ghostward_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            return path;
        }

        // Кладёт снимок в галерею и открывает системное «Поделиться». false — если не вышло.
        public static bool Share(Texture2D tex, string text)
        {
            string path = SaveLocal(tex);
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
                using var media = new AndroidJavaClass("android.provider.MediaStore$Images$Media");
                string uriString = media.CallStatic<string>("insertImage", resolver, path, "Ghost Ward", text);
                if (string.IsNullOrEmpty(uriString)) return false;

                using var uriClass = new AndroidJavaClass("android.net.Uri");
                using var uri = uriClass.CallStatic<AndroidJavaObject>("parse", uriString);
                using var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND");
                intent.Call<AndroidJavaObject>("setType", "image/png");
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.STREAM", uri);
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text);
                intent.Call<AndroidJavaObject>("addFlags", 1); // FLAG_GRANT_READ_URI_PERMISSION
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Поделиться");
                activity.Call("startActivity", chooser);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GhostWard] Поделиться не вышло: " + e.Message);
                return false;
            }
#else
            Debug.Log("[GhostWard] Фото сохранено: " + path);
            return true;
#endif
        }
    }

    // Мимолётный силуэт сущности — улика «Силуэт» в ночном режиме камеры.
    public class Apparition : MonoBehaviour
    {
        Material mat;
        float life, total;
        Transform cam;

        public static void Show(EntityDef def, Vector3 pos, Transform cam, float seconds)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.name = "Apparition";
            go.transform.position = pos;
            go.transform.localScale = new Vector3(def.Height * 0.8f, def.Height, 1f);
            var a = go.AddComponent<Apparition>();
            a.mat = Entity.CreateMaterial(def, UnityEngine.Random.value * 10f);
            a.mat.SetFloat("_Flip", Entity.ArchOf(def.Kind) == EntityKind.Kikimora ? 1f : 0f);
            go.GetComponent<MeshRenderer>().sharedMaterial = a.mat;
            a.life = a.total = seconds;
            a.cam = cam;
        }

        void Update()
        {
            life -= Time.deltaTime;
            mat.SetFloat("_Fade", Mathf.Clamp01(life / total) * 0.55f);
            var dir = transform.position - cam.position;
            if (dir.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            if (life <= 0f) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
