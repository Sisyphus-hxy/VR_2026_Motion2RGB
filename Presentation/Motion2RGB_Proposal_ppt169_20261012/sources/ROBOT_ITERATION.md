# Robot and camera motion iteration

## Open in Unity

1. Stop Play Mode and open the existing Motion2RGB demo scene.
2. Wait for FBX/texture import and C# compilation to finish.
3. Select Tools > Motion2RGB > Create Robot Scene From Current Scene.
4. The menu saves a separate Motion2RGB_Robot scene and attaches the imported Robot Kyle.
   Existing scene geometry and controls are copied. The original scene is preserved.
5. Enter Play Mode. Walk and look down with the mouse to inspect the legs.
   The observer window shows the complete robot. Shift runs; Space jumps.

## Changes

- Robot Kyle's real thigh, knee, ankle and arm bones use the same phase as head shake.
  Gait Hz represents steps per second; each full left/right cycle contains two steps.
- Cameras attach to a mount beneath the imported Head bone. Clean RGB is stabilized;
  Degraded RGB receives the head gait. Same Pose still compares identical camera poses.
- First-person cameras render a separate skinned body with head/neck triangles removed.
  The complete textured model remains visible to the observer camera.
- Walk bob amplitude: 0.08 m; run bob amplitude: 0.115 m; UI maximum: 0.20 m.
  The amplitude control also scales lateral, pitch and roll motion.
- Motion blur reprojects depth through the camera's current and previous view/projection
  matrices. Actual camera translation and rotation include bobbing, jumping and turning.
  Exposure is adjustable; trail samples are directional and normalized to preserve brightness.
- Camera cuts and teleports reset history. This is an approximation of camera exposure
  using adjacent rendered poses, not a full acceleration or moving-object simulation.
  Procedural foot animation is synchronized; it is not a physics/IK foot-contact solver.

## Verified

- Backup: Backups/RobotIteration_20261011_193920.
- Downloaded FBX (330704 bytes), color texture and normal texture; checked FBX skeleton names.
- Runtime and Editor C# compile via Unity's Roslyn compiler and existing project references.
  Both returned exit code 0. Logs: Logs/RobotCompile.
- Existing .unity scenes were not changed by these code edits.

## Pending

Unity was not launched for this iteration, per the user's code-only preference.
FBX import, generated scene, first-person leg framing, observer appearance, shader GPU
compilation, directional blur brightness and jump behavior still require Play Mode verification.

## Smoother motion blur update

- Source backup: `Backups/BlurStrength_20261011_200908`.
- Added `motionGain = 2.2` and `motionDepthLimit = 8` to the blur component.
  The depth limit is an artistic approximation for visible distant-scene translation;
  actual depth is still used to filter foreground/background edges.
- Camera linear and angular motion now use exponential filtering: 0.09 s response
  and 0.18 s settling time constants. A stop gradually fades instead of abruptly
  clearing; approximately 95% of the residual fades over 0.54 s. Reversals blend
  toward the new direction. Head motion and jumps use full camera movement.
- No running-speed threshold is involved. A stationary camera converges to zero blur.
  Disable `smoothCameraMotion` to compare the direct pose-based response.
- Increased blur sampling from 12 to 20 taps, retaining brightness normalization.
- New fields work on existing saved scenes without recreating the robot scene.
- Runtime and Editor C# were compiled with Unity Roslyn, both exit code 0.
- `Tools/check_blur_response.py` checks the numerical response at 30/60/120 fps,
  vertical jump screen displacement and constant-color weight normalization.
  These are numerical checks, not GPU rendering or Play Mode visual validation.
