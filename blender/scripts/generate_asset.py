"""Generate one asset from its spec: reset scene, run builder, normalize pivot.

Usage from code::

    from generate_asset import generate_asset
    result = generate_asset("campfire_t1")
    result["root"]          # root empty named after the asset id
    result["spec"]          # parsed spec dict

The heavy lifting (materials, registry, pivot, collision) lives in
``abbey_materials.py`` and ``asset_framework.py``. Builders self-register when
the ``builders`` package is imported.
"""

from __future__ import annotations

import sys
from pathlib import Path

_SCRIPTS_DIR = str(Path(__file__).resolve().parent)
if _SCRIPTS_DIR not in sys.path:
    sys.path.insert(0, _SCRIPTS_DIR)

import bpy

import asset_framework as fw
import builders  # noqa: F401  (import registers all builders)


def generate_asset(asset_id: str) -> dict:
    """Build *asset_id* into a fresh scene and return measurements.

    Steps: reset scene -> load spec -> run registered builder -> single root
    empty -> center-bottom pivot -> collision proxy -> measure.
    """
    spec = fw.load_spec(asset_id)
    builder = fw.get_builder(asset_id)

    fw.reset_scene()
    created = builder(spec)
    if not created:
        raise RuntimeError(f"Builder for '{asset_id}' returned no objects")

    # Builders may or may not create the root themselves.
    root = bpy.data.objects.get(asset_id)
    if root is None or root.type != "EMPTY":
        root = fw.make_root(asset_id, created)
    else:
        for obj in created:
            if obj.parent is None and obj is not root:
                obj.parent = root

    bpy.context.view_layer.update()
    fw.apply_center_bottom_pivot(root)
    # Consistent-density box UVs for the shared tileable pixel textures
    # (materials with an Image Texture node sample the 'UVMap' layer).
    fw.apply_box_uvs(root)

    if bpy.data.objects.get(f"{asset_id}{fw.COLLISION_SUFFIX}") is None:
        fw.add_collision_proxy(root, asset_id)
    bpy.context.view_layer.update()

    return {
        "id": asset_id,
        "spec": spec,
        "root": root,
        "objects": fw.iter_asset_objects(root),
        "dimensions": fw.measure_dimensions(root),
        "triangle_count": fw.count_triangles(root),
        "materials": fw.used_materials(root),
        "anchors": [
            {"name": o.name, "type": o.get("abbey_anchor", "generic")}
            for o in fw.anchor_objects(root)
        ],
    }
