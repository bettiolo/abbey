# Verification status — Unity now verifies locally through MCP

**Short version: the Unity/C# side now has a working local editor verification path through
MCP for Unity on macOS. CI still does not compile or test Unity until GameCI license secrets
are configured.**

## Current verified path

Runtime-affecting validation baseline: commit `63df7e2` was verified on 2026-07-05
using macOS with Unity `6000.5.2f1` through MCP for Unity. Later commits such as
`94d4e3e` and `815671f` only update validation/support docs and do not change Unity
runtime code, tests, assets, editor tools, or workflows. Do not bump this section only
to record another docs-only validation pass; update it when runtime/tooling/test inputs
or validation evidence change.

- `tools/restart_unity_mcp.sh` starts the pinned Unity editor and MCP bridge.
- `tools/run_unity_mcp_gate.sh --no-restart` runs the Unity gate, EditMode tests,
  PlayMode tests, and a final console check through MCP.
- Local MCP result for the validated runtime baseline:
  - Unity gate: passed (`unity/Build/reports/unity_gate_report.json`, generated
    `2026-07-05T14:09:44Z`)
  - Scene build: passed
  - Generated asset import validation: passed
  - EditMode tests: 180/180 passed
  - PlayMode tests: 39/39 passed
  - Console errors after the Unity gate: 0
  - Canonical screenshots written to `unity/Build/screenshots/`
- `./tools/check_all.sh` result on the same runtime tree: OK
  - design validation: 7/7 passed
  - asset validation: 258 passed, 8 skipped
  - Blender changed-asset verification: passed, no changed asset specs or builders
  - Unity batch steps: skipped because the editor was open; covered by the MCP gate above

Use this command when the editor/MCP bridge is not already running:

```sh
tools/run_unity_mcp_gate.sh
```

Use this faster form when MCP is already connected:

```sh
UNITY_MCP_UV_OFFLINE=1 tools/run_unity_mcp_gate.sh --no-restart
```

## CI status

GameCI still skips the Unity test job. `.github/workflows/unity.yml` gates the real test job
   behind a `license-check` that requires the `UNITY_LICENSE`, `UNITY_EMAIL`, and
   `UNITY_PASSWORD` repository secrets. Those secrets are **not configured**, so on every push
   the `license-check` job passes and the `Unity EditMode/PlayMode tests` job is **skipped**.
   The workflow therefore reports "success" **without ever compiling the C#.**

Latest observed GitHub "Unity tests" workflow was for runtime commit `63df7e2`:
[#28743459269](https://github.com/bettiolo/abbey/actions/runs/28743459269) →
`license-check` = success, `Unity … tests` = **skipped**. The "Blender assets" workflow,
by contrast, genuinely runs and passes when triggered. No GitHub Actions run was created
for docs-only validation-record commits `94d4e3e` or `815671f`.

## What this means

- Every **GameCI green** on this repo currently means **"Blender assets passed + Unity tests
  skipped."** It is **not** evidence that the game compiles or runs.
- Local macOS editor verification through MCP **is** evidence that the C# compiles, tests run,
  generated asset import validation passes, and canonical screenshots can be captured.
- `./tools/check_all.sh` still skips Unity steps while the Unity editor is open because
  batchmode cannot take the project lock. In that situation, run
  `tools/run_unity_mcp_gate.sh --no-restart` for the Unity side.
- The Phase 2 runtime is no longer "authored but unverified"; it is **locally verified through
  MCP**, but still **unverified in CI**.

## How to actually run / verify it

- **Verify locally through MCP:** run `tools/run_unity_mcp_gate.sh`.
- **Play it:** open `unity/` in the pinned Unity editor and follow
  [RUNNING_ON_MAC.md](RUNNING_ON_MAC.md) §1.
- **Make CI verify it:** add three secrets under GitHub → *Settings → Secrets and variables
  → Actions*:
  - `UNITY_LICENSE` — the contents of a Unity `.ulf` license file
  - `UNITY_EMAIL`, `UNITY_PASSWORD` — the Unity account credentials
  Once present, the `Unity EditMode/PlayMode tests` matrix job runs for real on each push, and
  a green Unity workflow will finally mean the C# compiles and the tests pass.
