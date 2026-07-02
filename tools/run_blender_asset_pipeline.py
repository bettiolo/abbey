#!/usr/bin/env python3
"""Run the Blender asset pipeline with whichever Blender runtime is available.

Resolution order:
  1. The pip-installed ``bpy`` module ("module mode"): the batch entry point
     ``blender/scripts/build_asset_batch.py`` is executed as a plain python3
     subprocess and imports bpy itself.
  2. A ``blender`` binary on PATH (or $BLENDER_PATH): executed as
     ``blender -b -P blender/scripts/build_asset_batch.py -- <args>``.
  3. Neither available -> print a SKIP message and exit 3.

Exit codes:
  0  pipeline succeeded (or nothing to do for --changed)
  3  SKIP: no Blender runtime, or the pipeline entry script does not exist yet
  *  any other code is propagated verbatim from the pipeline subprocess

Usage:
  python3 tools/run_blender_asset_pipeline.py --asset campfire_t1 [--asset ...]
  python3 tools/run_blender_asset_pipeline.py --all
  python3 tools/run_blender_asset_pipeline.py --changed
"""

from __future__ import annotations

import argparse
import importlib.util
import os
import shutil
import subprocess
import sys
from pathlib import Path

EXIT_SKIP = 3

REPO_ROOT = Path(__file__).resolve().parent.parent
BATCH_SCRIPT = REPO_ROOT / "blender" / "scripts" / "build_asset_batch.py"
SPEC_DIR = REPO_ROOT / "blender" / "asset_specs"
BUILDER_DIR = REPO_ROOT / "blender" / "scripts"

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


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--asset", action="append", default=[], metavar="ID",
                        help="asset id to build (repeatable)")
    parser.add_argument("--all", action="store_true", help="build every asset spec")
    parser.add_argument("--changed", action="store_true",
                        help="build assets whose specs/builders changed vs the "
                             f"merge-base with {INTEGRATION_BRANCH}")
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
    cmd = prefix + pipeline_args
    print(f"Blender runner: {mode} mode")
    print("Running:", " ".join(cmd))
    result = subprocess.run(cmd, cwd=REPO_ROOT)
    return result.returncode


if __name__ == "__main__":
    sys.exit(main())
