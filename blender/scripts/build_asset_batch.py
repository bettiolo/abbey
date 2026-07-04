"""CLI entrypoint for the asset factory. Runs under BOTH environments:

    # bpy python module:
    uv run --with-requirements tools/requirements-dev.txt --with bpy python blender/scripts/build_asset_batch.py --asset campfire_t1
    uv run --with-requirements tools/requirements-dev.txt --with bpy python blender/scripts/build_asset_batch.py --all

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
    parser.add_argument(
        "--output-root",
        metavar="DIR",
        default=None,
        help="write glb/blend/previews/metadata under DIR instead of "
        "blender/generated/ (used by the pipeline verify mode)",
    )
    return parser.parse_args(argv)


def _relpath(path: Path) -> str:
    """Repo-relative path for portable metadata.

    Outputs are reported relative to the *canonical* location
    (``blender/generated/...``) even when --output-root redirects the build to
    a temp directory, so verify-mode metadata is byte-comparable to the
    committed metadata."""
    p = Path(path).resolve()
    try:
        return str(Path("blender") / "generated" / p.relative_to(fw.GENERATED_DIR))
    except ValueError:
        pass
    try:
        return str(p.relative_to(fw.BLENDER_DIR.parent))
    except ValueError:
        return str(path)


def _without_generated_at(metadata: dict) -> dict:
    comparable = dict(metadata)
    comparable.pop("generated_at", None)
    return comparable


def _write_metadata_if_changed(meta_path: Path, metadata: dict) -> None:
    if meta_path.exists():
        try:
            existing = json.loads(meta_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            existing = None
        if (
            isinstance(existing, dict)
            and _without_generated_at(existing) == _without_generated_at(metadata)
            and "generated_at" in existing
        ):
            metadata["generated_at"] = existing["generated_at"]

    text = json.dumps(metadata, indent=2) + "\n"
    if meta_path.exists() and meta_path.read_text(encoding="utf-8") == text:
        return
    meta_path.write_text(text, encoding="utf-8")


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
    _write_metadata_if_changed(meta_path, metadata)

    status = "PASSED" if validation["passed"] else "FAILED"
    print(f"[{status}] {asset_id} -> {meta_path}")
    for check in validation["checks"]:
        mark = "ok " if check["passed"] else "FAIL"
        print(f"  [{mark}] {check['name']}: {check['detail']}")
    return metadata


def main(argv: list[str]) -> int:
    args = _cli_args(argv)
    if args.output_root:
        root = fw.set_generated_root(args.output_root)
        print(f"Output root overridden: {root}")
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
