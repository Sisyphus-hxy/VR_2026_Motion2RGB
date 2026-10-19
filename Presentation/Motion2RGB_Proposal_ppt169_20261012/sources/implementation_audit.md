# 项目事实记录（2026-10-12）

以当前 Assets/Motion2RGB/Scripts/Runtime、Shaders 及 Motion2RGB_Robot 1.unity 为准。README与DESIGN早于机器人迭代，部分数学表达已过时。

- Unity 2022.3.62f3，Built-in。Demo和Robot场景存在。原有Assets未修改。
- MotionStateTracker从PlayerRig位姿差估计速度、角速度；步频f=v/L，行走阈值0.15 m/s，跑步阈值2.1 m/s。Turning要求角速度>55 deg/s且平面速度超过行走阈值。原地转向仍可能分类Stationary，但相机运动依然产生模糊。
- GaitMotionDriver当前相位增量为πfΔt，而非旧DESIGN的2πfΔt。f为步数每秒，左右完整周期为两步。横向A_x cosφ、垂直-A_y cos2φ。pitch/roll也由同相位生成，指数平滑。
- RobotGaitAnimator共享步态相位，含跳跃姿态和稳定Clean参考；非物理足接触/IK。
- CameraDegradationController支持Same Pose、开关与模式选择。Same Pose复制Clean世界位姿，保留共享时钟，不应声称它直接禁用步态时钟。
- DirectionalMotionBlur当前读取深度，利用当前逆VP与上一帧或平滑虚拟历史VP重投影。曝光T(E)=lerp(0.005,maximumExposureSeconds,E)，乘intensity与motionGain，除帧时间；motionGain默认2.2、motionDepthLimit默认8，是艺术参数。屏幕轨迹截断maxTrailPixels。
- Shader保留当前像素并沿轨迹取20个额外样本，以深度差降低权重，归一化保持常量颜色亮度。并非完整曝光积分或动态物体速度模型。
- SensorNoise、ResolutionDrop、ChromaticAberration在当前代码中存在，场景有mode button；没有独立运行截图。后三者不应都称作运动幅度连续驱动。
- UI五个Slider、多个Toggle、IDLE/WALK/RUN、Screenshot、模式切换；双RenderTexture和RawImage；Telemetry Graph与Robot Observer View存在。
- 用户截图demo_1、demo_2真实显示双RGB、机器人观察窗、UI与Telemetry。demo_2显示WALKING。demo_1为Stationary但有角速度与模糊，不将其误称Running。
- 迭代报告仅编译/数值检查，缺乏量化真实性、真实标定和导航成功率实验。当前用户已确认Demo可运行并提供截图。
- Unity进程存在但无可见主窗口、无现成可用自动化接口，未改动项目或重建场景来截图。

参考文献核查：RobustNav由Chattopadhyay、Hoffman、Mottaghi、Kembhavi发表（ICCV 2021），研究视觉与动力学corruptions。CVF网页403，arXiv:2106.04531与官方AllenAI页面交叉核对。Tobin等Domain Randomization（IROS 2017），arXiv:1703.06907。引用仅用于研究动机，不作为本项目实验结果。
