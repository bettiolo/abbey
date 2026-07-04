#!/usr/bin/env python3
"""Capture in-engine Unity screenshots headlessly (CI proof-of-look step).

HONEST STATUS: this is the harness half only. It requires
  1. a Unity editor install (UNITY_PATH or a common install location), and
  2. the editor-side capture method delivered by task P01-09:
     ``Abbey.Editor.ScreenshotCapture.CaptureFromCLI`` — which builds the
     prototype scene programmatically (Tools -> Abbey -> Build Prototype Scene),
     positions the locked iso camera, and writes PNGs to
     ``unity/Screenshots/``.

CI path (documented for .github/workflows and future agents):
  Unity -batchmode -projectPath unity \
        -executeMethod Abbey.Editor.ScreenshotCapture.CaptureFromCLI \
        -quit -logFile -
Screenshots are then uploaded as a CI artifact for human review gates.

Without a Unity editor this script exits 3 (SKIP). It never fakes output.
"""

from __future__ import annotations

import os
import shutil
import subprocess
import sys
from pathlib import Path

EXIT_SKIP = 3

REPO_ROOT = Path(__file__).resolve().parent.parent
PROJECT_PATH = REPO_ROOT / "unity"
UNITY_VERSION = "6000.5.2f1"
EXECUTE_METHOD = "Abbey.Editor.ScreenshotCapture.CaptureFromCLI"
OUTPUT_DIR = PROJECT_PATH / "Screenshots"


def unity_version() -> str:
    version_file = PROJECT_PATH / "ProjectSettings" / "ProjectVersion.txt"
    if version_file.is_file():
        for line in version_file.read_text(encoding="utf-8").splitlines():
            if line.startswith("m_EditorVersion:"):
                version = line.split(":", 1)[1].strip()
                if version:
                    return version
    return UNITY_VERSION


def find_unity() -> str | None:
    env = os.environ.get("UNITY_PATH")
    if env and os.access(env, os.X_OK):
        return env
    version = unity_version()
    candidates = [
        Path.home() / f"Unity/Hub/Editor/{version}/Editor/Unity",
        Path.home() / f"Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity",
        Path(f"/opt/unity/editors/{version}/Editor/Unity"),
        Path("/opt/unity/Editor/Unity"),
        Path("/usr/bin/unity-editor"),
        Path(f"/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity"),
    ]
    for c in candidates:
        if c.is_file() and os.access(c, os.X_OK):
            return str(c)
    return shutil.which("unity-editor") or shutil.which("Unity")


def main() -> int:
    if not PROJECT_PATH.is_dir():
        print(f"SKIP: Unity project not found at {PROJECT_PATH} (task P01-01 not merged yet).")
        return EXIT_SKIP

    unity = find_unity()
    if unity is None:
        print("SKIP: no Unity editor available; screenshot capture runs in CI (GameCI).")
        print(f"      When present, this runs: Unity -batchmode -projectPath unity "
              f"-executeMethod {EXECUTE_METHOD} -quit -logFile -")
        return EXIT_SKIP

    cmd = [
        unity, "-batchmode",
        "-projectPath", str(PROJECT_PATH),
        "-executeMethod", EXECUTE_METHOD,
        "-quit", "-logFile", "-",
    ]
    print("Running:", " ".join(cmd))
    result = subprocess.run(cmd)
    if result.returncode != 0:
        print(f"Screenshot capture failed (exit {result.returncode}). "
              f"The editor method {EXECUTE_METHOD} is delivered by task P01-09; "
              "until it is merged, failure here is expected.", file=sys.stderr)
        return result.returncode

    pngs = sorted(OUTPUT_DIR.glob("*.png")) if OUTPUT_DIR.is_dir() else []
    if not pngs:
        print(f"Unity exited 0 but no screenshots found under {OUTPUT_DIR}.", file=sys.stderr)
        return 1
    print(f"Captured {len(pngs)} screenshot(s):")
    for p in pngs:
        print(f"  {p.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
