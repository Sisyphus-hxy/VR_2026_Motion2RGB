using UnityEngine;

namespace Motion2RGB
{
    [RequireComponent(typeof(Camera))]
    public class CameraOverexposure : MonoBehaviour
    {
        [Range(0f, 3f)] public float intensity = 1f;
        [Range(0f, 8f)] public float bloomIntensity = 1.8f;
        [Range(0f, 4f)] public float maximumExposureBoost = 1.8f;
        [Range(0.1f, 8f)] public float responseSeconds = 0.35f;
        [Range(0f, 1f)] public float lightInfluence = 1f;

        private Material material;
        private Camera targetCamera;
        private float exposureBoost;
        private Transform[] lamps;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            Shader shader = Shader.Find("Hidden/Motion2RGB/CameraOverexposure");
            if (shader != null) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            GameObject[] objects = GameObject.FindGameObjectsWithTag("Untagged");
            var found = new System.Collections.Generic.List<Transform>();
            foreach (GameObject obj in objects)
                if (obj.name.StartsWith("Ceiling Lamp")) found.Add(obj.transform);
            lamps = found.ToArray();
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        private void Update()
        {
            float source = 0f;
            if (lamps != null)
            {
                for (int i = 0; i < lamps.Length; i++)
                {
                    if (lamps[i] == null) continue;
                    Vector3 offset = lamps[i].position - transform.position;
                    float distance = offset.magnitude;
                    float facing = Mathf.Clamp01(Vector3.Dot(transform.forward, offset.normalized) * 0.5f + 0.5f);
                    source = Mathf.Max(source, Mathf.Clamp01(1f - distance / 8f) * (0.45f + facing * 0.55f));
                }
            }
            float target = source * maximumExposureBoost * lightInfluence;
            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.01f, responseSeconds));
            exposureBoost = Mathf.Lerp(exposureBoost, target, blend);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null)
            {
                Graphics.Blit(source, destination);
                return;
            }
            material.SetFloat("_Exposure", 1f + exposureBoost * intensity);
            material.SetFloat("_BloomIntensity", bloomIntensity * intensity * (0.55f + exposureBoost));
            material.SetFloat("_Threshold", 0.72f);
            material.SetVector("_TexelSize", new Vector4(1f / source.width, 1f / source.height, source.width, source.height));
            Graphics.Blit(source, destination, material);
        }
    }
}
