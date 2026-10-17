using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GhostWard
{
    // Запасной режим без ARCore: видео с задней камеры фоном, поворот камеры по гироскопу.
    // Сущности висят вокруг игрока; ходить нельзя, но крутиться на месте — да.
    // В редакторе: поворот камеры зажатой правой кнопкой мыши.
    public class GyroRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }

        static readonly int ModeId = Shader.PropertyToID("_Mode");
        static readonly int GlitchId = Shader.PropertyToID("_Glitch");
        static readonly int StyleId = Shader.PropertyToID("_Style");
        static readonly int AltId = Shader.PropertyToID("_Alt");

        WebCamTexture webcam;
        Material feedMat;
        ThermalSeg seg;
        bool coldPerson;

        public RoomMap Room { get; private set; }
        public bool PersonInView => seg != null && seg.PersonInView;
        public bool AnimalInView => seg != null && seg.AnimalInView;
        RawImage background;
        RectTransform canvasRect;
        Vector2 editorLook;
        string gyroSource = "нет";
        string camStatus = "нет";

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
            Input.gyro.enabled = true;
            rig.seg = go.AddComponent<ThermalSeg>();
            rig.seg.Init();
            rig.Room = RoomMap.Create(rig);
            rig.CreateBackground();
            return rig;
        }

        void CreateBackground()
        {
            // холст далеко за сущностями, чтобы видео рисовалось первым
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
            feedMat = new Material(Shader.Find("GhostWard/Feed"));
            background.material = feedMat;
            var rt = background.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                camStatus = "ждём разрешения";
                var cb = new UnityEngine.Android.PermissionCallbacks();
                cb.PermissionGranted += _ => StartWebcam();
                cb.PermissionDenied += _ => camStatus = "разрешение отклонено";
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera, cb);
                return;
            }
#endif
            StartWebcam();
        }

        public WebCamTexture Webcam => webcam;

        void StartWebcam()
        {
            string device = null;
            foreach (var d in WebCamTexture.devices)
                if (!d.isFrontFacing) { device = d.name; break; }
            if (device == null && WebCamTexture.devices.Length > 0) device = WebCamTexture.devices[0].name;
            if (device == null) { camStatus = "камера не найдена"; return; }

            webcam = new WebCamTexture(device, 1280, 720, 30);
            webcam.Play();
            background.texture = webcam;
            background.color = Color.white;
            camStatus = "запуск";
        }

        // шагомер (Game.OnSteps) сдвигает игрока: камера плавно догоняет место, куда он дошёл
        Vector3 walkTarget = new(0f, 1.4f, 0f);
        public void Walk(Vector3 delta)
        {
            delta.y = 0f;
            walkTarget += delta;
        }

        void Update()
        {
            transform.position = Vector3.MoveTowards(transform.position, walkTarget, Time.deltaTime * 1.8f);
            UpdateRotation();
            FitBackground();
            if (webcam != null && webcam.width >= 32)
                seg.Tick(webcam, webcam.videoRotationAngle, webcam.videoVerticallyMirrored);
        }

        // гироскоп: сначала датчик Input System, если его нет — старый Input.gyro
        void UpdateRotation()
        {
            var sensor = AttitudeSensor.current;
            if (sensor != null)
            {
                if (!sensor.enabled) InputSystem.EnableDevice(sensor);
                var q = sensor.attitude.ReadValue();
                if (q != default && q != Quaternion.identity)
                {
                    gyroSource = "AttitudeSensor";
                    transform.rotation = Convert(q);
                    return;
                }
            }

            if (SystemInfo.supportsGyroscope)
            {
                var q = Input.gyro.attitude;
                if (q != default && q != Quaternion.identity)
                {
                    gyroSource = "Input.gyro";
                    transform.rotation = Convert(q);
                    return;
                }
            }

            gyroSource = "нет";
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                editorLook += mouse.delta.ReadValue() * 0.15f;
                editorLook.y = Mathf.Clamp(editorLook.y, -80f, 80f);
            }
            transform.rotation = Quaternion.Euler(-editorLook.y, editorLook.x, 0f);
        }

        // ориентация телефона (правая система координат) → камера Unity, портретный экран
        static Quaternion Convert(Quaternion q) => Quaternion.Euler(90f, 0f, 0f) * new Quaternion(q.x, q.y, -q.z, -q.w);

        // растягиваем видео с обрезкой краёв (cover) с учётом поворота сенсора
        void FitBackground()
        {
            if (webcam == null) return;
            if (webcam.width < 32) { camStatus = webcam.isPlaying ? "ждём кадр" : "не запустилась"; return; }
            camStatus = $"{webcam.width}x{webcam.height} {webcam.videoRotationAngle}°";

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

        // вид камеры: 0 — тёмная, 1 — ночной режим; glitch — помехи рядом с сущностью
        public void SetLook(Vision style, Vision episodeStyle, float episode, float glitch)
        {
            feedMat.SetFloat(StyleId, (int)style);
            feedMat.SetFloat(AltId, (int)episodeStyle);
            feedMat.SetFloat(ModeId, episode);
            feedMat.SetFloat(GlitchId, glitch);
            bool segOn = seg != null && seg.Running && seg.Mask != null && style == Vision.Thermal;
            feedMat.SetFloat("_SegOn", segOn ? 1f : 0f);
            if (segOn)
            {
                feedMat.SetTexture("_SegTex", seg.Mask);
                feedMat.SetFloat("_SegAngle", webcam != null ? webcam.videoRotationAngle : 0);
                feedMat.SetFloat("_SegMirror", webcam != null && webcam.videoVerticallyMirrored ? 1f : 0f);
                feedMat.SetFloat("_ColdPerson", coldPerson ? 1f : 0f);
            }

            bool maskReady = seg != null && seg.Running && seg.HasResult;
            float p = maskReady ? possess : 0f;
            feedMat.SetFloat("_Possess", p);
            if (p > 0f)
            {
                feedMat.SetTexture("_SegTex", seg.Mask);
                feedMat.SetFloat("_SegAngle", webcam != null ? webcam.videoRotationAngle : 0);
                feedMat.SetFloat("_SegMirror", webcam != null && webcam.videoVerticallyMirrored ? 1f : 0f);
                feedMat.SetFloat("_FaceOn", seg.FaceFound ? 1f : 0f);
                feedMat.SetVector("_EyeA", seg.EyeA);
                feedMat.SetVector("_EyeB", seg.EyeB);
                feedMat.SetVector("_Mouth", seg.Mouth);
                feedMat.SetFloat("_FaceSize", seg.FaceSize);
            }
        }

        // одержимость 0..1: маска на человека и лицо в любом виде камеры
        float possess;
        public void SetPossession(float amount, bool wantFace)
        {
            possess = amount;
            if (seg != null) seg.FaceWanted = wantFace;
        }

        // тепловизор с распознаванием живых; coldPerson — «одержимость»: человек холодный
        public void SetThermalSeg(bool on, bool coldPerson)
        {
            this.coldPerson = coldPerson;
            if (seg != null) seg.SetActive(on);
        }

        public string Diagnostics() => $"гироскоп: {gyroSource}\nкамера: {camStatus}";

        const int SampleSize = 64;
        // вырез кадра «как на экране» для карты глубины: параметры того же преобразования, что в SampleLuma
        public void SetupViewCrop(Material m)
        {
            m.SetVector("_Size", canvasRect.rect.size);
            m.SetVector("_Img", background.rectTransform.sizeDelta);
            m.SetFloat("_Angle", webcam != null ? webcam.videoRotationAngle : 0);
            m.SetFloat("_Mirror", webcam != null && webcam.videoVerticallyMirrored ? 1f : 0f);
        }

        RenderTexture sampleRt;
        Texture2D sampleTex;

        // Яркость кадра в точках экрана (viewport 0..1). -1 — точка вне видео.
        // Видео уменьшается до 64×64 на GPU, на CPU читается только этот маленький кадр.
        public bool SampleLuma(Vector2[] viewport, float[] luma)
        {
            if (webcam == null || webcam.width < 32) return false;

            if (sampleRt == null)
            {
                sampleRt = new RenderTexture(SampleSize, SampleSize, 0, RenderTextureFormat.ARGB32);
                sampleTex = new Texture2D(SampleSize, SampleSize, TextureFormat.RGBA32, false);
            }
            Graphics.Blit(webcam, sampleRt);
            var prev = RenderTexture.active;
            RenderTexture.active = sampleRt;
            sampleTex.ReadPixels(new Rect(0, 0, SampleSize, SampleSize), 0, 0, false);
            sampleTex.Apply(false);
            RenderTexture.active = prev;
            var px = sampleTex.GetPixels32();

            // экран → видео: обратное к тому, как FitBackground растянул и повернул картинку
            var size = canvasRect.rect.size;
            var img = background.rectTransform.sizeDelta;
            float rad = webcam.videoRotationAngle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            for (int i = 0; i < viewport.Length; i++)
            {
                var l = new Vector2((viewport[i].x - 0.5f) * size.x, (viewport[i].y - 0.5f) * size.y);
                var r = new Vector2(l.x * cos - l.y * sin, l.x * sin + l.y * cos);
                float u = r.x / img.x + 0.5f, v = r.y / img.y + 0.5f;
                if (webcam.videoVerticallyMirrored) v = 1f - v;
                if (u < 0f || u > 1f || v < 0f || v > 1f) { luma[i] = -1f; continue; }
                var c = px[Mathf.Min((int)(v * SampleSize), SampleSize - 1) * SampleSize + Mathf.Min((int)(u * SampleSize), SampleSize - 1)];
                luma[i] = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
            }
            return true;
        }

        void OnDestroy()
        {
            if (webcam != null) webcam.Stop();
            if (sampleRt != null) sampleRt.Release();
        }
    }
}
