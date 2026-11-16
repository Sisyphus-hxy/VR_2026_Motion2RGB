"""Image-quality metrics for comparing clean and degraded sequences.

Pure numpy + Pillow. SSIM uses a box window implemented with integral images, which is
fast enough to score a full sequence without scipy.

usage: python metrics.py <clean_dir> <degraded_dir> [--csv out.csv]
"""
import argparse
import glob
import json
import os

import numpy as np
from PIL import Image


def _gray(path):
    return np.asarray(Image.open(path).convert("L"), dtype=np.float32)


def psnr(a, b, peak=255.0):
    mse = float(np.mean((a - b) ** 2))
    if mse <= 1e-12:
        return float("inf")
    return 10.0 * np.log10(peak * peak / mse)


def _box(img, r):
    """Mean over a (2r+1)^2 window via an integral image."""
    h, w = img.shape
    pad = np.pad(img, r + 1, mode="edge")
    integral = pad.cumsum(0).cumsum(1)
    # window sums aligned to the image grid
    k = 2 * r + 1
    s = (integral[k:k + h, k:k + w]
         - integral[0:h, k:k + w]
         - integral[k:k + h, 0:w]
         + integral[0:h, 0:w])
    return s / (k * k)


def ssim(a, b, r=4, data_range=255.0):
    a = a.astype(np.float64)
    b = b.astype(np.float64)
    c1 = (0.01 * data_range) ** 2
    c2 = (0.03 * data_range) ** 2
    mu_a = _box(a, r)
    mu_b = _box(b, r)
    aa = _box(a * a, r) - mu_a * mu_a
    bb = _box(b * b, r) - mu_b * mu_b
    ab = _box(a * b, r) - mu_a * mu_b
    num = (2 * mu_a * mu_b + c1) * (2 * ab + c2)
    den = (mu_a ** 2 + mu_b ** 2 + c1) * (aa + bb + c2)
    return float(np.mean(num / den))


def grad_energy(gray):
    gx = np.abs(np.diff(gray, axis=1)).mean()
    gy = np.abs(np.diff(gray, axis=0)).mean()
    return float(gx + gy)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("clean")
    ap.add_argument("degraded")
    ap.add_argument("--csv", default="")
    ap.add_argument("--limit", type=int, default=0)
    args = ap.parse_args()

    clean_files = sorted(glob.glob(os.path.join(args.clean, "*.png")))
    deg_files = sorted(glob.glob(os.path.join(args.degraded, "*.png")))
    if args.limit:
        clean_files, deg_files = clean_files[:args.limit], deg_files[:args.limit]
    n = min(len(clean_files), len(deg_files))
    if n == 0:
        raise SystemExit("no matching frames")

    rows = []
    for i in range(n):
        a = _gray(clean_files[i])
        b = _gray(deg_files[i])
        if a.shape != b.shape:
            raise SystemExit("size mismatch at frame %d: %s vs %s" % (i, a.shape, b.shape))
        ea, eb = grad_energy(a), grad_energy(b)
        rows.append({"frame": i, "psnr": psnr(a, b), "ssim": ssim(a, b),
                     "grad_clean": ea, "grad_degraded": eb,
                     "grad_ratio": eb / ea if ea > 0 else 0.0})

    psnrs = [r["psnr"] for r in rows if np.isfinite(r["psnr"])]
    print("frames: %d" % n)
    print("PSNR  mean %.2f dB   min %.2f   max %.2f" % (np.mean(psnrs), np.min(psnrs), np.max(psnrs)))
    print("SSIM  mean %.4f      min %.4f" % (np.mean([r["ssim"] for r in rows]), np.min([r["ssim"] for r in rows])))
    print("gradient energy ratio (degraded/clean)  mean %.3f" % np.mean([r["grad_ratio"] for r in rows]))
    print("\n%6s %8s %7s %8s" % ("frame", "PSNR", "SSIM", "grad%"))
    for r in rows[:8]:
        print("%6d %8.2f %7.4f %7.0f%%" % (r["frame"], r["psnr"], r["ssim"], 100 * r["grad_ratio"]))
    if n > 8:
        print("...")

    if args.csv:
        with open(args.csv, "w", encoding="utf-8", newline="") as fh:
            fh.write("frame,psnr,ssim,grad_clean,grad_degraded,grad_ratio\n")
            for r in rows:
                fh.write("%d,%.4f,%.5f,%.4f,%.4f,%.5f\n" % (
                    r["frame"], r["psnr"], r["ssim"], r["grad_clean"], r["grad_degraded"], r["grad_ratio"]))
        print("\nwritten:", args.csv)


if __name__ == "__main__":
    main()
