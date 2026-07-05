#!/usr/bin/env bash
# Run the Unity-side verification loop through an already installed MCP for Unity setup.
#
# This is the fast local path for macOS/editor verification:
#   1. Reuse a connected Unity MCP instance, or start one with restart_unity_mcp.sh.
#   2. Run Tools/Abbey/Run Unity Gate.
#   3. Run EditMode and PlayMode tests through MCP.
#   4. Print final console errors and report paths.
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: tools/run_unity_mcp_gate.sh [--no-restart] [--skip-tests]

Options:
  --no-restart   Fail instead of running tools/restart_unity_mcp.sh when MCP is disconnected.
  --skip-tests   Run only the Unity gate and final console check.
  -h, --help     Show this help.

Environment overrides:
  MCP_CLI_TIMEOUT        Seconds for individual unity-mcp CLI calls. Default: 180.
  TEST_POLL_TIMEOUT      Seconds each test poll may wait server-side. Default: 60.
  UNITY_MCP_UV_OFFLINE   Set to 1 to add uv --offline for cached MCP packages.
  UV_CACHE_DIR           Defaults to .uv-cache in the repo.
EOF
}

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

NO_RESTART=0
SKIP_TESTS=0
while [ "$#" -gt 0 ]; do
  case "$1" in
    --no-restart) NO_RESTART=1 ;;
    --skip-tests) SKIP_TESTS=1 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "ERROR: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
  shift
done

export UV_CACHE_DIR="${UV_CACHE_DIR:-$REPO_ROOT/.uv-cache}"
MCP_CLI_TIMEOUT="${MCP_CLI_TIMEOUT:-180}"
TEST_POLL_TIMEOUT="${TEST_POLL_TIMEOUT:-60}"
REPORT_PATH="unity/Build/reports/unity_gate_report.json"

UV_BASE=(uv run)
if [ "${UNITY_MCP_UV_OFFLINE:-0}" = "1" ]; then
  UV_BASE+=(--offline)
fi
MCP_CMD=("${UV_BASE[@]}" --with mcpforunityserver unity-mcp -t "$MCP_CLI_TIMEOUT")
PY_CMD=(uv run python)

json_get() {
  local expr="$1"
  "${PY_CMD[@]}" -c '
import json
import sys

data = json.load(sys.stdin)
safe_builtins = {"len": len, "int": int, "str": str}
value = eval(sys.argv[1], {"__builtins__": safe_builtins}, {"data": data})
if isinstance(value, bool):
    print("true" if value else "false")
elif value is None:
    print("")
else:
    print(value)
' "$expr"
}

mcp_json() {
  "${MCP_CMD[@]}" --format json "$@"
}

has_instance() {
  local output
  if ! output="$(mcp_json instances 2>/dev/null)"; then
    return 1
  fi
  local count
  count="$(printf '%s' "$output" | json_get 'len(data.get("instances") or [])')"
  [ "${count:-0}" -gt 0 ]
}

ensure_mcp() {
  if has_instance; then
    echo "Unity MCP: connected."
    return 0
  fi

  if [ "$NO_RESTART" -eq 1 ]; then
    echo "ERROR: Unity MCP is not connected and --no-restart was set." >&2
    return 1
  fi

  echo "Unity MCP: disconnected; starting bridge..."
  ./tools/restart_unity_mcp.sh
}

run_unity_gate() {
  echo
  echo "=== unity gate (MCP) ==="
  rm -f "$REPORT_PATH"
  mcp_json raw execute_menu_item '{"menu_path":"Tools/Abbey/Run Unity Gate"}'

  local elapsed
  for elapsed in $(seq 1 "$MCP_CLI_TIMEOUT"); do
    if [ -f "$REPORT_PATH" ]; then
      break
    fi
    sleep 1
  done

  if [ ! -f "$REPORT_PATH" ]; then
    echo "ERROR: Unity gate did not write $REPORT_PATH within ${MCP_CLI_TIMEOUT}s." >&2
    return 1
  fi

  sed -n '1,220p' "$REPORT_PATH"
  local passed
  passed="$(json_get 'data.get("passed")' <"$REPORT_PATH")"
  if [ "$passed" != "true" ]; then
    echo "ERROR: Unity gate failed. Report: $REPORT_PATH" >&2
    return 1
  fi
}

run_tests() {
  local mode="$1"
  echo
  echo "=== unity ${mode} tests (MCP) ==="

  local start_json job_id status poll_json failed result_state
  start_json="$(mcp_json raw run_tests "{\"mode\":\"${mode}\",\"include_failed_tests\":true,\"init_timeout\":120000}")"
  printf '%s\n' "$start_json"
  job_id="$(printf '%s' "$start_json" | json_get 'data["result"]["data"]["job_id"]')"
  if [ -z "$job_id" ]; then
    echo "ERROR: Unity ${mode} test job did not return a job_id." >&2
    return 1
  fi

  while :; do
    poll_json="$(mcp_json raw get_test_job "{\"job_id\":\"${job_id}\",\"include_failed_tests\":true,\"wait_timeout\":${TEST_POLL_TIMEOUT}}")"
    status="$(printf '%s' "$poll_json" | json_get 'data["result"]["data"]["status"]')"
    if [ "$status" != "running" ]; then
      break
    fi
  done

  printf '%s\n' "$poll_json"
  failed="$(printf '%s' "$poll_json" | json_get 'data["result"]["data"]["result"]["summary"]["failed"]')"
  result_state="$(printf '%s' "$poll_json" | json_get 'data["result"]["data"]["result"]["summary"]["resultState"]')"
  if [ "$status" != "succeeded" ] || [ "${failed:-1}" != "0" ] || [ "$result_state" != "Passed" ]; then
    echo "ERROR: Unity ${mode} tests failed or did not complete cleanly." >&2
    return 1
  fi
}

read_final_console() {
  echo
  echo "=== unity console errors (MCP) ==="
  mcp_json raw read_console '{"type":"error","count":50,"include_stacktrace":false}'
}

ensure_mcp
run_unity_gate
if [ "$SKIP_TESTS" -eq 0 ]; then
  run_tests EditMode
  run_tests PlayMode
fi
read_final_console

echo
echo "Unity MCP gate complete."
echo "  Report:      $REPORT_PATH"
echo "  Screenshots: unity/Build/screenshots/"
