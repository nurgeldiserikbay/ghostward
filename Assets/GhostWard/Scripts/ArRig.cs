using System.Collections;
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
        AROcclusionManager occlusion;
        int frames;

        public static IEnumerator TryCreate(System.Action<ArRig> done, System.Action<string> log)
        {
            var loader = XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null
                ? XRGeneralSettings.Instance.Manager.activeLoader
                : null;
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

            var sessionGo = new GameObject("AR Session");
            var session = sessionGo.AddComponent<ARSession>();
            session.attemptUpdate = false; // сами в Маркет не отправляем
            sessionGo.AddComponent<ARInputManager>();

            // На части телефонов ARCore отвечает «нужно установить», хотя телефон не поддерживается, —
            // Маркет тогда показывает «устройство несовместимо». Поэтому установку предлагаем,
            // только если игрок сам нажал «Установить AR» в настройках; иначе тихо уходим в гироскоп.
            yield return ARSession.CheckAvailability();
            if (ARSession.state == ARSessionState.NeedsInstall && Settings.ArInstallRequested)
            {
                Settings.ArInstallRequested = false;
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
            camGo.AddComponent<ARCameraBackground>();
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
        }

        public string Diagnostics()
        {
            string depth = occlusion != null ? occlusion.currentEnvironmentDepthMode.ToString() : "-";
            return $"AR {ARSession.state} / {ARSession.notTrackingReason}\nкадры {frames}  глубина {depth}";
        }

        // полностью отпускаем ARCore, чтобы камера освободилась для режима гироскопа
        public void Shutdown()
        {
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
