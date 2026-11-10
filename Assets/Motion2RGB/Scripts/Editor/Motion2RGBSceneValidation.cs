// Validate the committed scene and, when installed, the local-only campus.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Motion2RGB.Editor
{
    public static class Motion2RGBSceneValidation
    {
        [Serializable]
        private class Report
        {
            public string scene;
            public int renderers;
            public int materials;
            public int missingScripts;
            public int missingMaterials;
            public int missingMeshes;
            public int untexturedPbrMaterials;
            public int unsupportedShaders;
            public int routeObstructions;
            public bool routeChecked;
            public string graphicsDevice;

            public bool HasErrors => missingScripts + missingMaterials + missingMeshes + untexturedPbrMaterials + unsupportedShaders + routeObstructions != 0;
        }

        [Serializable]
        private class ValidationSummary { public Report[] scenes; }

        [MenuItem("Tools/Motion2RGB/Checks/Validate Retained Scenes", false, 20)]
        public static void ValidateRetainedScenes()
        {
            if (!Motion2RGBSceneMenu.CanSwitchScenes()) return;
            if (!File.Exists(Motion2RGBSceneMenu.OriginalScene))
                throw new FileNotFoundException("The committed robot scene is missing.", Motion2RGBSceneMenu.OriginalScene);
            string[] available = Motion2RGBSceneMenu.RetainedScenes.Where(File.Exists).ToArray();
            string[] scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
            if (!scenes.SequenceEqual(available.OrderBy(p => p)))
                throw new InvalidOperationException("Assets may contain only the original robot scene and the optional local campus.");
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length == 0 || buildScenes[0].path != Motion2RGBSceneMenu.OriginalScene
                || buildScenes.Any(s => !s.enabled || !available.Contains(s.path))
                || buildScenes.Select(s => s.path).Distinct().Count() != buildScenes.Length)
                throw new InvalidOperationException("Build Settings must start with the robot scene and may additionally include the installed local campus.");

            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Report[] reports = available.Select(path =>
                {
                    EditorSceneManager.OpenScene(path);
                    return InspectScene(path == Motion2RGBSceneMenu.CampusScene);
                }).ToArray();
                Directory.CreateDirectory("Previews");
                File.WriteAllText("Previews/RetainedScenesValidation.json", JsonUtility.ToJson(new ValidationSummary { scenes = reports }, true));
                if (reports.Any(r => r.HasErrors)) throw new InvalidOperationException("Scene validation failed; see Previews/RetainedScenesValidation.json.");
                Debug.Log(reports.Length + " available scene(s) passed validation; no scene assets were rewritten.");
            }
            finally
            {
                if (previous.Length > 0 && previous.All(s => !string.IsNullOrEmpty(s.path) && File.Exists(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        [MenuItem("Tools/Motion2RGB/Checks/Export Campus Previews", false, 21)]
        public static void Capture()
        {
            if (!Motion2RGBSceneMenu.TryOpen(Motion2RGBSceneMenu.CampusScene)) return;
            Report report = InspectScene(true);
            Directory.CreateDirectory("Previews");
            File.WriteAllText("Previews/CampusValidation.json", JsonUtility.ToJson(report, true));
            if (report.HasErrors) throw new InvalidOperationException("Campus validation failed: " + JsonUtility.ToJson(report));
            CaptureView("Campus_Street", new Vector3(-3, 1.75f, -14), new Vector3(0, 2.6f, 22), 68);
            CaptureView("Campus_Overview", new Vector3(0, 17, -24), new Vector3(0, 1, 12), 68);
            CaptureView("Campus_Equipment", new Vector3(1.5f, 2.1f, 1.5f), new Vector3(-10, 1.6f, 12), 60);
            Debug.Log("Campus validation passed; preview images exported to Previews/.");
        }

        [MenuItem("Tools/Motion2RGB/Checks/Export Campus Previews", true)]
        private static bool CanCaptureCampus() { return File.Exists(Motion2RGBSceneMenu.CampusScene); }

        private static Report InspectScene(bool checkRoute)
        {
            GameObject[] objects = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
            Renderer[] renderers = objects.SelectMany(go => go.GetComponents<Renderer>()).ToArray();
            Material[] materials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
            Report report = new Report {
                scene = SceneManager.GetActiveScene().path,
                renderers = renderers.Length,
                materials = materials.Length,
                missingScripts = objects.Sum(go => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go)),
                missingMaterials = renderers.Sum(r => r.sharedMaterials.Count(m => m == null)),
                missingMeshes = objects.SelectMany(go => go.GetComponents<MeshFilter>()).Count(m => m.sharedMesh == null)
                    + renderers.OfType<SkinnedMeshRenderer>().Count(r => r.sharedMesh == null),
                untexturedPbrMaterials = materials.Count(m => m.shader != null && m.shader.name == "Motion2RGB/PolyHaven PBR" && m.mainTexture == null),
                unsupportedShaders = materials.Count(m => m.shader == null || !m.shader.isSupported),
                routeChecked = checkRoute,
                graphicsDevice = SystemInfo.graphicsDeviceName
            };
            if (!checkRoute) return report;
            Physics.SyncTransforms();
            AutoDemoController auto = Object.FindObjectOfType<AutoDemoController>();
            if (auto == null || auto.patrolWaypoints == null || auto.patrolWaypoints.Length < 2)
                throw new InvalidOperationException("The campus route is missing.");
            // Exclude the player capsule while checking each route segment.
            CharacterController player = auto.GetComponent<CharacterController>();
            bool wasEnabled = player != null && player.enabled;
            if (player != null) player.enabled = false;
            try
            {
                for (int i = 0; i < auto.patrolWaypoints.Length; i++)
                {
                    Vector3 a = auto.patrolWaypoints[i].position;
                    Vector3 b = auto.patrolWaypoints[(i + 1) % auto.patrolWaypoints.Length].position;
                    if (Physics.SphereCast(a, .35f, (b - a).normalized, out RaycastHit hit, (b - a).magnitude,
                        ~0, QueryTriggerInteraction.Ignore))
                    {
                        report.routeObstructions++;
                        Debug.LogWarning("Campus route obstruction: " + hit.collider.name);
                    }
                }
            }
            finally { if (player != null) player.enabled = wasEnabled; }
            return report;
        }

        private static void CaptureView(string name, Vector3 position, Vector3 target, float fov)
        {
            GameObject go = new GameObject("Temporary Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
            Camera camera = go.AddComponent<Camera>();
            camera.transform.position = position;
            camera.transform.LookAt(target);
            camera.fieldOfView = fov;
            camera.aspect = 16f / 9;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 180;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = false;
            RenderTexture render = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            render.antiAliasing = 4;
            render.Create();
            camera.targetTexture = render;
            RenderTexture previous = RenderTexture.active;
            Texture2D screenshot = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                camera.Render();
                RenderTexture.active = render;
                screenshot.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                screenshot.Apply();
                File.WriteAllBytes("Previews/" + name + ".png", screenshot.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                render.Release();
                Object.DestroyImmediate(render);
                Object.DestroyImmediate(screenshot);
                Object.DestroyImmediate(go);
            }
        }
    }
}
