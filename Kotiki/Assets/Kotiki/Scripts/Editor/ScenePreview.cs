using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kotiki.EditorTools
{
    /// <summary>Снимки сцены без открытия редактора — чтобы проверять результат глазами.</summary>
    public static class ScenePreview
    {
        const string ScenePath = "Assets/Kotiki/Scenes/Zelenogradsk.unity";

        [MenuItem("Котики/2. Сделать снимки сцены")]
        public static void RenderMenu() => Render();

        /// Unity -batchmode -quit -executeMethod Kotiki.EditorTools.ScenePreview.RenderHeadless
        public static void RenderHeadless()
        {
            Render();
            Debug.Log("[Котики] снимки готовы");
        }

        public static void Render()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var cam = Camera.main;
            if (cam == null)
            {
                foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) { cam = c; break; }
            }
            if (cam == null) throw new System.Exception("В сцене нет камеры");

            var data = MapData.Load(File.ReadAllText(MapImporter.DataPath));
            var home = new Vector3(data.Home[0], 0f, data.Home[1]);
            var pier = data.Pier != null && data.Pier.Length > 0
                ? new Vector3(data.Pier[0].x, 0f, data.Pier[0].y)
                : home;

            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../previews"));
            Directory.CreateDirectory(outDir);

            var shots = new (string name, Vector3 pos, Vector3 look, float fov)[]
            {
                ("01-home", home + new Vector3(70f, 55f, 100f), home + Vector3.up * 14f, 45f),
                ("02-home-street", home + new Vector3(-15f, 3f, -120f), home + Vector3.up * 16f, 50f),
                ("06-home-top", home + new Vector3(0f, 150f, -30f), home, 55f),
                ("03-city", new Vector3(120f, 320f, -60f), new Vector3(250f, 0f, -420f), 55f),
                ("04-sea", pier + new Vector3(-250f, 170f, -420f), pier + new Vector3(0f, 0f, 500f), 55f),
                ("05-park", new Vector3(80f, 90f, -260f), new Vector3(260f, 0f, -420f), 55f),
            };

            const int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.targetTexture = rt;

            // первый кадр URP в batchmode отдаёт недогруженные материалы
            cam.transform.position = home + new Vector3(200f, 200f, 200f);
            cam.transform.LookAt(home);
            cam.Render();
            cam.Render();

            foreach (var shot in shots)
            {
                cam.transform.position = shot.pos;
                cam.transform.LookAt(shot.look);
                cam.fieldOfView = shot.fov;
                cam.Render();
                cam.Render();

                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                var path = Path.Combine(outDir, shot.name + ".png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log("[Котики] снимок " + path);
            }

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
