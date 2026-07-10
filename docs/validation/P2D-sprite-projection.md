# P2D reversible sprite-projection candidate

Validated locally on 2026-07-10 at runtime commit `63bda64` with Unity `6000.5.2f1`.

## Implemented candidate

- Curated and pinned the official Merchant Shade / octoshrimpy Mini World Sprites archive
  under CC0 1.0. The committed subset contains 32 sheets, 341 deterministic slices, and
  61 mapped Abbey roles.
- Generated texture import settings and `MiniWorldSpriteProjectionCatalog.asset` from the
  manifest; the importer and validator reject source drift, path traversal, bad geometry,
  duplicate identities, invalid footprints, and catalog drift.
- Added a reversible presentation child that never changes gameplay-root transforms,
  collision, movement, jobs, combat, or tick order. Disabling projection restores every
  legacy renderer's previous enabled state.
- Added camera-facing actors/buildings/props, stable projected-depth ordering, authored wall
  footprints, phase tint, runtime-spawn registration, and XZ-tiled terrain patches.
- Integrated both generated maps while preserving scene names and all existing gameplay
  components.

## Authoritative verification

- Unity MCP gate: passed; Prototype01 and Map2Prototype built.
- Sprite manifest/import/catalog validation: passed.
- EditMode: 415/415 passed.
- PlayMode: 70/70 passed.
- Unity console errors: 0.
- Canonical images inspected: `day_camp`, `dusk_recall`, `night_attack`, `morning_after`,
  `map2_grove_day`, and `map2_false_bell_night`.
- `./tools/check_all.sh`: OK; design 7/7, assets 355 passed / 8 skipped, Blender changed
  verification clean. Unity batch steps skipped because the MCP editor held the project
  lock; the authoritative MCP results above cover them.

## Honest visual blocker

The pinned pack has no honest sprite for 23 roles. Signature gaps include the Bellkeeper,
Black Hound, Stag, ruined bell tower, cloister, shipwreck pieces, hound chain, sacred flame,
and several bespoke nightmares; common gaps include campfire, lantern post, and charcoal
kiln. The manifest records every missing role and its reason. These objects deliberately
retain the existing reversible 3D identity instead of receiving a misleading substitute.

Consequently this branch is a draft technical candidate, not an approved final art pass.
`P2D-07` remains in progress and `GATE-P2D-ART` remains pending until those gaps are either
filled from reviewed compatible sources or explicitly accepted at the human art review.
