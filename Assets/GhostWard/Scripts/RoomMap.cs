using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace GhostWard
{
    // Карта комнаты для режима гироскопа: панорама расстояний вокруг игрока (клетки по 2° азимута и высоты).
    // Пока игрок осматривается, кадры экрана уходят в нейросеть глубины (RoomDepth); её относительный
    // результат переводится в метры: телефон в руке на высоте глаз (EyeHeight), значит луч в пол под
    // наклоном a упирается в него на 1.4 / sin(a) м — по таким точкам подбирается масштаб кадра,
    // а там, где пол не виден, — по местам, уже известным с прошлых кадров.
    //
    // Что даёт игре:
    //  • сущность ставится перед стеной или шкафом, а не внутри них (MaxDistance), на видимый пол (FloorReach);
    //  • высота телефона над полом (PhoneHeight): у нейросети нет метров, но есть пропорции — отношение
    //    «потолок над глазами / пол под глазами» от масштаба не зависит. Потолки в жилых комнатах почти
    //    всегда ~2.6 м, отсюда высота телефона: стоит игрок, сидит или держит телефон у пояса.
    //    Пока потолка не видели — 1.4 м. Внутри карта хранится «как при 1.4 м», наружу — умноженной на Scale;
    //  • высота потолка (CeilingHeight);
    //  • перекрытие: стол, диван, косяк двери ближе сущности — заслоняют её (шейдеры, _GW_Room).
    public class RoomMap : MonoBehaviour
    {
        public const int Cols = 180, Rows = 90;   // клетки по 2°
        public const float EyeHeight = 1.4f;       // высота телефона, пока не измерили
        const float TypicalCeiling = 2.6f;
        const float Unknown = 50f;                 // в текстуре: «ничего не известно» — ничего не заслоняет
        const float Budget = 3f;                   // мс работы нейросети за кадр

        static readonly int RoomId = Shader.PropertyToID("_GW_Room");
        static readonly int OriginId = Shader.PropertyToID("_GW_RoomOrigin");

        public bool Capturing { get; set; }
        public float Interval { get; set; } = 0.4f;
        public int Frames { get; private set; }
        public bool Has => Frames > 0;
        public float CeilingHeight { get; private set; } = -1f;   // над полом, -1 — не знаем
        public float PhoneHeight { get; private set; } = EyeHeight;  // над полом, м
        public bool HeightMeasured { get; private set; }
        public bool Available => !failed && Settings.Quality > 0;
        float Scale => PhoneHeight / EyeHeight;
        public string Status { get; private set; } = "нет";
        public string LastFrame { get; private set; } = "ещё не было";   // чем кончился последний кадр глубины

        readonly float[] inv = new float[Cols * Rows];      // средняя обратная дальность в клетке, 1/м
        readonly float[] weight = new float[Cols * Rows];
        readonly float[] frameSum = new float[Cols * Rows];
        readonly int[] frameCount = new int[Cols * Rows];
        readonly ushort[] texData = new ushort[Cols * Rows];
        Texture2D tex;
        bool dirty;

        GyroRig rig;
        RoomDepth depth;
        RenderTexture crop;
        Material cropMat;
        bool readbackPending, failed;
        float timer;
        Quaternion shotRot, lastRot;
        float tanX, tanY;
        Vector3 origin, shotOrigin;

        public static RoomMap Create(GyroRig rig)
        {
            var map = rig.gameObject.AddComponent<RoomMap>();
            map.rig = rig;
            return map;
        }

        // keepHeight — игрок просто перешёл на другое место: держит телефон так же, высоту не теряем
        public void Clear(bool keepHeight = false)
        {
            if (!keepHeight)
            {
                PhoneHeight = EyeHeight;
                HeightMeasured = false;
            }
            System.Array.Clear(inv, 0, inv.Length);
            System.Array.Clear(weight, 0, weight.Length);
            Frames = 0;
            CeilingHeight = -1f;
            dirty = true;
            Shader.SetGlobalVector(OriginId, Vector4.zero);
        }

        void Update()
        {
            if (rig == null) return;   // в редакторе карту заполняют вручную
            var cam = rig.Cam;
            float turn = Quaternion.Angle(cam.transform.rotation, lastRot) / Mathf.Max(Time.deltaTime, 1e-3f);
            lastRot = cam.transform.rotation;

            if (depth != null && depth.Busy)
            {
                if (depth.Step(Budget)) Integrate(depth.Take(), depth.Width, depth.Height, shotRot, tanX, tanY, shotOrigin);
                return;
            }
            if (dirty) Upload();
            if (!Capturing || failed || readbackPending || Settings.Quality == 0) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            // на быстром повороте кадр смазан и отстаёт от гироскопа — ждём, пока телефон замрёт
            if (turn > 25f) return;
            var webcam = rig.Webcam;
            if (webcam == null || webcam.width < 32) return;

            if (depth == null)
            {
                RoomDepth.SizeFor(cam.aspect, out int w, out int h);
                depth = new RoomDepth();
                depth.Init(w, h);
                crop = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                cropMat = new Material(Shader.Find("GhostWard/ViewCrop"));
            }
            if (!depth.Ready) { failed = true; Status = LastFrame = "нет модели"; return; }

            timer = Interval;
            rig.SetupViewCrop(cropMat);
            Graphics.Blit(webcam, crop, cropMat);
            shotRot = cam.transform.rotation;
            shotOrigin = cam.transform.position;
            tanY = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            tanX = tanY * cam.aspect;
            readbackPending = true;
            AsyncGPUReadback.Request(crop, 0, TextureFormat.RGBA32, OnFrame);
        }

        void OnFrame(AsyncGPUReadbackRequest req)
        {
            readbackPending = false;
            if (req.hasError || depth == null || !depth.Ready) return;
            depth.Begin(req.GetData<byte>());
        }

        // ---------- из относительной глубины кадра — в метры, на панораму ----------

        static int CellOf(Vector3 dir)
        {
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            int c = Mathf.FloorToInt((yaw + 180f) * 0.5f);
            c = ((c % Cols) + Cols) % Cols;
            int r = Mathf.Clamp(Mathf.FloorToInt((pitch + 90f) * 0.5f), 0, Rows - 1);
            return r * Cols + c;
        }

        // disparity — Width×Height, строки сверху вниз; rot и from — поворот и место камеры в момент снимка
        public bool Integrate(float[] disparity, int w, int h, Quaternion rot, float tx, float ty, Vector3 from)
        {
            origin = from;
            int n = w * h;
            var dirs = new Vector3[n];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var d = new Vector3(((x + 0.5f) / w * 2f - 1f) * tx, (1f - (y + 0.5f) / h * 2f) * ty, 1f);
                    dirs[y * w + x] = rot * d.normalized;
                }

            // подбор 1/дальность = s · disparity + t: пол (луч вниз) и уже известные клетки
            var q = new float[n];
            var target = new float[n];
            var wt = new float[n];
            int m = 0;
            for (int i = 0; i < n; i += 2)
            {
                var d = dirs[i];
                if (d.y < -0.17f)
                {
                    q[m] = disparity[i];
                    target[m] = -d.y / EyeHeight;
                    wt[m++] = 1f;
                }
                else
                {
                    int cell = CellOf(d);
                    if (weight[cell] < 1f) continue;
                    q[m] = disparity[i];
                    target[m] = inv[cell];
                    wt[m++] = 0.6f;
                }
            }
            if (m < 120) { Status = LastFrame = "мало пола в кадре"; return false; }

            var inlier = new bool[m];
            for (int i = 0; i < m; i++) inlier[i] = true;
            float s = 0f, t = 0f;
            int kept = m;
            for (int iter = 0; iter < 6; iter++)
            {
                double sw = 0, sq = 0, sqq = 0, st = 0, sqt = 0;
                for (int i = 0; i < m; i++)
                {
                    if (!inlier[i]) continue;
                    double k = wt[i];
                    sw += k; sq += k * q[i]; sqq += k * q[i] * q[i]; st += k * target[i]; sqt += k * q[i] * target[i];
                }
                double det = sw * sqq - sq * sq;
                if (sw <= 0) break;
                if (System.Math.Abs(det) < 1e-9 * sw * sw) { s = (float)(sqt / System.Math.Max(sqq, 1e-9)); t = 0f; }
                else { s = (float)((sw * sqt - sq * st) / det); t = (float)((st - s * sq) / sw); }
                kept = 0;
                for (int i = 0; i < m; i++)
                {
                    inlier[i] = Mathf.Abs(s * q[i] + t - target[i]) < 0.2f * target[i];
                    if (inlier[i]) kept++;
                }
            }
            if (s <= 0f || kept < 80 || kept < m / 4) { Status = $"не сошлось {kept}/{m}"; LastFrame = "масштаб не сошёлся"; return false; }

            System.Array.Clear(frameSum, 0, frameSum.Length);
            System.Array.Clear(frameCount, 0, frameCount.Length);
            for (int i = 0; i < n; i++)
            {
                float r = Mathf.Clamp(s * disparity[i] + t, 1f / 12f, 1f / 0.3f);
                int cell = CellOf(dirs[i]);
                frameSum[cell] += r;
                frameCount[cell]++;
            }
            for (int c = 0; c < frameSum.Length; c++)
            {
                if (frameCount[c] == 0) continue;
                float r = frameSum[c] / frameCount[c];
                float wOld = weight[c];
                inv[c] = (inv[c] * wOld + r) / (wOld + 1f);
                weight[c] = Mathf.Min(wOld + 1f, 4f);   // новые кадры быстро поправляют старое
            }
            Frames++;
            UpdateCeiling();
            spotsDirty = true;
            dirty = true;
            Status = $"кадров {Frames}, пол {kept}/{m}, телефон {PhoneHeight:0.00} м" + (HeightMeasured ? " (по потолку)" : " (обычная)");
            LastFrame = "ок";
            spotsDirty = true;
            Upload();
            return true;
        }

        // потолок — высота, на которой сходится больше всего точек выше 15°: у потолка она одна и та же,
        // у стен — разная на каждой высоте взгляда
        void UpdateCeiling()
        {
            var bins = new int[35];   // 1.6 … 5.0 м по 10 см — в единицах карты «при 1.4 м»
            for (int r = Rows / 2 + 7; r < Rows; r++)
            {
                float sin = Mathf.Sin(((r + 0.5f) * 2f - 90f) * Mathf.Deg2Rad);
                for (int c = 0; c < Cols; c++)
                {
                    int i = r * Cols + c;
                    if (weight[i] <= 0f) continue;
                    int b = Mathf.RoundToInt((EyeHeight + sin / inv[i] - 1.6f) * 10f);
                    if (b >= 0 && b < bins.Length) bins[b]++;
                }
            }
            int best = 0;
            for (int b = 1; b < bins.Length; b++) if (bins[b] > bins[best]) best = b;
            int sum = bins[best] + (best > 0 ? bins[best - 1] : 0) + (best < bins.Length - 1 ? bins[best + 1] : 0);
            if (sum < 40) return;
            float ceiling = 1.6f + best * 0.1f;
            // высота телефона — один раз за ночь, по двум кадрам и больше: иначе фигуры «прыгали» бы по высоте
            if (!HeightMeasured && Frames >= 2)
            {
                PhoneHeight = Mathf.Clamp(TypicalCeiling * EyeHeight / ceiling, 0.7f, 1.8f);
                HeightMeasured = true;
            }
            CeilingHeight = Mathf.Clamp(ceiling * Scale, 2.1f, 4f);
        }

        void Upload()
        {
            dirty = false;
            if (tex == null)
            {
                tex = new Texture2D(Cols, Rows, TextureFormat.RHalf, false, true)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapModeU = TextureWrapMode.Repeat,
                    wrapModeV = TextureWrapMode.Clamp
                };
            }
            for (int i = 0; i < texData.Length; i++)
                texData[i] = Mathf.FloatToHalf(weight[i] > 0f ? Scale / inv[i] : Unknown);
            tex.SetPixelData(texData, 0);
            tex.Apply(false);
            Shader.SetGlobalTexture(RoomId, tex);
            Shader.SetGlobalVector(OriginId, new Vector4(origin.x, origin.y, origin.z, Has ? 1f : 0f));
        }

        // ---------- вопросы игры ----------

        // дальность в клетке, м; -1 — не видели
        public float Range(float yawDeg, float pitchDeg)
        {
            var d = Quaternion.Euler(-pitchDeg, yawDeg, 0f) * Vector3.forward;
            int i = CellOf(d);
            return weight[i] > 0f ? Scale / inv[i] : -1f;
        }

        // ближайшее препятствие по горизонтали в секторе (азимут ± halfWidth): только точки на высоте туловища
        // (1–2.1 м над полом) — стена, шкаф, дверь. Пол, потолок и низкая мебель не мешают: за столом или
        // диваном фигура может стоять — они её просто заслонят снизу.
        public float Free(float yawDeg, float halfWidth)
        {
            float best = float.MaxValue;
            bool any = false;
            for (float dy = -halfWidth; dy <= halfWidth; dy += 2f)
                for (float p = -40f; p <= 40f; p += 2f)
                {
                    float r = Range(yawDeg + dy, p);
                    if (r < 0f) continue;
                    any = true;
                    float above = PhoneHeight + Mathf.Sin(p * Mathf.Deg2Rad) * r;
                    if (above < 1f || above > 2.1f) continue;
                    best = Mathf.Min(best, r * Mathf.Cos(p * Mathf.Deg2Rad));
                }
            return !any ? -1f : best == float.MaxValue ? 12f : best;
        }

        // насколько далеко по этому азимуту можно поставить фигуру, чтобы она была перед стеной/шкафом, а не в них;
        // -1 — направление не осмотрено
        public float MaxDistance(float yawDeg)
        {
            if (!Has) return -1f;
            float free = Free(yawDeg, 6f);
            if (free < 0f) return -1f;
            return (free - 0.25f) / 1.12f - 0.1f;   // запас на ошибку глубины, как в шейдере перекрытия
        }

        // самая дальняя видимая точка пола по азимуту (фигура там «стоит» на настоящем полу); -1 — пола не видно
        public float FloorReach(float yawDeg)
        {
            if (!Has) return -1f;
            float best = -1f;
            for (float dy = -4f; dy <= 4f; dy += 2f)
                for (float p = -70f; p <= -6f; p += 2f)
                {
                    float r = Range(yawDeg + dy, p);
                    if (r < 0f) continue;
                    float above = PhoneHeight + Mathf.Sin(p * Mathf.Deg2Rad) * r;
                    if (Mathf.Abs(above) < 0.18f * Scale) best = Mathf.Max(best, r * Mathf.Cos(p * Mathf.Deg2Rad));
                }
            return best;
        }

        // ---------- места, а не случайные точки ----------
        // По каждому азимуту (шаг 2°): где стена или шкаф на высоте туловища, где низкая мебель, докуда виден пол.
        // Из этого — места, где сущность выглядит частью комнаты:
        //  • проём — в середине далеко, по бокам близко (косяки): стоит в двери или виднеется за ней;
        //  • угол — дальше соседних стен: стоит в углу;
        //  • за мебелью — стол, диван, кровать ближе стены: стоит за ними, низ закрыт;
        //  • открытое место — просто перед стеной, на видимом полу.
        public enum SpotKind { Open, Doorway, Corner, Furniture }

        public struct Spot
        {
            public float Yaw, Dist;
            public SpotKind Kind;
        }

        public string LastSpot { get; private set; } = "";
        public Vector3 Origin => origin;   // откуда снята карта (игрок мог отойти — тогда она снимается заново)

        readonly float[] wallCol = new float[Cols], lowCol = new float[Cols], floorCol = new float[Cols];
        bool spotsDirty = true;
        const float MaxPlace = 6.5f;

        static float ColYaw(int c) => (c + 0.5f) * 2f - 180f;

        void AnalyzeColumns()
        {
            spotsDirty = false;
            for (int c = 0; c < Cols; c++)
            {
                float yaw = ColYaw(c);
                float wall = float.MaxValue, low = float.MaxValue, floor = -1f;
                bool any = false;
                for (float p = -60f; p <= 40f; p += 2f)
                {
                    float r = Range(yaw, p);
                    if (r < 0f) continue;
                    any = true;
                    float sin = Mathf.Sin(p * Mathf.Deg2Rad), horiz = r * Mathf.Cos(p * Mathf.Deg2Rad);
                    float above = PhoneHeight + sin * r;
                    if (above >= 1f && above <= 2.1f) wall = Mathf.Min(wall, horiz);
                    else if (above >= 0.25f && above < 1f) low = Mathf.Min(low, horiz);
                    else if (Mathf.Abs(above) < 0.18f * Scale) floor = Mathf.Max(floor, horiz);
                }
                wallCol[c] = !any ? -1f : wall == float.MaxValue ? 12f : wall;
                lowCol[c] = low == float.MaxValue ? -1f : low;
                floorCol[c] = floor;
            }
        }

        float Wall(int c) => wallCol[((c % Cols) + Cols) % Cols];

        // мин. стена в диапазоне колонок; -1 — там не смотрели
        float MinWall(int from, int to)
        {
            float m = float.MaxValue;
            for (int c = from; c <= to; c++)
            {
                float w = Wall(c);
                if (w < 0f) return -1f;
                m = Mathf.Min(m, w);
            }
            return m;
        }

        // Место для сущности по её повадке. darkness(yaw) — темнота сектора 0..1 (-1 — не знаем).
        // maxAngle — не дальше этого угла от взгляда (место прямо в кадре)
        public bool TryPickSpot(float viewYaw, float minAngle, float maxDist, Lair lair, System.Func<float, float> darkness, out Spot spot,
                                float maxAngle = 180f)
        {
            spot = default;
            if (!Has) return false;
            if (spotsDirty) AnalyzeColumns();

            var spots = new System.Collections.Generic.List<Spot>();
            var weights = new System.Collections.Generic.List<float>();
            float total = 0f;
            void Add(int c, float dist, SpotKind kind, float w)
            {
                if (maxDist > 0f) dist = Mathf.Min(dist, maxDist);
                // дальше 6.5 м фигура мелкая, а глубина по одному кадру ненадёжна (окна, зеркала видятся «далью»)
                dist = Mathf.Min(dist, MaxPlace);
                float limit = Wall(c) - 0.35f;
                dist = Mathf.Min(dist, limit);
                if (dist < 1.1f || w <= 0f) return;
                float d = darkness(ColYaw(c));
                if (d < 0f) d = 0.5f;
                if (lair == Lair.Bright) d = 1f - d;
                w *= 0.15f + d * d;
                spots.Add(new Spot { Yaw = ColYaw(c) + Random.Range(-0.8f, 0.8f), Dist = dist, Kind = kind });
                weights.Add(w);
                total += w;
            }

            for (int c = 0; c < Cols; c++)
            {
                float wall = wallCol[c];
                if (wall < 1.5f) continue;   // не осмотрено или стена вплотную
                float off = Mathf.Abs(Mathf.DeltaAngle(ColYaw(c), viewYaw));
                if (off < minAngle || off > maxAngle) continue;

                float left = MinWall(c - 5, c - 2), right = MinWall(c + 2, c + 5);
                float jamb = Mathf.Max(left, right);
                bool doorway = left > 0f && right > 0f && wall > jamb + 1f && wall > 2.2f && jamb < MaxPlace - 0.5f;
                bool corner = !doorway && left > 0f && right > 0f && wall < MaxPlace + 0.5f
                              && wall >= MinWall(c - 4, c + 4) && wall >= Wall(c - 4) && wall >= Wall(c + 4)
                              && wall - (Wall(c - 9) + Wall(c + 9)) * 0.5f > 0.15f;
                float low = lowCol[c];
                bool furniture = low > 0.6f && low < wall - 0.9f;

                // вес места для каждой повадки
                switch (lair)
                {
                    case Lair.Far:
                        if (doorway) Add(c, Mathf.Min(wall - 0.5f, jamb + Random.Range(0.8f, 2.2f)), SpotKind.Doorway, 5f);
                        if (corner && wall > 3f) Add(c, wall - 0.45f, SpotKind.Corner, 1.5f);
                        if (wall > 3.5f) Add(c, Random.Range(3.2f, Mathf.Min(wall - 0.4f, MaxPlace)), SpotKind.Open, 0.4f);
                        break;
                    case Lair.Near:
                        if (furniture && low < 2.4f) Add(c, low + 0.55f, SpotKind.Furniture, 4f);
                        Add(c, Random.Range(1.4f, 2.2f), SpotKind.Open, 0.6f);
                        break;
                    case Lair.Floor:
                        // ползающим — на видимый пол, чтобы было видно, как они там
                        if (floorCol[c] > 1.4f) Add(c, Random.Range(1.6f, floorCol[c]), SpotKind.Open, 3f);
                        if (furniture) Add(c, low + 0.5f, SpotKind.Furniture, 1f);
                        break;
                    case Lair.DarkCorner:
                        if (corner) Add(c, wall - 0.45f, SpotKind.Corner, 4f);
                        if (furniture) Add(c, low + 0.55f, SpotKind.Furniture, 2.5f);
                        if (doorway) Add(c, jamb + 0.35f, SpotKind.Doorway, 2f);
                        Add(c, Random.Range(2.2f, Mathf.Min(wall - 0.4f, MaxPlace)), SpotKind.Open, 0.3f);
                        break;
                    case Lair.Ceiling:
                        if (corner) Add(c, wall - 0.5f, SpotKind.Corner, 3f);
                        Add(c, Random.Range(2.2f, Mathf.Min(wall - 0.4f, MaxPlace)), SpotKind.Open, 1f);
                        break;
                    default:   // Behind, Bright — любое разумное место
                        if (doorway) Add(c, jamb + 0.35f, SpotKind.Doorway, 2f);
                        if (corner) Add(c, wall - 0.45f, SpotKind.Corner, 2f);
                        if (furniture) Add(c, low + 0.55f, SpotKind.Furniture, 2f);
                        Add(c, Random.Range(2f, Mathf.Min(wall - 0.4f, MaxPlace)), SpotKind.Open, 1f);
                        break;
                }
            }
            if (total <= 0f) return false;

            float r = Random.value * total;
            for (int i = 0; i < spots.Count; i++)
            {
                r -= weights[i];
                if (r > 0f) continue;
                spot = spots[i];
                LastSpot = spot.Kind switch
                {
                    SpotKind.Doorway => "проём", SpotKind.Corner => "угол", SpotKind.Furniture => "за мебелью", _ => "открыто"
                } + $" {spot.Dist:0.0} м";
                return true;
            }
            return false;
        }

        // для редактора: картинка панорамы (светлое — близко)
        public Texture2D Preview()
        {
            var t = new Texture2D(Cols, Rows, TextureFormat.RGB24, false);
            var px = new Color[Cols * Rows];
            for (int i = 0; i < px.Length; i++)
                px[i] = weight[i] > 0f ? Color.Lerp(Color.black, Color.white, Mathf.InverseLerp(1f / 8f, 1f / 0.8f, inv[i])) : new Color(0.3f, 0f, 0f);
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        void OnDestroy()
        {
            depth?.Dispose();
            if (crop != null) crop.Release();
            Shader.SetGlobalVector(OriginId, Vector4.zero);
        }
    }
}
