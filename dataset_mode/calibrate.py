"""E2: estimate the effective exposure from real motion-blurred frames.

The idea
--------
A real frame from a walking capture is already blurred by the camera shutter. The blur
length in pixels is  L = |flow per second| * T * G  (T = shutter time). Our geometric
model predicts |flow per second| from the poses, so measuring L in the image yields T*G.

How L is measured
-----------------
Uniform linear blur multiplies the spectrum by a sinc, so the log-spectrum has periodic
zeros and its 1-D cepstrum peaks at the blur length. We rotate each frame so the dominant
flow is horizontal, then look for the first prominent cepstral peak.

Validation
----------
`--validate` applies a *known* exposure to a real frame with our own model, then runs the
estimator on the result. That measures the estimator's bias before it is used on real data.
"""
import argparse
import json
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import degrade as dg


def flow_field(depth, K, pose, prev_pose, depth_limit=None):
    """Per-pixel displacement between two poses, in pixels."""
    h, w = depth.shape
    fx, fy, cx, cy = K
    z = depth.astype(np.float32)
    u = np.arange(w, dtype=np.float32)[None, :]
    v = np.arange(h, dtype=np.float32)[:, None]
    x_cam = (u + 0.5 - cx) / fx * z
    y_cam = (v + 0.5 - cy) / fy * z
    points = np.stack([x_cam, y_cam, z], axis=-1)

    R = pose[:3, :3].astype(np.float32)
    t = pose[:3, 3].astype(np.float32)
    world = points @ R.T + t
    if depth_limit:
        scale = np.minimum(1.0, depth_limit / np.maximum(z, 1e-3))
        world = t + (world - t) * scale[..., None]

    Rp = prev_pose[:3, :3].astype(np.float32)
    tp = prev_pose[:3, 3].astype(np.float32)
    prev_cam = (world - tp) @ Rp
    zp = prev_cam[..., 2]
    ok = zp > 1e-4
    safe = np.maximum(zp, 1e-6)
    up = fx * prev_cam[..., 0] / safe + cx
    vp = fy * prev_cam[..., 1] / safe + cy
    du = (u + 0.5) - up
    dv = (v + 0.5) - vp
    mag = np.hypot(du, dv)
    ang = np.degrees(np.arctan2(dv, du))
    return mag, ang, ok & (z > 0.2)


def dominant_direction(mag, ang, mask):
    """Magnitude-weighted circular mean of the flow direction."""
    m = mag[mask]
    a = np.radians(ang[mask])
    if m.size == 0:
        return 0.0
    w = m - m.min() + 1e-3
    s = float(np.sum(w * np.sin(2 * a)))
    c = float(np.sum(w * np.cos(2 * a)))
    return float(np.degrees(0.5 * np.arctan2(s, c)))


def cepstral_length(gray, angle_deg, min_len=3, max_len=70):
    """Estimate uniform blur length (px) along `angle_deg` from the 1-D cepstrum."""
    img = Image.fromarray(gray.astype(np.uint8))
    rotated = img.rotate(-angle_deg, resample=Image.BILINEAR, expand=True)
    a = np.asarray(rotated, dtype=np.float32)
    h, w = a.shape
    if w < max_len * 3:
        return None

    win = np.hanning(h)[:, None] * np.hanning(w)[None, :]
    spec = np.abs(np.fft.rfft(a * win, axis=1))
    spec = np.log1p(spec)
    # collapse across rows: motion blur affects every row the same way
    profile = spec.mean(axis=0)
    profile -= profile.mean()
    cep = np.abs(np.fft.irfft(profile))

    lo, hi = min_len, min(max_len, len(cep) - 1)
    if hi <= lo:
        return None
    seg = cep[lo:hi]
    idx = int(np.argmax(seg))
    peak = float(seg[idx])
    floor = float(np.median(seg)) + 1e-9
    if peak < 1.8 * floor:          # no clear periodicity -> not measurable
        return None
    return float(lo + idx)


def patch_estimates(gray, mag, ang, mask, trail, size=192, step=96):
    """Run the cepstral estimator on patches whose flow direction is locally uniform."""
    h, w = gray.shape
    results = []
    for y0 in range(0, h - size, step):
        for x0 in range(0, w - size, step):
            m = mask[y0:y0 + size, x0:x0 + size]
            if m.mean() < 0.85:
                continue
            patch = gray[y0:y0 + size, x0:x0 + size]
            if patch.std() < 12.0:
                continue
            a = ang[y0:y0 + size, x0:x0 + size][m]
            wgt = mag[y0:y0 + size, x0:x0 + size][m]
            rad = np.radians(a)
            s = float(np.sum(wgt * np.sin(2 * rad)))
            c = float(np.sum(wgt * np.cos(2 * rad)))
            local_dir = float(np.degrees(0.5 * np.arctan2(s, c)))
            # reject patches where the flow is not coherent (radial spread)
            coherence = float(np.hypot(s, c) / max(np.sum(wgt), 1e-6))
            if coherence < 0.9:
                continue
            L = cepstral_length(patch, local_dir)
            if L is None:
                continue
            t = trail[y0:y0 + size, x0:x0 + size][m]
            results.append({"length_px": L, "trail_px": float(np.median(t)),
                            "direction": local_dir, "y": y0, "x": x0})
    return results


def analyse(run_dir, limit=40, sample=40):
    meta, frames = dg.load_sequence(run_dir)
    K = meta["intrinsics"]
    fps = float(meta.get("fps", 30.0))
    dt = 1.0 / fps

    rows = []
    for i in range(1, min(len(frames), limit)):
        fr = frames[i]
        rgb = dg.load_rgb(fr["rgb"])
        depth = dg.fill_depth_holes(dg.load_depth(fr["depth"]))
        mag, ang, mask = flow_field(depth, K, fr["c2w"], frames[i - 1]["c2w"])
        if mask.sum() < 0.2 * mask.size:
            continue
        gray = rgb.mean(axis=2) * 255.0
        # trail for a unit effective exposure (T*G = dt) equals the inter-frame flow
        patches = patch_estimates(gray, mag, ang, mask, mag)
        for pt in patches:
            pt["frame"] = i
        rows.extend(patches)

    if not rows:
        print("no measurable frames")
        return []

    print("%6s %5s %5s %8s %8s %9s" % ("frame", "x", "y", "dir", "L_est", "flow_px"))
    for r in rows[:20]:
        print("%6d %5d %5d %8.1f %8.2f %9.2f" % (r["frame"], r["x"], r["y"], r["direction"], r["length_px"], r["trail_px"]))
    print("... %d patches total" % len(rows))
    return rows


def fit_exposure(rows, gain):
    """L = median_flow * T * G / dt  ->  T = L * dt / (median_flow * G)."""
    ts = []
    for r in rows:
        if r["trail_px"] > 0.5:
            ts.append(r["length_px"] * (1.0 / 30.0) / (r["trail_px"] * gain))
    if not ts:
        return None
    ts = np.asarray(ts)
    return {"median_ms": float(np.median(ts) * 1000.0),
            "iqr_ms": float((np.percentile(ts, 75) - np.percentile(ts, 25)) * 1000.0),
            "n": int(ts.size)}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("run_dir")
    ap.add_argument("--limit", type=int, default=30)
    ap.add_argument("--gain", type=float, default=2.2)
    ap.add_argument("--validate", action="store_true")
    args = ap.parse_args()

    if args.validate:
        meta, frames = dg.load_sequence(args.run_dir)
        K = meta["intrinsics"]
        dt = 1.0 / float(meta.get("fps", 30.0))
        print("validation: apply a known exposure, then try to recover it\n")
        for known_ms in (10, 15, 20, 30):
            fr = frames[10]
            rgb = dg.load_rgb(fr["rgb"])
            depth = dg.fill_depth_holes(dg.load_depth(fr["depth"]))
            out, trail = dg.degrade_frame(rgb, depth, K, fr["c2w"], frames[9]["c2w"],
                                          known_ms / 1000.0, dt, motion_gain=args.gain)
            mag, ang, mask = flow_field(depth, K, fr["c2w"], frames[9]["c2w"])
            gray = out.mean(axis=2) * 255.0
            length = np.hypot(trail[..., 0], trail[..., 1])
            pts = patch_estimates(gray, mag, ang, mask, length)
            if pts:
                est = float(np.median([p["length_px"] for p in pts]))
                pred = float(np.median([p["trail_px"] for p in pts]))
                print("  exp=%2d ms   predicted L=%.2f px   cepstrum L=%.2f px   (n=%d patches)"
                      % (known_ms, pred, est, len(pts)))
            else:
                print("  exp=%2d ms   no coherent patches" % known_ms)
        return

    rows = analyse(args.run_dir, limit=args.limit)
    fit = fit_exposure(rows, args.gain)
    if fit:
        print("\nimplied exposure (gain=%.1f): %.1f ms  (IQR %.1f ms, n=%d)"
              % (args.gain, fit["median_ms"], fit["iqr_ms"], fit["n"]))
        with open(os.path.join(args.run_dir, "calibration.json"), "w", encoding="utf-8") as fh:
            json.dump({"gain": args.gain, "rows": rows, "fit": fit}, fh, indent=2)
        print("written:", os.path.join(args.run_dir, "calibration.json"))


if __name__ == "__main__":
    main()



