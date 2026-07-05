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

- Kenney Nature Kit, CC0: trees, rocks, campfire, logs, canoe.
- Quaternius Medieval Village MegaKit Standard, CC0: crate, wagon-as-barrel proxy,
  and abbey wall straight segment.

Approved but not yet selected into Unity placeholders:

- Poly Haven, CC0: temporary PBR textures/HDRIs/models for realistic terrain, rock,
  wood, stone, and lighting tests.
- ambientCG, CC0: temporary PBR material sets for terrain, ground scars, stone, plaster,
  roofing, wood, fabric, and metal tests.

Cached but not committed as runtime placeholders:

- Quaternius Medieval Village roof and door wall modules. They did not improve the
  prototype camera read enough to justify staging now.
- Quaternius Universal Base Characters. A visual review showed these read as mannequins
  in the prototype camera and are weaker placeholders than the generated villager.

## Prototype Replacement Intent

Safe replacement targets:

- `campfire_t1`: `abbey_placeholder_campfire_stones.fbx`
- `storage_pile_t1`: `abbey_placeholder_storage_logs.fbx`
- `shipwreck_hull`: `abbey_placeholder_shipwreck_hull.fbx` until a better wrecked
  ship source is staged
- `shipwreck_crate_closed`: `abbey_placeholder_wreck_crate.fbx`
- `shipwreck_barrel`: `abbey_placeholder_wreck_barrel_proxy.fbx` until a barrel is
  available
- `forest_tree_01`: `abbey_placeholder_forest_pine.fbx`
- `forest_tree_02`: `abbey_placeholder_forest_oak.fbx`
- `rock_cluster_01`: `abbey_placeholder_rock_cluster.fbx`
- `abbey_wall_broken`: `abbey_placeholder_ruined_wall_straight.fbx`

Keep generated for now:

- `villager_lowpoly`, because the staged Quaternius base humanoids read as mannequins
  in the prototype camera and are weaker placeholders than the generated villager.
- `bellkeeper_lowpoly`, because the Bellkeeper is an identity character.
- `black_hound_lowpoly` and `hound_chain`, because the beast bond depends on a custom
  silhouette.
- `bell_tower_ruined`, because it anchors the abbey silhouette.
- `lantern_post_t1`, until a compatible lamp or torch prop is staged.
- `shelter_t1`, because a visual MCP pass showed the staged Quaternius wall and roof
  modules read as flat panels at the prototype camera scale.

Note: selected FBX `.meta` files are committed with the Abbey placeholder copies so
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
