using System.Collections;
using System.Diagnostics;
using Unity.Collections;
using Unity.InferenceEngine;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GhostWard
{
    // Глубина по одному кадру камеры — нейросеть Depth Anything V2 Small (Apache 2.0) прямо на телефоне.
    // Вход — кадр Width×Height (стороны кратны 14 — размеру «патча» модели), выход — «обратная глубина»
    // (чем больше, тем ближе) в относительных единицах: у каждого кадра свой масштаб, его подгоняет
    // RoomMap по полу и по уже известным местам. Считается на процессоре по частям, чтобы не было пауз.
    public class RoomDepth
    {
        static readonly float[] Mean = { 0.485f, 0.456f, 0.406f }, Std = { 0.229f, 0.224f, 0.225f };

        public int Width { get; private set; }
        public int Height { get; private set; }

        Worker worker;
        Tensor<float> input;
        float[] pixels;
        IEnumerator running;
        bool pending;
        readonly Stopwatch watch = new();

        public bool Ready => worker != null;
        public bool Busy => running != null || pending;

        // размер входа под соотношение сторон кадра: около 200 «патчей» — ~1 с на телефоне
        public static void SizeFor(float aspect, out int width, out int height)
        {
            int rows = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(200f / Mathf.Max(aspect, 0.2f))), 6, 30);
            int cols = Mathf.Clamp(Mathf.RoundToInt(200f / rows), 6, 30);
            width = cols * 14;
            height = rows * 14;
        }

        public void Init(int width, int height)
        {
            Width = width;
            Height = height;
            var asset = Resources.Load<ModelAsset>("Depth");
            if (asset == null) { Debug.LogWarning("[GhostWard] Модель глубины не найдена"); return; }
            try
            {
                worker = new Worker(ModelLoader.Load(asset), BackendType.CPU);
                input = new Tensor<float>(new TensorShape(1, 3, height, width));
                pixels = new float[3 * width * height];
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GhostWard] Глубина недоступна: " + e.Message);
                worker = null;
            }
        }

        // кадр RGBA (строки снизу вверх, как из видеокарты) → вход модели (NCHW, сверху вниз, нормировка ImageNet)
        void Fill(NativeArray<byte> frame)
        {
            int plane = Width * Height;
            for (int row = 0; row < Height; row++)
            {
                int y = Height - 1 - row;
                int src = row * Width * 4, dst = y * Width;
                for (int x = 0; x < Width; x++, src += 4, dst++)
                {
                    pixels[dst] = (frame[src] / 255f - Mean[0]) / Std[0];
                    pixels[plane + dst] = (frame[src + 1] / 255f - Mean[1]) / Std[1];
                    pixels[2 * plane + dst] = (frame[src + 2] / 255f - Mean[2]) / Std[2];
                }
            }
            input.Upload(pixels);
        }

        public void Begin(NativeArray<byte> frame)
        {
            if (!Ready || Busy) return;
            Fill(frame);
            running = worker.ScheduleIterable(input);
        }

        // продвинуть расчёт не дольше budgetMs; true — результат готов (Take)
        public bool Step(float budgetMs)
        {
            if (!Ready) { running = null; pending = false; return false; }
            if (running != null)
            {
                watch.Restart();
                try
                {
                    while (watch.Elapsed.TotalMilliseconds < budgetMs)
                        if (!running.MoveNext()) { running = null; pending = true; break; }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[GhostWard] Ошибка глубины: " + e.Message);
                    running = null;
                    worker?.Dispose();
                    worker = null;
                }
                return false;
            }
            if (!pending) return false;
            return worker.PeekOutput() is not Tensor<float> o || o.IsReadbackRequestDone();
        }

        // забрать результат: Width×Height, строки сверху вниз
        public float[] Take()
        {
            pending = false;
            return (worker.PeekOutput() as Tensor<float>).DownloadToArray();
        }

        // для редактора: посчитать сразу
        public float[] RunBlocking(NativeArray<byte> frame)
        {
            Fill(frame);
            worker.Schedule(input);
            return (worker.PeekOutput() as Tensor<float>).DownloadToArray();
        }

        public void Dispose()
        {
            worker?.Dispose();
            input?.Dispose();
            worker = null;
            input = null;
            running = null;
            pending = false;
        }
    }
}
