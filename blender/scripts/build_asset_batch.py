"""CLI entrypoint for the asset factory. Runs under BOTH environments:

    # bpy python module (this container):
    python3 blender/scripts/build_asset_batch.py --asset campfire_t1
    python3 blender/scripts/build_asset_batch.py --all

    # blender binary:
    blender -b -P blender/scripts/build_asset_batch.py -- --asset campfire_t1

Per asset: reset scene -> builder -> pivot/collision -> GLB + .blend ->
4 previews -> validation -> blender/generated/metadata/<id>.meta.json.
Exit code is nonzero if any asset fails validation.
"""

from __future__ import annotations

import argparse
import datetime
import json
import sys
import traceback
from pathlib import Path

_SCRIPTS_DIR = str(Path(__file__).resolve().parent)
if _SCRIPTS_DIR not in sys.path:
    sys.path.insert(0, _SCRIPTS_DIR)

import asset_framework as fw
from export_glb import export_asset
from generate_asset import generate_asset
from render_preview import render_previews
from validate_asset import validate_asset


def _cli_args(argv: list[str]) -> argparse.Namespace:
    # Under 'blender -b -P script -- <args>' our args come after the '--'.
    if "--" in argv:
        argv = argv[argv.index("--") + 1 :]
    else:
        argv = argv[1:]
    parser = argparse.ArgumentParser(description="Abbey asset factory")
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--asset", action="append", help="asset id (repeatable)")
    group.add_argument(
        "--all", action="store_true", help="build every spec under blender/asset_specs/"
    )
    return parser.parse_args(argv)


def _relpath(path: Path) -> str:
    """Path relative to the repo root (parent of blender/) for portable metadata."""
    try:
        return str(Path(path).resolve().relative_to(fw.BLENDER_DIR.parent))
    except ValueError:
        return str(path)


def build_asset(asset_id: str) -> dict:
    """Full pipeline for one asset. Returns the metadata dict (also written)."""
    print(f"\n=== building {asset_id} ===")
    result = generate_asset(asset_id)
    root = result["root"]
    spec = result["spec"]

    files = export_asset(root, asset_id)
    previews = render_previews(root, asset_id)
    validation = validate_asset(root, spec, files, previews)

    metadata = {
        "id": asset_id,
        "generated_at": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "spec": spec,
        "dimensions": result["dimensions"],
        "triangle_count": result["triangle_count"],
        "material_count": len(result["materials"]),
        "materials": result["materials"],
        "anchors": result["anchors"],
        "files": {
            "glb": _relpath(files["glb"]),
            "blend": _relpath(files["blend"]),
            "previews": {k: _relpath(v) for k, v in previews.items()},
        },
        "validation": validation,
    }

    fw.METADATA_DIR.mkdir(parents=True, exist_ok=True)
    meta_path = fw.METADATA_DIR / f"{asset_id}.meta.json"
    with open(meta_path, "w", encoding="utf-8") as fh:
        json.dump(metadata, fh, indent=2)
        fh.write("\n")

    status = "PASSED" if validation["passed"] else "FAILED"
    print(f"[{status}] {asset_id} -> {meta_path}")
    for check in validation["checks"]:
        mark = "ok " if check["passed"] else "FAIL"
        print(f"  [{mark}] {check['name']}: {check['detail']}")
    return metadata


def main(argv: list[str]) -> int:
    args = _cli_args(argv)
    asset_ids = fw.list_spec_ids() if args.all else args.asset
    if not asset_ids:
        print("No asset specs found.", file=sys.stderr)
        return 2

    failures: list[str] = []
    for asset_id in asset_ids:
        try:
            metadata = build_asset(asset_id)
        except Exception:
            traceback.print_exc()
            failures.append(asset_id)
            continue
        if not metadata["validation"]["passed"]:
            failures.append(asset_id)

    print(f"\n=== batch done: {len(asset_ids) - len(failures)}/{len(asset_ids)} passed ===")
    if failures:
        print(f"FAILED assets: {failures}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
