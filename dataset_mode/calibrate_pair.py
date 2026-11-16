"""Calibrate the effective shutter time from a sharp/blurred image pair.

Why a pair
----------
Blind deconvolution on a normal scene fails because the blur varies with depth. A *planar*
textured target seen head-on has a single depth, so the blur is uniform and its length can
be measured reliably from the cepstrum. The sharp frame gives the optical/AA width, the
blurred frame gives optical + motion width.

Two motion types are supported:

  linear       a plane at distance z moving at v m/s across the view
               L = f_x * v * T / z          ->  T = L * z / (f_x * v)
  rotational   camera panning at omega rad/s
               L = f_x * omega * T          ->  T = L / (f_x * omega)

usage
-----
  python calibrate_pair.py --selftest
  python calibrate_pair.py --sharp a.png --blurred b.png --fx 1200 \
      --distance 2.0 --speed 1.2 --angle 0 --out report.json
  python calibrate_pair.py --sharp a.png --blurred b.png --fx 1200 --omega 0.8
"""
import argparse
import json
import math

import numpy as np
from PIL import Image


def load_gray(path):
    return np.asarray(Image.open(path).convert("L"), dtype=np.float32)


def _rotate(img, angle):
    from PIL import Image as I
    return np.asarray(I.fromarray(img.astype(np.uint8)).rotate(-angle, resample=I.BILINEAR, expand=True),
                      dtype=np.float32)


def cepstral_length(img, angle_deg, min_len=4, max_len=120, min_confidence=2.6):
    """Uniform blur length along `angle_deg` from the 1-D cepstrum."""
    a = _rotate(img, angle_deg)
    h, w = a.shape
    if min(h, w) < 32:
        return None
    win = np.hanning(h)[:, None] * np.hanning(w)[None, :]
    spec = np.log1p(np.abs(np.fft.rfft(a * win, axis=1)))
    profile = spec.mean(axis=0)
    profile -= profile.mean()
    cep = np.abs(np.fft.irfft(profile))
    lo, hi = min_len, min(max_len, len(cep) - 1)
    seg = cep[lo:hi]
    if seg.size < 4:
        return None
    # The first few lags carry the DC/window lobe and swamp the blur peak, so ignore
    # everything below min_len and take the strongest local maximum above it.
    idx = int(np.argmax(seg))
    peak = float(seg[idx])
    floor = float(np.median(seg)) + 1e-9
    if peak < min_confidence * floor:
        return None
    return float(lo + idx)


def edge_width(img, angle_deg, row_band=20):
    """10-90 % width of the strongest edge after rotating the blur direction horizontal."""
    a = _rotate(img, angle_deg)
    h, w = a.shape
    cy = h // 2
    band = a[max(0, cy - row_band):cy + row_band, :].mean(axis=0)
    grad = np.abs(np.diff(band))
    if grad.size < 8:
        return None
    edge = int(np.argmax(grad))
    left = float(np.mean(band[max(0, edge - 60):max(1, edge - 20)]))
    right = float(np.mean(band[min(w - 1, edge + 20):min(w, edge + 60)]))
    hi, lo = max(left, right), min(left, right)
    if hi - lo < 15:
        return None
    hi_l, lo_l = lo + 0.9 * (hi - lo), lo + 0.1 * (hi - lo)

    def cross(level, start, stop, step):
        for i in range(start, stop, step):
            a0, b0 = band[i], band[i + 1]
            if (a0 - level) * (b0 - level) <= 0 and abs(b0 - a0) > 1e-6:
                return float(i) + (level - a0) / (b0 - a0)
        return None

    if right > left:
        x0 = cross(lo_l, edge, 1, -1)
        x1 = cross(hi_l, edge, w - 2, 1)
    else:
        x0 = cross(hi_l, edge, 1, -1)
        x1 = cross(lo_l, edge, w - 2, 1)
    if x0 is None or x1 is None:
        return None
    return abs(x1 - x0)


def solve(kind, length_px, fx, distance=None, speed=None, omega=None):
    if kind == "rotational":
        if not omega:
            raise ValueError("omega required for rotational")
        return length_px / (fx * omega)
    if not (distance and speed):
        raise ValueError("distance and speed required for linear")
    return length_px * distance / (fx * speed)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--sharp")
    ap.add_argument("--blurred")
    ap.add_argument("--fx", type=float)
    ap.add_argument("--distance", type=float, help="metres from camera to target plane")
    ap.add_argument("--speed", type=float, help="target/camera linear speed, m/s")
    ap.add_argument("--omega", type=float, help="pan rate, rad/s")
    ap.add_argument("--angle", type=float, default=0.0, help="blur direction in the image, degrees")
    ap.add_argument("--out")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args()

    if args.selftest:
        rng = np.random.default_rng(3)
        tex = np.zeros((400, 600), np.float32)
        for _ in range(60):
            x = rng.integers(0, 590)
            wid = rng.integers(2, 9)
            tex[:, x:x + wid] = rng.uniform(40, 220, size=(1, wid))
        tex = np.clip(tex + rng.normal(0, 3, tex.shape), 0, 255)

        print("selftest: blur a synthetic plane by a known amount and recover T")
        print("%8s %10s %10s %10s" % ("true L", "cepstrum", "edge", "T (ms) @ f=1200,z=2,v=1.2"))
        for L in (0, 4, 8, 16, 32):
            if L == 0:
                blurred = tex
            else:
                k = np.ones(L, np.float32) / L
                blurred = np.apply_along_axis(lambda r: np.convolve(r, k, mode="same"), 1, tex)
            cl = cepstral_length(blurred, 0.0)
            cl_sharp = cepstral_length(tex, 0.0)
            if cl and cl_sharp and abs(cl - cl_sharp) < 2.0:
                cl = None          # no measurable difference -> no motion blur
            ew_b = edge_width(blurred, 0.0)
            t_ms = solve("linear", cl, 1200.0, distance=2.0, speed=1.2) * 1000 if cl else None
            print("%8d %10s %10s %10s" % (
                L,
                "%.1f" % cl if cl else "n/a",
                "%.1f" % ew_b if ew_b else "n/a",
                "%.1f" % t_ms if t_ms else "n/a (no blur)"))
        return

    if not (args.sharp and args.blurred and args.fx):
        raise SystemExit("need --sharp --blurred --fx (or --selftest)")

    sharp = load_gray(args.sharp)
    blurred = load_gray(args.blurred)
    if sharp.shape != blurred.shape:
        raise SystemExit("pair has different sizes: %s vs %s" % (sharp.shape, blurred.shape))

    l_cep = cepstral_length(blurred, args.angle)
    l_cep_sharp = cepstral_length(sharp, args.angle)
    if l_cep and l_cep_sharp and abs(l_cep - l_cep_sharp) < 2.0:
        print("warning: sharp and blurred give the same length -> no measurable motion blur")
        l_cep = None
    ew_s = edge_width(sharp, args.angle)
    ew_b = edge_width(blurred, args.angle)
    l_edge = math.sqrt(max(0.0, ew_b ** 2 - ew_s ** 2)) if (ew_s and ew_b) else None

    kind = "rotational" if args.omega else "linear"
    report = {"kind": kind, "fx": args.fx, "angle_deg": args.angle,
              "cepstral_length_px": l_cep, "edge_motion_px": l_edge,
              "edge_sharp_px": ew_s, "edge_blurred_px": ew_b}

    print("cepstral blur length : %s px" % ("%.2f" % l_cep if l_cep else "n/a"))
    print("edge-width motion    : %s px (sharp %.2f, blurred %.2f)" % (
        "%.2f" % l_edge if l_edge else "n/a", ew_s or -1, ew_b or -1))

    if l_cep:
        t = solve(kind, l_cep, args.fx, args.distance, args.speed, args.omega)
        report["exposure_s"] = t
        report["exposure_ms"] = t * 1000.0
        print("\n==> effective exposure: %.1f ms  (%s model)" % (t * 1000.0, kind))

    if args.out:
        with open(args.out, "w", encoding="utf-8") as fh:
            json.dump(report, fh, indent=2)
        print("written:", args.out)


if __name__ == "__main__":
    main()



