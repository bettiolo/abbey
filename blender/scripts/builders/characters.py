"""Builder family: human placeholders — the Bellkeeper and a villager.

Low-poly placeholders (never block gameplay on art), but the SILHOUETTE is
the deliverable: at dusk these are black shapes at the edge of the light.

bellkeeper_lowpoly: hooded keeper in a heavy dark robe. Both signature items
live on the outline: the lantern-staff thrust forward (-Y) with the warm
glass swinging under its crook, and the opposite arm raised high ringing a
gold hand-bell. Anchors: flame (light, at the lantern glass), bell (bell).

villager_lowpoly: ordinary survivor — pale tunic, slight forward stoop, bare
head, dark trousers/boots, a canvas bundle hugged under one arm. Soft,
rounded, unheroic outline that cannot be confused with the Bellkeeper.
Anchor: carry (attach, at the bundle).

Budgets: character class, 1500 tris / 3 materials each.
Facing -Y (toward the iso camera's near side), like building doors.
"""

from __future__ import annotations

import math

import bpy

from asset_framework import add_anchor, register_builder
from builders._shapes import add_box, add_cone, add_cylinder


@register_builder("bellkeeper_lowpoly")
def build_bellkeeper(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []
    robe = "mat_dark_wood"

    # --- robe_skirt + chest: heavy tapered mass, no visible legs ----------------
    objects.append(
        add_cone("robe_skirt", robe, radius=0.34, radius_top=0.22, depth=0.95,
                 vertices=8, location=(0.0, 0.0, 0.475))
    )
    objects.append(
        add_box("chest", robe, size=(0.46, 0.34, 0.55), location=(0.0, -0.01, 1.2))
    )
    objects.append(  # sloped shoulder cap
        add_box("shoulders", robe, size=(0.52, 0.36, 0.16), location=(0.0, -0.01, 1.5))
    )

    # --- hood: deep cowl, peak leaning forward ----------------------------------
    objects.append(
        add_box("hood_base", robe, size=(0.30, 0.30, 0.28), location=(0.0, -0.02, 1.66))
    )
    objects.append(
        add_cone("hood_peak", robe, radius=0.17, depth=0.30, vertices=6,
                 location=(0.0, 0.03, 1.86), rotation=(math.radians(18.0), 0.0, 0.0))
    )
    objects.append(  # hood mouth: dark opening plate angled down-forward
        add_box("hood_mouth", robe, size=(0.22, 0.10, 0.20),
                location=(0.0, -0.16, 1.64), rotation=(math.radians(-14.0), 0.0, 0.0))
    )

    # --- lantern_staff: left arm thrust forward (-Y), staff with a crook ---------
    objects.append(
        add_box("arm_staff", robe, size=(0.14, 0.52, 0.15),
                location=(-0.26, -0.33, 1.37), rotation=(math.radians(-12.0), 0.0, math.radians(18.0)))
    )
    objects.append(
        add_cylinder("lantern_staff", "mat_dark_wood", radius=0.045, depth=1.85,
                     vertices=6, location=(-0.30, -0.52, 0.95),
                     rotation=(math.radians(6.0), 0.0, 0.0))
    )
    objects.append(  # crook arm the lantern hangs from
        add_box("staff_crook", "mat_dark_wood", size=(0.06, 0.28, 0.06),
                location=(-0.30, -0.62, 1.86))
    )

    # --- lantern: warm glass block swinging under the crook ----------------------
    objects.append(
        add_box("lantern_top", "mat_dark_wood", size=(0.17, 0.17, 0.05),
                location=(-0.30, -0.72, 1.70))
    )
    objects.append(
        add_box("lantern", "mat_warm_window", size=(0.14, 0.14, 0.20),
                location=(-0.30, -0.72, 1.57))
    )
    objects.append(
        add_box("lantern_base", "mat_dark_wood", size=(0.16, 0.16, 0.04),
                location=(-0.30, -0.72, 1.45))
    )

    # --- bell_arm: right arm raised high, gold hand-bell above the fist ----------
    objects.append(
        add_box("bell_arm_lower", robe, size=(0.13, 0.13, 0.42),
                location=(0.33, 0.02, 1.52), rotation=(0.0, math.radians(-24.0), 0.0))
    )
    objects.append(
        add_box("bell_arm_upper", robe, size=(0.12, 0.12, 0.40),
                location=(0.46, 0.02, 1.86), rotation=(0.0, math.radians(10.0), 0.0))
    )
    objects.append(
        add_cone("hand_bell", "mat_sacred_gold", radius=0.14, radius_top=0.06,
                 depth=0.22, vertices=8, location=(0.46, 0.02, 2.14),
                 rotation=(math.radians(8.0), math.radians(-14.0), 0.0))
    )
    objects.append(
        add_cone("bell_clapper", "mat_sacred_gold", radius=0.035, depth=0.09,
                 vertices=5, location=(0.47, 0.03, 2.0))
    )

    # --- anchors -------------------------------------------------------------------
    objects.append(add_anchor("flame", (-0.30, -0.72, 1.57), anchor_type="light"))
    objects.append(add_anchor("bell", (0.46, 0.02, 2.14), anchor_type="bell"))
    return objects


@register_builder("villager_lowpoly")
def build_villager(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []
    stoop = math.radians(8.0)  # slight forward lean (toward -Y)

    # --- legs + boots -------------------------------------------------------------
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"leg_{i}", "mat_dark_wood", size=(0.16, 0.18, 0.62),
                    location=(sx * 0.12, 0.0, 0.31))
        )
        objects.append(
            add_box(f"boot_{i}", "mat_dark_wood", size=(0.17, 0.26, 0.12),
                    location=(sx * 0.12, -0.04, 0.06))
        )

    # --- tunic: soft belted mass, stooped forward ----------------------------------
    objects.append(
        add_box("hips", "mat_canvas", size=(0.42, 0.30, 0.28), location=(0.0, 0.0, 0.72))
    )
    objects.append(
        add_box("tunic", "mat_canvas", size=(0.44, 0.32, 0.52),
                location=(0.0, -0.04, 1.08), rotation=(stoop, 0.0, 0.0))
    )
    objects.append(
        add_box("belt", "mat_dark_wood", size=(0.46, 0.34, 0.07),
                location=(0.0, -0.01, 0.88))
    )

    # --- head: bare, pale, slightly bowed -------------------------------------------
    objects.append(
        add_box("neck", "mat_bone", size=(0.13, 0.13, 0.12),
                location=(0.0, -0.08, 1.38))
    )
    objects.append(
        add_box("head", "mat_bone", size=(0.24, 0.24, 0.26),
                location=(0.0, -0.10, 1.55), rotation=(stoop, 0.0, 0.0))
    )
    objects.append(  # ragged hair cap
        add_box("hair", "mat_dark_wood", size=(0.26, 0.26, 0.09),
                location=(0.0, -0.11, 1.68), rotation=(stoop, 0.0, 0.0))
    )

    # --- arms: right hangs, left hugs the bundle -------------------------------------
    objects.append(
        add_box("arm_right", "mat_canvas", size=(0.12, 0.13, 0.52),
                location=(0.29, -0.02, 1.06), rotation=(math.radians(-4.0), 0.0, math.radians(-6.0)))
    )
    objects.append(
        add_box("hand_right", "mat_bone", size=(0.10, 0.11, 0.12),
                location=(0.31, -0.03, 0.76))
    )
    objects.append(
        add_box("arm_left", "mat_canvas", size=(0.12, 0.34, 0.13),
                location=(-0.30, -0.20, 1.12), rotation=(0.0, 0.0, math.radians(12.0)))
    )

    # --- bundle: heavy canvas sack hugged against the hip ------------------------------
    objects.append(
        add_box("bundle", "mat_canvas", size=(0.30, 0.34, 0.38),
                location=(-0.33, -0.26, 0.95), rotation=(0.06, 0.04, math.radians(14.0)))
    )
    objects.append(
        add_box("bundle_knot", "mat_dark_wood", size=(0.10, 0.10, 0.07),
                location=(-0.35, -0.28, 1.16))
    )

    # --- anchors -------------------------------------------------------------------------
    objects.append(add_anchor("carry", (-0.33, -0.26, 0.95), anchor_type="attach"))
    return objects
