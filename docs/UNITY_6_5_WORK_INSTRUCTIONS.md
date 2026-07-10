# Unity 6.5 implementation instructions for an LLM builder

Use this document as the prompt for a focused implementation session. The goal is to take
advantage of Unity 6.5 in this project after the third-party Unity MCP workflow has already
been added elsewhere.

## Role

You are a builder agent working in `/Users/mak/Code/bettiolo/abbey`. Implement a small,
reviewable set of Unity 6.5 improvements that make the Abbey prototype easier to verify
from an editor automation workflow.

Follow `AGENTS.md` exactly. In particular:

- Make small changes.
- Do not hand-edit generated files under `blender/generated/` or `unity/Assets/Generated/`.
- Do not create assets ad hoc. New generated assets must start with a spec in
  `blender/asset_specs/`.
- Keep balance values in data files or ScriptableObjects, not in MonoBehaviours.
- Run `./tools/check_all.sh` before finishing, or clearly document why a step skipped or
  failed.
- Unity may not be available in the agent environment. If Unity steps skip because no
  editor is installed, report that clearly.

## Assumptions

- The project is pinned to Unity `6000.5.2f1` in
  `unity/ProjectSettings/ProjectVersion.txt`.
- A third-party Unity MCP bridge is already implemented in another workflow. Do not spend
  this task installing or replacing MCP unless local repo files clearly show missing wiring.
- The useful pattern from Unreal Engine 5.8 MCP is editor automation: inspect project state,
  run editor actions, collect logs, capture screenshots, and return a concise report.
- This repo already has editor entry points for scene bootstrap, generated asset import
  validation, Unity tests, and screenshot capture. Extend that surface instead of inventing
  a parallel system.

## Scope

Implement the first safe slice from the list below. Prefer one or two related items in a
single branch. Do not attempt the whole list unless explicitly asked.

### Item 1: Abbey Unity editor gate command

Add a single editor CLI entry point that a human, Unity MCP, or CI can call to run the
Unity-side verification loop.

Suggested path:

- Add `unity/Assets/_Game/Scripts/Editor/AbbeyUnityGate.cs`.
- Add a menu item such as `Tools/Abbey/Run Unity Gate`.
- Add a batchmode method such as `Abbey.EditorTools.AbbeyUnityGate.RunFromCLI`.

The gate should run these steps in order where possible:

1. Build or open the generated prototype scene using the existing scene bootstrapper.
2. Run generated asset import validation using the existing importer or validator command.
3. Check for compile errors and log errors in the Unity console if an API is available.
4. Capture canonical screenshots through `ScreenshotCapture.CaptureCanonicalShots` or an
   equivalent public wrapper.
5. Write a machine-readable report to `unity/Build/reports/unity_gate_report.json`.

Keep the report small and deterministic:

```json
{
  "generatedAt": "ISO-8601 timestamp",
  "unityVersion": "6000.5.2f1",
  "sceneBuilt": true,
  "assetImportValidation": "pass",
  "canonicalScreenshots": ["day_camp.png", "dusk_recall.png"],
  "errors": []
}
```

Acceptance:

- The command compiles in Unity.
- The command can run in batchmode without depending on a hand-authored scene.
- If a step cannot run, the report includes a clear error and the CLI exits nonzero.
- Add focused EditMode tests for any pure report-building or validation logic.

### Item 2: Project Auditor report gate

Unity 6.5 strengthened Project Auditor with async analysis, obsolete API detection, and
unsafe `EntityId` checks. Add a small Abbey wrapper that exports an audit report.

Suggested path:

- Add `unity/Assets/_Game/Scripts/Editor/ProjectAuditRunner.cs`.
- Add `Tools/Abbey/Run Project Audit`.
- Add a CLI method such as `Abbey.EditorTools.ProjectAuditRunner.RunFromCLI`.
- Write output to `unity/Build/reports/project_audit_report.json` or `.md`.

Acceptance:

- If Project Auditor APIs are unavailable in this project, fail gracefully with a clear
  report instead of throwing.
- If compile errors prevent analysis, record that explicitly.
- Do not add Project Auditor as a package unless Unity requires it and the package choice is
  documented.

### Item 3: Expanded generated asset validation

The current Unity-side validator mostly checks anchor presence. Extend it to make Unity
imports more trustworthy.

Suggested files:

- `unity/Assets/_Game/Scripts/Editor/GeneratedAssetValidator.cs`
- `unity/Assets/_Game/Scripts/Editor/GeneratedAssetImporter.cs`
- `unity/Assets/_Game/Tests/EditMode/GeneratedAssetValidatorTests.cs`

Add checks for:

- Root object exists.
- At least one renderer exists for visual assets.
- Bounds are finite and nonzero.
- Pivot is near center-bottom for placeable assets.
- All metadata anchors exist as descendants.
- Materials are assigned and do not use missing shader placeholders.
- Optional: collider or footprint consistency if metadata already exposes enough data.

Acceptance:

- Existing import report shape remains backward compatible unless all call sites are updated.
- New validator logic is pure enough to test without importing actual GLB files.
- Existing tests continue to pass.

### Item 4: Light audit report

Light is the core mechanic. Add an editor report that samples the prototype map and shows
how much territory is Safe, Edge, or Dark.

Suggested path:

- Add `unity/Assets/_Game/Scripts/Editor/LightAuditRunner.cs`.
- Add `Tools/Abbey/Run Light Audit`.
- Write `unity/Build/reports/light_audit_report.json`.

Report contents:

- Sample bounds and grid spacing.
- Count and percentage of Safe, Edge, and Dark samples.
- List of active `LightSource` names, radii, fuel status, and sacred flag.
- Optional warnings for obvious problems, such as no Safe samples around the campfire.

Acceptance:

- Sampling uses `DarknessEvaluator.Classify`.
- The command builds or opens the prototype scene programmatically.
- No balance values are hard-coded in runtime MonoBehaviours.

### Item 5: Start UI Toolkit migration for overlays

Unity 6.5 added better UI Toolkit runtime support, `PanelRenderer`, UI Toolkit profiling,
and improved UI testing. Start migrating the debug UI away from `OnGUI`.

Suggested first target:

- `unity/Assets/_Game/Scripts/Runtime/Debug/DebugOverlay.cs`

Approach:

- Keep the existing `OnGUI` overlay working until the UI Toolkit replacement is verified.
- Add a new UI Toolkit overlay component in a separate file.
- Do not redesign the whole UI. Preserve the same debug information and hotkey behavior.
- Add tests only for pure formatting or data collection helpers.

Acceptance:

- Existing debug overlay behavior is not removed unless the replacement is tested in Unity.
- No gameplay values move into UI code.
- Text remains readable at common desktop resolutions.

## Recommended order

1. Start with Item 3 if you cannot run Unity locally. It has the best pure-test surface.
2. Start with Item 1 if Unity is available locally through MCP or a real editor.
3. Add Item 2 after Item 1, because both write reports into `unity/Build/reports/`.
4. Add Item 4 once the gate command exists.
5. Defer Item 5 unless the user asks for UI work or Unity verification is available.

## Current guardrails

- URP 17.5 is now the committed render pipeline. Keep gameplay logic pipeline-agnostic
  and route generated transient materials through `AbbeyMaterialFactory`.
- Do not add Netcode, Entities, Cinemachine, VFX Graph, ProBuilder, Addressables, or the
  new Input System just because Unity 6.5 updated them.
- Do not change the asset pipeline to use AI asset generation directly. The repo requires
  spec -> Blender script -> Blender headless -> GLB -> preview -> validation -> Unity
  import.
- Do not make Unity coverage a hard gate while CI lacks Unity license secrets.

## Verification commands

Run these from the repo root:

```sh
./tools/check_all.sh
```

If Unity is installed:

```sh
./tools/run_unity_tests.sh editmode
./tools/run_unity_tests.sh playmode
```

If you add a Unity CLI entry point, document the exact command in the final response. The
shape should be similar to:

```sh
Unity -batchmode -projectPath unity \
  -executeMethod Abbey.EditorTools.AbbeyUnityGate.RunFromCLI \
  -quit -logFile -
```

Screenshot capture requires a GPU context and should run without `-nographics`.

## Final response format

Report:

- Files changed.
- Which item(s) from this document were implemented.
- Commands run and their result.
- Whether Unity compile/tests ran or skipped.
- Any follow-up work that remains.

Keep the response short and concrete.
