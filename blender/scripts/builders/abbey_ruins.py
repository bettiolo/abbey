"""Builder family: the ruined abbey — broken wall, ruined bell tower, hound chain.

abbey_wall_broken: old-stone wall segment with a jagged descending break line,
protruding weathered blocks, rubble spilled at its foot. Pure mat_old_stone so
it reads as one ancient mass even in grayscale.

bell_tower_ruined: tall square tower, jagged broken crown, the front wall low
and collapsed so the sacred-gold bell (hanging from a charred beam) is visible
from the iso camera. Something huge lairs on the ground floor.
Anchors: bell (bell), hound_lair (lair), door (door).

hound_chain: massive stone anchor block, iron mounting ring, heavy chain of
fat links draping down the block and across the ground. Anchor: attach_point.

Budgets: wall 1200/4, tower 2500/4, chain 800/3.
"""

from __future__ import annotations

import math
import random

import bpy

from asset_framework import add_anchor, register_builder
from builders._shapes import add_box, add_cone, add_cylinder, add_torus


@register_builder("abbey_wall_broken")
def build_abbey_wall_broken(spec: dict) -> list[bpy.types.Object]:
    rng = random.Random(11)
    objects: list[bpy.types.Object] = []

    # --- base_course: full-length footing ------------------------------------
    objects.append(
        add_box("base_course", "mat_old_stone", size=(1.9, 0.5, 0.5), location=(0.0, 0.0, 0.25))
    )

    # --- broken_courses: jagged descending break from left to right ----------
    for i, (x, w, h) in enumerate((
        (-0.66, 0.52, 1.58),
        (-0.26, 0.42, 1.28),
        (0.12, 0.40, 0.95),
        (0.50, 0.38, 0.62),
        (0.78, 0.26, 0.40),
    )):
        objects.append(
            add_box(
                f"course_{i}", "mat_old_stone",
                size=(w, 0.42, h),
                location=(x, rng.uniform(-0.02, 0.02), 0.5 + h / 2.0),
                rotation=(rng.uniform(-0.03, 0.03), rng.uniform(-0.04, 0.04), rng.uniform(-0.04, 0.04)),
            )
        )
    # jagged snapped tooth on the tall end
    objects.append(
        add_box("tooth", "mat_old_stone", size=(0.3, 0.36, 0.42),
                location=(-0.78, 0.0, 1.88), rotation=(0.0, 0.12, 0.06))
    )

    # --- protruding weathered blocks on the faces -----------------------------
    for i in range(5):
        objects.append(
            add_box(
                f"face_block_{i}", "mat_old_stone",
                size=(0.28, 0.14, 0.20),
                location=(
                    rng.uniform(-0.85, 0.2),
                    rng.choice((-1, 1)) * 0.20,
                    rng.uniform(0.7, 1.6),
                ),
                rotation=(0.0, 0.0, rng.uniform(-0.1, 0.1)),
            )
        )

    # --- rubble spilled at the broken end --------------------------------------
    for i in range(6):
        s = rng.uniform(0.14, 0.30)
        objects.append(
            add_box(
                f"rubble_{i}", "mat_old_stone",
                size=(s, s * 0.85, s * 0.7),
                location=(rng.uniform(0.3, 0.8), rng.uniform(-0.38, 0.38), s * 0.3),
                rotation=(rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2), rng.uniform(0.0, math.tau)),
            )
        )

    return objects


@register_builder("bell_tower_ruined")
def build_bell_tower_ruined(spec: dict) -> list[bpy.types.Object]:
    rng = random.Random(13)
    objects: list[bpy.types.Object] = []

    half = 1.15       # inner tower half-size (walls at +-half)
    wall_t = 0.35

    # --- tower_walls: back tall, sides mid, front low with the doorway --------
    # back wall (+Y): tallest, still crowned
    objects.append(
        add_box("wall_back", "mat_old_stone", size=(2.3, wall_t, 4.8), location=(0.0, half, 2.4))
    )
    # side walls: broken to different heights (collapse reads from iso camera)
    objects.append(
        add_box("wall_left", "mat_old_stone", size=(wall_t, 2.3, 4.2), location=(-half, 0.0, 2.1))
    )
    objects.append(
        add_box("wall_right", "mat_old_stone", size=(wall_t, 2.3, 3.1), location=(half, 0.0, 1.55))
    )
    # front wall (-Y): lowest — collapsed, exposing the bell; doorway cut by
    # building it as two jambs + lintel
    objects.append(
        add_box("front_jamb_left", "mat_old_stone", size=(0.72, wall_t, 2.2), location=(-0.79, -half, 1.1))
    )
    objects.append(
        add_box("front_jamb_right", "mat_old_stone", size=(0.72, wall_t, 2.2), location=(0.79, -half, 1.1))
    )
    objects.append(
        add_box("front_lintel", "mat_old_stone", size=(1.3, wall_t + 0.06, 0.35), location=(0.0, -half, 1.95))
    )

    # --- corner_piers: chunky, snapped at different heights --------------------
    for i, ((sx, sy), h) in enumerate(zip(((1, 1), (-1, 1), (-1, -1), (1, -1)), (5.2, 4.6, 2.6, 2.3))):
        objects.append(
            add_box(
                f"corner_pier_{i}", "mat_old_stone",
                size=(0.5, 0.5, h),
                location=(sx * (half + 0.10), sy * (half + 0.10), h / 2.0),
                rotation=(0.0, 0.0, rng.uniform(-0.03, 0.03)),
            )
        )

    # --- broken_crown: jagged teeth along the surviving top edges --------------
    for i in range(4):
        h = rng.uniform(0.35, 0.7)
        objects.append(
            add_box(
                f"crown_tooth_back_{i}", "mat_old_stone",
                size=(0.34, wall_t * 0.9, h),
                location=(-0.85 + i * 0.55, half, 4.8 + h / 2.0),
                rotation=(rng.uniform(-0.06, 0.06), rng.uniform(-0.08, 0.08), 0.0),
            )
        )
    for i in range(3):
        h = rng.uniform(0.3, 0.6)
        objects.append(
            add_box(
                f"crown_tooth_left_{i}", "mat_old_stone",
                size=(wall_t * 0.9, 0.34, h),
                location=(-half, -0.7 + i * 0.65, 4.2 + h / 2.0),
                rotation=(rng.uniform(-0.08, 0.08), rng.uniform(-0.06, 0.06), 0.0),
            )
        )

    # --- belfry_beam + bell: charred beam spanning the tower, gold bell under --
    objects.append(
        add_box("belfry_beam", "mat_dark_wood", size=(2.5, 0.22, 0.24), location=(0.0, 0.35, 4.55))
    )
    objects.append(
        add_box("belfry_strut", "mat_dark_wood", size=(0.16, 0.16, 0.9),
                location=(-0.85, 0.35, 4.05), rotation=(0.0, 0.35, 0.0))
    )
    # bell: flared cone body + crown + clapper, hanging just below the beam
    objects.append(
        add_cone(
            "bell_body", "mat_sacred_gold",
            radius=0.46, radius_top=0.22, depth=0.62, vertices=10,
            location=(0.0, 0.35, 4.05),
        )
    )
    objects.append(
        add_cylinder(
            "bell_crown", "mat_sacred_gold",
            radius=0.14, depth=0.18, vertices=8,
            location=(0.0, 0.35, 4.42),
        )
    )
    objects.append(
        add_cone(
            "bell_clapper", "mat_sacred_gold",
            radius=0.09, depth=0.22, vertices=6,
            location=(0.0, 0.35, 3.70),
        )
    )

    # --- rubble: the collapsed front wall, spilled outward ---------------------
    for i in range(8):
        s = rng.uniform(0.18, 0.42)
        objects.append(
            add_box(
                f"rubble_{i}", "mat_old_stone",
                size=(s, s * 0.8, s * 0.65),
                location=(rng.uniform(-1.1, 1.1), -half - rng.uniform(0.0, 0.12), s * 0.28),
                rotation=(rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25), rng.uniform(0.0, math.tau)),
            )
        )

    # --- anchors -----------------------------------------------------------------
    objects.append(add_anchor("bell", (0.0, 0.35, 4.05), anchor_type="bell"))
    objects.append(add_anchor("hound_lair", (0.0, 0.25, 0.05), anchor_type="lair"))
    objects.append(add_anchor("door", (0.0, -half - 0.3, 0.0), anchor_type="door"))
    return objects


@register_builder("hound_chain")
def build_hound_chain(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []

    # --- anchor_block: massive stone block, slightly settled ---------------------
    objects.append(
        add_box(
            "anchor_block", "mat_old_stone",
            size=(0.55, 0.5, 0.46), location=(-0.18, 0.05, 0.22),
            rotation=(0.02, -0.03, math.radians(8.0)),
        )
    )
    objects.append(
        add_box("block_cap", "mat_old_stone", size=(0.42, 0.38, 0.14),
                location=(-0.18, 0.05, 0.50), rotation=(0.0, 0.0, math.radians(-6.0)))
    )

    # --- mount_ring: heavy iron ring bolted to the block top ---------------------
    objects.append(
        add_torus(
            "mount_ring", "mat_iron",
            major_radius=0.13, minor_radius=0.035,
            major_segments=8, minor_segments=4,
            location=(-0.14, 0.05, 0.60),
            rotation=(0.0, math.radians(90.0), math.radians(10.0)),
        )
    )
    objects.append(
        add_box("mount_plate", "mat_iron", size=(0.16, 0.16, 0.05), location=(-0.16, 0.05, 0.555))
    )

    # --- chain_links: fat links draping down the block and across the ground -----
    # (x, y, z, pitch, yaw) — alternating link orientation
    link_path = (
        (0.02, 0.04, 0.52, math.radians(50.0), 0.0),
        (0.10, 0.03, 0.38, math.radians(65.0), math.radians(90.0)),
        (0.17, 0.02, 0.22, math.radians(80.0), 0.0),
        (0.24, 0.00, 0.08, math.radians(90.0), math.radians(90.0)),
        (0.33, -0.03, 0.045, math.radians(90.0), 0.0),
        (0.41, -0.05, 0.045, math.radians(90.0), math.radians(90.0)),
    )
    for i, (x, y, z, pitch, yaw) in enumerate(link_path):
        objects.append(
            add_torus(
                f"chain_link_{i}", "mat_iron",
                major_radius=0.075, minor_radius=0.028,
                major_segments=6, minor_segments=4,
                location=(x, y, z),
                rotation=(pitch, 0.0, yaw),
            )
        )
    # broken final link, half-open (a cut torus is faked with a small bent bar)
    objects.append(
        add_box("broken_link_bar", "mat_iron", size=(0.12, 0.03, 0.03),
                location=(0.43, -0.06, 0.035), rotation=(0.0, 0.0, math.radians(-25.0)))
    )

    # --- anchors ------------------------------------------------------------------
    objects.append(add_anchor("attach_point", (0.43, -0.06, 0.05), anchor_type="attach"))
    return objects
