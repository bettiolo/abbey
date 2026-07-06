# P4-01 Map 1 Systems-Test Promotion

## What changed

Map 1 / `Prototype01` is now explicitly the all-mechanics test map. It carries the
forest resource set (`old_wood`, `green_wood`, `apples`, `venison`, `herbs`, `resin`,
`sacred_seeds`, `charcoal`), the requested forest building catalog entries, Forest Debt,
and the forest misdirection nightmare set.

The main config surfaces are `EconomyConfig` (forest production recipes),
`BuildingCatalog` (forest buildables), and `ThreatConfig` (Forest Debt, misdirection
triggers, false-bell values). The debug surface is the F4 Nightmare panel, with supporting
resource visibility on F2 Economy.

## Technical verification

- `ThreatSourceSystem` treats Forest Debt as forest-source exploitation pressure. It rises
  from old-growth cutting, green wood, venison, resin, charcoal, grove intrusion, night
  burning, and forced forest labour; it falls from replanting, grove shrines, deer
  protection, tree burials, and restraint.
- `FalseGuidanceSystem` owns the prototype True Bell vs False Bell layer. Forest Debt or
  forest nightmares can activate fog that reduces non-sacred lantern reach. A Bell Mimic
  emits a false bell and projects a false light; affected villagers follow it until a true
  bell-boosted recall clears the lure.
- `NightmareType` includes Root Walker, Bell Mimic, Antler Wraith, Hollow Deer, and
  Charcoal Dead. They reuse the consequence-nightmare controller while their triggers and
  stats remain in `ThreatConfig`.
- `PrototypeSceneBuilder` places the forest buildings in Map 1 and manually staffs the
  production buildings so the resource loop can be exercised immediately before bespoke
  forest job UI/roles exist.

Run:

```bash
./tools/check_all.sh
UNITY_MCP_UV_OFFLINE=1 MCP_CLI_TIMEOUT=300 TEST_POLL_TIMEOUT=180 tools/run_unity_mcp_gate.sh --no-restart
```

Expected current results:

- Unity gate report passes with scene/import/screenshots, console errors 0.
- EditMode: 383/383 passed.
- PlayMode: 69/69 passed.
- `./tools/check_all.sh` passes design validation, changed Blender verify, asset validation,
  Unity EditMode/PlayMode, and screenshot capture.

Task-owned tests:

- `Map1SystemsTestMapTests`
- `ThreatSourceTests.ForestDebt_RisesFromExtraction_AndFallsFromRestoration`
- forest additions in `ConsequenceNightmareTests`
- forest resource id additions in `ResourceLedgerTests`

## Visual verification

- F2 Economy panel shows the added resources and staffed production buildings.
- F4 Nightmare panel shows Forest Debt, false-guidance fog, false bells, path shifts, and
  the armed forest nightmares.
- EditMode coverage: resource ids, full forest catalog, recipe coverage, Forest Debt
  up/down fold, nightmare arming, false bell lure, true bell break, and lantern-vs-sacred
  fog behavior.

Build `Tools -> Abbey -> Build Prototype Scene`, press Play, and verify the generated map
contains the forest production cluster near the forest edge. To force the False Bell loop,
raise Forest Debt through old wood / venison / charcoal production or trigger a Bell Mimic
through tests/debugging; affected villagers should walk toward a false light until the
Bellkeeper's true bell breaks the lure.

Design intent: this does not mean the shipped first map should teach all mechanics. Once
the full loop feels good, split out a curated first map that introduces a smaller subset
and keep this scene as the regression/playtest map.
