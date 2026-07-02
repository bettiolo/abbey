"""Export the generated asset: GLB (+Y up, anchors included) and .blend save.

- GLB goes to  blender/generated/glb/<id>.glb        (committed, Unity imports it)
- BLEND goes to blender/generated/blend/<id>.blend   (gitignored build artifact)

bpy 5.x note: ``bpy.ops.export_scene.gltf`` with ``export_yup=True`` and
``use_selection=True`` — verified against the 5.0 operator RNA.
"""

from __future__ import annotations

import sys
from pathlib import Path

_SCRIPTS_DIR = str(Path(__file__).resolve().parent)
if _SCRIPTS_DIR not in sys.path:
    sys.path.insert(0, _SCRIPTS_DIR)

import bpy

import asset_framework as fw


def _select_only(objects) -> None:
    for obj in bpy.context.scene.objects:
        obj.select_set(False)
    for obj in objects:
        obj.select_set(True)
    if objects:
        bpy.context.view_layer.objects.active = objects[0]


def export_glb(root: bpy.types.Object, asset_id: str) -> Path:
    """Export the asset hierarchy (root, meshes, anchor empties, collision) to GLB."""
    fw.GLB_DIR.mkdir(parents=True, exist_ok=True)
    glb_path = fw.GLB_DIR / f"{asset_id}.glb"
    _select_only(fw.iter_asset_objects(root))
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_extras=True,   # keeps the 'abbey_anchor' custom props on empties
        export_apply=True,
        export_cameras=False,
        export_lights=False,
        export_animations=False,
    )
    return glb_path


def save_blend(asset_id: str) -> Path:
    """Save the whole working scene as blender/generated/blend/<id>.blend."""
    fw.BLEND_DIR.mkdir(parents=True, exist_ok=True)
    blend_path = fw.BLEND_DIR / f"{asset_id}.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
    return blend_path


def export_asset(root: bpy.types.Object, asset_id: str) -> dict:
    """Export GLB + save blend; returns {'glb': path, 'blend': path}."""
    glb_path = export_glb(root, asset_id)
    blend_path = save_blend(asset_id)
    return {"glb": glb_path, "blend": blend_path}
