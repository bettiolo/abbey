# Abbey Material Placeholders

This folder contains the curated runtime copies of temporary CC0 PBR material maps.
The full source downloads live in the ignored `third_party_cache/` folder; Unity
runtime code must reference only these Abbey-named placeholder files.

Downloaded on 2026-07-05. All selected files are 1k JPG maps.

## Selected Sources

| Abbey placeholder | Source asset | Provider | Source URL | Cache path |
| --- | --- | --- | --- | --- |
| `abbey_placeholder_ground_grass_*` | `Grass005` | ambientCG, CC0 | https://ambientcg.com/a/Grass005 | `third_party_cache/ambientCG/Materials/Grass005/` |
| `abbey_placeholder_beach_sand_*` | `coast_sand_03` | Poly Haven, CC0 | https://polyhaven.com/a/coast_sand_03 | `third_party_cache/PolyHaven/Textures/coast_sand_03/` |
| `abbey_placeholder_abbey_stone_*` | `rock_face_03` | Poly Haven, CC0 | https://polyhaven.com/a/rock_face_03 | `third_party_cache/PolyHaven/Textures/rock_face_03/` |
| `abbey_placeholder_weathered_wood_*` | `Wood095` | ambientCG, CC0 | https://ambientcg.com/a/Wood095 | `third_party_cache/ambientCG/Materials/Wood095/` |

## Runtime Use

The prototype scene builder currently uses the albedo maps for:

- meadow and forest ground;
- beach sand;
- abbey hill, stone, rock, paving, and wall materials;
- generic wood, barrel, crate, hull, plank, shelter, and storage materials.

Normal and roughness maps are committed beside the albedo maps because they are part of
the selected placeholder material sets and are useful for the next material pass. They
are not wired yet; do that through stable Unity import settings or material assets, not
by mutating importer metadata inside the scene builder.

## Not Picked Yet

Good future candidates from the same sources:

- Poly Haven HDRIs for neutral lighting reference and screenshot consistency.
- Poly Haven scanned rocks, boulders, gravel roads, bark, mossy rock, and additional
  coastal terrain variants.
- ambientCG mud, wet ground, moss, plaster, roofing tiles, fabric, metal, decals,
  stone wall variants, dirt paths, wood beams, and weathering overlays.
- Poly Haven `forest_ground_04` was evaluated for meadow/forest ground but reads too
  gray and rocky at the prototype camera distance.
- Poly Haven `aerial_grass_rock` was evaluated for meadow/forest ground but still reads
  more gray-brown than camp meadow in the prototype camera.
- Larger 2k/4k versions of the selected maps if a designer explicitly asks for sharper
  close camera shots.

Do not import whole libraries into `unity/Assets/`. Pick an asset, copy only the
runtime maps with `abbey_placeholder_*` names, and document the source here.
