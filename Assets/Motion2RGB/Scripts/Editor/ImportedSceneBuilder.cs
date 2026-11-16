using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Motion2RGB.EditorTools
{
    /// <summary>
    /// Builds a scene around a complete CC0 environment imported as glTF (3dassets.dev starter
    /// scenes). The robot rig, cameras and UI come from the original scene; the warehouse
    /// geometry is removed. Select the environment with the M2R_IMPORT environment variable:
    ///   "office" (default) or "flat".
    /// </summary>
    public static class ImportedSceneBuilder
    {
        private const string SourceScene = "Assets/Motion2RGB/Scenes/Motion2RGB_Robot.unity";
        private const string GlbRoot = "Assets/Motion2RGB/Showcase/Scenes";

        private static readonly string[] RemovePrefixes =
        {
            "Floor", "Floor Stripe", "Overhead Beam", "Ceiling Lamp", "Table", "Wall Panel",
            "Route Beacon", "Crate", "Turn Wall", "Pillar", "Goal Marker", "Back Wall",
            "Left Corridor Wall", "Right Corridor Wall", "End Wall", "Cabinet"
        };

        [MenuItem("Tools/Motion2RGB/Experiments/Build Imported Scene")]
        public static void Build()
        {
            string which = (System.Environment.GetEnvironmentVariable("M2R_IMPORT") ?? "office").ToLowerInvariant();
            string glb, target;
            Vector3 rigPos;
            float rigYaw;

            if (which == "flat")
            {
                glb = GlbRoot + "/one_bedroom_flat.glb";
                target = "Assets/Motion2RGB/Scenes/Motion2RGB_Flat.unity";
                rigPos = new Vector3(0f, 1.05f, -4.0f);
                rigYaw = 0f;
            }
            else
            {
                glb = GlbRoot + "/office_floor.glb";
                target = "Assets/Motion2RGB/Scenes/Motion2RGB_Office.unity";
                rigPos = new Vector3(-3.2f, 1.05f, -1.6f);
                rigYaw = 55f;
            }

            AssetDatabase.Refresh();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(glb);
            if (prefab == null)
            {
                Debug.LogError("[Import] glTF not loaded: " + glb +
                               " (is com.unity.cloud.gltfast installed and the asset imported?)");
                return;
            }

            var scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, target, true))
            {
                Debug.LogError("[Import] could not save " + target);
                return;
            }

            int removed = 0;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "PlayerRig" || go.name == "Motion2RGB Cruise Route") continue;
                if (RemovePrefixes.Any(p => go.name.StartsWith(p)))
                {
                    Object.DestroyImmediate(go);
                    removed++;
                }
            }

            var env = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            env.name = "Imported Environment";
            env.transform.position = Vector3.zero;
            env.transform.rotation = Quaternion.identity;

            var bounds = ComputeBounds(env, out bool hasBounds);
            if (hasBounds)
            {
                BuildShell(bounds);
                rigPos = new Vector3(bounds.min.x + 1.1f, 1.05f, bounds.min.z + 1.1f);
                rigYaw = 42f;
            }

            var rig = GameObject.Find("PlayerRig");
            if (rig != null)
            {
                rig.transform.position = rigPos;
                rig.transform.rotation = Quaternion.Euler(0f, rigYaw, 0f);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.56f, 0.58f);

            var fill = new GameObject("Imported Scene Fill Light");
            var fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.9f;
            fillLight.color = new Color(1f, 0.97f, 0.92f);
            fill.transform.rotation = Quaternion.Euler(42f, -30f, 0f);

            var key = GameObject.Find("Key Light");
            if (key != null)
            {
                var light = key.GetComponent<Light>();
                if (light != null) light.intensity = Mathf.Max(light.intensity, 1.1f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, target);
            Debug.Log("[Import] built " + target + " glb=" + glb + " removed=" + removed);
        }

        private static Bounds ComputeBounds(GameObject root, out bool ok)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            ok = renderers.Length > 0;
            if (!ok) return new Bounds(Vector3.zero, Vector3.one * 10f);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Closes the imported shell so the solid-colour camera clear never shows.</summary>
        private static void BuildShell(Bounds b)
        {
            var mat = new Material(Shader.Find("Standard")) { name = "Imported_ShellMat" };
            mat.color = new Color(0.62f, 0.63f, 0.65f);
            mat.SetFloat("_Glossiness", 0.1f);

            float h = Mathf.Max(b.max.y + 0.6f, 2.4f);
            var c = b.center;
            var size = b.size;
            float pad = 0.4f;

            var floorMat = new Material(Shader.Find("Standard")) { name = "Imported_FloorMat" };
            floorMat.color = new Color(0.45f, 0.44f, 0.42f);
            floorMat.SetFloat("_Glossiness", 0.2f);
            ShellBox("Shell Floor", new Vector3(c.x, -0.05f, c.z), new Vector3(size.x + pad * 2f, 0.1f, size.z + pad * 2f), floorMat);
            ShellBox("Shell Ceiling", new Vector3(c.x, h, c.z), new Vector3(size.x + pad * 2f, 0.2f, size.z + pad * 2f), mat);
            ShellBox("Shell Left", new Vector3(b.min.x - pad, h * 0.5f, c.z), new Vector3(0.2f, h, size.z + pad * 2f), mat);
            ShellBox("Shell Right", new Vector3(b.max.x + pad, h * 0.5f, c.z), new Vector3(0.2f, h, size.z + pad * 2f), mat);
            ShellBox("Shell Back", new Vector3(c.x, h * 0.5f, b.min.z - pad), new Vector3(size.x + pad * 2f, h, 0.2f), mat);
            ShellBox("Shell Front", new Vector3(c.x, h * 0.5f, b.max.z + pad), new Vector3(size.x + pad * 2f, h, 0.2f), mat);
        }

        private static void ShellBox(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
        }
    }
}



