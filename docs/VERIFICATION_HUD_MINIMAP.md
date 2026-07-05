# Verify the player-facing HUD and minimap (commit `6f281fd`)

**Audience: an LLM agent driving the local Unity editor through MCP for Unity, starting
cold with no conversation context.** This document is self-contained. Follow it top to
bottom; the steps are ordered cheapest-first so a compile failure is caught before you
spend time in Play mode.

## 1. Purpose & scope

Commit `6f281fd` ("Add player-facing HUD and minimap panels (F7/F8)") on branch
`claude/game-hud-minimap-smk0hz` adds two display-only IMGUI panels that ship with the
game, in the same style as the existing F1–F6 debug panels, plus their EditMode tests and
the scene wiring. Files added/changed:

- `unity/Assets/_Game/Scripts/Runtime/UI/GameHud.cs` — new, `Abbey.UI.GameHud` (Abbey.Runtime).
- `unity/Assets/_Game/Scripts/Runtime/UI/MinimapPanel.cs` — new, `Abbey.UI.MinimapPanel` (Abbey.Runtime).
- `unity/Assets/_Game/Scripts/Runtime/UI.meta` — new folder meta.
- `unity/Assets/_Game/Tests/EditMode/GameHudTests.cs` — new, 5 tests, `Abbey.Tests.EditMode.GameHudTests`.
- `unity/Assets/_Game/Tests/EditMode/MinimapPanelTests.cs` — new, 7 tests, `Abbey.Tests.EditMode.MinimapPanelTests`.
- `unity/Assets/_Game/Scripts/Editor/PrototypeSceneBuilder.cs` — modified: `BuildSessionReportsAndPanels`
  now creates a `PlayerHud` GameObject with `GameHud` + `MinimapPanel` components.

**Why this is unverified:** the code was authored in an agent container that has **no Unity
editor**, so the C# has **never been compiled or run**, and `./tools/check_all.sh` SKIPs
every Unity step there (see [AGENTS.md](../AGENTS.md) and
[VERIFICATION_STATUS.md](VERIFICATION_STATUS.md)). CI does not compile Unity either (the
GameCI test job is gated behind unset license secrets and SKIPs). A compile error is the
single most likely failure mode — verify that first.

## 2. Preconditions

- Branch `claude/game-hud-minimap-smk0hz` is checked out on `unity/`.
- The pinned Unity editor **`6000.5.2f1`** (Unity 6.5, `6000.5.x`) is open on the `unity/`
  project with the MCP for Unity bridge connected. If it is not, follow
  [UNITY_MCP.md](UNITY_MCP.md): run `./tools/restart_unity_mcp.sh`, then verify with the
  `mcpforunity://editor/state` and `mcpforunity://instances` resources before proceeding.
- You can drive the editor through MCP tools (menu execution, test runner, console read,
  Play-mode control, screenshot/Game-view capture).

## 3. Verification steps (cheapest-first)

### a. Compile check (do this first)

Trigger a domain reload / asset refresh (e.g. `Assets > Refresh`, or the MCP menu
equivalent), wait for the editor to finish compiling, then **read the Unity console** and
confirm **zero compile errors** across the `Abbey.Runtime`, `Abbey.Editor`,
`Abbey.Tests.EditMode`, and `Abbey.Tests.PlayMode` assemblies.

- The fastest path that also covers this is `tools/run_unity_mcp_gate.sh --no-restart`,
  whose `compileAndConsoleCheck` step forces a synchronous import and fails on any console
  error. But a bare console read after refresh is cheaper if you only want the compile
  signal.
- Watch specifically for: missing `using` directives, a symbol that does not exist on the
  types the panels read live (`GameClock`, `ResourceLedger`, `DuskRecallSystem`,
  `MonsterController`, `BellkeeperController`, `HoundController`, `DarknessEvaluator`,
  `LightSource`, `VillagerState`, `DayPhase`, `LightZone`), or a namespace typo.
- If it does not compile, go to §4 (failure handling) before running any tests.

### b. EditMode tests

Run the EditMode suite through MCP. Expect:

- **`Abbey.Tests.EditMode.GameHudTests` — 5 tests, all pass**
  (`FormatClock_FormatsDayPhaseAndPercent`, `FormatClock_ClampsProgressToValidPercent`,
  `FormatStockLine_ShowsEveryTrackedResourceAndCapacity`,
  `CountsAsLiving_ExcludesDeadAndMissing`, `CountsAsLiving_IncludesEveryOtherState`).
- **`Abbey.Tests.EditMode.MinimapPanelTests` — 7 tests, all pass**
  (`WorldToMap_*` ×4, `MapPixelToWorld_RoundTripsThroughWorldToMap`, `GroundColorFor_*` ×2).
- **No regressions** in the existing EditMode suite. The last recorded local baseline was
  **180/180 EditMode passing** (VERIFICATION_STATUS.md); with the 12 new tests the total
  should be **192/192**. Treat any pre-existing test now failing as a regression to record.

If you can filter, run just the two new classes first for a fast signal, then the full
suite for the regression check.

### c. PlayMode tests

Run the PlayMode suite. **No PlayMode tests were added** by this commit, so this is purely
a regression check: expect the previously recorded **39/39 PlayMode passing** to still
hold. Any new failure is a regression.

### d. Rebuild the scene and confirm the wiring

Execute menu **`Tools/Abbey/Build Prototype Scene`** (`PrototypeSceneBuilder.BuildPrototypeScene`).
Then confirm in the generated scene that a GameObject named **`PlayerHud`** exists and
carries **both** a `GameHud` component **and** a `MinimapPanel` component. (The gate's
scene step also rebuilds the scene, so `run_unity_mcp_gate.sh` covers the build itself, but
it does not assert the `PlayerHud` object — check that explicitly.)

### e. Enter Play mode and capture a screenshot of the HUD

Enter Play mode and capture the **Game view** (not a camera-rig render). Note: the gate's
canonical screenshots (`day_camp.png` … `morning_after.png`) render `CameraRig.TargetCamera`
to a RenderTexture and will **not** contain the IMGUI overlay — you must capture the Game
view while in Play mode to see the HUD/minimap. Use the MCP screenshot / Game-view capture
tool.

With the freshly built prototype scene at the start of Day 1 (default `visible = true` on
both components), the overlay must show, grounded in the code:

**Top-center strip** (660×52 box, horizontally centered, 8px from top):
- Day/phase clock reading like **`Day 1 — Day N%`** (`GameHud.FormatClock`: `Day {n} — {Phase} {pct}%`,
  percent clamped 0–100). On a just-built scene the phase is Day with a low percent.
- A thin **phase-progress bar** (~180×5px) under the clock text, tinted by phase
  (Day = warm yellow).
- Stockpile line: **`Wood {w}   Food {f}   Oil {o}   Medicine {m}   Stored {n}/{cap}`**
  (`FormatStockLine`, live from `ResourceLedger`).
- Villager headcount on the right: **`Villagers 12/12`** — the scene spawns 12 villagers
  (11 around camp + 1 far NW), and none are Dead/Missing at start. A `   Monsters n`
  suffix appears only when monsters are alive (none at Day start).

**Bottom-center strip** (480×30 box, centered, 8px from bottom) — only drawn once the HUD
has found the `BellkeeperController` (it rescans every ~2s, so give it a moment):
- **`HP`** label + red bar (fill = `Health / bellkeeperMaxHealth`).
- **`ST`** label + blue bar (fill = `Stamina / bellkeeperMaxStamina`).
- **`Food x{carried}   Flame {LIT|out}`** — at start the hero carries no lit flame, so
  expect `Flame out`.

**Top-right minimap** (176px square, 8px from top-right, `MinimapPanel`):
- Phase-tinted ground (Day = muted green), rebuilt every 0.5s.
- Warm **safe/edge light circles** (concentric warm tints, `SafeLight` lerped 0.60/0.28
  over ground) around the lit light sources: the campfire at camp center, the lantern post
  just off-center, and the sacred abbey flame in the NE.
- Markers: **white** = hero (near center, world ~(2,-2)); **violet** = hound near the abbey
  in the **NE / top-right**; **green** = villagers clustered around camp center; **gold** =
  the sacred flame in the **NE**; **orange** = other lit light sources (campfire, lantern).

**Expected-and-correct, not a bug:** the minimap is **north-up and axis-aligned** (world +Z
is up / map-north), while the game camera is isometric. The minimap deliberately does not
match the camera's rotation. Because GUI space grows downward, world +Z lands at the **top**
of the map, so the NE abbey/hound/flame sit in the **upper-right** — consistent with the
`WorldToMap` tests.

### f. Toggle checks

- Press **F7**: the HUD hides, leaving only a small hint label **`[F7] HUD`** near
  top-center.
- Press **F8**: the minimap hides, leaving only a hint label **`[F8] minimap`** near
  top-right.
- Press each again to confirm it toggles back.

**Fallback if MCP cannot inject key presses:** while in Play mode, set the `visible` field
to `false` on the `GameHud` (and `MinimapPanel`) component via the inspector or a tiny
editor script (`FindFirstObjectByType<GameHud>().visible = false;`), and confirm the
`[F7] HUD` / `[F8] minimap` hint labels render in place of the full panels. Setting
`visible = true` again restores them.

### g. Optional night check

To exercise the phase-driven visuals, advance the clock to **Night**. Two ways:
- Temporarily shrink the phase durations on the `PrototypeConfig` asset so Night arrives in
  seconds (revert after), **or**
- Let Play mode run through Dusk into Night (~120s at default durations).

At Night confirm:
- The minimap **ground darkens** (`GroundColorFor(Night)` is near-black `0.07,0.08,0.13`,
  which is why `GroundColorFor_NightIsDarkerThanDay` passes).
- When monsters spawn, **red markers** appear on the minimap and the HUD headcount gains a
  **`Monsters n`** suffix.
- Remember the minimap background rebuilds only every 0.5s, so the darkening can lag up to
  half a second behind the phase change.

## 4. Pass/fail criteria

**PASS** requires all of:

- [ ] No compile errors in `Abbey.Runtime`, `Abbey.Editor`, `Abbey.Tests.EditMode`,
      `Abbey.Tests.PlayMode`.
- [ ] `GameHudTests` (5) and `MinimapPanelTests` (7) all pass.
- [ ] No EditMode regressions (expect 192/192 total) and no PlayMode regressions
      (expect 39/39).
- [ ] `Tools/Abbey/Build Prototype Scene` produces a `PlayerHud` GameObject with both
      `GameHud` and `MinimapPanel`.
- [ ] In Play mode the top-center strip, bottom-center hero strip, and top-right minimap
      render with the values described in §3e.
- [ ] F7 / F8 (or the `visible` fallback) toggle the panels and show the hint labels.

**On failure:**

- If it is a **trivial compile error** (missing `using`, a typo, a renamed symbol), fix it
  **directly on this branch** (`claude/game-hud-minimap-smk0hz`) and re-run from §3a.
- Otherwise, **record the failure** — do not paper over it.
- **Either way**, update [VERIFICATION_STATUS.md](VERIFICATION_STATUS.md) per its
  conventions with the dated result (verified / fixed-then-verified / failed-with-reason),
  and commit with a clear message describing what was verified or what failed.

## 5. Known limitations already accepted

These are intentional and are **not** verification failures:

- **IMGUI (`OnGUI`) prototype UI**, consistent with the existing F1–F6 debug panels — this
  is not the final UI technology.
- **Fixed pixel layout** (660/480px strips, 176px map) — not resolution-adaptive.
- The minimap **background rebuilds every 0.5s** (`redrawIntervalSeconds`), so light-state /
  phase changes on the map can lag up to half a second. Markers themselves are per-frame.
- **No camera-frustum indicator** on the minimap; it shows position markers only, north-up
  and axis-aligned rather than matching the isometric game camera.
