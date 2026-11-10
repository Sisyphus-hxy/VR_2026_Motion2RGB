using UnityEngine;

namespace Motion2RGB
{
    /// <summary>
    /// Small piece of metadata placed on generated scenes so a build or a
    /// capture can identify which environment was used.
    /// </summary>
    public sealed class SceneVariantInfo : MonoBehaviour
    {
        public string displayName = "Industrial Corridor";
        public string source = "Procedural";
    }
}
