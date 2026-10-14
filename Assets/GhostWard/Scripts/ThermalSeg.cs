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
    // Работает, пока выбран тепловизор или идёт «одержимость», и качество графики не «экономное».
    //
    // Лицо: BlazeFace (Google MediaPipe, Apache 2.0, из unity/inference-engine-blaze-face) по тому же кадру,
    // только пока нужна маска одержимости (FaceWanted). Глаза и рот — в координатах выпрямленного кадра 0..1
    // (как у маски сегментации), чтобы шейдер камеры положил маску точно на лицо.
    public class ThermalSeg : MonoBehaviour
    {
        const int Size = 224;
        const float Budget = 2f;          // мс работы нейросети за кадр

        public Texture Mask => maskTex;
        public bool PersonInView { get; private set; }
        public bool AnimalInView { get; private set; }
        public bool HasResult { get; private set; }

        // лицо для маски одержимости
        public bool FaceWanted { get; set; }
        public bool FaceFound { get; private set; }
        public Vector2 EyeA { get; private set; }     // глаза, рот — 0..1 в выпрямленном кадре, v снизу вверх
        public Vector2 EyeB { get; private set; }
        public Vector2 Mouth { get; private set; }
        public float FaceSize { get; private set; }   // ширина лица в долях кадра
        const int FaceIn = 128;
        Worker faceWorker;
        Tensor<float> faceInput;
        float[] facePixels;
        float faceSeenAt = -10f;
        public bool Running => enabledByGame && worker != null && !failed;
        bool failed;   // модель упала — больше не пытаемся

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

        // пауза между расчётами: на средней графике реже; во время одержимости — чаще, чтобы маска шла за лицом
        float Interval => FaceWanted ? 0.25f : Settings.Quality >= 2 ? 0.35f : 0.7f;

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

            var face = Resources.Load<ModelAsset>("FaceDetect");
            if (face != null)
            {
                try
                {
                    faceWorker = new Worker(ModelLoader.Load(face), BackendType.CPU);
                    faceInput = new Tensor<float>(new TensorShape(1, FaceIn, FaceIn, 3));
                    facePixels = new float[FaceIn * FaceIn * 3];
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[GhostWard] Поиск лиц недоступен: " + e.Message);
                    faceWorker = null;
                }
            }
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
                    failed = true;
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
            if (FaceWanted && faceWorker != null) DetectFace(data);
            else if (Time.time - faceSeenAt > 0.6f) FaceFound = false;
        }

        // Поиск лица по тому же кадру: 224 → 128 (NHWC, значения −1..1), модель маленькая — считаем сразу.
        void DetectFace(NativeArray<byte> data)
        {
            const float scale = (float)Size / FaceIn;
            for (int y = 0; y < FaceIn; y++)
            {
                int row = Size - 1 - Mathf.Min(Size - 1, (int)(y * scale));   // y — сверху вниз, в кадре — снизу вверх
                for (int x = 0; x < FaceIn; x++)
                {
                    int src = (row * Size + Mathf.Min(Size - 1, (int)(x * scale))) * 4;
                    int dst = (y * FaceIn + x) * 3;
                    facePixels[dst] = data[src] / 127.5f - 1f;
                    facePixels[dst + 1] = data[src + 1] / 127.5f - 1f;
                    facePixels[dst + 2] = data[src + 2] / 127.5f - 1f;
                }
            }
            try
            {
                faceInput.Upload(facePixels);
                faceWorker.Schedule(faceInput);
                var boxes = (faceWorker.PeekOutput(0) as Tensor<float>).DownloadToArray();    // 896 × 16
                var scores = (faceWorker.PeekOutput(1) as Tensor<float>).DownloadToArray();   // 896
                int best = 0;
                for (int i = 1; i < scores.Length; i++) if (scores[i] > scores[best]) best = i;
                float score = 1f / (1f + Mathf.Exp(-scores[best]));
                if (score < 0.6f)
                {
                    if (Time.time - faceSeenAt > 0.6f) FaceFound = false;
                    return;
                }
                Anchor(best, out float ax, out float ay);
                int b = best * 16;
                Vector2 Point(int k) => new(ax + boxes[b + k] / FaceIn, 1f - (ay + boxes[b + k + 1] / FaceIn));
                var eyeA = Point(4);
                var eyeB = Point(6);
                var mouth = Point(10);
                float size = boxes[b + 2] / FaceIn;
                // сглаживаем: модель считает ~4 раза в секунду, маска не должна прыгать
                float k = FaceFound ? 0.55f : 1f;
                EyeA = Vector2.Lerp(EyeA, eyeA, k);
                EyeB = Vector2.Lerp(EyeB, eyeB, k);
                Mouth = Vector2.Lerp(Mouth, mouth, k);
                FaceSize = Mathf.Lerp(FaceSize, size, k);
                FaceFound = true;
                faceSeenAt = Time.time;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GhostWard] Ошибка поиска лиц: " + e.Message);
                faceWorker?.Dispose();
                faceWorker = null;
                FaceFound = false;
            }
        }

        // якоря BlazeFace short-range: сетка 16×16 по 2 якоря, затем 8×8 по 6 — всего 896
        static void Anchor(int i, out float x, out float y)
        {
            int fm, n;
            if (i < 512) { fm = 16; n = 2; }
            else { i -= 512; fm = 8; n = 6; }
            int cell = i / n;
            x = (cell % fm + 0.5f) / fm;
            y = (cell / fm + 0.5f) / fm;
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
            faceWorker?.Dispose();
            faceInput?.Dispose();
            worker = null;
            input = null;
            faceWorker = null;
            faceInput = null;
            if (upright != null) upright.Release();
        }
    }
}
