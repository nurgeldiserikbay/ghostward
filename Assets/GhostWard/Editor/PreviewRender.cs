using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GhostWard.EditorTools
{
    // Рисует все сущности в одну картинку — посмотреть силуэты без телефона.
    // Unity.exe -batchmode -quit -projectPath . -executeMethod GhostWard.EditorTools.PreviewRender.Entities
    public static class PreviewRender
    {
        [MenuItem("GhostWard/Превью сущностей")]
        public static void Entities()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            const int cols = 4, cell = 400;
            int rows = (Catalog.Entities.Length + cols - 1) / cols;

            var camGo = new GameObject("Preview Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = rows * 0.5f * 1.25f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.15f, 0.17f);
            cam.transform.position = new Vector3(0f, 0f, -10f);

            for (int i = 0; i < Catalog.Entities.Length; i++)
            {
                var def = Catalog.Entities[i];
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                float x = (i % cols - (cols - 1) * 0.5f) * 1.0f;
                float y = -((i / cols) - (rows - 1) * 0.5f) * 1.25f;
                q.transform.position = new Vector3(x, y, 0f);
                q.transform.localScale = new Vector3(0.8f, 1f, 1f);
                q.GetComponent<MeshRenderer>().sharedMaterial = Entity.CreateMaterial(def, 2.1f + i);
            }

            var rt = new RenderTexture(cols * cell, rows * (int)(cell * 1.25f), 24, RenderTextureFormat.ARGB32);
            cam.aspect = (float)rt.width / rt.height;
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }

            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/entities.png", tex.EncodeToPNG());
            Debug.Log("[GhostWard] Превью: Logs/entities.png");
        }

        // Экраны дневника: список, статья, предметы — в Logs/journal_*.png
        public static void Journal()
        {
            Know.EnsureStarters();
            Catalog.MarkSeen(EntityKind.Ghost);
            Catalog.MarkSeen(EntityKind.Shade);
            Catalog.MarkSeen(EntityKind.Albasty);
            Catalog.MarkSeen(EntityKind.Mara);

            Shoot("list", ui => ui.DebugJournal("list"));
            Shoot("entry", ui => ui.DebugJournal("entry", EntityKind.Ghost));
            Shoot("entry_locked", ui => ui.DebugJournal("entry", EntityKind.Kikimora));
            Shoot("clues", ui => ui.DebugJournal("clues"));
            Shoot("howto", ui => ui.DebugJournal("howto"));
        }

        // Тепловизор на тестовом кадре (Logs/entities.png) и холодное пятно — Logs/thermal.png
        public static void Thermal()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.transform.position = new Vector3(0, 0, -5);
            var rt = new RenderTexture(800, 500, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.aspect = 1.6f;

            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes("Logs/entities.png"));
            var feed = GameObject.CreatePrimitive(PrimitiveType.Quad);
            feed.transform.localScale = new Vector3(1.6f, 1f, 1f);
            var m = new Material(Shader.Find("GhostWard/Feed"));
            m.mainTexture = src;
            m.SetFloat("_Style", 5f);
            feed.GetComponent<MeshRenderer>().sharedMaterial = m;

            var spot = GameObject.CreatePrimitive(PrimitiveType.Quad);
            spot.transform.position = new Vector3(0.45f, -0.1f, -1f);
            spot.transform.localScale = Vector3.one * 0.35f;
            var sm = new Material(Shader.Find("GhostWard/ColdSpot"));
            sm.SetFloat("_Alpha", 0.9f);
            spot.GetComponent<MeshRenderer>().sharedMaterial = sm;

            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes("Logs/thermal.png", tex.EncodeToPNG());
        }

        // Проверка нейросети тепловизора на картинках из папки (-testImg <папка>) — Logs/seg_*.png
        public static void ThermalSegTest()
        {
            var args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, "-testImg");
            if (at < 0 || at + 1 >= args.Length) { Debug.LogError("[GhostWard] нужен -testImg <папка>"); return; }

            foreach (var file in Directory.GetFiles(args[at + 1]))
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var src = new Texture2D(2, 2);
                src.LoadImage(File.ReadAllBytes(file));

                var seg = new GameObject("Seg").AddComponent<ThermalSeg>();
                seg.Init();
                seg.SetActive(true);
                { var t0 = System.DateTime.Now; while (!seg.HasResult && (System.DateTime.Now - t0).TotalSeconds < 60) seg.Tick(src, 0, false); }
                Debug.Log($"[GhostWard] {Path.GetFileName(file)}: человек={seg.PersonInView} животное={seg.AnimalInView}");

                foreach (bool cold in new[] { false, true })
                {
                    var cam = new GameObject("Cam").AddComponent<Camera>();
                    cam.orthographic = true;
                    cam.orthographicSize = 0.5f;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.black;
                    cam.transform.position = new Vector3(0, 0, -5);
                    float aspect = (float)src.width / src.height;
                    var rt = new RenderTexture(Mathf.RoundToInt(500 * aspect), 500, 24, RenderTextureFormat.ARGB32);
                    cam.targetTexture = rt;
                    cam.aspect = aspect;

                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.transform.localScale = new Vector3(aspect, 1f, 1f);
                    var m = new Material(Shader.Find("GhostWard/Feed"));
                    m.mainTexture = src;
                    m.SetFloat("_Style", 5f);
                    m.SetFloat("_SegOn", 1f);
                    m.SetTexture("_SegTex", seg.Mask);
                    m.SetFloat("_ColdPerson", cold ? 1f : 0f);
                    quad.GetComponent<MeshRenderer>().sharedMaterial = m;

                    var request = new RenderPipeline.StandardRequest { destination = rt };
                    if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
                    else cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes($"Logs/seg_{Path.GetFileNameWithoutExtension(file)}{(cold ? "_cold" : "")}.png", tex.EncodeToPNG());
                    Object.DestroyImmediate(cam.gameObject);
                    Object.DestroyImmediate(quad);
                }
            }
        }

        // Музыка и акценты в WAV — Logs/audio/*.wav, с проверкой на тишину, перегруз и NaN
        public static void Audio()
        {
            Directory.CreateDirectory("Logs/audio");
            var host = new GameObject("Music");
            Music.Create(host);
            var clips = new System.Collections.Generic.List<AudioClip>();
            foreach (var s in host.GetComponents<AudioSource>()) if (s.clip != null) clips.Add(s.clip);
            var m = host.GetComponent<Music>();
            foreach (Music.Stinger st in System.Enum.GetValues(typeof(Music.Stinger)))
            {
                m.Play(st);
                var src = host.GetComponents<AudioSource>()[^1];
            }
            // акценты кешируются внутри Music — достаём через отражение
            var cache = typeof(Music).GetField("StingerClips", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .GetValue(null) as System.Collections.IDictionary;
            foreach (System.Collections.DictionaryEntry e in cache) clips.Add((AudioClip)e.Value);

            foreach (var clip in clips)
            {
                var data = new float[clip.samples * clip.channels];
                clip.GetData(data, 0);
                float peak = 0f, sum = 0f;
                int bad = 0;
                foreach (var v in data)
                {
                    if (float.IsNaN(v) || float.IsInfinity(v)) { bad++; continue; }
                    peak = Mathf.Max(peak, Mathf.Abs(v));
                    sum += v * v;
                }
                float rms = Mathf.Sqrt(sum / Mathf.Max(1, data.Length));
                Debug.Log($"[GhostWard] {clip.name}: {clip.length:0.0} с, пик {peak:0.00}, RMS {rms:0.000}, NaN {bad}");
                WriteWav($"Logs/audio/{clip.name}.wav", data, clip.frequency);
            }
        }

        static void WriteWav(string path, float[] data, int rate)
        {
            using var fs = new FileStream(path, FileMode.Create);
            using var bw = new BinaryWriter(fs);
            int bytes = data.Length * 2;
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + bytes);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            bw.Write(16); bw.Write((short)1); bw.Write((short)1);
            bw.Write(rate); bw.Write(rate * 2); bw.Write((short)2); bw.Write((short)16);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(bytes);
            foreach (var v in data) bw.Write((short)(Mathf.Clamp(float.IsNaN(v) ? 0f : v, -1f, 1f) * 32767));
        }

        // Главное меню, настройки и обучение на нескольких языках — Logs/screen_*.png
        public static void Screens()
        {
            foreach (var lang in new[] { Lang.En, Lang.Ru, Lang.Kk, Lang.Es })
            {
                Loc.Set(lang);
                string l = lang.ToString().ToLower();
                Shoot($"menu_{l}", ui => ui.ShowMenu(), "screen");
                Shoot($"settings_{l}", ui => ui.ShowSettings(), "screen");
                Shoot($"tutorial_{l}", ui => ui.DebugTutorial(2), "screen");
                Shoot($"ranks_{l}", ui => ui.DebugPanel("ranks"), "screen");
                Shoot($"trophies_{l}", ui => ui.DebugPanel("trophies"), "screen");
                Shoot($"story_{l}", ui => ui.DebugPanel("story"), "screen");
                Shoot($"dawn_{l}", ui => ui.DebugPanel("dawn"), "screen");
                Shoot($"hud_{l}", ui => ui.DebugHud(Inventory.Starting(), false), "screen");
                Shoot($"bag_{l}", ui => ui.DebugHud(Inventory.Starting(), true), "screen");
            }
            Loc.Set(Lang.En);
        }

        static void Shoot(string name, System.Action<GameUI> open, string prefix = "journal")
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.2f, 0.22f);
            var rt = new RenderTexture(540, 1170, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.aspect = 540f / 1170f;

            var ui = GameUI.Create();
            var canvas = ui.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
            open(ui);
            Canvas.ForceUpdateCanvases();
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes($"Logs/{prefix}_{name}.png", tex.EncodeToPNG());
        }
    }
}
