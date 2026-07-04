# Art reference: Abbey / Town Center / Field baseline

Source: user-provided pixel-art isometric diorama renders, committed in this folder:

- **`docs/abbey-town-1.png` — THE BASELINE** (user decision 2026-07-02). All palette
  extraction and silhouette matching targets this file first.
- `docs/abbey-town-2.png` … `abbey-town-4.png` — same complex, other rotations.
- `docs/abbey-town-ruin-1.png` — **ruined state** of the same complex: the reference for
  damaged/ruined asset variants (abbey_gate_ruined, bell_tower_ruined, broken walls) and
  for the Broken-Abbey moral variant.

Because the actual pixels are in-repo, palette claims must stay **traceable to the
baseline image** (deterministic sampling/clustering of `abbey-town-1.png`, masking the
baked checkerboard background: near-greyscale pixels with r≈g≈b > 170). The two palette
tables below play different roles — see the decision note under the measured table:
the transcribed ramps are the **albedo inputs** the texture generator uses; the measured
clusters are the **lit-appearance target** that finished renders (previews) are verified
against. Neither supersedes the other; they sit at different points of the pipeline.

## Overall read

A square diorama tile: grass top, exposed dirt cliff sides. One quadrant is a gothic
abbey church with attached cloister; one is a large half-timbered town hall / guildhall
with a massive stone bell tower; the rest is village ground — dirt paths, stone paving,
vegetable field rows, a roofed well, barrels, crates, chickens, a dog, market stalls,
blue-and-yellow pennant flags. Warm, saturated, storybook pixel-art. Strong readable
silhouettes; visible tile courses on every roof; chunky stone block outlines; smoke wisps
from chimneys.

This matches the game's Sanctuary-Abbey mood target (GAME_DESIGN §9, ART_BIBLE moral
variants): bucolic, safe, inviting — the "day" pole of the day/night contrast.

## Measured palette (sampled from `abbey-town-1.png`, checkerboard masked — lit-appearance target)

Dominant clusters (12-bit quantization, ≥900 samples at stride 2):

| Surface | Measured values |
|---------|----------------|
| roof_terracotta | `#874934 #763a26 #683726 #582617` |
| grass | `#86a736 #769828` (olive — darker than first transcription) |
| dirt_path / soil | `#c7a46a #775636 #563926 #452a19` |
| stone_warm | `#b5aa95 #857767 #776759 #665748` |
| stone_cool / slate | `#686666 #595655 #544c46 #4a4746` |
| timber | `#664629 #483425 #382618 #271507` |
| deep shadow / outlines | `#170802 #261b15` |

**Decision (A2-01 review, 2026-07-02): the measured clusters are the verification
target for LIT RENDERS, not the generator's albedo input.** The baseline is itself a
fully lit, shaded render — the clusters above bake in the diorama's sun angle, ambient
occlusion and dark outline pass. Feeding them back into the pipeline as albedo would
double-apply lighting once the game's own rig lights the scene (terracotta roofs would
render near-black at noon). Therefore:

- `blender/scripts/generate_textures.py` builds the texture set from the **transcribed
  ramps below as albedo** — hardcoded exact hex values, fixed seeds, no image inputs —
  so the committed PNGs are byte-for-byte reproducible.
- Traceability to the baseline is enforced **at the render end**: the committed `day`
  previews of the kit are compared against `abbey-town-1.png` (silhouettes, material
  reads, and lit colors approaching the measured clusters) at every review of this kit.
- Expected albedo-vs-measured deltas are the lighting term, not palette drift: e.g.
  roof highlight `#d97b4a` (albedo) vs `#874934` (measured lit+shadowed), dirt mid
  `#b3925f` vs `#775636`. If a lit day preview drifts AWAY from the measured table,
  retune the transcribed ramps — the measured table wins that argument.
- The transcribed ramps also remain the only record for surfaces too small to cluster
  reliably (bells, flags, stained glass) — verify those against local pixel
  neighborhoods when possible.

## Transcribed palette (albedo ramps, hex: highlight / mid / shadow / deep)

| Ramp | Values | Used for |
|------|--------|----------|
| roof_terracotta | `#d97b4a #b5532e #7e3520 #5f2718` | all tiled roofs, ridge caps |
| roof_slate | `#8a8896 #6b6a78 #4f4e5c #3a3947` | church spire, tower accents |
| stone_cool | `#b8b7c0 #a8a7b0 #8b8a94 #62616c` | bell tower base, defensive walls |
| stone_warm | `#c2b49a #a89a80 #7d715c #5c5344` | church nave, cloister, paving edges |
| plaster | `#efe3c4 #e8d9b8 #d9c8a4 #b8a988` | half-timber infill |
| timber | `#8a5f3a #6b4a2f #543a24 #3d2a1a` | beams, doors, stalls, well frame |
| grass | `#7fb43c #5f9430 #47762a #35601f` | ground top, clumps |
| dirt_path | `#cfae7e #b3925f #8f7148 #6e5738` | roads, field soil |
| cliff_soil | `#6f4a33 #573828 #3f2719 #2e1c12` | diorama base sides |
| paving | `#b0aba0 #918c80 #6e6a5e #4f4c44` | plaza flagstones |
| bell_gold | `#f6d878 #e8b83a #c6952c #96701e` | bells, finials |
| flag_blue / flag_yellow | `#3e6bd9 #2f52a8` / `#e8c93a #c6a92e` | pennants (map-identity colors) |
| glass_stained | `#4a6fb0 #3f8f8a #a5453f` | church windows (lit warm `#e8c063` at night) |
| veg_field | `#c94a35 #d78f2e #4e8f3a` | tomato/pumpkin/leaf rows |

These ramps supersede the placeholder flat colors in the shared material library; the
library stays **closed** — same 17 names + the texture set below, colors retuned to match.

## Architectural vocabulary (per building, for the modelers)

**Bell tower (the game's signature structure)**: square cool-stone base 2 storeys with
narrow slit windows; jettied half-timber third storey; open belfry with 1–2 gold bells
under round arches; steep pyramidal terracotta roof; blue/yellow pennant on a pole at the
apex. Footprint ~2×2 tiles, height ~6 m equivalent.

**Abbey church**: warm-stone gothic nave with steep terracotta roof; pointed-arch stained
glass windows with tracery (2-tone blue/teal + red accents); stepped buttresses; ornate
west portal with archivolts; slender octagonal crossing spire in slate with gold finial;
stone cross finials on gable ends. Footprint ~4×2.

**Cloister**: low arcade wall attached to the church — round-arched open colonnade facing
an inner garth (grass + benches/tables where monks sit); terracotta lean-to roof; encloses
a courtyard ~3×2.

**Town hall / guildhall**: stone ground floor with arched door, two jettied half-timber
upper floors (dark diagonal bracing), steep multi-gabled terracotta roof with dormers;
massive external stone chimney with smoke; attached market-stall awnings (timber posts,
red/produce crates beneath). Footprint ~3×2.

**Field plot**: raised soil rows (dirt_path ramp) with alternating crops — tomato red
clusters, leafy greens, pumpkins; slightly irregular row ends. 2×1 or 2×2.

**Well**: circular cool-stone shaft, timber A-frame, small pitched terracotta roof,
rope + bucket; barrels and chickens nearby. 1×1.

**Ground kit**: dirt path segments (soft irregular edges over grass), flagstone plaza
patches, grass base tile with cliff-soil skirt (the diorama-slice look), scattered props:
barrels, crates, sacks, hay bundle, pennant pole.

**Composition proof**: `abbey_town_diorama_t1` is the first assembled Blender proof against
`abbey-town-1`: a square grass diorama with church/cloister on the left, enlarged
town-hall/tower mass on the right, plaza, dirt paths, garden, well, crates, barrels, smoke,
and pennant accents. It is a review target for silhouette/composition, not the final gameplay
scene layout.

## Texture extraction plan

The pixel-art surfaces are regular enough to regenerate as **tileable procedural
pixel-textures** (nearest-neighbor filtered, 64×64) rather than cropping the reference:
`tex_roof_tiles` (offset course pattern, per-tile 4-step ramp shading),
`tex_stone_blocks` (irregular coursed blocks, dark mortar lines), `tex_plaster_timber`
(plaster field — beams stay geometry), `tex_grass` (speckled 4-tone), `tex_dirt_path`,
`tex_paving` (flagstones), `tex_cliff_soil` (horizontal strata). Generated by script into
`blender/kits/materials/textures/`, wired into the shared material library, sampled with
'Closest' interpolation so the pixel-art read survives into Unity.

An opt-in AI candidate pass also exists for the owned baseline image:

```sh
GEMINI_API_KEY=... uv run --with-requirements tools/requirements-dev.txt python blender/scripts/generate_ai_textures.py
```

This calls Gemini/Nano Banana 2 with `docs/abbey-town-1.png` and writes review candidates to
`blender/kits/materials/textures_ai/` plus `ai_texture_manifest.json`. These files are not
automatically wired into `abbey_materials.py`: review them first, then deliberately promote
selected textures into the closed shared material library if they beat the deterministic
procedural set.

## Style rules distilled from the reference

- Every roof shows individual tile courses; ridges get lighter cap tiles.
- Stone reads as blocks (visible joints), never smooth.
- Half-timber bracing is diagonal-heavy and dark against pale plaster.
- One accent metal: gold (bells, finials). One accent cloth: blue/yellow pennants.
- Life props (chickens, dog, barrels, produce) sell the bucolic-day fantasy — model as
  separate tiny assets, never baked into buildings.
- Night variant of the same kit: windows glow warm (`#e8c063`), stained glass saturates,
  everything else drops toward the desaturated night grade (Unity-side).

## Asset budget note

The church and town hall are set-pieces; ART_BIBLE gains a **landmark** class
(max 6000 tris, max 4 materials) for them. Everything else stays within existing classes.

## Known closed-library compromises (accepted at A2-01 review; retune candidates)

The 17-material library stays closed, so a few reference surfaces map onto the nearest
existing material instead of getting a bespoke one:

- **Church crossing spire** uses `mat_wet_stone` (stained-glass blue) instead of the
  reference's slate. The church's 4-material landmark budget is spent on
  `mat_old_stone` / `mat_thatch` / `mat_wet_stone` (windows, portal) / `mat_sacred_gold`
  (finial); slate via `mat_iron` would be a fifth. Reads bluer than the reference —
  first candidate to retune if the landmark budget ever grows.
- **Pennant yellow tail** (`pennant_pole`, bell tower) uses `mat_sacred_gold`
  (metallic 0.9, faint emission) for what the reference defines as `flag_yellow` cloth;
  the library has no yellow cloth material.
- **Field-plot tomatoes** use `mat_thatch` (terracotta + `tex_roof_tiles`) as the
  closest non-emissive red; the tile courses are invisible at game camera distance.
- **`tex_cliff_soil`** is generated and committed but not yet wired to any material —
  reserved for the diorama grass-top/cliff-side ground tile asset (see "Ground kit").
