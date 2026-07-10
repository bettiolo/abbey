# Third-Party Cache

This folder is the local, ignored cache for downloaded source packs. Git tracks only
this README; the pack contents below it are intentionally ignored.

Unity runtime code must not reference files from this cache. When a mesh, texture, or
material placeholder is selected for the prototype, copy only the chosen runtime files
into `unity/Assets/_Game/Art/Placeholders/` with `abbey_placeholder_*` names and record
their source in the relevant placeholder README.

Expected local layout when the cache is populated:

- `Kenney/NatureKit/`
- `Kenney/PirateKit/`
- `KayKit/MedievalBuilder/`
- `KayKit/DungeonRemastered/`
- `Quaternius/MedievalVillageMegaKit/`
- `Quaternius/UltimateModularRuins/`
- `Quaternius/UniversalBaseCharacters/`
- `PolyHaven/Textures/`
- `PolyHaven/HDRIs/`
- `PolyHaven/Models/`
- `ambientCG/Materials/`
- `ambientCG/HDRIs/`
- `ambientCG/Models/`
- `MerchantShade/MiniWorldSprites/`

The cache is allowed to contain full extracted packs, source licenses, and exploratory
files that are not yet committed as runtime placeholders.

Current source references:

- Kenney Nature Kit: https://kenney.nl/assets/nature-kit
- Kenney Pirate Kit: https://kenney.nl/assets/pirate-kit
- KayKit Medieval Builder Pack: https://kaylousberg.itch.io/kaykit-medieval-builder-pack
- KayKit Dungeon Remastered: https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0
- Quaternius Medieval Village MegaKit: https://quaternius.itch.io/medieval-village-megakit
- Quaternius Ultimate Modular Ruins Pack: https://quaternius.com/packs/ultimatemodularruins.html
- Quaternius Universal Base Characters: https://quaternius.itch.io/universal-base-characters
- Poly Haven: https://polyhaven.com/
- ambientCG: https://ambientcg.com/
- Merchant Shade Mini World Sprites: https://merchant-shade.itch.io/16x16-mini-world-sprites

Current selected texture/material cache entries:

- `ambientCG/Materials/Grass005/` -> `abbey_placeholder_ground_grass_*`
- `PolyHaven/Textures/coast_sand_03/` -> `abbey_placeholder_beach_sand_*`
- `PolyHaven/Textures/rock_face_03/` -> `abbey_placeholder_abbey_stone_*`
- `ambientCG/Materials/Wood095/` -> `abbey_placeholder_weathered_wood_*`

See `unity/Assets/_Game/Art/Placeholders/Materials/README.md` for the acquisition
method, source URLs, and prototype roles for these selected material placeholders.

The current cache was populated from downloads used on 2026-07-04 and 2026-07-05.
Re-download packs from the source pages above if local cache contents are missing or
stale. `Quaternius/UltimateModularRuins/` may be partial if Google Drive blocks one of
the public file links during scripted download; Unity runtime placeholders do not
reference that cache directly.

Merchant Shade's cache is acquired only through itch.io's public first-party download
flow by `tools/acquire_merchant_shade_miniworld.py`. The tool pins itch game `703908`,
upload `7054436`, archive size `2,084,074`, and SHA-256
`79eb000cfd3f64fee8ac8307f02bb867dc8b4fd7ce5a150119c51dedfa563f1f`; it rejects
source/license drift, HTML responses, and unsafe ZIP members. The ZIP, guide, extracted
pack, and acquisition audit remain ignored. Only the reviewed CC0 subset recorded by
`unity/Assets/_Game/Art/Placeholders/MerchantShadeMiniWorld/manifest.json` is committed.
