using UnityEngine;

namespace Motion2RGB
{
    [RequireComponent(typeof(Camera))]
    public class DirectionalMotionBlur : MonoBehaviour
    {
        [Range(0f, 1f)] public float intensity = 0.55f;
        public bool blurEnabled = true;
        public MotionStateTracker tracker;
        public float exposureProxy = 0.65f;
        public float angularContribution = 0.006f;
        public DegradationMode mode = DegradationMode.MotionBlurDepthAware;
        [Range(0.1f, 1f)] public float resolutionScale = 0.5f;
        [Range(0f, 1f)] public float noiseAmount = 0.22f;
        [Range(0f, 1f)] public float chromaticAmount = 0.35f;
        [Range(0.1f, 8f)] public float depthSensitivity = 3.5f;
        [Range(0f, 1f)] public float trailStrength = 0.85f;
        [Range(1f, 200f)] public float maxTrailPixels = 90f;
        [Tooltip("Exposure seconds at Exposure Proxy = 1. Includes head shake, jump and turn motion.")]
        [Range(0.005f, 0.1f)] public float maximumExposureSeconds = 0.065f;
        [Header("Camera motion visibility")]
        [Tooltip("Scales all camera motion: walking, running, head shake, jump and rotation.")]
        [Range(0.1f, 8f)] public float motionGain = 2.2f;
        [Tooltip("Caps distance for blur reprojection only, making far-scene walking and jumping visible. This is an artistic approximation.")]
        [Range(1f, 60f)] public float motionDepthLimit = 8f;
        [Header("Motion smoothing")]
        public bool smoothCameraMotion = true;
        [Tooltip("Seconds for motion to build up. Smooths frame-to-frame speed and head shake.")]
        [Range(0.01f, 0.3f)] public float motionResponseSeconds = 0.09f;
        [Tooltip("Seconds for the blur to settle after slowing down or stopping.")]
        [Range(0.01f, 0.5f)] public float motionSettleSeconds = 0.18f;
        public Color tint = Color.white;

        private Material material;
        private Camera targetCamera;
        private Matrix4x4 previousViewProjection;
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private bool hasPreviousPose;
        private Vector3 filteredVelocity;
        private Vector3 filteredAngularVelocity;

        private void Awake()
        {
            if (tracker == null) tracker = GetComponentInParent<MotionStateTracker>();
            Shader shader = Shader.Find("Hidden/Motion2RGB/DirectionalMotionBlur");
            if (shader != null) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            targetCamera = GetComponent<Camera>();
            targetCamera.depthTextureMode |= DepthTextureMode.Depth;
        }

        private void OnEnable() => ResetHistory();
        public void ResetHistory()
        {
            hasPreviousPose = false;
            filteredVelocity = Vector3.zero;
            filteredAngularVelocity = Vector3.zero;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            Matrix4x4 viewProjection = GL.GetGPUProjectionMatrix(targetCamera.projectionMatrix, true)
                * targetCamera.worldToCameraMatrix;
            bool cameraCut = !hasPreviousPose || Vector3.Distance(previousPosition, transform.position) > 2f
                || Quaternion.Angle(previousRotation, transform.rotation) > 75f;
            if (cameraCut)
            {
                previousViewProjection = viewProjection;
                filteredVelocity = Vector3.zero;
                filteredAngularVelocity = Vector3.zero;
            }

            if (!blurEnabled || intensity <= 0.001f || material == null)
            {
                Graphics.Blit(source, destination);
                StorePose(viewProjection);
                return;
            }

            Vector3 velocity = tracker != null ? tracker.Velocity : Vector3.forward;
            Vector2 direction = new Vector2(velocity.x, velocity.z);
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
            direction.Normalize();
            Vector2 screenDirection = new Vector2(direction.x, -direction.y);
            material.SetInt("_Mode", (int)mode);
            material.SetVector("_BlurDirection", screenDirection);
            material.SetMatrix("_CurrentInverseVP", viewProjection.inverse);
            float dt = Mathf.Max(Time.deltaTime, 0.001f);
            Matrix4x4 motionViewProjection = previousViewProjection;
            if (smoothCameraMotion && !cameraCut)
                motionViewProjection = SmoothedPreviousViewProjection(dt);
            material.SetMatrix("_PreviousVP", motionViewProjection);
            float exposure = Mathf.Lerp(0.005f, maximumExposureSeconds, exposureProxy) * intensity;
            material.SetFloat("_ExposureFraction", exposure * motionGain / dt);
            material.SetVector("_CurrentCameraPosition", transform.position);
            material.SetFloat("_MotionDepthLimit", motionDepthLimit);
            material.SetFloat("_TrailStrength", trailStrength);
            material.SetFloat("_MaxTrailPixels", maxTrailPixels);
            material.SetFloat("_ResolutionScale", resolutionScale);
            material.SetFloat("_NoiseAmount", noiseAmount * intensity);
            material.SetFloat("_ChromaticAmount", chromaticAmount * intensity);
            material.SetFloat("_DepthSensitivity", depthSensitivity);
            material.SetFloat("_NoiseSeed", Time.unscaledTime);
            material.SetColor("_Tint", tint);
            Graphics.Blit(source, destination, material);
            StorePose(viewProjection);
        }

        private Matrix4x4 SmoothedPreviousViewProjection(float dt)
        {
            Vector3 velocity = (transform.position - previousPosition) / dt;
            Quaternion delta = transform.rotation * Quaternion.Inverse(previousRotation);
            // Use the shortest signed rotation so a direction reversal cannot flip to 360 degrees.
            if (delta.w < 0f) delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            Vector3 angularVelocity = angle > 0.001f ? axis * (angle / dt) : Vector3.zero;
            filteredVelocity = FilterMotion(filteredVelocity, velocity, dt);
            filteredAngularVelocity = FilterMotion(filteredAngularVelocity, angularVelocity, dt);

            // Reproject through a virtual recent pose based on filtered motion.
            // A brief residual exposure remains at stops without accumulating dark frames.
            Vector3 position = transform.position - filteredVelocity * dt;
            float angularSpeed = filteredAngularVelocity.magnitude;
            Quaternion rotation = angularSpeed > 0.001f
                ? Quaternion.AngleAxis(-angularSpeed * dt, filteredAngularVelocity / angularSpeed) * transform.rotation
                : transform.rotation;
            Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f))
                * Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
            return GL.GetGPUProjectionMatrix(targetCamera.projectionMatrix, true) * view;
        }

        private Vector3 FilterMotion(Vector3 current, Vector3 target, float dt)
        {
            float duration = target.sqrMagnitude < current.sqrMagnitude ? motionSettleSeconds : motionResponseSeconds;
            float blend = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, duration));
            Vector3 filtered = Vector3.Lerp(current, target, blend);
            return target == Vector3.zero && filtered.sqrMagnitude < 0.000001f ? Vector3.zero : filtered;
        }

        private void StorePose(Matrix4x4 viewProjection)
        {
            previousViewProjection = viewProjection;
            previousPosition = transform.position;
            previousRotation = transform.rotation;
            hasPreviousPose = true;
        }

        public void SetEnabled(bool enabled) => blurEnabled = enabled;

        public void SetMode(DegradationMode nextMode) => mode = nextMode;

        public void CycleMode()
        {
            int next = ((int)mode + 1) % System.Enum.GetValues(typeof(DegradationMode)).Length;
            mode = (DegradationMode)next;
        }
    }
}
