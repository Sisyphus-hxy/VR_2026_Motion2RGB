# Motion2RGB

Motion2RGB 是一个面向课程展示的 Unity 视觉退化原型：运动状态驱动相机轨迹，再生成 Clean RGB 与 Motion-Degraded RGB 双视图。它是启发式演示，不是经过真实相机数据标定的成像模拟器。

## 环境

- Unity `2022.3.62f3`
- 当前项目使用内置 Built-in Render Pipeline，未安装 URP
- 使用 Unity 内置 Input Manager 和 UGUI，无需额外包

## 快速运行

1. 用 Unity Hub 打开本项目。
2. 等待脚本编译完成。
3. 菜单选择 `Tools > Motion2RGB > Build Demo Scene`。
4. 菜单选择 `Tools > Motion2RGB > Open Demo Scene`，打开 `Assets/Motion2RGB/Scenes/Motion2RGB_Demo.unity`。
5. 点击 Play。左侧是 Clean RGB，右侧是 Degraded RGB。

若项目已有生成场景，直接打开该场景即可。截图保存在 `Assets/Motion2RGB/Exports/`。

## 控制

- `WASD` 移动，`Shift` 加速，鼠标旋转视角，`Space` 跳跃
- `Esc` 释放鼠标，点击 Game View 重新捕获，`R` 重置位置
- `F` 切换自动演示，路线包含静止、行走、跑步、转弯和停止
- 底部面板可以调节 Walking Speed、Stride Length、Gait Amplitude、Blur Intensity、Exposure Proxy，并切换 Motion Blur、Gait Motion、Same Pose、Auto Demo
- Exposure Proxy 是简化的退化强度控制，不代表经过标定的曝光时间

## 重建

`Build Demo Scene` 会创建或更新 `Assets/Motion2RGB/` 下的演示资源，不会修改原 `SampleScene`。项目不依赖收费资源或外部下载。
