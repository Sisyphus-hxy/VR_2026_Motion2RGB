"""Compile against Unity's existing reference assemblies without opening the editor."""
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[1]
output = root / "Logs/RobotCompile"
output.mkdir(parents=True, exist_ok=True)
compiler = Path("E:/software_E/Unity_2022/Editor/Data")
for assembly, folder in (("Assembly-CSharp", "Runtime"), ("Assembly-CSharp-Editor", "Editor")):
    template = next((root / "Library/Bee/artifacts").glob("*.dag/" + assembly + ".rsp"))
    lines = []
    for line in template.read_text(encoding="utf-8-sig").splitlines():
        if line.startswith("-out:"):
            line = '-out:"' + str(output / (assembly + ".dll")) + '"'
        elif line.startswith("-refout:"):
            line = '-refout:"' + str(output / (assembly + ".ref.dll")) + '"'
        elif line.startswith('"Assets/') and line.endswith('.cs"'):
            continue
        elif line.startswith("-r:") and line.endswith('/Assembly-CSharp.ref.dll"'):
            line = '-r:"' + str(output / "Assembly-CSharp.ref.dll") + '"'
        lines.append(line)
    lines.extend('"' + str(p.relative_to(root)) + '"' for p in sorted((root / "Assets/Motion2RGB/Scripts" / folder).glob("*.cs")))
    response = output / (assembly + ".rsp")
    response.write_text("\n".join(lines), encoding="utf-8")
    result = subprocess.run([str(compiler / "Tools/netcorerun/netcorerun.exe"),
        str(compiler / "DotNetSdkRoslyn/csc.dll"), "@" + str(response)], cwd=root, capture_output=True, text=True)
    (output / (assembly + ".log")).write_text(result.stdout + result.stderr, encoding="utf-8")
    print(assembly, "exit", result.returncode, result.stdout, result.stderr, flush=True)
    if result.returncode:
        raise SystemExit(result.returncode)
