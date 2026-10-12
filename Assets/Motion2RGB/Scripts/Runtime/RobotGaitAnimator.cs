using UnityEngine;

namespace Motion2RGB
{
    [DefaultExecutionOrder(150)]
    public class RobotGaitAnimator : MonoBehaviour
    {
        public GaitMotionDriver gait;
        public Transform leftHip, rightHip, leftKnee, rightKnee, leftFoot, rightFoot;
        public Transform leftShoulder, rightShoulder;
        public Camera cleanCamera;
        public Transform headReference;
        public Transform cameraLook;
        public bool animateLimbs = true;
        [Range(0f, 50f)] public float legSwingDegrees = 27f;
        [Range(0f, 80f)] public float kneeLiftDegrees = 48f;
        [Header("Running")]
        [Range(1f, 2.5f)] public float runningLegMultiplier = 1.3f;
        [Range(1f, 3f)] public float runningKneeMultiplier = 1.45f;
        [Range(0f, 80f)] public float walkingArmSwingDegrees = 18f;
        [Range(0f, 120f)] public float runningArmSwingDegrees = 48f;
        [Header("Jumping")]
        [Range(0f, 1f)] public float jumpLocomotionBlend = 0.12f;
        [Range(0f, 60f)] public float jumpTuckDegrees = 36f;
        [Range(0f, 80f)] public float jumpArmLiftDegrees = 34f;
        [Range(0f, 1f)] public float jumpForwardLean = 0.35f;
        public FirstPersonController controller;
        private float activity;
        private Transform[] joints;
        private Quaternion[] restRotations;
        private Vector3[] bendAxes;
        private float[] jointAngles;

        private void Awake()
        {
            joints = new[] { leftHip, rightHip, leftKnee, rightKnee, leftFoot, rightFoot, leftShoulder, rightShoulder };
            restRotations = new Quaternion[joints.Length];
            bendAxes = new Vector3[joints.Length];
            jointAngles = new float[joints.Length];
            if (controller == null) controller = GetComponent<FirstPersonController>();
            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i] == null) continue;
                restRotations[i] = joints[i].localRotation;
                bendAxes[i] = joints[i].InverseTransformDirection(transform.right).normalized;
            }
        }

        private void LateUpdate()
        {
            if (gait == null) return;
            MotionStateTracker tracker = gait.tracker;
            bool running = tracker != null && tracker.CurrentMode == MotionMode.Running;
            float jumpWeight = controller != null ? controller.JumpPoseWeight : 0f;
            activity = Mathf.Lerp(activity, animateLimbs && gait.IsMoving ? 1f : 0f,
                1f - Mathf.Exp(-Time.deltaTime * 14f));
            for (int i = 0; i < jointAngles.Length; i++) jointAngles[i] = 0f;

            float locomotionWeight = Mathf.Lerp(1f, jumpLocomotionBlend, jumpWeight);
            float legMultiplier = running ? runningLegMultiplier : 1f;
            float kneeMultiplier = running ? runningKneeMultiplier : 1f;
            PoseLeg(leftHip, leftKnee, leftFoot, gait.Phase, activity * locomotionWeight, legMultiplier, kneeMultiplier);
            PoseLeg(rightHip, rightKnee, rightFoot, gait.Phase + Mathf.PI, activity * locomotionWeight, legMultiplier, kneeMultiplier);
            float armSwing = running ? runningArmSwingDegrees : walkingArmSwingDegrees;
            AddJointRotation(leftShoulder, Mathf.Cos(gait.Phase) * armSwing * activity * locomotionWeight);
            AddJointRotation(rightShoulder, -Mathf.Cos(gait.Phase) * armSwing * activity * locomotionWeight);
            if (jumpWeight > 0f) PoseJump(jumpWeight);
            ApplyJointRotations();
            if (cameraLook != null && headReference != null)
                cameraLook.localRotation = headReference.localRotation;
            // Clean RGB is a stabilized reference at the same nominal head pose.
            // Both cameras remain children of the animated robot head.
            if (cleanCamera != null && headReference != null)
                cleanCamera.transform.SetPositionAndRotation(headReference.position, headReference.rotation);
        }

        private void PoseLeg(Transform hip, Transform knee, Transform foot, float phase, float poseActivity, float legMultiplier, float kneeMultiplier)
        {
            float swing = -Mathf.Cos(phase) * legSwingDegrees * legMultiplier * poseActivity;
            float lift = Mathf.Max(0f, -Mathf.Sin(phase)) * kneeLiftDegrees * kneeMultiplier * poseActivity;
            AddJointRotation(hip, swing);
            AddJointRotation(knee, lift);
            AddJointRotation(foot, -swing - lift);
        }

        private void PoseJump(float weight)
        {
            JumpStyle style = controller != null ? controller.CurrentJumpStyle : JumpStyle.Stationary;
            float verticalSpeed = controller != null ? controller.JumpVerticalVelocity : 0f;
            float elapsed = controller != null ? controller.JumpElapsed : 0f;
            float takeoff = 1f - Mathf.Clamp01(elapsed / 0.18f);
            float falling = Mathf.Clamp01(Mathf.InverseLerp(0f, 5f, -verticalSpeed));
            float styleScale = style == JumpStyle.Running ? 1.25f : style == JumpStyle.Walking ? 1f : 0.78f;
            float tuck = jumpTuckDegrees * styleScale * Mathf.Lerp(0.72f, 1.15f, falling) * weight;
            float lean = style == JumpStyle.Running ? -jumpForwardLean * 18f : style == JumpStyle.Walking ? -jumpForwardLean * 8f : 0f;
            AddJointRotation(leftHip, lean + tuck * 0.25f);
            AddJointRotation(rightHip, lean + tuck * 0.25f);
            AddJointRotation(leftKnee, tuck);
            AddJointRotation(rightKnee, tuck);
            AddJointRotation(leftFoot, -tuck * 0.55f);
            AddJointRotation(rightFoot, -tuck * 0.55f);

            float armLift = jumpArmLiftDegrees * (style == JumpStyle.Stationary ? 1f : 0.75f) * weight;
            float alternating = style == JumpStyle.Running ? Mathf.Sin(elapsed * 15f) * 10f * weight : 0f;
            AddJointRotation(leftShoulder, -armLift + alternating);
            AddJointRotation(rightShoulder, -armLift - alternating);
            // Keep the launch crouch visible, then settle into a tucked airborne pose.
            if (takeoff > 0f)
            {
                float crouch = takeoff * weight;
                AddJointRotation(leftKnee, 12f * crouch);
                AddJointRotation(rightKnee, 12f * crouch);
            }
        }

        private void AddJointRotation(Transform joint, float degrees)
        {
            if (joint == null) return;
            int index = System.Array.IndexOf(joints, joint);
            if (index >= 0) jointAngles[index] += degrees;
        }

        private void ApplyJointRotations()
        {
            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i] == null) continue;
                joints[i].localRotation = restRotations[i] * Quaternion.AngleAxis(jointAngles[i], bendAxes[i]);
            }
        }
    }
}
