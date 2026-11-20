"""Edge-width based motion-blur measurement for the Motion2RGB calibration target.

The target is a single vertical step edge at the centre of a board whose angular
size is held constant across depths.  We therefore know exactly where the edge
should be (the projected board centre) and only have to measure how wide the
10-90 % transition is.  Anchoring on the known edge position avoids locking onto
the board silhouette, which is a much stronger edge than the step itself.
"""
import json
import os
import sys

import numpy as np
from PIL import Image

HALF_WINDOW = 95     # px; board half width is ~109 px, so this stays inside the board
ROW_HALF = 14


def load_gray(path):
    return np.asarray(Image.open(path).convert("L"), dtype=np.float32)


def edge_width(img, u, v, half=HALF_WINDOW):
    h, w = img.shape
    x0 = int(max(0, u - half))
    x1 = int(min(w, u + half))
    y0 = int(max(0, v - ROW_HALF))
    y1 = int(min(h, v + ROW_HALF))
    if x1 - x0 < 40 or y1 - y0 < 2:
        return None
    profile = img[y0:y1, x0:x1].mean(axis=0)
    centre = len(profile) // 2

    left = float(np.mean(profile[2:12]))
    right = float(np.mean(profile[-12:-2]))
    hi, lo = max(left, right), min(left, right)
    if hi - lo < 30.0:
        return None

    hi_level = lo + 0.9 * (hi - lo)
    lo_level = lo + 0.1 * (hi - lo)

    # Locate the step itself close to the known board centre, so the board
    # silhouette (a much stronger edge) can never win the search.
    span = range(max(1, centre - 40), min(len(profile) - 2, centre + 40))
    edge = max(span, key=lambda i: abs(profile[i + 1] - profile[i]))

    def crossing(level, start, stop, step):
        for i in range(start, stop, step):
            a, b = profile[i], profile[i + 1]
            if (a - level) * (b - level) <= 0:
                if abs(b - a) < 1e-6:
                    return float(i)
                return float(i) + (level - a) / (b - a)
        return None

    if right > left:      # rising edge: dark on the left, bright on the right
        x_lo = crossing(lo_level, edge, 1, -1)
        x_hi = crossing(hi_level, edge, len(profile) - 2, 1)
    else:                 # falling edge
        x_hi = crossing(hi_level, edge, 1, -1)
        x_lo = crossing(lo_level, edge, len(profile) - 2, 1)
    if x_hi is None or x_lo is None:
        return None
    return abs(x_hi - x_lo)


def run(run_dir, half=HALF_WINDOW):
    rows = []
    for cond in sorted(os.listdir(run_dir)):
        cdir = os.path.join(run_dir, cond)
        meta_path = os.path.join(cdir, "frames.jsonl")
        if not os.path.isfile(meta_path):
            continue
        with open(meta_path, encoding="utf-8") as fh:
            frames = [json.loads(line) for line in fh if line.strip()]
        for fr in frames:
            idx = fr["frame"]
            clean_path = os.path.join(cdir, "clean_%04d.png" % idx)
            degr_path = os.path.join(cdir, "degraded_%04d.png" % idx)
            if not (os.path.isfile(clean_path) and os.path.isfile(degr_path)):
                continue
            u = fr.get("boardU", 0.0)
            v = fr.get("boardV", 0.0)
            wc = edge_width(load_gray(clean_path), u, v, half)
            wd = edge_width(load_gray(degr_path), u, v, half)
            if wc is None or wd is None:
                print("skip %s frame %d (clean=%s degr=%s)" % (cond, idx, wc, wd))
                continue
            blur = float(np.sqrt(max(0.0, wd * wd - wc * wc)))
            rows.append({
                "condition": cond,
                "label": fr["label"],
                "profile": fr.get("profile"),
                "frame": idx,
                "v": fr.get("v"),
                "omegaDeg": fr.get("omegaDeg"),
                "exposure": fr.get("exposureSeconds"),
                "depth": fr.get("boardDepth"),
                "u": u,
                "width_clean": wc,
                "width_degraded": wd,
                "blur_px": blur,
            })
    return rows


def main():
    run_dir = sys.argv[1]
    rows = run(run_dir, float(sys.argv[2]) if len(sys.argv) > 2 else HALF_WINDOW)
    if not rows:
        print("no measurable frames")
        return
    groups = {}
    for r in rows:
        groups.setdefault(r["condition"], []).append(r)
    print("%-24s %5s %7s %6s %8s %8s" % ("condition", "v", "exp_ms", "depth", "w_deg", "blur_px"))
    for cond in sorted(groups):
        g = groups[cond]
        print("%-24s %5.2f %7.1f %6.2f %8.2f %8.2f" % (
            cond, g[0]["v"], g[0]["exposure"] * 1000.0, g[0]["depth"],
            np.mean([r["width_degraded"] for r in g]),
            np.mean([r["blur_px"] for r in g])))


if __name__ == "__main__":
    main()

