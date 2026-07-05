# Third-Party Asset Strategy

The goal is to unblock gameplay work without turning the project into an asset
production exercise. Use off-the-shelf assets for replaceable prototype art, and
reserve the Blender pipeline for assets that define the game's identity.

## Policy

- Use third-party assets for gameplay placeholders, readable props, terrain dressing,
  temporary PBR textures/materials, generic villagers, crates, walls, trees, rocks, camp
  objects, and other non-signature art.
- Keep custom generated assets for identity pieces: the ruined abbey silhouette, bell
  tower, Black Hound, hound chain, signature nightmares, sacred flame language, and any
  asset that needs to match the art bible exactly.
- Prefer CC0 or permissive assets with clear provenance. Do not rely on assets whose
  license cannot be recorded in the repo.
- Do not import whole asset packs unless the pack is intentionally small. Stage only the
  files the prototype needs.
- Do not edit third-party source files directly. If Unity needs scale, materials,
  colliders, or composition, wrap the source model in a prefab or editor-generated
  replacement object.
- Do not feed third-party assets into AI generation or texture generation workflows.
- Do not let replacement art block the core loop. If a third-party asset is missing, the
  scene builder must still fall back to generated art or primitives.

## Source Priority

1. **Poly Haven CC0 assets** for higher-fidelity temporary PBR textures, HDRIs, rocks,
   scanned natural props, and other realistic surface/reference assets.
2. **ambientCG CC0 assets** for temporary PBR materials: ground, mud, grass, stone,
   plaster, wood, roof, fabric, metal, and weathering overlays.
3. **Kenney/Quaternius/KayKit CC0 assets** only for blockout, simple props, or gameplay
   silhouettes when the low-poly style is acceptable for the specific object.
4. **Paid store/Fab/marketplace packs** only for a clear bottleneck where CC0 sources
   are not enough. Record the invoice, license, permitted seats, and redistribution
   limits before committing anything.
5. **Custom Blender generation** when an asset is an identity asset or when the gameplay
   needs a very specific silhouette that store assets do not cover.

## Temporary Texture Sources

Approved default sources:

- **Poly Haven**: https://polyhaven.com/ - CC0 textures, HDRIs, and models. Use for
  natural ground, stone, rock, bark/wood, weathered surfaces, and scene lighting
  reference.
- **ambientCG**: https://ambientcg.com/ - CC0 PBR materials, HDRIs, and models. Use for
  ground, mud, grass, stone, plaster, roof, fabric, metal, and wood material tests.

Manual-review-only sources:

- **ShareTextures** and similar "CC0-based" libraries with redistribution limits. These
  can be useful for local experiments, but do not commit raw files or derived texture
  sets unless the exact asset license allows repository redistribution.
- **Fab/Megascans/paid stores**. These can be excellent visual placeholders, but usually
  belong in a private/local dependency workflow rather than a public committed asset
  path. Document seat/license requirements before use.

## Cache And Runtime Layout

Downloaded source packs and texture/material archives live in the ignored repo-local
cache at `third_party_cache/`. The cache may contain full extracted packs, source
licenses, exploratory textures, HDRIs, and material maps, but Unity runtime code must not
reference files from it.

Only selected placeholders are committed under
`unity/Assets/_Game/Art/Placeholders/`, using Abbey-owned `abbey_placeholder_*`
filenames. Mesh placeholders currently live under `Generic/`; selected texture/material
placeholders should live under a dedicated textures/materials subfolder when first used.
Those files are replaceable by designers without preserving third-party pack folder
structure in the Unity project.

Currently cached/selected sources:

- Kenney Nature Kit, CC0: trees, rocks, campfire, and logs.
- Kenney Pirate Kit, CC0: wrecked ship, shipwreck crate, and shipwreck barrel.
- KayKit Medieval Builder Pack, CC0: temporary shelter/house placeholder.
- KayKit Dungeon Remastered, CC0: temporary broken wall and lit torch placeholders.
- Poly Haven, CC0: selected 1k JPG PBR texture sets for beach sand and abbey stone
  surfaces.
- ambientCG, CC0: selected 1k JPG PBR texture sets for meadow/forest ground and
  weathered wood placeholders.

The exact acquisition method, source asset IDs, cache paths, and Abbey placeholder roles
for the selected material maps are recorded in
`unity/Assets/_Game/Art/Placeholders/Materials/README.md`.

Approved but not yet selected into Unity placeholders:

- Quaternius Ultimate Modular Ruins Pack, CC0: promising ruin modules, props, trees,
  and dungeon pieces. The 2026-07-05 scripted Google Drive sync was partial because
  Drive blocked public file URLs mid-folder, so no runtime placeholder currently
  depends on this cache.
- Poly Haven HDRIs, scanned rocks, boulders, bark, mossy rocks, gravel roads, and
  additional coastal/forest terrain variants. `forest_ground_04` was evaluated for
  meadow/forest ground and skipped because it reads too gray and rocky in the prototype
  camera; `aerial_grass_rock` was evaluated too and still read more gray-brown than
  camp meadow.
- ambientCG mud, wet ground, moss, plaster, roofing tiles, fabric, metal, decals,
  dirt paths, wood beams, weathering overlays, and stone wall variants.
- Paid store/Fab/Megascans assets. These remain manual-review-only and should be treated
  as private/local dependencies unless the exact license permits repo redistribution.

Cached but not committed as runtime placeholders:

- Quaternius Medieval Village roof and door wall modules. They did not improve the
  prototype camera read enough to justify staging now.
- Quaternius Medieval Village crate, wagon proxy, and straight wall modules. These were
  replaced by better Kenney/KayKit placeholders.
- Quaternius Universal Base Characters. A visual review showed these read as mannequins
  in the prototype camera and are weaker placeholders than the generated villager.

## Prototype Replacement Intent

Safe replacement targets:

- `map_meadow`: `abbey_placeholder_ground_grass_albedo.jpg`
- `map_ForestFloor`: `abbey_placeholder_ground_grass_albedo.jpg`
- `map_Beach`: `abbey_placeholder_beach_sand_albedo.jpg`
- `map_abbey_hill`: `abbey_placeholder_abbey_stone_albedo.jpg`
- generic wood/stone placeholder materials: curated Abbey albedo maps from
  `unity/Assets/_Game/Art/Placeholders/Materials/`
- `campfire_t1`: `abbey_placeholder_campfire_stones.fbx`
- `storage_pile_t1`: `abbey_placeholder_storage_logs.fbx`
- `shipwreck_hull`: `abbey_placeholder_shipwreck_hull.fbx`
- `shipwreck_crate_closed`: `abbey_placeholder_wreck_crate.fbx`
- `shipwreck_barrel`: `abbey_placeholder_wreck_barrel.fbx`
- `shelter_t1`: `abbey_placeholder_shelter_house.fbx`
- `lantern_post_t1`: `abbey_placeholder_lantern_torch.glb`
- `forest_tree_01`: `abbey_placeholder_forest_pine.fbx`
- `forest_tree_02`: `abbey_placeholder_forest_oak.fbx`
- `rock_cluster_01`: `abbey_placeholder_rock_cluster.fbx`
- `abbey_wall_broken`: `abbey_placeholder_ruined_wall_broken.glb`

Keep generated for now:

- `villager_lowpoly`, because the staged Quaternius base humanoids read as mannequins
  in the prototype camera and are weaker placeholders than the generated villager.
- `bellkeeper_lowpoly`, because the Bellkeeper is an identity character.
- `black_hound_lowpoly` and `hound_chain`, because the beast bond depends on a custom
  silhouette.
- `bell_tower_ruined`, because it anchors the abbey silhouette.

Note: selected placeholder `.meta` files are committed with the Abbey placeholder copies so
Unity import identity stays stable across checkouts.

## Workflow

1. Add or choose the source pack or texture/material set.
2. Record pack name, source URL, license, download date, and selected files in
   the relevant `unity/Assets/_Game/Art/Placeholders/` README.
3. Keep the downloaded pack contents in `third_party_cache/`; do not commit the cache
   contents.
4. Copy only selected runtime files into
   `unity/Assets/_Game/Art/Placeholders/` with Abbey-specific placeholder names.
5. Add wrapper prefabs or scene-builder mappings that reference the Abbey placeholder
   files by path.
6. Run `./tools/check_all.sh`. Unity import validation still requires a local Unity
   editor, as documented in `docs/VERIFICATION_STATUS.md`.
