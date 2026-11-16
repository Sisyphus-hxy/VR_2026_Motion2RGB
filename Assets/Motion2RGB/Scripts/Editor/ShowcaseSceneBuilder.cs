using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Motion2RGB.EditorTools
{
    /// <summary>
    /// Builds Assets/Motion2RGB/Scenes/Motion2RGB_Showcase.unity from the original robot scene
    /// and populates it with CC0 props (Poly Haven). The original scene is opened read-only and
    /// saved under a new path, so it is never modified.
    /// </summary>
    public static class ShowcaseSceneBuilder
    {
        private const string SourceScene = "Assets/Motion2RGB/Scenes/Motion2RGB_Robot.unity";
        private const string TargetScene = "Assets/Motion2RGB/Scenes/Motion2RGB_Showcase.unity";
        private const string PropRoot = "Assets/Motion2RGB/Showcase/Props";

        // asset folder, x, z, yaw, uniform scale hint
        private static readonly (string asset, float x, float z, float yaw, float scale)[] Placements =
        {
            ("old_military_crate", -2.2f, -4.8f, 20f, 1f),
            ("old_military_crate", -1.6f, -4.2f, 65f, 1f),
            ("cardboard_box_01", 1.8f, -4.6f, -15f, 1f),
            ("cardboard_box_01", 2.3f, -3.8f, 30f, 1f),
            ("industrial_pastic_container", -2.8f, -1.9f, 10f, 1f),
            ("metal_tool_chest", 2.6f, -1.1f, -8f, 1f),
            ("Shelf_01", 3.3f, 2.2f, -90f, 1f),
            ("Shelf_01", -3.0f, 3.4f, 90f, 1f),
            ("old_military_crate", 0.9f, 5.4f, -20f, 1f),
            ("industrial_pastic_container", -0.7f, 7.6f, 150f, 1f),
        };

        [MenuItem("Tools/Motion2RGB/Experiments/Build Showcase Scene")]
        public static void Build()
        {
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, TargetScene, true))
            {
                Debug.LogError("[Showcase] could not save " + TargetScene);
                return;
            }

            var root = new GameObject("Showcase Props").transform;
            SetupEnvironment();
            ApplySurfaces();
            int placed = 0;            foreach (var item in Placements)
            {
                string dir = Path.Combine(PropRoot, item.asset).Replace('\\', '/');
                string fbx = Path.Combine(dir, item.asset + "_1k.fbx").Replace('\\', '/');
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                if (prefab == null)
                {
                    Debug.LogWarning("[Showcase] missing model " + fbx);
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = item.asset + "_" + placed;
                instance.transform.SetParent(root, true);
                instance.transform.rotation = Quaternion.Euler(0f, item.yaw, 0f);

                // sit the model on the floor and keep it inside a sane size
                var renderers = instance.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var b = renderers[0].bounds;
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                    float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
                    float scale = item.scale * (maxDim > 4f ? 4f / maxDim : 1f);
                    instance.transform.localScale = Vector3.one * scale;

                    b = renderers[0].bounds;
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                    instance.transform.position = new Vector3(item.x, -b.min.y, item.z);
                }
                else
                {
                    instance.transform.position = new Vector3(item.x, 0f, item.z);
                }

                ApplyMaterial(instance, dir, item.asset);
                placed++;
            }

            foreach (string name in new[] { "Crate A", "Crate B" })
            {
                var go = GameObject.Find(name);
                if (go != null) go.SetActive(false);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TargetScene);
            Debug.Log("[Showcase] built " + TargetScene + " with " + placed + " props");
        }

        private static void ApplySurfaces()
        {
            const string texRoot = "Assets/Motion2RGB/Showcase/Textures";
            var floorDiff = AssetDatabase.LoadAssetAtPath<Texture2D>(texRoot + "/concrete_floor_02/concrete_floor_02_diff_1k.jpg");
            var floorNor = AssetDatabase.LoadAssetAtPath<Texture2D>(texRoot + "/concrete_floor_02/concrete_floor_02_nor_gl_1k.jpg");
            var wallDiff = AssetDatabase.LoadAssetAtPath<Texture2D>(texRoot + "/painted_concrete/painted_concrete_diff_1k.jpg");
            var wallNor = AssetDatabase.LoadAssetAtPath<Texture2D>(texRoot + "/painted_concrete/painted_concrete_nor_gl_1k.jpg");

            AssignSurface("Floor", "Showcase_FloorMat", floorDiff, floorNor, new Vector2(9f, 15f), 0.12f);
            AssignSurface("Left Corridor Wall", "Showcase_WallMat_L", wallDiff, wallNor, new Vector2(10f, 2f), 0.18f);
            AssignSurface("Right Corridor Wall", "Showcase_WallMat_R", wallDiff, wallNor, new Vector2(10f, 2f), 0.18f);
            AssignSurface("Back Wall", "Showcase_WallMat_B", wallDiff, wallNor, new Vector2(6f, 2f), 0.18f);
            AssignSurface("End Wall", "Showcase_WallMat_E", wallDiff, wallNor, new Vector2(6f, 2f), 0.18f);
        }

        private static void AssignSurface(string objectName, string materialName, Texture2D diffuse,
                                          Texture2D normal, Vector2 tiling, float smoothness)
        {
            var go = GameObject.Find(objectName);
            if (go == null)
            {
                Debug.LogWarning("[Showcase] surface object not found: " + objectName);
                return;
            }
            if (diffuse == null) return;

            const string matDir = "Assets/Motion2RGB/Showcase/Materials";
            if (!AssetDatabase.IsValidFolder(matDir))
            {
                AssetDatabase.CreateFolder("Assets/Motion2RGB/Showcase", "Materials");
            }

            string path = matDir + "/" + materialName + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard")) { name = materialName };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_MainTex", diffuse);
            mat.SetTextureScale("_MainTex", tiling);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetTextureScale("_BumpMap", tiling);
                mat.EnableKeyword("_NORMALMAP");
            }
            mat.SetFloat("_Glossiness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = mat;
        }

        private static void SetupEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.47f);

            // The preview cameras force a solid-colour clear, so a skybox would never show.
            // Close the room with a ceiling instead of leaving a black void overhead.
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Showcase Ceiling";
            ceiling.transform.position = new Vector3(0f, 4.3f, 5f);
            ceiling.transform.localScale = new Vector3(18f, 0.2f, 30f);
            Object.DestroyImmediate(ceiling.GetComponent<Collider>());
            var ceilingMat = new Material(Shader.Find("Standard")) { name = "Showcase Ceiling Mat" };
            ceilingMat.color = new Color(0.52f, 0.54f, 0.56f);
            ceilingMat.EnableKeyword("_EMISSION");
            ceilingMat.SetColor("_EmissionColor", new Color(0.32f, 0.33f, 0.35f));
            ceiling.GetComponent<Renderer>().sharedMaterial = ceilingMat;

            var fill = new GameObject("Showcase Fill Light");
            var fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.75f;
            fillLight.color = new Color(0.85f, 0.9f, 1f);
            fill.transform.rotation = Quaternion.Euler(38f, -35f, 0f);

            var back = new GameObject("Showcase Back Light");
            var backLight = back.AddComponent<Light>();
            backLight.type = LightType.Directional;
            backLight.intensity = 0.35f;
            back.transform.rotation = Quaternion.Euler(22f, 150f, 0f);
        }

        private static void ApplyMaterial(GameObject instance, string dir, string asset)
        {
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + asset + "_diff_1k.jpg");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + asset + "_nor_gl_1k.jpg");
            if (diffuse == null) return;

            var shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = asset + "_mat" };
            mat.SetTexture("_MainTex", diffuse);
            mat.SetFloat("_Glossiness", 0.25f);
            mat.SetFloat("_Metallic", 0.05f);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }

            string matPath = dir + "/" + asset + "_mat.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null)
            {
                existing.CopyPropertiesFromMaterial(mat);
            }
            else
            {
                AssetDatabase.CreateAsset(mat, matPath);
                existing = mat;
            }
            AssetDatabase.SaveAssets();

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial = existing;
            }
        }
    }
}

