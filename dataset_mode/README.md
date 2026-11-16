# 数据集模式：脱离 Unity 的退化管线

同一套"运动状态 → 深度重投影 → 加权采样"模型，用 numpy 重写了一遍，可以直接跑任意
RGB-D + 位姿序列（Replica / Habitat 渲染 / TUM RGB-D / 自己采集的数据），不再依赖 Unity。

和引擎里的实现一一对应：`degrade_frame()` 对应
`Assets/Motion2RGB/Shaders/DirectionalMotionBlur.shader` —— 同样是用当前/上一帧 VP 逐像素重投影，
沿轨迹取 20 个样本，按深度差加权，再做 3×3 邻域 clamp 抑制前后景串色。

## 依赖

只用 `numpy` + `Pillow`。

## 用法

```bash
# 单条序列（最后一个参数是并行进程数，默认 1）
python degrade.py <sequence_dir> <out_dir> [workers]

# 质量指标：干净目录 vs 退化目录
python metrics.py <clean_dir> <degraded_dir> [--csv out.csv]
```

输出：`out_dir/0000.png ...` 和 `degrade_log.json`（每帧平均/最大拖影长度）。

## 输入格式

```
sequence_dir/
  meta.json
  rgb/0000.png
  depth/0000.npy          # 或 0000.png（16 位，单位毫米）
```

```json
{
  "fps": 60,
  "intrinsics": [700.0, 700.0, 480.0, 270.0],
  "exposure_seconds": 0.0396,
  "motion_gain": 2.2,
  "depth_limit": null,
  "frames": [
    {
      "rgb": "rgb/0000.png",
      "depth": "depth/0000.npy",
      "c2w": [[1,0,0,0],[0,1,0,0],[0,0,1,-3],[0,0,0,1]]
    }
  ]
}
```

- `c2w` 是**相机到世界**的 4×4 矩阵（行主序）；
- `depth` 是相机 z，单位米；16 位 PNG 按毫米解析；
- `exposure_seconds` 与 `motion_gain` 和 Unity 里 `maximumExposureSeconds × Exposure Proxy × intensity`、
  `motionGain` 是同一组量，便于两边对齐参数；
- `depth_limit` 对应引擎里的 `motionDepthLimit`，`null` 表示关闭（推荐，物理上更干净）。

## 自检

```bash
python selftest.py
```

会渲染三块不同深度的标定平面并检查三条性质：

| 检查 | 结果 |
| --- | --- |
| 静止时输出等于输入 | max abs diff = 5.96e-08 |
| 平移：`blur × z` 恒定 | 21.47 / 22.20 / 22.75（z = 2 / 4 / 7 m），离散 5.8% |
| 旋转：模糊与深度无关 | 29.82 px（三个深度完全相同） |

平移组的 `blur × z` 落在 21–23，与 Unity 端 E1 实测的 20.8–23.5 一致，说明两边模型对齐。

> 注意：`[2]` 用的是 1.5 m/s、39.6 ms 曝光；阶跃边放在离轴 0.3·z 的位置。
> 如果把它放在主点上，前向运动在该点的光流为零，会测出 0 模糊——这不是 bug，是透视投影的必然。

## 已知限制

- 第一帧没有前一帧，输出等于输入（和引擎里 `ResetHistory` 后第一帧一致）；
- 只在帧间位姿上做一次曝光积分，没有做快门内的多子帧累积，因此没有遮挡/显露；
- 没有滚动快门、没有动态物体拖影（和引擎一致）；
- 逐帧纯 numpy，960×540 × 20 tap 大约 1–2 s/帧，适合离线跑，不适合实时。

## 接到真实数据集

两个适配器都在 `adapters/`，把原始格式转成上面的 `meta.json` + `depth/*.npy`。

### TUM RGB-D

```bash
python adapters/tum_rgbd.py --src D:\data\rgbd_dataset_freiburg3_walking_xyz --out runs\tum_fr3
```

- 读取 `rgb.txt` / `depth.txt` / `groundtruth.txt`，按最近时间戳做三方关联（默认容差 0.02 s）；
- 深度按 **5000** 缩放成米（TUM 的约定），可用 `--depth-scale` 覆盖；
- 内参默认 freiburg3（535.4 / 539.2 / 320.1 / 247.6），可用 `--intrinsics fx fy cx cy` 覆盖；
- TUM 不记录曝光时间，`--exposure` 默认 0.02 s，按需要改；
- 位姿按 **相机到世界**（`T_wc`）解释，和 TUM 的 `groundtruth.txt` 一致。

### Replica

```bash
python adapters/replica.py --src D:\data\Replica\room_0 --out runs\replica_room0
```

- 读取 `results/frame%06d.jpg`、`results/depth%06d.png`、`trajectory.txt`；
- 深度按 **6553.5** 缩放（Replica 把 10 m 映射到 65535），可用 `--depth-scale` 覆盖；
- 内参默认 1200×680 的 600 / 600 / 599.5 / 339.5，可用 `--intrinsics` 覆盖；
- 位姿是 `trajectory.txt` 里的 `tx ty tz qx qy qz qw`（相机到世界）。

两个适配器都支持 `--stride` / `--max-frames` / `--copy-rgb`。

### 适配器测试

```bash
python adapters/test_adapters.py
```

用合成的小型 TUM / Replica 目录结构验证解析逻辑，全部通过：

| 检查 | 结果 |
| --- | --- |
| 四元数 → 旋转矩阵 | 绕 z 转 90° 正确 |
| TUM 时间戳关联（4 帧里 1 帧无匹配） | 保留 3 帧 |
| TUM fps 由时间戳中位数推出 | 30.30 |
| TUM 位姿 / 深度缩放 | 2.0 m 正确 |
| TUM → 退化全链路 | 3 帧输出 |
| Replica 位姿顺序 / 深度缩放 | 正确 |

### 接真实数据前要确认的两件事

1. **位姿方向**：本管线要 `c2w`（相机到世界）。TUM 的 `groundtruth.txt` 就是这个约定；如果你的副本给的是 `T_cw`，先取逆。
2. **深度缩放**：TUM 用 5000、Replica 用 6553.5 是常见约定，但不同派生版本可能不同。用一帧的有效深度范围和一个已知尺寸的物体核对一下，比信默认值可靠。

## 真实数据实测：TUM `fr3_walking_xyz`

已经在整条真实序列上跑通了全链路（`samples/tum_fr3_walking_result.png`）：

```
python adapters/tum_rgbd.py --src <...>\rgbd_dataset_freiburg3_walking_xyz --out runs\tum80 --start 200 --max-frames 80 --exposure 0.02
python degrade.py runs\tum80 runs\tum80_degraded
```

- **78 帧**、30.9 fps、曝光 20 ms（其中 2 帧因时间戳关联失败被跳过）；
- 拖影：均值 **6.6 px**、中位 6.2 px、最大 16.4 px；
- 梯度能量降到原来的 **79–87%**；
- 近处走动的人被明显拖糊、远处厂房几乎不动，与 `1/z` 一致；
- 速度：640×480 × 20 tap ≈ **2.3 s/帧**（含补洞），适合离线批处理。

### 真实数据上的一个关键坑：深度有 52% 是 0

TUM 的 walking 序列里超过一半的深度像素是无效值（0）。直接跑会有两个后果：

1. 统计出的"平均拖影"是假的（无效像素的重投影会爆到钳制上限 90 px）；
2. 更严重的是**画面会一块块花**——没有深度的区域保持清晰，有深度的区域在糊。

处理办法（已在 `degrade.py` 内实现，默认开启）：

- `fill_depth_holes()`：用邻域迭代填充无效深度（默认 24 轮，可覆盖约 24 px 的空洞半径）；
- 把无效像素的 trail 直接置 0，不再参与统计。

修复前后的对比很直观：同一段序列平均拖影从"46.7 px"变成"4.8 px"，后者才符合 20 ms 曝光下滑行的物理量。

> 这一条也适用于你们自己的 Kinect/RealSense 采集：真实深度永远是稀疏的，喂进任何深度感知的退化模型之前都得先补洞，否则退化会变成"斑块"而不是"模糊"。



## 性能（2026-11-16 优化）

640×480、20 tap、含深度补洞，78 帧的 TUM 序列：

| 版本 | 耗时 | 每帧 |
| --- | --- | --- |
| 优化前（串行） | 182 s | 2.33 s |
| 采样器合并 + 扁平索引 | 106 s | 1.36 s |
| 再加 6 进程并行 | **30 s** | **0.38 s** |

两处改动：

1. **颜色和深度一次采样**：原来每 tap 调两次 `_sample_bilinear`，索引算术做两遍；合并成 `_bilinear_pair`，并用扁平索引 `v*w+u` 代替二维花式索引。输出与原实现最大差 0.002（0.5/255），可视为等价。
2. **多进程**：每帧只依赖 meta 里的前一帧位姿，天然可并行。`ProcessPoolExecutor` 按帧分发，1/6 进程输出**逐像素一致**。

## 质量指标

`metrics.py` 给出 PSNR、SSIM（积分图实现的盒窗，不依赖 scipy）、梯度能量比。TUM 78 帧（曝光 20 ms）的结果：

| 指标 | 均值 |
| --- | --- |
| PSNR | 26.69 dB |
| SSIM | 0.8988 |
| 梯度能量比（退化/干净） | 0.858 |

第 0 帧没有前一帧，PSNR 为 ∞、SSIM 为 1.0（输出等于输入），统计时应排除。

## E2：真实曝光标定（尝试与结论）

目标：用真实模糊帧反推引擎的曝光 `T` 与增益 `G`。

做法（`calibrate.py`）：真实帧的模糊长度 `L = 光流(px/s) · T · G`，几何模型能算出光流，
于是测出 `L` 就能解出 `T·G`。`L` 用**倒谱法**估计（均匀线性模糊会让对数谱出现周期零点，
其倒谱在模糊长度处出峰）。

**估计器本身是对的**，在合成数据上做过单元测试：

| 真实 L | 5 | 10 | 20 | 30 |
| --- | --- | --- | --- | --- |
| 倒谱估计 | 5 | 10 | 20 | 30 |

**但用在我们的渲染结果上失败**：对已知 20 ms 曝光的图，估计器返回的是搜索下限 3 px，
而不是预期的 ~16 px。原因很清楚：

1. 我们的模糊是**深度加权**的，同一个 patch 内不同深度像素的拖影长度不同，
   不存在单一周期，倒谱峰被抹平；
2. 真实场景纹理杂乱，且 TUM 深度大面积缺失（补洞后 patch 内深度仍不连续）。

结论：**盲反卷积这条路走不通**，E2 需要成对的清晰/模糊数据。可行方案按成本排序：

| 方案 | 做法 | 成本 |
| --- | --- | --- |
| A. 自己采一对 | 固定场景、相机平移，用同一台设备拍两张（短曝光 / 长曝光），直接量 PSF | 约 10 分钟，最可靠 |
| B. 公开成对数据集 | RealBlur / Nah 等真实模糊数据集，自带清晰-模糊配对 | 下载 1–4 GB |
| C. 保持现状 | E1 用合成真值证明模型正确，E2 写作"future work" | 0 |

`calibrate.py --validate` 的验证模式保留着：一旦有了成对数据，可以直接用它确认估计器
在真实数据上的偏差，再跑 `python calibrate.py <seq_dir>` 得到拟合的 `T·G`。
