"""Builder family: Phase-3 renewable-economy production buildings (P3-04).

The ART_BIBLE rule is that a building reads through its visible production props,
so each of these greybox structures IS its function:

pasture_t1: a fenced grazing paddock — a post-and-rail timber fence ringing a
grassy plot, a long water trough and a round hay bale. Anchor: work_slot (gate).

charcoal_kiln_t1: a squat domed earthen kiln — a round stone-and-earth mound with
a stubby chimney and a dark stoke door glowing with embers, a small stacked-wood
pile and a heap of black charcoal beside it. Anchor: work_slot (the stoke door).

smithy_t1: an open-fronted forge shed — chunky timber posts under a slanted thatch
roof over a back wall, a stone forge with a tall chimney, an iron anvil on a
timber block out front and a wooden quench barrel. Anchor: work_slot (the anvil).

Budgets: all building class, 2500 tris / 4 materials, closed shared library only.
"""

from __future__ import annotations

import math
import random

import bpy

from asset_framework import add_anchor, register_builder
from builders._shapes import add_box, add_cone, add_cylinder, add_icosphere


@register_builder("pasture_t1")
def build_pasture(spec: dict) -> list[bpy.types.Object]:
    rng = random.Random(74)
    objects: list[bpy.types.Object] = []
    half = 1.9  # fence line for a 4x4 footprint

    # --- ground + grass: flat dirt patch with a grassy top slab -----------------
    objects.append(
        add_box("ground", "mat_dirt", size=(3.9, 3.9, 0.05), location=(0.0, 0.0, 0.025))
    )
    objects.append(
        add_box("grass", "mat_foliage", size=(3.7, 3.7, 0.06), location=(0.0, 0.0, 0.07))
    )
    # a few grass tufts for texture
    for i in range(6):
        x = rng.uniform(-1.4, 1.4)
        y = rng.uniform(-1.4, 1.4)
        objects.append(
            add_icosphere(f"tuft_{i}", "mat_foliage", radius=0.16,
                          location=(x, y, 0.12), scale=(1.0, 1.0, 0.55))
        )

    # --- fence_posts: dark uprights at corners and side midpoints ---------------
    post_xy = []
    for sx in (-1, 0, 1):
        for sy in (-1, 0, 1):
            if sx == 0 and sy == 0:
                continue
            post_xy.append((sx * half, sy * half))
    for i, (x, y) in enumerate(post_xy):
        objects.append(
            add_box(f"fence_post_{i}", "mat_dark_wood", size=(0.14, 0.14, 0.9),
                    location=(x, y, 0.45))
        )

    # --- fence_rails: two warm-wood rails per side (a gap left on -Y for the gate)
    for i, z in enumerate((0.42, 0.74)):
        objects.append(  # +Y
            add_box(f"rail_pY_{i}", "mat_warm_wood", size=(3.8, 0.08, 0.1),
                    location=(0.0, half, z))
        )
        objects.append(  # +X
            add_box(f"rail_pX_{i}", "mat_warm_wood", size=(0.08, 3.8, 0.1),
                    location=(half, 0.0, z))
        )
        objects.append(  # -X
            add_box(f"rail_nX_{i}", "mat_warm_wood", size=(0.08, 3.8, 0.1),
                    location=(-half, 0.0, z))
        )
        # -Y side: two short rails flanking a central gate gap
        objects.append(
            add_box(f"rail_nY_l_{i}", "mat_warm_wood", size=(1.3, 0.08, 0.1),
                    location=(-1.2, -half, z))
        )
        objects.append(
            add_box(f"rail_nY_r_{i}", "mat_warm_wood", size=(1.3, 0.08, 0.1),
                    location=(1.2, -half, z))
        )

    # --- water_trough: warm-wood box on dark legs, along the +X fence -----------
    objects.append(
        add_box("water_trough", "mat_warm_wood", size=(0.4, 1.6, 0.32),
                location=(1.35, 0.0, 0.28))
    )
    for i, y in enumerate((-0.6, 0.6)):
        objects.append(
            add_box(f"trough_leg_{i}", "mat_dark_wood", size=(0.12, 0.12, 0.22),
                    location=(1.35, y, 0.11))
        )

    # --- hay_bale: fat foliage roll in the +Y/-X corner -------------------------
    objects.append(
        add_cylinder("hay_bale", "mat_foliage", radius=0.42, depth=0.7, vertices=10,
                     location=(-1.35, 1.35, 0.42),
                     rotation=(0.0, math.radians(90.0), 0.0))
    )

    objects.append(add_anchor("work_slot", (0.0, -half - 0.1, 0.0), anchor_type="work"))
    return objects


@register_builder("charcoal_kiln_t1")
def build_charcoal_kiln(spec: dict) -> list[bpy.types.Object]:
    rng = random.Random(29)
    objects: list[bpy.types.Object] = []

    # --- kiln_dome: squat stone-and-earth mound (cone tapered to a flat top) -----
    objects.append(
        add_cone("kiln_dome", "mat_old_stone", radius=0.72, radius_top=0.26,
                 depth=1.2, vertices=10, location=(0.0, 0.05, 0.6))
    )
    objects.append(  # base skirt ring
        add_cylinder("kiln_base", "mat_old_stone", radius=0.78, depth=0.22, vertices=10,
                     location=(0.0, 0.05, 0.11))
    )

    # --- chimney: stubby stone stack off the crown ------------------------------
    objects.append(
        add_box("chimney", "mat_old_stone", size=(0.22, 0.22, 0.42),
                location=(0.0, 0.05, 1.42))
    )

    # --- stoke_door: dark opening at the front with an ember glow ----------------
    objects.append(
        add_box("stoke_door", "mat_dark_wood", size=(0.34, 0.12, 0.4),
                location=(0.0, -0.66, 0.3))
    )
    objects.append(
        add_box("ember_glow", "mat_ember", size=(0.22, 0.06, 0.24),
                location=(0.0, -0.72, 0.28))
    )

    # --- wood_stack: split logs stacked on the +X side --------------------------
    for i in range(4):
        z = 0.12 + (i // 2) * 0.2
        y = -0.18 + (i % 2) * 0.36
        objects.append(
            add_cylinder(f"stack_log_{i}", "mat_dark_wood", radius=0.1, depth=0.6,
                         vertices=6, location=(0.78, y, z),
                         rotation=(math.radians(90.0), 0.0, rng.uniform(-0.05, 0.05)))
        )

    # --- charcoal_heap: ashy black pile on the -X side --------------------------
    objects.append(
        add_icosphere("charcoal_heap", "mat_ash", radius=0.28,
                      location=(-0.74, -0.1, 0.16), scale=(1.1, 1.0, 0.6))
    )
    for i in range(3):
        objects.append(
            add_icosphere(f"charcoal_lump_{i}", "mat_ash", radius=0.1,
                          location=(-0.7 + rng.uniform(-0.12, 0.12),
                                    -0.42 + rng.uniform(-0.1, 0.1), 0.09))
        )

    objects.append(add_anchor("work_slot", (0.0, -0.95, 0.0), anchor_type="work"))
    return objects


@register_builder("smithy_t1")
def build_smithy(spec: dict) -> list[bpy.types.Object]:
    rng = random.Random(53)
    objects: list[bpy.types.Object] = []
    roof_pitch = math.radians(15.0)

    # --- back_wall (+Y) plank windbreak -----------------------------------------
    objects.append(
        add_box("back_wall", "mat_warm_wood", size=(2.6, 0.14, 1.5),
                location=(0.0, 1.28, 0.75))
    )

    # --- posts: chunky corners, front pair taller (roof slopes to the back) ------
    for i, (x, y, h) in enumerate((
        (-1.32, -1.28, 2.15), (1.32, -1.28, 2.15),
        (-1.32, 1.28, 1.75), (1.32, 1.28, 1.75),
    )):
        objects.append(
            add_box(f"post_{i}", "mat_warm_wood", size=(0.18, 0.18, h),
                    location=(x, y, h / 2.0))
        )

    # --- slanted_roof: one big thatch slab, high edge at the open front ----------
    objects.append(
        add_box("slanted_roof", "mat_thatch", size=(2.95, 2.85, 0.15),
                location=(0.0, 0.0, 2.15),
                rotation=(roof_pitch, 0.0, 0.0))
    )

    # --- forge: stone hearth block with a tall chimney at the back --------------
    objects.append(
        add_box("forge", "mat_old_stone", size=(1.0, 0.8, 0.85),
                location=(0.55, 0.95, 0.425))
    )
    objects.append(
        add_box("forge_hearth", "mat_iron", size=(0.5, 0.4, 0.12),
                location=(0.55, 0.78, 0.9))
    )
    objects.append(
        add_box("chimney", "mat_old_stone", size=(0.5, 0.5, 1.9),
                location=(0.55, 1.15, 1.35))
    )

    # --- anvil on a timber block, out front (-Y) --------------------------------
    objects.append(
        add_cylinder("anvil_block", "mat_warm_wood", radius=0.26, depth=0.55,
                     vertices=8, location=(-0.6, -0.7, 0.275))
    )
    objects.append(
        add_box("anvil_body", "mat_iron", size=(0.5, 0.22, 0.16),
                location=(-0.6, -0.7, 0.63))
    )
    objects.append(
        add_box("anvil_horn", "mat_iron", size=(0.22, 0.16, 0.1),
                location=(-0.86, -0.7, 0.63))
    )

    # --- quench_barrel: wooden barrel by the anvil ------------------------------
    objects.append(
        add_cylinder("quench_barrel", "mat_warm_wood", radius=0.28, depth=0.6,
                     vertices=10, location=(-1.05, 0.2, 0.3))
    )

    # --- scattered offcuts for read ---------------------------------------------
    for i in range(3):
        objects.append(
            add_box(f"offcut_{i}", "mat_iron",
                    size=(0.12, 0.28 + rng.uniform(-0.04, 0.04), 0.05),
                    location=(-0.2 + rng.uniform(-0.3, 0.3), -1.0 + rng.uniform(-0.15, 0.15),
                              0.03),
                    rotation=(0.0, 0.0, rng.uniform(0.0, math.tau)))
        )

    objects.append(add_anchor("work_slot", (-0.6, -1.1, 0.0), anchor_type="work"))
    return objects
