"""Run the official exporter with workspace directory ACL compatibility."""
import os
import runpy
import sys
from pathlib import Path

workspace = Path(__file__).resolve().parents[2]
original_mkdir = os.mkdir


def workspace_mkdir(path, mode=0o777, *, dir_fd=None):
    resolved = Path(path).resolve()
    if os.name == "nt" and mode == 0o700 and resolved.is_relative_to(workspace):
        mode = 0o777
    if dir_fd is None:
        return original_mkdir(path, mode)
    return original_mkdir(path, mode, dir_fd=dir_fd)


os.mkdir = workspace_mkdir
script = Path(sys.argv[1]).resolve()
sys.argv = sys.argv[1:]
sys.path.insert(0, str(script.parent))
runpy.run_path(str(script), run_name="__main__")
