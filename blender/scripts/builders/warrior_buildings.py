"""Builder family: Phase-3 warrior structures (P3-06).

warrior_lodge_t1: a stout timber longhall — chunky warm-wood body under a low
thatch gable, a plank door with an iron-banded shield hung over the lintel, a
weapon rack of spears leaning beside it, and a squat iron watch-lantern on the
ridge. Anchors: muster (work), flame (ridge lantern light).

watchtower_t1: a tall slender watchtower on four iron-braced legs — railed
lookout deck, pointed thatch cap, leaning ladder, a bright warm signal lantern
at the deck corner marking the dark edge of the light territory. Anchors:
watch_slot (deck), beacon (lantern light).

Budgets: both building class, 2600 tris / 4 materials.
"""

from __future__ import annotations

import math

import bpy

from asset_framework import add_anchor, register_builder
from builders._shapes import add_box, add_cone


@register_builder("warrior_lodge_t1")
def build_warrior_lodge(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []

    # --- body: chunky one-room hall ---------------------------------------------
    objects.append(
        add_box("body", "mat_warm_wood", size=(2.9, 2.9, 1.9), location=(0.0, 0.0, 0.95))
    )
    # corner posts to read as timber framing
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(
            add_box(f"corner_post_{i}", "mat_dark_wood", size=(0.16, 0.16, 1.95),
                    location=(sx * 1.4, sy * 1.4, 0.975))
        )

    # --- sloped_roof: shallow thatch gable (two pitched slabs meeting at a ridge) -
    pitch = math.radians(22.0)
    objects.append(
        add_box("sloped_roof_l", "mat_thatch", size=(1.7, 3.05, 0.16),
                location=(-0.72, 0.0, 2.28), rotation=(0.0, pitch, 0.0))
    )
    objects.append(
        add_box("sloped_roof_r", "mat_thatch", size=(1.7, 3.05, 0.16),
                location=(0.72, 0.0, 2.28), rotation=(0.0, -pitch, 0.0))
    )
    objects.append(
        add_box("ridge", "mat_dark_wood", size=(0.12, 3.05, 0.12), location=(0.0, 0.0, 2.60))
    )

    # --- door + iron-banded shield over the lintel (-Y front) --------------------
    objects.append(
        add_box("door", "mat_dark_wood", size=(0.9, 0.12, 1.3), location=(0.0, -1.46, 0.65))
    )
    objects.append(
        add_box("door_band", "mat_iron", size=(0.9, 0.06, 0.10), location=(0.0, -1.53, 0.95))
    )
    objects.append(
        add_box("shield", "mat_iron", size=(0.52, 0.09, 0.52), location=(0.0, -1.5, 1.62))
    )
    objects.append(
        add_box("shield_boss", "mat_dark_wood", size=(0.16, 0.06, 0.16), location=(0.0, -1.56, 1.62))
    )

    # --- weapon_rack: spears leaning by the door (+X of it) ----------------------
    objects.append(
        add_box("weapon_rack", "mat_warm_wood", size=(0.7, 0.1, 0.1), location=(0.95, -1.5, 1.15))
    )
    for i, off in enumerate((-0.18, 0.0, 0.18)):
        objects.append(
            add_box(f"spear_shaft_{i}", "mat_dark_wood", size=(0.05, 0.05, 1.55),
                    location=(0.95 + off, -1.46, 0.9),
                    rotation=(math.radians(9.0), 0.0, 0.0))
        )
        objects.append(
            add_box(f"spear_tip_{i}", "mat_iron", size=(0.07, 0.07, 0.22),
                    location=(0.95 + off, -1.34, 1.72),
                    rotation=(math.radians(9.0), 0.0, 0.0))
        )

    # --- ridge_lantern: squat iron watch-lantern on the ridge --------------------
    lz = 2.86
    objects.append(
        add_box("ridge_lantern", "mat_iron", size=(0.24, 0.24, 0.30), location=(0.55, 0.0, lz))
    )
    objects.append(
        add_box("ridge_lantern_cap", "mat_dark_wood", size=(0.28, 0.28, 0.06),
                location=(0.55, 0.0, lz + 0.18))
    )

    # --- anchors ------------------------------------------------------------------
    objects.append(add_anchor("muster", (0.0, -2.0, 0.0), anchor_type="work"))
    objects.append(add_anchor("flame", (0.55, 0.0, lz), anchor_type="light"))
    return objects


@register_builder("watchtower_t1")
def build_watchtower(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []
    leg = 0.82
    deck_z = 4.0

    # --- legs: four tall uprights ------------------------------------------------
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(
            add_box(f"leg_{i}", "mat_dark_wood", size=(0.18, 0.18, deck_z),
                    location=(sx * leg, sy * leg, deck_z / 2.0))
        )

    # --- iron_braces: X-bracing on the two camera-facing sides -------------------
    brace_len = 2.35
    for i, ang in enumerate((1, -1)):
        objects.append(  # -Y face
            add_box(f"brace_front_{i}", "mat_iron", size=(brace_len, 0.07, 0.09),
                    location=(0.0, -leg, deck_z * 0.5),
                    rotation=(0.0, ang * math.radians(52.0), 0.0))
        )
        objects.append(  # +X face
            add_box(f"brace_side_{i}", "mat_iron", size=(0.07, brace_len, 0.09),
                    location=(leg, 0.0, deck_z * 0.5),
                    rotation=(ang * math.radians(52.0), 0.0, 0.0))
        )

    # --- deck + rim beams --------------------------------------------------------
    objects.append(
        add_box("deck", "mat_warm_window", size=(1.7, 1.7, 0.14), location=(0.0, 0.0, deck_z))
    )
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"deck_beam_{i}", "mat_dark_wood", size=(1.8, 0.13, 0.16),
                    location=(0.0, sy * 0.82, deck_z - 0.11))
        )

    # --- railing: corner posts + top rails ---------------------------------------
    rail_z = deck_z + 0.5
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(
            add_box(f"rail_post_{i}", "mat_dark_wood", size=(0.10, 0.10, 0.6),
                    location=(sx * 0.78, sy * 0.78, deck_z + 0.3))
        )
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"rail_x_{i}", "mat_dark_wood", size=(1.66, 0.08, 0.08),
                    location=(0.0, sy * 0.78, rail_z))
        )
        objects.append(
            add_box(f"rail_y_{i}", "mat_dark_wood", size=(0.08, 1.66, 0.08),
                    location=(sy * 0.78, 0.0, rail_z))
        )

    # --- roof_cap: small thatch pyramid on four slim posts -----------------------
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(
            add_box(f"roof_post_{i}", "mat_dark_wood", size=(0.09, 0.09, 0.9),
                    location=(sx * 0.45, sy * 0.45, deck_z + 0.55))
        )
    objects.append(
        add_cone("roof_cap", "mat_thatch", radius=0.66, depth=0.7, vertices=4,
                 location=(0.0, 0.0, deck_z + 1.35),
                 rotation=(0.0, 0.0, math.radians(45.0)))
    )
    objects.append(
        add_box("roof_finial", "mat_iron", size=(0.08, 0.08, 0.28),
                location=(0.0, 0.0, deck_z + 1.78))
    )

    # --- ladder: leaning against the -Y front edge -------------------------------
    lean = math.radians(9.0)
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"ladder_rail_{i}", "mat_dark_wood", size=(0.07, 0.07, deck_z + 0.1),
                    location=(0.28 + sx * 0.17, -0.98, (deck_z + 0.1) / 2.0),
                    rotation=(lean, 0.0, 0.0))
        )
    for i in range(7):
        z = 0.35 + i * 0.55
        objects.append(
            add_box(f"ladder_rung_{i}", "mat_dark_wood", size=(0.38, 0.05, 0.05),
                    location=(0.28, -0.98 - (deck_z * 0.5 - z) * math.tan(lean), z))
        )

    # --- signal_lantern: bright warm beacon at the +X/-Y corner ------------------
    lx, ly = 0.76, -0.76
    objects.append(
        add_box("lantern_arm", "mat_dark_wood", size=(0.08, 0.36, 0.08),
                location=(lx, ly + 0.14, deck_z + 0.66))
    )
    objects.append(
        add_box("lantern_top", "mat_dark_wood", size=(0.18, 0.18, 0.05),
                location=(lx, ly, deck_z + 0.56))
    )
    objects.append(
        add_box("signal_lantern", "mat_warm_window", size=(0.15, 0.15, 0.24),
                location=(lx, ly, deck_z + 0.42))
    )
    objects.append(
        add_box("lantern_base", "mat_dark_wood", size=(0.17, 0.17, 0.04),
                location=(lx, ly, deck_z + 0.28))
    )

    # --- anchors -----------------------------------------------------------------
    objects.append(add_anchor("watch_slot", (0.0, 0.0, deck_z + 0.07), anchor_type="work"))
    objects.append(add_anchor("beacon", (lx, ly, deck_z + 0.42), anchor_type="light"))
    return objects
