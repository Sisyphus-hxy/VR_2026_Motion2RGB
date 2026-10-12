using UnityEngine;

namespace Motion2RGB
{
    [DefaultExecutionOrder(100)]
    public class GaitMotionDriver : MonoBehaviour
    {
        public MotionStateTracker tracker;
        public bool gaitPerturbationEnabled = true;
        [Min(0f)] public float verticalAmplitude = 0.08f;
        [Min(0f)] public float lateralAmplitude = 0.032f;
        [Min(0f)] public float pitchAmplitude = 1.6f;
        [Min(0f)] public float rollAmplitude = 2f;
        [Min(0.1f)] public float smoothing = 14f;

        public float Phase { get; private set; }
        public Vector3 HeadOffset => smoothedOffset;
        public bool IsMoving => tracker != null && tracker.PlanarSpeed > tracker.walkingSpeedThreshold;
        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private Vector3 smoothedOffset;
        private Vector3 smoothedEuler;

        private void Awake()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
            if (tracker == null) tracker = GetComponentInParent<MotionStateTracker>();
        }

        private void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float frequency = tracker != null ? tracker.GaitFrequencyHz : 0f;
            // The tracker reports steps/second. A complete left/right cycle
            // contains two steps; contacts are at phase 0 and PI.
            Phase = Mathf.Repeat(Phase + Mathf.PI * frequency * dt, Mathf.PI * 2f);
            float active = gaitPerturbationEnabled && IsMoving ? 1f : 0f;
            Vector3 targetOffset = active > 0f
                ? new Vector3(lateralAmplitude * Mathf.Cos(Phase), -verticalAmplitude * Mathf.Cos(2f * Phase), 0f)
                : Vector3.zero;
            Vector3 targetEuler = active > 0f
                ? new Vector3(pitchAmplitude * Mathf.Sin(2f * Phase), 0f, rollAmplitude * Mathf.Cos(Phase))
                : Vector3.zero;
            float blend = 1f - Mathf.Exp(-smoothing * dt);
            smoothedOffset = Vector3.Lerp(smoothedOffset, targetOffset, blend);
            smoothedEuler = Vector3.Lerp(smoothedEuler, targetEuler, blend);
            transform.localPosition = baseLocalPosition + smoothedOffset;
            transform.localRotation = baseLocalRotation * Quaternion.Euler(smoothedEuler);
        }

        public void SetBasePose(Vector3 position, Quaternion rotation)
        {
            baseLocalPosition = position;
            baseLocalRotation = rotation;
        }

        public void SetBaseRotation(Quaternion rotation)
        {
            baseLocalRotation = rotation;
        }

        public void SetAmplitude(float amplitude)
        {
            verticalAmplitude = Mathf.Max(0f, amplitude);
            lateralAmplitude = verticalAmplitude * 0.4f;
            pitchAmplitude = verticalAmplitude * 20f;
            rollAmplitude = verticalAmplitude * 25f;
        }
    }
}
