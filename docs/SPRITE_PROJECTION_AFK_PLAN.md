# AFK implementation plan: reversible 2.5D sprite projection

This is an execution contract for an orchestrating LLM and its subagents. It turns the
current generated 3D presentation into a coherent sprite presentation using Shade and
octoshrimpy's [Free 16x16 Mini World Sprites](https://merchant-shade.itch.io/16x16-mini-world-sprites),
without rewriting the game simulation.

The plan deliberately ends at a human visual gate. An unattended run may build, test,
commit, and push a complete candidate branch, but it must not merge that branch into
`main` until the camera, scale, palette, first building kit, and signature-character
proxies have been reviewed by the user.

## 1. Target outcome

The candidate must provide a switchable `SpriteProjection` presentation mode in both
generated maps.

- Gameplay roots remain on the existing XZ plane.
- Movement, jobs, footprints, obstacles, light territory, combat, and placement remain
  unchanged.
- The existing orthographic camera remains locked to pitch 30 degrees and yaw 45 degrees
  for the first candidate.
- Ground, shore, paths, streams, cliffs, and terrain tiers use an XZ-oriented sprite/tile
  presentation.
- Characters, buildings, trees, props, beasts, and monsters use camera-facing
  `SpriteRenderer` children with bottom-center anchors.
- Terrain height is visual only. Gameplay roots stay at Y=0 and authored roads remain
  traversable.
- Sorting is deterministic from projected XZ depth plus a catalog-owned role offset.
- Day, dusk, night, winter, and Safe/Edge/Dark states remain readable through sprite tint
  and overlays rather than mesh lighting.
- The old 3D presentation is disabled, never deleted, and can be restored by changing one
  data setting.
- The final sprite-mode proof contains no visible cubes, capsules, untextured planes,
  magenta materials, or accidental mixed 3D placeholders.

This is a presentation conversion, not a true 2D/Physics2D rewrite. A later top-down XY
rewrite would be a separate project because it would break the current camera contract,
input orientation, screenshot contracts, and camera tests.

## 2. Non-negotiable constraints

1. Read and obey `AGENTS.md`, `ART_BIBLE.md`, `docs/ORCHESTRATION.md`,
   `docs/THIRD_PARTY_ASSET_STRATEGY.md`, and `docs/VERIFICATION_STATUS.md` before work.
2. Do not modify `PlanarMotion`, movement speeds, gameplay-root positions, building
   footprints, or `WorldObstacle` behavior to make the art fit.
3. Do not add sprite colliders or switch gameplay to Physics2D.
4. Do not delete generated GLBs, Blender specs, shared materials, or the URP 3D fallback.
5. Do not hand-edit generated files or Unity sprite `.meta` data. The deterministic editor
   importer owns import settings, slicing, and the generated catalog asset.
6. Do not reference `third_party_cache/` at runtime.
7. Do not import the entire asset pack into Unity. Commit only the selected files needed by
   the two maps and currently spawnable roles.
8. Do not invent, redraw, upscale, or AI-modify missing sprites. A missing honest mapping is
   a reported blocker, not permission to create ad-hoc art.
9. Do not infer sheet layouts at runtime. Slice rectangles, pivots, animation frames, and
   directions belong in the committed manifest.
10. Do not approve or modify `GATE-P4`; it remains human-owned.
11. Never use a green GitHub workflow as Unity evidence. Local Unity MCP is the compile and
    test authority.
12. Every runtime-affecting merge must wait for compilation, inspect the console, run
    targeted tests, and capture a proof image before the next dependent task starts.

The existing Blender asset loop remains authoritative for generated 3D assets. Direct
third-party PNG selection follows the repository's third-party placeholder workflow; do
not pass unchanged PNGs through Blender merely to disguise their source. The full Blender
validation still runs at the end to prove that the existing generated asset set is intact.

## 3. Baseline and clean-worktree rule

At the time this plan was written:

- `origin/main` is `fe9d641ca35cce110167d040dcc8293ece8854d2`.
- The current worktree is on `task/P4-EPIC`.
- `unity/ProjectSettings/EditorBuildSettings.asset` has an existing uncommitted change.

That modification is user-owned. The AFK run must not clean, stash, restore, stage, or
commit it. Work from a new clean integration worktree instead:

```sh
git fetch origin
git worktree add -b task/P2D-EPIC \
  /Users/mak/.codex/worktrees/abbey-p2d origin/main
```

If that branch already exists after a resumed run, inspect it and its task state rather
than recreating it. The local `main` ref may be stale; use `origin/main` as the initial
baseline.

The orchestrator must record the exact baseline commit and the results of these commands
before spawning builders:

```sh
git -C /Users/mak/.codex/worktrees/abbey-p2d status --short --branch
git -C /Users/mak/.codex/worktrees/abbey-p2d rev-parse HEAD
cd /Users/mak/.codex/worktrees/abbey-p2d && ./tools/check_all.sh
```

Run the baseline Unity MCP gate from that same worktree. If main is already red, record
the pre-existing failure and stop; do not make this refactor responsible for guessing at
an unrelated repair.

## 4. Source and license contract

The official page currently declares CC0 1.0, commercial and noncommercial use,
modification, and no attribution requirement. It lists more than 100 characters, more
than 135 buildings, nature sprites, tiles, icons, UI, terrain levels, animals, and a
character customizer.

Use this cache layout:

```text
third_party_cache/MerchantShade/MiniWorldSprites/
  MiniWorldSprites.zip
  extracted/MiniWorldSprites/
```

Use this curated runtime layout:

```text
unity/Assets/_Game/Art/Placeholders/MerchantShadeMiniWorld/
  README.md
  LICENSE-CC0-1.0.txt
  manifest.json
  Terrain/
  Buildings/
  Characters/
  Nature/
  UI/
```

Rename committed PNGs with the `abbey_placeholder_miniworld_*` prefix. Do not commit the
ZIP, DOCX guide, pack previews, templates, or unused palette duplicates.

At the plan date the official archive is itch game `703908`, upload `7054436`, 2,084,074
bytes, with SHA-256:

```text
79eb000cfd3f64fee8ac8307f02bb867dc8b4fd7ce5a150119c51dedfa563f1f
```

Treat that hash as an acquisition pin, not an eternal fact. If the official upload ID,
size, or hash changes, stop the unattended acquisition and request a source re-audit. Do
not silently accept a different archive or substitute a similar pack. The current archive
does not contain an embedded license file, so the curated folder must record the source
page, authors, CC0 URL, download date, archive identity, and the guide's request not to
sell the pack as a standalone asset collection.

The manifest is the visual source of truth. It must contain:

- Source page, authors, CC0 URL, itch game/upload IDs, download date, archive size, and
  archive SHA-256.
- Original path, Abbey path, SHA-256, and pixel dimensions for every committed PNG.
- Sheet cell size and every named slice rectangle.
- Pivot, pixels per unit, orientation, category, and expected dimensions.
- Optional animation clips with explicit frame names, direction, and timing.
- Abbey gameplay asset ID or component role mapped to each selected sprite/variant.
- Visual scale, anchor offset, sorting category/offset, and day/night tint participation.
- Authored XZ obstacle footprints for walls, towers, and other blocking objects.
- A fallback policy and `temporaryIdentityProxy` flag for signature roles.

Never silently map an unrelated animal or humanoid to a signature role. If the pack has no
honest temporary proxy for the Bellkeeper, Black Hound, Stag, or a required nightmare,
mark the role unresolved and stop before the final visual gate. Early infrastructure
proofs may show the reversible 3D fallback, but final canonical sprite-mode screenshots
must not contain mixed 3D art.

Suggested initial source categories are terrain grass/dead grass/shore/cliff/water,
trees/pines/dead trees/rocks/wheat, houses/huts/chapels/keep/market/resources/tavern/tower/
workshop/barracks/docks, a restrained worker palette, required soldiers and undead,
bridges/well/tombstones/chests, and selector/highlight icons. The asset curator must inspect
the archive and guide before naming exact files; this paragraph is not permission to guess
paths or slice rectangles.

## 5. Unity import contract

`MiniWorldSpriteImporter` owns all selected texture imports and catalog generation. Each
texture must have:

- Texture type `Sprite`.
- Single or Multiple mode exactly as declared by the manifest.
- 16 pixels per unit unless the manifest explicitly records a reviewed exception.
- Point filtering.
- Mipmaps disabled.
- Compression disabled/uncompressed.
- NPOT scaling disabled.
- sRGB enabled for color art.
- Alpha transparency enabled.
- Wrap mode Clamp.
- Read/write disabled after import.
- Full Rect mesh.
- No generated physics shape.
- Bottom-center pivots for actors, buildings, nature, and props.
- Center pivots for terrain and UI.

Every slice rectangle must be unique, grid-aligned, in bounds, and addressable by a stable
manifest name. The importer generates and commits:

```text
unity/Assets/_Game/Settings/Rendering/MiniWorldSpriteProjectionCatalog.asset
```

Humans and subagents edit `manifest.json`, never the serialized catalog output. Add
`com.unity.2d.pixel-perfect` only after the local Unity editor resolves a version compatible
with Unity 6000.5.2f1. Do not guess a package version while AFK. If the package is not
compatible with the angled orthographic camera, keep package state unchanged and implement
project-owned integer zoom/pixel snapping in the rendering layer.

## 6. Runtime architecture contract

Create the sprite system under the existing rendering namespace/folder rather than
spreading presentation logic through gameplay systems:

```text
unity/Assets/_Game/Scripts/Runtime/Rendering/
  SpriteProjectionCatalog.cs
  SpriteProjectionConfig.cs
  SpriteProjectionBootstrap.cs
  SpriteProjectionFactory.cs
  SpriteBillboard.cs
  SpriteDepthSorter.cs
  SpriteActorPresenter.cs
  SpritePhaseTint.cs
  SpriteRoleTag.cs
```

Names may change during implementation only if the final design preserves these
responsibilities:

- `SpriteProjectionConfig` owns projection mode, PPU, world scale, supported zoom steps,
  sorting scale, terrain-tier offsets, tint curves, and fallback behavior. Do not hide
  visual constants in MonoBehaviours.
- `SpriteProjectionCatalog` resolves asset IDs and dynamic component roles to manifest
  entries and sprites.
- `SpriteProjectionFactory` adds a presentation child without moving or reparenting the
  gameplay root.
- `SpriteBillboard` faces the locked camera and preserves a bottom-center ground anchor.
- `SpriteDepthSorter` computes stable order from camera/projected XZ depth plus catalog
  role offset. Equal positions must have a deterministic tie-breaker that does not use an
  unstable instance ID.
- `SpriteActorPresenter` reads root movement/state to choose direction and animation. It
  must never write the root transform, target, speed, or gameplay state.
- `SpritePhaseTint` maps clock phase and `DarknessEvaluator` zone to data-owned colors.
- `SpriteProjectionBootstrap` covers initial and runtime-spawned objects. Prefer a central
  registry/discovery pass over edits to every gameplay spawner. The pass must be bounded,
  deterministic, and allocation-free after registration.
- `SpriteRoleTag` lets the scene builders name static art roles without making gameplay
  components depend on the sprite catalog.

Keep `AbbeyMaterialFactory` and existing mesh renderers for 3D rollback. Sprite mode
disables legacy `MeshRenderer`/`SkinnedMeshRenderer` components; it does not destroy them.
Material normalization in `PrototypeSceneBuilder` must skip `SpriteRenderer` so URP Lit
mesh materials are never assigned to sprites.

Use an XZ-oriented tile/grid presentation for the terrain proof. Validate the relevant
Unity API in the live editor before committing to a Tilemap orientation. If an angled
Tilemap cannot satisfy anchors and pixel stability, use deterministic sprite patches under
one terrain root; do not convert the simulation to XY merely to use a convenient editor
template.

Important collision rule: `PrototypeSceneBuilder.AddWorldObstacleFromVisibleBounds`
currently derives obstacles from renderer bounds. Camera-facing sprite bounds are not valid
XZ collision footprints. In sprite mode, walls, towers, and blocking structures must use
the authored catalog footprint. Renderer-derived bounds remain a 3D fallback only.

## 7. Task graph

```text
P2D-00 clean baseline, tracker, and implementation spec
  |
  +-- P2D-01 source audit, acquisition, curated CC0 subset, manifest
  |      |
  |      +-- P2D-03 deterministic Unity importer and catalog validation
  |
  +-- P2D-02 runtime projection framework and isolated proof
         |
         +-------------+
                       |
          P2D-02 + P2D-03 green
                       |
          +------------+-------------+
          |                          |
       P2D-04                      P2D-05
       Map 1 integration           runtime-spawned actor/building/item coverage
          |                          |
          +------------+-------------+
                       |
                    P2D-06
                    Map 2 integration
                       |
                    P2D-07
                    pixel/depth/night polish and final validation
                       |
                    GATE-P2D-ART
                    human screenshot and play review
```

Only P2D-01/P2D-02 and P2D-04/P2D-05 may run in parallel. Map 1 and Map 2 builder edits
must never run concurrently. Unity MCP operations are always serialized by the
orchestrator.

## 8. Task definitions and ownership

### P2D-00 — clean baseline, tracker, and spec

Owner: orchestrator only.

Deliverables:

- A clean `task/P2D-EPIC` worktree created from `origin/main`.
- Baseline `check_all.sh` and local MCP gate evidence.
- `P2D-EPIC`, P2D-01 through P2D-07, and `GATE-P2D-ART` records added to
  `REQUIREMENTS.yml` without altering `GATE-P4`.
- This plan linked from each task description.
- One tracker/spec commit pushed to `origin/task/P2D-EPIC`.

Acceptance:

- The original dirty worktree is byte-for-byte untouched.
- Baseline Unity instance path matches the new integration worktree.
- Existing EditMode and PlayMode totals are recorded, not inferred from CI.

### P2D-01 — source audit and curated sprite subset

Owner: asset curator.

Owned paths:

- `third_party_cache/MerchantShade/**` locally.
- `third_party_cache/README.md`.
- `unity/Assets/_Game/Art/Placeholders/MerchantShadeMiniWorld/**`.
- A new acquisition/inventory tool and its non-Unity tests.

Recommended tools:

```text
tools/acquire_merchant_shade_miniworld.py
tools/validate_merchant_shade_miniworld.py
```

All Python commands run through `uv`. Acquisition must reject HTML masquerading as ZIP,
ZIP path traversal, unexpected upload identity, missing official CC0 declaration, and hash
drift. It must never scrape past an itch.io login/payment/control boundary.

Acceptance:

- `git ls-files third_party_cache` still lists only `third_party_cache/README.md`.
- Every committed binary is listed in the manifest with matching hash and dimensions.
- Every manifest file exists and there are no unlisted PNGs in the curated runtime folder.
- A generated contact sheet/inventory report lets the reviewer inspect every selected
  sprite without opening the archive manually.
- Required map and runtime roles are either mapped or explicitly unresolved.

### P2D-02 — runtime projection framework and proof

Owner: runtime builder.

Owned paths:

- New `Runtime/Rendering/Sprite*` files.
- New projection-only EditMode/PlayMode test files.
- No scene-builder, pack, importer, gameplay, or tracker edits.

Build the framework against a tiny imported proof selection or test-created sprites. Do
not add permanent ad-hoc art. The proof must include terrain, path, tree, building,
villager, and animal roles under the existing camera.

Acceptance:

- Enabling/disabling projection does not change any gameplay-root transform.
- Two roots crossing in both X and Z sort deterministically without flicker.
- Sprites remain camera-facing while follow, pan, and supported zoom steps run.
- Projection update has no steady-state managed allocation.
- The 3D fallback returns when the data flag is disabled.

### P2D-03 — deterministic Unity importer and catalog

Owner: import builder.

Owned paths:

- `unity/Assets/_Game/Scripts/Editor/MiniWorldSpriteImporter.cs`.
- `unity/Assets/_Game/Scripts/Editor/MiniWorldProjectionValidator.cs`.
- Importer/catalog EditMode tests.
- The importer-generated catalog asset and selected PNG `.meta` files.
- No scene-builder or runtime projection edits.

Acceptance:

- Reimporting twice produces an identical catalog, sprite names, pivots, and `.meta`
  content.
- Tests assert the complete import contract in section 5.
- Every required catalog key resolves exactly once; duplicates and null sprites fail.
- Wall/tower footprints are positive and do not come from billboard bounds.
- Compilation finishes with zero console errors.

### P2D-04 — Map 1 integration

Owner: Map 1 scene builder.

Primary owned paths:

- `unity/Assets/_Game/Scripts/Editor/PrototypeSceneBuilder.cs`.
- New Map 1 projection scene tests.
- Map 1 screenshot capture only if required by the shared interface.

Route ground, patches, roads, stream, terrain relief, camp, beach, abbey, walls, trees,
buildings, villagers, hero, hound, and static props through the common sprite factory.
Preserve every existing GameObject name and gameplay component. Use terrain-level/cliff
sprites around the perimeter and abbey plateau without moving gameplay roots off Y=0.

Acceptance:

- The generated scene contains the projection bootstrap, catalog, and projected terrain.
- All 12 villagers and every visible static role have a mapped sprite.
- No visible primitive or 3D placeholder remains in the canonical sprite-mode framing.
- Walls and towers use authored XZ obstacle footprints.
- Hero and villagers cannot enter those footprints.
- The 3D mode still builds and passes its scene invariants.

### P2D-05 — runtime-spawned coverage

Owner: dynamic presentation builder.

Primary owned paths:

- New or existing `Runtime/Rendering/Sprite*` adapters.
- Focused tests for runtime buildings, construction/restoration states, warriors,
  newcomers, nightmares, carried resources, candle carriers, and objectives.
- Gameplay spawner files only when central discovery cannot cover a role, and only after
  the orchestrator assigns the exact file to avoid overlap.

Audit these known runtime creation surfaces:

- `Buildings/Building.cs` and `BuildingPlacer.cs`.
- `Villagers/VillagerJobAgent.cs`.
- `Nightmares/NightmareDirector.cs` and `NightEscalationSystem.cs`.
- `Combat/WarriorStructure.cs`.
- `Island/ArrivalSystem.cs`.
- `Decrees/OverdriveSystem.cs`.
- `Hero/BellkeeperController.cs` carried flame.

Acceptance:

- Constructing every catalog building produces a sprite rather than a cube.
- Every currently spawnable actor receives a sprite within one presentation registration
  cycle.
- Carried resources and construction/restoration states remain readable.
- Presentation never mutates gameplay state or tick order.
- Projection-on and projection-off movement distances are identical.

### P2D-06 — Map 2 integration

Owner: Map 2 scene builder, after Map 1 and dynamic coverage merge.

Primary owned paths:

- `unity/Assets/_Game/Scripts/Editor/Map2SceneBuilder.cs`.
- `unity/Assets/_Game/Scripts/Editor/Map2ScreenshotCapture.cs`.
- New Map 2 projection scene tests.

Make Map 2 use the same factory for its forest floor, stream, landmarks, structures,
graves, stag, trees, and proof markers. Preserve every landmark root name used by
`AbbeyUnityGate.ValidateMap2Scene` and the current delete-by-name behavior.

Acceptance:

- Map 2 contains no mixed primitive/GLB placeholder art in sprite mode.
- Sacred grove, false bell, deer paths, and forest clearings remain readable.
- Map 1 regeneration performed by Map 2 remains deterministic.
- Both maps can still be switched back to the existing 3D presentation.

### P2D-07 — pixel, sorting, phase polish, gate, and evidence

Owner: orchestrator plus one integration builder; reviewer remains read-only.

Primary paths:

- `SpritePhaseTint` and projection config/catalog data.
- `ScreenshotCapture.cs` and `Map2ScreenshotCapture.cs`.
- `AbbeyUnityGate.cs` and its tests.
- `docs/validation/P2D-sprite-projection.md`.
- `docs/VERIFICATION_STATUS.md` only after an authoritative successful local gate.

Extend the Unity gate with a `spriteImportValidation`/`spriteProjectionValidation` step
that fails on import drift, missing required mappings, invalid footprints, mixed visible
3D fallbacks, or missing projected terrain.

Acceptance:

- Point-filtered pixels are stable at every supported zoom step.
- No atlas bleeding, anchor hopping, or tree/building order popping is visible.
- Day, dusk, night, winter, Safe, Edge, and Dark are visibly distinct.
- HUD text remains non-overlapping at all screenshot resolutions.
- Both generated scenes build.
- All canonical screenshots are captured and inspected.
- Full EditMode and PlayMode suites pass with zero console errors.
- `./tools/check_all.sh` passes.
- Candidate branch is pushed with a clean worktree and a complete validation report.

## 9. Subagent execution protocol

The orchestrator owns `REQUIREMENTS.yml`, the integration branch, worktrees, reviews,
merges, all Unity MCP operations, and final validation evidence. Builders never edit the
tracker, merge, push `main`, or run Unity/MCP. Reviewers never edit files.

Create each builder worktree from the exact current P2D integration commit:

```text
task/P2D-01-assets
task/P2D-02-runtime
task/P2D-03-import
task/P2D-04-map1
task/P2D-05-dynamic
task/P2D-06-map2
task/P2D-07-polish
```

Use a separate absolute worktree path per branch. Because Codex subagents share the host
filesystem, every prompt must name the allowed worktree and paths explicitly. A builder
must use `git -C <assigned-worktree>` and must not edit another worktree.

### Builder prompt template

```text
You are the builder for <task-id>. Work only in <absolute-worktree> on branch <branch>.
Read AGENTS.md, docs/SPRITE_PROJECTION_AFK_PLAN.md, and the <task-id> record in
REQUIREMENTS.yml before editing. Your owned paths are: <paths>. Do not edit
REQUIREMENTS.yml, merge, push main, run Unity/MCP, alter gameplay balance, or touch any
other worktree. Use apply_patch for edits and uv for Python. Implement only the task
contract, run all available non-Unity and focused tests, inspect git diff/status, and
commit one working milestone. Final response: branch, commit(s), files changed, exact
tests/results, unresolved mappings/risks, and what the reviewer should scrutinize.
```

### Reviewer prompt template

```text
Review task <task-id> read-only in <absolute-worktree>. Read AGENTS.md and
docs/SPRITE_PROJECTION_AFK_PLAN.md. Review git diff <integration-commit>..<task-branch>,
including binaries through manifest/hash checks. Check scope ownership, determinism,
license provenance, generated-file policy, gameplay/presentation separation, tests, and
fallback behavior. Do not edit. Return exactly APPROVE or CHANGES_REQUESTED followed by
concrete file/line findings and missing validation.
```

### Orchestrator merge loop

1. Update the task from `todo` to `in_progress` and commit/push the tracker transition.
2. Create the builder worktree from the current integration commit.
3. Spawn the builder with exact path ownership.
4. Spawn a read-only reviewer when the builder reports a commit.
5. On changes requested, return the concrete findings to the same builder.
6. On approval, update the task to `approved`, merge with `--no-ff`, then update it to
   `merged` with the merge commit.
7. Run the appropriate integration checks. Revert a failed merge; never reset shared
   history.
8. Push `origin/task/P2D-EPIC` after every green merge batch so AFK state survives session
   loss.
9. Remove the merged worktree but retain the task branch until the visual gate.
10. Start only tasks whose dependencies are merged and green.

Run independent builders in parallel, but serialize every merge and every Unity operation.
Do not let agents edit `PrototypeSceneBuilder.cs` and `Map2SceneBuilder.cs` concurrently.

## 10. Unity MCP protocol

Unity is a single serialized resource owned by the orchestrator. Before every Unity call:

1. Confirm the connected instance points to
   `/Users/mak/.codex/worktrees/abbey-p2d/unity`.
2. Read `mcpforunity://editor/state`.
3. Proceed only when `ready_for_tools` is true, `is_compiling` is false, no domain reload
   is pending, and there are no blocking reasons.
4. After script/import changes, wait for compilation and read console errors before
   attaching components, building scenes, or running tests.
5. Run focused affected tests before a full suite.
6. Capture an inline 256-512 px Game-view proof after each visual integration merge.
7. Read warnings/errors after every scene build and screenshot pass.

After the import merge, validate every selected texture, named slice, pivot, PPU, filter
mode, compression mode, and catalog reference. After the runtime merge, run projection
EditMode and PlayMode tests. After each scene merge, build both generated scenes and
capture preliminary screenshots.

Final authoritative command sequence:

```sh
cd /Users/mak/.codex/worktrees/abbey-p2d
UNITY_MCP_UV_OFFLINE=1 tools/run_unity_mcp_gate.sh --no-restart
./tools/check_all.sh
```

If MCP is not connected, use `tools/run_unity_mcp_gate.sh` without `--no-restart`. Do not
run Unity from multiple builder worktrees. Do not present skipped Unity batch steps from
`check_all.sh` as passed Unity tests; report the local MCP gate totals separately.

## 11. Required tests

Add or extend tests that prove:

- Every required Map 1 and Map 2 visual role resolves exactly once.
- Import settings match section 5.
- Catalog entries have non-null sprites, valid anchors, valid scales, and deterministic
  sort data.
- Required wall/tower footprints are positive and catalog-authored.
- Projection does not change gameplay-root position, rotation, scale, component set,
  movement result, building footprint, or `WorldObstacle` rectangle.
- Projection-on and projection-off Bellkeeper/villager travel distances are equal for the
  same fixed ticks.
- A sprite wall blocks direct and target-driven movement exactly as the 3D wall does.
- Equal-depth sorting uses a stable tie-breaker and remains stable during crossing.
- Actor direction/animation reads motion without writing movement.
- Runtime buildings, construction/restoration states, newcomers, warriors, nightmares,
  objectives, candle carriers, and carried resources receive mapped sprites.
- Both scenes contain one projection bootstrap, projected terrain, and no missing-role
  diagnostics.
- All 12 initial villagers use sprite visuals.
- Sprite mode can be disabled to restore the 3D presentation.
- Camera remains orthographic and locked to the existing pitch/yaw.
- Phase tints distinguish day, dusk, night, winter, and light zones.
- Unity gate fails deliberately when a required mapping, sprite, footprint, or import
  setting is corrupted in a test fixture.

Do not weaken existing camera, wall, gameplay, scene, or screenshot tests to make the new
mode pass.

## 12. Visual acceptance matrix

The final evidence set must cover both maps and include at least the existing six canonical
shots. Review each at the default zoom and at one supported near zoom.

| Area | Pass condition |
|---|---|
| Pixel integrity | Point filtered, no smoothing, mip blur, atlas bleed, or shimmer |
| Characters | Readable silhouettes and roles; feet stay anchored; no giant/tiny variants |
| Buildings | Function readable; footprint and sprite agree; no overlap that hides entrances |
| Trees | Authored clusters leave roads/clearings open; deterministic depth order |
| Terrain | Meadow/shore/forest/path/stream distinct; visual tiers make the map feel non-flat |
| Walls | Visually solid and mechanically impassable; no sprite-bounds collision inference |
| Motion | No perceived or measured regression; sprite follows root without lag |
| Night | Safe/Edge/Dark readable; important actors visible without washing out danger |
| Seasons | Lush day and degraded winter remain distinct |
| HUD | No overlapping shortcut/help text at proof resolutions |
| Consistency | No cubes, capsules, untextured planes, magenta materials, or mixed GLBs |

Image checks can reject obvious failures, but they cannot approve charm, palette, camera,
or Settlers-like readability. Those remain the human gate.

## 13. Stop conditions

Stop the affected task, preserve its branch, push the current integration state, and write
a blocker report if any of these occurs:

- The official ZIP or explicit CC0 declaration cannot be obtained and verified.
- Downloading would require bypassing itch.io controls, authentication, payment, or access
  restrictions.
- The official archive identity/hash changed from the pinned audit.
- A required signature role has no honest proxy and final sprite mode would need mixed 3D
  art or newly invented art.
- A proposed fix requires changing gameplay simulation, camera angle, map coordinates,
  movement balance, collision semantics, or approving `GATE-P4`.
- A builder touches paths outside its assignment or unexpected user files appear.
- Existing regression tests fail from the candidate.
- The same compile/MCP failure survives three focused repair attempts.
- MCP cannot prove it is connected to the integration worktree.
- Screenshots show blank sprites, magenta materials, visible cubes/capsules, unreadable
  night, severe sorting artifacts, or wall penetration.
- `./tools/check_all.sh` fails.

Do not silently reduce test coverage, delete a failing fixture, accept a different asset
pack, or claim Unity validation from CI.

## 14. Rollback and recovery

- Projection mode is data-controlled; disabling it rebuilds both generated scenes with
  the existing 3D presentation.
- Never delete the 3D presentation path during this project.
- Revert a bad integration merge with `git revert -m 1 <merge-commit>`; never reset shared
  history.
- Rebuild generated scenes after a revert rather than hand-editing `.unity` files.
- Keep failed builder branches for diagnosis until the human gate is resolved.
- Snapshot/restore mutable Unity project settings. The existing MCP gate already protects
  `EditorSettings.asset` and `EditorBuildSettings.asset`.
- Commit only expected importer-generated `.meta` files. Unexpected project-setting,
  generated-scene, cache, or library files fail the clean-tree gate.
- On orchestrator restart, read `REQUIREMENTS.yml` and remote `task/P2D-EPIC` before
  spawning any new work. Salvage existing task branches rather than duplicating work.

## 15. Final unattended handoff

The AFK run ends by pushing the green candidate integration branch, not `main`. Report:

- Candidate branch and head commit.
- Every builder/reviewer branch and merged commit.
- Merchant Shade source URL, authors, CC0 URL, upload identity, archive hash, and selected
  file manifest.
- Unity gate report path and exact EditMode/PlayMode pass totals.
- Zero-error console evidence.
- `check_all.sh` result with any skips stated accurately.
- Absolute links to all canonical screenshots.
- How to switch back to 3D.
- Unresolved mappings and known limitations, especially visual-only terrain height.
- Confirmation that the original dirty worktree was untouched.
- A request for the `GATE-P2D-ART` screenshot/play verdict.

Only after explicit human approval may the orchestrator update the gate, merge or
fast-forward `task/P2D-EPIC` to `main`, run the authoritative gates once more on the exact
main commit, and push `origin/main`.

