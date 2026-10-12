using UnityEngine;

namespace Motion2RGB
{
    public enum MotionMode
    {
        Stationary,
        Walking,
        Running,
        Turning
    }

    [DefaultExecutionOrder(20)]
    public class MotionStateTracker : MonoBehaviour
    {
        [Header("Gait estimate")]
        [Min(0.1f)] public float strideLength = 0.7f;
        [Min(0f)] public float walkingSpeedThreshold = 0.15f;
        [Min(0f)] public float runningSpeedThreshold = 2.1f;

        public Vector3 Velocity { get; private set; }
        public float PlanarSpeed { get; private set; }
        public float AngularVelocityDegPerSec { get; private set; }
        public float GaitFrequencyHz { get; private set; }
        public MotionMode CurrentMode { get; private set; } = MotionMode.Stationary;

        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private bool initialized;

        private void OnEnable()
        {
            previousPosition = transform.position;
            previousRotation = transform.rotation;
            initialized = false;
        }

        private void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            if (!initialized)
            {
                previousPosition = transform.position;
                previousRotation = transform.rotation;
                initialized = true;
            }

            Vector3 frameVelocity = (transform.position - previousPosition) / dt;
            Velocity = Vector3.Lerp(Velocity, frameVelocity, 1f - Mathf.Exp(-dt * 18f));
            PlanarSpeed = new Vector3(Velocity.x, 0f, Velocity.z).magnitude;
            AngularVelocityDegPerSec = Mathf.Lerp(
                AngularVelocityDegPerSec,
                Quaternion.Angle(previousRotation, transform.rotation) / dt,
                1f - Mathf.Exp(-dt * 18f));
            GaitFrequencyHz = PlanarSpeed > walkingSpeedThreshold ? PlanarSpeed / Mathf.Max(strideLength, 0.1f) : 0f;

            if (AngularVelocityDegPerSec > 55f && PlanarSpeed > walkingSpeedThreshold)
                CurrentMode = MotionMode.Turning;
            else if (PlanarSpeed < walkingSpeedThreshold)
                CurrentMode = MotionMode.Stationary;
            else if (PlanarSpeed >= runningSpeedThreshold)
                CurrentMode = MotionMode.Running;
            else
                CurrentMode = MotionMode.Walking;

            previousPosition = transform.position;
            previousRotation = transform.rotation;
        }

        public void ResetMeasurement()
        {
            previousPosition = transform.position;
            previousRotation = transform.rotation;
            Velocity = Vector3.zero;
            PlanarSpeed = 0f;
            AngularVelocityDegPerSec = 0f;
            GaitFrequencyHz = 0f;
            CurrentMode = MotionMode.Stationary;
        }
    }
}
