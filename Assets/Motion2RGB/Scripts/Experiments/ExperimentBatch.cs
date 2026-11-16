using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Motion2RGB.Experiments
{
    [Serializable]
    public class ExperimentCondition
    {
        public string label = "cond";
        public string profile = "linear";   // static | linear | arc | pan | stopgo
        public float speed = 1.5f;          // m/s
        public float angularDeg = 0f;       // deg/s
        public float exposureProxy = 0.65f;
        public float blurIntensity = 0.9f;
        public float gaitAmplitude = 0.08f;
        public float strideLength = 0.7f;
        public bool samePose = false;
        public int degradationMode = 0;     // 0 depth blur, 1 noise, 2 res drop, 3 chromatic
        public float targetDepth = 0f;      // metres; 0 = no calibration target
        public float targetYawDeg = 20f;    // horizontal angle from the camera axis
    }

    [Serializable]
    public class ExperimentConfig
    {
        public string name = "run";
        public int fps = 60;
        public int width = 1280;
        public int height = 720;
        public int warmupFrames = 60;
        public int captureFrames = 12;
        public string imageFormat = "png";
        public bool asyncEncode = true;
        public string outputDir = "";
        public bool createTarget = false;
        public float targetSizeFactor = 0.39f;  // board width / distance, keeps angular size constant
        public ExperimentCondition[] conditions = new ExperimentCondition[0];
    }

    /// <summary>
    /// Deterministic capture harness.
    /// The trajectory is integrated with a fixed step (1/fps) driven by frame count,
    /// never by Time.deltaTime, so the recorded pose sequence is identical on every run.
    /// Time.captureFramerate pins deltaTime to the same 1/fps so the gait driver,
    /// motion tracker and blur shader see a constant frame interval as well.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class ExperimentBatch : MonoBehaviour
    {
        public string configPath;

        private ExperimentConfig config;
        private ExperimentCondition condition;
        private MotionStateTracker tracker;
        private DirectionalMotionBlur blur;
        private CameraDegradationController degradation;
        private GaitMotionDriver gait;
        private FirstPersonController controller;
        private AutoDemoController autoDemo;
        private Camera cleanCamera;
        private Camera degradedCamera;
        private RenderTexture cleanRT;
        private RenderTexture degradedRT;
        private Transform rig;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private GameObject targetObject;
        private Vector3 targetCenter;
        private bool targetActive;
        private Vector3 eyeOffset;

        private int conditionIndex = -1;
        private int warmupLeft;
        private int captureLeft;
        private int captureIndex;
        private bool captureThisFrame;
        private bool finished;
        private float dt;
        private Vector3 pose;
        private float yOffset;
        private float lastSpeed;
        private float lastOmega;
        private BlockingCollection<WriteJob> writeQueue;
        private Thread writerThread;
        private float sessionStart = -1f;
        private int sessionFrames;
        private double saveSeconds;
        private int savedFrames;
        private Texture2D cleanReadback;
        private Texture2D degradedReadback;
        private float yaw;
        private float simTime;
        private string runDir;
        private string conditionDir;
        private string donePath;

        private void Awake()
        {
            config = JsonUtility.FromJson<ExperimentConfig>(File.ReadAllText(configPath));
            if (config.conditions == null || config.conditions.Length == 0)
            {
                Debug.LogError("[Exp] config has no conditions");
                Finish(1);
                return;
            }

            dt = 1f / Mathf.Max(1, config.fps);
            Time.captureFramerate = Mathf.Max(1, config.fps);
            // Editors throttle the player loop when the window is not focused, which used to
            // dominate capture time (~430 ms/frame) far more than rendering or PNG encoding.
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;

            tracker = FindObjectOfType<MotionStateTracker>();
            blur = FindObjectOfType<DirectionalMotionBlur>();
            degradation = FindObjectOfType<CameraDegradationController>();
            gait = FindObjectOfType<GaitMotionDriver>();
            controller = FindObjectOfType<FirstPersonController>();
            autoDemo = FindObjectOfType<AutoDemoController>();

            if (degradation != null)
            {
                cleanCamera = degradation.cleanCamera;
                degradedCamera = blur != null ? blur.GetComponent<Camera>() : null;
            }

            if (controller != null)
            {
                controller.SetManualInput(false);
                CharacterController cc = controller.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
            }
            if (autoDemo != null) autoDemo.SetEnabled(false);

            string root = config.outputDir;
            if (string.IsNullOrEmpty(root)) root = Path.Combine(Application.dataPath, "..", "..", "Motion2RGB_verify", "experiments");
            root = Path.GetFullPath(root);
            runDir = Path.Combine(root, config.name);
            Directory.CreateDirectory(runDir);
            donePath = configPath + ".done";

            rig = tracker != null ? tracker.transform : (controller != null ? controller.transform : transform);
            startPosition = rig.position;
            startRotation = rig.rotation;
            pose = startPosition;

            Camera eye = degradedCamera != null ? degradedCamera : cleanCamera;
            eyeOffset = eye != null ? eye.transform.position - startPosition : new Vector3(0f, 1.65f, 0f);

            if (config.createTarget) CreateTarget();
            SetUpTargets();
            if (config.asyncEncode) StartWriter();
            StartCoroutine(CaptureLoop());
            BeginNextCondition();
        }

        private void CreateTarget()
        {
            targetObject = new GameObject("ExpCalibrationTarget");
            Mesh mesh = BuildStepEdgeQuad();
            targetObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = targetObject.AddComponent<MeshRenderer>();
            Shader shader = Shader.Find("Motion2RGB/Experiments/CalibrationBoard");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            Material material = new Material(shader);
            material.mainTexture = BuildStepEdgeTexture(512);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            targetObject.SetActive(false);
        }

        private static Texture2D BuildStepEdgeTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color light = new Color(0.92f, 0.92f, 0.92f);
            Color dark = new Color(0.04f, 0.04f, 0.04f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Single vertical step edge down the middle of the board.
                    tex.SetPixel(x, y, x < size / 2 ? light : dark);
                }
            }
            tex.Apply();
            return tex;
        }

        private static Mesh BuildStepEdgeQuad()
        {
            var mesh = new Mesh { name = "ExpCalibrationQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            // Two windings so the board is visible from either side.
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void GetMotion(ExperimentCondition c, float t, out float speed, out float angularDeg)
        {
            speed = 0f;
            angularDeg = 0f;
            switch (c.profile)
            {
                case "static":
                    break;
                case "linear":
                    speed = c.speed;
                    break;
                case "pan":
                    angularDeg = c.angularDeg;
                    break;
                case "arc":
                    speed = c.speed;
                    angularDeg = c.angularDeg;
                    break;
                case "stopgo":
                    speed = StopGoSpeed(t, c.speed);
                    break;
                case "combo":
                    ComboMotion(t, out speed, out angularDeg);
                    break;
                default:
                    speed = c.speed;
                    break;
            }
        }

        /// <summary>Integrates the same trajectory the capture will use, without touching the scene.</summary>
        private void PredictPose(int steps, out Vector3 position, out float predictedYaw)
        {
            position = startPosition;
            predictedYaw = 0f;
            float t = 0f;
            for (int i = 0; i < steps; i++)
            {
                GetMotion(condition, t, out float speed, out float angularDeg);
                t += dt;
                predictedYaw += angularDeg * dt;
                position += Quaternion.Euler(0f, predictedYaw, 0f) * Vector3.forward * (speed * dt);
            }
        }

        private void PlaceTarget()
        {
            if (targetObject == null) return;
            targetActive = condition.targetDepth > 0.01f;
            if (!targetActive)
            {
                targetObject.SetActive(false);
                return;
            }

            PredictPose(Mathf.Max(0, config.warmupFrames) + 1, out Vector3 cameraPos, out float cameraYaw);
            Vector3 eye = cameraPos + eyeOffset;
            Vector3 direction = Quaternion.Euler(0f, cameraYaw + condition.targetYawDeg, 0f) * Vector3.forward;
            targetCenter = eye + direction * condition.targetDepth;

            float size = Mathf.Max(0.05f, config.targetSizeFactor * condition.targetDepth);
            targetObject.transform.position = targetCenter;
            targetObject.transform.rotation = Quaternion.LookRotation(eye - targetCenter, Vector3.up);
            targetObject.transform.localScale = new Vector3(size, size, 1f);
            targetObject.SetActive(true);
        }

        private void SetUpTargets()
        {
            if (cleanCamera != null)
            {
                cleanRT = new RenderTexture(config.width, config.height, 24, RenderTextureFormat.ARGB32);
                cleanRT.name = "ExpCleanRT";
                cleanCamera.targetTexture = cleanRT;
            }
            if (degradedCamera != null)
            {
                degradedRT = new RenderTexture(config.width, config.height, 24, RenderTextureFormat.ARGB32);
                degradedRT.name = "ExpDegradedRT";
                degradedCamera.targetTexture = degradedRT;
                degradedCamera.depthTextureMode |= DepthTextureMode.Depth;
            }
        }

        private void Update()
        {
            if (finished || config == null) return;
            if (sessionStart < 0f) sessionStart = Time.realtimeSinceStartup;
            sessionFrames++;

            if (warmupLeft > 0)
            {
                StepPose();
                warmupLeft--;
                return;
            }

            if (captureLeft > 0)
            {
                StepPose();
                captureThisFrame = true;
                captureLeft--;
                return;
            }

            if (conditionIndex < config.conditions.Length)
            {
                BeginNextCondition();
            }
        }

        private void StepPose()
        {
            GetMotion(condition, simTime, out float speed, out float angularDeg);

            yOffset = condition.profile == "combo" ? ComboJump(simTime) : 0f;
            lastSpeed = speed;
            lastOmega = angularDeg;

            simTime += dt;
            yaw += angularDeg * dt;
            pose += Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * (speed * dt);
            Vector3 withHop = pose;
            withHop.y = startPosition.y + yOffset;
            rig.SetPositionAndRotation(withHop, Quaternion.Euler(0f, yaw, 0f) * startRotation);
        }

        /// <summary>One continuous take: walk, accelerate, three hops, brake into a turn, stop.</summary>
        private static void ComboMotion(float t, out float speed, out float omega)
        {
            if (t < 1.2f) { speed = 1.5f * (t / 1.2f); omega = 0f; }
            else if (t < 2.2f) { speed = 1.5f; omega = 0f; }
            else if (t < 2.8f) { speed = Mathf.Lerp(1.5f, 0f, (t - 2.2f) / 0.6f); omega = 0f; }
            else if (t < 4.2f) { speed = 0f; omega = 60f; }
            else { speed = 0f; omega = 20f; }
        }

        /// <summary>Ballistic hops, same shape the eye expects from a running robot.</summary>
        private static float ComboJump(float t)
        {
            float[] starts = { 4.4f, 5.0f, 5.6f };
            const float duration = 0.45f;
            const float height = 0.28f;
            foreach (float s in starts)
            {
                if (t >= s && t <= s + duration)
                {
                    float u = (t - s) / duration;
                    return 4f * height * u * (1f - u);
                }
            }
            return 0f;
        }

        private static float StopGoSpeed(float t, float target)
        {
            const float accel = 1.2f;
            const float cruiseSeconds = 1f;
            float ramp = target / accel;
            if (t < ramp) return accel * t;
            if (t < ramp + cruiseSeconds) return target;
            return Mathf.Max(0f, target - accel * (t - ramp - cruiseSeconds));
        }

        private void BeginNextCondition()
        {
            conditionIndex++;
            if (conditionIndex >= config.conditions.Length)
            {
                Finish(0);
                return;
            }

            condition = config.conditions[conditionIndex];
            string safeLabel = string.IsNullOrEmpty(condition.label) ? "cond" + conditionIndex : condition.label;
            foreach (char c in Path.GetInvalidFileNameChars()) safeLabel = safeLabel.Replace(c, '_');
            conditionDir = Path.Combine(runDir, string.Format(CultureInfo.InvariantCulture, "{0:00}_{1}", conditionIndex, safeLabel));
            Directory.CreateDirectory(conditionDir);

            degradation.blurIntensity = condition.blurIntensity;
            degradation.exposureProxy = condition.exposureProxy;
            degradation.samePoseComparison = condition.samePose;
            degradation.SetMode((DegradationMode)condition.degradationMode);
            degradation.SetBlurEnabled(true);
            degradation.SetGaitEnabled(true);

            if (gait != null)
            {
                gait.SetAmplitude(condition.gaitAmplitude);
                gait.gaitPerturbationEnabled = true;
            }
            if (tracker != null) tracker.strideLength = Mathf.Max(0.1f, condition.strideLength);

            pose = startPosition;
            yaw = 0f;
            simTime = 0f;
            captureIndex = 0;
            captureLeft = Mathf.Max(1, config.captureFrames);
            warmupLeft = Mathf.Max(0, config.warmupFrames);

            rig.SetPositionAndRotation(startPosition, startRotation);
            if (tracker != null) tracker.ResetMeasurement();
            if (blur != null) blur.ResetHistory();
            PlaceTarget();

            Debug.Log("[Exp] condition " + conditionIndex + " " + safeLabel + " profile=" + condition.profile);
        }

        private IEnumerator CaptureLoop()
        {
            while (!finished)
            {
                yield return new WaitForEndOfFrame();
                if (captureThisFrame && !finished)
                {
                    captureThisFrame = false;
                    CaptureFrame();
                }
            }
        }

        private void CaptureFrame()
        {
            if (cleanRT == null || degradedRT == null) return;
            string stem = string.Format(CultureInfo.InvariantCulture, "{0:0000}", captureIndex);
            Save(cleanRT, Path.Combine(conditionDir, "clean_" + stem));
            Save(degradedRT, Path.Combine(conditionDir, "degraded_" + stem));

            float exposureSeconds = degradation != null && blur != null
                ? Mathf.Lerp(0.005f, blur.maximumExposureSeconds, degradation.exposureProxy) * degradation.blurIntensity
                : 0f;

            Vector3 boardScreen = Vector3.zero;
            float boardDepth = 0f;
            if (targetActive && degradedCamera != null)
            {
                boardScreen = degradedCamera.WorldToScreenPoint(targetCenter);
                boardDepth = Vector3.Distance(degradedCamera.transform.position, targetCenter);
            }

            var row = new FrameRow
            {
                condition = conditionIndex,
                label = condition.label,
                profile = condition.profile,
                frame = captureIndex,
                t = simTime,
                v = lastSpeed,
                omegaDeg = lastOmega,
                poseX = pose.x,
                poseY = pose.y,
                poseZ = pose.z,
                yaw = yaw,
                exposureProxy = degradation != null ? degradation.exposureProxy : 0f,
                exposureSeconds = exposureSeconds,
                blurIntensity = degradation != null ? degradation.blurIntensity : 0f,
                motionGain = blur != null ? blur.motionGain : 0f,
                depthLimit = blur != null ? blur.motionDepthLimit : 0f,
                samePose = condition.samePose,
                trackerSpeed = tracker != null ? tracker.PlanarSpeed : 0f,
                trackerAngularDeg = tracker != null ? tracker.AngularVelocityDegPerSec : 0f,
                gaitHz = tracker != null ? tracker.GaitFrequencyHz : 0f,
                gaitPhase = gait != null ? gait.Phase : 0f,
                yOffset = yOffset,
                boardU = boardScreen.x,
                boardV = boardScreen.y,
                boardDepth = boardDepth,
                boardVisible = targetActive && boardScreen.z > 0f
            };

            File.AppendAllText(Path.Combine(conditionDir, "frames.jsonl"), JsonUtility.ToJson(row) + "\n");
            captureIndex++;
        }

        private class WriteJob
        {
            public byte[] raw;
            public string path;
            public int width;
            public int height;
        }

        private void StartWriter()
        {
            writeQueue = new BlockingCollection<WriteJob>(64);
            writerThread = new Thread(() =>
            {
                foreach (WriteJob job in writeQueue.GetConsumingEnumerable())
                {
                    try
                    {
                        byte[] bytes = ImageConversion.EncodeArrayToPNG(job.raw,
                            UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,
                            (uint)job.width, (uint)job.height);
                        File.WriteAllBytes(job.path, bytes);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("[Exp] writer failed: " + e.Message);
                    }
                }
            })
            { IsBackground = true, Name = "Motion2RGB-Writer" };
            writerThread.Start();
        }

        private void Save(RenderTexture rt, string pathNoExtension)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try { SaveInner(rt, pathNoExtension); }
            finally { saveSeconds += sw.Elapsed.TotalSeconds; savedFrames++; }
        }

        private void SaveInner(RenderTexture rt, string pathNoExtension)
        {
            string ext = string.Equals(config.imageFormat, "jpg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : ".png";
            string path = pathNoExtension + ext;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;

            if (config.asyncEncode && writeQueue != null && ext == ".png")
            {
                var job = new WriteJob
                {
                    raw = tex.GetRawTextureData<byte>().ToArray(),
                    path = path,
                    width = rt.width,
                    height = rt.height
                };
                writeQueue.Add(job);
            }
            else
            {
                byte[] bytes = ext == ".jpg" ? tex.EncodeToJPG(95) : tex.EncodeToPNG();
                File.WriteAllBytes(path, bytes);
            }
            Destroy(tex);
        }

        private void FlushWriter()
        {
            if (writeQueue == null) return;
            writeQueue.CompleteAdding();
            if (writerThread != null) writerThread.Join(20000);
            writeQueue.Dispose();
            writeQueue = null;
        }

        private void Finish(int code)
        {
            if (finished) return;
            finished = true;
            StopAllCoroutines();
            Time.captureFramerate = 0;
            FlushWriter();
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Exp] capture timing: {0:F1} ms per image ({1} images)", 1000.0 * saveSeconds / Mathf.Max(1, savedFrames), savedFrames));
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Exp] session: {0:F1} s for {1} frames = {2:F1} ms/frame",
                Time.realtimeSinceStartup - sessionStart, sessionFrames,
                1000.0 * (Time.realtimeSinceStartup - sessionStart) / Mathf.Max(1, sessionFrames)));
            try
            {
                File.WriteAllText(Path.Combine(runDir, "summary.json"), JsonUtility.ToJson(new Summary
                {
                    name = config.name,
                    conditions = conditionIndex,
                    fps = config.fps,
                    width = config.width,
                    height = config.height,
                    warmupFrames = config.warmupFrames,
                    captureFrames = config.captureFrames,
                    exitCode = code
                }, true));
                File.WriteAllText(donePath, code.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception e)
            {
                Debug.LogError("[Exp] finish write failed: " + e.Message);
            }
            Debug.Log("[Exp] finished code=" + code);
        }

        [Serializable]
        private class FrameRow
        {
            public int condition;
            public string label;
            public string profile;
            public int frame;
            public float t;
            public float v;
            public float omegaDeg;
            public float poseX;
            public float poseY;
            public float poseZ;
            public float yaw;
            public float exposureProxy;
            public float exposureSeconds;
            public float blurIntensity;
            public float motionGain;
            public float depthLimit;
            public bool samePose;
            public float trackerSpeed;
            public float trackerAngularDeg;
            public float gaitHz;
            public float gaitPhase;
            public float yOffset;
            public float boardU;
            public float boardV;
            public float boardDepth;
            public bool boardVisible;
        }

        [Serializable]
        private class Summary
        {
            public string name;
            public int conditions;
            public int fps;
            public int width;
            public int height;
            public int warmupFrames;
            public int captureFrames;
            public int exitCode;
        }
    }
}












