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
- Kenney Mini Characters: https://kenney.nl/assets/mini-characters,
  downloaded 2026-07-10, Creative Commons CC0. The downloaded archive SHA-256 is
  `9e1d48e6d7b8479ebbe84df71eb5bd8e1b3f0da546dea641890dccc8a02d0999`.

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
| `abbey_placeholder_terrain_hill.glb` | `terrain_hill` | KayKit Medieval Builder Pack | `Models/objects/gltf/detail_hill.gltf.glb` | CC0 |
| `abbey_placeholder_forest_cluster_a.glb` | `forest_cluster_01` | KayKit Medieval Builder Pack | `Models/objects/gltf/detail_forestA.gltf.glb` | CC0 |
| `abbey_placeholder_forest_cluster_b.glb` | `forest_cluster_02` | KayKit Medieval Builder Pack | `Models/objects/gltf/detail_forestB.gltf.glb` | CC0 |
| `abbey_placeholder_stream_bridge.glb` | `stream_bridge` | KayKit Medieval Builder Pack | `Models/objects/gltf/bridge.gltf.glb` | CC0 |
| `abbey_placeholder_well.glb` | `well_t1` | KayKit Medieval Builder Pack | `Models/objects/gltf/well.gltf.glb` | CC0 |
| `abbey_placeholder_warrior_lodge.glb` | `warrior_lodge_t1` | KayKit Medieval Builder Pack | `Models/objects/gltf/barracks.gltf.glb` | CC0 |
| `abbey_placeholder_watchtower.glb` | `watchtower_t1` | KayKit Medieval Builder Pack | `Models/objects/gltf/watchtower.gltf.glb` | CC0 |
| `abbey_placeholder_terrain_mountain.glb` | `terrain_mountain` | KayKit Medieval Builder Pack | `Models/objects/gltf/mountain.gltf.glb` | CC0 |
| `abbey_placeholder_forester_lumbermill.glb` | `forester_hut_t1` | KayKit Medieval Builder Pack | `Models/objects/gltf/lumbermill.gltf.glb` | CC0 |
| `abbey_placeholder_herbalist_house.glb` | `herbalist_hut_t1` | KayKit Medieval Builder Pack | `Models/objects/gltf/house.gltf.glb` | CC0 |
| `abbey_placeholder_orchard_plot.glb` | `orchard_plot_t1` | KayKit Medieval Builder Pack | `Models/objects/gltf/farm_plot.gltf.glb` | CC0 |
| `abbey_placeholder_hunter_blind.glb` | `hunter_blind_t1` | KayKit Medieval Builder Pack | `Models/objects/gltf/archeryrange.gltf.glb` | CC0 |
| `abbey_placeholder_stag_garden.glb` | `stag_garden_t1` | KayKit Medieval Builder Pack | `Models/objects/gltf/forest.gltf.glb` | CC0 |
| `abbey_placeholder_grove_shrine.glb` | `grove_shrine_t1` | KayKit Medieval Builder Pack | `Models/objects/gltf/wall_gate.gltf.glb` | CC0 |
| `abbey_placeholder_root_bridge.glb` | `root_bridge_t1` | KayKit Medieval Builder Pack | `Models/objects/gltf/bridge_roofed.gltf.glb` | CC0 |
| `abbey_placeholder_watchtower.glb` | `forest_watchpost_t1` | KayKit Medieval Builder Pack | `Models/objects/gltf/watchtower.gltf.glb` | CC0 |
| `abbey_placeholder_cloister_arch.glb` | `abbey_cloister_repair` | KayKit Dungeon Remastered | `Assets/gltf/wall_arched.gltf.glb` | CC0 |
| `KenneyMiniCharacters/abbey_placeholder_settler_female_b.glb` | `settler_female_b` | Kenney Mini Characters | `Models/GLB format/character-female-b.glb` | CC0 |
| `KenneyMiniCharacters/abbey_placeholder_settler_female_d.glb` | `settler_female_d` | Kenney Mini Characters | `Models/GLB format/character-female-d.glb` | CC0 |
| `KenneyMiniCharacters/abbey_placeholder_settler_female_f.glb` | `settler_female_f` | Kenney Mini Characters | `Models/GLB format/character-female-f.glb` | CC0 |
| `KenneyMiniCharacters/abbey_placeholder_settler_male_a.glb` | `settler_male_a` | Kenney Mini Characters | `Models/GLB format/character-male-a.glb` | CC0 |
| `KenneyMiniCharacters/abbey_placeholder_settler_male_b.glb` | `settler_male_b` | Kenney Mini Characters | `Models/GLB format/character-male-b.glb` | CC0 |
| `KenneyMiniCharacters/abbey_placeholder_settler_male_d.glb` | `settler_male_d` | Kenney Mini Characters | `Models/GLB format/character-male-d.glb` | CC0 |
| `KenneyMiniCharacters/Textures/colormap.png` | settler shared atlas | Kenney Mini Characters | `Models/GLB format/Textures/colormap.png` | CC0 |

The eight KayKit terrain/camp files added on 2026-07-10 have SHA-256 hashes,
in the same table order, beginning `e985f028`, `3ac0d487`, `61c82ce0`,
`fe8b1fc3`, `52660b34`, `d24534c1`, `cb4620b5`, and `a027d4d8`. The exact
source binaries remain in the ignored cache.

The eight KayKit structure files added on 2026-07-10 have SHA-256 prefixes
`4c299278`, `2b067cc2`, `46cd8413`, `9fbe1653`, `d119b27c`, `7c27509f`,
`f7e353fd`, and `916a7d81` in the new structure-table order above. The six
Kenney settler GLBs have prefixes `2288438e`, `67f61708`, `2ff43118`,
`77572792`, `791fc0c2`, and `dd12b2e7`; their shared atlas begins `0d4947d3`.
