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
            Catalog.MarkSeen(EntityKind.Ghost);
            Catalog.MarkSeen(EntityKind.Shade);
            Catalog.MarkSeen(EntityKind.Albasty);
            Catalog.MarkSeen(EntityKind.Mara);

            Shoot("list", ui => ui.DebugJournal("list"));
            Shoot("entry", ui => ui.DebugJournal("entry", EntityKind.Kikimora));
            Shoot("clues", ui => ui.DebugJournal("clues"));
            Shoot("howto", ui => ui.DebugJournal("howto"));
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
