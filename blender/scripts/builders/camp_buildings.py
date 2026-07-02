"""Builder family: first-camp structures — storage pile, shelter, lantern post.

storage_pile_t1: the building IS its production prop (ART_BIBLE): a low pallet
stacked with chunky crates and fat canvas sacks, rope lashing over the top
crate. Anchor: deposit_point (work) at the open front edge.

shelter_t1: chunky one-room hut dominated by a big overhanging sloped thatch
roof; stout dark door, one warm glowing window, stubby wooden smoke stack.
Anchors: door (door), smoke (particle).

lantern_post_t1: tall dark post + bracket arm + one fat hanging lantern —
warm_window glass in an iron cage. Anchors: flame (light), fuel_hook (hook).

Budgets: storage 800/4, shelter 2500/4, lantern 600/3.
"""

from __future__ import annotations

import math
import random

import bpy

from asset_framework import add_anchor, register_builder
from builders._shapes import add_box


@register_builder("storage_pile_t1")
def build_storage_pile(spec: dict) -> list[bpy.types.Object]:
    rng = random.Random(23)
    objects: list[bpy.types.Object] = []

    # --- pallet ---------------------------------------------------------------
    objects.append(
        add_box("pallet", "mat_dark_wood", size=(1.8, 1.8, 0.12), location=(0.0, 0.0, 0.06))
    )
    for i, x in enumerate((-0.8, 0.0, 0.8)):
        objects.append(
            add_box(f"pallet_skid_{i}", "mat_dark_wood", size=(0.14, 1.7, 0.08), location=(x, 0.0, 0.16))
        )

    # --- crates: one big at the back, one stacked, one small beside ------------
    def crate(tag: str, size: float, loc: tuple[float, float, float], yaw: float) -> None:
        objects.append(
            add_box(f"crate_{tag}", "mat_warm_wood", size=(size, size, size * 0.9),
                    location=loc, rotation=(0.0, 0.0, yaw))
        )
        # thick dark corner posts (silhouette accent)
        h = size * 0.98
        for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
            ox = sx * size / 2.0
            oy = sy * size / 2.0
            c, s = math.cos(yaw), math.sin(yaw)
            objects.append(
                add_box(
                    f"crate_{tag}_post_{i}", "mat_dark_wood",
                    size=(size * 0.16, size * 0.16, h),
                    location=(loc[0] + ox * c - oy * s, loc[1] + ox * s + oy * c, loc[2]),
                    rotation=(0.0, 0.0, yaw),
                )
            )

    crate("big", 0.66, (-0.42, 0.45, 0.53), math.radians(4.0))
    crate("top", 0.48, (-0.40, 0.42, 1.12), math.radians(-14.0))
    crate("small", 0.44, (0.48, 0.55, 0.42), math.radians(20.0))

    # --- rope lashing over the top crate ---------------------------------------
    objects.append(
        add_box("rope_band_top", "mat_canvas", size=(0.56, 0.07, 0.035),
                location=(-0.40, 0.42, 1.36), rotation=(0.0, 0.0, math.radians(-14.0)))
    )
    objects.append(
        add_box("rope_knot", "mat_canvas", size=(0.12, 0.09, 0.07),
                location=(-0.40, 0.42, 1.38), rotation=(0.0, 0.0, math.radians(30.0)))
    )

    # --- sacks: fat squashed canvas lumps at the front -------------------------
    for i, (x, y, s) in enumerate(((0.35, -0.35, 0.5), (-0.25, -0.5, 0.44), (0.62, -0.6, 0.38))):
        objects.append(
            add_box(
                f"sack_{i}", "mat_canvas",
                size=(s, s * 0.85, s * 0.62),
                location=(x, y, 0.12 + s * 0.31),
                rotation=(rng.uniform(-0.08, 0.08), rng.uniform(-0.08, 0.08), rng.uniform(0.0, math.tau)),
            )
        )
    # one sack leaning on the big crate
    objects.append(
        add_box("sack_leaning", "mat_canvas", size=(0.42, 0.34, 0.56),
                location=(0.18, 0.28, 0.42), rotation=(0.12, -0.28, math.radians(35.0)))
    )

    # --- anchors ----------------------------------------------------------------
    objects.append(add_anchor("deposit_point", (0.0, -0.95, 0.0), anchor_type="work"))
    return objects


@register_builder("shelter_t1")
def build_shelter(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []
    roof_pitch = math.radians(35.0)

    # --- body: stout plank box ---------------------------------------------------
    objects.append(
        add_box("body", "mat_warm_wood", size=(1.6, 1.4, 1.05), location=(0.0, 0.0, 0.525))
    )
    # corner posts (dark, chunky)
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(
            add_box(f"corner_post_{i}", "mat_dark_wood", size=(0.16, 0.16, 1.15),
                    location=(sx * 0.78, sy * 0.68, 0.575))
        )

    # --- stepped gable core under the ridge ---------------------------------------
    objects.append(
        add_box("gable_low", "mat_warm_wood", size=(1.5, 1.15, 0.42), location=(0.0, 0.0, 1.26))
    )
    objects.append(
        add_box("gable_high", "mat_warm_wood", size=(1.4, 0.7, 0.36), location=(0.0, 0.0, 1.62))
    )

    # --- big overhanging sloped roof ----------------------------------------------
    for si, sy in enumerate((1, -1)):
        objects.append(
            add_box(
                f"roof_slab_{si}", "mat_thatch",
                size=(1.95, 1.15, 0.16),
                location=(0.0, sy * 0.46, 1.55),
                rotation=(-sy * roof_pitch, 0.0, 0.0),
            )
        )
    objects.append(
        add_box("ridge_cap", "mat_dark_wood", size=(2.0, 0.2, 0.14), location=(0.0, 0.0, 1.92))
    )

    # --- door (front, -Y) + window (side, +X) + tiny front window ------------------
    objects.append(
        add_box("door", "mat_dark_wood", size=(0.52, 0.10, 0.88), location=(0.1, -0.72, 0.44))
    )
    objects.append(
        add_box("door_lintel", "mat_dark_wood", size=(0.68, 0.12, 0.12), location=(0.1, -0.72, 0.94))
    )
    objects.append(
        add_box("window_side", "mat_warm_window", size=(0.08, 0.42, 0.42), location=(0.82, 0.1, 0.62))
    )
    objects.append(
        add_box("window_front", "mat_warm_window", size=(0.30, 0.08, 0.30), location=(-0.45, -0.72, 0.62))
    )

    # --- smoke_stack: stubby wooden flue poking through the roof --------------------
    objects.append(
        add_box("smoke_stack", "mat_dark_wood", size=(0.24, 0.24, 0.85), location=(0.52, 0.32, 1.85))
    )
    objects.append(
        add_box("smoke_stack_cap", "mat_dark_wood", size=(0.34, 0.34, 0.08), location=(0.52, 0.32, 2.31))
    )

    # --- anchors ----------------------------------------------------------------------
    objects.append(add_anchor("door", (0.1, -0.80, 0.0), anchor_type="door"))
    objects.append(add_anchor("smoke", (0.52, 0.32, 2.45), anchor_type="particle"))
    return objects


@register_builder("lantern_post_t1")
def build_lantern_post(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []
    arm_x = 0.40  # lantern hangs here

    # --- post + base ----------------------------------------------------------
    objects.append(
        add_box("base", "mat_dark_wood", size=(0.34, 0.34, 0.16), location=(0.0, 0.0, 0.08))
    )
    objects.append(
        add_box("post", "mat_dark_wood", size=(0.15, 0.15, 2.2), location=(0.0, 0.0, 1.16))
    )

    # --- bracket_arm + diagonal brace ------------------------------------------
    objects.append(
        add_box("bracket_arm", "mat_dark_wood", size=(0.60, 0.11, 0.11), location=(0.22, 0.0, 2.18))
    )
    objects.append(
        add_box("brace", "mat_dark_wood", size=(0.09, 0.09, 0.42),
                location=(0.20, 0.0, 2.00), rotation=(0.0, math.radians(-42.0), 0.0))
    )

    # --- lantern: iron cage around warm glass -----------------------------------
    objects.append(
        add_box("lantern_hook", "mat_iron", size=(0.05, 0.05, 0.12), location=(arm_x, 0.0, 2.08))
    )
    objects.append(
        add_box("lantern_top", "mat_iron", size=(0.24, 0.24, 0.06), location=(arm_x, 0.0, 1.99))
    )
    objects.append(
        add_box("lantern_glass", "mat_warm_window", size=(0.17, 0.17, 0.24), location=(arm_x, 0.0, 1.84))
    )
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(
            add_box(f"lantern_bar_{i}", "mat_iron", size=(0.035, 0.035, 0.26),
                    location=(arm_x + sx * 0.095, sy * 0.095, 1.84))
        )
    objects.append(
        add_box("lantern_base", "mat_iron", size=(0.21, 0.21, 0.05), location=(arm_x, 0.0, 1.69))
    )

    # --- fuel_hook: small iron peg on the post where the oil flask hangs ---------
    objects.append(
        add_box("fuel_hook_peg", "mat_iron", size=(0.16, 0.06, 0.06),
                location=(-0.13, 0.0, 1.15), rotation=(0.0, math.radians(-18.0), 0.0))
    )

    # --- anchors -------------------------------------------------------------------
    objects.append(add_anchor("flame", (arm_x, 0.0, 1.84), anchor_type="light"))
    objects.append(add_anchor("fuel_hook", (-0.20, 0.0, 1.12), anchor_type="hook"))
    return objects
