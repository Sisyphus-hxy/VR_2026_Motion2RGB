using UnityEngine;

namespace Motion2RGB
{
    public enum JumpStyle
    {
        Stationary,
        Walking,
        Running
    }

    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public Camera cleanCamera;
        public Camera degradedCamera;
        public Transform headLookPivot;
        public float walkingSpeed = 1.5f;
        public float runningSpeed = 3f;
        public float mouseSensitivity = 2.2f;
        public float gravity = -20f;
        public bool lockCursorOnStart = true;
        [Min(0.01f)] public float landingPoseDuration = 0.16f;

        private CharacterController characterController;
        private float pitch;
        private float verticalVelocity;
        private float jumpElapsed;
        private float landingPoseTimer;
        private JumpStyle jumpStyle = JumpStyle.Stationary;
        private bool airborneLastFrame;
        private bool manualInputEnabled = true;

        public bool IsAirborne => characterController != null && !characterController.isGrounded;
        public float JumpElapsed => jumpElapsed;
        public float JumpVerticalVelocity => verticalVelocity;
        public JumpStyle CurrentJumpStyle => jumpStyle;
        public float JumpPoseWeight => IsAirborne ? 1f : Mathf.Clamp01(landingPoseTimer / Mathf.Max(0.01f, landingPoseDuration));

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Start()
        {
            ConfigurePreviewCamera(cleanCamera);
            ConfigurePreviewCamera(degradedCamera);
            if (lockCursorOnStart)
                Cursor.lockState = CursorLockMode.Locked;
        }

        private void LateUpdate()
        {
            // RenderTextures can retain pixels on platforms where the camera's
            // skybox clear path does not cover the whole target. Clear both
            // preview targets before either camera renders the next frame.
            ClearPreviewTarget(cleanCamera);
            ClearPreviewTarget(degradedCamera);
        }

        private static void ConfigurePreviewCamera(Camera camera)
        {
            if (camera == null) return;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.forceIntoRenderTexture = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            RenderTexture texture = camera.targetTexture;
            if (texture == null) return;
            texture.Release();
            texture.useMipMap = false;
            texture.autoGenerateMips = false;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            texture.Create();
        }

        private static void ClearPreviewTarget(Camera camera)
        {
            if (camera == null || camera.targetTexture == null || !camera.targetTexture.IsCreated()) return;

            RenderTexture previous = RenderTexture.active;
            Graphics.SetRenderTarget(camera.targetTexture);
            GL.Clear(true, true, camera.backgroundColor);
            RenderTexture.active = previous;
        }

        private void Update()
        {
            if (!manualInputEnabled) return;

            if (Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;
            if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0)) Cursor.lockState = CursorLockMode.Locked;

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                transform.Rotate(Vector3.up, Input.GetAxis("Mouse X") * mouseSensitivity);
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -80f, 80f);
                ApplyPitch();
            }

            Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            input = Vector3.ClampMagnitude(input, 1f);
            bool runHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float speed = runHeld ? runningSpeed : walkingSpeed;
            Vector3 motion = transform.TransformDirection(input) * speed;
            if (characterController.isGrounded)
            {
                verticalVelocity = -1f;
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    verticalVelocity = 6f;
                    float inputAmount = input.magnitude;
                    jumpStyle = inputAmount < 0.1f
                        ? JumpStyle.Stationary
                        : (runHeld ? JumpStyle.Running : JumpStyle.Walking);
                    jumpElapsed = 0f;
                    landingPoseTimer = 0f;
                    airborneLastFrame = true;
                }
            }
            verticalVelocity += gravity * Time.deltaTime;
            motion.y = verticalVelocity;
            characterController.Move(motion * Time.deltaTime);

            if (!characterController.isGrounded)
            {
                airborneLastFrame = true;
                jumpElapsed += Time.deltaTime;
            }
            else if (airborneLastFrame)
            {
                landingPoseTimer = landingPoseDuration;
                jumpElapsed = 0f;
                airborneLastFrame = false;
            }
            landingPoseTimer = Mathf.Max(0f, landingPoseTimer - Time.deltaTime);

            if (Input.GetKeyDown(KeyCode.R)) ResetPose();
        }

        private void ApplyPitch()
        {
            if (headLookPivot != null)
            {
                headLookPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                return;
            }
            if (cleanCamera != null) cleanCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            if (degradedCamera != null)
            {
                Quaternion viewRotation = Quaternion.Euler(pitch, 0f, 0f);
                GaitMotionDriver gait = degradedCamera.GetComponent<GaitMotionDriver>();
                if (gait != null) gait.SetBaseRotation(viewRotation);
                else degradedCamera.transform.localRotation = viewRotation;
            }
        }

        public void SetManualInput(bool enabled)
        {
            manualInputEnabled = enabled;
            if (!enabled) Cursor.lockState = CursorLockMode.None;
        }

        public void ResetPose()
        {
            characterController.enabled = false;
            transform.SetPositionAndRotation(new Vector3(0f, 1.05f, -7f), Quaternion.identity);
            characterController.enabled = true;
            pitch = 0f;
            verticalVelocity = 0f;
            jumpElapsed = 0f;
            landingPoseTimer = 0f;
            jumpStyle = JumpStyle.Stationary;
            airborneLastFrame = false;
            ApplyPitch();
            var tracker = GetComponent<MotionStateTracker>();
            if (tracker != null) tracker.ResetMeasurement();
            if (degradedCamera != null)
                degradedCamera.GetComponent<DirectionalMotionBlur>()?.ResetHistory();
        }
    }
}
