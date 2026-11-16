"""Motion2RGB reference degradation, outside Unity.

This mirrors the shader in Assets/Motion2RGB/Shaders/DirectionalMotionBlur.shader
so the same motion-state model can be applied to any RGB-D sequence with poses,
instead of only to the Unity scene:

    world   = R * (x, y, z) + t                       # camera-to-world
    uv_prev = K * (R_prev^T * (world - t_prev))       # reproject into previous view
    trail   = (uv - uv_prev) * T * G / dt             # pixels travelled during exposure
    out     = weighted average of `taps` samples along the trail

Weights follow the shader: depth-difference gate x 1/(1 + rel * depth_sensitivity),
plus a 3x3 neighbourhood clamp that stops foreground colour leaking into the background.

Dependencies: numpy + Pillow only.
"""
from __future__ import annotations

import json
import os

import numpy as np
from PIL import Image


# ----------------------------------------------------------------------------- helpers

def _smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def _bilinear_pair(rgb, depth, u, v):
    """Sample colour and depth at the same float coordinates in one pass.

    Kept separate from _sample_bilinear because the tap loop needs both, and computing
    the index arithmetic twice was the dominant cost of the whole pipeline.
    """
    h, w = depth.shape
    u0f = np.floor(u)
    v0f = np.floor(v)
    fu = (u - u0f).astype(np.float32)
    fv = (v - v0f).astype(np.float32)
    u0 = u0f.astype(np.int32)
    v0 = v0f.astype(np.int32)

    valid = (u0 >= 0) & (u0 < w - 1) & (v0 >= 0) & (v0 < h - 1)
    np.clip(u0, 0, w - 2, out=u0)
    np.clip(v0, 0, h - 2, out=v0)

    idx = v0 * w + u0
    idx_r = idx + w

    depth_flat = depth.reshape(-1)
    d00 = depth_flat[idx]
    d01 = depth_flat[idx + 1]
    d10 = depth_flat[idx_r]
    d11 = depth_flat[idx_r + 1]

    fu_d = fu
    fv_d = fv
    top_d = d00 + (d01 - d00) * fu_d
    bot_d = d10 + (d11 - d10) * fu_d
    sample_depth = top_d + (bot_d - top_d) * fv_d

    rgb_flat = rgb.reshape(-1, 3)
    c00 = rgb_flat[idx]
    c01 = rgb_flat[idx + 1]
    c10 = rgb_flat[idx_r]
    c11 = rgb_flat[idx_r + 1]

    fu_c = fu[..., None]
    fv_c = fv[..., None]
    top_c = c00 + (c01 - c00) * fu_c
    bot_c = c10 + (c11 - c10) * fu_c
    sample_colour = top_c + (bot_c - top_c) * fv_c

    return sample_colour, sample_depth, valid


def _sample_bilinear(img, u, v):
    """Sample img (H,W) or (H,W,C) at float pixel coordinates (u, v)."""
    h, w = img.shape[:2]
    u0 = np.floor(u).astype(np.int32)
    v0 = np.floor(v).astype(np.int32)
    u1, v1 = u0 + 1, v0 + 1
    valid = (u0 >= 0) & (u1 < w) & (v0 >= 0) & (v1 < h)

    u0c = np.clip(u0, 0, w - 1)
    u1c = np.clip(u1, 0, w - 1)
    v0c = np.clip(v0, 0, h - 1)
    v1c = np.clip(v1, 0, h - 1)

    fu = (u - u0)
    fv = (v - v0)
    if img.ndim == 3:
        fu = fu[..., None]
        fv = fv[..., None]

    a = img[v0c, u0c]
    b = img[v0c, u1c]
    c = img[v1c, u0c]
    d = img[v1c, u1c]
    top = a * (1.0 - fu) + b * fu
    bottom = c * (1.0 - fu) + d * fu
    return top * (1.0 - fv) + bottom * fv, valid


def _neighbourhood_extremes(img):
    """3x3 min / max for every pixel (same taps as NeighborhoodMin/Max in the shader)."""
    pad = np.pad(img, ((1, 1), (1, 1), (0, 0)) if img.ndim == 3 else ((1, 1), (1, 1)), mode="edge")
    h, w = img.shape[:2]
    stack_min = None
    stack_max = None
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            sl = pad[1 + dy:1 + dy + h, 1 + dx:1 + dx + w]
            stack_min = sl.copy() if stack_min is None else np.minimum(stack_min, sl)
            stack_max = sl.copy() if stack_max is None else np.maximum(stack_max, sl)
    return stack_min, stack_max


# ----------------------------------------------------------------------------- core

def fill_depth_holes(depth, iterations=24, min_depth=1e-3):
    """Fill invalid (<=0) depth pixels by iterative neighbour averaging.

    Real RGB-D sequences (especially walking captures) have 50 %+ missing depth.
    Without filling, those pixels stay sharp while everything else blurs, which
    looks like patchwork instead of motion blur.
    """
    d = depth.astype(np.float32).copy()
    valid = d > min_depth
    if valid.all():
        return d
    for _ in range(iterations):
        if valid.all():
            break
        pd = np.pad(np.where(valid, d, 0.0), 1, mode="edge")
        pv = np.pad(valid.astype(np.float32), 1, mode="constant")
        acc = (pd[:-2, 1:-1] * pv[:-2, 1:-1] + pd[2:, 1:-1] * pv[2:, 1:-1]
               + pd[1:-1, :-2] * pv[1:-1, :-2] + pd[1:-1, 2:] * pv[1:-1, 2:])
        cnt = pv[:-2, 1:-1] + pv[2:, 1:-1] + pv[1:-1, :-2] + pv[1:-1, 2:]
        fill = (~valid) & (cnt > 0)
        d[fill] = acc[fill] / cnt[fill]
        valid = valid | fill
    return d


def degrade_frame(rgb, depth, K, T_c2w, T_prev, exposure_seconds, dt,
                  motion_gain=2.2, taps=20, max_trail_px=90.0,
                  depth_sensitivity=3.5, trail_strength=0.85,
                  depth_limit=None, neighbourhood_blend=0.35,
                  depth_reject=0.35):
    """Apply one exposure of depth-aware motion blur.

    rgb              float32 (H, W, 3) in 0..1
    depth            float32 (H, W) in metres (camera z)
    K                (fx, fy, cx, cy)
    T_c2w, T_prev    4x4 camera-to-world matrices for this frame and the previous one
    exposure_seconds shutter time; dt is the frame interval
    depth_limit      optional artistic clamp on the reconstruction distance (Unity default 8 m)
    """
    h, w = depth.shape
    fx, fy, cx, cy = K
    z = depth.astype(np.float32)
    rgb = rgb.astype(np.float32)

    v_index = np.arange(h, dtype=np.float32)[:, None]
    u_index = np.arange(w, dtype=np.float32)[None, :]
    x_cam = (u_index + 0.5 - cx) / fx * z
    y_cam = (v_index + 0.5 - cy) / fy * z
    points = np.stack([x_cam, y_cam, z], axis=-1)

    R = T_c2w[:3, :3].astype(np.float32)
    t = T_c2w[:3, 3].astype(np.float32)
    world = points @ R.T + t

    if depth_limit is not None and depth_limit > 0:
        scale = np.minimum(1.0, depth_limit / np.maximum(z, 1e-3))
        world = t + (world - t) * scale[..., None]

    Rp = T_prev[:3, :3].astype(np.float32)
    tp = T_prev[:3, 3].astype(np.float32)
    prev_cam = (world - tp) @ Rp
    zp = prev_cam[..., 2]
    ok = zp > 1e-4

    safe_z = np.maximum(zp, 1e-6)
    u_prev = fx * prev_cam[..., 0] / safe_z + cx
    v_prev = fy * prev_cam[..., 1] / safe_z + cy

    u_now = u_index + 0.5
    v_now = v_index + 0.5
    exposure_fraction = exposure_seconds * motion_gain / max(dt, 1e-6)

    trail = np.empty((h, w, 2), dtype=np.float32)
    trail[..., 0] = (u_now - u_prev) * exposure_fraction
    trail[..., 1] = (v_now - v_prev) * exposure_fraction
    length = np.hypot(trail[..., 0], trail[..., 1])
    clamp = np.minimum(1.0, max_trail_px / np.maximum(length, 1e-4))
    trail *= clamp[..., None]
    # invalid depth (no return) or points behind the previous camera: no physical trail
    trail[~(ok & (z > 1e-3))] = 0.0

    n_min, n_max = _neighbourhood_extremes(rgb)
    accumulator = rgb.copy()
    total = np.ones((h, w), dtype=np.float32)

    for k in range(1, taps + 1):
        f = k / float(taps)
        # su/sv are continuous image coordinates (pixel centre i sits at i + 0.5);
        # the bilinear sampler wants array coordinates where pixel i sits at i.
        su = u_now + trail[..., 0] * f - 0.5
        sv = v_now + trail[..., 1] * f - 0.5

        colour, sampled_z, valid = _bilinear_pair(rgb, z, su, sv)

        rel = np.abs(sampled_z - z) / np.maximum(z, 0.25)
        edge = 1.0 - _smoothstep(depth_reject * 0.5, depth_reject, rel)
        gate = np.maximum(0.35, edge)
        weight = gate / (1.0 + rel * depth_sensitivity)
        weight *= valid
        weight *= ok

        clamped = np.clip(colour, n_min, n_max)
        colour += (clamped - colour) * neighbourhood_blend

        accumulator += colour * weight[..., None]
        total += weight

    blurred = accumulator / total[..., None]
    out = rgb * (1.0 - trail_strength) + blurred * trail_strength
    return np.clip(out, 0.0, 1.0), trail


# ----------------------------------------------------------------------------- I/O

def load_depth(path):
    """Depth as metres. Supports .npy (float metres) and 16-bit PNG (millimetres)."""
    if path.endswith(".npy"):
        return np.load(path).astype(np.float32)
    img = np.asarray(Image.open(path))
    if img.dtype == np.uint16:
        return img.astype(np.float32) / 1000.0
    return img.astype(np.float32)


def load_rgb(path):
    return np.asarray(Image.open(path).convert("RGB"), dtype=np.float32) / 255.0


def load_sequence(seq_dir):
    """Read meta.json describing a sequence.

    {
      "fps": 60, "intrinsics": [fx, fy, cx, cy], "exposure_seconds": 0.0396,
      "motion_gain": 2.2,
      "frames": [{"rgb": "rgb/0000.png", "depth": "depth/0000.npy",
                  "c2w": [[...],[...],[...],[...]]}, ...]
    }
    """
    with open(os.path.join(seq_dir, "meta.json"), encoding="utf-8") as fh:
        meta = json.load(fh)
    frames = []
    for item in meta["frames"]:
        frames.append({
            "rgb": os.path.join(seq_dir, item["rgb"]),
            "depth": os.path.join(seq_dir, item["depth"]),
            "c2w": np.asarray(item["c2w"], dtype=np.float32).reshape(4, 4),
        })
    return meta, frames


def _render_one(job):
    """Worker entry point: degrade one frame and write it. Picklable on purpose."""
    index, rgb_path, depth_path, pose, prev_pose, out_dir, K, exposure, dt, gain, limit, fill = job
    rgb = load_rgb(rgb_path)
    depth = load_depth(depth_path)
    if fill:
        depth = fill_depth_holes(depth)
    out, trail = degrade_frame(rgb, depth, K,
                               np.asarray(pose, dtype=np.float32).reshape(4, 4),
                               np.asarray(prev_pose, dtype=np.float32).reshape(4, 4),
                               exposure, dt, motion_gain=gain, depth_limit=limit)
    Image.fromarray((out * 255.0 + 0.5).astype(np.uint8)).save(
        os.path.join(out_dir, "%04d.png" % index))
    length = np.hypot(trail[..., 0], trail[..., 1])
    return {"frame": index,
            "mean_trail_px": float(np.mean(length)),
            "max_trail_px": float(np.max(length))}


def degrade_sequence(seq_dir, out_dir, verbose=True, workers=1):
    meta, frames = load_sequence(seq_dir)
    os.makedirs(out_dir, exist_ok=True)
    K = meta["intrinsics"]
    fps = float(meta.get("fps", 60.0))
    dt = 1.0 / fps
    exposure = float(meta["exposure_seconds"])
    gain = float(meta.get("motion_gain", 2.2))
    limit = meta.get("depth_limit", None)
    fill = bool(meta.get("fill_depth_holes", True))

    jobs = []
    for i, fr in enumerate(frames):
        prev = frames[i - 1]["c2w"] if i > 0 else fr["c2w"]
        jobs.append((i, fr["rgb"], fr["depth"], fr["c2w"].tolist(), prev.tolist(),
                     out_dir, K, exposure, dt, gain, limit, fill))

    if workers and workers > 1:
        from concurrent.futures import ProcessPoolExecutor
        records = []
        with ProcessPoolExecutor(max_workers=workers) as pool:
            for rec in pool.map(_render_one, jobs, chunksize=max(1, len(jobs) // (workers * 4))):
                records.append(rec)
                if verbose and rec["frame"] % 20 == 0:
                    print("  frame %04d  mean trail %.1f px" % (rec["frame"], rec["mean_trail_px"]))
    else:
        records = []
        for job in jobs:
            rec = _render_one(job)
            records.append(rec)
            if verbose:
                print("  frame %04d  mean trail %.1f px" % (rec["frame"], rec["mean_trail_px"]))

    records.sort(key=lambda r: r["frame"])
    with open(os.path.join(out_dir, "degrade_log.json"), "w", encoding="utf-8") as fh:
        json.dump({"sequence": os.path.basename(seq_dir.rstrip("/\\")),
                   "exposure_seconds": exposure, "motion_gain": gain,
                   "workers": workers, "frames": records}, fh, indent=2)
    return records


if __name__ == "__main__":
    import sys
    if len(sys.argv) < 3:
        print("usage: python degrade.py <sequence_dir> <out_dir> [workers]")
        raise SystemExit(1)
    n = int(sys.argv[3]) if len(sys.argv) > 3 else 1
    degrade_sequence(sys.argv[1], sys.argv[2], workers=n)

