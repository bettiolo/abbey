# Art reference: Abbey / Town Center / Field baseline

Source: user-provided pixel-art isometric diorama renders, committed in this folder:

- **`docs/abbey-town-1.png` — THE BASELINE** (user decision 2026-07-02). All palette
  extraction and silhouette matching targets this file first.
- `docs/abbey-town-2.png` … `abbey-town-4.png` — same complex, other rotations.
- `docs/abbey-town-ruin-1.png` — **ruined state** of the same complex: the reference for
  damaged/ruined asset variants (abbey_gate_ruined, bell_tower_ruined, broken walls) and
  for the Broken-Abbey moral variant.

Because the actual pixels are in-repo, palette work must be **derived from the baseline
image programmatically** (deterministic sampling/clustering of `abbey-town-1.png`, masking
the baked checkerboard background: near-greyscale pixels with r≈g≈b > 170). The hand
ramps below are the calibration record; measured values win over transcribed ones.

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

## Measured palette (sampled from `abbey-town-1.png`, checkerboard masked — AUTHORITATIVE)

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

The generator must re-derive these at build time from the PNG itself (fixed seed, fixed
sampling) so the textures stay traceable to the baseline. The transcribed ramps below are
kept for surfaces too small to cluster reliably (bells, flags, stained glass) — verify
them against local pixel neighborhoods when possible.

## Transcribed palette (hex, pixel-art ramps: highlight / mid / shadow / deep)

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

## Texture extraction plan

The pixel-art surfaces are regular enough to regenerate as **tileable procedural
pixel-textures** (nearest-neighbor filtered, 64×64) rather than cropping the reference:
`tex_roof_tiles` (offset course pattern, per-tile 4-step ramp shading),
`tex_stone_blocks` (irregular coursed blocks, dark mortar lines), `tex_plaster_timber`
(plaster field — beams stay geometry), `tex_grass` (speckled 4-tone), `tex_dirt_path`,
`tex_paving` (flagstones), `tex_cliff_soil` (horizontal strata). Generated by script into
`blender/kits/materials/textures/`, wired into the shared material library, sampled with
'Closest' interpolation so the pixel-art read survives into Unity.

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
