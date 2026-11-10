# Motion2RGB

Motion2RGB 是一个面向课程展示的 Unity 视觉退化原型：运动状态驱动相机轨迹，再生成 Clean RGB 与 Motion-Degraded RGB 双视图。它是启发式演示，不是经过真实相机数据标定的成像模拟器。

## 环境

- Unity `2022.3.62f3`
- 当前项目使用内置 Built-in Render Pipeline，未安装 URP
- 使用 Unity 内置 Input Manager 和 UGUI，无需额外包

## 获取项目

```sh
git clone https://github.com/Sisyphus-hxy/VR_2026_Motion2RGB.git
cd VR_2026_Motion2RGB
```

仓库根目录就是 Unity 项目。原版机器人演示所需的模型、贴图、材质及 `.meta` 文件均包含在仓库中，正常克隆后可以运行。首次打开需要等待 Unity 导入资源并恢复 Packages 中的依赖。

**厂房直路场景和约 4.21GB 的 Poly Haven 素材仅保留在本地，不随 Git 上传。**新克隆的项目中，厂房打开和预览菜单会自动禁用，验证工具只检查已安装的场景。

仓库已有的 `Robot.zip` 是之前导出的独立 Windows 演示，解压后运行 `My project.exe`，并保留同级数据目录和 DLL；它不会随本次代码更新重新构建。PPT、PDF、视频和讲稿仍位于 `Presentation/Exports/`，汇报源文件位于 `Presentation/Motion2RGB_Proposal_ppt169_20261012/`。

## 快速运行

1. 用 Unity Hub 打开本项目。
2. 等待脚本编译完成。
3. 在 Project 面板打开 `Assets/Motion2RGB/Scenes/`，双击原版机器人场景；保留了本地厂房文件的机器也可以打开厂房场景。
4. 也可以通过 `Tools > Motion2RGB` 中对应的 Open 菜单打开。
5. 点击 Play。左侧是 Clean RGB，右侧是 Degraded RGB。

| 场景 | 内容 | 菜单 |
| --- | --- | --- |
| `Motion2RGB_Robot.unity` | Git 提交 `62cf24d` 的原版机器人演示，作为默认启动场景 | `Open Original Robot Scene` |
| `Motion2RGB_RealAssetCampus.unity`（仅本地） | 厂房、直路与写实设备组成的工业园区 | `Open Real Asset Campus` |

本地维护这两个场景，Git 仓库及默认 Build Settings 只包含原版机器人场景。有完整厂房文件时，可在本机 Build Settings 中自行加入厂房场景进行构建。运行中的截图导出到 `Assets/Motion2RGB/Exports/`。

## 控制

- `WASD` 移动，`Shift` 加速，鼠标旋转视角，`Space` 跳跃
- `Esc` 释放鼠标，点击 Game View 重新捕获，`R` 重置位置
- `F` 切换自动演示，路线包含静止、行走、跑步、转弯和停止
- 底部面板可以调节 Walking Speed、Stride Length、Gait Amplitude、Blur Intensity、Exposure Proxy，并切换 Motion Blur、Gait Motion、Same Pose、Auto Demo
- Exposure Proxy 是简化的退化强度控制，不代表经过标定的曝光时间

## 维护

- `Scripts/Runtime/` 保留相机退化、机器人步态、控制与 UI 逻辑。
- `Scripts/Editor/Motion2RGBSceneMenu.cs` 负责打开已保存的原版场景，以及可选的本地厂房场景。
- `Tools > Motion2RGB > Checks > Validate Retained Scenes` 检查场景列表、脚本、网格、材质与园区巡航通道，结果写入 `Previews/RetainedScenesValidation.json`。
- `Tools > Motion2RGB > Checks > Export Campus Previews` 导出园区预览。
- 场景生成器、自动重建触发器、中间场景和未使用的专用材质已移出项目；整理前的内容保存在 `Backups/SceneCleanup_*.zip`。
- 写实素材库保留在 `Assets/Motion2RGB/RealScenes/PolyHaven/Library4K/`。下载来源、体积及校验方法见 [园区说明](Assets/Motion2RGB/Documentation/REAL_ASSET_CAMPUS.md)。

Robot Kyle 的来源和授权说明保留在 [PROVENANCE.md](Assets/Motion2RGB/ThirdParty/RobotKyle/PROVENANCE.md) 与 [SOURCE_LICENSE.md](Assets/Motion2RGB/ThirdParty/RobotKyle/SOURCE_LICENSE.md)。
