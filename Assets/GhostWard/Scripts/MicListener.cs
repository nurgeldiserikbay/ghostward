using UnityEngine;

namespace GhostWard
{
    // Громкость с микрофона относительно фонового шума комнаты.
    // Speaking — игрок говорит (вопрос радио духов), Loud — закричал или зашумел (сущность слышит).
    public class MicListener : MonoBehaviour
    {
        public bool Available { get; private set; }
        public bool Denied { get; private set; }
        public float Level { get; private set; }      // 0..1 над фоном
        public bool Speaking => Level > 0.12f;
        public bool Loud => Level > 0.35f;

        AudioClip clip;
        readonly float[] buffer = new float[512];
        float floor = 0.01f;
        bool requested;

        public void Begin()
        {
            if (Available || requested) return;
            requested = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                var cb = new UnityEngine.Android.PermissionCallbacks();
                cb.PermissionGranted += _ => StartMic();
                cb.PermissionDenied += _ => Denied = true;
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, cb);
                return;
            }
#endif
            StartMic();
        }

        void StartMic()
        {
            if (Microphone.devices.Length == 0) { Denied = true; return; }
            clip = Microphone.Start(null, true, 1, 16000);
            Available = clip != null;
        }

        void Update()
        {
            if (!Available) return;
            int pos = Microphone.GetPosition(null);
            if (pos < buffer.Length) return;
            clip.GetData(buffer, pos - buffer.Length);

            float sum = 0f;
            foreach (var v in buffer) sum += v * v;
            float rms = Mathf.Sqrt(sum / buffer.Length);

            // фон комнаты: быстро опускается, медленно поднимается
            floor = rms < floor ? Mathf.Lerp(floor, rms, 0.2f) : Mathf.Lerp(floor, rms, Time.unscaledDeltaTime * 0.05f);
            float above = Mathf.Clamp01((rms - floor * 1.5f) * 12f);
            Level = Mathf.Lerp(Level, above, above > Level ? 0.5f : Time.unscaledDeltaTime * 4f);
        }

        void OnDestroy()
        {
            if (Available) Microphone.End(null);
        }
    }
}
