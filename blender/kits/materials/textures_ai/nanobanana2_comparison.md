# Nano Banana 2 Comparison

Reference target: `docs/abbey-town-1.png`.

Written reference checked: `docs/ART_REFERENCE_ABBEY.md` matches the baseline image at the main art-direction level: square grass diorama, exposed cliff soil sides, abbey church/cloister on the left, town hall and bell tower on the right, terracotta roof courses, chunky stone blocks, pale half-timber plaster, tan paths, warm-gray paving, field rows, well, barrels/crates, smoke, and blue/yellow pennants. The measured palette table was not re-sampled in this pass because the environment lacks image libraries, but the document correctly treats those values as lit-render targets and the transcribed ramps as albedo inputs.

## Retained Variants

| Texture | Retained file | Status | Comparison to `abbey-town-1.png` |
|---|---|---|---|
| `tex_roof_tiles` | `tex_roof_tiles_nb2_v2.jpg` | CANDIDATE | Strong terracotta match; rounded tiles and light ridge rows resemble the reference roofs. |
| `tex_plaster_timber` | `tex_plaster_timber_nb2_v1.jpg` | CANDIDATE | Pale warm plaster with quiet mottling; good match for half-timber infill. |
| `tex_grass` | `tex_grass_nb2_v2.jpg` | CANDIDATE | Clean pixel-art grass; olive greens and small speckles are closer than v1. |
| `tex_dirt_path` | `tex_dirt_path_nb2_v2.jpg` | CANDIDATE | Tan packed dirt with soft ruts and pebbles; no grass border. |
| `tex_paving` | `tex_paving_nb2_v2.jpg` | REVIEW | Good irregular flagstone shapes, but dark joints are heavier than the reference plaza. |
| `tex_stained_glass` | `tex_stained_glass_nb2_v1.jpg` | CANDIDATE | Gothic tracery and blue/teal/red/gold panes match the church-window vocabulary better than v2. |

## Deleted Rejected Variants

| File | Reason |
|---|---|
| `tex_roof_tiles_nb2_v1.png` | More blocky rectangular roof read; v2 better matches rounded terracotta courses. |
| `tex_plaster_timber_nb2_v2.png` | Usable but less restrained than v1, with brighter patching. |
| `tex_grass_nb2_v1.png` | Too busy and saturated compared with the quieter grass in the reference. |
| `tex_dirt_path_nb2_v1.png` | Obvious radial/X pattern, poor fit for natural path material. |
| `tex_paving_nb2_v1.png` | Cooler and flatter than v2; not as close to the warm reference paving. |
| `tex_stained_glass_nb2_v2.png` | Reads as ornamental wallpaper rather than gothic window-pane material. |

No retained candidate has been promoted into `blender/kits/materials/textures/` or wired into `abbey_materials.py`.
