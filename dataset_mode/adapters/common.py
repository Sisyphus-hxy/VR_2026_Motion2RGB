"""Shared helpers for the TUM / Replica adapters."""
from __future__ import annotations

import json
import os

import numpy as np


def quat_to_matrix(qx, qy, qz, qw):
    """(x, y, z, w) quaternion -> 3x3 rotation matrix."""
    n = np.sqrt(qx * qx + qy * qy + qz * qz + qw * qw)
    if n < 1e-12:
        return np.eye(3, dtype=np.float64)
    x, y, z, w = qx / n, qy / n, qz / n, qw / n
    return np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
    ], dtype=np.float64)


def c2w(tx, ty, tz, qx, qy, qz, qw):
    m = np.eye(4, dtype=np.float64)
    m[:3, :3] = quat_to_matrix(qx, qy, qz, qw)
    m[:3, 3] = (tx, ty, tz)
    return m


def read_index(path):
    """Read a TUM-style '<timestamp> <relative path>' index file."""
    entries = {}
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            parts = line.split()
            if len(parts) < 2:
                continue
            entries[float(parts[0])] = parts[1]
    return entries


def read_trajectory(path):
    """Read '<timestamp> tx ty tz qx qy qz qw' lines."""
    entries = {}
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            parts = [p for p in line.replace(",", " ").split() if p]
            if len(parts) < 8:
                continue
            ts = float(parts[0])
            entries[ts] = c2w(*[float(v) for v in parts[1:8]])
    return entries


def nearest(timestamp, candidates, max_diff):
    """Nearest timestamp within max_diff, else None."""
    if not candidates:
        return None
    best = min(candidates, key=lambda t: abs(t - timestamp))
    return best if abs(best - timestamp) <= max_diff else None


def median_dt(timestamps):
    if len(timestamps) < 2:
        return 0.0333
    diffs = np.diff(sorted(timestamps))
    diffs = diffs[diffs > 1e-6]
    return float(np.median(diffs)) if diffs.size else 0.0333


def save_depth_png_to_npy(src_png, dst_npy, scale):
    """16-bit depth PNG (millimetres or arbitrary scale) -> float metres .npy."""
    from PIL import Image
    raw = np.asarray(Image.open(src_png))
    depth = raw.astype(np.float32) / float(scale)
    np.save(dst_npy, depth)
    return depth


def write_meta(out_dir, frames, intrinsics, fps, exposure, gain, depth_limit=None, extra=None):
    os.makedirs(out_dir, exist_ok=True)
    meta = {
        "fps": fps,
        "intrinsics": [float(v) for v in intrinsics],
        "exposure_seconds": exposure,
        "motion_gain": gain,
        "depth_limit": depth_limit,
        "frames": frames,
    }
    if extra:
        meta.update(extra)
    with open(os.path.join(out_dir, "meta.json"), "w", encoding="utf-8") as fh:
        json.dump(meta, fh, indent=2)
    return meta
