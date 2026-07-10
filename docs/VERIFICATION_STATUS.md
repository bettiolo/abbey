# Verification status — Unity verifies locally through MCP; CI will not run Unity

**Short version: the Unity/C# side has a working local editor verification path through
MCP for Unity on macOS, and that local gate is the project's compile/test authority.
CI does not compile or test Unity, and this is a standing decision, not a temporary gap:
the repo owner is on Unity Personal (free) and will not be configuring GameCI license
secrets.**

## Current verified path

Runtime-affecting validation baseline: commit `63bda64` was verified on 2026-07-10 using
macOS with Unity `6000.5.2f1` through MCP for Unity. This baseline covers the Phase 2/3
game, Phase 4 tasks `P4-01` through `P4-05`, and the URP 17.5 migration: both generated
maps, the Stag/covenant and forest story systems, campaign carryover/transition, 49
generated imports, committed URP settings, shared URP material creation, and the draft
reversible Mini World sprite-projection candidate. `GATE-P4` and `GATE-P2D-ART` remain
pending human review. Do not bump this section only to record another
docs-only validation pass; update it when runtime/tooling, test inputs, generated Unity
imports, renderer configuration, or validation evidence change.

- `tools/restart_unity_mcp.sh` starts the pinned Unity editor and MCP bridge.
- `tools/run_unity_mcp_gate.sh --no-restart` runs the Unity gate, EditMode tests,
  PlayMode tests, and a final console check through MCP.
- Local MCP result for the validated runtime baseline:
  - Unity gate: passed (`unity/Build/reports/unity_gate_report.json`, generated
    `2026-07-10T21:12:03Z`)
  - Scene builds: passed for Prototype01 and Map2Prototype
  - Generated asset import validation: passed, 49/49 imported assets
  - Mini World sprite manifest/import/catalog validation: passed, 32 selected sheets,
    341 slices, and 61 mapped roles
  - EditMode tests: 415/415 passed
  - PlayMode tests: 70/70 passed
  - Console errors after the Unity gate: 0
  - MCP graphics inspection: all six sprite candidate proof screenshots visually inspected;
    XZ terrain tiling, phase tint, and reversible mapped actors/buildings are visible
- `./tools/check_all.sh` result on the same runtime tree: OK
  - design validation: 7/7 passed
  - asset validation: 355 passed, 8 skipped
  - Blender changed-asset verification: all 49 assets matched a clean rebuild
  - Unity batch steps: skipped because the editor was open; covered by the MCP gate above
  - Sprite candidate caveat: 23 roles are explicitly unresolved by the pinned CC0 pack,
    so their identity-preserving 3D fallbacks keep `P2D-07` and `GATE-P2D-ART` pending

Use this command when the editor/MCP bridge is not already running:

```sh
tools/run_unity_mcp_gate.sh
```

Use this faster form when MCP is already connected:

```sh
UNITY_MCP_UV_OFFLINE=1 tools/run_unity_mcp_gate.sh --no-restart
```

## CI status — Unity job permanently skipped (by decision)

GameCI skips the Unity test job. `.github/workflows/unity.yml` gates the real test job
behind a `license-check` that requires the `UNITY_LICENSE`, `UNITY_EMAIL`, and
`UNITY_PASSWORD` repository secrets. Those secrets are **not configured and will not be**:
the repo owner uses Unity Personal (free), which as far as we know does not support this
CI licensing flow. So on every push the `license-check` job passes and the
`Unity EditMode/PlayMode tests` job is **skipped**, and the workflow reports "success"
**without ever compiling the C#.** The "Blender assets" workflow, by contrast, genuinely
runs and passes.

The workflow is kept (rather than deleted) so that the door stays open: if licensing ever
changes — GameCI does document a manual-activation path that can produce a `.ulf` for
Personal licenses, should we ever want to try it — adding the three secrets flips the job
on with no other changes.
Observed GitHub "Unity tests" workflows on `main` continue to be non-authoritative:
`license-check` can succeed while the `Unity … tests` matrix is **skipped**. The
"Blender assets" workflow, by contrast, genuinely runs when triggered.

## What this means

- Every **GameCI green** on this repo means **"Blender assets passed + Unity tests
  skipped."** It is **never** evidence that the game compiles or runs.
- The **local MCP gate is the compile/test authority** for this project. Local macOS editor
  verification through MCP **is** evidence that the C# compiles, tests run, generated asset
  import validation passes, and canonical screenshots can be captured.
- **Process rule:** run `tools/run_unity_mcp_gate.sh` on the Mac before merging any change
  that touches `unity/` (or generated assets Unity imports). There is no CI backstop.
- `./tools/check_all.sh` still skips Unity steps while the Unity editor is open because
  batchmode cannot take the project lock. In that situation, run
  `tools/run_unity_mcp_gate.sh --no-restart` for the Unity side.
- The Phase 2/3 runtime, Phase 4 implementation, and URP migration are **locally verified
  through MCP**. Phase 4 still needs the separate human replayability verdict; CI
  verification is out of scope.

## How to actually run / verify it

- **Verify locally through MCP:** run `tools/run_unity_mcp_gate.sh`. This is the
  authoritative gate.
- **Play it:** open `unity/` in the pinned Unity editor and follow
  [RUNNING_ON_MAC.md](RUNNING_ON_MAC.md) §1.
