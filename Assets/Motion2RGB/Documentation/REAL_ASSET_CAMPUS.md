# 写实工业园区与 Poly Haven 素材库

素材放在本项目的 `Assets/Motion2RGB/RealScenes/PolyHaven/Library4K/`，实际位置在 E 盘。

## 本地可选内容

按项目约定，厂房场景 `.unity` 及其 `.meta`、整个 `RealScenes/` 素材目录和 `Previews/` 预览仅保留本地，不上传 GitHub。
Git 仓库提供原版机器人场景，以及兼容本地厂房的代码。缺少厂房文件时，对应打开、预览菜单禁用，验证工具正常检查原版。
默认 Build Settings 只包含原版机器人场景；需要在本机打包厂房时，可手动加入本地厂房场景。

`Tools/download_polyhaven_library.py` 可以重新下载素材库，但不会重建场景布局或恢复原有 `.meta` GUID。
要在另一台机器还原完整厂房，需要一起复制原机器的厂房 `.unity`、`.meta`、`RealScenes/` 及其 `.meta` 文件。

## 直接查看场景

1. 用 Unity 2022.3 打开本项目，等右下角导入、编译进度结束。
2. 在 **Project** 面板打开 `Assets → Motion2RGB → Scenes`，双击 `Motion2RGB_RealAssetCampus`。
3. 点击编辑器顶部的 **▶ Play**。按 **F** 切换自动巡航；鼠标和移动操作沿用现有 Motion2RGB 演示。

也可以点击 Unity 窗口最上方的菜单 **Tools → Motion2RGB → Open Real Asset Campus**。
这里的 Tools 是菜单栏中的文字，不是 Project 面板里的文件夹，也不是场景编辑工具按钮。
该菜单直接打开已保存的园区，沿用相机、RGB 对比界面和控制脚本。

本地项目维护两个场景：Git 提交 `62cf24d` 中的 `Motion2RGB_Robot.unity`，以及这个厂房直路场景。
旧的生成菜单和自动重建逻辑已经移除。场景可以直接在 Unity 中编辑并保存；整理前的脚本和中间版本归档在项目根目录的 `Backups/SceneCleanup_*.zip`。

## 下载内容和体积

| 内容 | 数量 | 格式 |
| --- | ---: | --- |
| 工业设备、厂房模块、路灯、树木、街道家具等模型组 | 42 | FBX 与 4K JPG 贴图 |
| 沥青、混凝土、地砖材质 | 3 | 4K PBR 贴图 |
| 街道、工厂、车间等实景环境光 | 8 | 8K HDR |

540 个源文件合计 **4,214,761,385 字节，约 4.21GB / 3.93GiB**，低于 10GB 源文件预算。
Windows 资源管理器通常会显示接近 3.93GB。Unity 的 `Library/` 导入缓存属于额外占用。
使用中的普通贴图默认以 2K 压缩格式导入，天空以 4K 导入，磁盘上的 4K/8K 源文件保留。

这些是写实模型、模块化建筑和实景环境光；园区布局由项目脚本组合，HDRI 用作天空与光照背景。
园区保留中间的通行路线，两侧布置装卸设备、研究工作台、路灯和绿化。
课程中关于沉浸环境、数字孪生和可重复训练的思路，体现为环境细节、分区标识和固定巡航路线。

## 材质和验证

贴图已完成匹配，法线和非颜色贴图已设置对应的导入方式；材质保存在 `RealScenes/PolyHaven/CampusGenerated/`。
`PolyHavenPBR.shader` 在 Built-in Render Pipeline 中读取反照率、法线、粗糙度、金属度、AO 和透明遮罩。
无需额外安装 URP/HDRP 或 glTF 插件。

`download_manifest.json` 保存官方源地址、文件大小和 MD5，`verification_report.json` 保存本地文件校验结果。
可在项目根目录运行以下命令重新校验，命令不会联网：

```powershell
python Tools\download_polyhaven_library.py --verify-only
```

**Tools → Motion2RGB → Checks → Export Campus Previews** 可检查材质、脚本和巡航通道，并将截图输出到项目根目录的 `Previews/`。

本次已在 Unity 2022.3、Direct3D 11 / RTX 4060 Laptop GPU 上生成场景并渲染三个视角。
检查的 252 个 Renderer、54 个材质中，没有缺失脚本、缺失材质、未绑定底色贴图或不支持的 Shader；巡航通道检查为 0 个障碍。
实际预览位于 `Previews/Campus_Street.png`、`Campus_Equipment.png`、`Campus_Overview.png`，检查结果见 `Previews/CampusValidation.json`。

## 来源

所有下载素材来自 [Poly Haven](https://polyhaven.com)，采用 [CC0 许可](https://polyhaven.com/license)。
下载使用 [Poly Haven 官方公开 API](https://polyhaven.com/our-api)。资产清单可用于追溯每个源文件。
