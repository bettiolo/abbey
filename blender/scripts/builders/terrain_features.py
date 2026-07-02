"""Builder family: wilderness terrain features — trees and rocks.

forest_tree_01: leaning dark trunk under a chunky stacked blob canopy. The
canopy is three offset icosphere blobs — a bold rounded silhouette that stays
readable when the forest edge goes black at night.

rock_cluster_01: one dominant tilted boulder with smaller chunks huddled
against it. Pure old_stone.

Budgets: tree 800/3, rocks 400/3.
"""

from __future__ import annotations

import math
import random

import bpy

from asset_framework import register_builder
from builders._shapes import add_box, add_cylinder, add_icosphere


@register_builder("forest_tree_01")
def build_forest_tree(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []

    # --- trunk: leaning slightly, with one branch stub ---------------------------
    objects.append(
        add_cylinder(
            "trunk", "mat_dark_wood",
            radius=0.10, depth=1.3, vertices=7,
            location=(0.0, 0.0, 0.65), rotation=(0.04, 0.09, 0.0),
        )
    )
    objects.append(
        add_cylinder(
            "branch_stub", "mat_dark_wood",
            radius=0.05, depth=0.45, vertices=6,
            location=(0.16, -0.10, 1.15), rotation=(math.radians(55.0), 0.0, math.radians(-35.0)),
        )
    )

    # --- canopy_blobs: three chunky offset blobs, big below, small on top --------
    objects.append(
        add_icosphere("canopy_low", "mat_foliage", radius=0.42,
                      location=(0.04, 0.02, 1.75), scale=(1.0, 0.95, 0.85))
    )
    objects.append(
        add_icosphere("canopy_mid", "mat_foliage", radius=0.32,
                      location=(0.16, 0.10, 2.25), scale=(0.95, 1.0, 0.9))
    )
    objects.append(
        add_icosphere("canopy_top", "mat_foliage", radius=0.23,
                      location=(-0.10, -0.06, 2.60), scale=(1.0, 0.9, 0.95))
    )

    return objects


@register_builder("rock_cluster_01")
def build_rock_cluster(spec: dict) -> list[bpy.types.Object]:
    rng = random.Random(31)
    objects: list[bpy.types.Object] = []

    # --- main_boulder: dominant, tilted ------------------------------------------
    objects.append(
        add_box(
            "main_boulder", "mat_old_stone",
            size=(0.48, 0.42, 0.52), location=(-0.08, 0.05, 0.22),
            rotation=(0.10, -0.14, math.radians(25.0)),
        )
    )

    # --- side_rocks: smaller chunks huddled against it -----------------------------
    for i, (x, y, s) in enumerate((
        (0.28, -0.18, 0.30),
        (0.18, 0.26, 0.24),
        (-0.30, -0.24, 0.20),
        (-0.34, 0.24, 0.16),
    )):
        objects.append(
            add_box(
                f"side_rock_{i}", "mat_old_stone",
                size=(s, s * 0.85, s * 0.75),
                location=(x, y, s * 0.28),
                rotation=(rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2), rng.uniform(0.0, math.tau)),
            )
        )

    return objects
