using System.Diagnostics;
using System.IO;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GhostWard.EditorTools
{
    // Модель глубины Depth Anything V2 Small (Apache 2.0, onnx-community/depth-anything-v2-small).
    // Исходный ONNX (99 МБ) кладётся во временную папку Assets/GhostWard/DepthSrc (в git не попадает);
    // Convert ужимает веса до 8 бит и сохраняет Assets/GhostWard/Resources/Depth.sentis (~25 МБ).
    // Test — по картинкам из папки (-testImg <папка>): кадр как с телефона в портрете, камера смотрит
    // прямо (наклон 0, вертикальный угол 60°) → Logs/depth_*.png (глубина) и Logs/room_*.png (панорама RoomMap),
    // в лог — время расчёта, найденная высота потолка и сколько места до стены прямо по курсу.
    public static class DepthTools
    {
        const string Src = "Assets/GhostWard/DepthSrc/model.onnx";
        const string Dst = "Assets/GhostWard/Resources/Depth.sentis";

        public static void Convert()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ModelAsset>(Src);
            if (asset == null) { Debug.LogError("[GhostWard] нет " + Src); return; }
            var model = ModelLoader.Load(asset);
            ModelQuantizer.QuantizeWeights(QuantizationType.Uint8, ref model);
            ModelWriter.Save(Dst, model);
            AssetDatabase.Refresh();
            Debug.Log($"[GhostWard] модель глубины: {new FileInfo(Dst).Length / 1048576f:0.0} МБ");
        }

        // Кадр «как на телефоне»: фото комнаты фоном, по карте глубины — три фигуры: за столом (4 м),
        // перед ним (2 м) и у дальней стены. Видно, заслоняет ли настоящая мебель нарисованную сущность.
        static void RenderOcclusion(Texture2D photo, string name, float aspect, float floorY)
        {
            const int w = 460, h = 1000;
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 60f;
            cam.transform.position = new Vector3(0f, 1.4f, 0f);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.aspect = aspect;

            // фон: середина фото в пропорциях экрана, ровно во весь кадр на 20 м
            float srcAspect = (float)photo.width / photo.height;
            var scale = srcAspect > aspect ? new Vector2(aspect / srcAspect, 1f) : new Vector2(1f, srcAspect / aspect);
            var bg = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(photo, bg, scale, (Vector2.one - scale) * 0.5f);
            var feed = GameObject.CreatePrimitive(PrimitiveType.Quad);
            float fh = 2f * 20f * Mathf.Tan(30f * Mathf.Deg2Rad);
            feed.transform.position = new Vector3(0f, 1.4f, 20f);
            feed.transform.localScale = new Vector3(fh * aspect, fh, 1f);
            var fm = new Material(Shader.Find("GhostWard/Upright"));
            fm.mainTexture = bg;
            feed.GetComponent<MeshRenderer>().sharedMaterial = fm;

            var made = new System.Collections.Generic.List<GameObject> { cam.gameObject, feed };
            // разный рост: тень 1.8 м, двойник 1.75, упырь 1.7 — ступнями на полу
            foreach (var (yaw, dist, kind) in new[] { (0f, 4f, EntityKind.Shade), (-9f, 2f, EntityKind.Double), (8f, 6.5f, EntityKind.Upyr) })
            {
                var def = Catalog.Get(kind);
                var fig = Entity.FigureOf(def);
                var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.transform.position = dir * dist + Vector3.up * (floorY + fig.Feet);
                q.transform.rotation = Quaternion.LookRotation(dir);
                q.transform.localScale = new Vector3(fig.Size.x, fig.Size.y, 1f);
                var em = Entity.CreateMaterial(def, 2.2f + yaw);
                em.SetFloat("_World", 1f);
                em.SetFloat("_Dim", 0.9f);
                q.GetComponent<MeshRenderer>().sharedMaterial = em;
                made.Add(q);
            }
            Shader.SetGlobalFloat("_GW_Light", 0.6f);
            Shader.SetGlobalFloat("_GW_Mode", 0f);
            Shader.SetGlobalFloat("_GW_Style", 0f);
            var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, request)) UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, request);
            else cam.Render();
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes($"Logs/occl_{name}.png", tex.EncodeToPNG());
            foreach (var g in made) Object.DestroyImmediate(g);
        }

        // Глубина каждой картинки сущности (Resources/Entities/<Kind>.png) → Resources/EntityDepth/<Kind>.png:
        // белое — ближе к игроку. Нейросеть видит фигуру на сером фоне; глубина нормируется по самой фигуре
        // (5–95 %), фон заполняется глубиной ближайшего края (иначе края сетки тянулись бы назад), сглаживается.
        public static void EntityDepth()
        {
            const string inDir = "Assets/Resources/Entities", outDir = "Assets/Resources/EntityDepth";
            Directory.CreateDirectory(outDir);
            foreach (var file in Directory.GetFiles(inDir, "*.png"))
            {
                var src = new Texture2D(2, 2);
                if (!src.LoadImage(File.ReadAllBytes(file))) continue;
                // не каждое сочетание сторон модель принимает (сдвиг при пересчёте сетки патчей) — пробуем соседние
                float aspect = (float)src.width / src.height;
                foreach (int rows in new[] { 16, 17, 15, 18, 14, 20, 21 })
                {
                    int w = Mathf.RoundToInt(rows * aspect) * 14, h = rows * 14;
                    if (TryEntityDepth(src, file, outDir, w, h)) break;
                }
            }
            AssetDatabase.Refresh();
        }

        static bool TryEntityDepth(Texture2D src, string file, string outDir, int w, int h)
        {
            {
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(src, rt);
                var read = new Texture2D(w, h, TextureFormat.RGBA32, false);
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                read.Apply();
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);

                // фигура на ровном сером фоне — так сеть не придумывает глубину пустоте
                var px = read.GetPixels32();   // снизу вверх
                var mask = new bool[w * h];
                for (int i = 0; i < px.Length; i++)
                {
                    float a = px[i].a / 255f;
                    mask[i] = a > 0.5f;
                    px[i] = Color32.Lerp(new Color32(110, 110, 110, 255), new Color32(px[i].r, px[i].g, px[i].b, 255), a);
                }
                read.SetPixels32(px);
                read.Apply();

                var depth = new RoomDepth();
                depth.Init(w, h);
                float[] disp;
                try { disp = depth.RunBlocking(read.GetRawTextureData<byte>()); }   // сверху вниз
                catch (System.Exception) { depth.Dispose(); return false; }
                depth.Dispose();

                // в порядок «снизу вверх», как у текстуры
                var d = new float[w * h];
                var vals = new System.Collections.Generic.List<float>();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int b = (h - 1 - y) * w + x;
                        d[b] = disp[y * w + x];
                        if (mask[b]) vals.Add(d[b]);
                    }
                if (vals.Count < 50) { Debug.LogWarning("[GhostWard] рельеф: нет фигуры в " + file); return true; }
                vals.Sort();
                float lo = vals[vals.Count / 20], hi = vals[vals.Count * 19 / 20];
                for (int i = 0; i < d.Length; i++)
                    d[i] = mask[i] ? Mathf.Clamp01((d[i] - lo) / Mathf.Max(hi - lo, 1e-4f)) : -1f;

                // фон — глубиной ближайшего края фигуры
                for (int pass = 0; pass < w + h; pass++)
                {
                    bool any = false;
                    var next = (float[])d.Clone();
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            int i = y * w + x;
                            if (d[i] >= 0f) continue;
                            float sum = 0f;
                            int n = 0;
                            if (x > 0 && d[i - 1] >= 0f) { sum += d[i - 1]; n++; }
                            if (x < w - 1 && d[i + 1] >= 0f) { sum += d[i + 1]; n++; }
                            if (y > 0 && d[i - w] >= 0f) { sum += d[i - w]; n++; }
                            if (y < h - 1 && d[i + w] >= 0f) { sum += d[i + w]; n++; }
                            if (n > 0) next[i] = sum / n;
                            else any = true;
                        }
                    d = next;
                    if (!any) break;
                }
                for (int i = 0; i < d.Length; i++) if (d[i] < 0f) d[i] = 0f;

                // сгладить: рельеф без ступенек
                for (int pass = 0; pass < 3; pass++)
                {
                    var next = new float[d.Length];
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            float sum = 0f;
                            int n = 0;
                            for (int dy = -1; dy <= 1; dy++)
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    int xx = x + dx, yy = y + dy;
                                    if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                                    sum += d[yy * w + xx];
                                    n++;
                                }
                            next[y * w + x] = sum / n;
                        }
                    d = next;
                }

                var outTex = new Texture2D(w, h, TextureFormat.RGB24, false);
                var cols = new Color[w * h];
                for (int i = 0; i < d.Length; i++) cols[i] = new Color(d[i], d[i], d[i]);
                outTex.SetPixels(cols);
                outTex.Apply();
                string name = Path.GetFileNameWithoutExtension(file);
                File.WriteAllBytes($"{outDir}/{name}.png", outTex.EncodeToPNG());
                Debug.Log($"[GhostWard] рельеф {name}: {w}×{h}");
                return true;
            }
        }

        // Рельеф глазами игрока: фигура в 1.8 м, камера на высоте 0.5 м (лёжа), 1.4 м (стоя) и 2.2 м (сверху);
        // слева — плоская картинка, справа — рельеф → Logs/relief_<Kind>.png
        public static void ReliefTest()
        {
            var kinds = new[] { EntityKind.Shade, EntityKind.Double, EntityKind.Upyr, EntityKind.Ghost };
            const int w = 300, h = 520;
            foreach (var kind in kinds)
            {
                var def = Catalog.Get(kind);
                if (Entity.Picture(kind) == null || Entity.Relief(kind) == null) continue;
                var sheet = new Texture2D(w * 2, h * 3, TextureFormat.RGB24, false);
                float[] heights = { 0.5f, 1.4f, 2.2f };
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 2; col++)
                    {
                        var cam = new GameObject("Cam").AddComponent<Camera>();
                        cam.clearFlags = CameraClearFlags.SolidColor;
                        cam.backgroundColor = new Color(0.16f, 0.15f, 0.14f);
                        cam.fieldOfView = 70f;
                        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                        cam.targetTexture = rt;
                        cam.aspect = (float)w / h;
                        var fig = Entity.FigureOf(def);
                        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        q.transform.localScale = new Vector3(fig.Size.x, fig.Size.y, 1f);
                        q.transform.position = new Vector3(0f, fig.Feet, 1.8f);
                        if (col == 1) Entity.UseRelief(q, def);
                        var em = Entity.CreateMaterial(def, 2.2f);
                        em.SetFloat("_World", 1f);
                        em.SetFloat("_Dim", 1f);
                        if (col == 0) em.SetFloat("_Relief", 0f);
                        q.GetComponent<MeshRenderer>().sharedMaterial = em;
                        // смотрит на фигуру с высоты heights[row], фигура стоит вертикально
                        cam.transform.position = new Vector3(0f, heights[row], 0f);
                        cam.transform.LookAt(new Vector3(0f, fig.Size.y * 0.5f, 1.8f));
                        Shader.SetGlobalFloat("_GW_Light", 0.8f);
                        Shader.SetGlobalFloat("_GW_Mode", 0f);
                        Shader.SetGlobalFloat("_GW_Style", 0f);
                        var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
                        if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, request)) UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, request);
                        else cam.Render();
                        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                        RenderTexture.active = rt;
                        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                        tex.Apply();
                        RenderTexture.active = null;
                        sheet.SetPixels(col * w, (2 - row) * h, w, h, tex.GetPixels());
                        Object.DestroyImmediate(cam.gameObject);
                        Object.DestroyImmediate(q);
                    }
                sheet.Apply();
                File.WriteAllBytes($"Logs/relief_{kind}.png", sheet.EncodeToPNG());
            }
        }

        public static void Test()
        {
            var args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, "-testImg");
            if (at < 0 || at + 1 >= args.Length) { Debug.LogError("[GhostWard] нужен -testImg <папка>"); return; }

            const float aspect = 0.46f;   // портретный экран телефона
            RoomDepth.SizeFor(aspect, out int w, out int h);
            var depth = new RoomDepth();
            depth.Init(w, h);
            foreach (var file in Directory.GetFiles(args[at + 1]))
            {
                var src = new Texture2D(2, 2);
                if (!src.LoadImage(File.ReadAllBytes(file))) continue;
                // середина картинки с пропорциями экрана
                float srcAspect = (float)src.width / src.height;
                var scale = srcAspect > aspect ? new Vector2(aspect / srcAspect, 1f) : new Vector2(1f, srcAspect / aspect);
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(src, rt, scale, (Vector2.one - scale) * 0.5f);
                var read = new Texture2D(w, h, TextureFormat.RGBA32, false);
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                read.Apply();
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);

                var watch = Stopwatch.StartNew();
                var disparity = depth.RunBlocking(read.GetRawTextureData<byte>());
                watch.Stop();
                string name = Path.GetFileNameWithoutExtension(file);

                float min = float.MaxValue, max = float.MinValue;
                foreach (var d in disparity) { min = Mathf.Min(min, d); max = Mathf.Max(max, d); }
                var outTex = new Texture2D(w, h, TextureFormat.RGB24, false);
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float v = Mathf.InverseLerp(min, max, disparity[y * w + x]);
                        px[(h - 1 - y) * w + x] = new Color(v, v, v);   // тензор сверху вниз
                    }
                outTex.SetPixels(px);
                outTex.Apply();
                File.WriteAllBytes($"Logs/depth_{name}.png", outTex.EncodeToPNG());

                var go = new GameObject("RoomMapTest");
                var room = go.AddComponent<RoomMap>();
                float ty = Mathf.Tan(30f * Mathf.Deg2Rad);
                bool ok = room.Integrate(disparity, w, h, Quaternion.identity, ty * aspect, ty, new Vector3(0f, 1.4f, 0f));
                if (ok) room.Integrate(disparity, w, h, Quaternion.identity, ty * aspect, ty, new Vector3(0f, 1.4f, 0f));   // второй кадр — высоту телефона берём с двух
                File.WriteAllBytes($"Logs/room_{name}.png", room.Preview().EncodeToPNG());
                Debug.Log($"[GhostWard] глубина {name} {w}×{h}: {watch.ElapsedMilliseconds} мс; карта: {(ok ? "да" : "нет")} ({room.Status}); " +
                          $"потолок {room.CeilingHeight:0.00} м, телефон {room.PhoneHeight:0.00} м{(room.HeightMeasured ? " (по потолку)" : "")}; впереди свободно {room.MaxDistance(0f):0.00} м " +
                          $"(препятствие {room.Free(0f, 6f):0.00} м); пол виден до {room.FloorReach(0f):0.00} м; " +
                          $"на уровне глаз {room.Range(0f, 0f):0.00} м, вниз 30° {room.Range(0f, -30f):0.00} м (пол был бы {1.4f / Mathf.Sin(30f * Mathf.Deg2Rad):0.00})");
                if (ok) RenderOcclusion(src, name, aspect, 1.4f - room.PhoneHeight);
                if (ok)
                    foreach (var lair in new[] { Lair.Far, Lair.Near, Lair.DarkCorner, Lair.Floor })
                    {
                        var seen = new System.Collections.Generic.Dictionary<string, int>();
                        for (int k = 0; k < 40; k++)
                            if (room.TryPickSpot(0f, 0f, -1f, lair, _ => 0.5f, out var spot))
                            {
                                string key = $"{spot.Kind} {spot.Yaw:0}° {spot.Dist:0.0}м";
                                seen[key] = seen.TryGetValue(key, out int n) ? n + 1 : 1;
                            }
                        var list = new System.Collections.Generic.List<string>();
                        foreach (var kv in seen) list.Add($"{kv.Key}×{kv.Value}");
                        list.Sort();
                        Debug.Log($"[GhostWard] места {name} {lair}: {string.Join(", ", list.GetRange(0, System.Math.Min(10, list.Count)))}");
                    }
                Object.DestroyImmediate(go);
            }
            depth.Dispose();
        }
    }
}
