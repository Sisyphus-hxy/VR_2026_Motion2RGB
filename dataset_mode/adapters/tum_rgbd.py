"""Convert a TUM RGB-D sequence into the dataset-mode format.

Expected layout (the usual `rgbd_dataset_freiburg3_*` folder):

    rgb.txt            # <timestamp> rgb/<timestamp>.png
    depth.txt          # <timestamp> depth/<timestamp>.png
    groundtruth.txt    # <timestamp> tx ty tz qx qy qz qw
    rgb/ depth/

Usage:
    python tum_rgbd.py --src D:\\data\\rgbd_dataset_freiburg3_walking_xyz --out runs\\tum_fr3
"""
from __future__ import annotations

import argparse
import os
import shutil
import sys

import common


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True, help="TUM sequence folder")
    ap.add_argument("--out", required=True, help="output folder for meta.json + depth .npy")
    ap.add_argument("--intrinsics", nargs=4, type=float, default=[535.4, 539.2, 320.1, 247.6],
                    help="fx fy cx cy (default: freiburg3)")
    ap.add_argument("--depth-scale", type=float, default=5000.0,
                    help="raw depth units per metre (TUM ships 5000)")
    ap.add_argument("--exposure", type=float, default=0.02, help="shutter seconds (TUM does not record it)")
    ap.add_argument("--gain", type=float, default=2.2)
    ap.add_argument("--max-diff", type=float, default=0.02, help="association tolerance in seconds")
    ap.add_argument("--stride", type=int, default=1)
    ap.add_argument("--start", type=int, default=0, help="skip the first N selected frames")
    ap.add_argument("--max-frames", type=int, default=0, help="0 = all")
    ap.add_argument("--copy-rgb", action="store_true", help="copy RGB into the output folder")
    args = ap.parse_args()

    rgb_index = common.read_index(os.path.join(args.src, "rgb.txt"))
    depth_index = common.read_index(os.path.join(args.src, "depth.txt"))
    gt_path = os.path.join(args.src, "groundtruth.txt")
    if not os.path.isfile(gt_path):
        sys.exit("groundtruth.txt not found in %s" % args.src)
    gt = common.read_trajectory(gt_path)

    rgb_ts = sorted(rgb_index)
    depth_ts = sorted(depth_index)
    gt_ts = sorted(gt)

    chosen = rgb_ts[::max(1, args.stride)]
    if args.start > 0:
        chosen = chosen[args.start:]
    if args.max_frames > 0:
        chosen = chosen[:args.max_frames]

    os.makedirs(args.out, exist_ok=True)
    depth_out = os.path.join(args.out, "depth")
    rgb_out = os.path.join(args.out, "rgb")
    os.makedirs(depth_out, exist_ok=True)
    if args.copy_rgb:
        os.makedirs(rgb_out, exist_ok=True)

    frames = []
    skipped = 0
    for i, ts in enumerate(chosen):
        d_ts = common.nearest(ts, depth_ts, args.max_diff)
        g_ts = common.nearest(ts, gt_ts, args.max_diff)
        if d_ts is None or g_ts is None:
            skipped += 1
            continue

        depth_src = os.path.join(args.src, depth_index[d_ts])
        depth_dst = os.path.join(depth_out, "%06d.npy" % i)
        common.save_depth_png_to_npy(depth_src, depth_dst, args.depth_scale)

        rgb_src = os.path.join(args.src, rgb_index[ts])
        if args.copy_rgb:
            rgb_dst = os.path.join(rgb_out, "%06d.png" % i)
            shutil.copyfile(rgb_src, rgb_dst)
        else:
            rgb_dst = os.path.abspath(rgb_src)

        frames.append({
            "rgb": rgb_dst,
            "depth": depth_dst,
            "c2w": gt[g_ts].tolist(),
            "source_timestamp": ts,
        })

    if not frames:
        sys.exit("no associated frames; relax --max-diff or check the folder layout")

    fps = 1.0 / common.median_dt([f["source_timestamp"] for f in frames])
    common.write_meta(args.out, frames, args.intrinsics, fps, args.exposure, args.gain,
                      depth_limit=None,
                      extra={"source": os.path.abspath(args.src), "dataset": "tum-rgbd",
                             "depth_scale": args.depth_scale})
    print("TUM -> %s" % args.out)
    print("  frames: %d  (skipped %d, stride %d)" % (len(frames), skipped, args.stride))
    print("  fps: %.2f   intrinsics: %s" % (fps, args.intrinsics))


if __name__ == "__main__":
    main()

