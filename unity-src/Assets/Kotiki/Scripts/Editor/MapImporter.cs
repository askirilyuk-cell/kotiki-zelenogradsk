using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Kotiki.EditorTools
{
    /// <summary>
    /// Шаг 1: data.json -> сцена URP (земля, вода, дороги, дома-коробки, деревья).
    /// </summary>
    public static class MapImporter
    {
        const string DataPath = "Assets/Kotiki/Data/data.json";
        const string ScenePath = "Assets/Kotiki/Scenes/Zelenogradsk.unity";
        const string MaterialsDir = "Assets/Kotiki/Materials";
        const string SettingsDir = "Assets/Kotiki/Settings";
        const float ChunkSize = 200f;

        // Высоты слоёв: слои лежат чуть друг над другом, чтобы не мерцали.
        const float YGreen = 0.004f;
        const float YBeach = 0.02f;
        const float YWater = 0.03f;
        const float YIsland = 0.04f;
        const float YRoad = 0.06f;
        const float YPier = 0.12f;

        [MenuItem("Котики/1. Импортировать карту (data.json)")]
        public static void ImportMenu()
        {
            if (!File.Exists(DataPath))
            {
                EditorUtility.DisplayDialog("Нет data.json",
                    $"Положите файл по пути {DataPath}.\nОн собирается скриптами tools/ в корне проекта.", "Ок");
                return;
            }

            try
            {
                Import();
                EditorUtility.DisplayDialog("Готово",
                    "Карта собрана. Проверьте дом на Тургенева — он жёлтый, с красным маячком.", "Ок");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Ошибка импорта", e.Message, "Ок");
            }
        }

        /// Для запуска без редактора: Unity -batchmode -quit -executeMethod Kotiki.EditorTools.MapImporter.ImportHeadless
        public static void ImportHeadless()
        {
            Import();
            Debug.Log("[Котики] импорт в batchmode завершён");
        }

        public static void Import()
        {
            var json = File.ReadAllText(DataPath);
            var data = MapData.Load(json);
            Debug.Log($"[Котики] data.json: домов {data.Buildings.Count}, линий {data.Roads.Count}, " +
                      $"водоёмов {data.Waters.Count}, зелени {data.Green.Count}, котиков {data.Cats.Count}");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var mats = new Materials();
            var layers = new Layers();
            var root = new GameObject("Карта");

            BuildGround(root, data, layers);
            BuildWater(root, data, layers);
            BuildGreenery(root, data, layers);
            BuildRoads(root, data, layers);
            BuildBuildings(root, data, layers);
            BuildTrees(root, data, layers);
            BuildPier(root, data, layers);
            BuildHomeMarker(root, data);
            BuildLabels(root, data);

            layers.Flush(root.transform, mats);

            SetupLighting();
            SetupCamera(data);
            SetupPostProcessing();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets/Kotiki/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            var homeWorld = new Vector3(data.Home[0], data.Home[2], -data.Home[1]);
            var homeBuilding = FindHomeBuilding(data);
            Debug.Log(homeBuilding != null
                ? $"[Котики] наш дом найден: высота {homeBuilding.Height:0.0} м, мир {homeWorld}"
                : "[Котики] ВНИМАНИЕ: дом с флагом isHome не найден в b[]");

            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(homeWorld, Quaternion.Euler(25f, 135f, 0f), 220f);

            AssetDatabase.SaveAssets();
        }

        static Building FindHomeBuilding(MapData data)
        {
            var p = new Vector2(data.Home[0], data.Home[1]);
            foreach (var b in data.Buildings)
                if (b.IsHome) return b;
            foreach (var b in data.Buildings)
                if (GeoMath.PointInPolygon(b.Footprint, p)) return b;
            return null;
        }

        // ---------- слои с нарезкой на чанки ----------

        class Layers
        {
            readonly Dictionary<string, Dictionary<long, MeshBuilder>> _layers =
                new Dictionary<string, Dictionary<long, MeshBuilder>>();

            public MeshBuilder Get(string layer, Vector2 p)
            {
                if (!_layers.TryGetValue(layer, out var chunks))
                    _layers[layer] = chunks = new Dictionary<long, MeshBuilder>();

                long cx = Mathf.FloorToInt(p.x / ChunkSize);
                long cz = Mathf.FloorToInt(p.y / ChunkSize);
                long key = cx * 100000 + cz;
                if (!chunks.TryGetValue(key, out var mb))
                    chunks[key] = mb = new MeshBuilder();
                return mb;
            }

            public void Flush(Transform root, Materials mats)
            {
                foreach (var layerPair in _layers)
                {
                    var layerRoot = new GameObject(layerPair.Key).transform;
                    layerRoot.SetParent(root, false);
                    foreach (var chunk in layerPair.Value)
                    {
                        if (chunk.Value.IsEmpty) continue;
                        var go = new GameObject(layerPair.Key + "_" + chunk.Key);
                        go.transform.SetParent(layerRoot, false);
                        go.AddComponent<MeshFilter>().sharedMesh = chunk.Value.ToMesh(go.name);
                        var mr = go.AddComponent<MeshRenderer>();
                        mr.sharedMaterial = mats.Get(layerPair.Key);
                        bool solid = layerPair.Key.StartsWith("Стена") || layerPair.Key == "Дом" ||
                                     layerPair.Key.StartsWith("Крыша") || layerPair.Key == "Хвоя" ||
                                     layerPair.Key == "Листва" || layerPair.Key == "Ствол";
                        mr.shadowCastingMode = solid ? ShadowCastingMode.On : ShadowCastingMode.Off;
                        mr.receiveShadows = true;
                    }
                }
            }
        }

        // ---------- материалы ----------

        class Materials
        {
            readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

            public Material Get(string name)
            {
                if (_cache.TryGetValue(name, out var mat)) return mat;
                mat = LoadOrCreate(name);
                _cache[name] = mat;
                return mat;
            }

            static Material LoadOrCreate(string name)
            {
                var path = $"{MaterialsDir}/{name}.mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing != null) return existing;

                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                var mat = new Material(shader) { name = name };

                var c = Spec(name);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c.color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c.color);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", c.smoothness);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", c.smoothness);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", c.metallic);

                var facadeKind = FacadeKind(name);
                if (facadeKind != null)
                {
                    var tex = FacadeTextures.Get(facadeKind);
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                }

                Directory.CreateDirectory(MaterialsDir);
                AssetDatabase.CreateAsset(mat, path);
                return mat;
            }

            static string FacadeKind(string name)
            {
                if (name == "Дом" || name.StartsWith("Стена частная")) return "частная";
                if (name.StartsWith("Стена панельная")) return "панельная";
                if (name.StartsWith("Стена общественная")) return "общественная";
                if (name.StartsWith("Стена храм")) return "храм";
                return null;
            }

            static (Color color, float smoothness, float metallic) Spec(string name)
            {
                switch (name)
                {
                    case "Земля": return (Hex("#6f8f54"), 0.05f, 0f);
                    case "Песок": return (Hex("#d9c89b"), 0.12f, 0f);
                    case "Вода": return (Hex("#2f7fb0"), 0.9f, 0f);
                    case "Остров": return (Hex("#5d8b46"), 0.05f, 0f);
                    case "Газон": return (Hex("#5f8a48"), 0.05f, 0f);
                    case "Лес": return (Hex("#3a5c34"), 0.05f, 0f);
                    case "Спортплощадка": return (Hex("#4f7f4a"), 0.25f, 0f);
                    case "Асфальт": return (Hex("#3f3f42"), 0.35f, 0f);
                    case "Тротуар": return (Hex("#8d8a83"), 0.25f, 0f);
                    case "Пешеходная": return (Hex("#a8a294"), 0.25f, 0f);
                    case "ЖД": return (Hex("#6b6b6b"), 0.3f, 0.4f);
                    case "Дом": return (Hex("#f2c94c"), 0.2f, 0f);
                    case "Стена частная 1": return (Hex("#e6cfae"), 0.12f, 0f);
                    case "Стена частная 2": return (Hex("#efe2c2"), 0.12f, 0f);
                    case "Стена частная 3": return (Hex("#c9d3bd"), 0.12f, 0f);
                    case "Стена панельная 1": return (Hex("#cfd4d8"), 0.15f, 0f);
                    case "Стена панельная 2": return (Hex("#e2d8c6"), 0.15f, 0f);
                    case "Стена сарай": return (Hex("#8f8677"), 0.1f, 0f);
                    case "Стена общественная": return (Hex("#e8e0cc"), 0.15f, 0f);
                    case "Стена храм": return (Hex("#e8d0c0"), 0.15f, 0f);
                    case "Цоколь": return (Hex("#7d7468"), 0.1f, 0f);
                    case "Крыша 1": return (Hex("#a5502f"), 0.18f, 0f);
                    case "Крыша 2": return (Hex("#8c4a3a"), 0.18f, 0f);
                    case "Крыша 3": return (Hex("#6e6a5f"), 0.18f, 0f);
                    case "Крыша плоская": return (Hex("#6e6e70"), 0.2f, 0f);
                    case "Ствол": return (Hex("#6b4a2f"), 0.05f, 0f);
                    case "Хвоя": return (Hex("#2f5c37"), 0.05f, 0f);
                    case "Листва": return (Hex("#4d7c3a"), 0.05f, 0f);
                    case "Пирс": return (Hex("#b08968"), 0.12f, 0f);
                    case "Маячок": return (Hex("#ff2d55"), 0.6f, 0f);
                    default: return (Hex("#cccccc"), 0.15f, 0f);
                }
            }

            static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }

        // ---------- слои карты ----------

        static void BuildGround(GameObject root, MapData data, Layers layers)
        {
            Vector2 min = data.BoundsMin - Vector2.one * 400f;
            Vector2 max = data.BoundsMax + Vector2.one * 400f;
            // море уходит за границы города — земля должна быть под ним
            if (data.Sea != null)
                foreach (var p in data.Sea)
                {
                    min = Vector2.Min(min, p - Vector2.one * 300f);
                    max = Vector2.Max(max, p + Vector2.one * 300f);
                }

            layers.Get("Земля", new Vector2(min.x, min.y)).AddFlatPolygon(new[]
            {
                new Vector2(min.x, min.y), new Vector2(max.x, min.y),
                new Vector2(max.x, max.y), new Vector2(min.x, max.y)
            }, 0f);
        }

        static void BuildWater(GameObject root, MapData data, Layers layers)
        {
            if (data.Sea != null && data.Sea.Length >= 3)
                layers.Get("Вода", data.Sea[0]).AddFlatPolygon(data.Sea, YWater);

            foreach (var body in data.Waters)
            {
                layers.Get("Вода", body.Outer[0]).AddFlatPolygon(body.Outer, YWater);
                foreach (var hole in body.Holes)
                    if (hole.Length >= 3)
                        layers.Get("Остров", hole[0]).AddFlatPolygon(hole, YIsland);
            }

            foreach (var beach in data.Beach)
                if (beach.Length >= 3)
                    layers.Get("Песок", beach[0]).AddFlatPolygon(beach, YBeach);
        }

        static void BuildGreenery(GameObject root, MapData data, Layers layers)
        {
            foreach (var area in data.Green)
            {
                if (area.Contour.Length < 3) continue;
                var layer = area.Kind == 1 ? "Лес" : area.Kind == 2 ? "Спортплощадка" : "Газон";
                layers.Get(layer, area.Contour[0]).AddFlatPolygon(area.Contour, YGreen);
            }
        }

        static void BuildRoads(GameObject root, MapData data, Layers layers)
        {
            foreach (var road in data.Roads)
            {
                if (road.Points.Length < 2) continue;
                string layer;
                float y = YRoad;
                switch (road.Cls)
                {
                    case 0: layer = "ЖД"; break;
                    case 2: layer = "Тротуар"; break;
                    case 3: layer = "Пешеходная"; break;
                    case 4: layer = "Вода"; y = YWater; break;
                    default: layer = "Асфальт"; break;
                }
                layers.Get(layer, road.Points[0]).AddRibbon(road.Points, Mathf.Max(1.2f, road.Width), y);
            }
        }

        static void BuildBuildings(GameObject root, MapData data, Layers layers)
        {
            foreach (var b in data.Buildings)
            {
                if (b.Footprint.Length < 3) continue;
                var anchor = b.Footprint[0];
                int hash = Hash(anchor);

                string wallLayer;
                if (b.IsHome) wallLayer = "Дом";
                else
                    switch (b.Kind)
                    {
                        case 1: wallLayer = hash % 2 == 0 ? "Стена панельная 1" : "Стена панельная 2"; break;
                        case 2: wallLayer = "Стена сарай"; break;
                        case 3: wallLayer = "Стена общественная"; break;
                        case 4: wallLayer = "Стена храм"; break;
                        default: wallLayer = "Стена частная " + (1 + hash % 3); break;
                    }

                string roofLayer = b.HipRoof ? "Крыша " + (1 + hash % 3) : "Крыша плоская";

                Props.AddBuilding(layers.Get(wallLayer, anchor), layers.Get(roofLayer, anchor),
                                  layers.Get("Цоколь", anchor), b);
            }
        }

        static int Hash(Vector2 p) => Mathf.Abs((int)(p.x * 7.31f + p.y * 13.17f));

        static void BuildTrees(GameObject root, MapData data, Layers layers)
        {
            var rng = new System.Random(20261030);
            int count = 0;
            const int budget = 8000;
            var buildings = data.Buildings;

            foreach (var area in data.Green)
            {
                if (area.Kind == 2 || area.Contour.Length < 3) continue; // площадки не засаживаем
                if (count >= budget) break;

                float areaSize = Mathf.Abs(GeoMath.SignedArea(area.Contour));
                float perTree = area.Kind == 1 ? 200f : 1500f;
                int want = Mathf.Clamp(Mathf.RoundToInt(areaSize / perTree), 0, Mathf.Min(400, budget - count));
                if (want == 0) continue;

                Vector2 min = area.Contour[0], max = area.Contour[0];
                foreach (var p in area.Contour)
                {
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }

                int placed = 0, attempts = 0;
                while (placed < want && attempts < want * 12)
                {
                    attempts++;
                    var p = new Vector2(
                        Mathf.Lerp(min.x, max.x, (float)rng.NextDouble()),
                        Mathf.Lerp(min.y, max.y, (float)rng.NextDouble()));

                    if (!GeoMath.PointInPolygon(area.Contour, p)) continue;
                    if (BlockedForTree(p, buildings, data)) continue;

                    float scale = 0.8f + (float)rng.NextDouble() * 0.6f;
                    bool pine = area.Kind == 1 ? rng.NextDouble() < 0.65 : rng.NextDouble() < 0.25;
                    Props.AddTree(layers.Get("Ствол", p), layers.Get(pine ? "Хвоя" : "Листва", p), p, scale, pine);
                    placed++;
                    count++;
                }
            }
            Debug.Log($"[Котики] деревьев: {count}");
        }

        static bool BlockedForTree(Vector2 p, List<Building> buildings, MapData data)
        {
            // грубая проверка: не внутри дома и не в воде
            for (int i = 0; i < buildings.Count; i++)
            {
                var fp = buildings[i].Footprint;
                if (fp.Length < 3) continue;
                Vector2 min = fp[0], max = fp[0];
                foreach (var q in fp) { min = Vector2.Min(min, q); max = Vector2.Max(max, q); }
                if (p.x < min.x - 1f || p.x > max.x + 1f || p.y < min.y - 1f || p.y > max.y + 1f) continue;
                if (GeoMath.PointInPolygon(fp, p)) return true;
            }
            foreach (var w in data.Waters)
                if (GeoMath.PointInPolygon(w.Outer, p)) return true;
            return false;
        }

        static void BuildPier(GameObject root, MapData data, Layers layers)
        {
            if (data.Pier == null || data.Pier.Length < 3) return;
            layers.Get("Пирс", data.Pier[0]).AddFlatPolygon(data.Pier, YPier);
        }

        static void BuildHomeMarker(GameObject root, MapData data)
        {
            var pos = new Vector3(data.Home[0], 0f, -data.Home[1]);
            var marker = new GameObject("Наш дом — маячок");
            marker.transform.SetParent(root.transform, false);
            marker.transform.position = pos;

            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Луч";
            pillar.transform.SetParent(marker.transform, false);
            pillar.transform.localPosition = new Vector3(0f, 16f, 0f);
            pillar.transform.localScale = new Vector3(0.8f, 16f, 0.8f);
            Object.DestroyImmediate(pillar.GetComponent<Collider>());

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader) { name = "Маячок" };
            var color = new Color(1f, 0.18f, 0.33f, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static void BuildLabels(GameObject root, MapData data)
        {
            var labels = new GameObject("Ориентиры");
            labels.transform.SetParent(root.transform, false);
            foreach (var label in data.Labels)
            {
                var go = new GameObject(label.Name);
                go.transform.SetParent(labels.transform, false);
                go.transform.position = new Vector3(label.Pos.x, 8f, -label.Pos.y);
                // Текст появится в шаге 3 (UI/миникарта), пока — пустые якоря.
            }
        }

        // ---------- свет и камера ----------

        static void SetupLighting()
        {
            var sunGo = new GameObject("Солнце");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.85f, 0.63f);
            sun.intensity = 2.1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sunGo.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.81f, 0.90f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.70f, 0.78f);
            RenderSettings.ambientGroundColor = new Color(0.54f, 0.48f, 0.33f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.73f, 0.83f, 0.91f);
            RenderSettings.fogStartDistance = 500f;
            RenderSettings.fogEndDistance = 5200f;

            var sky = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            if (sky != null) RenderSettings.skybox = sky;
        }

        static Camera SetupCamera(MapData data)
        {
            var go = new GameObject("Главная камера") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 6000f;

            var home = new Vector3(data.Home[0], 0f, -data.Home[1]);
            go.transform.position = home + new Vector3(-55f, 60f, -80f);
            go.transform.LookAt(home + Vector3.up * 8f);

            var urpCam = cam.GetUniversalAdditionalCameraData();
            urpCam.renderPostProcessing = true;
            urpCam.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            urpCam.antialiasingQuality = AntialiasingQuality.Medium;
            return cam;
        }

        static void SetupPostProcessing()
        {
            Directory.CreateDirectory(SettingsDir);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, $"{SettingsDir}/Post.asset");

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.ACES;
            AssetDatabase.AddObjectToAsset(tone, profile);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.overrideState = true;
            color.postExposure.value = 0.3f;
            color.saturation.overrideState = true;
            color.saturation.value = 8f;
            AssetDatabase.AddObjectToAsset(color, profile);

            var go = new GameObject("Постобработка");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.profile = profile;
        }
    }
}
