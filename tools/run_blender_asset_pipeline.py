#!/usr/bin/env python3
"""Run the Blender asset pipeline with whichever Blender runtime is available.

Resolution order:
  1. The pip-installed ``bpy`` module ("module mode"): the batch entry point
     ``blender/scripts/build_asset_batch.py`` is executed as a plain Python
     subprocess and imports bpy itself.
  2. A ``blender`` binary on PATH (or $BLENDER_PATH): executed as
     ``blender -b -P blender/scripts/build_asset_batch.py -- <args>``.
  3. Neither available -> print a SKIP message and exit 3.

Exit codes:
  0  pipeline succeeded (or nothing to do for --changed)
  3  SKIP: no Blender runtime, or the pipeline entry script does not exist yet
  *  any other code is propagated verbatim from the pipeline subprocess

Usage:
  uv run --with-requirements tools/requirements-dev.txt python tools/run_blender_asset_pipeline.py --asset campfire_t1
  uv run --with-requirements tools/requirements-dev.txt --with bpy python tools/run_blender_asset_pipeline.py --all
  uv run --with-requirements tools/requirements-dev.txt python tools/run_blender_asset_pipeline.py --changed
  uv run --with-requirements tools/requirements-dev.txt python tools/run_blender_asset_pipeline.py --changed --verify

Verify mode (--verify, used by tools/check_all.sh by default):
  Builds the selected assets into a TEMP directory (via build_asset_batch.py
  --output-root) and compares the results against the committed artifacts in
  blender/generated/ WITHOUT touching the working tree. Volatile noise is
  ignored:
    - metadata:  compared as JSON with 'generated_at' dropped and the
                 "(N bytes)" GLB size inside validation details masked
                 (it co-varies with the GLB, which is compared separately)
    - previews:  Cycles render noise makes pixel/byte comparison useless, so
                 previews are compared by existence + PNG pixel dimensions only
    - GLB:       byte-compare first; on mismatch (e.g. Blender version drift)
                 fall back to a structural compare: both non-empty and sizes
                 within GLB_SIZE_TOLERANCE, with geometry equality already
                 covered by the metadata compare (dimensions/triangle_count)
    - .blend:    gitignored build artifact, not compared
  Substantive differences exit 1 with a per-asset report.
"""

from __future__ import annotations

import argparse
import filecmp
import importlib.util
import json
import os
import re
import shutil
import struct
import subprocess
import sys
import tempfile
from pathlib import Path

EXIT_SKIP = 3

REPO_ROOT = Path(__file__).resolve().parent.parent
BATCH_SCRIPT = REPO_ROOT / "blender" / "scripts" / "build_asset_batch.py"
SPEC_DIR = REPO_ROOT / "blender" / "asset_specs"
BUILDER_DIR = REPO_ROOT / "blender" / "scripts"
COMMITTED_GENERATED = REPO_ROOT / "blender" / "generated"

PREVIEW_VARIANTS = ("day", "night", "winter", "grayscale")

# GLB structural-compare tolerance (relative size drift) when byte-compare
# fails, e.g. across Blender/exporter versions. Geometry is still pinned by the
# metadata compare (dimensions, triangle_count, materials, anchors).
GLB_SIZE_TOLERANCE = 0.25

# "(12345 bytes)" inside validation check details — derived from the GLB size,
# which is compared separately with its own tolerance.
_BYTES_RE = re.compile(r"\(\d+ bytes\)")

# Local "main". Promotion to origin/main is a human-gated PR (see REQUIREMENTS.yml).
INTEGRATION_BRANCH = "claude/abbey-llm-pipeline-wel2xo"


def skip(message: str) -> int:
    print(f"SKIP: {message}")
    return EXIT_SKIP


def resolve_runner() -> tuple[str, list[str]] | None:
    """Return (mode, argv-prefix) or None if no Blender runtime exists.

    mode is "module" (pip bpy) or "binary" (blender -b -P).
    """
    if importlib.util.find_spec("bpy") is not None:
        return ("module", [sys.executable, str(BATCH_SCRIPT)])

    blender = os.environ.get("BLENDER_PATH") or shutil.which("blender")
    if blender:
        return ("binary", [blender, "-b", "--factory-startup", "-P", str(BATCH_SCRIPT), "--"])

    return None


def _git(*args: str) -> str:
    return subprocess.check_output(
        ["git", "-C", str(REPO_ROOT), *args], text=True, stderr=subprocess.DEVNULL
    ).strip()


def changed_files() -> list[str]:
    """Files changed vs the merge-base with the integration branch,
    plus uncommitted and untracked files in the working tree."""
    base = None
    for ref in (INTEGRATION_BRANCH, f"origin/{INTEGRATION_BRANCH}"):
        try:
            base = _git("merge-base", ref, "HEAD")
            break
        except subprocess.CalledProcessError:
            continue

    files: set[str] = set()
    try:
        if base:
            files.update(_git("diff", "--name-only", base).splitlines())
        else:
            print(
                f"WARNING: integration branch '{INTEGRATION_BRANCH}' not found; "
                "only considering uncommitted/untracked changes.",
                file=sys.stderr,
            )
            files.update(_git("diff", "--name-only", "HEAD").splitlines())
        # Untracked files matter: a builder agent may not have committed a new spec yet.
        files.update(_git("ls-files", "--others", "--exclude-standard").splitlines())
    except subprocess.CalledProcessError as exc:
        print(f"ERROR: git inspection failed: {exc}", file=sys.stderr)
        return []
    return sorted(f for f in files if f)


def assets_from_changes(files: list[str]) -> tuple[list[str], bool]:
    """Return (asset_ids, rebuild_all). Builder/framework script changes force --all."""
    asset_ids: set[str] = set()
    rebuild_all = False
    for f in files:
        p = Path(f)
        try:
            rel = (REPO_ROOT / p).resolve().relative_to(REPO_ROOT)
        except ValueError:
            rel = p
        parts = rel.parts
        if len(parts) >= 2 and parts[0] == "blender":
            if parts[1] == "asset_specs" and rel.suffix == ".json":
                asset_ids.add(rel.stem)
            elif parts[1] == "scripts" and rel.suffix == ".py":
                rebuild_all = True
    return sorted(asset_ids), rebuild_all


# ---------------------------------------------------------------------------
# Verify mode: compare a temp build against the committed artifacts
# ---------------------------------------------------------------------------


def _png_dimensions(path: Path) -> tuple[int, int] | None:
    """(width, height) from the PNG IHDR chunk, or None if not a valid PNG."""
    try:
        with open(path, "rb") as fh:
            header = fh.read(24)
    except OSError:
        return None
    if len(header) < 24 or header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
        return None
    width, height = struct.unpack(">II", header[16:24])
    return width, height


def _normalize_metadata(meta: dict) -> dict:
    """Strip volatile fields: the generated_at timestamp and the GLB byte size
    embedded in validation check details."""
    out = json.loads(json.dumps(meta))  # deep copy
    out.pop("generated_at", None)
    for check in out.get("validation", {}).get("checks", []):
        if isinstance(check.get("detail"), str):
            check["detail"] = _BYTES_RE.sub("(<n> bytes)", check["detail"])
    return out


def _diff_json(old, new, path: str = "$") -> list[str]:
    """Human-readable list of leaf differences between two JSON values."""
    if type(old) is not type(new):
        return [f"{path}: type {type(old).__name__} -> {type(new).__name__}"]
    if isinstance(old, dict):
        diffs = []
        for key in sorted(set(old) | set(new)):
            if key not in old:
                diffs.append(f"{path}.{key}: added")
            elif key not in new:
                diffs.append(f"{path}.{key}: removed")
            else:
                diffs.extend(_diff_json(old[key], new[key], f"{path}.{key}"))
        return diffs
    if isinstance(old, list):
        if len(old) != len(new):
            return [f"{path}: list length {len(old)} -> {len(new)}"]
        diffs = []
        for i, (a, b) in enumerate(zip(old, new)):
            diffs.extend(_diff_json(a, b, f"{path}[{i}]"))
        return diffs
    if old != new:
        return [f"{path}: {old!r} -> {new!r}"]
    return []


def _verify_asset(asset_id: str, temp_root: Path) -> list[str]:
    """Compare one temp-built asset against blender/generated/. Returns a list
    of substantive problems (empty = clean)."""
    problems: list[str] = []

    # --- metadata: JSON equality minus volatile fields -----------------------
    committed_meta_path = COMMITTED_GENERATED / "metadata" / f"{asset_id}.meta.json"
    new_meta_path = temp_root / "metadata" / f"{asset_id}.meta.json"
    if not committed_meta_path.is_file():
        problems.append(
            f"metadata: {committed_meta_path.relative_to(REPO_ROOT)} is not "
            "committed — new asset? Regenerate intentionally with "
            "tools/check_all.sh --write (or run the pipeline without --verify) "
            "and commit the outputs."
        )
        return problems
    old_meta = json.loads(committed_meta_path.read_text(encoding="utf-8"))
    new_meta = json.loads(new_meta_path.read_text(encoding="utf-8"))
    for diff in _diff_json(_normalize_metadata(old_meta), _normalize_metadata(new_meta)):
        problems.append(f"metadata: {diff}")

    # --- GLB: byte-compare, structural fallback ------------------------------
    committed_glb = COMMITTED_GENERATED / "glb" / f"{asset_id}.glb"
    new_glb = temp_root / "glb" / f"{asset_id}.glb"
    if not committed_glb.is_file():
        problems.append(f"glb: committed {committed_glb.relative_to(REPO_ROOT)} missing")
    elif not new_glb.is_file():
        problems.append("glb: pipeline produced no GLB")
    elif not filecmp.cmp(committed_glb, new_glb, shallow=False):
        old_size = committed_glb.stat().st_size
        new_size = new_glb.stat().st_size
        drift = abs(new_size - old_size) / max(old_size, 1)
        if old_size == 0 or new_size == 0 or drift > GLB_SIZE_TOLERANCE:
            problems.append(
                f"glb: bytes differ AND size drift {drift:.1%} exceeds "
                f"{GLB_SIZE_TOLERANCE:.0%} ({old_size} -> {new_size} bytes)"
            )
        else:
            print(
                f"    note: {asset_id}.glb bytes differ (likely exporter/Blender "
                f"version drift); structural compare OK (size drift {drift:.1%}, "
                "geometry pinned by metadata)"
            )

    # --- previews: existence + PNG dimensions (Cycles noise => no pixel diff) -
    for variant in PREVIEW_VARIANTS:
        name = f"{asset_id}_preview_{variant}.png"
        committed_png = COMMITTED_GENERATED / "previews" / name
        new_png = temp_root / "previews" / name
        if not committed_png.is_file():
            problems.append(f"preview {variant}: committed {name} missing")
            continue
        if not new_png.is_file():
            problems.append(f"preview {variant}: pipeline did not render {name}")
            continue
        old_dim = _png_dimensions(committed_png)
        new_dim = _png_dimensions(new_png)
        if old_dim is None or new_dim is None:
            problems.append(f"preview {variant}: {name} is not a valid PNG")
        elif old_dim != new_dim:
            problems.append(
                f"preview {variant}: dimensions {old_dim[0]}x{old_dim[1]} -> "
                f"{new_dim[0]}x{new_dim[1]}"
            )

    return problems


def verify_against_committed(temp_root: Path) -> int:
    """Compare every asset built under *temp_root* with blender/generated/."""
    built = sorted(temp_root.glob("metadata/*.meta.json"))
    if not built:
        print("verify: pipeline produced no metadata — nothing to compare.",
              file=sys.stderr)
        return 1

    failed = []
    print(f"\n=== verify: comparing {len(built)} built asset(s) against "
          f"{COMMITTED_GENERATED.relative_to(REPO_ROOT)} ===")
    for meta_path in built:
        asset_id = meta_path.name[: -len(".meta.json")]
        problems = _verify_asset(asset_id, temp_root)
        if problems:
            failed.append(asset_id)
            print(f"[DIFF] {asset_id}")
            for problem in problems:
                print(f"    - {problem}")
        else:
            print(f"[ok]   {asset_id}")

    if failed:
        print(
            f"\nverify FAILED for {len(failed)}/{len(built)} asset(s): {failed}\n"
            "Committed artifacts are stale (or the build changed). If the new "
            "output is intended, regenerate with tools/check_all.sh --write and "
            "commit blender/generated/.",
            file=sys.stderr,
        )
        return 1
    print(f"verify OK: all {len(built)} asset(s) match the committed artifacts "
          "(ignoring generated_at + preview render noise).")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--asset", action="append", default=[], metavar="ID",
                        help="asset id to build (repeatable)")
    parser.add_argument("--all", action="store_true", help="build every asset spec")
    parser.add_argument("--changed", action="store_true",
                        help="build assets whose specs/builders changed vs the "
                             f"merge-base with {INTEGRATION_BRANCH}")
    parser.add_argument("--verify", action="store_true",
                        help="build into a temp directory and compare against "
                             "the committed blender/generated/ instead of "
                             "overwriting it (leaves the working tree clean)")
    args = parser.parse_args(argv)

    if not (args.asset or args.all or args.changed):
        parser.error("one of --asset, --all, --changed is required")

    if not BATCH_SCRIPT.exists():
        return skip(f"pipeline entry {BATCH_SCRIPT.relative_to(REPO_ROOT)} does not exist "
                    "yet (Blender pipeline task not merged). Nothing to run.")

    pipeline_args: list[str] = []
    if args.all:
        pipeline_args = ["--all"]
    elif args.changed:
        files = changed_files()
        asset_ids, rebuild_all = assets_from_changes(files)
        if rebuild_all:
            print("Builder scripts changed -> rebuilding all assets.")
            pipeline_args = ["--all"]
        elif asset_ids:
            print(f"Changed assets: {', '.join(asset_ids)}")
            for asset_id in asset_ids:
                pipeline_args += ["--asset", asset_id]
        else:
            print("No changed asset specs or builder scripts. Nothing to build.")
            return 0
    else:
        for asset_id in args.asset:
            pipeline_args += ["--asset", asset_id]

    runner = resolve_runner()
    if runner is None:
        return skip("no Blender runtime found (neither the 'bpy' python module nor a "
                    "'blender' binary on PATH / $BLENDER_PATH).")

    mode, prefix = runner
    temp_root: Path | None = None
    if args.verify:
        temp_root = Path(tempfile.mkdtemp(prefix="abbey_verify_"))
        pipeline_args += ["--output-root", str(temp_root)]
        print(f"Verify mode: building into {temp_root} (committed artifacts untouched)")

    try:
        cmd = prefix + pipeline_args
        print(f"Blender runner: {mode} mode")
        print("Running:", " ".join(cmd))
        result = subprocess.run(cmd, cwd=REPO_ROOT)
        if result.returncode != 0:
            return result.returncode
        if temp_root is not None:
            return verify_against_committed(temp_root)
        return 0
    finally:
        if temp_root is not None:
            shutil.rmtree(temp_root, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
