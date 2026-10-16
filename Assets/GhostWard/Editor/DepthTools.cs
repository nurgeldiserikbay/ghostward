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
        static void RenderOcclusion(Texture2D photo, string name, float aspect)
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

            var def = Catalog.Get(EntityKind.Ghost);
            var made = new System.Collections.Generic.List<GameObject> { cam.gameObject, feed };
            foreach (var (yaw, dist) in new[] { (0f, 4f), (-9f, 2f), (8f, 6.5f) })
            {
                var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.transform.position = dir * dist + Vector3.up * def.Height * 0.5f;
                q.transform.rotation = Quaternion.LookRotation(dir);
                q.transform.localScale = new Vector3(def.Height * 0.8f, def.Height, 1f);
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
                File.WriteAllBytes($"Logs/room_{name}.png", room.Preview().EncodeToPNG());
                Debug.Log($"[GhostWard] глубина {name} {w}×{h}: {watch.ElapsedMilliseconds} мс; карта: {(ok ? "да" : "нет")} ({room.Status}); " +
                          $"потолок {room.CeilingHeight:0.00} м; впереди свободно {room.MaxDistance(0f):0.00} м " +
                          $"(препятствие {room.Free(0f, 6f):0.00} м); пол виден до {room.FloorReach(0f):0.00} м; " +
                          $"на уровне глаз {room.Range(0f, 0f):0.00} м, вниз 30° {room.Range(0f, -30f):0.00} м (пол был бы {1.4f / Mathf.Sin(30f * Mathf.Deg2Rad):0.00})");
                if (ok) RenderOcclusion(src, name, aspect);
                Object.DestroyImmediate(go);
            }
            depth.Dispose();
        }
    }
}
