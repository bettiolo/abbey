# Unity MCP on macOS

Use this helper when an agent needs to connect to the local Unity editor through
MCP for Unity:

```sh
./tools/restart_unity_mcp.sh
```

Once the bridge is connected, use the combined verification helper:

```sh
tools/run_unity_mcp_gate.sh --no-restart
```

That command runs `Tools > Abbey > Run Unity Gate`, then EditMode and PlayMode
tests through MCP, then prints final Unity console errors. If MCP is not already
connected, omit `--no-restart` and it will call `restart_unity_mcp.sh` first.

The script does the same sequence that worked by hand:

1. Stops Unity editors already running this repo's `unity/` project.
2. Stops the existing listener on `MCP_PORT` (`8080` by default).
3. Launches the pinned Unity editor with `open -na ... -projectPath unity`.
4. Adds a temporary editor script that exposes
   `Tools > Abbey > Start MCP Bridge Now`.
5. Uses macOS Accessibility to invoke `Assets > Refresh`, then the temporary
   menu command.
6. Verifies `mcpforunity://instances` and `mcpforunity://editor/state`.
7. Removes the temporary editor script before exiting.

## Requirements

- macOS with Unity Hub and the version pinned in
  `unity/ProjectSettings/ProjectVersion.txt`.
- The Unity package `com.coplaydev.unity-mcp` installed in the project.
- `uv`, because the final verification uses a direct Python MCP client.
- Xcode Command Line Tools, or another install that provides `swift`.
- Accessibility permission for the app running the command. On macOS this is in
  **System Settings > Privacy & Security > Accessibility**. Enable the terminal,
  IDE, or Codex host app that launches the script.

## Overrides

```sh
UNITY_APP=/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app ./tools/restart_unity_mcp.sh
UNITY_PATH=/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity ./tools/restart_unity_mcp.sh
MCP_PORT=8081 ./tools/restart_unity_mcp.sh
```

Useful timeouts:

```sh
STOP_TIMEOUT=30 START_TIMEOUT=240 MENU_TIMEOUT=180 MCP_TIMEOUT=180 ./tools/restart_unity_mcp.sh
```

Debug options:

```sh
./tools/restart_unity_mcp.sh --keep-temp-script
./tools/restart_unity_mcp.sh --no-kill-port
tools/run_unity_mcp_gate.sh --skip-tests
UNITY_MCP_UV_OFFLINE=1 tools/run_unity_mcp_gate.sh --no-restart
```

## Manual fallback

If Accessibility automation is blocked, use the editor UI:

1. Open `unity/` in the pinned Unity editor.
2. Open **Window > MCP for Unity > Toggle MCP Window**.
3. Start the MCP server and bridge from the Connect tab.
4. Verify with the same direct MCP client path the script uses:

```sh
uv run --with mcp python - <<'PY'
import asyncio
from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client

async def main():
    async with streamablehttp_client("http://127.0.0.1:8080/mcp", terminate_on_close=False) as (read, write, _):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.read_resource("mcpforunity://instances")
            print(result.contents[0].text)

asyncio.run(main())
PY
```

## Troubleshooting

- `Unity menu bar is not available yet`: wait for the project to finish opening,
  or grant Accessibility permission to the app running the script.
- `Menu item not found: Tools > Abbey > Start MCP Bridge Now`: Unity has not
  compiled the temporary editor script yet. Rerun the script or increase
  `MENU_TIMEOUT`.
- Port `8080` is occupied: stop the process manually, use the script default
  port cleanup, or set `MCP_PORT` if the Unity MCP package is configured for a
  different port.
- Unity opens but MCP never verifies: check `/tmp/abbey-unity-editor.log`,
  `/tmp/abbey_mcp_autostart.log`, and
  `~/Library/Application Support/UnityMCP/Logs/unity_mcp_server.log`.

The helper intentionally uses a temporary Unity editor script because the MCP
server and bridge start APIs live inside the Unity editor. Once the bridge is up,
agents should prefer MCP resources such as `mcpforunity://editor/state` and
`mcpforunity://instances` to verify state before using tools.
