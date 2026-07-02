"""Builder family: sacred small structures — candle shrine, grave marker,
infirmary corner.

candle_shrine_t1: stepped stone plinth, back slab with a sheltered niche
holding a gold icon, a huddled cluster of fat pale candles with warm flame
tips. The camp's first sacred light. Anchors: flame (light), pray_slot (work).

infirmary_corner_t1: two low plank walls in an L, canvas awning over the
corner, a raised cot with a pale bedroll, a ground bedroll, stool + bone bowl.
Sickness is visible, not abstract. Anchors: bed (rest), work_slot (work).

grave_marker: one fresh grave — leaning stone cross at the head of a low
earth mound, field stones at its foot. The cost of a bad night, in one prop.

Budgets: shrine 800/4, infirmary 2500/4, grave 300/3.
"""

from __future__ import annotations

import math
import random

import bpy

from asset_framework import add_anchor, register_builder
from builders._shapes import add_box, add_cone, add_cylinder, add_icosphere


@register_builder("candle_shrine_t1")
def build_candle_shrine(spec: dict) -> list[bpy.types.Object]:
    rng = random.Random(53)
    objects: list[bpy.types.Object] = []

    # --- stepped_plinth ---------------------------------------------------------
    objects.append(
        add_box("plinth_low", "mat_old_stone", size=(1.25, 1.25, 0.22),
                location=(0.0, 0.0, 0.11))
    )
    objects.append(
        add_box("plinth_high", "mat_old_stone", size=(0.95, 0.95, 0.24),
                location=(0.0, 0.08, 0.34))
    )

    # --- back_slab + niche (two cheeks + hood make the recess) -------------------
    objects.append(
        add_box("back_slab", "mat_old_stone", size=(0.85, 0.24, 1.15),
                location=(0.0, 0.32, 1.03))
    )
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"niche_cheek_{i}", "mat_old_stone", size=(0.16, 0.30, 0.62),
                    location=(sx * 0.335, 0.18, 1.05))
        )
    objects.append(
        add_box("niche_hood", "mat_old_stone", size=(0.85, 0.34, 0.20),
                location=(0.0, 0.18, 1.46))
    )
    # little stone gable on top
    objects.append(
        add_cone("niche_gable", "mat_old_stone", radius=0.5, depth=0.34, vertices=4,
                 location=(0.0, 0.22, 1.73))
    )

    # --- gold_icon: small shining icon inside the niche ---------------------------
    objects.append(
        add_box("icon_body", "mat_sacred_gold", size=(0.22, 0.06, 0.34),
                location=(0.0, 0.24, 1.02))
    )
    objects.append(
        add_cone("icon_halo", "mat_sacred_gold", radius=0.10, depth=0.10, vertices=6,
                 location=(0.0, 0.24, 1.26))
    )

    # --- candle_cluster: fat pale candles, uneven heights, on the high step -------
    candles = (
        (-0.24, -0.16, 0.30, 0.075), (0.02, -0.26, 0.42, 0.09),
        (0.26, -0.14, 0.24, 0.07), (-0.05, -0.02, 0.55, 0.10),
        (0.24, -0.34, 0.34, 0.065), (-0.30, -0.34, 0.20, 0.06),
    )
    for i, (x, y, h, r) in enumerate(candles):
        objects.append(
            add_cylinder(f"candle_{i}", "mat_bone", radius=r, depth=h, vertices=7,
                         location=(x, y, 0.46 + h / 2.0),
                         rotation=(rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03), 0.0))
        )
        # --- flames: tiny warm tips ------------------------------------------------
        objects.append(
            add_cone(f"flame_{i}", "mat_flame", radius=0.035, depth=0.10, vertices=5,
                     location=(x, y, 0.46 + h + 0.05))
        )

    # --- anchors -------------------------------------------------------------------
    objects.append(add_anchor("flame", (-0.05, -0.02, 1.1), anchor_type="light"))
    objects.append(add_anchor("pray_slot", (0.0, -0.62, 0.0), anchor_type="work"))
    return objects


@register_builder("infirmary_corner_t1")
def build_infirmary_corner(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []

    # --- l_walls: low plank walls along +Y (back) and -X (left) ------------------
    objects.append(
        add_box("wall_back", "mat_warm_wood", size=(2.7, 0.13, 1.15),
                location=(0.0, 1.32, 0.575))
    )
    objects.append(
        add_box("wall_left", "mat_warm_wood", size=(0.13, 2.7, 1.15),
                location=(-1.32, 0.0, 0.575))
    )
    for i, (x, y) in enumerate(((-1.32, 1.32), (1.30, 1.32), (-1.32, -1.30))):
        objects.append(
            add_box(f"wall_post_{i}", "mat_dark_wood", size=(0.16, 0.16, 1.3),
                    location=(x, y, 0.65))
        )

    # --- awning: canvas sheet over the cot corner only, so the iso camera
    # still sees the beds (the sickness must be visible) ---------------------------
    for i, (x, y, h) in enumerate(((1.15, 0.15, 1.55), (1.15, 1.25, 1.65))):
        objects.append(
            add_box(f"awning_pole_{i}", "mat_dark_wood", size=(0.11, 0.11, h),
                    location=(x, y, h / 2.0))
        )
    objects.append(
        add_box("awning", "mat_canvas", size=(2.1, 1.75, 0.06),
                location=(0.25, 0.72, 1.62),
                rotation=(math.radians(-9.0), math.radians(-14.0), 0.0))
    )

    # --- cot: raised frame + pale bedroll + pillow --------------------------------
    objects.append(
        add_box("cot_frame", "mat_dark_wood", size=(1.7, 0.8, 0.16),
                location=(0.15, 0.85, 0.36))
    )
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(
            add_box(f"cot_leg_{i}", "mat_dark_wood", size=(0.10, 0.10, 0.30),
                    location=(0.15 + sx * 0.76, 0.85 + sy * 0.33, 0.15))
        )
    objects.append(
        add_box("cot_bedroll", "mat_canvas", size=(1.6, 0.7, 0.22),
                location=(0.15, 0.85, 0.55))
    )
    objects.append(
        add_box("cot_pillow", "mat_canvas", size=(0.34, 0.55, 0.14),
                location=(-0.52, 0.85, 0.68))
    )
    # sick-blanket lump on the cot (someone is under it)
    objects.append(
        add_box("blanket_lump", "mat_canvas", size=(0.85, 0.5, 0.18),
                location=(0.35, 0.85, 0.70), rotation=(0.0, 0.0, 0.05))
    )

    # --- ground bedroll: second patient overflow ----------------------------------
    objects.append(
        add_box("ground_bedroll", "mat_canvas", size=(1.55, 0.62, 0.18),
                location=(0.0, -0.75, 0.09), rotation=(0.0, 0.0, math.radians(-8.0)))
    )
    objects.append(
        add_box("ground_pillow", "mat_canvas", size=(0.30, 0.5, 0.12),
                location=(-0.62, -0.68, 0.20), rotation=(0.0, 0.0, math.radians(-8.0)))
    )

    # --- stool + bone bowl between the beds -----------------------------------------
    objects.append(
        add_cylinder("stool", "mat_dark_wood", radius=0.18, depth=0.38, vertices=7,
                     location=(0.95, 0.0, 0.19))
    )
    objects.append(
        add_cylinder("bowl", "mat_bone", radius=0.13, depth=0.09, vertices=7,
                     location=(0.95, 0.0, 0.43))
    )
    objects.append(
        add_box("rag", "mat_canvas", size=(0.22, 0.16, 0.03),
                location=(0.99, 0.14, 0.395), rotation=(0.0, 0.0, 0.5))
    )

    # --- anchors ---------------------------------------------------------------------
    objects.append(add_anchor("bed", (0.15, 0.85, 0.65), anchor_type="rest"))
    objects.append(add_anchor("work_slot", (0.95, -0.35, 0.0), anchor_type="work"))
    return objects


@register_builder("grave_marker")
def build_grave_marker(spec: dict) -> list[bpy.types.Object]:
    rng = random.Random(59)
    objects: list[bpy.types.Object] = []

    # --- earth_mound: fresh-turned soil, slightly sunken at the foot --------------
    objects.append(
        add_icosphere("earth_mound", "mat_dirt", radius=0.42, subdivisions=1,
                      location=(0.0, -0.12, 0.02),
                      scale=(0.85, 1.35, 0.35))
    )

    # --- cross: leaning stone cross at the head (+Y) -------------------------------
    lean = math.radians(-8.0)
    objects.append(
        add_box("cross_post", "mat_old_stone", size=(0.16, 0.13, 0.95),
                location=(0.0, 0.42, 0.48),
                rotation=(lean, 0.0, 0.0))
    )
    objects.append(
        add_box("cross_arm", "mat_old_stone", size=(0.52, 0.12, 0.15),
                location=(0.0, 0.435, 0.70),
                rotation=(lean, 0.0, 0.0))
    )
    objects.append(
        add_box("cross_base", "mat_old_stone", size=(0.34, 0.26, 0.18),
                location=(0.0, 0.40, 0.09))
    )

    # --- field_stones: settled against the mound edge ------------------------------
    for i, (x, y, r) in enumerate(((0.30, -0.42, 0.09), (-0.26, -0.50, 0.07), (0.10, -0.62, 0.06))):
        objects.append(
            add_icosphere(f"field_stone_{i}", "mat_old_stone", radius=r, subdivisions=1,
                          location=(x, y, r * 0.6),
                          rotation=(rng.uniform(-0.4, 0.4), rng.uniform(-0.4, 0.4), rng.uniform(0.0, math.tau)),
                          scale=(1.0, 0.85, 0.7))
        )

    return objects
