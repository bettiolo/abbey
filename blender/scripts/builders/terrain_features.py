"""Builder family: wilderness terrain features — trees and rocks.

forest_tree_01: leaning dark trunk under a chunky stacked blob canopy. The
canopy is three offset icosphere blobs — a bold rounded silhouette that stays
readable when the forest edge goes black at night.

rock_cluster_01: one dominant tilted boulder with smaller chunks huddled
against it. Pure old_stone. Squashed low-poly icospheres, not boxes — in
grayscale, cube rocks read as crates and collide with storage_pile. At
subdivisions=1 each icosphere is a 20-tri icosahedron: 4 rocks = 80 tris.

Budgets: tree 800/3, rocks 400/3.
"""

from __future__ import annotations

import math
import random

import bpy

from asset_framework import register_builder
from builders._shapes import add_cone, add_cylinder, add_icosphere


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


@register_builder("forest_tree_02")
def build_forest_tree_02(spec: dict) -> list[bpy.types.Object]:
    """Tall dark conifer — spearhead silhouette, distinct from tree_01's blobs."""
    objects: list[bpy.types.Object] = []

    # --- trunk: straight, slightly thicker at the base ---------------------------
    objects.append(
        add_cylinder(
            "trunk", "mat_dark_wood",
            radius=0.13, depth=1.1, vertices=7,
            location=(0.0, 0.0, 0.55), rotation=(0.02, -0.03, 0.0),
        )
    )

    # --- foliage_tiers: three stacked cones narrowing upward ---------------------
    for i, (radius, depth, z) in enumerate(((0.68, 1.25, 1.45), (0.52, 1.15, 2.25), (0.36, 1.0, 3.0))):
        objects.append(
            add_cone(
                f"foliage_tier_{i}", "mat_foliage",
                radius=radius, depth=depth, vertices=8,
                location=(0.015 * (i - 1), -0.01 * i, z),
            )
        )

    # --- tip: thin dark spike past the last tier ----------------------------------
    objects.append(
        add_cone(
            "tip", "mat_foliage",
            radius=0.14, depth=0.55, vertices=6,
            location=(0.0, -0.02, 3.6),
        )
    )

    return objects


@register_builder("rock_cluster_01")
def build_rock_cluster(spec: dict) -> list[bpy.types.Object]:
    rng = random.Random(31)
    objects: list[bpy.types.Object] = []

    # --- main_boulder: dominant, tilted, slightly buried ---------------------------
    objects.append(
        add_icosphere(
            "main_boulder", "mat_old_stone", radius=0.34,
            location=(-0.08, 0.04, 0.26),
            rotation=(0.10, -0.14, math.radians(25.0)),
            scale=(1.0, 0.88, 0.92),
        )
    )

    # --- side_rocks: smaller squashed chunks huddled against the boulder ----------
    for i, (x, y, r, squash) in enumerate((
        (0.26, -0.16, 0.18, 0.72),
        (0.16, 0.24, 0.15, 0.80),
        (-0.26, -0.22, 0.13, 0.66),
    )):
        objects.append(
            add_icosphere(
                f"side_rock_{i}", "mat_old_stone", radius=r,
                location=(x, y, r * squash * 0.72),
                rotation=(rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), rng.uniform(0.0, math.tau)),
                scale=(1.0, rng.uniform(0.78, 0.95), squash),
            )
        )

    return objects
