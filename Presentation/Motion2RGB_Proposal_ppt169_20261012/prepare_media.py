"""Archive the supplied demo video and extract real frames for presentation review."""
from pathlib import Path
import json
import shutil
import subprocess
import imageio_ffmpeg

ROOT = Path(__file__).resolve().parent
VIDEO = ROOT / "media" / "demo_video.mp4"
VIDEO.parent.mkdir(exist_ok=True)
shutil.copy2("E:/software_E/Unity_2022/demo_video.mp4", VIDEO)
ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
probe = subprocess.run([ffmpeg, "-i", str(VIDEO)], capture_output=True, text=True)
(ROOT / "analysis" / "video_probe.txt").write_text(probe.stderr, encoding="utf-8")
print(probe.stderr[-4000:])
for seconds in (2, 8, 15):
    output = ROOT / "images" / f"video_{seconds:02d}s.png"
    subprocess.run([ffmpeg, "-y", "-ss", str(seconds), "-i", str(VIDEO), "-frames:v", "1", str(output)], check=True, capture_output=True)
print("Real video frames extracted.")
