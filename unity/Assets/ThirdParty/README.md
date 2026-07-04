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

## Recreating This Import

The repo commits only the selected runtime files. To recreate the current import from
fresh downloads:

1. Download the source archives.

   Kenney Nature Kit has a direct archive URL:

   ```sh
   curl -L -o /tmp/kenney_nature-kit.zip \
     https://kenney.nl/media/pages/assets/nature-kit/37ac38a37b-1677698939/kenney_nature-kit.zip
   ```

   Quaternius packs should be downloaded through the Itch pages listed below:

   - https://quaternius.itch.io/medieval-village-megakit
   - https://quaternius.itch.io/universal-base-characters

   On each page, choose **Download Now**, use the free option, and download the
   Standard archive. Save them as:

   - `/tmp/quaternius-medieval-village-megakit-standard.zip`
   - `/tmp/quaternius-universal-base-characters-standard.zip`

2. Extract the selected files only.

   The Quaternius archive folders contain square brackets, so extract those archives to
   temp folders first and copy from there:

   ```sh
   mkdir -p /tmp/abbey-medieval-extract /tmp/abbey-characters-extract
   unzip -q -o /tmp/quaternius-medieval-village-megakit-standard.zip -d /tmp/abbey-medieval-extract
   unzip -q -o /tmp/quaternius-universal-base-characters-standard.zip -d /tmp/abbey-characters-extract
   ```

   Recreate the destination folders:

   ```sh
   mkdir -p \
     unity/Assets/ThirdParty/Kenney/NatureKit/FBX \
     unity/Assets/ThirdParty/Quaternius/MedievalVillageMegaKit/FBX \
     unity/Assets/ThirdParty/Quaternius/UniversalBaseCharacters/BaseCharacters/Unity
   ```

   Copy the Kenney files:

   ```sh
   unzip -j -o /tmp/kenney_nature-kit.zip \
     'Models/FBX format/campfire_stones.fbx' \
     'Models/FBX format/canoe.fbx' \
     'Models/FBX format/log_stackLarge.fbx' \
     'Models/FBX format/rock_largeA.fbx' \
     'Models/FBX format/tree_oak.fbx' \
     'Models/FBX format/tree_pineDefaultA.fbx' \
     -d unity/Assets/ThirdParty/Kenney/NatureKit/FBX

   unzip -j -o /tmp/kenney_nature-kit.zip \
     'License.txt' \
     -d unity/Assets/ThirdParty/Kenney/NatureKit
   ```

   Copy the Quaternius files:

   ```sh
   cp /tmp/abbey-medieval-extract/'Medieval Village MegaKit[Standard]'/FBX/Prop_Crate.fbx \
     unity/Assets/ThirdParty/Quaternius/MedievalVillageMegaKit/FBX/
   cp /tmp/abbey-medieval-extract/'Medieval Village MegaKit[Standard]'/FBX/Prop_Wagon.fbx \
     unity/Assets/ThirdParty/Quaternius/MedievalVillageMegaKit/FBX/
   cp /tmp/abbey-medieval-extract/'Medieval Village MegaKit[Standard]'/FBX/Roof_Tower_RoundTiles.fbx \
     unity/Assets/ThirdParty/Quaternius/MedievalVillageMegaKit/FBX/
   cp /tmp/abbey-medieval-extract/'Medieval Village MegaKit[Standard]'/FBX/Wall_UnevenBrick_Door_Round.fbx \
     unity/Assets/ThirdParty/Quaternius/MedievalVillageMegaKit/FBX/
   cp /tmp/abbey-medieval-extract/'Medieval Village MegaKit[Standard]'/FBX/Wall_UnevenBrick_Straight.fbx \
     unity/Assets/ThirdParty/Quaternius/MedievalVillageMegaKit/FBX/
   cp /tmp/abbey-medieval-extract/'Medieval Village MegaKit[Standard]'/License_Standard.txt \
     unity/Assets/ThirdParty/Quaternius/MedievalVillageMegaKit/

   cp /tmp/abbey-characters-extract/'Universal Base Characters[Standard]'/'Base Characters'/Unity/Superhero_Female_FullBody.fbx \
     unity/Assets/ThirdParty/Quaternius/UniversalBaseCharacters/BaseCharacters/Unity/
   cp /tmp/abbey-characters-extract/'Universal Base Characters[Standard]'/'Base Characters'/Unity/Superhero_Male_FullBody.fbx \
     unity/Assets/ThirdParty/Quaternius/UniversalBaseCharacters/BaseCharacters/Unity/
   cp /tmp/abbey-characters-extract/'Universal Base Characters[Standard]'/License_Standard.txt \
     unity/Assets/ThirdParty/Quaternius/UniversalBaseCharacters/
   ```

3. Open the project in Unity so FBX `ModelImporter` metadata can be generated or
   normalized locally.

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
