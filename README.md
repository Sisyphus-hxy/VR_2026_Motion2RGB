# Motion2RGB

Motion2RGB 是一个面向课程展示的 Unity 视觉退化原型：运动状态驱动相机轨迹，再生成 Clean RGB 与 Motion-Degraded RGB 双视图。它是启发式演示，不是经过真实相机数据标定的成像模拟器。

## 环境

- Unity `2022.3.62f3`
- 当前项目使用内置 Built-in Render Pipeline，未安装 URP
- 使用 Unity 内置 Input Manager 和 UGUI；Unity Package Manager 会按 `Packages/manifest.json` 和 `packages-lock.json` 恢复项目依赖

## 获取完整项目

```sh
git clone https://github.com/Sisyphus-hxy/VR_2026_Motion2RGB.git
cd VR_2026_Motion2RGB
```

仓库根目录就是 Unity 项目根目录。机器人 FBX、贴图、材质、场景、脚本及 `.meta` 文件均已包含，普通 `git clone` 即可获取，不需要 Git LFS 或另行下载模型。当前演示不使用 AI 模型权重。

Unity 编辑器需要单独安装。首次打开时，Unity 会重建 `Library` 并恢复包依赖，需要等待导入和编译完成；恢复注册表中的包时需要联网。

## 直接运行 Windows 演示

完整解压仓库根目录的 `Robot.zip`，双击其中的 `My project.exe`。请保留同级的 `My project_Data`、`MonoBleedingEdge` 和 DLL 文件；该演示包不需要安装 Unity 编辑器。

此压缩包是已有的演示构建；需要验证或修改当前源码时，请使用下面的 Unity 运行方式。

## 快速运行

1. 在 Unity Hub 中添加克隆后的 `VR_2026_Motion2RGB` 目录，使用 Unity `2022.3.62f3` 打开。
2. 等待资源导入和脚本编译完成。
3. 打开 `Assets/Motion2RGB/Scenes/Motion2RGB_Robot.unity`；这是当前构建设置启用的机器人演示场景。
4. 点击 Play。左侧是 Clean RGB，右侧是 Degraded RGB，观察窗口显示机器人。

基础演示场景位于 `Assets/Motion2RGB/Scenes/Motion2RGB_Demo.unity`，也可以通过 `Tools > Motion2RGB > Open Demo Scene` 打开。已有场景可以直接运行，无需先重建。截图保存在 `Assets/Motion2RGB/Exports/`。

## 仓库内容

| 路径 | 内容 |
| --- | --- |
| `Assets/` | Unity 场景、代码、Shader、机器人模型和贴图 |
| `Packages/`、`ProjectSettings/` | 包版本、Unity 版本与项目设置 |
| `Robot.zip` | 可独立运行的 Windows 演示构建 |
| `Presentation/Exports/` | 汇报 PPTX、PDF、演示视频、讲稿和页面预览 |
| `Presentation/Motion2RGB_Proposal_ppt169_20261012/` | 汇报源文件和复现说明，依赖见其中的 `BUILD_README.md` |
| `Tools/` | 可选的模型检查、数值检查和开发辅助脚本 |

Unity 缓存、日志、本地备份、重复的构建目录、浏览器配置和下载的第三方工具不纳入版本控制。PPT 和视频通过普通 Git 保存，克隆时会一并下载。

## 模型来源

Robot Kyle 的来源记录在 [PROVENANCE.md](Assets/Motion2RGB/ThirdParty/RobotKyle/PROVENANCE.md)，原仓库版权与授权文本保存在 [SOURCE_LICENSE.md](Assets/Motion2RGB/ThirdParty/RobotKyle/SOURCE_LICENSE.md)。分发这些资源时请保留对应文件。

## 可选开发工具

`python Tools/check_blur_response.py` 和 `python Tools/inspect_robot_fbx.py` 可以在安装 Python 后运行，不需要启动 Unity。数值检查不能替代 Play Mode 或 GPU 渲染验证。

`Tools/compile_motion2rgb.py` 和 `Tools/download_robot.py` 保留了原开发机器的 Unity 安装路径和代理设置，仅供开发参考；普通克隆和运行不需要执行它们。在其他机器使用前，请按自己的环境调整对应路径和代理配置。

## 控制

- `WASD` 移动，`Shift` 加速，鼠标旋转视角，`Space` 跳跃
- `Esc` 释放鼠标，点击 Game View 重新捕获，`R` 重置位置
- `F` 切换自动演示，路线包含静止、行走、跑步、转弯和停止
- 底部面板可以调节 Walking Speed、Stride Length、Gait Amplitude、Blur Intensity、Exposure Proxy，并切换 Motion Blur、Gait Motion、Same Pose、Auto Demo
- Exposure Proxy 是简化的退化强度控制，不代表经过标定的曝光时间

## 重建

`Build Demo Scene` 会创建或更新 `Assets/Motion2RGB/` 下的演示资源，不会修改原 `SampleScene`。项目不依赖收费资源或外部下载。
