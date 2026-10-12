using UnityEngine;

namespace Motion2RGB
{
    [DefaultExecutionOrder(200)]
    public class CameraDegradationController : MonoBehaviour
    {
        public MotionStateTracker tracker;
        public GaitMotionDriver gaitDriver;
        public DirectionalMotionBlur blur;
        public Camera cleanCamera;
        public bool samePoseComparison;
        public bool moduleEnabled = true;
        public DegradationMode mode = DegradationMode.MotionBlurDepthAware;
        [Range(0f, 1f)] public float blurIntensity = 0.75f;
        [Range(0f, 1f)] public float exposureProxy = 0.65f;

        private void Awake()
        {
            if (tracker == null) tracker = GetComponentInParent<MotionStateTracker>();
            if (gaitDriver == null) gaitDriver = GetComponent<GaitMotionDriver>();
            if (blur == null) blur = GetComponent<DirectionalMotionBlur>();
        }

        private void LateUpdate()
        {
            if (blur != null)
            {
                blur.enabled = moduleEnabled;
                blur.mode = mode;
                blur.intensity = blurIntensity;
                blur.exposureProxy = exposureProxy;
            }
            if (samePoseComparison && cleanCamera != null)
            {
                // World pose also works when the cameras have different parents.
                // Keep the shared clock running so legs never lose their phase.
                transform.SetPositionAndRotation(cleanCamera.transform.position, cleanCamera.transform.rotation);
            }
            else if (gaitDriver != null && gaitDriver.transform != transform)
                transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        public void SetBlurEnabled(bool enabled)
        {
            moduleEnabled = enabled;
            if (blur != null) blur.blurEnabled = enabled;
        }

        public void SetGaitEnabled(bool enabled)
        {
            if (gaitDriver != null) gaitDriver.gaitPerturbationEnabled = enabled;
        }

        public void SetMode(DegradationMode nextMode)
        {
            mode = nextMode;
            if (blur != null) blur.SetMode(nextMode);
        }

        public void CycleMode()
        {
            int next = ((int)mode + 1) % System.Enum.GetValues(typeof(DegradationMode)).Length;
            SetMode((DegradationMode)next);
        }

        public string ModeLabel()
        {
            switch (mode)
            {
                case DegradationMode.SensorNoise: return "NOISE";
                case DegradationMode.ResolutionDrop: return "RES DROP";
                case DegradationMode.ChromaticAberration: return "CHROMATIC";
                default: return "DEPTH BLUR";
            }
        }
    }
}
