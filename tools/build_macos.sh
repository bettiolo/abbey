#!/usr/bin/env bash
# Build the standalone macOS app (P01-13).
#
# Usage: tools/build_macos.sh
# Output: unity/Build/macOS/Abbey.app
#
# Unity editor resolution: $UNITY_PATH first, then the macOS Unity Hub install for
# the version pinned in unity/ProjectSettings/ProjectVersion.txt, then the same
# fallback locations the other tool scripts use, then PATH.
#
# Exit codes:
#   0 build succeeded and Abbey.app exists
#   1 Unity ran but the build failed
#   2 bad usage / broken project checkout
#   3 SKIP: no Unity editor available (normal in the agent container)
set -uo pipefail

if [ "$#" -ne 0 ]; then
  echo "Usage: $0   (no arguments)" >&2
  exit 2
fi

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_PATH="$REPO_ROOT/unity"
APP_PATH="$PROJECT_PATH/Build/macOS/Abbey.app"
EXECUTE_METHOD="Abbey.EditorTools.Builds.BuildMacOS"

if [ ! -d "$PROJECT_PATH" ]; then
  echo "ERROR: Unity project not found at $PROJECT_PATH." >&2
  exit 2
fi

VERSION_FILE="$PROJECT_PATH/ProjectSettings/ProjectVersion.txt"
UNITY_VERSION="6000.5.2f1"
if [ -f "$VERSION_FILE" ]; then
  PINNED="$(awk '/^m_EditorVersion:/ {print $2}' "$VERSION_FILE")"
  if [ -n "$PINNED" ]; then
    UNITY_VERSION="$PINNED"
  fi
fi

find_unity() {
  if [ -n "${UNITY_PATH:-}" ] && [ -x "${UNITY_PATH}" ]; then
    echo "$UNITY_PATH"; return 0
  fi
  local candidates=(
    "/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"
    "$HOME/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"
    "$HOME/Unity/Hub/Editor/$UNITY_VERSION/Editor/Unity"
    "/opt/unity/editors/$UNITY_VERSION/Editor/Unity"
    "/opt/unity/Editor/Unity"
    "/usr/bin/unity-editor"
  )
  local c
  for c in "${candidates[@]}"; do
    if [ -x "$c" ]; then echo "$c"; return 0; fi
  done
  command -v unity-editor 2>/dev/null && return 0
  command -v Unity 2>/dev/null && return 0
  return 1
}

UNITY_BIN="$(find_unity || true)"
if [ -z "$UNITY_BIN" ]; then
  echo "SKIP: no Unity editor found (set UNITY_PATH or install $UNITY_VERSION via Unity Hub,"
  echo "      including the 'Mac Build Support (Mono)' module)."
  echo "      On a Mac this script then runs:"
  echo "      Unity -batchmode -quit -projectPath unity -executeMethod $EXECUTE_METHOD -logFile -"
  exit 3
fi

echo "Building macOS app with: $UNITY_BIN (editor $UNITY_VERSION)"
"$UNITY_BIN" -batchmode -quit \
  -projectPath "$PROJECT_PATH" \
  -executeMethod "$EXECUTE_METHOD" \
  -logFile -
UNITY_EXIT=$?

if [ "$UNITY_EXIT" -ne 0 ]; then
  echo "macOS build FAILED (Unity exit code $UNITY_EXIT)." >&2
  exit "$UNITY_EXIT"
fi

if [ ! -d "$APP_PATH" ]; then
  echo "Unity exited 0 but $APP_PATH is missing." >&2
  exit 1
fi

echo "macOS build OK: $APP_PATH"
echo "First launch of an unsigned local build: right-click Abbey.app -> Open (Gatekeeper)."
exit 0
