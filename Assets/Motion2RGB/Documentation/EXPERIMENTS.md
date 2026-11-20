# Motion2RGB 实验模式（确定性采集）

用于把"演示"变成"可测"：固定步长驱动轨道、逐帧导出 Clean / Degraded 两张图、同时记录真值。

## 文件

| 文件 | 作用 |
| --- | --- |
| `Assets/Motion2RGB/Scripts/Experiments/ExperimentBatch.cs` | 运行时。按配置逐条件积分位姿、抓帧、写 JSONL |
| `Assets/Motion2RGB/Scripts/Editor/ExperimentBatchRunner.cs` | 编辑器入口。读 `M2R_CONFIG`，开场景、进 Play、等结束标记后退出 |
| `Assets/Motion2RGB/Documentation/EXPERIMENTS.md` | 本文档 |

## 为什么可重复

- 位姿用**帧计数**积分（`dt = 1/fps` 常数），不读 `Time.deltaTime`；
- `Time.captureFramerate = fps` 把引擎的 deltaTime 也钉在 `1/fps`，所以步态驱动、运动测量、模糊 shader 看到的时间间隔完全一致；
- 每个条件开始前重置位姿、`MotionStateTracker.ResetMeasurement()`、`DirectionalMotionBlur.ResetHistory()`；
- 实测：同一配置连跑两次，24 张 PNG 的 SHA256 全部相同。

## 运行

```powershell
$env:M2R_CONFIG = 'E:\...\exp\smoke02.json'
& 'E:\unity2022\Editor\Editor\Unity.exe' -projectPath 'E:\...\VR_2026_Motion2RGB' `
    -executeMethod Motion2RGB.EditorTools.ExperimentBatchRunner.Run -logFile run.log
```

进程会在写完全部结果后自动退出；退出码来自 `summary.json` 的 `exitCode`。

## 配置

```json
{
  "name": "e1_velocity",
  "fps": 60,
  "width": 1280,
  "height": 720,
  "warmupFrames": 60,
  "captureFrames": 3,
  "imageFormat": "png",
  "outputDir": "E:\\...\\experiments",
  "conditions": [
    {
      "label": "linear15_e065",
      "profile": "linear",
      "speed": 1.5,
      "angularDeg": 0,
      "exposureProxy": 0.65,
      "blurIntensity": 0.9,
      "gaitAmplitude": 0.08,
      "strideLength": 0.7,
      "samePose": false,
      "degradationMode": 0
    }
  ]
}
```

`profile` 取值：

- `static`：完全静止（应无模糊）；
- `linear`：匀速直线，`speed` m/s；
- `arc`：`speed` 与 `angularDeg` 同时生效，转弯半径 `R = v / ω`；
- `pan`：只转不走（`speed = 0`），用于验证"原地转向也能产生退化"；
- `stopgo`：加速 / 匀速 / 减速，加速度 1.2 m/s²。

`degradationMode`：0 深度模糊、1 噪声、2 降分辨率、3 色差。

## 输出

```
<outputDir>/<name>/
  00_static_e065/
    clean_0000.png  degraded_0000.png
    frames.jsonl
  01_linear15_e065/ ...
  summary.json
<name>.json.done        # 完跑标记，runner 据此退出
```

`frames.jsonl` 每行一帧，同时给出**真值**和**测量值**：

- 真值：`v`、`omegaDeg`、`poseX/Y/Z`、`yaw`、`exposureProxy`、`exposureSeconds`；
- 测量：`trackerSpeed`、`trackerAngularDeg`、`gaitHz`、`gaitPhase`。

两者放在一起，就可以直接量化"测量链路有没有失真"——这正是自动巡航那一版出问题的地方。

## 已知约束

- 相机被重定向到运行时新建的 RenderTexture（默认 1280×720），UI 上的 RawImage 在实验期间会显示空白，属预期；
- 实验期间 `FirstPersonController` 的手动输入和 `AutoDemoController` 都被关闭，角色由本组件直接驱动；
- `samePose = true` 时退化相机会复制 Clean 相机位姿，此时只比较成像、不比较轨迹；
- `motionDepthLimit = 8 m` 会把远处像素按 8 m 处理，远景模糊偏强，这是既有的艺术近似，写论文时要标明。

## 标定板（`createTarget`）

E1 需要一个稳定、可测的模糊对象，所以实验模式会在运行时生成一块**黑白阶跃边**标定板（不建场景文件、不改任何现有资源）：

- 位置：按条件放在**捕获时刻相机位姿**的正前方，默认偏航 20°，距离 = `targetDepth`；
- 尺寸：`targetSizeFactor × depth`（默认 0.39），保证不同深度在画面里占据**同样的角度**；
- 材质：`Motion2RGB/Experiments/CalibrationBoard`（`Assets/Motion2RGB/Experiments/CalibrationBoard.shader`）。

> 注意：这块板必须用自带 `ShadowCaster` / `DepthOnly` pass 的 shader。Unity 自带的 `Unlit/Texture` 没有这两个 pass，物体不会写进 `_CameraDepthTexture`，深度感知模糊读到的会是对应像素上的**背景深度** —— 现象是纯旋转时能糊、纯平移时几乎不糊。

## E1 测量方法

对每一帧取标定板中心所在的水平条带，测阶跃边的 **10–90% 宽度**（像素），再用 `sqrt(w_degraded² − w_clean²)` 扣掉成像本身的边缘宽度。

理论预测：`L = (u − cx) · v · T · G / z`，其中 `u − cx` 是标定板相对主点的水平偏移，`v` 是速度，`T` 是曝光秒数，`G` 是 `motionGain`，`z` 是深度。

## E1 结果（2026-11-16）

5 个速度（0.5–3.0 m/s）× 5 个曝光（proxy 0.2–1.0），深度固定 2.2 m，共 25 个条件，每个条件 2 帧：

- 过原点线性拟合：**k = 0.896，R² = 0.9917**；
- 深度扫描（v = 1.5 m/s）：`blur × z = 20.8 / 22.5 / 23.5 px·m`（z = 1.0 / 2.2 / 4.4 m），`1/z` 关系成立，R² = 0.994。

斜率 0.90 而不是 1.0，说明实测偏小约 10%：主要来自 10–90% 宽度对较长拖影的低估、以及深度差降权与邻域 clamp 对边缘的削峰。论文里应写成"与理论预测高度线性，斜率 0.90"。

28 个条件（含深度扫描）整跑约 27 秒。

## E1c 纯旋转（2026-11-16）

`profile = "pan"`，`speed = 0`，只转不走；标定板加大到 `targetSizeFactor = 0.8`（转向拖影长，需要更宽的测量窗口，脚本传 `half = 200`）：

| ω (deg/s) | 15 | 30 | 60 | 90 | 120 |
| --- | --- | --- | --- | --- | --- |
| 实测模糊 (px) | 9.64 | 19.54 | 35.93 | 54.82 | 58.13 |

- 15–90 °/s 过原点线性拟合：**k = 0.605 px/(deg/s)，R² = 0.9982**；
- **深度无关性**：ω = 90 °/s 时，z = 1.0 / 2.2 / 4.4 m 的模糊是 53.5 / 54.8 / 54.8 px，离散度 **2.4%**。这是纯旋转与纯平移最本质的区别：旋转的像素位移 `f·ω·T` 里没有 `z`。

两项注意：

1. **120 °/s 开始饱和**（58 px，按 90 °/s 的斜率应约 73 px）。原因是长拖影下 20 个采样点之间间隔变大，配合邻域 clamp 的削峰，长拖影被压缩。这同时解释了 E1 里 k = 0.896 < 1 的系统性偏低。
2. 实测的 10–90% 宽度大约是几何拖影长度的 **0.8–0.9 倍**（盒式模糊的理论关系），所以绝对斜率不要直接和 `f·ω·T·G` 比较，比趋势和 R²。

## 复现命令

```powershell
# 1) 生成配置（见 Experiments/analysis 中的示例）
# 2) 跑
$env:M2R_CONFIG = '<...>\e1c_pan.json'
Unity.exe -projectPath <repo> -executeMethod Motion2RGB.EditorTools.ExperimentBatchRunner.Run -logFile run.log
# 3) 量测
python Experiments/analysis/measure_blur.py <run_dir> 200
# 4) 出图
python Experiments/analysis/e1c_pan.py <run_dir>
```



## 采集性能（2026-11-16）

一次采集的耗时构成（1280×720、60 帧）：

| 环节 | 耗时 | 说明 |
| --- | --- | --- |
| 编辑器启动 + 导入 | ~13 s | 每次运行固定成本 |
| 进 Play 后每帧 | ~200 ms | 其中渲染约 180 ms |
| 每张图保存 | 8.8 ms | 读回 + 入队，编码在后台线程 |

改动：`CaptureFrame` 只做 `ReadPixels` 和 `GetRawTextureData`（主线程，约 9 ms/张），
PNG 编码与写盘交给后台线程 (`BlockingCollection` + `Thread`)。
`ImageConversion.EncodeArrayToPNG` 可以脱离主线程调用（已实测验证）。

效果：同样 60 帧，会话时间从 ~17 s 降到 ~13 s（约 23%）。**瓶颈不在写盘**，
而在 Unity 的渲染循环，所以再优化写盘收益有限。

顺带确认：降低分辨率（640×360）只从 202 ms/帧降到约 140 ms/帧；关闭实时阴影、
隐藏 UI Canvas 对帧时间没有可测影响（已回退，避免改变画面）。

```json
{ "asyncEncode": true }   // 设为 false 可回退到主线程同步编码
```
