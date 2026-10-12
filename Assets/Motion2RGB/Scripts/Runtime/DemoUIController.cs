using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Motion2RGB
{
    public class DemoUIController : MonoBehaviour
    {
        public FirstPersonController controller;
        public MotionStateTracker tracker;
        public GaitMotionDriver gait;
        public CameraDegradationController degradation;
        public AutoDemoController autoDemo;

        public Slider walkingSpeedSlider;
        public Slider strideSlider;
        public Slider gaitAmplitudeSlider;
        public Slider blurSlider;
        public Slider exposureSlider;
        public Toggle blurToggle;
        public Toggle gaitToggle;
        public Toggle samePoseToggle;
        public Toggle autoDemoToggle;
        public Text statsText;
        public Text hintText;
        public Button stationaryButton;
        public Button walkingButton;
        public Button runningButton;
        public Button resetButton;
        public Button screenshotButton;
        public Button degradationModeButton;

        private float fps;

        private void Start()
        {
            Bind();
            SetPreset(1);
        }

        private void Bind()
        {
            if (walkingSpeedSlider != null) walkingSpeedSlider.onValueChanged.AddListener(v => { if (controller != null) controller.walkingSpeed = v; });
            if (strideSlider != null) strideSlider.onValueChanged.AddListener(v => { if (tracker != null) tracker.strideLength = v; });
            if (gaitAmplitudeSlider != null) gaitAmplitudeSlider.onValueChanged.AddListener(v => gait?.SetAmplitude(v));
            if (blurSlider != null) blurSlider.onValueChanged.AddListener(v => { if (degradation != null) degradation.blurIntensity = v; });
            if (exposureSlider != null) exposureSlider.onValueChanged.AddListener(v => { if (degradation != null) degradation.exposureProxy = v; });
            if (blurToggle != null) blurToggle.onValueChanged.AddListener(v => degradation?.SetBlurEnabled(v));
            if (gaitToggle != null) gaitToggle.onValueChanged.AddListener(v => degradation?.SetGaitEnabled(v));
            if (samePoseToggle != null) samePoseToggle.onValueChanged.AddListener(v => { if (degradation != null) degradation.samePoseComparison = v; });
            if (autoDemoToggle != null) autoDemoToggle.onValueChanged.AddListener(v => autoDemo?.SetEnabled(v));
            if (stationaryButton != null) stationaryButton.onClick.AddListener(() => SetPreset(0));
            if (walkingButton != null) walkingButton.onClick.AddListener(() => SetPreset(1));
            if (runningButton != null) runningButton.onClick.AddListener(() => SetPreset(2));
            if (resetButton != null) resetButton.onClick.AddListener(ResetParameters);
            if (screenshotButton != null) screenshotButton.onClick.AddListener(CaptureScreenshot);
            if (degradationModeButton != null) degradationModeButton.onClick.AddListener(CycleDegradationMode);
        }

        private void Update()
        {
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 1f - Mathf.Exp(-Time.unscaledDeltaTime * 8f));
            if (statsText == null || tracker == null) return;
            string mode = tracker.CurrentMode.ToString().ToUpperInvariant();
            statsText.text = $"MODE  {mode}\nSPEED  {tracker.PlanarSpeed:0.00} m/s\nANGULAR  {tracker.AngularVelocityDegPerSec:0.0} deg/s\nGAIT  {tracker.GaitFrequencyHz:0.00} Hz\nFPS  {fps:0.0}";
            if (hintText != null) hintText.text = autoDemo != null && autoDemo.autoDemoEnabled ? "AUTO CRUISE  |  F to stop" : "WASD move  |  Shift run  |  Mouse look  |  Esc unlock  |  F auto cruise  |  R reset";
            if (degradationModeButton != null && degradation != null)
            {
                Text modeText = degradationModeButton.GetComponentInChildren<Text>();
                if (modeText != null) modeText.text = "MODE  " + degradation.ModeLabel();
            }
        }

        public void SetPreset(int preset)
        {
            if (controller == null || tracker == null || gait == null || degradation == null) return;
            if (preset == 0)
            {
                controller.walkingSpeed = 0f; tracker.strideLength = 0.7f; gait.verticalAmplitude = 0f; degradation.blurIntensity = 0.1f;
            }
            else if (preset == 2)
            {
                controller.walkingSpeed = 3f; tracker.strideLength = 0.9f; gait.verticalAmplitude = 0.115f; degradation.blurIntensity = 1f;
            }
            else
            {
                controller.walkingSpeed = 1.5f; tracker.strideLength = 0.7f; gait.verticalAmplitude = 0.08f; degradation.blurIntensity = 0.9f;
            }
            gait.SetAmplitude(gait.verticalAmplitude);
            if (walkingSpeedSlider != null) walkingSpeedSlider.SetValueWithoutNotify(controller.walkingSpeed);
            if (strideSlider != null) strideSlider.SetValueWithoutNotify(tracker.strideLength);
            if (gaitAmplitudeSlider != null) gaitAmplitudeSlider.SetValueWithoutNotify(gait.verticalAmplitude);
            if (blurSlider != null) blurSlider.SetValueWithoutNotify(degradation.blurIntensity);
        }

        public void ResetParameters()
        {
            SetPreset(1);
            if (controller != null) controller.ResetPose();
            if (autoDemo != null) autoDemo.SetEnabled(false);
        }

        public void CycleDegradationMode()
        {
            if (degradation != null) degradation.CycleMode();
        }

        public void CaptureScreenshot()
        {
            string root = Application.isEditor ? Application.dataPath : Application.persistentDataPath;
            string folder = Path.Combine(root, "Motion2RGB/Exports");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "Motion2RGB_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png");
            ScreenCapture.CaptureScreenshot(path, 1);
            if (hintText != null) hintText.text = "Screenshot queued: " + path;
        }
    }
}
