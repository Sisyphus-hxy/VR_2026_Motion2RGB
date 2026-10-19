<!-- ppt-master-schema: spec-lock/v1 -->
# Execution Lock

## canvas
- viewBox: 0 0 1280 720
- format: PPT 16:9

## communication
- primary_language: zh-CN
- audience: VR课程教师与同学
- objective: 5分钟内理解运动驱动RGB原型的动机、实际实现和后续可验证工作。
- core_message: 已有实时双RGB原型，后续验证运动驱动退化模型。
- consumption_mode: presentation

## mode
- mode: custom
- mode_behavior: 按用户七页顺序解释动机、因果链、系统、方法、当前Demo和计划，事实可核查。

## visual_style
- visual_style: custom
- visual_style_behavior: 浅底大字、左对齐标题、细线关系、真实截图，结果页主图，流程和公式原生可编辑。

## colors
- background: #FFFFFF
- secondary_bg: #F2F5F7
- primary: #123D64
- accent: #0089AA
- secondary_accent: #B87924
- body_text: #222C35
- secondary_text: #576772
- divider: #D6DFE5

## typography
- font_family: Microsoft YaHei, Arial, sans-serif
- title_family: Microsoft YaHei, Arial, sans-serif
- body_family: Microsoft YaHei, Arial, sans-serif
- body: 28
- title: 40
- subtitle: 30
- annotation: 22
- footer: 14
- display: 64

## icons
- library: none
- inventory: none

## page_rhythm
- P01: anchor
- P02: anchor
- P03: breathing
- P04: dense
- P05: dense
- P06: anchor
- P07: breathing

## pptx_structure
- mode: flat

## images
- demo-cover: images/demo_1.png | source=user | crop=adaptive
- video-poster: images/video_08s.png | source=user | crop=no-crop

## forbidden
- `mask`, `<style>`, `class`, external CSS, `<foreignObject>`, `textPath`, `@font-face`, `<animate*>`, `<set>`, `<script>` / event attributes, `<iframe>`
- HTML named entities in text; write typography as raw Unicode and escape XML reserved characters
- 禁止虚构 Demo 截图、测试数值或成功结果。 (user)
- 不要重新创建 Unity 项目，不要修改现有功能。 (user)
