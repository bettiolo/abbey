#!/usr/bin/env bash
# Run Unity EditMode/PlayMode tests headlessly.
#
# Usage: tools/run_unity_tests.sh [editmode|playmode|all]   (default: all)
#
# Unity editor resolution: $UNITY_PATH, then common install locations, then PATH.
# Exit codes:
#   0 all requested test runs passed
#   1 at least one test failed (or Unity itself failed)
#   2 bad usage
#   3 SKIP: no Unity editor available (normal in the agent container; CI runs GameCI)
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_PATH="$REPO_ROOT/unity"
UNITY_VERSION="6000.5.2f1"
RESULTS_DIR="$REPO_ROOT/test-results"

MODE="${1:-all}"
case "$MODE" in
  editmode|playmode|all) ;;
  *) echo "Usage: $0 [editmode|playmode|all]" >&2; exit 2 ;;
esac

if [ ! -d "$PROJECT_PATH" ]; then
  echo "SKIP: Unity project not found at $PROJECT_PATH (task P01-01 not merged yet)."
  exit 3
fi

find_unity() {
  if [ -n "${UNITY_PATH:-}" ] && [ -x "${UNITY_PATH}" ]; then
    echo "$UNITY_PATH"; return 0
  fi
  local candidates=(
    "$HOME/Unity/Hub/Editor/$UNITY_VERSION/Editor/Unity"
    "/opt/unity/editors/$UNITY_VERSION/Editor/Unity"
    "/opt/unity/Editor/Unity"
    "/usr/bin/unity-editor"
    "/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"
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
  echo "SKIP: no Unity editor found (set UNITY_PATH or install $UNITY_VERSION)."
  echo "      Unity tests run in CI via GameCI (.github/workflows/unity.yml)."
  exit 3
fi

mkdir -p "$RESULTS_DIR"

# Summarize a NUnit3 results XML. Prints counts; exits 1 on failures.
summarize_results() {
  local xml="$1"
  python3 - "$xml" <<'PYEOF'
import sys
import xml.etree.ElementTree as ET

path = sys.argv[1]
try:
    root = ET.parse(path).getroot()
except Exception as exc:  # noqa: BLE001
    print(f"  could not parse results XML {path}: {exc}")
    sys.exit(1)

total = int(root.get("total", 0))
passed = int(root.get("passed", 0))
failed = int(root.get("failed", 0))
skipped = int(root.get("skipped", 0)) + int(root.get("inconclusive", 0))
print(f"  total={total} passed={passed} failed={failed} skipped={skipped}")

for case in root.iter("test-case"):
    if case.get("result") == "Failed":
        name = case.get("fullname") or case.get("name")
        msg = case.find("./failure/message")
        text = (msg.text or "").strip().splitlines()[0] if msg is not None and msg.text else ""
        print(f"  FAILED: {name}  {text}")

sys.exit(1 if failed > 0 or total == 0 else 0)
PYEOF
}

run_platform() {
  local platform="$1"          # EditMode | PlayMode
  local key
  key="$(echo "$platform" | tr '[:upper:]' '[:lower:]')"
  local xml="$RESULTS_DIR/unity-$key-results.xml"
  local log="$RESULTS_DIR/unity-$key.log"

  echo "== Unity $platform tests =="
  "$UNITY_BIN" -batchmode -nographics \
    -projectPath "$PROJECT_PATH" \
    -runTests -testPlatform "$platform" \
    -testResults "$xml" \
    -logFile - >"$log" 2>&1
  local unity_exit=$?

  if [ ! -f "$xml" ]; then
    echo "  Unity exited with code $unity_exit and produced no results XML."
    echo "  Last log lines ($log):"
    tail -n 20 "$log" | sed 's/^/    /'
    return 1
  fi

  summarize_results "$xml"
}

overall=0
if [ "$MODE" = "editmode" ] || [ "$MODE" = "all" ]; then
  run_platform EditMode || overall=1
fi
if [ "$MODE" = "playmode" ] || [ "$MODE" = "all" ]; then
  run_platform PlayMode || overall=1
fi

if [ "$overall" -eq 0 ]; then
  echo "Unity tests: PASS ($MODE)"
else
  echo "Unity tests: FAIL ($MODE)" >&2
fi
exit "$overall"
