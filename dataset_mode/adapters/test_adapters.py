"""Adapter tests on synthetic fixtures that mimic the real folder layouts.

Creates a miniature TUM sequence and a miniature Replica room, runs both
adapters, and checks the produced meta.json / depth / pose values. Then runs the
degradation on the TUM output end-to-end.

Run:  python test_adapters.py
"""
import contextlib
import io
import json
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))

import common
import tum_rgbd
import replica
import degrade

ROOT = os.path.join(os.path.dirname(HERE), "_selftest", "adapters")


def _rgb(path):
    arr = np.zeros((48, 64, 3), dtype=np.uint8)
    arr[:, :32] = 40
    arr[:, 32:] = 220
    Image.fromarray(arr).save(path)


def _depth(path, metres, scale):
    arr = np.full((48, 64), int(round(metres * scale)), dtype=np.uint16)
    Image.fromarray(arr).save(path)


def build_tum(src):
    os.makedirs(os.path.join(src, "rgb"), exist_ok=True)
    os.makedirs(os.path.join(src, "depth"), exist_ok=True)

    rgb_ts = [1000.000, 1000.033, 1000.066, 1001.000]   # last one has no depth/gt
    depth_ts = [1000.001, 1000.034, 1000.067]
    gt = {1000.002: (0.0, 0.0, 0.0), 1000.035: (0.05, 0.0, 0.0), 1000.068: (0.10, 0.0, 0.0)}

    with open(os.path.join(src, "rgb.txt"), "w", encoding="utf-8") as fh:
        fh.write("# timestamp filename\n")
        for ts in rgb_ts:
            name = "rgb/%.6f.png" % ts
            _rgb(os.path.join(src, name))
            fh.write("%.6f %s\n" % (ts, name))

    with open(os.path.join(src, "depth.txt"), "w", encoding="utf-8") as fh:
        fh.write("# timestamp filename\n")
        for ts in depth_ts:
            name = "depth/%.6f.png" % ts
            _depth(os.path.join(src, name), 2.0, 5000.0)
            fh.write("%.6f %s\n" % (ts, name))

    with open(os.path.join(src, "groundtruth.txt"), "w", encoding="utf-8") as fh:
        fh.write("# timestamp tx ty tz qx qy qz qw\n")
        for ts, (tx, ty, tz) in gt.items():
            fh.write("%.6f %.4f %.4f %.4f 0 0 0 1\n" % (ts, tx, ty, tz))


def build_replica(src):
    results = os.path.join(src, "results")
    os.makedirs(results, exist_ok=True)
    lines = []
    for i in range(3):
        name = "frame%06d.jpg" % i
        _rgb(os.path.join(results, name))
        _depth(os.path.join(results, "depth%06d.png" % i), 2.0, 6553.5)
        lines.append("%.3f %.4f 0 0 0 0 0 1" % (i * 0.033, i * 0.02))
    with open(os.path.join(src, "trajectory.txt"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")


def run(module, argv):
    old = sys.argv
    sys.argv = [module.__name__] + argv
    try:
        with contextlib.redirect_stdout(io.StringIO()) as buf:
            module.main()
        return buf.getvalue()
    finally:
        sys.argv = old


def check(label, condition, detail=""):
    print("  %-46s %s %s" % (label, "PASS" if condition else "FAIL", detail))
    return condition


def main():
    os.makedirs(ROOT, exist_ok=True)
    ok = True

    print("\n[quaternion]")
    R = common.quat_to_matrix(0.0, 0.0, np.sin(np.pi / 4), np.cos(np.pi / 4))
    ok &= check("90 deg about z", np.allclose(R, [[0, -1, 0], [1, 0, 0], [0, 0, 1]], atol=1e-6))

    print("\n[TUM adapter]")
    tum_src = os.path.join(ROOT, "tum_src")
    tum_out = os.path.join(ROOT, "tum_out")
    build_tum(tum_src)
    out = run(tum_rgbd, ["--src", tum_src, "--out", tum_out, "--exposure", "0.02"])
    meta = json.load(open(os.path.join(tum_out, "meta.json"), encoding="utf-8"))
    ok &= check("4 rgb frames, 1 unmatched -> 3 kept", len(meta["frames"]) == 3,
                "got %d" % len(meta["frames"]))
    ok &= check("fps from median dt", abs(meta["fps"] - 1 / 0.033) < 0.5, "%.2f" % meta["fps"])
    ok &= check("pose x of frame 1", abs(meta["frames"][1]["c2w"][0][3] - 0.05) < 1e-6)
    d = np.load(meta["frames"][0]["depth"])
    ok &= check("depth 10000 raw / 5000 => 2.0 m", abs(float(d.mean()) - 2.0) < 1e-6,
                "%.3f" % d.mean())
    ok &= check("rgb path exists", os.path.isfile(meta["frames"][0]["rgb"]))
    ok &= check("stdout reports frames", "frames: 3" in out)

    print("\n[TUM -> degrade end to end]")
    deg_dir = os.path.join(ROOT, "tum_degraded")
    degrade.degrade_sequence(tum_out, deg_dir, verbose=False)
    files = sorted(f for f in os.listdir(deg_dir) if f.endswith(".png"))
    ok &= check("degraded frames written", len(files) == 3, str(files))

    print("\n[Replica adapter]")
    rep_src = os.path.join(ROOT, "replica_src")
    rep_out = os.path.join(ROOT, "replica_out")
    build_replica(rep_src)
    run(replica, ["--src", rep_src, "--out", rep_out])
    meta = json.load(open(os.path.join(rep_out, "meta.json"), encoding="utf-8"))
    ok &= check("3 frames", len(meta["frames"]) == 3)
    ok &= check("trajectory order -> x of frame 2", abs(meta["frames"][2]["c2w"][0][3] - 0.04) < 1e-6,
                "%.3f" % meta["frames"][2]["c2w"][0][3])
    d = np.load(meta["frames"][0]["depth"])
    ok &= check("depth 2 m at scale 6553.5", abs(float(d.mean()) - 2.0) < 1e-3, "%.3f" % d.mean())

    print("\nresult: %s" % ("ALL PASS" if ok else "FAILURES PRESENT"))
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())

