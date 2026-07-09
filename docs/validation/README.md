# Validation guides (Phase 3 onward)

Every Phase 3 task ships a validation guide in this directory, named after the task id
(`P3-01.md`, `P3-02.md`, …). The guide is written for an LLM (or human) driving the
**local Unity editor through MCP for Unity** — because CI never compiles or tests Unity
code on this repo (see [VERIFICATION_STATUS.md](../VERIFICATION_STATUS.md)); the local
MCP gate is the compile/test authority.

## Latest runtime validation

Phase 4's implementation candidate is locally verified at commit `13957fb`
(2026-07-10, Unity `6000.5.2f1`, MCP for Unity):

- `UNITY_MCP_UV_OFFLINE=1 MCP_CLI_TIMEOUT=300 TEST_POLL_TIMEOUT=180 tools/run_unity_mcp_gate.sh --no-restart`
  passed: both scene builds/import/screenshots, EditMode 388/388, PlayMode 70/70,
  console errors 0.
- `./tools/check_all.sh` passed: design 7/7, asset validation 335 passed /
  8 skipped, all 49 assets matched a clean rebuild; Unity batch steps skipped
  because the editor was open and are covered by the MCP gate.
- `GATE-P4` is ready but pending human review; automated validation is not its verdict.

Each guide must let a fresh session with Unity-MCP access verify the task with no other
context. Required sections:

1. **What changed** — one paragraph: the system, its config asset, its debug surface.
2. **Technical verification** — exact commands and expected results:
   - `tools/restart_unity_mcp.sh` to start the pinned editor + MCP bridge (or
     `UNITY_MCP_UV_OFFLINE=1 tools/run_unity_mcp_gate.sh --no-restart` when already
     connected);
   - `tools/run_unity_mcp_gate.sh` for the full gate (Unity gate, EditMode, PlayMode,
     console check);
   - which test fixtures belong to this task and the expected pass counts (state the
     new EditMode/PlayMode totals so a regression is obvious);
   - `./tools/check_all.sh` in a non-Unity container (Unity steps SKIP is normal there).
3. **Visual verification** — what to look at in the editor or in captured screenshots:
   which debug panel/overlay to open, which scene to build
   (`Tools → Abbey → Build Prototype Scene`), what to trigger, and what "correct"
   looks like (concrete, e.g. "night length bar grows from Spring to Winter",
   "razed home's light gizmo disappears").

Keep guides short and imperative. If a task changes balance data, name the
ScriptableObject (Resources path) so the verifier can inspect values instead of
hunting through MonoBehaviours.

Phase 4's integrated Map 2 candidate is covered by
[P4-02-through-P4-05.md](P4-02-through-P4-05.md); the earlier Map 1 mechanics promotion
remains in [P4-01-map1-systems-test.md](P4-01-map1-systems-test.md).
