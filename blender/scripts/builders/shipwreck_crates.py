"""Builder family: shipwreck crates — the wreck is the settlement's first mine.

shipwreck_crate_closed: chunky closed cargo crate. Warm plank body, thick dark
corner frames (strong silhouette from the iso camera), pale rope lashing with a
knot on top. Reads as "salvage me" at a distance and in grayscale.

Anchors: carry_handle (grip) — where the Bellkeeper/villagers grab it.
Budget: 400 tris / 3 materials (mat_warm_wood, mat_dark_wood, mat_canvas).
"""

from __future__ import annotations

import math

import bpy

from asset_framework import add_anchor, register_builder
from builders._shapes import add_box

BODY_W = 0.72   # X
BODY_D = 0.72   # Y
BODY_H = 0.60   # Z
FRAME_T = 0.10  # frame thickness


@register_builder("shipwreck_crate_closed")
def build_crate_closed(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []
    hw, hd, hz = BODY_W / 2.0, BODY_D / 2.0, BODY_H / 2.0

    # --- crate_body ---------------------------------------------------------
    objects.append(
        add_box(
            "crate_body",
            "mat_warm_wood",
            size=(BODY_W, BODY_D, BODY_H),
            location=(0.0, 0.0, hz),
        )
    )

    # --- corner_frames: 4 vertical posts + 8 horizontal rails ---------------
    post_h = BODY_H + FRAME_T * 0.6
    for i, (sx, sy) in enumerate([(1, 1), (1, -1), (-1, 1), (-1, -1)]):
        objects.append(
            add_box(
                f"frame_post_{i:02d}",
                "mat_dark_wood",
                size=(FRAME_T, FRAME_T, post_h),
                location=(sx * hw, sy * hd, post_h / 2.0),
            )
        )
    rail_len_x = BODY_W + FRAME_T * 0.5
    rail_len_y = BODY_D + FRAME_T * 0.5
    for j, z in enumerate((FRAME_T * 0.45, BODY_H)):
        for i, sy in enumerate((1, -1)):
            objects.append(
                add_box(
                    f"frame_rail_x_{j}{i}",
                    "mat_dark_wood",
                    size=(rail_len_x, FRAME_T * 0.9, FRAME_T * 0.9),
                    location=(0.0, sy * hd, z),
                )
            )
        for i, sx in enumerate((1, -1)):
            objects.append(
                add_box(
                    f"frame_rail_y_{j}{i}",
                    "mat_dark_wood",
                    size=(FRAME_T * 0.9, rail_len_y, FRAME_T * 0.9),
                    location=(sx * hw, 0.0, z),
                )
            )

    # --- rope_lashing: straps over the top + down the sides, plus a knot ----
    rope_t = 0.035   # strap thickness
    rope_w = 0.065   # strap width
    lift = 0.02      # sits just proud of the wood
    # Two parallel bands wrapping over the top and down the ±X sides.
    for i, y0 in enumerate((-0.17, 0.17)):
        objects.append(
            add_box(
                f"rope_band_x_{i:02d}_top",
                "mat_canvas",
                size=(BODY_W + 2.0 * (lift + rope_t), rope_w, rope_t),
                location=(0.0, y0, BODY_H + lift),
            )
        )
        for k, sx in enumerate((1, -1)):
            objects.append(
                add_box(
                    f"rope_band_x_{i:02d}_side_{k}",
                    "mat_canvas",
                    size=(rope_t, rope_w, BODY_H + lift),
                    location=(sx * (hw + lift + rope_t * 0.5), y0, (BODY_H + lift) / 2.0),
                )
            )
    # One crossing band wrapping over the top and down the ±Y sides.
    objects.append(
        add_box(
            "rope_band_y_top",
            "mat_canvas",
            size=(rope_w, BODY_D + 2.0 * (lift + rope_t), rope_t),
            location=(0.0, 0.0, BODY_H + lift + rope_t),
        )
    )
    for k, sy in enumerate((1, -1)):
        objects.append(
            add_box(
                f"rope_band_y_side_{k}",
                "mat_canvas",
                size=(rope_w, rope_t, BODY_H + lift),
                location=(0.0, sy * (hd + lift + rope_t * 0.5), (BODY_H + lift) / 2.0),
            )
        )
    objects.append(
        add_box(
            "rope_knot",
            "mat_canvas",
            size=(0.16, 0.11, 0.08),
            location=(0.0, 0.0, BODY_H + lift + rope_t),
            rotation=(0.0, 0.0, math.radians(25.0)),
        )
    )

    # --- anchors --------------------------------------------------------------
    objects.append(
        add_anchor("carry_handle", (0.0, 0.0, BODY_H + FRAME_T), anchor_type="grip")
    )

    return objects
