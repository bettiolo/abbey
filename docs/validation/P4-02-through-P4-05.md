# P4-02 through P4-05 — Abbey of Antlers integration

## What changed

Phase 4 now has a generated second scene, `Map2Prototype`: sacred grove, orchard, deep
forest, stream, charcoal camp, deer paths, stone circle, hidden graves, corrupted logging
camp, and the Abbey of Antlers. The Stag uses a spec/generated GLB and a deterministic
`StagCovenantSystem` fold over trust, patience, wound, wildness, and covenant. `Map2Config`
owns every threshold, reaction, interaction cost, dilemma day, carryover modifier, and
win requirement. Map 1's saved result grants one Bellkeeper trait; its spring-ship prompt
loads Map 2. The P panel exposes Stag state, Forest Debt, trait, both victory routes, the
four dilemmas, and the final story line.

## Technical verification

Run:

```sh
./tools/check_all.sh
UNITY_MCP_UV_OFFLINE=1 MCP_CLI_TIMEOUT=300 TEST_POLL_TIMEOUT=240 \
  tools/run_unity_mcp_gate.sh --no-restart
```

Expected Phase 4 baseline:

- Unity gate passes both generated scenes, 49/49 imported assets, four Map 1 proofs and
  two Map 2 proofs, with zero recorded console errors.
- EditMode: 388/388. PlayMode: 70/70.
- Asset validation: 335 passed, 8 skipped; all 49 assets structurally match a clean
  pipeline rebuild. `stag_beneath_abbey_lowpoly` separately passes pivot, footprint,
  372/1500 triangle, 3/3 shared-material, anchor, collision, and preview checks.
- Task-owned tests: `Map2SystemsTests`, `Map2FullLoopPlayModeTests`, and the Map 2 fields
  in `AbbeyUnityGateTests`.

The two win routes are intentionally distinct: an Allied Stag + restorative stock + low
Forest Debt yields Covenant Victory; old wood/charcoal/venison can yield Exploitative
Victory while debt stays below the hard ceiling and covenant remains unbroken. Bellkeeper
death, no living villagers, or Horned Accuser is a loss.

## Visual verification

1. Build **Tools → Abbey → Build Campaign Scenes (Start Map 1)** to test the transition,
   or **Build Map 2 Scene** for a focused pass. In Map 2 press Play, then press **P**.
2. Confirm the grove centers the Stag and the map reads as forest rather than Map 1 coast.
3. Use 1–4 for indirect Stag interactions. Use 5 to raise forest dilemmas and **I** to
   choose them. Use 6/7 only as extraction/restoration debug accelerators.
4. Confirm `map2_grove_day.png` has no magenta materials and shows the Stag inside a dense
   grove. Confirm `map2_false_bell_night.png` shows the cold false light offset from the
   settlement in a darkened forest.
5. Replay once toward each victory. `GATE-P4` remains human-owned: judge whether the two
   routes produce meaningfully different stories before approving the phase.
