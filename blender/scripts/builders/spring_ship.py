"""Builder: spring_ship_t1 — the Phase-3 victory ship (P3-14).

A small single-masted coastal ship rebuilt from wreck timber, sitting on a
timber launch cradle: a long dark keel under a curved clinker hull (overlapping
plank strakes) with a raised prow at the bow, a planked deck, a stubby central
mast carrying a crossed spar with a furled canvas sail lashed to it, and coiled
rope rigging fore and aft. From the iso camera the read is: long dark hull on
skids, bright canvas bundle up the mast = "the ship that carries them out in
spring".

Envelope: X = along keel (bow at +X), Y = beam, Z = height. Footprint 6 x 3 x 3.4.
Anchors: stage_keel/stage_hull/stage_rigging (work), stage_sail (flag).
Budget: 2600 tris / 4 materials (mat_dark_wood, mat_warm_wood, mat_canvas, mat_iron).
"""

from __future__ import annotations

import math

import bpy

from asset_framework import add_anchor, register_builder
from builders._shapes import add_box, add_cylinder, add_torus


def _stay_xz(
    name: str,
    a: tuple[float, float, float],
    b: tuple[float, float, float],
    thick: float = 0.035,
    material: str = "mat_dark_wood",
) -> bpy.types.Object:
    """A taut rope run between two points in a vertical X-Z plane (y ~ const)."""
    ax, _ay, az = a
    bx, _by, bz = b
    dx, dz = bx - ax, bz - az
    length = math.hypot(dx, dz)
    mid = ((a[0] + b[0]) / 2.0, (a[1] + b[1]) / 2.0, (a[2] + b[2]) / 2.0)
    return add_box(
        name, material,
        size=(length, thick, thick), location=mid,
        rotation=(0.0, math.atan2(-dz, dx), 0.0),
    )


def _stay_yz(
    name: str,
    a: tuple[float, float, float],
    b: tuple[float, float, float],
    thick: float = 0.035,
    material: str = "mat_dark_wood",
) -> bpy.types.Object:
    """A taut rope run between two points in a vertical Y-Z plane (x ~ const)."""
    _ax, ay, az = a
    _bx, by, bz = b
    dy, dz = by - ay, bz - az
    length = math.hypot(dy, dz)
    mid = ((a[0] + b[0]) / 2.0, (a[1] + b[1]) / 2.0, (a[2] + b[2]) / 2.0)
    return add_box(
        name, material,
        size=(thick, length, thick), location=mid,
        rotation=(math.atan2(dz, dy), 0.0, 0.0),
    )


@register_builder("spring_ship_t1")
def build_spring_ship(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []

    # --- launch_cradle: skids + sleepers + angled shores on the ground -----------
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"cradle_skid_{i}", "mat_warm_wood",
                    size=(4.8, 0.24, 0.22), location=(0.0, sy * 0.72, 0.11))
        )
    for i, sx in enumerate((-1.6, 0.0, 1.6)):
        objects.append(
            add_box(f"cradle_sleeper_{i}", "mat_warm_wood",
                    size=(0.34, 2.0, 0.22), location=(sx, 0.0, 0.11))
        )
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(
            add_box(f"cradle_shore_{i}", "mat_warm_wood",
                    size=(0.14, 0.14, 0.95), location=(sx * 1.45, sy * 0.86, 0.6),
                    rotation=(-sy * 0.24, 0.0, 0.0))
        )

    # --- keel: long dark timber the hull is rebuilt on ---------------------------
    objects.append(
        add_box("keel", "mat_dark_wood", size=(4.6, 0.30, 0.26), location=(0.0, 0.0, 0.36))
    )

    # --- clinker_hull: garboard bottom + overlapping plank strakes ---------------
    objects.append(
        add_box("hull_bottom", "mat_dark_wood", size=(4.3, 1.5, 0.46), location=(0.0, 0.0, 0.70))
    )
    strakes = (
        # (length, y, z, tilt, material)
        (4.20, 0.72, 0.90, 0.30, "mat_dark_wood"),
        (4.05, 0.85, 1.15, 0.34, "mat_dark_wood"),
        (3.90, 0.95, 1.38, 0.30, "mat_warm_wood"),  # sheer strake / gunwale
    )
    for i, sy in enumerate((1, -1)):
        for r, (length, y, z, tilt, mat) in enumerate(strakes):
            objects.append(
                add_box(f"clinker_hull_{i}_{r}", mat,
                        size=(length, 0.13, 0.32), location=(0.0, sy * y, z),
                        rotation=(sy * tilt, 0.0, 0.0))
            )
    # iron rivet bands wrapping the hull amidships and fore/aft
    for i, sx in enumerate((-1.4, 0.0, 1.4)):
        objects.append(
            add_box(f"hull_band_{i}", "mat_iron",
                    size=(0.10, 2.05, 0.10), location=(sx, 0.0, 1.02))
        )

    # --- raised_prow: leaning stem post + head at the bow, low stern post --------
    objects.append(
        add_box("raised_prow", "mat_dark_wood", size=(0.28, 0.34, 1.5),
                location=(2.25, 0.0, 1.35), rotation=(0.0, -0.40, 0.0))
    )
    objects.append(
        add_box("prow_head", "mat_dark_wood", size=(0.30, 0.30, 0.42),
                location=(2.60, 0.0, 1.98), rotation=(0.0, -0.40, 0.0))
    )
    objects.append(
        add_box("stern_post", "mat_dark_wood", size=(0.26, 0.32, 0.95),
                location=(-2.15, 0.0, 1.5), rotation=(0.0, 0.22, 0.0))
    )

    # --- deck: warm planked deck with dark plank seams ---------------------------
    deck_top = 1.51
    objects.append(
        add_box("deck", "mat_warm_wood", size=(3.8, 1.7, 0.12), location=(0.0, 0.0, deck_top - 0.06))
    )
    for i, sy in enumerate((-0.8, -0.4, 0.4, 0.8)):
        objects.append(
            add_box(f"deck_plank_{i}", "mat_dark_wood",
                    size=(3.8, 0.04, 0.03), location=(0.0, sy, deck_top))
        )

    # --- mast: stubby central mast with iron band + partner block ----------------
    mast_base = deck_top
    mast_top = mast_base + 1.6
    objects.append(
        add_box("mast_partner", "mat_warm_wood", size=(0.32, 0.32, 0.20),
                location=(0.0, 0.0, mast_base + 0.06))
    )
    objects.append(
        add_cylinder("mast", "mat_dark_wood", radius=0.10, depth=1.6, vertices=8,
                     location=(0.0, 0.0, (mast_base + mast_top) / 2.0))
    )
    objects.append(
        add_cylinder("mast_band", "mat_iron", radius=0.12, depth=0.08, vertices=8,
                     location=(0.0, 0.0, mast_base + 0.55))
    )

    # --- spar: crossed yard near the mast head, along the beam -------------------
    spar_z = mast_top - 0.30
    objects.append(
        add_cylinder("spar", "mat_dark_wood", radius=0.07, depth=2.0, vertices=7,
                     location=(0.02, 0.0, spar_z), rotation=(math.radians(90.0), 0.0, 0.0))
    )

    # --- furled_sail: canvas roll lashed under the spar with iron ties -----------
    objects.append(
        add_cylinder("furled_sail", "mat_canvas", radius=0.15, depth=1.7, vertices=8,
                     location=(0.02, 0.0, spar_z - 0.14), rotation=(math.radians(90.0), 0.0, 0.0))
    )
    for i, sy in enumerate((-0.55, 0.0, 0.55)):
        objects.append(
            add_box(f"sail_tie_{i}", "mat_iron",
                    size=(0.34, 0.05, 0.34), location=(0.02, sy, spar_z - 0.14))
        )

    # --- rigging: fore/back stays, shrouds, a coiled rope + deck cleats ----------
    mast_head = (0.0, 0.0, mast_top - 0.05)
    objects.append(_stay_xz("rigging_forestay", mast_head, (2.45, 0.0, 1.95)))
    objects.append(_stay_xz("rigging_backstay", mast_head, (-2.10, 0.0, 1.55)))
    for i, sy in enumerate((1, -1)):
        objects.append(
            _stay_yz(f"rigging_shroud_{i}", (0.0, 0.0, spar_z), (0.0, sy * 1.0, deck_top - 0.02))
        )
    objects.append(
        add_torus("rigging_coil", "mat_dark_wood",
                  major_radius=0.18, minor_radius=0.05, major_segments=8, minor_segments=4,
                  location=(-1.25, 0.6, deck_top + 0.05), rotation=(math.radians(90.0), 0.0, 0.0))
    )
    for i, (cx, cy) in enumerate(((1.3, -0.62), (-1.5, 0.55))):
        objects.append(
            add_box(f"cleat_{i}", "mat_iron",
                    size=(0.22, 0.09, 0.09), location=(cx, cy, deck_top + 0.05))
        )

    # --- anchors: build stages (work) + the sail flag ----------------------------
    objects.append(add_anchor("stage_keel", (-1.3, 0.0, 0.5), anchor_type="work"))
    objects.append(add_anchor("stage_hull", (1.0, 1.15, 0.95), anchor_type="work"))
    objects.append(add_anchor("stage_rigging", (0.0, 0.0, deck_top + 0.1), anchor_type="work"))
    objects.append(add_anchor("stage_sail", (0.02, 0.0, spar_z), anchor_type="flag"))
    return objects
