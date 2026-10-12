using System.IO;
using UnityEditor;

namespace Motion2RGB.Editor
{
    public class Motion2RGBAssetBuildPostprocessor : AssetPostprocessor
    {
        private const string TriggerPath = "Assets/Motion2RGB/BUILD_ON_EDITOR_START.trigger";

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!File.Exists(TriggerPath)) return;
            File.Delete(TriggerPath);
            EditorApplication.delayCall += () =>
            {
                Motion2RGBSceneBuilder.BuildDemoScene();
                Motion2RGBSceneBuilder.OpenDemoScene();
            };
        }
    }
}
