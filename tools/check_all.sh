#!/usr/bin/env bash
# check_all.sh — the definition of done (AGENTS.md).
#
# Runs every validation the current environment supports and prints a summary
# table. Steps that cannot run here (no Blender output yet, no Unity editor)
# report SKIP, not FAIL. Exit code is nonzero iff at least one step FAILed.
#
# Usage: tools/check_all.sh [--full] [--write]
#   --full   run the Blender pipeline over --all specs instead of --changed
#   --write  regenerate blender/generated/ in place (intentional regeneration).
#            By default the pipeline runs in VERIFY mode: it builds into a temp
#            directory and compares against the committed artifacts, so a
#            default run leaves the working tree untouched.
#
# Step exit-code convention: 0 = PASS, 3 = SKIP, anything else = FAIL.
# Python tools run through uv when available, using tools/requirements-dev.txt.
# pytest exit 5 ("no tests collected") also counts as SKIP.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

FULL=0
WRITE=0
for arg in "$@"; do
  case "$arg" in
    --full) FULL=1 ;;
    --write) WRITE=1 ;;
    -h|--help) sed -n '2,16p' "$0"; exit 0 ;;
    *) echo "Unknown argument: $arg" >&2; exit 2 ;;
  esac
done

LOG_DIR="$(mktemp -d "${TMPDIR:-/tmp}/abbey_check_all.XXXXXX")"
declare -a STEP_NAMES=()
declare -a STEP_STATUSES=()
declare -a STEP_LOGS=()
ANY_FAIL=0

# run_step <name> <kind> <cmd...>
#   kind "pytest": exit 5 => SKIP; exit 0 with zero passed tests => SKIP
#   kind "cmd":    exit 3 => SKIP
run_step() {
  local name="$1" kind="$2"; shift 2
  local log="$LOG_DIR/$(echo "$name" | tr ' /' '__').log"
  echo ""
  echo "=== $name ==="
  echo "    \$ $*"
  "$@" >"$log" 2>&1
  local code=$?
  local status
  if [ "$code" -eq 0 ]; then
    status=PASS
    if [ "$kind" = "pytest" ] && ! grep -Eq '[0-9]+ passed' "$log"; then
      # collected but everything skipped (e.g. no generated assets yet)
      status=SKIP
    fi
  elif [ "$code" -eq 3 ] && [ "$kind" = "cmd" ]; then
    status=SKIP
  elif [ "$code" -eq 5 ] && [ "$kind" = "pytest" ]; then
    status=SKIP
  else
    status=FAIL
    ANY_FAIL=1
  fi

  tail -n 15 "$log" | sed 's/^/    /'
  echo "--- $name: $status (exit $code, full log: $log)"

  STEP_NAMES+=("$name")
  STEP_STATUSES+=("$status")
  STEP_LOGS+=("$log")
}

PY_CMD=(python3)
if command -v uv >/dev/null 2>&1; then
  export UV_CACHE_DIR="${UV_CACHE_DIR:-$REPO_ROOT/.uv-cache}"
  PY_CMD=(uv run --with-requirements tools/requirements-dev.txt python)
else
  echo "NOTE: uv not found; falling back to python3. Install uv for a hermetic Python tool env."
fi

# (a) design validation (REQUIREMENTS.yml consistency)
run_step "design validation (pytest)" pytest \
  "${PY_CMD[@]}" -m pytest tests/design_validation -q

# (b) blender asset pipeline
# Default: verify mode — build into a temp dir and compare against the
# committed blender/generated/ so the gate never dirties the working tree.
# --write regenerates in place (do that only for intentional regeneration).
PIPELINE_ARGS=()
if [ "$FULL" -eq 1 ]; then PIPELINE_ARGS+=(--all); else PIPELINE_ARGS+=(--changed); fi
if [ "$WRITE" -eq 0 ]; then PIPELINE_ARGS+=(--verify); fi
run_step "blender pipeline (${PIPELINE_ARGS[*]})" cmd \
  "${PY_CMD[@]}" tools/run_blender_asset_pipeline.py "${PIPELINE_ARGS[@]}"

# (c) generated asset validation
run_step "asset validation (pytest)" pytest \
  "${PY_CMD[@]}" -m pytest tests/asset_validation -q

# (d)/(e) unity tests
run_step "unity editmode tests" cmd \
  bash tools/run_unity_tests.sh editmode
run_step "unity playmode tests" cmd \
  bash tools/run_unity_tests.sh playmode

# (f) in-engine screenshot capture
run_step "unity screenshot capture" cmd \
  "${PY_CMD[@]}" tools/capture_unity_screenshot.py

echo ""
echo "==============================================="
echo " check_all summary"
echo "==============================================="
for i in "${!STEP_NAMES[@]}"; do
  printf ' %-32s %s\n' "${STEP_NAMES[$i]}" "${STEP_STATUSES[$i]}"
done
echo "==============================================="

if [ "$ANY_FAIL" -ne 0 ]; then
  echo "RESULT: FAIL (logs in $LOG_DIR)"
  exit 1
fi
echo "RESULT: OK (no failures; SKIPs are allowed where tooling/output is absent)"
exit 0
