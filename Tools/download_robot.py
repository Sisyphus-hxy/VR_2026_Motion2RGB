"""Download only Robot Kyle assets and their license from Unity Japan's repository."""
from pathlib import Path
import re
from urllib.parse import quote
import requests

root = Path(__file__).resolve().parents[1]
session = requests.Session()
session.trust_env = False
session.proxies = {"https": "http://127.0.0.1:7897"}
destination = root / "Assets/Motion2RGB/ThirdParty/RobotKyle"
destination.mkdir(parents=True, exist_ok=True)
base = "vjkit/Assets/VJKit/Models/Robot Kyle/"
paths = {base + "Model/Robot Kyle.fbx"}
for folder in ("", "Materials", "Model", "Textures"):
    response = session.get("https://github.com/unity3d-jp/fuze-vj-kit/tree/master/" + quote(base + folder), timeout=30)
    if response.status_code == 404:
        continue
    response.raise_for_status()
    for path in re.findall(r'"path":"([^"]+)"', response.text):
        if path.startswith(base) and not path.endswith(".meta"):
            paths.add(path)
for path in sorted(paths):
    response = session.get("https://raw.githubusercontent.com/unity3d-jp/fuze-vj-kit/master/" + quote(path), timeout=30)
    if response.status_code == 404:
        continue
    response.raise_for_status()
    target = destination / path[len(base):]
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(response.content)
    print(target.relative_to(destination), len(response.content), flush=True)
response = session.get("https://raw.githubusercontent.com/unity3d-jp/fuze-vj-kit/master/README.md", timeout=30)
response.raise_for_status()
(destination / "SOURCE_LICENSE.md").write_bytes(response.content)
