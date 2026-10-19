"""Numerical checks of the motion response, independent of Unity rendering."""
import math

def evolve(current, target, seconds, fps):
    for _ in range(round(seconds * fps)):
        duration = 0.18 if abs(target) < abs(current) else 0.09
        current += (target - current) * (1 - math.exp(-1 / fps / duration))
    return current

for fps in (30, 60, 120):
    onset = evolve(0, 1.5, 0.3, fps)
    settled = evolve(onset, 0, 0.3, fps)
    assert 1.4 < onset < 1.5
    assert 0.1 < settled < 0.4
    assert abs(evolve(0, 0, 1, fps)) == 0
    assert evolve(1.5, -1.5, 0.5, fps) < -1.3
    print(f"{fps} fps: walk after 0.3s={onset:.4f}, stop residual after 0.3s={settled:.4f}")

# A straight upward camera movement produces vertical image motion; downward
# movement reverses it. Distant scenery uses the explicit artistic distance cap.
height = 540
focal_pixels = height / (2 * math.tan(math.radians(72) / 2))
exposure = (0.005 + (0.065 - 0.005) * 0.8) * 0.9
gain = 2.2
pixels = focal_pixels * 6 * exposure * gain / min(60, 8)
assert pixels > 20
for fps in (30, 60, 120):
    displacement = 6 / fps
    jump_pixels = focal_pixels * displacement / 8 * exposure * gain * fps
    assert math.isclose(jump_pixels, pixels)
print(f"Pure vertical jump at 6 m/s: {pixels:.2f} px reference trail at 540p, direction reverses on descent")

# Uniform-color input must retain its brightness with any subset of accepted taps.
for accepted in (0, 1, 10, 20):
    weights = [1.0] + [(1 - (i / 20) * 0.6) / 3.0 for i in range(1, accepted + 1)]
    assert math.isclose(sum(0.7 * w for w in weights) / sum(weights), 0.7)
print("Normalization preserves constant brightness for rejected depth/border taps")
