<!-- ppt-master-schema: design-spec/v1 -->
# Motion2RGB Proposal - Design Spec

## I. Project Information

| Item | Value |
| --- | --- |
| Project Name | Motion2RGB_Proposal |
| Canvas Format | PPT 16:9 1280x720 |
| Page Count | 7 |
| Primary Language | zh-CN |
| Target Audience | VR课程教师与同学 |
| Communication Intent | 解释视觉采集差异、技术方案与真实进度，说明后续工作 |
| Desired Audience Outcome | 理解运动驱动退化原型的范围、近似假设和可验证目标 |
| Core Message / Ask / Action | 已有可展示的实时双RGB原型，后续验证运动驱动视觉退化模型 |
| Delivery Context | 2026-10-15课堂现场5分钟开题汇报 |
| Artifact Afterlife | 可编辑汇报材料与期末工作基础 |
| Reading Mode | presentation |
| Content Strategy | 保持用户七页顺序，事实以当前代码和截图为准 |
| Design Style | 实证技术报告 |
| AI Image Acquisition Path | not applicable |
| Generation Mode | continuous |
| Spec Refinement | disabled |
| Speaker Notes | enabled — explicit user instruction |
| Custom Animations | disabled — final Stage-2 proactive policy |
| Narration Audio | disabled — final Stage-2 proactive policy |
| Created Date | 2026-10-12 |

## II. Canvas Specification

| Property | Value |
| --- | --- |
| Format | PPT 16:9 |
| Dimensions | 1280x720 |
| viewBox | `0 0 1280 720` |
| Margins | 48 px |
| Content Area | x48-1232 y40-664 |

## III. Visual Theme

### Theme Style

- **Mode**: custom
- **Mode Behavior**: 按动机、因果链、系统、方法、实证、计划的顺序讲解。每页一个核心信息，保留开题的阶段边界。
- **Visual style**: custom
- **Visual Style Behavior**: 白色场域、左对齐大字与细分隔线。截图维持原始色彩，流程用可编辑节点与方向线，方法页分步态和成像两个区域，结果页使用大图与短边栏。
- **Theme**: 运动驱动RGB采集仿真
- **Tone**: 克制、可核查、学术技术报告

### Color Scheme

| Role | HEX | Purpose |
| --- | --- | --- |
| Background | #FFFFFF | 页面主体 |
| Secondary background | #F2F5F7 | 分区 |
| Primary | #123D64 | 标题、基准分支 |
| Accent | #0089AA | 退化分支与运动链路 |
| Secondary accent | #B87924 | 后续验证标识 |
| Body text | #222C35 | 正文 |
| Secondary text | #576772 | 说明与引用 |
| Divider | #D6DFE5 | 细分隔线 |

## IV. Typography System

### Font Plan

| Role | Character (Reference) | Primary | English if non-English | Fallback tail |
| --- | --- | --- | --- | --- |
| Title | 现代无衬线 | Microsoft YaHei | Arial | sans-serif |
| Body | 清晰无衬线 | Microsoft YaHei | Arial | sans-serif |

- **Title stack**: Microsoft YaHei, Arial, sans-serif
- **Body stack**: Microsoft YaHei, Arial, sans-serif

### Font Size Hierarchy

| Purpose | Anchor Size (px) |
| --- | ---: |
| Body | 28 |
| Title | 40 |
| Subtitle | 30 |
| Annotation | 22 |
| Footer | 14 |
| Display | 64 |

## V. Layout Principles

### Deck-wide Direction

- **Hierarchy direction**: 标题先识别问题，主体由画面、关系或模型解释，底部给出边界。
- **Composition tendency**: 封面宽幅证据，背景双域对照，因果链横向推进，架构双分支，方法上下分层，结果主图，计划两域。
- **Cross-page continuity**: 统一标题轴与页码。青蓝代表运动驱动退化，深蓝代表参考图像。
- **Spacing posture**: 信息有组织、结果页视觉最重。
- **Spacing anchors**: page margin 48, block gap 24, column gutter 32, corner radius 0, body leading 42 px

## VI. Icon Usage Specification

- **Primary bundled library**: none

| Icon Path | Suitable Scenarios |
| --- | --- |

## VIII. Image Resource List

| Filename | Dimensions | Ratio | Purpose | Type | Image pattern | Crop Policy | Acquire Via | Status | Reference | text_policy | page_role |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| demo_1.png | 1805x960 | 1.8802 | 封面真实双RGB | Screenshot | 宽幅保留两相机主要画面 | adaptive | user | Existing | 用户Unity Demo截图1 | native-overlay | cover |
| video_08s.png | 2560x1440 | 1.7778 | Demo视频海报，Running及双视图 | Screenshot | 完整视频帧 | no-crop | user | Existing | 从用户demo_video.mp4第8秒提取 | native-overlay | content |

## IX. Content Outline

#### Slide 01 - Motion2RGB

- **Audience move**: 从项目名称认识到一个已有Demo的视觉采集原型。
- **Relationships**: contrast: Clean RGB与Degraded RGB共享室内场景。
- **Composition**: 大标题与宽幅真实双视图。
- **Title**: Motion2RGB
- **Core message**: 面向Sim-to-Real的运动状态驱动视觉退化仿真系统。
- **Content**: Motion-Conditioned Visual Degradation Simulation；for Sim-to-Real；中文项目名；VR课程开题2026.10.15。
- **Images**: demo_1.png，保留双RGB主体，移除编辑器工具栏与底部UI只在封面。
- **Cover impact**: 真实Clean与Degraded差异作为开场证据。

#### Slide 02 - 背景与动机

- **Audience move**: 从一般Sim-to-Real问题聚焦到运动采集差异。
- **Relationships**: contrast: 理想仿真采集与真实运动采集；link: 步态及转向改变曝光期间相机位姿。
- **Composition**: 两个采集条件对照，下方明确研究范围。
- **Title**: 背景与动机
- **Core message**: 关注运动引起的视觉采集差异。
- **Content**: 理想仿真RGB通常来自瞬时清晰渲染；实际步态与转向会扰动相机，曝光期间运动产生模糊；本项目生成可控RGB观测原型；RobustNav与Domain Randomization提供背景。

#### Slide 03 - 问题建模

- **Audience move**: 理解运动状态如何影响RGB观测。
- **Relationships**: order/link: Motion State、Camera Motion、Visual Degradation、RGB Observation；link: 速度和步幅决定步频，相机位姿变化和深度决定屏幕位移。
- **Composition**: 横向四步因果链及下方模型语义。
- **Title**: 问题建模
- **Core message**: 运动先改变相机轨迹，再影响图像退化。
- **Content**: v、ω、步频；步态/转向/跳跃；重投影与加权采样；Clean与Degraded观测；状态分类不会直接作为模糊强度查表。

#### Slide 04 - 系统架构

- **Audience move**: 理解当前代码中真实的双分支实现。
- **Relationships**: link: 输入控制PlayerRig，Tracker驱动Gait，Gait影响DegradedCamera；split: 场景投影为Clean与Degraded；order: DegradedCamera后处理到RenderTexture到RawImage。
- **Composition**: 输入链上置，中部Clean和Degraded两分支，底部统一UI输出。
- **Title**: 系统架构
- **Core message**: 同一场景的参考与退化双相机实时并排输出。
- **Content**: Built-in；FirstPersonController；MotionStateTracker；GaitMotionDriver；机器人同相位动画；DirectionalMotionBlur；OnRenderImage/Graphics.Blit；双RenderTexture/UGUI；Same Pose比较；当前代码/场景已接入。

#### Slide 05 - 技术方法

- **Audience move**: 理解模型与近似边界。
- **Relationships**: link: 步频更新相位生成相机扰动；order: RGB和深度、位姿重投影、方向性加权采样、退化输出。
- **Composition**: 左步态模型，右重投影模型，下方渲染链和边界说明。
- **Title**: 技术方法
- **Core message**: 共享步态时钟与深度重投影的启发式近似。
- **Content**: f=v/L，完整左右周期两步；相位增量πfΔt；横向cosφ、垂直-cos2φ；实际相机运动含步态/跳跃/转向，平滑后重投影；20个沿轨迹采样与深度差权重；T(E)曝光代理映射，I强度，G运动增益；艺术增益与距离截断；不是完整物理曝光积分。
- **Mathematical content**: f=v/L,\quad \phi_{t+1}=\phi_t+\pi f\Delta t,\quad x=A_x\cos\phi,\quad y=-A_y\cos(2\phi),\quad \mathbf{d}=(\mathbf{u}-\mathbf{u}_{prev})\frac{T(E)IG}{\Delta t}

#### Slide 06 - 当前Demo

- **Audience move**: 看到真实结果与当前验证范围。
- **Relationships**: contrast: Clean与Degraded；membership: 双视图、机器人观察窗、实时UI属于当前Demo；contrast: 可展示内容与待进一步验证内容。
- **Composition**: 20.57秒真实Demo视频主图，短边栏明确进度，完整视频帧作静态海报。
- **Title**: 当前 Demo
- **Core message**: 已有运动驱动双RGB与实时参数控制原型。
- **Content**: 截图可见Walking，视频可见Running与转向过程、双RGB、机器人与实时参数；其他退化模式代码已接入，尚缺独立展示证据；没有导航成功率或真实性量化结果。
- **Images**: video_08s.png完整保留比例，作为嵌入视频海报；video的media/demo_video.mp4在发布时嵌入第6页。

#### Slide 07 - 后续工作与预期产出

- **Audience move**: 理解必做任务、可选扩展与最终交付。
- **Relationships**: membership: 场景运动、模型验证、量化与报告属于必做；真实标定和动态物体属于可选；order: 验证形成最终系统与报告。
- **Composition**: 主要必做工作与次要研究扩展两域，底部最终产出。
- **Title**: 后续工作与预期产出
- **Core message**: 将可展示原型推进为可重复的视觉退化实验系统。
- **Content**: 必做为丰富室内场景和运动模式、验证深度重投影与运动平滑、Same Pose消融和图像指标/性能记录、最终展示与期末报告；可选真实相机标定、动态物体Motion Blur、视觉导航接口；预期可控第一视角RGB与后续鲁棒性评测原型。
- **Closing impact**: 可重复控制运动状态，观察RGB退化。

## X. Speaker Notes Requirements

- **Generation**: enabled
- **Filename**: match each SVG filename under notes/
- **Content**: 中文自然口语，所有成果来自代码和用户截图，附精确来源。讲解边界，避免性能提升断言。
- **Total duration**: 300 seconds
- **Notes style**: conversational
- **Presentation purpose**: 解释动机、方案、进度、预期效果
