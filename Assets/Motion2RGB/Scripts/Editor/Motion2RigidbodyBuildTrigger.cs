using System.IO;
using UnityEditor;
using UnityEngine;

namespace Motion2RGB.Editor
{
    [InitializeOnLoad]
    public static class Motion2RigidbodyBuildTrigger
    {
        // Re-run the one-shot build when the trigger file is present.
        private const string TriggerPath = "Assets/Motion2RGB/BUILD_ON_EDITOR_START.trigger";

        static Motion2RigidbodyBuildTrigger()
        {
            EditorApplication.update += CheckForRequestedBuild;
        }

        private static void CheckForRequestedBuild()
        {
            if (!File.Exists(TriggerPath)) return;
            // SceneBuilder uses EditorSceneManager.NewScene, which Unity forbids
            // while Play Mode is active. The one-shot request can safely stop
            // Play Mode, then the next editor tick performs the build.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
                return;
            }
            EditorApplication.update -= CheckForRequestedBuild;
            File.Delete(TriggerPath);
            AssetDatabase.Refresh();
            EditorApplication.delayCall += () =>
            {
                Motion2RGBSceneBuilder.BuildDemoScene();
                Motion2RGBSceneBuilder.OpenDemoScene();
            };
        }
    }
}
