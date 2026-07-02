"""Builder: shipwreck_hull — the wreck itself, the settlement's first "mine".

Two broken hull halves torn apart amidships with jagged ribs at the tear, a
tilted mast stump carrying a torn sail, and debris planks in the gap. From the
iso camera the read is: long dark mass, bright canvas flag, hole in the middle
= "the ship is dead, pick its bones".

Anchors: salvage_point_1..3 (salvage), bell_fragment (loot).
Budget: 2500 tris / 3 materials (mat_dark_wood, mat_canvas, mat_iron).
"""

from __future__ import annotations

import math
import random

import bpy

from asset_framework import add_anchor, register_builder
from builders._shapes import add_box, add_cylinder, add_torus

# Overall envelope: 6 x 3 x 3 (X = along keel, torn gap around x = 0).


@register_builder("shipwreck_hull")
def build_shipwreck_hull(spec: dict) -> list[bpy.types.Object]:
    rng = random.Random(7)
    objects: list[bpy.types.Object] = []

    # --- keel: runs under both halves, hints they were once one ship --------
    objects.append(
        add_box("keel", "mat_dark_wood", size=(5.4, 0.28, 0.22), location=(0.0, 0.0, 0.11))
    )

    # --- stern_half (x in [-2.9, -0.5]) -------------------------------------
    objects.append(
        add_box(
            "stern_bottom", "mat_dark_wood",
            size=(2.3, 1.7, 0.5), location=(-1.7, 0.0, 0.35),
        )
    )
    # flared hull side plank rows
    for si, sy in enumerate((1, -1)):
        for row in range(3):
            objects.append(
                add_box(
                    f"stern_side_{si}_{row}", "mat_dark_wood",
                    size=(2.3 - row * 0.12, 0.14, 0.40),
                    location=(-1.75 - row * 0.04, sy * (0.82 + row * 0.10), 0.62 + row * 0.34),
                    rotation=(sy * 0.30, 0.0, 0.0),
                )
            )
    objects.append(
        add_box(
            "stern_transom", "mat_dark_wood",
            size=(0.18, 1.6, 1.35), location=(-2.80, 0.0, 0.78),
            rotation=(0.0, -0.10, 0.0),
        )
    )
    objects.append(
        add_box("stern_deck", "mat_dark_wood", size=(1.4, 1.45, 0.10), location=(-2.05, 0.0, 1.16))
    )
    # jagged ribs at the tear
    for i in range(3):
        objects.append(
            add_box(
                f"stern_rib_{i}", "mat_dark_wood",
                size=(0.10, 0.13, 0.85 + rng.uniform(0.0, 0.5)),
                location=(-0.62, -0.60 + i * 0.60, 0.95 + rng.uniform(0.0, 0.2)),
                rotation=(rng.uniform(-0.15, 0.15), rng.uniform(-0.35, -0.10), 0.0),
            )
        )

    # --- bow_half (x in [0.6, 2.95]) ----------------------------------------
    objects.append(
        add_box("bow_bottom", "mat_dark_wood", size=(2.1, 1.5, 0.42), location=(1.62, 0.0, 0.30))
    )
    for si, sy in enumerate((1, -1)):
        objects.append(
            add_box(
                f"bow_side_{si}", "mat_dark_wood",
                size=(2.15, 0.14, 0.9),
                location=(1.58, sy * 0.62, 0.75),
                rotation=(sy * 0.22, 0.0, -sy * 0.24),
            )
        )
    objects.append(
        add_box(
            "bow_stem", "mat_dark_wood",
            size=(0.22, 0.22, 1.3), location=(2.68, 0.0, 0.85),
            rotation=(0.0, 0.18, 0.0),
        )
    )
    for i in range(3):
        objects.append(
            add_box(
                f"bow_rib_{i}", "mat_dark_wood",
                size=(0.10, 0.13, 0.75 + rng.uniform(0.0, 0.45)),
                location=(0.74, -0.55 + i * 0.55, 0.85 + rng.uniform(0.0, 0.2)),
                rotation=(rng.uniform(-0.15, 0.15), rng.uniform(0.10, 0.35), 0.0),
            )
        )

    # --- debris planks scattered in the tear gap ----------------------------
    for i in range(5):
        objects.append(
            add_box(
                f"debris_plank_{i}", "mat_dark_wood",
                size=(0.65 + rng.uniform(0.0, 0.35), 0.16, 0.07),
                location=(rng.uniform(-0.30, 0.35), rng.uniform(-1.0, 1.0), 0.06),
                rotation=(0.0, rng.uniform(-0.08, 0.08), rng.uniform(0.0, math.tau)),
            )
        )

    # --- mast_stump: tilted, snapped, with iron bands + torn sail -----------
    mast_rot_y = 0.16
    mast_center = (-1.55, 0.12, 1.42)
    objects.append(
        add_cylinder(
            "mast_stump", "mat_dark_wood",
            radius=0.11, depth=2.3, vertices=8,
            location=mast_center, rotation=(0.0, mast_rot_y, 0.0),
        )
    )
    # iron bands follow the mast axis
    for bi, t in enumerate((-0.55, 0.45)):
        objects.append(
            add_cylinder(
                f"mast_band_{bi}", "mat_iron",
                radius=0.13, depth=0.09, vertices=8,
                location=(
                    mast_center[0] + math.sin(mast_rot_y) * t,
                    mast_center[1],
                    mast_center[2] + math.cos(mast_rot_y) * t,
                ),
                rotation=(0.0, mast_rot_y, 0.0),
            )
        )
    # yard arm across the top
    yard_z = 2.30
    yard_x = mast_center[0] + math.sin(mast_rot_y) * (yard_z - mast_center[2])
    objects.append(
        add_cylinder(
            "yard_arm", "mat_dark_wood",
            radius=0.06, depth=1.7, vertices=7,
            location=(yard_x, 0.0, yard_z),
            rotation=(math.radians(90.0), 0.0, 0.0),
        )
    )
    # torn_sail: two ragged canvas strips of different lengths hanging down
    objects.append(
        add_box(
            "torn_sail_a", "mat_canvas",
            size=(0.06, 0.58, 0.95), location=(yard_x + 0.02, -0.38, yard_z - 0.53),
            rotation=(0.06, 0.10, 0.0),
        )
    )
    objects.append(
        add_box(
            "torn_sail_b", "mat_canvas",
            size=(0.06, 0.44, 0.55), location=(yard_x + 0.03, 0.30, yard_z - 0.33),
            rotation=(-0.05, 0.14, 0.0),
        )
    )

    # --- iron details: fallen anchor ring + cleats ---------------------------
    objects.append(
        add_torus(
            "anchor_ring", "mat_iron",
            major_radius=0.22, minor_radius=0.05,
            major_segments=8, minor_segments=4,
            location=(-2.35, 1.12, 0.24),
            rotation=(math.radians(72.0), 0.0, math.radians(20.0)),
        )
    )
    for ci, (cx, cy) in enumerate(((-1.2, -0.95), (2.0, 0.72))):
        objects.append(
            add_box(
                f"cleat_{ci}", "mat_iron",
                size=(0.22, 0.09, 0.09), location=(cx, cy, 1.02),
                rotation=(0.0, 0.0, 0.3 * (1 if cy > 0 else -1)),
            )
        )

    # --- anchors -------------------------------------------------------------
    objects.append(add_anchor("salvage_point_1", (-2.05, 0.0, 1.25), anchor_type="salvage"))
    objects.append(add_anchor("salvage_point_2", (1.62, 0.0, 0.55), anchor_type="salvage"))
    objects.append(add_anchor("salvage_point_3", (0.05, -0.65, 0.12), anchor_type="salvage"))
    objects.append(add_anchor("bell_fragment", (0.15, 0.55, 0.15), anchor_type="loot"))

    return objects
