# 对比视频

| 文件 | 内容 | 时长 |
| --- | --- | --- |
| `motion2rgb_compare_reel.mp4` | 标题卡 → Unity 一镜到底（走 → 停 → 转身 → **连跳三次**，办公场景）→ TUM 真实数据（78 帧）→ 结尾 | 13.5 s |
| `unity_compare_6s.mp4` | 早期版本：只保留 Unity 三段，原始 primitive 场景 | 6 s |
| `scene_office.png` | 办公场景单帧对比 | — |
| `scene_office_jump.png` | 跳跃瞬间（y = 0.23 m，可见竖向拖影） | — |
| `scene_before_after.png` | 厂房 + 道具版的前后对比 | — |

规格：1944×616、30 fps、H.264、无音轨。

## 场景

工程里现在有三个可切换的环境（`M2R_SCENE` 环境变量），**队友的原场景 `Motion2RGB_Robot.unity` 始终没被修改**：

| 场景 | 来源 | 做法 |
| --- | --- | --- |
| `Motion2RGB_Robot.unity` | 原场景 | 不动 |
| `Motion2RGB_Showcase.unity` | Poly Haven 单体模型 + 材质 | 另存后加道具、贴图、天花板 |
| `Motion2RGB_Office.unity` | 3dassets.dev「Downtown Office Interiors」整套场景（CC0，GLB） | 另存后删掉厂房物件、导入整套场景、补外壳 |

### 办公场景（2026-11-16）

整套场景以 glTF 形式提供，因此工程新增了一个包依赖：

```json
"com.unity.cloud.gltfast": "6.9.1"
```

这是 `Packages/manifest.json` 里**唯一的一行改动**；`packages-lock.json` 随之更新。移除该依赖会让办公场景无法加载。

构建命令：

```powershell
$env:M2R_IMPORT = 'office'      # 或 'flat'（One Bedroom Flat）
Unity.exe -projectPath <repo> -batchmode -quit `
  -executeMethod Motion2RGB.EditorTools.ImportedSceneBuilder.Build
```

`ImportedSceneBuilder` 会做四件事：

1. 从原场景另存目标场景，删除 61 个厂房物件（保留角色、相机、UI、巡航路线）；
2. 实例化 GLB 场景；
3. **补外壳**：这套 pack 只是"开放式办公层"，没有天花板和地板，而相机被 `FirstPersonController` 强制清成纯色，不补就会看到黑色虚空 —— 所以按包围盒自动加地板、天花板和四面墙；
4. 把角色放到房间角落、斜向看向整个开间。

> 素材为 CC0（3dassets.dev），可自由使用。办公场景约 1.2 MB，卧室场景 1.0 MB。

## 怎么生成的

1. **Unity 段**：`ExperimentBatch` 以 30 fps、固定步长跑三条轨迹，逐帧导出 Clean / Degraded 渲染目标（`samePose = true`，所以差异只来自退化本身）：

```powershell
$env:M2R_CONFIG = '<...>\office_video.json'
$env:M2R_SCENE  = 'Assets/Motion2RGB/Scenes/Motion2RGB_Office.unity'
Unity.exe -projectPath <repo> -executeMethod Motion2RGB.EditorTools.ExperimentBatchRunner.Run
```

2. **TUM 段**：`dataset_mode/degrade.py` 跑 `fr3_walking_xyz` 的 78 帧（曝光 20 ms）。

3. **合成**：PIL 逐帧拼左右画面 + 标签 + 遥测行，ffmpeg 编码：

```bash
python build_reel.py <unity_run> <tum_run> <tum_degraded> frames/
ffmpeg -framerate 30 -i frames/%05d.png -c:v libx264 -crf 18 -pix_fmt yuv420p out.mp4
```

## 画面里的信息

- 左下角：`v`（速度）、`w`（角速度）、曝光时间、`motionGain`；
- 右下角：当前状态（WALKING / RUNNING / TURNING IN PLACE）与帧号；
- TUM 段右下角标注数据集名与帧号。

## 还可以做的

- 加字幕与配音，扩成课程要求的 3–5 分钟项目视频；
- 在 Unity 段加入 `Exposure Proxy` 从 0.2 扫到 1.0 的对比，直观展示曝光的影响。

## 运镜（2026-11-16 更新）

`ExperimentBatch` 新增 `profile = "combo"`：一条 6.5 秒的连续时间线，不再靠多条件拼接（多条件会跳切）。

| 时间 | 动作 |
| --- | --- |
| 0 – 1.2 s | 从静止加速到 1.5 m/s |
| 1.2 – 2.2 s | 匀速前进 |
| 2.2 – 2.8 s | 减速停住 |
| 2.8 – 4.2 s | 原地转身 60 °/s |
| 4.2 – 6.5 s | 边慢转（20 °/s）边连跳三次（抛物线下落，高 0.28 m，每次 0.45 s） |

跳跃是给角色加一条**竖直抛物线位移**（`4·h·u·(1-u)`），相机跟着上下走，所以竖直方向的运动模糊是真实的几何结果，不是后处理。`frames.jsonl` 里记录了 `yOffset`，视频右下角会显示 `JUMPING`。

> 早期版本用 2.8 m/s 跑 4 秒，直接怼到墙上，画面全白；现在改成"走一段、停下、原地转和跳"，避免撞墙。
