using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;

namespace GhostWard
{
    // Настоящее AR: ARCore отслеживает положение телефона, призраки привязаны к комнате,
    // карта глубины прячет их за реальными предметами.
    public class ArRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public bool DepthOcclusion { get; private set; }

        GameObject sessionGo;
        AROcclusionManager occlusion;

        public static IEnumerator TryCreate(System.Action<ArRig> done)
        {
            var sessionGo = new GameObject("AR Session");
            sessionGo.AddComponent<ARSession>();
            sessionGo.AddComponent<ARInputManager>();

            // ждём проверки поддержки; при необходимости ARSession сам предложит установить сервисы AR
            float timeout = 15f;
            while (timeout > 0f && ARSession.state is ARSessionState.None or ARSessionState.CheckingAvailability
                       or ARSessionState.NeedsInstall or ARSessionState.Installing)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (ARSession.state is ARSessionState.Unsupported or ARSessionState.None
                or ARSessionState.CheckingAvailability or ARSessionState.NeedsInstall or ARSessionState.Installing)
            {
                Destroy(sessionGo);
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

            camGo.AddComponent<ARCameraManager>();
            camGo.AddComponent<ARCameraBackground>();
            camGo.AddComponent<AudioListener>();

            var pose = camGo.AddComponent<TrackedPoseDriver>();
            var pos = new InputAction("Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            pos.AddBinding("<HandheldARInputDevice>/devicePosition");
            var rot = new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            rot.AddBinding("<HandheldARInputDevice>/deviceRotation");
            pose.positionInput = new InputActionProperty(pos);
            pose.rotationInput = new InputActionProperty(rot);

            occlusion = camGo.AddComponent<AROcclusionManager>();
            occlusion.requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
            occlusion.environmentDepthTemporalSmoothingRequested = true;
            occlusion.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.PreferEnvironmentOcclusion;

            origin.Camera = Cam;
            origin.CameraFloorOffsetObject = offset;
        }

        void Update()
        {
            DepthOcclusion = occlusion != null && occlusion.currentEnvironmentDepthMode != EnvironmentDepthMode.Disabled;
        }

        void OnDestroy()
        {
            if (sessionGo != null) Destroy(sessionGo);
        }
    }
}
