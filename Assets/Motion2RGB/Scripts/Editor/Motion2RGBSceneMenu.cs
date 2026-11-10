using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Motion2RGB.Editor
{
    /// <summary>Opens the committed robot demo and the optional, local-only campus.</summary>
    public static class Motion2RGBSceneMenu
    {
        internal const string OriginalScene = "Assets/Motion2RGB/Scenes/Motion2RGB_Robot.unity";
        internal const string CampusScene = "Assets/Motion2RGB/Scenes/Motion2RGB_RealAssetCampus.unity";
        internal static readonly string[] RetainedScenes = { OriginalScene, CampusScene };

        [MenuItem("Tools/Motion2RGB/Open Original Robot Scene", false, 1)]
        public static void OpenOriginal() { TryOpen(OriginalScene); }

        [MenuItem("Tools/Motion2RGB/Open Real Asset Campus", false, 2)]
        public static void OpenCampus() { TryOpen(CampusScene); }

        [MenuItem("Tools/Motion2RGB/Open Real Asset Campus", true)]
        private static bool CampusAvailable() { return File.Exists(CampusScene); }

        internal static bool CanSwitchScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Stop Play Mode before switching scenes.");
                return false;
            }
            return Application.isBatchMode || EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        internal static bool TryOpen(string path)
        {
            if (!CanSwitchScenes()) return false;
            if (!File.Exists(path)) throw new FileNotFoundException("The saved scene is missing. Restore it from Git or the project backup.", path);
            EditorSceneManager.OpenScene(path);
            return true;
        }
    }
}
