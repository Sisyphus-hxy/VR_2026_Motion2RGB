"""E1c: rotation-only blur.

Prediction for a pure rotation is L = f * omega * T * G, which has no depth term,
so the same angular rate must blur the target by the same amount at any depth.
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import measure_blur as mb
from e1_analysis import draw_panel, fit_through_origin

HALF = 200.0


def collect(run_dir):
    rows = []
    for cond in sorted(os.listdir(run_dir)):
        cdir = os.path.join(run_dir, cond)
        meta = os.path.join(cdir, "frames.jsonl")
        if not os.path.isfile(meta):
            continue
        with open(meta, encoding="utf-8") as fh:
            frames = [json.loads(l) for l in fh if l.strip()]
        per = []
        for fr in frames:
            clean = os.path.join(cdir, "clean_%04d.png" % fr["frame"])
            degr = os.path.join(cdir, "degraded_%04d.png" % fr["frame"])
            if not (os.path.isfile(clean) and os.path.isfile(degr)):
                continue
            wc = mb.edge_width(mb.load_gray(clean), fr["boardU"], fr["boardV"], HALF)
            wd = mb.edge_width(mb.load_gray(degr), fr["boardU"], fr["boardV"], HALF)
            if wc is None or wd is None:
                continue
            per.append((fr["omegaDeg"], fr["boardDepth"], fr["exposureSeconds"],
                        fr["motionGain"], float(np.sqrt(max(0.0, wd * wd - wc * wc)))))
        if per:
            arr = np.array(per, dtype=float)
            rows.append({
                "cond": cond,
                "omega": arr[:, 0].mean(),
                "z": arr[:, 1].mean(),
                "T": arr[:, 2].mean(),
                "G": arr[:, 3].mean(),
                "blur": arr[:, 4].mean(),
            })
    return rows


def main():
    run_dir = sys.argv[1]
    rows = collect(run_dir)
    if not rows:
        print("nothing measured")
        return

    print("%-18s %8s %6s %8s" % ("condition", "omega", "depth", "blur_px"))
    for r in rows:
        print("%-18s %8.1f %6.2f %8.2f" % (r["cond"], r["omega"], r["z"], r["blur"]))

    # 120 deg/s is excluded from the fit: the trail saturates (see report)
    sweep = [r for r in rows if r["omega"] <= 90.0001]
    depth = [r for r in rows if abs(r["omega"] - 90.0) < 0.001]

    if sweep:
        k, r2 = fit_through_origin([r["omega"] for r in sweep], [r["blur"] for r in sweep])
        print("\nangular sweep: k = %.4f px/(deg/s), R2 = %.4f" % (k, r2))
        for r in sweep:
            print("  omega=%5.1f  blur=%5.2f  px per deg/s=%.3f" % (r["omega"], r["blur"], r["blur"] / r["omega"]))

    if depth:
        vals = [r["blur"] for r in depth]
        print("\ndepth independence at fixed omega: %s  (spread %.1f%%)" % (
            ", ".join("%.2f" % v for v in vals), 100.0 * (max(vals) - min(vals)) / np.mean(vals)))

    img = Image.new("RGB", (1500, 640), (255, 255, 255))
    dr = ImageDraw.Draw(img)
    if sweep:
        draw_panel(dr, (20, 20, 720, 620), [(r["omega"], r["blur"]) for r in sweep], k, r2,
                   "E1c  rotation-only blur vs angular rate", "angular rate [deg/s]", "measured width [px]",
                   show_identity=False, note="T fixed (39.6 ms), depth 2.2 m")
    if depth:
        spread = 100.0 * (max(vals) - min(vals)) / float(np.mean(vals))
        draw_panel(dr, (760, 20, 1460, 620),
                   [(r["z"], r["blur"]) for r in depth], 0.0, 0.0,
                   "E1c  depth independence at 90 deg/s", "target depth [m]", "measured width [px]",
                   show_identity=False, show_fit=False,
                   note="blur %.1f / %.1f / %.1f px at z = 1.0 / 2.2 / 4.4 m   (spread %.1f%%)" %
                   (depth[0]["blur"], depth[1]["blur"] if len(depth) > 1 else 0.0,
                    depth[2]["blur"] if len(depth) > 2 else 0.0, spread))
    out = os.path.join(run_dir, "e1c_result.png")
    img.save(out)
    print("\nfigure:", out)


if __name__ == "__main__":
    main()

