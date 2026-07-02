"""Builder family: shipwreck salvage — the opened crate and the torn sail.

shipwreck_crate_open: shipwreck_crate_closed's sibling, cracked open. Same
body dimensions and dark corner frames so the pair reads as one family; the
lid leans against the crate's visible (-Y) side, pale canvas salvage bulges
over the rim, a cut rope hangs loose. Anchors: carry_handle (grip),
salvage_point (work).

sailcloth: a torn sail draped over a snapped diagonal spar — one tall canvas
triangle sagging into heavy ground folds, ragged trailing edge. Salvage pile
the villagers strip for shelter canvas. Anchor: salvage_point (work).

Budgets: crate 400/3 (mat_warm_wood, mat_dark_wood, mat_canvas);
sail 800/3 (mat_canvas, mat_dark_wood).
"""

from __future__ import annotations

import math

import bpy

from asset_framework import add_anchor, register_builder
from builders._shapes import add_box, add_cylinder

# Same body dimensions as shipwreck_crate_closed (one crate family).
BODY_W = 0.72
BODY_D = 0.72
BODY_H = 0.60
FRAME_T = 0.10
WALL_T = 0.07


@register_builder("shipwreck_crate_open")
def build_crate_open(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []
    hw, hd = BODY_W / 2.0, BODY_D / 2.0

    # --- crate_walls: floor + 4 plank walls (interior visible from above) ----
    objects.append(
        add_box("crate_floor", "mat_warm_wood", size=(BODY_W, BODY_D, 0.08),
                location=(0.0, 0.0, 0.04))
    )
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"wall_y_{i}", "mat_warm_wood",
                    size=(BODY_W, WALL_T, BODY_H),
                    location=(0.0, sy * (hd - WALL_T / 2.0), BODY_H / 2.0))
        )
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"wall_x_{i}", "mat_warm_wood",
                    size=(WALL_T, BODY_D, BODY_H),
                    location=(sx * (hw - WALL_T / 2.0), 0.0, BODY_H / 2.0))
        )

    # --- corner_frames: 4 dark posts + top rim rails (family silhouette) ------
    post_h = BODY_H + FRAME_T * 0.6
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(
            add_box(f"frame_post_{i:02d}", "mat_dark_wood",
                    size=(FRAME_T, FRAME_T, post_h),
                    location=(sx * hw, sy * hd, post_h / 2.0))
        )
    rail_len_x = BODY_W + FRAME_T * 0.5
    rail_len_y = BODY_D + FRAME_T * 0.5
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"rim_rail_x_{i}", "mat_dark_wood",
                    size=(rail_len_x, FRAME_T * 0.9, FRAME_T * 0.9),
                    location=(0.0, sy * hd, BODY_H))
        )
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"rim_rail_y_{i}", "mat_dark_wood",
                    size=(FRAME_T * 0.9, rail_len_y, FRAME_T * 0.9),
                    location=(sx * hw, 0.0, BODY_H))
        )

    # --- leaning_lid: pried off, leaning against the visible -Y side ----------
    objects.append(
        add_box("leaning_lid", "mat_warm_wood",
                size=(BODY_W * 0.94, 0.06, BODY_D * 0.94),
                location=(0.06, -hd - 0.24, 0.32),
                rotation=(math.radians(-28.0), 0.0, math.radians(6.0)))
    )
    for i, x in enumerate((-0.24, 0.32)):
        objects.append(
            add_box(f"lid_batten_{i}", "mat_dark_wood",
                    size=(0.09, 0.05, BODY_D * 0.8),
                    location=(0.06 + x, -hd - 0.27, 0.33),
                    rotation=(math.radians(-28.0), 0.0, math.radians(6.0)))
        )

    # --- salvage_contents: canvas sacks bulging over the rim ------------------
    objects.append(
        add_box("salvage_bulge", "mat_canvas", size=(0.52, 0.52, 0.30),
                location=(-0.04, 0.04, BODY_H - 0.02),
                rotation=(0.06, -0.05, math.radians(14.0)))
    )
    objects.append(
        add_box("salvage_sack_top", "mat_canvas", size=(0.34, 0.30, 0.22),
                location=(0.10, -0.10, BODY_H + 0.16),
                rotation=(0.10, 0.12, math.radians(-20.0)))
    )
    objects.append(
        add_box("salvage_sack_spill", "mat_canvas", size=(0.30, 0.26, 0.20),
                location=(hw + 0.16, 0.20, 0.10),
                rotation=(0.05, -0.08, math.radians(35.0)))
    )

    # --- loose_rope: cut lashing hanging over the +X rim ----------------------
    objects.append(
        add_box("rope_rim", "mat_canvas", size=(0.24, 0.065, 0.035),
                location=(hw + 0.02, -0.16, BODY_H + 0.05),
                rotation=(0.0, math.radians(-30.0), math.radians(8.0)))
    )
    objects.append(
        add_box("rope_hang", "mat_canvas", size=(0.035, 0.065, 0.34),
                location=(hw + 0.115, -0.18, BODY_H - 0.14),
                rotation=(0.0, math.radians(6.0), 0.0))
    )

    # --- anchors ---------------------------------------------------------------
    objects.append(add_anchor("carry_handle", (0.0, 0.0, BODY_H + FRAME_T), anchor_type="grip"))
    objects.append(add_anchor("salvage_point", (0.0, -hd - 0.45, 0.0), anchor_type="work"))
    return objects


@register_builder("sailcloth")
def build_sailcloth(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []

    ridge_pitch = math.radians(23.0)   # yard slopes down from -X (propped) to +X

    # --- spar_stump: shattered mast stump the yard is propped on ---------------
    objects.append(
        add_cylinder("spar_stump", "mat_dark_wood",
                     radius=0.12, depth=0.95, vertices=7,
                     location=(-0.85, 0.05, 0.45),
                     rotation=(0.05, -0.06, 0.0))
    )

    # --- broken_spar: fallen yard, one end on the stump, tip on the ground -----
    # Ridge line runs along X: high end (-1.05, 0, ~1.0) on the stump, low tip
    # (1.05, 0, ~0.2) on the ground. Axis via Ry(90 + pitch).
    objects.append(
        add_cylinder("broken_spar", "mat_dark_wood",
                     radius=0.08, depth=2.35, vertices=7,
                     location=(0.0, 0.0, 0.62),
                     rotation=(0.0, math.radians(90.0) + ridge_pitch, 0.0))
    )
    # snapped collar at the low tip (jagged break)
    objects.append(
        add_cylinder("spar_snap", "mat_dark_wood",
                     radius=0.10, depth=0.20, vertices=6,
                     location=(1.0, 0.0, 0.20),
                     rotation=(0.0, math.radians(90.0) + ridge_pitch, 0.0))
    )

    # --- draped_canvas: sail hanging over the yard like a sagging ridge tent ----
    # Two big panels lean off the ridge to +-Y; canvas shorter than the spar so
    # both wooden ends stay exposed (the silhouette says "wreck", not "tent").
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"drape_panel_{i}", "mat_canvas", size=(1.65, 0.05, 1.05),
                    location=(-0.115, sy * 0.435, 0.36),
                    rotation=(sy * math.radians(56.0), ridge_pitch, 0.0))
        )
    # ridge cap strip so the two panels read as one cloth over the yard
    objects.append(
        add_box("drape_ridge", "mat_canvas", size=(1.70, 0.34, 0.07),
                location=(0.0, 0.0, 0.67),
                rotation=(0.0, ridge_pitch, 0.0))
    )
    # sagging tail sliding off the low end of the yard
    objects.append(
        add_box("drape_tail", "mat_canvas", size=(0.85, 0.60, 0.05),
                location=(0.78, 0.10, 0.32),
                rotation=(math.radians(8.0), math.radians(32.0), math.radians(-10.0)))
    )

    # --- ground_folds: heavy canvas puddled along both sides --------------------
    for i, (x, y, w, d, yaw) in enumerate((
        (-0.45, 0.80, 1.00, 0.55, 10.0),
        (0.35, 0.72, 0.65, 0.45, -18.0),
        (-0.25, -0.78, 0.90, 0.50, -8.0),
        (0.55, -0.60, 0.55, 0.40, 30.0),
    )):
        objects.append(
            add_box(f"ground_fold_{i}", "mat_canvas", size=(w, d, 0.14),
                    location=(x, y, 0.07),
                    rotation=(0.0, 0.0, math.radians(yaw)))
        )

    # --- torn_edge: ragged strips trailing past the low tip ---------------------
    for i, (x, y, yaw) in enumerate(((1.05, 0.42, 55.0), (1.15, -0.15, 80.0))):
        objects.append(
            add_box(f"torn_strip_{i}", "mat_canvas", size=(0.45, 0.13, 0.05),
                    location=(x, y, 0.05),
                    rotation=(0.0, 0.0, math.radians(yaw)))
        )

    # --- anchors -----------------------------------------------------------------
    objects.append(add_anchor("salvage_point", (0.0, 1.0, 0.0), anchor_type="work"))
    return objects
