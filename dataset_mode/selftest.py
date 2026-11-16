"""Self-test for the dataset-mode degradation.

Renders one frontal plane at a time (filling the frame, single vertical step edge
in the middle) and checks the three properties the Unity implementation should
have:

  1. no motion            -> output equals the input
  2. constant translation -> blur scales with 1/z
  3. pure rotation        -> blur is independent of z

Run:  python selftest.py
"""
import json
import os

import numpy as np
from PIL import Image

import degrade as dg

W, H = 960, 540
FX = FY = 700.0
CX, CY = W / 2.0, H / 2.0
FPS = 60.0
DT = 1.0 / FPS
EXPOSURE = 0.0396
GAIN = 2.2
CAMERA_Z = -3.0
BG_DEPTH = 30.0
DISTANCES = (2.0, 4.0, 7.0)
OUT_ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_selftest")


def render(dist, camera_pose):
    """One frontal plane at `dist` metres, vertical step edge at x = 0."""
    R = camera_pose[:3, :3]
    t = camera_pose[:3, 3]

    u = np.arange(W, dtype=np.float32)[None, :]
    v = np.arange(H, dtype=np.float32)[:, None]
    dirs = np.stack([
        (u + 0.5 - CX) / FX * np.ones_like(v),
        (v + 0.5 - CY) / FY * np.ones_like(u),
        np.ones((H, W), dtype=np.float32),
    ], axis=-1)
    wd = dirs @ R.T

    z_world = CAMERA_Z + dist
    scale = (z_world - t[2]) / np.maximum(wd[..., 2], 1e-6)
    px = t[0] + wd[..., 0] * scale
    py = t[1] + wd[..., 1] * scale

    half_x = 0.75 * dist
    half_y = 0.45 * dist
    hit = (wd[..., 2] > 1e-6) & (scale > 0) & (np.abs(px) < half_x) & (np.abs(py) < half_y)

    depth = np.where(hit, scale, BG_DEPTH).astype(np.float32)
    shade = np.where(px < 0.3 * dist, 0.15, 0.95).astype(np.float32)
    rgb = np.full((H, W, 3), 0.06, dtype=np.float32)
    for c in range(3):
        rgb[..., c] = np.where(hit, shade, rgb[..., c])
    return rgb, depth


def c2w(dz=0.0, yaw_deg=0.0):
    yaw = np.deg2rad(yaw_deg)
    c, s = np.cos(yaw), np.sin(yaw)
    m = np.eye(4, dtype=np.float32)
    m[0, 0], m[0, 2] = c, s
    m[2, 0], m[2, 2] = -s, c
    m[2, 3] = CAMERA_Z + dz
    return m


def edge_width(profile):
    centre = len(profile) // 2
    span = range(max(1, centre - 80), min(len(profile) - 2, centre + 80))
    edge = max(span, key=lambda i: abs(profile[i + 1] - profile[i]))
    left = float(np.mean(profile[2:14]))
    right = float(np.mean(profile[-14:-2]))
    hi, lo = max(left, right), min(left, right)
    if hi - lo < 20:
        return None
    hi_l = lo + 0.9 * (hi - lo)
    lo_l = lo + 0.1 * (hi - lo)

    def crossing(level, start, stop, step):
        for i in range(start, stop, step):
            a, b = profile[i], profile[i + 1]
            if (a - level) * (b - level) <= 0:
                return float(i) + (level - a) / (b - a) if abs(b - a) > 1e-6 else float(i)
        return None

    if right > left:
        x_lo = crossing(lo_l, edge, 1, -1)
        x_hi = crossing(hi_l, edge, len(profile) - 2, 1)
    else:
        x_hi = crossing(hi_l, edge, 1, -1)
        x_lo = crossing(lo_l, edge, len(profile) - 2, 1)
    if x_hi is None or x_lo is None:
        return None
    return abs(x_hi - x_lo)


def u_step(dist):
    """Screen column of the step edge: fixed angle, so it stays comparable across depths."""
    return CX + FX * 0.3


def width_at_edge(gray, dist, half=120):
    u = int(u_step(dist))
    prof = gray[int(CY) - 10: int(CY) + 10, u - half: u + half].mean(axis=0)
    return edge_width(prof)


def main():
    os.makedirs(OUT_ROOT, exist_ok=True)
    clean_widths = {}
    for dist in DISTANCES:
        rgb, _ = render(dist, c2w())
        clean_widths[dist] = width_at_edge(rgb.mean(axis=2) * 255.0, dist)

    rgb0, depth0 = render(2.0, c2w())

    print("\n[1] static camera")
    static, _ = dg.degrade_frame(rgb0, depth0, [FX, FY, CX, CY], c2w(), c2w(), EXPOSURE, DT, motion_gain=GAIN)
    diff = float(np.max(np.abs(static - rgb0)))
    print("    max |output - input| = %.3e   %s" % (diff, "PASS" if diff < 1e-3 else "FAIL"))

    print("\n[2] constant translation 1.5 m/s  (expect blur * z const)")
    products = []
    for dist in DISTANCES:
        rgb, depth = render(dist, c2w())
        out, _ = dg.degrade_frame(rgb, depth, [FX, FY, CX, CY], c2w(), c2w(dz=-1.5 * DT),
                                  EXPOSURE, DT, motion_gain=GAIN)
        wc = clean_widths[dist]
        wd = width_at_edge(out.mean(axis=2) * 255.0, dist)
        blur = float(np.sqrt(max(0.0, wd * wd - wc * wc)))
        products.append(blur * dist)
        print("    z=%.1f m   clean %.2f px   degraded %.2f px   blur %.2f px   blur*z %.2f"
              % (dist, wc, wd, blur, blur * dist))
    spread = 100.0 * (max(products) - min(products)) / max(np.mean(products), 1e-6)
    print("    spread of blur*z = %.1f%%   %s" % (spread, "PASS" if spread < 15 else "FAIL"))

    print("\n[3] pure rotation 30 deg/s  (expect depth independent)")
    blurs = []
    for dist in DISTANCES:
        rgb, depth = render(dist, c2w())
        out, _ = dg.degrade_frame(rgb, depth, [FX, FY, CX, CY], c2w(), c2w(yaw_deg=-30.0 * DT),
                                  EXPOSURE, DT, motion_gain=GAIN)
        wc = clean_widths[dist]
        wd = width_at_edge(out.mean(axis=2) * 255.0, dist)
        blur = float(np.sqrt(max(0.0, wd * wd - wc * wc)))
        blurs.append(blur)
        print("    z=%.1f m   clean %.2f px   degraded %.2f px   blur %.2f px" % (dist, wc, wd, blur))
    spread = 100.0 * (max(blurs) - min(blurs)) / max(np.mean(blurs), 1e-6)
    print("    spread across depths = %.1f%%   %s" % (spread, "PASS" if spread < 15 else "FAIL"))

    # write a short sequence in the dataset format so degrade.py CLI has an example
    seq_dir = os.path.join(OUT_ROOT, "sequence")
    os.makedirs(os.path.join(seq_dir, "rgb"), exist_ok=True)
    os.makedirs(os.path.join(seq_dir, "depth"), exist_ok=True)
    frames = []
    for i, dz in enumerate([0.0, 1.5 * DT, 3.0 * DT]):
        rgb, depth = render(4.0, c2w(dz=dz))
        Image.fromarray((rgb * 255).astype(np.uint8)).save(os.path.join(seq_dir, "rgb", "%04d.png" % i))
        np.save(os.path.join(seq_dir, "depth", "%04d.npy" % i), depth)
        frames.append({"rgb": "rgb/%04d.png" % i, "depth": "depth/%04d.npy" % i, "c2w": c2w(dz=dz).tolist()})
    with open(os.path.join(seq_dir, "meta.json"), "w", encoding="utf-8") as fh:
        json.dump({"fps": FPS, "intrinsics": [FX, FY, CX, CY], "exposure_seconds": EXPOSURE,
                   "motion_gain": GAIN, "frames": frames}, fh, indent=2)

    rgb, depth = render(4.0, c2w())
    out, _ = dg.degrade_frame(rgb, depth, [FX, FY, CX, CY], c2w(), c2w(dz=-1.5 * DT), EXPOSURE, DT, motion_gain=GAIN)
    canvas = Image.new("RGB", (W, H * 2 + 8), (0, 0, 0))
    canvas.paste(Image.fromarray((rgb * 255).astype(np.uint8)), (0, 0))
    canvas.paste(Image.fromarray((out * 255).astype(np.uint8)), (0, H + 8))
    canvas.save(os.path.join(OUT_ROOT, "selftest.png"))
    print("\nfigure: %s" % os.path.join(OUT_ROOT, "selftest.png"))
    print("sequence: %s" % seq_dir)


if __name__ == "__main__":
    main()

