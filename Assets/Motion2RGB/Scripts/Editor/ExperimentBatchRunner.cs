using System;
using System.IO;
using Motion2RGB.Experiments;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Motion2RGB.EditorTools
{
    /// <summary>
    /// Headless entry point for the deterministic capture harness.
    /// Unity.exe -projectPath &lt;project&gt; -executeMethod Motion2RGB.EditorTools.ExperimentBatchRunner.Run
    /// with the environment variable M2R_CONFIG pointing at the run config JSON.
    /// </summary>
    public static class ExperimentBatchRunner
    {
        private const string ScenePath = "Assets/Motion2RGB/Scenes/Motion2RGB_Robot.unity";
        private const double TimeoutSeconds = 300.0;

        private static string donePath;
        private static DateTime startedAt;

        public static void Run()
        {
            string configPath = Environment.GetEnvironmentVariable("M2R_CONFIG");
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                Debug.LogError("[Exp] M2R_CONFIG missing or not found: " + configPath);
                EditorApplication.Exit(2);
                return;
            }

            configPath = Path.GetFullPath(configPath);
            donePath = configPath + ".done";
            if (File.Exists(donePath)) File.Delete(donePath);

            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            string scenePath = Environment.GetEnvironmentVariable("M2R_SCENE");
            if (string.IsNullOrEmpty(scenePath)) scenePath = ScenePath;

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            GameObject host = new GameObject("ExperimentBatch");
            ExperimentBatch batch = host.AddComponent<ExperimentBatch>();
            batch.configPath = configPath;

            startedAt = DateTime.UtcNow;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
            Debug.Log("[Exp] started, config=" + configPath);
        }

        private static void Tick()
        {
            if (File.Exists(donePath))
            {
                int code = 0;
                try { code = int.Parse(File.ReadAllText(donePath).Trim()); } catch { }
                EditorApplication.update -= Tick;
                Time.captureFramerate = 0;
                Debug.Log("[Exp] done marker found, exit=" + code);
                EditorApplication.Exit(code);
                return;
            }

            if ((DateTime.UtcNow - startedAt).TotalSeconds > TimeoutSeconds)
            {
                EditorApplication.update -= Tick;
                Time.captureFramerate = 0;
                Debug.LogError("[Exp] timeout after " + TimeoutSeconds + "s");
                EditorApplication.Exit(3);
            }
        }
    }
}
