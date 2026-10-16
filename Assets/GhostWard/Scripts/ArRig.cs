using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using TrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;

namespace GhostWard
{
    // Настоящее AR: ARCore отслеживает положение телефона, сущности привязаны к комнате,
    // карта глубины прячет их за реальными предметами.
    public class ArRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public bool ReceivingFrames => frames > 0;


        GameObject sessionGo;
        ARCameraManager cameraManager;
        ARCameraBackground background;
        AROcclusionManager occlusion;
        ARPlaneManager planes;
        ARRaycastManager raycaster;
        readonly List<ARRaycastHit> hits = new();
        int frames;

        // Сервисы Google AR (ARCore) уже стоят на телефоне? Если нет — AR не трогаем вовсе:
        // любое обращение к ARCore на неподдерживаемом телефоне норовит открыть Маркет.
        static bool ArCoreInstalled()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var pm = activity.Call<AndroidJavaObject>("getPackageManager");
                using var info = pm.Call<AndroidJavaObject>("getPackageInfo", "com.google.ar.core", 0);
                return info != null;
            }
            catch (System.Exception) { return false; }   // NameNotFoundException — не установлены
#else
            return true;
#endif
        }

        public static IEnumerator TryCreate(System.Action<ArRig> done, System.Action<string> log)
        {
            // AR выключен в настройках или сервисов AR нет (и игрок сам не просил их поставить) — режим гироскопа
            // просьба «Установить AR» — одноразовая: на неподдерживаемом телефоне ARCore отвечает «не поддерживается»,
            // и без сброса игра поднимала бы AR (и Маркет) при каждом запуске
            bool installRequested = Settings.ArInstallRequested;
            Settings.ArInstallRequested = false;
            if (Settings.ArMode == 1 || (!ArCoreInstalled() && !installRequested))
            {
                log(Settings.ArMode == 1 ? "AR: выключен в настройках" : "AR: сервисы AR не установлены");
                done(null);
                yield break;
            }

            // загрузчик XR запускаем сами (автозапуск при старте приложения выключен)
            var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager != null && manager.activeLoader == null)
            {
                yield return manager.InitializeLoader();
                if (manager.activeLoader != null) manager.StartSubsystems();
            }

            var loader = manager != null ? manager.activeLoader : null;
            if (loader == null)
            {
                log("AR: загрузчик XR не запущен");
                done(null);
                yield break;
            }

            // AR выключен в настройках — сразу режим гироскопа
            if (Settings.ArMode == 1)
            {
                log("AR: выключен в настройках");
                StopXr();
                done(null);
                yield break;
            }

            // объект создаётся выключенным: ARSession проверяет и ставит ARCore уже в OnEnable,
            // и если не успеть выключить attemptUpdate, телефон без поддержки AR уходит в Маркет
            var sessionGo = new GameObject("AR Session");
            sessionGo.SetActive(false);
            var session = sessionGo.AddComponent<ARSession>();
            session.attemptUpdate = false; // сами в Маркет не отправляем
            sessionGo.AddComponent<ARInputManager>();
            sessionGo.SetActive(true);

            // На части телефонов ARCore отвечает «нужно установить», хотя телефон не поддерживается, —
            // Маркет тогда показывает «устройство несовместимо». Поэтому установку предлагаем,
            // только если игрок сам нажал «Установить AR» в настройках; иначе тихо уходим в гироскоп.
            yield return ARSession.CheckAvailability();
            if (ARSession.state == ARSessionState.NeedsInstall && installRequested)
            {
                log("AR: установка сервисов AR");
                yield return ARSession.Install();
            }

            float timeout = 10f;
            while (timeout > 0f && ARSession.state is ARSessionState.None or ARSessionState.CheckingAvailability or ARSessionState.Installing)
            {
                timeout -= Time.unscaledDeltaTime;
                log("AR: " + ARSession.state);
                yield return null;
            }

            if (ARSession.state < ARSessionState.Ready)
            {
                log("AR недоступен: " + ARSession.state);
                Destroy(sessionGo);
                StopXr();
                done(null);
                yield break;
            }

            var originGo = new GameObject("XR Origin");
            var rig = originGo.AddComponent<ArRig>();
            rig.sessionGo = sessionGo;
            rig.Build(originGo);
            done(rig);
        }

        void Build(GameObject originGo)
        {
            var origin = originGo.AddComponent<XROrigin>();
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(originGo.transform, false);

            var camGo = new GameObject("AR Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(offset.transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Color.black;
            Cam.nearClipPlane = 0.05f;
            Cam.farClipPlane = 40f;

            cameraManager = camGo.AddComponent<ARCameraManager>();
            cameraManager.frameReceived += _ => frames++;
            background = camGo.AddComponent<ARCameraBackground>();
            camGo.AddComponent<AudioListener>();

            var pose = camGo.AddComponent<TrackedPoseDriver>();
            var pos = new InputAction("Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            pos.AddBinding("<HandheldARInputDevice>/devicePosition");
            var rot = new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            rot.AddBinding("<HandheldARInputDevice>/deviceRotation");
            pose.positionInput = new InputActionProperty(pos);
            pose.rotationInput = new InputActionProperty(rot);
            pos.Enable();
            rot.Enable();

            occlusion = camGo.AddComponent<AROcclusionManager>();
            occlusion.requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
            occlusion.environmentDepthTemporalSmoothingRequested = true;
            occlusion.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.PreferEnvironmentOcclusion;

            origin.Camera = Cam;
            origin.CameraFloorOffsetObject = offset;

            // плоскости: пол (куда ставить ноги) и стены (за которые не ставить); без отрисовки
            planes = originGo.AddComponent<ARPlaneManager>();
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            raycaster = originGo.AddComponent<ARRaycastManager>();
        }

        // Пол: самая низкая большая горизонтальная плоскость на 0.9–2.2 м ниже телефона
        public bool TryFloor(out float y)
        {
            y = 0f;
            if (planes == null) return false;
            float camY = Cam.transform.position.y;
            bool found = false;
            foreach (var p in planes.trackables)
            {
                if (p.alignment != PlaneAlignment.HorizontalUp || p.trackingState != TrackingState.Tracking) continue;
                if (p.size.x * p.size.y < 0.3f) continue;
                float py = p.transform.position.y;
                float below = camY - py;
                if (below < 0.9f || below > 2.2f) continue;   // выше — это стол, а не пол
                if (!found || py < y) { y = py; found = true; }
            }
            return found;
        }

        // Сколько свободного места по направлению (по горизонтали): до стены или предмета — по плоскостям
        // и карте глубины ARCore. -1 — ничего не нашли (стена не видна — не ограничиваем).
        public float MaxDistance(Vector3 from, Vector3 dir)
        {
            if (raycaster == null) return -1f;
            float best = -1f;
            // на уровне глаз и чуть ниже — где у фигуры туловище
            for (int k = 0; k < 2; k++)
            {
                var d = (dir + Vector3.down * (k == 0 ? 0f : 0.25f)).normalized;
                if (!raycaster.Raycast(new Ray(from, d), hits, TrackableType.PlaneWithinPolygon | TrackableType.Depth)) continue;
                foreach (var h in hits)
                {
                    // пол под ногами не стена
                    if (h.trackable is ARPlane p && p.alignment == PlaneAlignment.HorizontalUp) continue;
                    var flat = h.pose.position - from;
                    flat.y = 0f;
                    float dist = flat.magnitude;
                    if (dist < 0.2f) continue;
                    best = best < 0f ? dist : Mathf.Min(best, dist);
                }
            }
            return best < 0f ? -1f : best - 0.3f;
        }

        RenderTexture sampleRt;
        Texture2D sampleTex;
        const int SampleSize = 64;

        // Яркость кадра в точках экрана (viewport 0..1), как GyroRig.SampleLuma: фон AR рисуется
        // в маленькую текстуру тем же материалом, что и на экран, — точка экрана совпадает с точкой кадра.
        public bool SampleLuma(Vector2[] viewport, float[] luma)
        {
            if (background == null || background.material == null || frames == 0) return false;
            if (sampleRt == null)
            {
                sampleRt = new RenderTexture(SampleSize, SampleSize, 0, RenderTextureFormat.ARGB32);
                sampleTex = new Texture2D(SampleSize, SampleSize, TextureFormat.RGBA32, false);
            }
            Graphics.Blit(null, sampleRt, background.material);
            var prev = RenderTexture.active;
            RenderTexture.active = sampleRt;
            sampleTex.ReadPixels(new Rect(0, 0, SampleSize, SampleSize), 0, 0, false);
            sampleTex.Apply(false);
            RenderTexture.active = prev;
            var px = sampleTex.GetPixels32();
            for (int i = 0; i < viewport.Length; i++)
            {
                var v = viewport[i];
                if (v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) { luma[i] = -1f; continue; }
                var c = px[Mathf.Min((int)(v.y * SampleSize), SampleSize - 1) * SampleSize + Mathf.Min((int)(v.x * SampleSize), SampleSize - 1)];
                luma[i] = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
            }
            return true;
        }

        public string Diagnostics()
        {
            string depth = occlusion != null ? occlusion.currentEnvironmentDepthMode.ToString() : "-";
            return $"AR {ARSession.state} / {ARSession.notTrackingReason}\nкадры {frames}  глубина {depth}";
        }

        // полностью отпускаем ARCore, чтобы камера освободилась для режима гироскопа
        public void Shutdown()
        {
            if (sampleRt != null) sampleRt.Release();
            Destroy(gameObject);
            if (sessionGo != null) Destroy(sessionGo);
            StopXr();
        }

        static void StopXr()
        {
            var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null || manager.activeLoader == null) return;
            manager.StopSubsystems();
            manager.DeinitializeLoader();
        }
    }
}
