# Verification status — the Unity game is currently unrunnable in automation

**Short version: the Unity/C# side of this project has never been compiled or run by any
automated system. It can only be compiled and played by opening `unity/` in a local Unity
editor (see [RUNNING_ON_MAC.md](RUNNING_ON_MAC.md) §1).**

Two independent reasons, as of 2026-07-04:

1. **No Unity editor in the agent / CI container.** The LLM agent works in a headless Linux
   container with Python + Blender (`bpy`) only. There is no Unity editor, so C# cannot be
   compiled, EditMode/PlayMode tests cannot run, and no build or screenshot can be produced
   locally. `./tools/check_all.sh` deliberately **SKIPs** every Unity step in this
   environment — it is green on Blender assets + design validation only.

2. **GameCI skips the Unity test job.** `.github/workflows/unity.yml` gates the real test job
   behind a `license-check` that requires the `UNITY_LICENSE`, `UNITY_EMAIL`, and
   `UNITY_PASSWORD` repository secrets. Those secrets are **not configured**, so on every push
   the `license-check` job passes and the `Unity EditMode/PlayMode tests` job is **skipped**.
   The workflow therefore reports "success" **without ever compiling the C#.**

   Verified on `main` commit `bd8575d` (Phase 2 landing): workflow run "Unity tests" #21 →
   `license-check` = success, `Unity … tests` = **skipped**. The "Blender assets" workflow,
   by contrast, genuinely runs and passes.

## What this means

- Every "GameCI green" on this repo currently means **"Blender assets passed + Unity tests
  skipped."** It is **not** evidence that the game compiles or runs.
- All Unity C# merged to date — Prototype 0.1 and the Phase 2 vertical slice — has been
  verified only by human / agent code review and hand-tracing, **not by a compiler.** There
  may be compile errors or test failures that no automated gate has caught.
- New C# files added by the agent also ship **without `.cs.meta` companions** (the agent has
  no editor to generate them); Unity creates them on first import.

## How to actually run / verify it

- **Play it:** open `unity/` in the pinned Unity editor and follow
  [RUNNING_ON_MAC.md](RUNNING_ON_MAC.md) §1. This is the only way to run the game today.
- **Make CI verify it:** add three secrets under GitHub → *Settings → Secrets and variables
  → Actions*:
  - `UNITY_LICENSE` — the contents of a Unity `.ulf` license file
  - `UNITY_EMAIL`, `UNITY_PASSWORD` — the Unity account credentials
  Once present, the `Unity EditMode/PlayMode tests` matrix job runs for real on each push, and
  a green Unity workflow will finally mean the C# compiles and the tests pass.

Until one of those happens, treat the Unity runtime as **authored but unverified.**
