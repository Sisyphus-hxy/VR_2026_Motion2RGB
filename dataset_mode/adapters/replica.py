"""Convert a Replica room into the dataset-mode format.

Expected layout (the usual `Replica/room_0` folder):

    results/frame000000.jpg
    results/depth000000.png        # 16-bit, scaled; 6553.5 => metres (10 m range)
    trajectory.txt                 # <time> tx ty tz qx qy qz qw

Usage:
    python replica.py --src D:\\data\\Replica\\room_0 --out runs\\replica_room0
"""
from __future__ import annotations

import argparse
import os
import shutil
import sys

import numpy as np

import common


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True, help="Replica room folder")
    ap.add_argument("--out", required=True)
    ap.add_argument("--intrinsics", nargs=4, type=float, default=[600.0, 600.0, 599.5, 339.5],
                    help="fx fy cx cy (default: Replica 1200x680)")
    ap.add_argument("--depth-scale", type=float, default=6553.5,
                    help="raw depth units per metre; Replica stores 10 m as 65535")
    ap.add_argument("--exposure", type=float, default=0.02)
    ap.add_argument("--gain", type=float, default=2.2)
    ap.add_argument("--stride", type=int, default=1)
    ap.add_argument("--max-frames", type=int, default=0)
    ap.add_argument("--copy-rgb", action="store_true")
    args = ap.parse_args()

    results = os.path.join(args.src, "results")
    traj_path = os.path.join(args.src, "trajectory.txt")
    if not os.path.isdir(results):
        sys.exit("results/ not found in %s" % args.src)
    if not os.path.isfile(traj_path):
        sys.exit("trajectory.txt not found in %s" % args.src)

    poses = common.read_trajectory(traj_path)
    traj_ts = sorted(poses)

    rgb_files = sorted(f for f in os.listdir(results) if f.startswith("frame") and f.lower().endswith(".jpg"))
    depth_files = sorted(f for f in os.listdir(results) if f.startswith("depth") and f.lower().endswith(".png"))
    if not rgb_files or not depth_files:
        sys.exit("no frame*.jpg / depth*.png in %s/results" % args.src)

    def frame_id(name):
        digits = "".join(ch for ch in name if ch.isdigit())
        return int(digits) if digits else -1

    rgb_by_id = {frame_id(f): f for f in rgb_files}
    depth_by_id = {frame_id(f): f for f in depth_files}
    ids = sorted(set(rgb_by_id) & set(depth_by_id))[::max(1, args.stride)]
    if args.max_frames > 0:
        ids = ids[:args.max_frames]

    os.makedirs(args.out, exist_ok=True)
    depth_out = os.path.join(args.out, "depth")
    rgb_out = os.path.join(args.out, "rgb")
    os.makedirs(depth_out, exist_ok=True)
    if args.copy_rgb:
        os.makedirs(rgb_out, exist_ok=True)

    frames = []
    for i, fid in enumerate(ids):
        depth_src = os.path.join(results, depth_by_id[fid])
        depth_dst = os.path.join(depth_out, "%06d.npy" % i)
        common.save_depth_png_to_npy(depth_src, depth_dst, args.depth_scale)

        rgb_src = os.path.join(results, rgb_by_id[fid])
        if args.copy_rgb:
            rgb_dst = os.path.join(rgb_out, "%06d.png" % i)
            shutil.copyfile(rgb_src, rgb_dst)
        else:
            rgb_dst = os.path.abspath(rgb_src)

        # trajectory is one pose per frame id when the lengths match, otherwise index by order
        if len(traj_ts) == len(rgb_files):
            pose_key = traj_ts[fid] if fid < len(traj_ts) else traj_ts[-1]
        else:
            pose_key = traj_ts[min(fid, len(traj_ts) - 1)]

        frames.append({"rgb": rgb_dst, "depth": depth_dst, "c2w": poses[pose_key].tolist()})

    if not frames:
        sys.exit("no usable frames")

    fps = 1.0 / common.median_dt(traj_ts)
    common.write_meta(args.out, frames, args.intrinsics, fps, args.exposure, args.gain,
                      depth_limit=None,
                      extra={"source": os.path.abspath(args.src), "dataset": "replica",
                             "depth_scale": args.depth_scale})
    print("Replica -> %s" % args.out)
    print("  frames: %d   fps: %.2f   intrinsics: %s" % (len(frames), fps, args.intrinsics))


if __name__ == "__main__":
    main()
