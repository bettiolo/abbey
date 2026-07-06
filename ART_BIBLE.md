# Art Bible — The Abbey at World's End

Real 3D assets, art-directed like a 2D strategy diorama.

## Camera contract (never break this)

- Orthographic camera, fixed isometric angle: **pitch 30°, yaw 45°**, no free rotation
  in prototype.
- Fixed sun direction (matches camera-relative light so silhouettes always read).
- Zoom changes orthographic size only.

## Core visual rules

- Low/mid-poly stylised assets, painterly materials, strong silhouettes.
- Buildings readable from zoomed-out view; every building shows its function through
  visible production props (log pile at the woodcutter, crates at storage).
- 2D UI overlay only.
- Shader-driven desaturation for night, fear, winter, and nightmare zones.
- Every asset must remain readable in a grayscale preview — this is a hard validation check.
- **Seasonal day-mood arc.** The daytime landscape is a shrinking gift (GAME_DESIGN.md §7,
  Food and hunger): spring/summer read **lush and rich** — saturated greens, flower meadows,
  berry bushes and forageable growth, a genuinely *happy* bucolic day. Autumn desaturates and
  thins that abundance (turning colour, bare beds, emptied larder); winter reaches the full
  desaturated cold above, where the free landscape is gone and only the built settlement
  carries the eye. The day should visibly *degrade across the seasons*, so winter feels earned
  rather than merely stated.

## Shape language

**Buildings**: large roofs, chunky walls, exaggerated chimneys, clear doors, visible
production props, readable silhouette at distance.

**Abbey**: old stone, broken arches, bell tower, crypt, cloister, relic chamber, candle
clusters, graves. Both repaired and ruined states.

**Shipwreck**: broken hull, crates, torn sails, rope, barrels, wet planks, bell fragment,
salvage piles.

**Night assets**: silhouettes first; glowing eyes sparingly; thin limbs; unnatural posture;
shapes visible at the edge of light.

**The Black Hound**: large, wounded, memorable — even as a placeholder the silhouette
matters most.

## Shared material library (closed set — do not add without updating this file)

```
mat_warm_wood   mat_dark_wood   mat_old_stone   mat_wet_stone
mat_thatch      mat_canvas      mat_iron        mat_warm_window
mat_sacred_gold mat_bone        mat_snow        mat_ash
mat_nightmare_black             mat_flame       mat_ember
mat_foliage     mat_dirt
```

Implemented once in `blender/scripts/abbey_materials.py`. Assets reference materials by
name only. Max 4 materials per asset.

Materials may sample the shared tileable pixel-texture set (`tex_roof_tiles`,
`tex_stone_blocks`, `tex_plaster_timber`, `tex_grass`, `tex_dirt_path`, `tex_paving`,
`tex_cliff_soil` in `blender/kits/materials/textures/`, 'Closest' interpolation), whose
palette ramps are defined in [docs/ART_REFERENCE_ABBEY.md](docs/ART_REFERENCE_ABBEY.md) —
the quantified transcription of the user-provided pixel-art baseline renders.

## Asset budgets

| Class | Max triangles | Max materials |
|-------|--------------|---------------|
| Prop | 800 | 3 |
| Building | 2500 | 4 |
| Character/beast placeholder | 1500 | 3 |
| Terrain feature | 1200 | 3 |
| Landmark (church, town hall) | 6000 | 4 |

## Per-asset deliverables

Every generated asset ships: `.glb`, `.meta.json`, and four isometric previews
(`day`, `night`, `winter`, `grayscale`). Pivot at **center-bottom**. Named anchors
(smoke, door, worker slots…) as glTF empties. Footprint must fit declared tile size.

## Moral visual language (Phase 3+)

The abbey physically reflects the settlement's soul: Sanctuary (warm light, gardens),
Fortress (walls, iron, watch posts), Famine (smoke, bone charms, locked stores),
Cult (candles everywhere, kneeling villagers), Broken (flicker, ruin, whispers).
Screenshots must tell this story without UI.
