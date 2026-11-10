"""Download a curated Poly Haven CC0 library for the Motion2RGB scenes.

The manifest deliberately uses 4K JPG texture maps plus FBX geometry for a
small set of industrial and campus assets. HDRIs are stored separately and
can be assigned to the Built-in Render Pipeline by the scene builder.
"""

from concurrent.futures import ThreadPoolExecutor, as_completed
import argparse
from collections import Counter
from hashlib import md5
import json
from pathlib import Path
import ssl
import time
import urllib.request
from urllib.error import URLError


PROJECT = Path(__file__).resolve().parents[1]
ROOT = PROJECT / "Assets" / "Motion2RGB" / "RealScenes" / "PolyHaven" / "Library4K"
API = "https://api.polyhaven.com"
USER_AGENT = "Motion2RGB-PolyHaven-Library/1.0"
MAX_BYTES = 9_500_000_000

MODEL_SLUGS = [
    "modular_fire_escape", "modular_chainlink_fence", "modular_industrial_pipes_01",
    "modular_airduct_rectangular_01", "modular_electricity_poles", "modular_metal_gutter",
    "rollershutter_door", "rollershutter_window_01", "security_light", "security_camera_01",
    "security_camera_02", "utility_box_01", "utility_box_02", "fire_hydrant",
    "concrete_road_barrier", "concrete_road_barrier_02", "overhead_crane", "portable_generator",
    "industrial_storage_cart", "caged_hanging_light", "hanging_industrial_lamp",
    "industrial_wall_lamp", "industrial_pastic_container", "plastic_crate_02", "barrel_03",
    "cement_bag", "hand_truck", "ladder_sectioned_01", "industrial_microscope",
    "metal_office_desk", "wall_clock", "painted_wooden_cabinet", "painted_wooden_shelves",
    "street_lamp_02", "tree_small_02", "jacaranda_tree", "outdoor_table_chair_set_01",
    "industrial_pipe_lamp", "modern_ceiling_lamp_01", "modular_street_seating", "power_box_01",
    "modular_factory_facade",
]

SURFACE_SLUGS = ["asphalt_02", "concrete_floor_02", "floor_bricks_02"]

HDRI_SLUGS = [
    "abandoned_construction", "urban_street_01", "urban_street_02", "workshop",
    "boiler_room", "concrete_tunnel", "construction_yard", "brick_factory_02",
]


def request_json(url):
    context = ssl.create_default_context()
    last_error = None
    for attempt in range(5):
        try:
            request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
            with urllib.request.urlopen(request, context=context, timeout=60) as response:
                return json.loads(response.read().decode("utf-8"))
        except (URLError, TimeoutError, OSError) as error:
            last_error = error
            time.sleep(2 + attempt * 2)
    raise last_error


def plan_model(slug):
    files = request_json(f"{API}/files/{slug}")
    selected = []
    fbx = files.get("fbx", {}).get("4k", {}).get("fbx")
    if fbx:
        selected.append((f"{slug}/{slug}_4k.fbx", fbx))
    for key, variants in files.items():
        info = variants.get("4k", {}).get("jpg") if isinstance(variants, dict) else None
        if info:
            selected.append((f"{slug}/{key}_4k.jpg", info))
    return {"slug": slug, "type": "model", "files": selected}


def plan_hdri(slug):
    files = request_json(f"{API}/files/{slug}")
    info = files.get("hdri", {}).get("8k", {}).get("hdr")
    if not info:
        info = files.get("hdri", {}).get("4k", {}).get("hdr")
    return {"slug": slug, "type": "hdri", "files": [(f"HDRI/{slug}_8k.hdr", info)] if info else []}


def plan_surface(slug):
    plan = plan_model(slug)
    plan["type"] = "texture"
    return plan


def download(item):
    relative, info = item
    if not info:
        return relative, "missing", 0
    destination = ROOT / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists() and destination.stat().st_size == info.get("size"):
        return relative, "cached", destination.stat().st_size
    context = ssl.create_default_context()
    temporary = PROJECT / "Temp" / "PolyHavenDownloads" / (relative + ".part")
    temporary.parent.mkdir(parents=True, exist_ok=True)
    last_error = None
    for attempt in range(4):
        try:
            url = info["url"] + (f"?m2rgb_retry={attempt}" if attempt else "")
            request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
            with urllib.request.urlopen(request, context=context, timeout=180) as response, temporary.open("wb") as stream:
                while True:
                    chunk = response.read(1024 * 1024)
                    if not chunk:
                        break
                    stream.write(chunk)
            digest = md5(temporary.read_bytes()).hexdigest()
            if temporary.stat().st_size != info["size"] or digest != info["md5"]:
                raise OSError(f"Incomplete or corrupt download: {relative}")
            last_error = None
            break
        except (URLError, TimeoutError, OSError) as error:
            last_error = error
            temporary.unlink(missing_ok=True)
            time.sleep(3 + attempt * 3)
    if last_error is not None:
        raise last_error
    temporary.replace(destination)
    return relative, "downloaded", destination.stat().st_size


def verify_manifest(manifest):
    failures = []
    total = 0
    count = 0
    for plan in manifest["items"]:
        for relative, info in plan["files"]:
            count += 1
            path = ROOT / relative
            if not path.exists():
                failures.append(relative + ": missing")
                continue
            digest = md5()
            with path.open("rb") as stream:
                for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                    digest.update(chunk)
            total += path.stat().st_size
            if path.stat().st_size != info["size"] or digest.hexdigest() != info["md5"]:
                failures.append(relative + ": size/checksum mismatch")
    report = {"files": count, "bytes": total, "GB": round(total / 1e9, 3),
              "asset_types": dict(Counter(p["type"] for p in manifest["items"])),
              "failures": failures}
    (ROOT / "verification_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
    if failures:
        raise RuntimeError("Library verification failed; see verification_report.json")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify-only", action="store_true", help="Verify all local files without accessing the network.")
    args = parser.parse_args()
    previous_manifest = ROOT / "download_manifest.json"
    if args.verify_only:
        verify_manifest(json.loads(previous_manifest.read_text(encoding="utf-8")))
        return
    if previous_manifest.exists():
        saved = json.loads(previous_manifest.read_text(encoding="utf-8"))
        plans = saved.get("items", [])
        print(f"Reusing existing manifest with {len(plans)} assets; downloading missing files.")
        known = {p["slug"] for p in plans}
        plans += [plan_model(s) for s in MODEL_SLUGS if s not in known]
        plans += [plan_hdri(s) for s in HDRI_SLUGS if s not in known]
        plans += [plan_surface(s) for s in SURFACE_SLUGS if s not in known]
    else:
        plans = [plan_model(slug) for slug in MODEL_SLUGS]
        plans += [plan_hdri(slug) for slug in HDRI_SLUGS]
        plans += [plan_surface(slug) for slug in SURFACE_SLUGS]
    files = [file for plan in plans for file in plan["files"]]
    planned_bytes = sum(file[1].get("size", 0) for file in files if file[1])
    print(f"Planned files: {len(files)}; planned download: {planned_bytes / 1e9:.2f} GB")
    if planned_bytes > MAX_BYTES:
        raise RuntimeError("The planned download exceeds the 9.5 GB safety cap.")

    ROOT.mkdir(parents=True, exist_ok=True)
    manifest = {
        "source": "https://polyhaven.com",
        "license": "CC0",
        "resolution": "4K JPG + FBX; 8K HDRI",
        "planned_bytes": planned_bytes,
        "items": plans,
    }
    (ROOT / "download_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    results = []
    with ThreadPoolExecutor(max_workers=6) as executor:
        futures = [executor.submit(download, item) for item in files]
        for future in as_completed(futures):
            relative, state, size = future.result()
            results.append((relative, state, size))
            if state != "cached":
                print(f"[{state:10}] {relative} ({size / 1e6:.1f} MB)", flush=True)

    total = sum(size for _, _, size in results)
    (ROOT / "README_CC0.md").write_text(
        "# Poly Haven 4K library\n\n"
        "Downloaded from https://polyhaven.com using the public API. "
        "Poly Haven assets are CC0. Geometry is FBX, textures are 4K JPG, and lighting files are 8K HDR.\n\n"
        "The download manifest records the source URL and MD5 for every file.\n",
        encoding="utf-8",
    )
    print(f"Downloaded library size: {total / 1e9:.2f} GB")
    verify_manifest(manifest)


if __name__ == "__main__":
    main()
