# Abbey — Unity project (`unity/Assets/_Game`)

Prototype 0.1: core simulation, light territory, locked isometric camera, and the
first-night actor systems (Bellkeeper, villagers + dusk recall, Black Hound bond,
monsters + nightmare director).

## Layout & assemblies

```
Assets/_Game/
  Scripts/Runtime/    Abbey.Runtime.asmdef      — all gameplay code (namespace Abbey.*)
    Core/             GameClock, EventBus, GameEventLog, PrototypeConfig, PlanarMotion
    Light/            LightSource, DarknessEvaluator (Safe/Edge/Dark territory)
    CameraRig/        IsoCameraController (locked iso camera)
    Hero/             BellkeeperController (the directly controlled hero)
    Villagers/        VillagerAgent (12-state machine), DuskRecallSystem
    Beast/            HoundController (the Black Hound bond)
    Nightmares/       MonsterController (pale hound), NightmareDirector
  Scripts/Editor/     Abbey.Editor.asmdef       — editor-only, references Abbey.Runtime
                      PrototypeSceneBuilder: Tools → Abbey → Build Prototype Scene
  Tests/EditMode/     Abbey.Tests.EditMode.asmdef  — NUnit, editor platform only
  Tests/PlayMode/     Abbey.Tests.PlayMode.asmdef  — UnityTest coroutine tests
```

## Control map (Bellkeeper, prototype 0.1)

| Input | Action |
|-------|--------|
| WASD / arrows (`useDirectInput`) | Move the Bellkeeper, camera-relative under the locked 45° yaw. Overrides any queued move target. |
| `SetMoveTarget(worldPos)` API | Click-to-move style destination (also what tests drive). |
| `RingBell()` | Recall pulse: raises `BellRang(position, bellRadius)`. Recalls covered villagers with a speed boost, calms panic, calls a bonded hound. 5s cooldown. |
| `CarryFlame(bool)` | Raise/douse a mobile `LightSource` child. Drains stamina per second while lit; self-extinguishes at 0 stamina. Toggle cooldown stops flickering. |
| `Rescue(villager)` / `ReleaseRescued()` | Attach one villager in rescued-follow; release inside Safe light to complete the rescue (`VillagerRescued`). |
| `FeedHound(hound)` | Spend one carried food within interact range: hunger down, trust up (`HoundFed`). |

Camera: WASD/arrows pan (when the camera has no follow target), scroll wheel zooms
orthographic size only. The camera never rotates.

## System list & how the actors compose

Everything communicates through two spines — the **EventBus** (typed events:
`PhaseChanged(DayPhase)`, `BellRang(Vector3,float)`, `VillagerEndangered(GameObject)`,
`VillagerRescued(GameObject)`, `HoundFed(float)`, `MonsterSpawned(GameObject)`) and the
**GameEventLog** (append-only records; every `Raise*` also logs). Systems never call
each other directly except through small public APIs (hero → villager/hound).

- **GameClock** ticks Day → Dusk → Night → Dawn and raises `PhaseChanged`.
- **LightSource / DarknessEvaluator** turn positions into Safe/Edge/Dark territory.
  Everything else asks the evaluator; nobody duplicates light math.
- **BellkeeperController** is the player: its abilities emit `BellRang`/`HoundFed`
  and drive `VillagerAgent.BeginRescue`/`ReleaseRescue`.
- **VillagerAgent** is a per-villager state machine (Idle, AssignedToWork,
  WalkingToTask, Working, CarryingResource, ReturningToStorage, ReturningToLight,
  Panicking, Injured, Resting, Missing, Dead). By day it loops task ↔ storage; in
  the Dark at Dusk/Night fear rises (bravery, seeded and deterministic, slows it),
  panic wanders, and prolonged darkness means Injured then Missing.
- **DuskRecallSystem** (static registry all villagers join in OnEnable) listens for
  `PhaseChanged(Dusk)` and `BellRang`. At dusk, bell-covered villagers recall
  immediately with a speed bonus; uncovered ones recall late (the drama beat) and,
  beyond the config distance, raise `VillagerEndangered`. The registry doubles as
  the villager lookup for monsters and the night summary.
- **HoundController** holds the bond values (trust/hunger/pain/fear/attachment,
  0..1) and states Chained → Wary → Fed → Following. `Feed()` moves the values and
  raises `HoundFed`; past the fed threshold the hound answers `BellRang` and
  intercepts monsters in engage range. Starving or low-trust hounds ignore the bell
  — the snub is logged, never shown as a meter.
- **MonsterController** hunts the nearest Dark/Edge villager from the recall
  registry, refuses any step into Safe/too-bright light (recoils), flees while the
  hound presses it, and dies to hound attacks.
- **NightmareDirector** listens for `PhaseChanged`: at Night it spawns monsters at
  deterministic dark ring points (`FindDarkSpawnPoint`, seeded); at Dawn it despawns
  them and writes a `NightSummary` record (dead/injured/missing villagers, monsters
  killed, whether the hound helped — read back from the log itself).

Determinism contract: every actor has `autoTick` (Update/Time.deltaTime) and a
manual `Tick(float dt)`; all randomness is seeded (`simulationSeed`, villager
`seed`); all movement is `PlanarMotion` straight-line XZ steering (no NavMesh, no
physics). Tests set `autoTick = false` everywhere and step simulated time exactly.

Asmdefs reference each other **by name**, tests declare `UNITY_INCLUDE_TESTS` and
reference `nunit.framework.dll` via `overrideReferences`.

## Architecture rules in force (GAME.md §4 / AGENTS.md)

- **All tunables live in `Abbey.Core.PrototypeConfig`** (ScriptableObject). Nothing
  balance-related is hard-coded in a MonoBehaviour. `PrototypeConfig.LoadOrDefault()`
  returns the optional `Resources/PrototypeConfig` asset or coded defaults, so tests
  and CI never require an asset file.
- **One event log, many consumers**: every `EventBus.Raise*` also appends to the
  static `GameEventLog`. Later systems (moral pressures, hound evolution, morning
  reports) read the log, never each other.
- **Scenes are generated by code** (`PrototypeSceneBuilder`), never hand-authored;
  no `.unity` file is committed or required by any logic or test.
- **Deterministic simulation**: `GameClock` and `LightSource` support manual
  `Tick(float dt)` with `autoTick = false`, so tests step time exactly.
- **Camera contract (ART_BIBLE.md)**: `IsoCameraController` enforces orthographic
  projection and pitch 30 / yaw 45 every frame and exposes **no rotation API**.
  Zoom changes `orthographicSize` only.

## How tests run in CI

There is no Unity editor in the agent container. GameCI (`game-ci/unity-test-runner`,
editor 6000.0.32f1 — Unity 6 LTS — per `ProjectSettings/ProjectVersion.txt`) runs both
suites:

- **EditMode** — pure logic: darkness classification (overlap, extinguished, fuel
  depletion), clock phase sequencing + `PhaseChanged` events, config sanity, the
  villager state table, hound bond math/transitions, bellkeeper cooldown/stamina
  math, and director spawn-point selection. Gameplay components are
  `[ExecuteAlways]` so `Awake`/`OnEnable` registration works when tests
  `AddComponent` in edit mode.
- **PlayMode** — lifecycle + scenario behaviour: auto-tick from `Update`, camera
  lock re-asserted each `LateUpdate`, light registration on enable/destroy, and the
  actor scenarios (dusk recall with/without the bell, monster held out of Safe
  light, fed hound answers the bell and intercepts, starving hound ignores it).
  Tests build their world programmatically (`new GameObject()` + `AddComponent`),
  never load scenes, set `autoTick = false` on every component, drive `Tick(dt)`
  manually, and only ever yield inside bounded loop counters — no realtime waits.

Test isolation: every `[SetUp]`/`[TearDown]` calls `EventBus.ResetAll()`,
`GameEventLog.Clear()`, `DarknessEvaluator.Clear()`, `DuskRecallSystem.Clear()`,
`MonsterController.ClearRegistry()` and destroys spawned objects.

## Why the built-in render pipeline (for now)

The greybox prototype uses the **built-in RP**: zero pipeline configuration to keep
CI green and iteration fast while the game is unlit primitives. URP (needed for the
shader-driven desaturation of night/fear/winter/nightmare states) arrives at the
Milestone 1 polish pass; gameplay code never touches pipeline-specific APIs, so the
switch is contained to settings + materials.

## Notes

- `ProjectSettings/ProjectSettings.asset` is intentionally not committed: Unity 6
  generates valid defaults on first open (CI included), including the default
  active input handler (legacy Input Manager), which our controls rely on. Set productName
  "The Abbey at World's End" / companyName "Abbey" when the settings file is first
  generated and committed.
- `.meta` files are generated by the Unity editor/CI import step, not hand-authored.
