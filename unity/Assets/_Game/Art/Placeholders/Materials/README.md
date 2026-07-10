# Abbey Material Placeholders

This folder contains the curated runtime copies of temporary CC0 PBR material maps.
The full source downloads live in the ignored `third_party_cache/` folder; Unity
runtime code must reference only these Abbey-named placeholder files.

Downloaded on 2026-07-05. All selected files are 1k JPG maps.

## Acquisition

Poly Haven assets were discovered through the public API using a unique User-Agent and
downloaded from the file URLs returned by `https://api.polyhaven.com/files/<asset_id>`.
For each selected Poly Haven material, the 1k JPG `Diffuse`, `nor_gl`, and `Rough`
maps were copied into this folder with Abbey-owned names.

ambientCG assets were discovered through the v3 assets API and downloaded as 1K-JPG ZIP
archives from the `downloads` URL returned by:

- `https://ambientCG.com/api/v3/assets?type=material&q=grass&sort=popular&limit=8&include=title,url,downloads,maps,tags`
- `https://ambientCG.com/api/v3/assets?type=material&q=wood&sort=popular&limit=10&include=title,url,downloads,maps,tags`

The selected archives were `https://ambientCG.com/get?file=Grass005_1K-JPG.zip` and
`https://ambientCG.com/get?file=Wood095_1K-JPG.zip`. Their `Color`, `NormalGL`, and
`Roughness` maps were copied into this folder with Abbey-owned names.

## Selected Sources

| Abbey placeholder | Prototype role | Source asset | Provider | Source URL | Cache path |
| --- | --- | --- | --- | --- | --- |
| `abbey_placeholder_ground_grass_*` | Meadow and forest floor map material (`map_meadow`, `map_ForestFloor`) | `Grass005` | ambientCG, CC0 | https://ambientcg.com/a/Grass005 | `third_party_cache/ambientCG/Materials/Grass005/` |
| `abbey_placeholder_beach_sand_*` | Shipwreck beach map material (`map_Beach`) | `coast_sand_03` | Poly Haven, CC0 | https://polyhaven.com/a/coast_sand_03 | `third_party_cache/PolyHaven/Textures/coast_sand_03/` |
| `abbey_placeholder_abbey_stone_*` | Abbey hill, rocks, walls, paving, and plaza placeholder material (`map_abbey_hill` plus stone-like imported placeholders) | `rock_face_03` | Poly Haven, CC0 | https://polyhaven.com/a/rock_face_03 | `third_party_cache/PolyHaven/Textures/rock_face_03/` |
| `abbey_placeholder_weathered_wood_*` | Generic temporary wood for barrels, crates, hulls, planks, shelter, and storage placeholders | `Wood095` | ambientCG, CC0 | https://ambientcg.com/a/Wood095 | `third_party_cache/ambientCG/Materials/Wood095/` |

## Runtime Use

The prototype scene builder uses the albedo and OpenGL normal maps for:

- meadow and forest ground;
- beach sand;
- abbey hill, stone, rock, paving, and wall materials;
- generic wood, barrel, crate, hull, plank, shelter, and storage materials.

`UrpProjectConfigurator` reproducibly imports the normal maps as linear normal textures
and the roughness maps as linear data. URP Lit materials receive the matching normal map
and a surface-appropriate smoothness scalar. The original roughness maps remain committed
for a future packed-mask pass; they are deliberately not connected directly because URP
Lit expects smoothness in a packed alpha channel rather than a standalone roughness map.

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
