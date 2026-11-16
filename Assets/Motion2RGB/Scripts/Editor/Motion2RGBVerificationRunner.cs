using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Motion2RGB.EditorTools
{
    /// <summary>
    /// One-shot verification runner used to prove the shipped scene still plays
    /// and to dump frames plus telemetry for later comparison.
    /// Invoke with: Unity.exe -projectPath ... -executeMethod Motion2RGB.EditorTools.Motion2RGBVerificationRunner.Run
    /// </summary>
    public static class Motion2RGBVerificationRunner
    {
        private const string ScenePath = "Assets/Motion2RGB/Scenes/Motion2RGB_Robot.unity";
        private static readonly float[] CaptureTimes = { 3f, 7f, 12f, 17f };

        private static string outputDir;
        private static float startedAt;
        private static int captureIndex;
        private static bool playStarted;
        private static StringBuilder telemetry;
        private static bool savedDomainReloadValue;
        private static EnterPlayModeOptions savedDomainReloadOptions;

        public static void Run()
        {
            outputDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Motion2RGB_verify"));
            Directory.CreateDirectory(outputDir);
            telemetry = new StringBuilder();
            telemetry.AppendLine("time,speed,angularDeg,gaitHz,mode,exposureProxy,intensity,exposureSeconds,motionGain,depthLimit,camX,camY,camZ,camYaw");

            savedDomainReloadValue = EditorSettings.enterPlayModeOptionsEnabled;
            savedDomainReloadOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            AutoDemoController autoDemo = UnityEngine.Object.FindObjectOfType<AutoDemoController>();
            if (autoDemo != null) autoDemo.SetEnabled(true);

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
            Debug.Log("[Verify] play mode requested, output=" + outputDir);
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;

            if (!playStarted)
            {
                playStarted = true;
                startedAt = Time.realtimeSinceStartup;
                Debug.Log("[Verify] play mode entered");
                return;
            }

            float elapsed = Time.realtimeSinceStartup - startedAt;
            if (captureIndex < CaptureTimes.Length && elapsed >= CaptureTimes[captureIndex])
            {
                Capture(captureIndex, elapsed);
                captureIndex++;
                if (captureIndex >= CaptureTimes.Length) startedAt = Time.realtimeSinceStartup - CaptureTimes[CaptureTimes.Length - 1];
                return;
            }

            if (captureIndex >= CaptureTimes.Length && elapsed >= CaptureTimes[CaptureTimes.Length - 1] + 1.5f)
            {
                Finish(0);
            }
            else if (elapsed > 90f)
            {
                Debug.LogError("[Verify] timeout");
                Finish(1);
            }
        }

        private static void Capture(int index, float elapsed)
        {
            string framePath = Path.Combine(outputDir, string.Format(CultureInfo.InvariantCulture, "frame_{0:00}_{1:0.0}s.png", index, elapsed));
            ScreenCapture.CaptureScreenshot(framePath, 1);

            MotionStateTracker tracker = UnityEngine.Object.FindObjectOfType<MotionStateTracker>();
            DirectionalMotionBlur blur = UnityEngine.Object.FindObjectOfType<DirectionalMotionBlur>();
            Camera cam = blur != null ? blur.GetComponent<Camera>() : null;
            string mode = tracker != null ? tracker.CurrentMode.ToString() : "?";
            Vector3 p = cam != null ? cam.transform.position : Vector3.zero;
            float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;

            telemetry.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0:0.00},{1:0.000},{2:0.0},{3:0.000},{4},{5:0.00},{6:0.00},{7:0.000},{8:0.00},{9:0.0},{10:0.000},{11:0.000},{12:0.000},{13:0.0}",
                elapsed,
                tracker != null ? tracker.PlanarSpeed : 0f,
                tracker != null ? tracker.AngularVelocityDegPerSec : 0f,
                tracker != null ? tracker.GaitFrequencyHz : 0f,
                mode,
                blur != null ? blur.exposureProxy : 0f,
                blur != null ? blur.intensity : 0f,
                blur != null ? blur.maximumExposureSeconds : 0f,
                blur != null ? blur.motionGain : 0f,
                blur != null ? blur.motionDepthLimit : 0f,
                p.x, p.y, p.z, yaw));

            Debug.Log(string.Format(CultureInfo.InvariantCulture, "[Verify] frame {0} at {1:0.0}s speed={2:0.00} ang={3:0.0} mode={4}",
                index, elapsed, tracker != null ? tracker.PlanarSpeed : 0f,
                tracker != null ? tracker.AngularVelocityDegPerSec : 0f, mode));
        }

        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            try
            {
                File.WriteAllText(Path.Combine(outputDir, "telemetry.csv"), telemetry.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError("[Verify] telemetry write failed: " + e.Message);
            }

            EditorSettings.enterPlayModeOptionsEnabled = savedDomainReloadValue;
            EditorSettings.enterPlayModeOptions = savedDomainReloadOptions;
            Debug.Log("[Verify] finished with code " + code);
            EditorApplication.Exit(code);
        }
    }
}
