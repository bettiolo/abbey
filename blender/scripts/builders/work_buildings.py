"""Builder family: Phase-2 work buildings — woodcutter lean-to and guard post.

woodcutter_t1: ART_BIBLE says the building shows its function through visible
production props — so this is an open-fronted lean-to whose whole front IS the
work: a big stacked log pile under the slanted thatch roof, a chopping block
with a wedged iron axe out front, split wood scattered around it.
Anchors: work_slot (chopping block), deposit_point (log pile edge).

guard_post_t1: tall watch platform on four braced legs — railed deck, small
pyramidal thatch cap, leaning ladder, one warm hanging lantern (the light-
territory marker at night). Anchors: watch_slot (deck), flame (lantern).

Budgets: both building class, 2500 tris / 4 materials.
"""

from __future__ import annotations

import math
import random

import bpy

from asset_framework import add_anchor, register_builder
from builders._shapes import add_box, add_cone, add_cylinder


@register_builder("woodcutter_t1")
def build_woodcutter(spec: dict) -> list[bpy.types.Object]:
    rng = random.Random(41)
    objects: list[bpy.types.Object] = []
    roof_pitch = math.radians(16.0)

    # --- back_wall (+Y) and half side wall (-X): plank windbreak ---------------
    objects.append(
        add_box("back_wall", "mat_warm_wood", size=(2.5, 0.14, 1.35),
                location=(0.0, 0.95, 0.675))
    )
    objects.append(
        add_box("side_wall", "mat_warm_wood", size=(0.14, 1.9, 1.25),
                location=(-1.25, 0.0, 0.625))
    )

    # --- posts: chunky dark corners, front pair taller (roof slopes back) ------
    for i, (x, y, h) in enumerate((
        (-1.28, -0.95, 1.95), (1.28, -0.95, 1.95),
        (-1.28, 0.98, 1.55), (1.28, 0.98, 1.55),
    )):
        objects.append(
            add_box(f"post_{i}", "mat_dark_wood", size=(0.18, 0.18, h),
                    location=(x, y, h / 2.0))
        )

    # --- slanted_roof: one big thatch slab, high edge at the open front --------
    objects.append(
        add_box("slanted_roof", "mat_thatch", size=(2.95, 2.5, 0.14),
                location=(0.0, 0.05, 1.86),
                rotation=(roof_pitch, 0.0, 0.0))
    )
    objects.append(
        add_box("roof_edge_beam", "mat_dark_wood", size=(2.95, 0.16, 0.12),
                location=(0.0, -1.12, 2.16))
    )

    # --- log_pile: pyramid of fat logs under the roof, right side --------------
    log_r = 0.16
    rows = ((0.28, 4, log_r), (0.28 + log_r * 1.75, 3, log_r), (0.28 + log_r * 3.4, 2, log_r))
    for row_i, (z, count, r) in enumerate(rows):
        for j in range(count):
            y = (j - (count - 1) / 2.0) * (r * 2.06)
            objects.append(
                add_cylinder(f"log_{row_i}_{j}", "mat_warm_wood",
                             radius=r, depth=1.5 + rng.uniform(-0.12, 0.12), vertices=7,
                             location=(0.62 + rng.uniform(-0.03, 0.03), y * 0.9 + 0.12, z),
                             rotation=(0.0, math.radians(90.0), rng.uniform(-0.04, 0.04)))
            )
    # pile retaining stakes
    for i, y in enumerate((-0.55, 0.75)):
        objects.append(
            add_box(f"pile_stake_{i}", "mat_dark_wood", size=(0.10, 0.10, 1.0),
                    location=(1.42, y, 0.5))
        )

    # --- chopping_block + axe out front (-Y), left of the pile ------------------
    objects.append(
        add_cylinder("chopping_block", "mat_dark_wood",
                     radius=0.26, depth=0.55, vertices=8,
                     location=(-0.55, -1.38, 0.275))
    )
    objects.append(
        add_box("axe_handle", "mat_dark_wood", size=(0.07, 0.07, 0.62),
                location=(-0.42, -1.31, 0.78),
                rotation=(math.radians(18.0), math.radians(-32.0), 0.0))
    )
    objects.append(
        add_box("axe_head", "mat_iron", size=(0.24, 0.06, 0.16),
                location=(-0.58, -1.40, 0.60),
                rotation=(math.radians(18.0), math.radians(-32.0), 0.0))
    )

    # --- split_logs: wedges scattered by the block -------------------------------
    for i in range(4):
        objects.append(
            add_box(f"split_log_{i}", "mat_warm_wood",
                    size=(0.14, 0.34 + rng.uniform(-0.05, 0.05), 0.13),
                    location=(-1.0 + rng.uniform(-0.25, 0.7), -1.15 + rng.uniform(-0.2, 0.2), 0.065),
                    rotation=(0.0, 0.0, rng.uniform(0.0, math.tau)))
        )

    # --- anchors ------------------------------------------------------------------
    objects.append(add_anchor("work_slot", (-0.55, -1.8, 0.0), anchor_type="work"))
    objects.append(add_anchor("deposit_point", (0.62, -0.9, 0.0), anchor_type="work"))
    return objects


@register_builder("guard_post_t1")
def build_guard_post(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []
    leg = 0.95          # leg centers at +-leg
    deck_z = 2.3

    # --- legs: four chunky uprights ---------------------------------------------
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(
            add_box(f"leg_{i}", "mat_dark_wood", size=(0.20, 0.20, deck_z),
                    location=(sx * leg, sy * leg, deck_z / 2.0))
        )

    # --- cross_braces: X-bracing on the two camera-facing sides ------------------
    brace_len = 2.55
    for i, ang in enumerate((1, -1)):
        objects.append(  # -Y face
            add_box(f"brace_front_{i}", "mat_dark_wood", size=(brace_len, 0.09, 0.12),
                    location=(0.0, -leg, 1.15),
                    rotation=(0.0, ang * math.radians(38.0), 0.0))
        )
        objects.append(  # +X face
            add_box(f"brace_side_{i}", "mat_dark_wood", size=(0.09, brace_len, 0.12),
                    location=(leg, 0.0, 1.15),
                    rotation=(ang * math.radians(38.0), 0.0, 0.0))
        )

    # --- deck + rim beams ----------------------------------------------------------
    objects.append(
        add_box("deck", "mat_warm_wood", size=(2.25, 2.25, 0.14), location=(0.0, 0.0, deck_z))
    )
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"deck_beam_x_{i}", "mat_dark_wood", size=(2.35, 0.14, 0.18),
                    location=(0.0, sy * 1.1, deck_z - 0.12))
        )

    # --- railing: corner posts + top rails around the deck ---------------------------
    rail_z = deck_z + 0.52
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(
            add_box(f"rail_post_{i}", "mat_dark_wood", size=(0.11, 0.11, 0.62),
                    location=(sx * 1.05, sy * 1.05, deck_z + 0.31))
        )
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"rail_x_{i}", "mat_warm_wood", size=(2.2, 0.09, 0.09),
                    location=(0.0, sy * 1.05, rail_z))
        )
        objects.append(
            add_box(f"rail_y_{i}", "mat_warm_wood", size=(0.09, 2.2, 0.09),
                    location=(sy * 1.05, 0.0, rail_z))
        )

    # --- roof_cap: small thatch pyramid on four slim posts ----------------------------
    # (radius kept modest: a 45-degree-yawed cone's AABB inflates by sqrt(2))
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(
            add_box(f"roof_post_{i}", "mat_dark_wood", size=(0.10, 0.10, 1.05),
                    location=(sx * 0.55, sy * 0.55, deck_z + 0.60))
        )
    objects.append(
        add_cone("roof_cap", "mat_thatch", radius=0.88, depth=0.72, vertices=4,
                 location=(0.0, 0.0, deck_z + 1.48),
                 rotation=(0.0, 0.0, math.radians(45.0)))
    )
    objects.append(
        add_box("roof_finial", "mat_dark_wood", size=(0.09, 0.09, 0.30),
                location=(0.0, 0.0, deck_z + 1.95))
    )

    # --- ladder: leaning against the -Y front edge -------------------------------------
    lean = math.radians(10.0)
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"ladder_rail_{i}", "mat_warm_wood", size=(0.08, 0.08, 2.35),
                    location=(0.35 + sx * 0.19, -1.30, 1.16),
                    rotation=(lean, 0.0, 0.0))
        )
    for i in range(5):
        z = 0.35 + i * 0.42
        objects.append(
            add_box(f"ladder_rung_{i}", "mat_warm_wood", size=(0.42, 0.06, 0.06),
                    location=(0.35, -1.30 - (1.16 - z) * math.tan(lean), z))
        )

    # --- lantern: hanging off the camera-facing (+X, -Y) corner --------------------------
    lx, ly = 1.02, -1.02
    objects.append(
        add_box("lantern_arm", "mat_dark_wood", size=(0.09, 0.42, 0.09),
                location=(lx, ly + 0.16, deck_z + 0.72))
    )
    objects.append(
        add_box("lantern_top", "mat_dark_wood", size=(0.20, 0.20, 0.05),
                location=(lx, ly, deck_z + 0.60))
    )
    objects.append(
        add_box("lantern_glass", "mat_warm_window", size=(0.15, 0.15, 0.22),
                location=(lx, ly, deck_z + 0.46))
    )
    objects.append(
        add_box("lantern_base", "mat_dark_wood", size=(0.18, 0.18, 0.04),
                location=(lx, ly, deck_z + 0.33))
    )

    # --- anchors ---------------------------------------------------------------------------
    objects.append(add_anchor("watch_slot", (0.0, 0.0, deck_z + 0.07), anchor_type="work"))
    objects.append(add_anchor("flame", (lx, ly, deck_z + 0.46), anchor_type="light"))
    return objects
