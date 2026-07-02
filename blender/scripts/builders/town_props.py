"""Builder family: town-center props and ground — well, pennant pole, paving.

well_t1: round stone shaft with a rim, timber A-frame carrying a windlass with
a crank, hanging rope + bucket, small pitched terracotta roof.
Anchors: bucket (attach), water (water surface inside the shaft).

pennant_pole: slender dark pole, gold cap finial, and the settlement's
blue/yellow pennant (mat_wet_stone blue field + mat_sacred_gold tail — the
map-identity accent from docs/ART_REFERENCE_ABBEY.md).

paving_patch: 1x1 flagstone ground patch (tex_paving via mat_ash) with a few
individually raised stones and dirt at the worn edges.

Budgets: well 600/3, pennant 200/3, paving 300/3.
"""

from __future__ import annotations

import math

import bpy

from asset_framework import add_anchor, register_builder
from builders._shapes import add_box, add_cone, add_cylinder

DEG = math.radians


@register_builder("well_t1")
def build_well(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []

    # --- stone_shaft: round shaft + rim + dark water hole ---------------------
    objects.append(add_cylinder("shaft", "mat_old_stone", radius=0.3, depth=0.5,
                                vertices=8, location=(0.0, 0.0, 0.25)))
    objects.append(add_cylinder("rim", "mat_old_stone", radius=0.34, depth=0.1,
                                vertices=8, location=(0.0, 0.0, 0.55)))
    objects.append(add_cylinder("water_hole", "mat_dark_wood", radius=0.24, depth=0.06,
                                vertices=8, location=(0.0, 0.0, 0.58)))

    # --- a_frame: leaning timber legs + tie beam --------------------------------
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"leg_{i}", "mat_dark_wood", size=(0.09, 0.09, 1.3),
                    location=(sx * 0.3, 0.0, 0.85), rotation=(0.0, -sx * DEG(12.0), 0.0))
        )
    objects.append(add_box("tie_beam", "mat_dark_wood", size=(0.5, 0.08, 0.08),
                           location=(0.0, 0.0, 1.42)))

    # --- windlass: axle + crank ---------------------------------------------------
    objects.append(add_cylinder("axle", "mat_dark_wood", radius=0.05, depth=0.6,
                                vertices=6, location=(0.0, 0.0, 1.15),
                                rotation=(0.0, DEG(90.0), 0.0)))
    objects.append(add_box("crank_arm", "mat_dark_wood", size=(0.05, 0.05, 0.22),
                           location=(0.33, 0.0, 1.06)))
    objects.append(add_box("crank_knob", "mat_dark_wood", size=(0.12, 0.05, 0.05),
                           location=(0.37, 0.0, 0.96)))

    # --- rope + bucket -------------------------------------------------------------
    objects.append(add_box("rope", "mat_dark_wood", size=(0.035, 0.035, 0.36),
                           location=(0.0, 0.0, 0.97)))
    # Named bucket_body, not "bucket": that exact name is reserved for the anchor.
    objects.append(add_cylinder("bucket_body", "mat_dark_wood", radius=0.1, depth=0.16,
                                vertices=6, location=(0.0, 0.0, 0.72)))
    objects.append(add_box("bucket_handle", "mat_dark_wood", size=(0.03, 0.16, 0.03),
                           location=(0.0, 0.0, 0.81)))

    # --- pitched_roof: little terracotta cap over the frame -------------------------
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"roof_{i}", "mat_thatch", size=(0.9, 0.55, 0.06),
                    location=(0.0, sy * 0.21, 1.52), rotation=(-sy * DEG(40.0), 0.0, 0.0))
        )
    objects.append(add_box("ridge", "mat_thatch", size=(0.9, 0.1, 0.07),
                           location=(0.0, 0.0, 1.69)))

    # --- anchors ---------------------------------------------------------------------
    objects.append(add_anchor("bucket", (0.0, 0.0, 0.72), anchor_type="attach"))
    objects.append(add_anchor("water", (0.0, 0.0, 0.61), anchor_type="water"))
    return objects


@register_builder("pennant_pole")
def build_pennant_pole(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []

    # --- pole + gold cap finial ----------------------------------------------------
    objects.append(add_box("base", "mat_dark_wood", size=(0.2, 0.2, 0.12),
                           location=(0.0, 0.0, 0.06)))
    objects.append(add_cylinder("pole", "mat_dark_wood", radius=0.035, depth=2.45,
                                vertices=5, location=(0.0, 0.0, 1.345)))
    objects.append(add_cone("cap_finial", "mat_sacred_gold", radius=0.05, depth=0.12,
                            vertices=6, location=(0.0, 0.0, 2.63)))

    # --- pennant: blue field + pointed yellow tail (map-identity accent) ------------
    objects.append(add_box("pennant_blue", "mat_wet_stone", size=(0.55, 0.025, 0.24),
                           location=(0.31, 0.0, 2.42)))
    tail = add_cone("pennant_tail", "mat_sacred_gold", radius=0.12, depth=0.3,
                    vertices=4, location=(0.73, 0.0, 2.42), rotation=(0.0, DEG(90.0), 0.0))
    tail.scale = (tail.scale.x, tail.scale.y * 0.09, tail.scale.z)
    objects.append(tail)
    return objects


@register_builder("paving_patch")
def build_paving_patch(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []

    # --- flagstone_slab ---------------------------------------------------------------
    objects.append(add_box("slab", "mat_ash", size=(0.98, 0.98, 0.06), location=(0, 0, 0.03)))

    # --- raised_stones: a few stones proud of the surface -------------------------------
    for i, (x, y, w, d, yaw) in enumerate((
        (-0.25, 0.2, 0.26, 0.2, 11.0),
        (0.3, -0.15, 0.2, 0.24, -8.0),
        (0.12, 0.34, 0.18, 0.16, 22.0),
        (-0.32, -0.28, 0.22, 0.18, -15.0),
    )):
        objects.append(add_box(f"stone_{i}", "mat_ash", size=(w, d, 0.1),
                               location=(x, y, 0.05), rotation=(0.0, 0.0, DEG(yaw))))

    # --- dirt_fringe: worn edges showing dirt --------------------------------------------
    # sits proud of the slab (never coincident faces -> no z-fighting)
    for i, (x, y, w, d) in enumerate((
        (-0.2, -0.42, 0.3, 0.12),
        (0.41, 0.18, 0.12, 0.26),
        (0.3, -0.4, 0.2, 0.12),
        (-0.41, 0.3, 0.12, 0.2),
    )):
        objects.append(add_box(f"dirt_{i}", "mat_dirt", size=(w, d, 0.085),
                               location=(x, y, 0.0425)))
    return objects
