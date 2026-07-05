# Third-Party Cache

This folder is the local, ignored cache for downloaded source packs. Git tracks only
this README; the pack contents below it are intentionally ignored.

Unity runtime code must not reference files from this cache. When a mesh, texture, or
material placeholder is selected for the prototype, copy only the chosen runtime files
into `unity/Assets/_Game/Art/Placeholders/` with `abbey_placeholder_*` names and record
their source in the relevant placeholder README.

Expected local layout when the cache is populated:

- `Kenney/NatureKit/`
- `Quaternius/MedievalVillageMegaKit/`
- `Quaternius/UniversalBaseCharacters/`
- `PolyHaven/Textures/`
- `PolyHaven/HDRIs/`
- `PolyHaven/Models/`
- `ambientCG/Materials/`
- `ambientCG/HDRIs/`
- `ambientCG/Models/`

The cache is allowed to contain full extracted packs, source licenses, and exploratory
files that are not yet committed as runtime placeholders.

Current source references:

- Kenney Nature Kit: https://kenney.nl/assets/nature-kit
- Quaternius Medieval Village MegaKit: https://quaternius.itch.io/medieval-village-megakit
- Quaternius Universal Base Characters: https://quaternius.itch.io/universal-base-characters
- Poly Haven: https://polyhaven.com/
- ambientCG: https://ambientcg.com/

The current cache was populated from downloads used on 2026-07-04. Re-download packs
from the source pages above if local cache contents are missing or stale.
