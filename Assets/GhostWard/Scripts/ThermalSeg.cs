using System.Collections;
using System.Diagnostics;
using Unity.Collections;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace GhostWard
{
    // Распознавание людей и животных для тепловизора: нейросеть LRASPP MobileNetV3 (torchvision, BSD)
    // прямо на телефоне. Вход — выпрямленный кадр 224×224, выход — маска: R = человек, G = животное.
    //
    // Без остановок кадра:
    //  • кадр забирается с видеокарты асинхронно (AsyncGPUReadback), а не синхронным чтением;
    //  • модель считается на процессоре по частям — не дольше ~2 мс за кадр; на видеокарте первый запуск
    //    компилирует шейдеры (зависание), а на части мобильных видеокарт вычисления нестабильны;
    //  • маска собирается в обычную текстуру; на средней графике расчёт реже.
    // Работает, только пока выбран тепловизор и качество графики не «экономное».
    public class ThermalSeg : MonoBehaviour
    {
        const int Size = 224;
        const float Budget = 2f;          // мс работы нейросети за кадр

        public Texture Mask => maskTex;
        public bool PersonInView { get; private set; }
        public bool AnimalInView { get; private set; }
        public bool HasResult { get; private set; }
        public bool Running => enabledByGame && worker != null;

        Worker worker;
        Tensor<float> input;
        RenderTexture upright;
        Material uprightMat;
        Texture2D maskTex;
        Color32[] maskPixels;
        IEnumerator running;
        bool resultPending, enabledByGame, readbackPending;
        float timer;
        float[] pixels;                    // вход модели: 3 × 224 × 224
        readonly Stopwatch watch = new();

        // пауза между расчётами: на средней графике реже
        static float Interval => Settings.Quality >= 2 ? 0.35f : 0.7f;

        public void Init()
        {
            var asset = Resources.Load<ModelAsset>("ThermalSeg");
            if (asset == null) { Debug.LogWarning("[GhostWard] Модель тепловизора не найдена"); return; }
            try
            {
                worker = new Worker(ModelLoader.Load(asset), BackendType.CPU);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GhostWard] Нейросеть тепловизора недоступна: " + e.Message);
                worker = null;
                return;
            }
            input = new Tensor<float>(new TensorShape(1, 3, Size, Size));
            pixels = new float[3 * Size * Size];
            // sRGB: байты из видеокарты — обычные цвета 0..255, как у картинок, на которых училась модель
            upright = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            uprightMat = new Material(Shader.Find("GhostWard/Upright"));
            maskTex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            maskPixels = new Color32[Size * Size];
        }

        public void SetActive(bool on)
        {
            enabledByGame = on && Settings.Quality > 0;
            if (!enabledByGame) PersonInView = AnimalInView = false;
        }

        // source — кадр камеры как есть; angle/mirror — как его разворачивает фон, чтобы кадр стал прямым
        public void Tick(Texture source, int angle, bool mirror)
        {
            if (!Running || source == null) return;

            if (readbackPending) return;   // кадр ещё едет с видеокарты

            // идёт расчёт — продвигаем его, пока не исчерпан бюджет кадра
            if (running != null)
            {
                try
                {
                    watch.Restart();
                    while (watch.Elapsed.TotalMilliseconds < Budget)
                        if (!running.MoveNext()) { running = null; resultPending = true; break; }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[GhostWard] Ошибка нейросети тепловизора: " + e.Message);
                    running = null;
                    enabledByGame = false;
                }
                return;
            }

            if (resultPending)
            {
                // фоновые задачи процессора ещё считают — ждём, не блокируя кадр
                if (worker.PeekOutput() is Tensor<float> o && !o.IsReadbackRequestDone()) return;
                resultPending = false;
                ReadResult();
            }

            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = Interval;

            uprightMat.SetFloat("_Angle", angle);
            uprightMat.SetFloat("_Mirror", mirror ? 1f : 0f);
            Graphics.Blit(source, upright, uprightMat);
            readbackPending = true;
            AsyncGPUReadback.Request(upright, 0, TextureFormat.RGBA32, OnFrame);
        }

        // кадр пришёл: байты → тензор (NCHW, строки сверху вниз), запуск модели
        void OnFrame(AsyncGPUReadbackRequest req)
        {
            readbackPending = false;
            if (req.hasError || worker == null || !enabledByGame) return;
            NativeArray<byte> data = req.GetData<byte>();
            int plane = Size * Size;
            const float k = 1f / 255f;
            for (int row = 0; row < Size; row++)
            {
                int y = Size - 1 - row;            // в кадре видеокарты первая строка — нижняя
                int src = row * Size * 4, dst = y * Size;
                for (int x = 0; x < Size; x++, src += 4, dst++)
                {
                    pixels[dst] = data[src] * k;
                    pixels[plane + dst] = data[src + 1] * k;
                    pixels[2 * plane + dst] = data[src + 2] * k;
                }
            }
            input.Upload(pixels);
            running = worker.ScheduleIterable(input);
        }

        // результат готов (расчёт на процессоре завершён) — маска в текстуру и проверка, есть ли кто в кадре
        void ReadResult()
        {
            if (worker.PeekOutput() is not Tensor<float> output) return;
            var a = output.DownloadToArray();
            int plane = Size * Size;
            int person = 0, animal = 0;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int t = y * Size + x;
                    float p = a[t], q = a[plane + t];
                    if (p > 0.5f) person++;
                    if (q > 0.5f) animal++;
                    // тензор идёт сверху вниз, текстура — снизу вверх
                    maskPixels[(Size - 1 - y) * Size + x] = new Color32((byte)(p * 255f), (byte)(q * 255f), 0, 255);
                }
            maskTex.SetPixels32(maskPixels);
            maskTex.Apply(false);
            PersonInView = person > plane * 0.02f;
            AnimalInView = animal > plane * 0.01f;
            HasResult = true;
        }

        void OnDestroy()
        {
            worker?.Dispose();
            input?.Dispose();
            if (upright != null) upright.Release();
        }
    }
}
