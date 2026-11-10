using UnityEngine;

namespace Motion2RGB
{
    /// <summary>Keeps a depth-tested TextMesh material synchronized with its dynamic font atlas.</summary>
    [ExecuteAlways, RequireComponent(typeof(TextMesh))]
    public sealed class WorldLabel : MonoBehaviour
    {
        private void OnEnable()
        {
            Font.textureRebuilt += RefreshAtlas;
            TextMesh text = GetComponent<TextMesh>();
            if (text.font == null) return;
            text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
            RefreshAtlas(text.font);
        }

        private void OnDisable() { Font.textureRebuilt -= RefreshAtlas; }

        private void RefreshAtlas(Font font)
        {
            TextMesh text = GetComponent<TextMesh>();
            Renderer target = GetComponent<Renderer>();
            if (text.font == font && target.sharedMaterial != null && target.sharedMaterial.shader.name == "Motion2RGB/World Label")
                target.sharedMaterial.mainTexture = font.material.mainTexture;
        }
    }
}
