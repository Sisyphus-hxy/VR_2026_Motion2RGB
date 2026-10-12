using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.IO;

namespace Motion2RGB.Editor
{
    public static class Motion2RGBSceneBuilder
    {
        private const string Root = "Assets/Motion2RGB";
        private const string ScenePath = Root + "/Scenes/Motion2RGB_Demo.unity";
        private const string RenderTextureFolder = Root + "/RenderTextures";
        private const string MaterialFolder = Root + "/Materials";

        [MenuItem("Tools/Motion2RGB/Build Demo Scene")]
        public static void BuildDemoScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.WriteAllText("Assets/Motion2RGB/BUILD_ON_EDITOR_START.trigger", "deferred");
                Debug.Log("Motion2RGB scene build deferred until Play Mode ends.");
                return;
            }
            EnsureFolders();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLighting();
            BuildMaterialsAndGeometry();

            RenderTexture cleanTexture = CreateRenderTexture("CleanRGB", 960, 540);
            RenderTexture degradedTexture = CreateRenderTexture("DegradedRGB", 960, 540);
            BuildPlayer(cleanTexture, degradedTexture, out MotionStateTracker tracker, out FirstPersonController controller, out GaitMotionDriver gait, out CameraDegradationController degradation, out AutoDemoController autoDemo);
            BuildPatrolRoute(autoDemo);
            BuildUI(cleanTexture, degradedTexture, controller, tracker, gait, degradation, autoDemo);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = GameObject.Find("PlayerRig");
            Debug.Log("Motion2RGB demo scene built: " + ScenePath);
        }

        [MenuItem("Tools/Motion2RGB/Open Demo Scene")]
        public static void OpenDemoScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.WriteAllText("Assets/Motion2RGB/BUILD_ON_EDITOR_START.trigger", "deferred");
                return;
            }
            if (!System.IO.File.Exists(ScenePath)) BuildDemoScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static void EnsureFolders()
        {
            string[] folders = { Root, Root + "/Scenes", Root + "/Scripts", Root + "/Scripts/Runtime", Root + "/Scripts/Editor", Root + "/Shaders", RenderTextureFolder, MaterialFolder, Root + "/Exports", Root + "/Documentation" };
            foreach (string folder in folders)
            {
                string[] parts = folder.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }
        }

        private static RenderTexture CreateRenderTexture(string name, int width, int height)
        {
            string path = RenderTextureFolder + "/" + name + ".renderTexture";
            RenderTexture existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (existing != null && existing.format != RenderTextureFormat.ARGBHalf)
            {
                existing.Release();
                AssetDatabase.DeleteAsset(path);
                existing = null;
            }
            if (existing != null)
            {
                existing.Release();
                existing.width = width;
                existing.height = height;
                existing.depth = 24;
                existing.useMipMap = false;
                existing.autoGenerateMips = false;
                existing.filterMode = FilterMode.Bilinear;
                existing.wrapMode = TextureWrapMode.Clamp;
                existing.antiAliasing = 1;
                existing.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
                existing.Create();
                EditorUtility.SetDirty(existing);
                return existing;
            }
            RenderTexture texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, antiAliasing = 1, useMipMap = false, autoGenerateMips = false };
            texture.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            texture.Create();
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        private static void BuildLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.48f, 0.50f, 0.52f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.54f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.22f, 0.23f);
            RenderSettings.ambientIntensity = 1.25f;
            GameObject lightObject = new GameObject("Key Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = Color.white;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }

        private static void BuildMaterialsAndGeometry()
        {
            Material floor = MakeMaterial("Floor", new Color(0.12f, 0.16f, 0.2f), 0.15f);
            Material wall = MakeMaterial("Wall", new Color(0.55f, 0.59f, 0.64f), 0.05f);
            Material accent = MakeMaterial("Accent", new Color(0.05f, 0.55f, 0.85f), 0.2f);
            Material yellow = MakeMaterial("WarningYellow", new Color(0.95f, 0.55f, 0.04f), 0.1f);
            Material red = MakeMaterial("WarningRed", new Color(0.85f, 0.08f, 0.05f), 0.1f);
            Material green = MakeMaterial("GoalGreen", new Color(0.12f, 0.75f, 0.34f), 0.1f);

            CreateCube("Floor", new Vector3(0f, -0.15f, 5f), new Vector3(18f, 0.3f, 30f), floor);
            CreateCube("Left Corridor Wall", new Vector3(-9f, 2f, 5f), new Vector3(0.35f, 4f, 30f), wall);
            CreateCube("Right Corridor Wall", new Vector3(9f, 2f, 5f), new Vector3(0.35f, 4f, 30f), wall);
            CreateCube("Back Wall", new Vector3(0f, 2f, -10f), new Vector3(18f, 4f, 0.35f), wall);
            CreateCube("End Wall", new Vector3(0f, 2f, 20f), new Vector3(18f, 4f, 0.35f), wall);
            CreateCube("Turn Wall A", new Vector3(5.5f, 2f, 10f), new Vector3(7f, 4f, 0.35f), wall);
            CreateCube("Turn Wall B", new Vector3(2f, 2f, 13.5f), new Vector3(0.35f, 4f, 7f), wall);

            for (int i = 0; i < 16; i++)
            {
                float z = -7f + i * 1.7f;
                CreateCube("Floor Stripe " + i, new Vector3(0f, 0.01f, z), new Vector3(0.12f, 0.02f, 0.9f), accent);
            }

            CreateCube("Crate A", new Vector3(-4.5f, 0.65f, 1.5f), new Vector3(1.3f, 1.3f, 1.3f), yellow);
            CreateCube("Crate B", new Vector3(4.2f, 0.45f, 5.5f), new Vector3(0.9f, 0.9f, 0.9f), red);
            CreateCube("Cabinet", new Vector3(-5.4f, 1.25f, 9f), new Vector3(1.2f, 2.5f, 0.9f), accent);
            CreateCube("Table Top", new Vector3(3.4f, 1.25f, 2.8f), new Vector3(2.6f, 0.18f, 1.2f), wall);
            for (int i = 0; i < 4; i++) CreateCylinder("Table Leg", new Vector3(2.5f + (i % 2) * 1.7f, 0.6f, 2.25f + (i / 2) * 1.0f), 0.09f, 1.2f, red);
            for (int i = 0; i < 5; i++) CreateCube("Pillar " + i, new Vector3(-6f + i * 3f, 1.6f, -1f + i * 4f), new Vector3(0.35f, 3.2f, 0.35f), i % 2 == 0 ? accent : yellow);
            CreateCube("Goal Marker", new Vector3(0f, 1f, 17f), new Vector3(2f, 2f, 0.15f), green);

            // Additional landmarks make the route readable at a glance and give
            // the depth-aware effect foreground and background edges to resolve.
            for (int i = 0; i < 4; i++)
            {
                float z = -4f + i * 5.2f;
                CreateCube("Wall Panel L " + i, new Vector3(-8.72f, 1.65f, z), new Vector3(0.08f, 1.7f, 2.1f), i % 2 == 0 ? accent : yellow);
                CreateCube("Wall Panel R " + i, new Vector3(8.72f, 1.65f, z + 1.8f), new Vector3(0.08f, 1.7f, 2.1f), i % 2 == 0 ? yellow : accent);
            }
            for (int i = 0; i < 5; i++)
            {
                float z = -5f + i * 5.5f;
                CreateCube("Overhead Beam " + i, new Vector3(0f, 3.45f, z), new Vector3(8.2f, 0.18f, 0.28f), wall);
                CreateCube("Route Beacon " + i, new Vector3(0f, 2.95f, z + 1.25f), new Vector3(0.7f, 0.08f, 0.08f), i % 2 == 0 ? green : accent);
            }
            for (int i = 0; i < 3; i++)
            {
                CreateCylinder("Safety Cone " + i, new Vector3(-3.8f + i * 3.8f, 0.35f, 7f + i * 2.0f), 0.28f, 0.7f, yellow);
            }
        }

        private static void BuildPatrolRoute(AutoDemoController autoDemo)
        {
            if (autoDemo == null) return;
            GameObject routeRoot = new GameObject("Motion2RGB Cruise Route");
            Vector3 origin = new Vector3(0f, 1.05f, -7f);
            Vector3[] positions =
            {
                origin,
                origin + new Vector3(0f, 0f, 9.5f),
                origin + new Vector3(4.8f, 0f, 9.5f),
                origin + new Vector3(4.8f, 0f, 4.0f),
                origin + new Vector3(-1.0f, 0f, 4.0f),
                origin + new Vector3(-1.0f, 0f, 0.0f)
            };
            Transform[] points = new Transform[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject point = new GameObject("Waypoint " + i);
                point.transform.SetParent(routeRoot.transform);
                point.transform.position = positions[i];
                points[i] = point.transform;
            }
            autoDemo.loopDuration = 24f;
            autoDemo.ConfigureRoute(points);
        }

        private static void BuildPlayer(RenderTexture cleanTexture, RenderTexture degradedTexture, out MotionStateTracker tracker, out FirstPersonController controller, out GaitMotionDriver gait, out CameraDegradationController degradation, out AutoDemoController autoDemo)
        {
            GameObject rig = new GameObject("PlayerRig");
            rig.transform.SetPositionAndRotation(new Vector3(0f, 1.05f, -7f), Quaternion.identity);
            CharacterController characterController = rig.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.3f;
            characterController.center = new Vector3(0f, -0.15f, 0f);
            characterController.stepOffset = 0.35f;
            tracker = rig.AddComponent<MotionStateTracker>();
            controller = rig.AddComponent<FirstPersonController>();
            autoDemo = rig.AddComponent<AutoDemoController>();

            GameObject cleanObject = new GameObject("CleanCamera");
            cleanObject.transform.SetParent(rig.transform, false);
            cleanObject.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            Camera clean = cleanObject.AddComponent<Camera>();
            ConfigureCamera(clean, cleanTexture);

            GameObject degradedObject = new GameObject("DegradedCamera");
            degradedObject.transform.SetParent(rig.transform, false);
            degradedObject.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            Camera degraded = degradedObject.AddComponent<Camera>();
            ConfigureCamera(degraded, degradedTexture);
            gait = degradedObject.AddComponent<GaitMotionDriver>();
            gait.tracker = tracker;
            DirectionalMotionBlur blur = degradedObject.AddComponent<DirectionalMotionBlur>();
            blur.tracker = tracker;
            degradation = degradedObject.AddComponent<CameraDegradationController>();
            degradation.tracker = tracker;
            degradation.gaitDriver = gait;
            degradation.blur = blur;
            degradation.cleanCamera = clean;
            controller.cleanCamera = clean;
            controller.degradedCamera = degraded;
            autoDemo.firstPersonController = controller;

            GameObject displayObject = new GameObject("DisplayCamera");
            Camera display = displayObject.AddComponent<Camera>();
            display.clearFlags = CameraClearFlags.SolidColor;
            display.backgroundColor = Color.black;
            display.cullingMask = 0;
            display.depth = -100f;
            display.stereoTargetEye = StereoTargetEyeMask.None;
        }

        private static void BuildUI(RenderTexture cleanTexture, RenderTexture degradedTexture, FirstPersonController controller, MotionStateTracker tracker, GaitMotionDriver gait, CameraDegradationController degradation, AutoDemoController autoDemo)
        {
            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Canvas canvasObject = new GameObject("Motion2RGB UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvasObject.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.sortingOrder = 10;
            canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            Transform canvas = canvasObject.transform;

            RawImage cleanImage = CreateRawImage("Clean RGB", canvas, cleanTexture, new Vector2(0f, 0.25f), new Vector2(0.5f, 1f));
            RawImage degradedImage = CreateRawImage("Degraded RGB", canvas, degradedTexture, new Vector2(0.5f, 0.25f), new Vector2(1f, 1f));
            CreateLabel("CLEAN RGB", canvas, new Vector2(0.015f, 0.92f), new Vector2(0.25f, 0.98f), 26, Color.white);
            CreateLabel("DEGRADED RGB", canvas, new Vector2(0.515f, 0.92f), new Vector2(0.82f, 0.98f), 26, new Color(1f, 0.78f, 0.35f));
            Image panel = CreatePanel("Controls", canvas, new Vector2(0f, 0f), new Vector2(1f, 0.24f), new Color(0.025f, 0.035f, 0.05f, 0.94f));
            Transform panelTransform = panel.transform;
            Text stats = CreateLabel("Stats", panelTransform, new Vector2(0.72f, 0.05f), new Vector2(0.99f, 0.30f), 17, new Color(0.65f, 0.9f, 1f));
            Text hint = CreateLabel("Hint", canvas, new Vector2(0.015f, 0.24f), new Vector2(0.98f, 0.285f), 17, new Color(0.72f, 0.78f, 0.86f));
            CreateLabel("SPEED / ANGULAR / GAIT", panelTransform, new Vector2(0.72f, 0.80f), new Vector2(0.99f, 0.96f), 12, new Color(0.7f, 0.78f, 0.86f));
            CreatePanel("Telemetry Graph Background", panelTransform, new Vector2(0.72f, 0.32f), new Vector2(0.99f, 0.79f), new Color(0.06f, 0.08f, 0.12f, 0.95f));
            CreateTelemetryGraph(panelTransform, tracker, new Vector2(0.72f, 0.32f), new Vector2(0.99f, 0.79f));

            Slider walkingSlider = CreateSlider("Walking Speed", panelTransform, new Vector2(0.02f, 0.62f), new Vector2(0.22f, 0.80f), 0.2f, 4f, controller.walkingSpeed);
            Slider stride = CreateSlider("Stride Length", panelTransform, new Vector2(0.25f, 0.62f), new Vector2(0.45f, 0.80f), 0.3f, 1.4f, tracker.strideLength);
            Slider amp = CreateSlider("Gait Amplitude", panelTransform, new Vector2(0.48f, 0.62f), new Vector2(0.70f, 0.80f), 0f, 0.2f, gait.verticalAmplitude);
            Slider blur = CreateSlider("Blur Intensity", panelTransform, new Vector2(0.02f, 0.31f), new Vector2(0.22f, 0.49f), 0f, 1f, degradation.blurIntensity);
            Slider exposure = CreateSlider("Exposure Proxy", panelTransform, new Vector2(0.25f, 0.31f), new Vector2(0.45f, 0.49f), 0f, 1f, degradation.exposureProxy);
            CreateLabel("Exposure Proxy is a simplified degradation control, not calibrated shutter time.", panelTransform, new Vector2(0.48f, 0.32f), new Vector2(0.75f, 0.48f), 13, new Color(0.65f, 0.68f, 0.75f));

            Toggle blurToggle = CreateToggle("Motion Blur", panelTransform, new Vector2(0.48f, 0.17f), new Vector2(0.60f, 0.29f), true);
            Toggle gaitToggle = CreateToggle("Gait Motion", panelTransform, new Vector2(0.60f, 0.17f), new Vector2(0.72f, 0.29f), true);
            Toggle samePose = CreateToggle("Same Pose", panelTransform, new Vector2(0.72f, 0.17f), new Vector2(0.82f, 0.29f), false);
            Toggle autoToggle = CreateToggle("Auto Demo", panelTransform, new Vector2(0.82f, 0.17f), new Vector2(0.94f, 0.29f), false);

            Button stationary = CreateButton("IDLE", panelTransform, new Vector2(0.02f, 0.08f), new Vector2(0.10f, 0.23f), new Color(0.18f, 0.25f, 0.35f));
            Button walkingButton = CreateButton("WALK", panelTransform, new Vector2(0.11f, 0.08f), new Vector2(0.19f, 0.23f), new Color(0.08f, 0.35f, 0.3f));
            Button running = CreateButton("RUN", panelTransform, new Vector2(0.20f, 0.08f), new Vector2(0.28f, 0.23f), new Color(0.5f, 0.22f, 0.08f));
            Button reset = CreateButton("RESET", panelTransform, new Vector2(0.29f, 0.08f), new Vector2(0.38f, 0.23f), new Color(0.25f, 0.25f, 0.28f));
            Button shot = CreateButton("SCREENSHOT", panelTransform, new Vector2(0.39f, 0.08f), new Vector2(0.48f, 0.23f), new Color(0.18f, 0.25f, 0.35f));
            Button modeButton = CreateButton("MODE  DEPTH BLUR", panelTransform, new Vector2(0.50f, 0.08f), new Vector2(0.62f, 0.23f), new Color(0.14f, 0.3f, 0.38f));

            DemoUIController ui = canvasObject.gameObject.AddComponent<DemoUIController>();
            ui.controller = controller; ui.tracker = tracker; ui.gait = gait; ui.degradation = degradation; ui.autoDemo = autoDemo;
            ui.walkingSpeedSlider = walkingSlider; ui.strideSlider = stride; ui.gaitAmplitudeSlider = amp; ui.blurSlider = blur; ui.exposureSlider = exposure;
            ui.blurToggle = blurToggle; ui.gaitToggle = gaitToggle; ui.samePoseToggle = samePose; ui.autoDemoToggle = autoToggle;
            ui.statsText = stats; ui.hintText = hint; ui.stationaryButton = stationary; ui.walkingButton = walkingButton; ui.runningButton = running; ui.resetButton = reset; ui.screenshotButton = shot;
            ui.degradationModeButton = modeButton;
        }

        private static MotionTelemetryGraph CreateTelemetryGraph(Transform parent, MotionStateTracker tracker, Vector2 min, Vector2 max)
        {
            GameObject graphObject = new GameObject("Motion Telemetry Graph", typeof(RectTransform), typeof(MotionTelemetryGraph));
            graphObject.transform.SetParent(parent, false);
            RectTransform rect = graphObject.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);
            MotionTelemetryGraph graph = graphObject.GetComponent<MotionTelemetryGraph>();
            graph.tracker = tracker;
            graph.raycastTarget = false;
            graph.color = Color.white;
            return graph;
        }

        private static Camera ConfigureCamera(Camera camera, RenderTexture texture)
        {
            camera.targetTexture = texture;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.fieldOfView = 72f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;
            camera.depth = -1f;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            camera.forceIntoRenderTexture = true;
            return camera;
        }

        private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.position = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }

        private static GameObject CreateCylinder(string name, Vector3 position, float radius, float height, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name; go.transform.position = position; go.transform.localScale = new Vector3(radius, height * 0.5f, radius); go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }

        private static Material MakeMaterial(string name, Color color, float metallic)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color; material.SetFloat("_Metallic", metallic); material.SetFloat("_Glossiness", 0.35f); return material;
        }

        private static Image CreatePanel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; go.GetComponent<Image>().color = color; return go.GetComponent<Image>();
        }

        private static RawImage CreateRawImage(string name, Transform parent, RenderTexture texture, Vector2 min, Vector2 max)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage)); go.transform.SetParent(parent, false); RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = new Vector2(8f, 8f); rect.offsetMax = new Vector2(-8f, -8f); RawImage image = go.GetComponent<RawImage>(); image.texture = texture; image.color = Color.white; return image;
        }

        private static Text CreateLabel(string text, Transform parent, Vector2 min, Vector2 max, int fontSize, Color color)
        {
            GameObject go = new GameObject(text, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false); RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; Text label = go.GetComponent<Text>(); label.text = text; label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = fontSize; label.color = color; label.alignment = TextAnchor.MiddleLeft; label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow; return label;
        }

        private static Slider CreateSlider(string label, Transform parent, Vector2 min, Vector2 max, float minValue, float maxValue, float value)
        {
            float splitY = Mathf.Lerp(min.y, max.y, 0.55f);
            CreateLabel(label, parent, new Vector2(min.x, splitY), max, 15, Color.white);
            GameObject go = new GameObject(label + " Slider", typeof(RectTransform), typeof(Slider)); go.transform.SetParent(parent, false); RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = new Vector2(max.x, splitY); rect.offsetMin = new Vector2(0f, 2f); rect.offsetMax = new Vector2(0f, -2f);
            Slider slider = go.GetComponent<Slider>(); slider.minValue = minValue; slider.maxValue = maxValue; slider.value = value; slider.direction = Slider.Direction.LeftToRight;
            Image background = CreatePanel("Background", go.transform, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f), new Color(0.15f, 0.2f, 0.28f));
            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image)); fillGo.transform.SetParent(go.transform, false); RectTransform fillRect = fillGo.GetComponent<RectTransform>(); fillRect.anchorMin = new Vector2(0f, 0.25f); fillRect.anchorMax = new Vector2(1f, 0.75f); fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero; fillGo.GetComponent<Image>().color = new Color(0.2f, 0.7f, 0.95f); slider.fillRect = fillRect;
            GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image)); handleGo.transform.SetParent(go.transform, false); RectTransform handleRect = handleGo.GetComponent<RectTransform>(); handleRect.sizeDelta = new Vector2(18f, 26f); handleGo.GetComponent<Image>().color = Color.white; slider.handleRect = handleRect; slider.targetGraphic = handleGo.GetComponent<Image>(); return slider;
        }

        private static Toggle CreateToggle(string label, Transform parent, Vector2 min, Vector2 max, bool value)
        {
            GameObject go = new GameObject(label, typeof(RectTransform), typeof(Toggle)); go.transform.SetParent(parent, false); RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            Toggle toggle = go.GetComponent<Toggle>(); toggle.isOn = value;
            GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image)); box.transform.SetParent(go.transform, false); RectTransform boxRect = box.GetComponent<RectTransform>(); boxRect.anchorMin = new Vector2(0f, 0.25f); boxRect.anchorMax = new Vector2(0.16f, 0.75f); boxRect.offsetMin = Vector2.zero; boxRect.offsetMax = Vector2.zero; Image boxImage = box.GetComponent<Image>(); boxImage.color = new Color(0.2f, 0.7f, 0.95f); toggle.targetGraphic = boxImage; toggle.graphic = boxImage;
            CreateLabel(label, go.transform, new Vector2(0.2f, 0f), new Vector2(1f, 1f), 14, Color.white); return toggle;
        }

        private static Button CreateButton(string label, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            GameObject go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false); RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = new Vector2(2f, 2f); rect.offsetMax = new Vector2(-2f, -2f); go.GetComponent<Image>().color = color; Button button = go.GetComponent<Button>(); Text text = CreateLabel(label, go.transform, Vector2.zero, Vector2.one, 14, Color.white); text.alignment = TextAnchor.MiddleCenter; return button;
        }
    }
}
