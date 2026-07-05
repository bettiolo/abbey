#!/usr/bin/env bash
# Restart the local Unity editor for this project and start the MCP for Unity bridge.
#
# This is intentionally macOS-specific: it launches Unity through LaunchServices and uses
# Accessibility to invoke Unity menu items once the editor UI is ready.
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: tools/restart_unity_mcp.sh [--keep-temp-script] [--no-kill-port]

Stops any Unity editor already running this repo's unity/ project, launches the pinned
Unity editor, injects a temporary editor menu command, starts MCP for Unity, verifies
mcpforunity://instances, then removes the temporary script.

Options:
  --keep-temp-script  Leave Assets/Editor/AbbeyMcpBridgeAutoStart.cs in place for debugging.
  --no-kill-port      Do not stop an existing listener on MCP_PORT before launching Unity.
  -h, --help          Show this help.

Environment overrides:
  UNITY_APP           Path to Unity.app. Defaults to the version pinned in ProjectVersion.txt.
  UNITY_PATH          Path to Unity binary or Unity.app. Used if UNITY_APP is unset.
  UNITY_BIN           Path to Unity binary. Used if UNITY_APP and UNITY_PATH are unset.
  MCP_PORT            MCP HTTP port. Default: 8080.
  UNITY_LOG           Unity editor log path. Default: /tmp/abbey-unity-editor.log.
  STOP_TIMEOUT        Seconds to wait before force-killing old processes. Default: 20.
  START_TIMEOUT       Seconds to wait for Unity's menu bar. Default: 180.
  MENU_TIMEOUT        Seconds to retry the injected start menu. Default: 120.
  MCP_TIMEOUT         Seconds to wait for MCP resource verification. Default: 120.
EOF
}

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_PATH="$REPO_ROOT/unity"
VERSION_FILE="$PROJECT_PATH/ProjectSettings/ProjectVersion.txt"

UNITY_VERSION="6000.5.2f1"
if [ -f "$VERSION_FILE" ]; then
  PINNED="$(awk '/^m_EditorVersion:/ {print $2}' "$VERSION_FILE")"
  if [ -n "$PINNED" ]; then
    UNITY_VERSION="$PINNED"
  fi
fi

MCP_PORT="${MCP_PORT:-8080}"
UNITY_LOG="${UNITY_LOG:-/tmp/abbey-unity-editor.log}"
MCP_START_LOG="/tmp/abbey_mcp_autostart.log"
STOP_TIMEOUT="${STOP_TIMEOUT:-20}"
START_TIMEOUT="${START_TIMEOUT:-180}"
MENU_TIMEOUT="${MENU_TIMEOUT:-120}"
MCP_TIMEOUT="${MCP_TIMEOUT:-120}"

KEEP_TEMP_SCRIPT=0
KILL_PORT=1
while [ "$#" -gt 0 ]; do
  case "$1" in
    --keep-temp-script) KEEP_TEMP_SCRIPT=1 ;;
    --no-kill-port) KILL_PORT=0 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "ERROR: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
  shift
done

EDITOR_DIR="$PROJECT_PATH/Assets/Editor"
EDITOR_META="$PROJECT_PATH/Assets/Editor.meta"
TEMP_SCRIPT="$EDITOR_DIR/AbbeyMcpBridgeAutoStart.cs"
EDITOR_DIR_EXISTED=0
EDITOR_META_EXISTED=0

cleanup() {
  if [ "$KEEP_TEMP_SCRIPT" -eq 1 ]; then
    return
  fi

  rm -f "$TEMP_SCRIPT" "$TEMP_SCRIPT.meta"
  if [ "$EDITOR_DIR_EXISTED" -eq 0 ]; then
    rmdir "$EDITOR_DIR" 2>/dev/null || true
  fi
  if [ ! -d "$EDITOR_DIR" ] && [ "$EDITOR_META_EXISTED" -eq 0 ]; then
    rm -f "$EDITOR_META"
  fi
}
trap cleanup EXIT

fail() {
  echo "ERROR: $*" >&2
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "$1 is required but was not found."
}

resolve_unity_app() {
  if [ -n "${UNITY_APP:-}" ]; then
    echo "$UNITY_APP"
    return 0
  fi

  local configured="${UNITY_PATH:-${UNITY_BIN:-}}"
  if [ -n "$configured" ]; then
    case "$configured" in
      *.app) echo "$configured"; return 0 ;;
      */Unity.app/Contents/MacOS/Unity) echo "${configured%/Contents/MacOS/Unity}"; return 0 ;;
      *) fail "UNITY_PATH/UNITY_BIN must point to Unity.app or Unity.app/Contents/MacOS/Unity." ;;
    esac
  fi

  local candidates=(
    "$HOME/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app"
    "/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app"
  )
  local candidate
  for candidate in "${candidates[@]}"; do
    if [ -d "$candidate" ]; then
      echo "$candidate"
      return 0
    fi
  done

  fail "Unity $UNITY_VERSION was not found. Install it with Unity Hub or set UNITY_APP."
}

unity_project_pids() {
  ps -axo pid,command | awk -v project="$PROJECT_PATH" '
    NR > 1 &&
    index($0, "Unity.app/Contents/MacOS/Unity") &&
    index($0, project) &&
    index($0, "AssetImportWorker") == 0 {
      print $1
    }
  '
}

mcp_port_pids() {
  lsof -tiTCP:"$MCP_PORT" -sTCP:LISTEN 2>/dev/null | sort -u || true
}

pid_list() {
  "$@" | tr '\n' ' ' | awk '{$1=$1; print}'
}

stop_matching_pids() {
  local label="$1"
  shift
  local pids
  pids="$(pid_list "$@")"
  if [ -z "$pids" ]; then
    echo "$label: none running."
    return 0
  fi

  echo "Stopping $label: $pids"
  local pid
  for pid in $pids; do
    kill -TERM "$pid" 2>/dev/null || true
  done

  local elapsed
  for elapsed in $(seq 1 "$STOP_TIMEOUT"); do
    sleep 1
    pids="$(pid_list "$@")"
    if [ -z "$pids" ]; then
      return 0
    fi
  done

  echo "Force stopping $label: $pids"
  for pid in $pids; do
    kill -KILL "$pid" 2>/dev/null || true
  done
}

write_menu_helper() {
  local helper="$1"
  cat >"$helper" <<'SWIFT'
import ApplicationServices
import Foundation

let environment = ProcessInfo.processInfo.environment
guard let pidString = environment["UNITY_PID"], let pidValue = Int32(pidString) else {
    fputs("UNITY_PID is missing or invalid\n", stderr)
    exit(2)
}
guard let pathString = environment["MENU_PATH"], !pathString.isEmpty else {
    fputs("MENU_PATH is missing\n", stderr)
    exit(2)
}
let mode = environment["MENU_MODE"] ?? "press"
let path = pathString.split(separator: "|").map(String.init)
if path.isEmpty {
    fputs("MENU_PATH is empty\n", stderr)
    exit(2)
}

if !AXIsProcessTrusted() {
    fputs("Accessibility permission is not granted to this terminal/Codex app.\n", stderr)
}

func attr(_ element: AXUIElement, _ name: String) -> AnyObject? {
    var value: AnyObject?
    let error = AXUIElementCopyAttributeValue(element, name as CFString, &value)
    return error == .success ? value : nil
}

func children(_ element: AXUIElement) -> [AXUIElement] {
    return attr(element, "AXChildren") as? [AXUIElement] ?? []
}

func title(_ element: AXUIElement) -> String {
    return attr(element, "AXTitle").map { "\($0)" } ?? ""
}

func findChild(named name: String, in parent: AXUIElement) -> AXUIElement? {
    for _ in 0..<20 {
        for child in children(parent) {
            if title(child) == name {
                return child
            }
        }
        usleep(100_000)
    }
    return nil
}

let app = AXUIElementCreateApplication(pid_t(pidValue))
guard let menuBarObject = attr(app, "AXMenuBar") else {
    fputs("Unity menu bar is not available yet.\n", stderr)
    exit(1)
}
let menuBar = menuBarObject as! AXUIElement

var parent = menuBar
for (index, name) in path.enumerated() {
    guard let item = findChild(named: name, in: parent) else {
        fputs("Menu item not found: \(path.prefix(index + 1).joined(separator: " > "))\n", stderr)
        exit(1)
    }

    if mode == "exists" && index == path.count - 1 {
        print("found \(path.joined(separator: " > "))")
        exit(0)
    }

    let error = AXUIElementPerformAction(item, kAXPressAction as CFString)
    if error != .success {
        fputs("Could not press menu item \(name): \(error.rawValue)\n", stderr)
        exit(1)
    }
    usleep(350_000)

    if index < path.count - 1 {
        guard let submenu = children(item).first else {
            fputs("Submenu is not available: \(path.prefix(index + 1).joined(separator: " > "))\n", stderr)
            exit(1)
        }
        parent = submenu
    }
}

print("pressed \(path.joined(separator: " > "))")
SWIFT
}

unity_menu_action() {
  local pid="$1"
  local mode="$2"
  local path="$3"
  local helper_dir helper status
  helper_dir="$(mktemp -d "${TMPDIR:-/tmp}/abbey-unity-menu.XXXXXX")"
  helper="$helper_dir/menu.swift"
  write_menu_helper "$helper"

  set +e
  UNITY_PID="$pid" MENU_MODE="$mode" MENU_PATH="$path" swift "$helper"
  status=$?
  set -e

  rm -rf "$helper_dir"
  return "$status"
}

wait_for_unity_pid() {
  local elapsed pids
  for elapsed in $(seq 1 "$START_TIMEOUT"); do
    pids="$(pid_list unity_project_pids)"
    if [ -n "$pids" ]; then
      echo "$pids" | awk '{print $1}'
      return 0
    fi
    sleep 1
  done
  fail "Unity did not start for project $PROJECT_PATH within ${START_TIMEOUT}s."
}

wait_for_unity_menu_bar() {
  local pid="$1"
  local elapsed
  echo "Waiting for Unity menu bar..."
  for elapsed in $(seq 1 "$START_TIMEOUT"); do
    if ! kill -0 "$pid" 2>/dev/null; then
      fail "Unity process $pid exited. Check $UNITY_LOG."
    fi
    if unity_menu_action "$pid" exists "File" >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done
  fail "Unity menu bar did not become available. Grant Accessibility permission and check $UNITY_LOG."
}

press_menu_with_retry() {
  local pid="$1"
  local path="$2"
  local timeout="$3"
  local elapsed
  for elapsed in $(seq 1 "$timeout"); do
    if unity_menu_action "$pid" press "$path"; then
      return 0
    fi
    sleep 1
  done
  return 1
}

write_temp_autostart_script() {
  if [ -d "$EDITOR_DIR" ]; then
    EDITOR_DIR_EXISTED=1
  fi
  if [ -f "$EDITOR_META" ]; then
    EDITOR_META_EXISTED=1
  fi
  mkdir -p "$EDITOR_DIR"

  cat >"$TEMP_SCRIPT" <<'CSHARP'
#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

public static class AbbeyMcpBridgeAutoStart
{
    private const string LogPath = "/tmp/abbey_mcp_autostart.log";

    [MenuItem("Tools/Abbey/Start MCP Bridge Now")]
    public static void StartNow()
    {
        _ = StartBridgeAsync();
    }

    private static async Task StartBridgeAsync()
    {
        Append("attempt");
        try
        {
            var server = MCPServiceLocator.Server;
            if (!server.IsLocalHttpServerReachable())
            {
                var launched = server.StartLocalHttpServer(quiet: true);
                Append($"server launch requested={launched}");
                for (var i = 0; i < 120 && !server.IsLocalHttpServerReachable(); i++)
                {
                    await Task.Delay(500);
                }
            }
            else
            {
                Append("server already reachable");
            }

            Append($"server reachable={server.IsLocalHttpServerReachable()}");
            var started = await MCPServiceLocator.Bridge.StartAsync();
            Append($"bridge start result={started} running={MCPServiceLocator.Bridge.IsRunning}");
            var verify = await MCPServiceLocator.Bridge.VerifyAsync();
            Append($"verify success={verify.Success} message={verify.Message}");
        }
        catch (Exception ex)
        {
            Append($"error {ex}");
            Debug.LogException(ex);
        }
    }

    private static void Append(string message)
    {
        File.AppendAllText(LogPath, $"{DateTime.UtcNow:o} {message}{Environment.NewLine}");
        Debug.Log($"[AbbeyMcpBridgeAutoStart] {message}");
    }
}
#endif
CSHARP
}

wait_for_mcp_instance() {
  local url="http://127.0.0.1:${MCP_PORT}/mcp"
  uv run --with mcp python - "$url" "$MCP_TIMEOUT" <<'PYEOF'
import asyncio
import json
import sys
import time

from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client


URL = sys.argv[1]
TIMEOUT = float(sys.argv[2])


async def read_text(session, uri):
    result = await session.read_resource(uri)
    contents = getattr(result, "contents", [])
    if not contents:
        return ""
    return getattr(contents[0], "text", str(contents[0]))


async def try_once():
    async with streamablehttp_client(URL, terminate_on_close=False) as (read, write, _):
        async with ClientSession(read, write) as session:
            await session.initialize()
            instances_text = await read_text(session, "mcpforunity://instances")
            instances = json.loads(instances_text)
            if int(instances.get("instance_count", 0)) < 1:
                return False, f"no Unity instances: {instances_text}"
            editor_state = await read_text(session, "mcpforunity://editor/state")
            print(instances_text)
            print(editor_state)
            return True, ""


async def main():
    deadline = time.monotonic() + TIMEOUT
    last_error = "not attempted"
    while time.monotonic() < deadline:
        try:
            ok, last_error = await try_once()
            if ok:
                return 0
        except Exception as exc:  # noqa: BLE001
            last_error = f"{type(exc).__name__}: {exc}"
        await asyncio.sleep(1)

    print(f"Timed out waiting for MCP at {URL}. Last error: {last_error}", file=sys.stderr)
    return 1


sys.exit(asyncio.run(main()))
PYEOF
}

if [ "$(uname -s)" != "Darwin" ]; then
  fail "this helper is macOS-only because it uses open(1) and Accessibility menu automation."
fi
if [ ! -d "$PROJECT_PATH" ]; then
  fail "Unity project was not found at $PROJECT_PATH."
fi
require_command lsof
require_command swift
require_command uv

UNITY_APP_RESOLVED="$(resolve_unity_app)"
if [ ! -d "$UNITY_APP_RESOLVED" ]; then
  fail "Unity app does not exist: $UNITY_APP_RESOLVED"
fi

echo "Project: $PROJECT_PATH"
echo "Unity:  $UNITY_APP_RESOLVED"
echo "MCP:    http://127.0.0.1:${MCP_PORT}/mcp"

stop_matching_pids "Unity editors for this project" unity_project_pids
if [ "$KILL_PORT" -eq 1 ]; then
  stop_matching_pids "listeners on TCP port $MCP_PORT" mcp_port_pids
fi

rm -f "$MCP_START_LOG"
: >"$UNITY_LOG"

echo "Launching Unity..."
open -na "$UNITY_APP_RESOLVED" --args -projectPath "$PROJECT_PATH" -logFile "$UNITY_LOG"
UNITY_PID="$(wait_for_unity_pid)"
echo "Unity PID: $UNITY_PID"

wait_for_unity_menu_bar "$UNITY_PID"

echo "Installing temporary MCP bridge menu command..."
write_temp_autostart_script

echo "Refreshing Unity assets..."
if ! press_menu_with_retry "$UNITY_PID" "Assets|Refresh" 60; then
  fail "could not invoke Assets > Refresh. Grant Accessibility permission and check $UNITY_LOG."
fi

echo "Starting MCP bridge from Unity..."
if ! press_menu_with_retry "$UNITY_PID" "Tools|Abbey|Start MCP Bridge Now" "$MENU_TIMEOUT"; then
  echo "Last MCP autostart log, if any:" >&2
  tail -n 40 "$MCP_START_LOG" 2>/dev/null >&2 || true
  fail "could not invoke Tools > Abbey > Start MCP Bridge Now before timeout."
fi

echo "Verifying MCP resources..."
wait_for_mcp_instance

echo
echo "Unity MCP is ready."
echo "  Unity PID: $UNITY_PID"
echo "  MCP URL:   http://127.0.0.1:${MCP_PORT}/mcp"
echo "  Unity log: $UNITY_LOG"
echo "  Start log: $MCP_START_LOG"
