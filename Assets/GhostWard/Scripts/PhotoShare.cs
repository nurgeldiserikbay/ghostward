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
    // Видение: сущность показывается на миг — стоит, проплывает вбок, мерцает. Не нападает.
    public class Apparition : MonoBehaviour
    {
        Material mat;
        float life, total, blink, alpha = 0.6f;
        Transform cam;
        Vector3 drift;
        bool flicker;

        public bool Alive => this != null && life > 0f;
        public void Kill() { if (this != null) Destroy(gameObject); }

        // помехи камеры: видение мерцает несколько долей секунды
        float glitchTime;
        public void Flicker(float seconds) { if (this != null) glitchTime = Mathf.Max(glitchTime, seconds); }

        // drift — скорость дрейфа (м/с); flicker — иногда пропадает на миг; alpha — насколько плотная
        public static Apparition Show(EntityDef def, Vector3 pos, Transform cam, float seconds,
                                      Vector3 drift = default, bool flicker = false, float alpha = 0.6f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.name = "Apparition";
            go.transform.position = pos;
            var figure = Entity.FigureOf(def);
            go.transform.localScale = new Vector3(figure.Size.x, figure.Size.y, 1f);
            var a = go.AddComponent<Apparition>();
            a.mat = Entity.CreateMaterial(def, UnityEngine.Random.value * 10f);
            a.mat.SetFloat("_Flip", Entity.ArchOf(def.Kind) == EntityKind.Kikimora ? 1f : 0f);
            a.mat.SetFloat("_World", 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = a.mat;
            a.life = a.total = seconds;
            a.cam = cam;
            a.drift = drift;
            a.flicker = flicker;
            a.alpha = alpha;
            return a;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            transform.position += drift * dt;
            // быстро проступает, держится, медленно тает
            float k = Mathf.Min(Mathf.Clamp01((total - life) / 0.3f), Mathf.Clamp01(life / Mathf.Min(0.6f, total * 0.4f)));
            if (flicker)
            {
                if (blink > 0f) { blink -= dt; k = 0f; }
                else if (UnityEngine.Random.value < dt * 0.6f) blink = UnityEngine.Random.Range(0.06f, 0.25f);
            }
            if (glitchTime > 0f)
            {
                glitchTime -= dt;
                if (UnityEngine.Random.value < 0.45f) k *= 0.08f;
            }
            mat.SetFloat("_Fade", k * alpha);
            var dir = transform.position - cam.position;
            dir.y = 0f;   // стоит вертикально, как сущность
            if (dir.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            if (life <= 0f) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
