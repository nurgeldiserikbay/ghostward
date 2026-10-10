using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GhostWard
{
    // Запасной режим без ARCore: видео с задней камеры фоном, поворот камеры по гироскопу.
    // Призраки висят вокруг игрока; ходить нельзя, но крутиться на месте — да.
    // В редакторе: поворот камеры зажатой правой кнопкой мыши.
    public class GyroRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public bool HasGyro { get; private set; }

        WebCamTexture webcam;
        RawImage background;
        RectTransform canvasRect;
        Vector2 editorLook;

        public static GyroRig Create()
        {
            var go = new GameObject("Gyro Camera") { tag = "MainCamera" };
            var rig = go.AddComponent<GyroRig>();
            rig.Cam = go.AddComponent<Camera>();
            rig.Cam.clearFlags = CameraClearFlags.SolidColor;
            rig.Cam.backgroundColor = new Color(0.03f, 0.03f, 0.05f);
            rig.Cam.nearClipPlane = 0.05f;
            rig.Cam.farClipPlane = 60f;
            rig.Cam.fieldOfView = 60f;
            go.transform.position = new Vector3(0f, 1.4f, 0f);
            go.AddComponent<AudioListener>();

            if (AttitudeSensor.current != null)
            {
                InputSystem.EnableDevice(AttitudeSensor.current);
                rig.HasGyro = true;
            }

            rig.CreateBackground();
            return rig;
        }

        void CreateBackground()
        {
            // холст далеко за призраками, чтобы видео рисовалось первым
            var canvasGo = new GameObject("Camera Feed", typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Cam;
            canvas.planeDistance = 50f;
            canvasRect = canvasGo.GetComponent<RectTransform>();

            var imgGo = new GameObject("Feed", typeof(RawImage));
            imgGo.transform.SetParent(canvasGo.transform, false);
            background = imgGo.GetComponent<RawImage>();
            background.color = Color.black;
            background.raycastTarget = false;
            var rt = background.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                var cb = new UnityEngine.Android.PermissionCallbacks();
                cb.PermissionGranted += _ => StartWebcam();
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera, cb);
                return;
            }
#endif
            StartWebcam();
        }

        void StartWebcam()
        {
            string device = null;
            foreach (var d in WebCamTexture.devices)
                if (!d.isFrontFacing) { device = d.name; break; }
            if (device == null && WebCamTexture.devices.Length > 0) device = WebCamTexture.devices[0].name;
            if (device == null) return;

            webcam = new WebCamTexture(device, 1280, 720, 30);
            webcam.Play();
            background.texture = webcam;
            background.color = Color.white;
        }

        void Update()
        {
            UpdateRotation();
            FitBackground();
        }

        void UpdateRotation()
        {
            if (HasGyro && AttitudeSensor.current != null)
            {
                var q = AttitudeSensor.current.attitude.ReadValue();
                transform.rotation = Quaternion.Euler(90f, 0f, 0f) * new Quaternion(q.x, q.y, -q.z, -q.w);
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                editorLook += mouse.delta.ReadValue() * 0.15f;
                editorLook.y = Mathf.Clamp(editorLook.y, -80f, 80f);
            }
            transform.rotation = Quaternion.Euler(-editorLook.y, editorLook.x, 0f);
        }

        // растягиваем видео с обрезкой краёв (cover) с учётом поворота сенсора
        void FitBackground()
        {
            if (webcam == null || webcam.width < 32) return;
            int angle = webcam.videoRotationAngle;
            bool sideways = angle % 180 != 0;
            float texW = sideways ? webcam.height : webcam.width;
            float texH = sideways ? webcam.width : webcam.height;
            var size = canvasRect.rect.size;
            float scale = Mathf.Max(size.x / texW, size.y / texH);
            var rt = background.rectTransform;
            rt.sizeDelta = new Vector2(webcam.width * scale, webcam.height * scale);
            rt.localEulerAngles = new Vector3(0f, 0f, -angle);
            background.uvRect = webcam.videoVerticallyMirrored ? new Rect(0f, 1f, 1f, -1f) : new Rect(0f, 0f, 1f, 1f);
        }

        void OnDestroy()
        {
            if (webcam != null) webcam.Stop();
        }
    }
}
