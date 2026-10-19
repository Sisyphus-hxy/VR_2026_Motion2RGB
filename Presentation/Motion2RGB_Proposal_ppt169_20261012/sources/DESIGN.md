# Motion2RGB 设计说明

## 数据流

`FirstPersonController` 根据实际 CharacterController 位移和旋转移动 PlayerRig。`MotionStateTracker` 从上一帧位姿估计平面速度、角速度、步频和运动状态。`GaitMotionDriver` 只作用于 DegradedCamera，在连续相位上叠加步态扰动。两台 Camera 分别输出 RenderTexture，并由 Canvas RawImage 并排显示。

## 步态模型

步频使用 `f = v / L`，其中 `v` 是估计的平面速度，`L` 是可调步幅。相位按 `phi += 2*pi*f*dt` 连续累积。相机局部位姿使用 `x = A_x sin(phi)`、`y = A_y sin(2 phi)`，并叠加小幅 pitch/roll，起步和停止时用指数平滑避免跳变。

## 运动模糊

当前项目的 `manifest.json` 没有 URP，因此没有强行安装或升级管线。DegradedCamera 使用 `DirectionalMotionBlur` 的屏幕空间方向性采样 shader，模糊量由平面速度、角速度、Blur Intensity 和 Exposure Proxy 共同决定；静止或关闭开关时直接拷贝画面。它是兼容 Built-in Render Pipeline 的近似，不等价于真实曝光积分。

如果未来切换到匹配的 URP 版本，可将该组件替换为只挂在 DegradedCamera Volume Layer 上的 URP Motion Blur Volume Override，并保留同一套 UI 参数。

## 双相机语义

默认 CleanCamera 使用基础位姿，DegradedCamera 使用步态扰动，因此两幅图不保证像素级配准。启用 Same Pose 后会复制 CleanCamera 位姿并停用步态驱动，用于只比较图像退化。
