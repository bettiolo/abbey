# Imagen Validation Report

Reference target: `docs/abbey-town-1.png`.

## Retained Imagen Candidates

| Texture | File | Status | Note |
|---|---|---|---|
| `tex_wood_planks` | `tex_wood_planks_imagen.png` | CANDIDATE | Flat edge-to-edge pixel-art plank texture with no text artifacts found. |
| `tex_stone_blocks` | `tex_stone_blocks_imagen_v2_2.png` | REVIEW | Clean flat masonry retry; compare against deterministic baseline and abbey-town-1 stone palette before promotion. |
| `tex_cliff_soil` | `tex_cliff_soil_imagen.png` | REVIEW | Good side-cutaway read but includes grass/top terrain and has seam risk. |

## Deleted Rejected Imagen Files

Rejected Imagen files were removed from this folder and are listed in `imagen_texture_manifest.json` under `rejected_files_deleted`.

No retained candidate has been promoted into `blender/kits/materials/textures/` or wired into `abbey_materials.py`.
