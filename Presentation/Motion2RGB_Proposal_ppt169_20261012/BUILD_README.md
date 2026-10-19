# Motion2RGB 开题汇报源文件

7页，16:9，2026年10月15日VR课程开题。Unity项目仅作为读取来源，Assets未修改。

## 文件

- `build_slides.mjs`：可编辑页面的SVG作者源文件。
- `svg_output/`：七页SVG，原生文本、形状和公式标记。
- `svg_final/`：图片内嵌的独立SVG预览。
- `images/`、`media/`：用户真实Demo素材，视频原始时长20.57秒。
- `notes/total.md`：逐页中文讲稿；`Speaker_Notes.md`：5分钟时间安排。
- `sources/implementation_audit.md`：实际代码与证据范围。
- `embed_demo.py`：在官方原生导出后嵌入视频，不重建页面。
- `run_skill_windows.py`：仅修复本环境工作目录的临时目录权限模式。
- `render_deck.ps1`：通过本机演示软件导出PNG与PDF。
- `verify_delivery.py`：核对原生对象、备注、关系及原始视频哈希。

## 复现

Node.js 24、Python 3.13。官方PPT Master v6.7.0：
https://github.com/hugohe3/ppt-master

使用版本提交：`ca04346de145ed6462ddf34682848e71df5e20a2`。

工作区已有 `../Tooling/ppt-master` 与 `../Tooling/deps`。在当前机器运行：

```powershell
./reproduce.ps1
```

在其他机器先下载同版本Skill，安装其运行依赖与以下依赖：

```powershell
python -m pip install python-pptx==1.0.2 PyMuPDF==1.28.2 uharfbuzz==0.56.3 skia-pathops==0.9.2 XlsxWriter==3.2.9 lxml Pillow
./reproduce.ps1 -SkillDirectory 'C:/path/ppt-master/skills/ppt-master'
```

本机COM注册实际指向WPS，最终PNG和PDF经WPS打开PPTX渲染，全部7页已视觉检查。正式汇报前在教室电脑打开PPTX，进入第6页点击视频并核对播放。PDF与PNG只含静态海报，视频单独保留以便备用。

## 实现边界

当前代码采用深度重投影与加权采样的启发式近似，已经超出旧README的全局方向性模糊描述。步态相位采用πfΔt，一个完整左右周期含两步。Exposure Proxy没有实机标定。量化真实性和导航收益尚无实验数据，页面没有宣称提升。
