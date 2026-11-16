using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Motion2RGB.EditorTools
{
    /// <summary>
    /// Builds Assets/Motion2RGB/Scenes/Motion2RGB_Apartment.unity: takes the robot scene (for the
    /// rig, cameras and UI), strips the warehouse geometry and furnishes a room with CC0 props.
    /// The source scene is opened read-only and saved under a new path.
    /// </summary>
    public static class ApartmentSceneBuilder
    {
        private const string SourceScene = "Assets/Motion2RGB/Scenes/Motion2RGB_Robot.unity";
        private const string TargetScene = "Assets/Motion2RGB/Scenes/Motion2RGB_Apartment.unity";
        private const string PropRoot = "Assets/Motion2RGB/Showcase/Props";
        private const string TexRoot = "Assets/Motion2RGB/Showcase/Textures";
        private const string MatRoot = "Assets/Motion2RGB/Showcase/Materials";

        // room extent (metres)
        private const float RoomX = 4.5f;      // half width
        private const float RoomZMin = -3f;
        private const float RoomZMax = 6f;
        private const float RoomHeight = 2.8f;

        private static readonly string[] RemovePrefixes =
        {
            "Floor", "Floor Stripe", "Overhead Beam", "Ceiling Lamp", "Table", "Wall Panel",
            "Route Beacon", "Crate", "Turn Wall", "Pillar", "Goal Marker", "Back Wall",
            "Left Corridor Wall", "Right Corridor Wall", "End Wall", "Cabinet"
        };

        private static readonly (string asset, float x, float y, float z, float yaw)[] Placements =
        {
            ("Sofa_01", 0f, 0f, 4.3f, 180f),
            ("CoffeeTable_01", 0f, 0f, 2.3f, 0f),
            ("wooden_bookshelf_worn", -4.0f, 0f, 1.2f, 90f),
            ("modern_wooden_cabinet", 4.0f, 0f, 4.3f, -90f),
            ("Television_01", 4.05f, 1.25f, 0.6f, -90f),
            ("potted_plant_01", -3.8f, 0f, -1.4f, 20f),
            ("modern_ceiling_lamp_01", 0f, 2.5f, 2.3f, 0f),
        };

        [MenuItem("Tools/Motion2RGB/Experiments/Build Apartment Scene")]
        public static void Build()
        {
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, TargetScene, true))
            {
                Debug.LogError("[Apartment] could not save " + TargetScene);
                return;
            }

            int removed = 0;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "Motion2RGB Cruise Route" || go.name == "PlayerRig") continue;
                if (RemovePrefixes.Any(p => go.name.StartsWith(p)))
                {
                    Object.DestroyImmediate(go);
                    removed++;
                }
            }

            var rig = GameObject.Find("PlayerRig");
            if (rig != null)
            {
                rig.transform.position = new Vector3(0f, 1.05f, -2.4f);
                rig.transform.rotation = Quaternion.identity;
            }

            BuildRoom();
            var root = new GameObject("Apartment Props").transform;
            int placed = 0;
            foreach (var item in Placements)
            {
                placed += Place(root, item.asset, item.x, item.y, item.z, item.yaw) ? 1 : 0;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TargetScene);
            Debug.Log("[Apartment] built " + TargetScene + " removed=" + removed + " props=" + placed);
        }

        private static bool Place(Transform root, string asset, float x, float y, float z, float yaw)
        {
            string dir = PropRoot + "/" + asset;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dir + "/" + asset + "_1k.fbx");
            if (prefab == null)
            {
                Debug.LogWarning("[Apartment] missing " + asset);
                return false;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab,
                EditorSceneManager.GetActiveScene());
            instance.name = asset + "_" + root.childCount;
            instance.transform.SetParent(root, true);
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var renderers = instance.GetComponentsInChildren<Renderer>();
            float drop = 0f;
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
                if (maxDim > 4f) instance.transform.localScale = Vector3.one * (4f / maxDim);
                b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                drop = b.min.y;
            }
            instance.transform.position = new Vector3(x, y - drop, z);

            ApplyMaterial(instance, dir, asset);
            return true;
        }

        private static void BuildRoom()
        {
            var floorMat = Surface("Apartment_FloorMat", TexRoot + "/wood_floor", new Vector2(6f, 6f), 0.25f);
            var wallMat = Surface("Apartment_WallMat", TexRoot + "/painted_plaster_wall", new Vector2(8f, 2f), 0.15f);

            float depth = RoomZMax - RoomZMin;
            float centerZ = (RoomZMax + RoomZMin) * 0.5f;
            Box("Floor", new Vector3(0f, -0.05f, centerZ), new Vector3(RoomX * 2f, 0.1f, depth), floorMat);
            Box("Ceiling", new Vector3(0f, RoomHeight, centerZ), new Vector3(RoomX * 2f, 0.1f, depth), CeilingMaterial());
            Box("Wall Left", new Vector3(-RoomX, RoomHeight * 0.5f, centerZ), new Vector3(0.15f, RoomHeight, depth), wallMat);
            Box("Wall Right", new Vector3(RoomX, RoomHeight * 0.5f, centerZ), new Vector3(0.15f, RoomHeight, depth), wallMat);
            Box("Wall Back", new Vector3(0f, RoomHeight * 0.5f, RoomZMin), new Vector3(RoomX * 2f, RoomHeight, 0.15f), wallMat);
            Box("Wall Front", new Vector3(0f, RoomHeight * 0.5f, RoomZMax), new Vector3(RoomX * 2f, RoomHeight, 0.15f), wallMat);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.78f, 0.78f, 0.8f);

            var sun = GameObject.Find("Key Light");
            if (sun != null)
            {
                var light = sun.GetComponent<Light>();
                if (light != null) light.intensity = Mathf.Max(light.intensity, 1.35f);
            }
            var fill = new GameObject("Apartment Fill Light");
            var fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.95f;
            fillLight.color = new Color(1f, 0.96f, 0.9f);
            fill.transform.rotation = Quaternion.Euler(35f, -140f, 0f);
        }

        private static Material CeilingMaterial()
        {
            string path = MatRoot + "/Apartment_CeilingMat.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard")) { name = "Apartment_CeilingMat" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = new Color(0.86f, 0.86f, 0.84f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.62f, 0.62f, 0.6f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material Surface(string name, string folder, Vector2 tiling, float smoothness)
        {
            var diff = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + Path.GetFileName(folder) + "_diff_1k.jpg");
            var nor = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + Path.GetFileName(folder) + "_nor_gl_1k.jpg");
            string path = MatRoot + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard")) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            if (diff != null)
            {
                mat.SetTexture("_MainTex", diff);
                mat.SetTextureScale("_MainTex", tiling);
            }
            if (nor != null)
            {
                mat.SetTexture("_BumpMap", nor);
                mat.SetTextureScale("_BumpMap", tiling);
                mat.EnableKeyword("_NORMALMAP");
            }
            mat.SetFloat("_Glossiness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
        }

        private static void ApplyMaterial(GameObject instance, string dir, string asset)
        {
            var files = Directory.GetFiles(dir, "*_diff_1k.jpg");
            if (files.Length == 0) return;
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(files[0].Replace('\\', '/'));
            var nors = Directory.GetFiles(dir, "*_nor_gl_1k.jpg");
            var normal = nors.Length > 0 ? AssetDatabase.LoadAssetAtPath<Texture2D>(nors[0].Replace('\\', '/')) : null;
            if (diffuse == null) return;

            string path = MatRoot + "/" + asset + "_mat.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard")) { name = asset + "_mat" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_MainTex", diffuse);
            mat.SetFloat("_Glossiness", 0.22f);
            mat.SetFloat("_Metallic", 0f);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(mat);

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial = mat;
            }
        }
    }
}

