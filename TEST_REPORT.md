# Test Report

## Static checks

- [x] 项目根目录存在 `Assets/`、`Packages/`、`ProjectSettings/`
- [x] Unity 版本确认：`2022.3.62f3`
- [x] 当前包清单确认未安装 URP，采用 Built-in 兼容退化 shader
- [x] 原 `Assets/Scenes/SampleScene.unity` 未修改
- [x] 生成器会创建独立 `Assets/Motion2RGB/Scenes/Motion2RGB_Demo.unity`

## Runtime checks

需要在本机 Unity Editor 中执行 `Tools > Motion2RGB > Build Demo Scene` 后点击 Play 完成。预期检查：双 RawImage 显示、WASD/鼠标控制、静止时模糊接近关闭、行走/跑步步态变化、快速转弯时方向性模糊、关闭 Blur 后右图清晰、F 自动演示、R 重置、截图导出。

本次自动化环境未能获取可见 Unity Editor 窗口，因此没有虚报 Play Mode 结果；若 Console 出现编译错误，应先等待导入完成并重新执行构建菜单。
