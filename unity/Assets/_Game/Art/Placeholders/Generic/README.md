# Generic Placeholder Art

These committed FBX/GLB files are selected prototype placeholders copied from permissive
third-party packs and renamed with Abbey-owned placeholder names. The downloaded source
packs belong in the ignored `third_party_cache/` folder, not under `unity/Assets/`.

Designers can replace any placeholder by preserving the Abbey filename, or by updating
`PrototypeSceneBuilder` to point at the replacement asset.

`PrototypeSceneBuilder` normalizes these models by visible renderer bounds after
instantiation. The source pack's authored unit scale is not trusted directly: each
placeholder gets an Abbey target world size, collision helper meshes are ignored, and
the visible bottom is snapped back to the requested ground position.

Source pages:

- Kenney Nature Kit: https://kenney.nl/assets/nature-kit, downloaded 2026-07-04,
  Creative Commons CC0.
- Kenney Pirate Kit: https://kenney.nl/assets/pirate-kit, downloaded 2026-07-05,
  Creative Commons CC0.
- KayKit Medieval Builder Pack: https://kaylousberg.itch.io/kaykit-medieval-builder-pack,
  downloaded 2026-07-05, Creative Commons CC0.
- KayKit Dungeon Remastered: https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0,
  downloaded 2026-07-05, Creative Commons CC0.
- Quaternius Medieval Village MegaKit: https://quaternius.itch.io/medieval-village-megakit,
  downloaded 2026-07-04, Creative Commons CC0.

| Abbey placeholder | Prototype asset id | Source pack | Source file | License |
| --- | --- | --- | --- | --- |
| `abbey_placeholder_campfire_stones.fbx` | `campfire_t1` | Kenney Nature Kit | `campfire_stones.fbx` | CC0 |
| `abbey_placeholder_storage_logs.fbx` | `storage_pile_t1` | Kenney Nature Kit | `log_stackLarge.fbx` | CC0 |
| `abbey_placeholder_shipwreck_hull.fbx` | `shipwreck_hull` | Kenney Pirate Kit | `ship-wreck.fbx` | CC0 |
| `abbey_placeholder_wreck_crate.fbx` | `shipwreck_crate_closed` | Kenney Pirate Kit | `crate.fbx` | CC0 |
| `abbey_placeholder_wreck_barrel.fbx` | `shipwreck_barrel` | Kenney Pirate Kit | `barrel.fbx` | CC0 |
| `abbey_placeholder_shelter_house.fbx` | `shelter_t1` | KayKit Medieval Builder Pack | `Models/objects/fbx/house.fbx` | CC0 |
| `abbey_placeholder_lantern_torch.glb` | `lantern_post_t1` | KayKit Dungeon Remastered | `Assets/gltf/torch_lit.gltf.glb` | CC0 |
| `abbey_placeholder_ruined_wall_broken.glb` | `abbey_wall_broken` | KayKit Dungeon Remastered | `Assets/gltf/wall_broken.gltf.glb` | CC0 |
| `abbey_placeholder_forest_pine.fbx` | `forest_tree_01` | Kenney Nature Kit | `tree_pineDefaultA.fbx` | CC0 |
| `abbey_placeholder_forest_oak.fbx` | `forest_tree_02` | Kenney Nature Kit | `tree_oak.fbx` | CC0 |
| `abbey_placeholder_rock_cluster.fbx` | `rock_cluster_01` | Kenney Nature Kit | `rock_largeA.fbx` | CC0 |
