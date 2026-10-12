using Unity.InferenceEngine;
using UnityEngine;

namespace GhostWard
{
    // Распознавание людей и животных для тепловизора: нейросеть LRASPP MobileNetV3 (torchvision, BSD)
    // прямо на телефоне. Вход — выпрямленный кадр 224×224, выход — маска: R = человек, G = животное.
    // Работает, только пока выбран тепловизор и качество графики не «экономное».
    public class ThermalSeg : MonoBehaviour
    {
        const int Size = 224;
        const float Interval = 0.15f;      // ~7 запусков в секунду
        const float PresenceInterval = 1f; // раз в секунду — есть ли кто-то в кадре

        public RenderTexture Mask { get; private set; }
        public bool PersonInView { get; private set; }
        public bool AnimalInView { get; private set; }
        public bool Running => enabledByGame && worker != null;

        Worker worker;
        Tensor<float> input;
        RenderTexture upright;
        Material uprightMat;
        float timer, presenceTimer;
        bool enabledByGame;

        public void Init()
        {
            var asset = Resources.Load<ModelAsset>("ThermalSeg");
            if (asset == null) { Debug.LogWarning("[GhostWard] Модель тепловизора не найдена"); return; }
            var model = ModelLoader.Load(asset);
            var backend = SystemInfo.supportsComputeShaders ? BackendType.GPUCompute : BackendType.GPUPixel;
            worker = new Worker(model, backend);
            input = new Tensor<float>(new TensorShape(1, 3, Size, Size));
            upright = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGB32);
            Mask = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear };
            uprightMat = new Material(Shader.Find("GhostWard/Upright"));
        }

        public void SetActive(bool on) => enabledByGame = on && Settings.Quality > 0;

        // source — кадр камеры как есть; angle/mirror — как его разворачивает фон, чтобы кадр стал прямым
        public void Tick(Texture source, int angle, bool mirror)
        {
            if (!Running || source == null)
            {
                PersonInView = AnimalInView = false;
                return;
            }
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = Interval;

            uprightMat.SetFloat("_Angle", angle);
            uprightMat.SetFloat("_Mirror", mirror ? 1f : 0f);
            Graphics.Blit(source, upright, uprightMat);
            TextureConverter.ToTensor(upright, input);
            worker.Schedule(input);
            var output = worker.PeekOutput() as Tensor<float>;
            if (output == null) return;
            TextureConverter.RenderToTexture(output, Mask);

            presenceTimer -= Interval;
            if (presenceTimer > 0f) return;
            presenceTimer = PresenceInterval;
            var cpu = output.DownloadToArray();
            float person = 0f, animal = 0f;
            int plane = Size * Size;
            for (int i = 0; i < plane; i += 7)
            {
                if (cpu[i] > 0.5f) person++;
                if (cpu[plane + i] > 0.5f) animal++;
            }
            float samples = plane / 7f;
            PersonInView = person / samples > 0.02f;
            AnimalInView = animal / samples > 0.01f;
        }

        void OnDestroy()
        {
            worker?.Dispose();
            input?.Dispose();
            if (upright != null) upright.Release();
            if (Mask != null) Mask.Release();
        }
    }
}
