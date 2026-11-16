"""E1 analysis: measured blur length vs the analytic prediction.

prediction:  L = (u - cx) * v * T * G / z
  (u - cx)  horizontal offset of the target from the principal point [px]
  v         camera speed [m/s]
  T         exposure [s]  (proxy mapped through maximumExposureSeconds * intensity)
  G         motion gain of the blur component
  z         target depth [m]
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import measure_blur as mb

W, H = 1280, 720
CX = W / 2.0


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
            wc = mb.edge_width(mb.load_gray(clean), fr["boardU"], fr["boardV"])
            wd = mb.edge_width(mb.load_gray(degr), fr["boardU"], fr["boardV"])
            if wc is None or wd is None:
                continue
            per.append({
                "u": fr["boardU"],
                "v": fr["v"],
                "T": fr["exposureSeconds"],
                "G": fr["motionGain"],
                "z": fr["boardDepth"],
                "blur": float(np.sqrt(max(0.0, wd * wd - wc * wc))),
                "label": fr["label"],
                "omega": fr["omegaDeg"],
            })
        if per:
            rows.append({
                "cond": cond,
                "label": per[0]["label"],
                "v": np.mean([p["v"] for p in per]),
                "T": np.mean([p["T"] for p in per]),
                "G": np.mean([p["G"] for p in per]),
                "z": np.mean([p["z"] for p in per]),
                "u": np.mean([p["u"] for p in per]),
                "omega": np.mean([p["omega"] for p in per]),
                "blur": np.mean([p["blur"] for p in per]),
                "n": len(per),
            })
    return rows


def fit_through_origin(x, y):
    x = np.asarray(x, float)
    y = np.asarray(y, float)
    k = float(np.sum(x * y) / max(np.sum(x * x), 1e-9))
    pred = k * x
    ss_res = float(np.sum((y - pred) ** 2))
    ss_tot = float(np.sum((y - y.mean()) ** 2))
    r2 = 1.0 - ss_res / ss_tot if ss_tot > 0 else 0.0
    return k, r2


def draw_panel(draw, box, pts, k, r2, title, xlabel, ylabel, show_identity=True, note="", show_fit=True):
    x0, y0, x1, y1 = box
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    xmax = max(max(xs), 1e-6) * 1.15
    ymax = max(max(ys), 1e-6) * 1.15
    plot_left, plot_right = x0 + 80, x1 - 20
    plot_top, plot_bottom = y0 + 40, y1 - 70
    sx = (plot_right - plot_left) / xmax
    sy = (plot_bottom - plot_top) / ymax

    def px(x, y):
        return (plot_left + x * sx, plot_bottom - y * sy)

    def fmt(value, span):
        return "%.2f" % value if span < 3 else "%.0f" % value

    draw.rectangle([plot_left, plot_top, plot_right, plot_bottom], outline=(0, 0, 0))
    for i in range(6):
        xv = xmax * i / 5.0
        yv = ymax * i / 5.0
        draw.line([px(0, yv), px(xmax, yv)], fill=(228, 228, 228))
        draw.line([px(xv, 0), px(xv, ymax)], fill=(228, 228, 228))
        draw.text((plot_left - 52, plot_bottom - yv * sy - 6), fmt(yv, ymax), fill=(70, 70, 70))
        draw.text((plot_left + xv * sx - 12, plot_bottom + 8), fmt(xv, xmax), fill=(70, 70, 70))

    if show_identity:
        ex = min(xmax, ymax)
        draw.line([px(0, 0), px(ex, ex)], fill=(185, 185, 185))

    if show_fit:
        draw.line([px(0, 0), px(xmax, k * xmax)], fill=(200, 40, 40), width=3)
    for x, y in pts:
        cx, cy = px(x, y)
        draw.ellipse([cx - 6, cy - 6, cx + 6, cy + 6], fill=(30, 90, 200))

    draw.text((plot_left, y0 + 8), title, fill=(0, 0, 0))
    draw.text((plot_left + 120, plot_bottom + 30), xlabel, fill=(0, 0, 0))
    draw.text((x0, plot_top + 6), ylabel, fill=(0, 0, 0))
    if show_fit:
        draw.text((plot_left + 90, plot_top + 10), "fit: y = %.3f x    R2 = %.4f" % (k, r2), fill=(200, 40, 40))
    if note:
        draw.text((plot_left, plot_bottom + 52), note, fill=(70, 70, 70))


def main():
    run_dir = sys.argv[1]
    rows = collect(run_dir)
    if not rows:
        print("nothing measured")
        return

    grid = [r for r in rows if r["label"].startswith("v")]
    depth = [r for r in rows if r["label"].startswith("depth")]

    for r in grid:
        r["pred"] = (r["u"] - CX) * r["v"] * r["T"] * r["G"] / r["z"]

    k, r2 = fit_through_origin([r["pred"] for r in grid], [r["blur"] for r in grid])
    print("E1 grid (%d conditions, n=%d frames each)" % (len(grid), grid[0]["n"]))
    print("%-14s %5s %8s %6s %8s %9s %8s" % ("label", "v", "T[ms]", "z", "u-cx", "pred_px", "meas_px"))
    for r in sorted(grid, key=lambda q: (q["v"], q["T"])):
        print("%-14s %5.2f %8.2f %6.2f %8.1f %9.2f %8.2f" % (
            r["label"], r["v"], r["T"] * 1000, r["z"], r["u"] - CX, r["pred"], r["blur"]))
    print("\nfit through origin: k = %.4f   R2 = %.4f" % (k, r2))

    if depth:
        print("\nDepth sweep (v fixed, expect blur * z = const):")
        for r in depth:
            print("  z=%.2f  blur=%.2f  blur*z=%.2f" % (r["z"], r["blur"], r["blur"] * r["z"]))

    img = Image.new("RGB", (1500, 640), (255, 255, 255))
    dr = ImageDraw.Draw(img)
    draw_panel(dr, (20, 20, 720, 620),
               [(r["pred"], r["blur"]) for r in grid], k, r2,
               "E1  measured blur vs (u-cx)*v*T*G/z", "predicted length [px]", "measured 10-90 width [px]")

    if depth:
        pts = [(1.0 / r["z"], r["blur"]) for r in depth]
        kd, r2d = fit_through_origin([p[0] for p in pts], [p[1] for p in pts])
        products = [r["blur"] * r["z"] for r in depth]
        draw_panel(dr, (760, 20, 1460, 620), pts, kd, r2d,
                   "E1b  blur vs 1/z  (v = 1.5 m/s, T fixed)", "1 / depth [1/m]", "measured width [px]",
                   show_identity=False,
                   note="blur * z = %.1f / %.1f / %.1f px*m  (z = 1.0 / 2.2 / 4.4 m)" % tuple(products))

    out = os.path.join(run_dir, "e1_result.png")
    img.save(out)
    print("\nfigure:", out)


if __name__ == "__main__":
    main()

