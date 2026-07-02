"""Builder: campfire_t1 — the first light zone of the settlement.

Silhouette: low chunky ring of stones, crossed logs, one bold teardrop flame.
Reads at a glance from the iso camera even in grayscale: dark ring, bright core.

Parts (spec.required_parts): stone_ring, logs, flame_placeholder.
Anchors: ember_glow (light), smoke (particle).
Budget: 800 tris / 3 materials (mat_old_stone, mat_dark_wood, mat_flame).
"""

from __future__ import annotations

import math
import random

import bpy

from asset_framework import add_anchor, register_builder
from builders._shapes import add_box, add_cone, add_cylinder

RING_RADIUS = 0.34
STONE_COUNT = 8
LOG_COUNT = 4


@register_builder("campfire_t1")
def build_campfire(spec: dict) -> list[bpy.types.Object]:
    rng = random.Random(41)  # deterministic variation
    objects: list[bpy.types.Object] = []

    # --- stone_ring: 8 chunky rough cubes around the fire pit -------------
    for i in range(STONE_COUNT):
        angle = (i / STONE_COUNT) * math.tau
        sx = 0.16 + rng.uniform(-0.03, 0.03)
        sy = 0.13 + rng.uniform(-0.02, 0.02)
        sz = 0.12 + rng.uniform(-0.02, 0.03)
        stone = add_box(
            f"stone_ring_{i:02d}",
            "mat_old_stone",
            size=(sx, sy, sz),
            location=(
                math.cos(angle) * RING_RADIUS,
                math.sin(angle) * RING_RADIUS,
                sz * 0.42,  # slightly sunk into the ground
            ),
            rotation=(
                rng.uniform(-0.12, 0.12),
                rng.uniform(-0.12, 0.12),
                angle + rng.uniform(-0.3, 0.3),
            ),
        )
        objects.append(stone)

    # --- logs: teepee of 4 crossed logs over the pit ----------------------
    lean = math.radians(38.0)
    for i in range(LOG_COUNT):
        angle = (i / LOG_COUNT) * math.tau + math.radians(45.0)
        log = add_cylinder(
            f"log_{i:02d}",
            "mat_dark_wood",
            radius=0.045,
            depth=0.46,
            vertices=7,
            location=(
                math.cos(angle) * 0.09,
                math.sin(angle) * 0.09,
                0.20,
            ),
            rotation=(lean, 0.0, angle + math.radians(90.0)),
        )
        objects.append(log)
    # one fallen log outside the ring for silhouette asymmetry
    objects.append(
        add_cylinder(
            "log_fallen",
            "mat_dark_wood",
            radius=0.05,
            depth=0.34,
            vertices=7,
            location=(0.33, -0.30, 0.05),
            rotation=(0.0, math.radians(90.0), math.radians(-35.0)),
        )
    )

    # --- flame_placeholder: bold teardrop (big cone + inner offset cone) --
    objects.append(
        add_cone(
            "flame_placeholder",
            "mat_flame",
            radius=0.17,
            depth=0.38,
            vertices=8,
            location=(0.0, 0.0, 0.33),
        )
    )
    objects.append(
        add_cone(
            "flame_tip",
            "mat_flame",
            radius=0.09,
            depth=0.24,
            vertices=8,
            location=(0.035, -0.02, 0.50),
            rotation=(math.radians(-6.0), math.radians(8.0), 0.0),
        )
    )

    # --- anchors -----------------------------------------------------------
    objects.append(add_anchor("ember_glow", (0.0, 0.0, 0.14), anchor_type="light"))
    objects.append(add_anchor("smoke", (0.0, 0.0, 0.60), anchor_type="particle"))

    return objects
