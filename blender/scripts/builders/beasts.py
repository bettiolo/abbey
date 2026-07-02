"""Builder family: beasts — placeholder silhouettes that must already be memorable.

black_hound_lowpoly: the Black Hound of the Bell Tower. ART_BIBLE: "large,
wounded, memorable — even as a placeholder the silhouette matters most."
Shoulder height ~1.2 m, heavy chest, gaunt haunches with exposed bone ribs,
lowered menacing head, iron collar, tail hanging low. Chunky boxes only —
the outline against the light is the whole job.

Anchors: mouth (interaction — feeding), collar (attach — the chain),
saddle_point (mount — future bond payoff).
Budget: 1500 tris / 3 materials (mat_nightmare_black, mat_bone, mat_iron).

Facing +X; footprint 2 x 1 (everything kept within x in [-1, 1]).
"""

from __future__ import annotations

import math

import bpy

from asset_framework import add_anchor, register_builder
from builders._shapes import add_box, add_cone, add_torus


@register_builder("black_hound_lowpoly")
def build_black_hound(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []
    black = "mat_nightmare_black"

    # --- torso: sagging spine between heavy chest and gaunt haunches -----------
    objects.append(
        add_box("torso", black, size=(0.90, 0.54, 0.46), location=(-0.14, 0.0, 0.93),
                rotation=(0.0, math.radians(-4.0), 0.0))
    )
    objects.append(
        add_box("chest", black, size=(0.52, 0.62, 0.60), location=(0.24, 0.0, 0.93),
                rotation=(0.0, math.radians(8.0), 0.0))
    )
    objects.append(
        add_box("haunches", black, size=(0.44, 0.56, 0.54), location=(-0.56, 0.0, 0.88),
                rotation=(0.0, math.radians(-10.0), 0.0))
    )

    # --- neck + lowered head + snout -------------------------------------------
    objects.append(
        add_box("neck", black, size=(0.40, 0.36, 0.40), location=(0.48, 0.0, 1.16),
                rotation=(0.0, math.radians(-35.0), 0.0))
    )
    objects.append(
        add_box("head", black, size=(0.40, 0.36, 0.30), location=(0.66, 0.0, 1.32),
                rotation=(0.0, math.radians(10.0), 0.0))
    )
    objects.append(
        add_box("snout", black, size=(0.26, 0.22, 0.16), location=(0.82, 0.0, 1.26),
                rotation=(0.0, math.radians(12.0), 0.0))
    )
    # ears: unnatural, slightly uneven
    objects.append(
        add_cone("ear_left", black, radius=0.09, depth=0.24, vertices=4,
                 location=(0.58, 0.13, 1.54), rotation=(math.radians(-12.0), math.radians(-8.0), 0.0))
    )
    objects.append(
        add_cone("ear_right", black, radius=0.09, depth=0.20, vertices=4,
                 location=(0.58, -0.13, 1.51), rotation=(math.radians(18.0), math.radians(-4.0), 0.0))
    )

    # --- legs: fronts planted, rears crouched (wounded, wary) --------------------
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"leg_front_{i}", black, size=(0.17, 0.17, 0.68),
                    location=(0.26, sy * 0.21, 0.34))
        )
        objects.append(
            add_box(f"paw_front_{i}", black, size=(0.24, 0.18, 0.10),
                    location=(0.31, sy * 0.21, 0.05))
        )
        objects.append(
            add_box(f"leg_rear_upper_{i}", black, size=(0.28, 0.18, 0.44),
                    location=(-0.58, sy * 0.22, 0.54), rotation=(0.0, math.radians(-18.0), 0.0))
        )
        objects.append(
            add_box(f"leg_rear_lower_{i}", black, size=(0.14, 0.14, 0.40),
                    location=(-0.66, sy * 0.22, 0.20), rotation=(0.0, math.radians(12.0), 0.0))
        )
        objects.append(
            add_box(f"paw_rear_{i}", black, size=(0.22, 0.16, 0.09),
                    location=(-0.62, sy * 0.22, 0.045))
        )

    # --- tail: hanging low and back (wounded) --------------------------------------
    objects.append(
        add_box("tail", black, size=(0.46, 0.10, 0.10), location=(-0.78, 0.02, 0.72),
                rotation=(0.0, math.radians(-55.0), math.radians(4.0)))
    )

    # --- exposed_ribs: bone accents on the gaunt left flank -------------------------
    for i in range(3):
        objects.append(
            add_box(
                f"rib_{i}", "mat_bone",
                size=(0.05, 0.06, 0.32),
                location=(-0.28 - i * 0.13, 0.275, 0.94),
                rotation=(math.radians(-8.0), math.radians(-6.0 + i * 4.0), 0.0),
            )
        )
    # old scar ridge along the haunch + spine knuckles
    objects.append(
        add_box("scar", "mat_bone", size=(0.28, 0.04, 0.05), location=(-0.54, 0.275, 1.00),
                rotation=(0.0, math.radians(-30.0), 0.0))
    )
    for i in range(3):
        objects.append(
            add_cone(f"spine_knuckle_{i}", "mat_bone", radius=0.045, depth=0.10, vertices=4,
                     location=(-0.08 - i * 0.18, 0.0, 1.18 - i * 0.02))
        )

    # --- iron_collar ------------------------------------------------------------------
    objects.append(
        add_torus(
            "iron_collar", "mat_iron",
            major_radius=0.26, minor_radius=0.05,
            major_segments=8, minor_segments=4,
            location=(0.52, 0.0, 1.20),
            rotation=(0.0, math.radians(55.0), 0.0),
        )
    )

    # --- anchors -------------------------------------------------------------------
    objects.append(add_anchor("mouth", (0.97, 0.0, 1.22), anchor_type="interaction"))
    objects.append(add_anchor("collar", (0.52, 0.0, 1.20), anchor_type="attach"))
    objects.append(add_anchor("saddle_point", (-0.16, 0.0, 1.17), anchor_type="mount"))
    return objects
