# Third-Party Asset Manifest

This folder contains selected off-the-shelf prototype assets. It intentionally does not
contain raw downloaded archives.

Rules:

- Keep vendor files in their source folders.
- Do not edit vendor FBX or texture files directly.
- Record every new vendor source below before using it in gameplay or scene generation.
- Prefer wrapper prefabs, editor placement code, or data mappings for scale, materials,
  and composition.
- Folder and text `.meta` files are committed. FBX `ModelImporter` metas are left for
  the local Unity editor to generate or normalize, since this environment cannot run
  Unity import validation.

## Sources

### Kenney Nature Kit

- Source: https://kenney.nl/assets/nature-kit
- License: Creative Commons CC0
- Downloaded: 2026-07-04
- Archive used locally: `/tmp/kenney_nature-kit.zip`
- Staged license file: `Kenney/NatureKit/License.txt`
- Selected files:
  - `Kenney/NatureKit/FBX/campfire_stones.fbx`
  - `Kenney/NatureKit/FBX/canoe.fbx`
  - `Kenney/NatureKit/FBX/log_stackLarge.fbx`
  - `Kenney/NatureKit/FBX/rock_largeA.fbx`
  - `Kenney/NatureKit/FBX/tree_oak.fbx`
  - `Kenney/NatureKit/FBX/tree_pineDefaultA.fbx`

### Quaternius Medieval Village MegaKit Standard

- Source: https://quaternius.itch.io/medieval-village-megakit
- License: Creative Commons CC0
- Downloaded: 2026-07-04
- Archive used locally: `/tmp/quaternius-medieval-village-megakit-standard.zip`
- Staged license file: `Quaternius/MedievalVillageMegaKit/License_Standard.txt`
- Selected files:
  - `Quaternius/MedievalVillageMegaKit/FBX/Prop_Crate.fbx`
  - `Quaternius/MedievalVillageMegaKit/FBX/Prop_Wagon.fbx`
  - `Quaternius/MedievalVillageMegaKit/FBX/Roof_Tower_RoundTiles.fbx`
  - `Quaternius/MedievalVillageMegaKit/FBX/Wall_UnevenBrick_Door_Round.fbx`
  - `Quaternius/MedievalVillageMegaKit/FBX/Wall_UnevenBrick_Straight.fbx`

### Quaternius Universal Base Characters Standard

- Source: https://quaternius.itch.io/universal-base-characters
- License: Creative Commons CC0
- Downloaded: 2026-07-04
- Archive used locally: `/tmp/quaternius-universal-base-characters-standard.zip`
- Staged license file: `Quaternius/UniversalBaseCharacters/License_Standard.txt`
- Selected files:
  - `Quaternius/UniversalBaseCharacters/BaseCharacters/Unity/Superhero_Female_FullBody.fbx`
  - `Quaternius/UniversalBaseCharacters/BaseCharacters/Unity/Superhero_Male_FullBody.fbx`

## Pending Sources

These are useful but not staged yet:

- Quaternius Stylized Nature MegaKit: https://quaternius.itch.io/stylized-nature-megakit
- Quaternius Fantasy Props MegaKit: https://quaternius.itch.io/fantasy-props-megakit
- Quaternius Ships: https://quaternius.com/packs/ships.html
- Quaternius Ultimate Modular Ruins: https://quaternius.com/packs/ultimatemodularruins.html
- Quaternius Ultimate Animated Animals: https://quaternius.com/packs/ultimateanimatedanimals.html
