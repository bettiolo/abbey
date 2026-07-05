"""Builder family: human placeholders — the Bellkeeper and a villager.

Readable placeholders (never block gameplay on art), but the SILHOUETTE is the
deliverable: at dusk these are black shapes at the edge of the light.

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
from builders._shapes import add_box, add_cone, add_cylinder, add_icosphere, add_torus


def _scaled(obj: bpy.types.Object, scale: tuple[float, float, float]) -> bpy.types.Object:
    """Apply object-level squash/stretch to keep the shared primitives reusable."""
    obj.scale = scale
    return obj


@register_builder("bellkeeper_lowpoly")
def build_bellkeeper(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []
    robe = "mat_dark_wood"

    # --- robe_skirt + chest: heavy tapered mass, no visible legs ----------------
    objects.append(
        add_cone("robe_skirt", robe, radius=0.34, radius_top=0.22, depth=0.95,
                 vertices=14, location=(0.0, 0.0, 0.475))
    )
    objects.append(
        _scaled(
            add_cylinder("chest", robe, radius=0.26, depth=0.58, vertices=12,
                         location=(0.0, -0.01, 1.2)),
            (1.0, 0.72, 1.0),
        )
    )
    objects.append(
        _scaled(
            add_icosphere("shoulders", robe, radius=0.30, subdivisions=1,
                          location=(0.0, -0.02, 1.48)),
            (1.12, 0.72, 0.32),
        )
    )

    # --- hood: deep rounded cowl, peak leaning forward -------------------------
    objects.append(
        _scaled(
            add_icosphere("hood", robe, radius=0.23, subdivisions=2,
                          location=(0.0, -0.06, 1.68),
                          rotation=(math.radians(4.0), 0.0, 0.0)),
            (1.0, 0.9, 1.05),
        )
    )
    objects.append(
        add_cone("hood_peak", robe, radius=0.17, radius_top=0.03, depth=0.36,
                 vertices=10, location=(0.0, -0.10, 1.87),
                 rotation=(math.radians(22.0), 0.0, 0.0))
    )
    objects.append(
        _scaled(
            add_icosphere("hood_mouth", robe, radius=0.16, subdivisions=1,
                          location=(0.0, -0.20, 1.63),
                          rotation=(math.radians(-10.0), 0.0, 0.0)),
            (0.9, 0.38, 0.7),
        )
    )

    # --- lantern_staff: left arm thrust forward (-Y), staff with a crook --------
    objects.append(
        add_cylinder("arm_staff", robe, radius=0.065, depth=0.58, vertices=8,
                     location=(-0.25, -0.32, 1.36),
                     rotation=(math.radians(76.0), math.radians(0.0), math.radians(12.0)))
    )
    objects.append(
        add_cylinder("lantern_staff", "mat_dark_wood", radius=0.045, depth=1.85,
                     vertices=8, location=(-0.30, -0.52, 0.95),
                     rotation=(math.radians(6.0), 0.0, 0.0))
    )
    objects.append(
        add_torus("staff_crook", "mat_dark_wood", major_radius=0.12, minor_radius=0.022,
                  major_segments=10, minor_segments=4, location=(-0.30, -0.64, 1.84),
                  rotation=(math.radians(90.0), 0.0, 0.0))
    )

    # --- lantern: warm rounded glass swinging under the crook -------------------
    objects.append(
        add_cylinder("lantern_top", "mat_dark_wood", radius=0.09, depth=0.045,
                     vertices=8, location=(-0.30, -0.72, 1.70))
    )
    objects.append(
        _scaled(
            add_icosphere("lantern", "mat_warm_window", radius=0.11, subdivisions=1,
                          location=(-0.30, -0.72, 1.57)),
            (0.9, 0.9, 1.25),
        )
    )
    objects.append(
        add_cylinder("lantern_base", "mat_dark_wood", radius=0.085, depth=0.04,
                     vertices=8, location=(-0.30, -0.72, 1.44))
    )

    # --- bell_arm: right arm raised high, gold hand-bell above the fist ----------
    objects.append(
        add_cylinder("bell_arm_lower", robe, radius=0.06, depth=0.46, vertices=8,
                     location=(0.32, 0.02, 1.54),
                     rotation=(0.0, math.radians(-26.0), 0.0))
    )
    objects.append(
        add_cylinder("bell_arm_upper", robe, radius=0.055, depth=0.42, vertices=8,
                     location=(0.45, 0.02, 1.87),
                     rotation=(0.0, math.radians(10.0), 0.0))
    )
    objects.append(
        add_cone("hand_bell", "mat_sacred_gold", radius=0.14, radius_top=0.06,
                 depth=0.22, vertices=12, location=(0.46, 0.02, 2.14),
                 rotation=(math.radians(8.0), math.radians(-14.0), 0.0))
    )
    objects.append(
        add_torus("bell_rim", "mat_sacred_gold", major_radius=0.105, minor_radius=0.012,
                  major_segments=12, minor_segments=4, location=(0.46, 0.02, 2.04),
                  rotation=(math.radians(8.0), math.radians(-14.0), 0.0))
    )
    objects.append(
        add_icosphere("bell_clapper", "mat_sacred_gold", radius=0.035, subdivisions=1,
                      location=(0.47, 0.03, 2.0))
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
            add_cylinder(f"legs_{i}", "mat_dark_wood", radius=0.065, depth=0.62,
                         vertices=8, location=(sx * 0.12, 0.0, 0.31))
        )
        objects.append(
            _scaled(
                add_icosphere(f"boots_{i}", "mat_dark_wood", radius=0.12, subdivisions=1,
                              location=(sx * 0.12, -0.04, 0.07)),
                (0.75, 1.1, 0.42),
            )
        )

    # --- tunic: soft belted mass, stooped forward -------------------------------
    objects.append(
        _scaled(
            add_icosphere("hips", "mat_bone", radius=0.23, subdivisions=1,
                          location=(0.0, 0.0, 0.72)),
            (1.05, 0.74, 0.62),
        )
    )
    objects.append(
        _scaled(
            add_cone("tunic", "mat_bone", radius=0.24, radius_top=0.20,
                     depth=0.58, vertices=12, location=(0.0, -0.04, 1.08),
                     rotation=(stoop, 0.0, 0.0)),
            (0.96, 0.72, 1.0),
        )
    )
    objects.append(
        _scaled(
            add_torus("belt", "mat_dark_wood", major_radius=0.23, minor_radius=0.025,
                      major_segments=12, minor_segments=4, location=(0.0, -0.01, 0.89)),
            (0.94, 0.72, 1.0),
        )
    )

    # --- head: bare, pale, slightly bowed -------------------------------------------
    objects.append(
        add_cylinder("neck", "mat_bone", radius=0.065, depth=0.12, vertices=8,
                     location=(0.0, -0.08, 1.38))
    )
    objects.append(
        _scaled(
            add_icosphere("head", "mat_bone", radius=0.16, subdivisions=2,
                          location=(0.0, -0.10, 1.55), rotation=(stoop, 0.0, 0.0)),
            (0.85, 0.82, 1.0),
        )
    )
    objects.append(
        _scaled(
            add_icosphere("hair", "mat_dark_wood", radius=0.14, subdivisions=1,
                          location=(0.0, -0.13, 1.66), rotation=(stoop, 0.0, 0.0)),
            (0.95, 0.85, 0.36),
        )
    )

    # --- arms: right hangs, left hugs the bundle -------------------------------------
    objects.append(
        add_cylinder("arms_right", "mat_bone", radius=0.055, depth=0.50, vertices=8,
                     location=(0.28, -0.02, 1.06),
                     rotation=(math.radians(-4.0), 0.0, math.radians(-6.0)))
    )
    objects.append(
        add_icosphere("hand_right", "mat_bone", radius=0.065, subdivisions=1,
                      location=(0.31, -0.03, 0.77))
    )
    objects.append(
        add_cylinder("arms_left", "mat_bone", radius=0.055, depth=0.36, vertices=8,
                     location=(-0.30, -0.20, 1.12),
                     rotation=(math.radians(86.0), 0.0, math.radians(12.0)))
    )

    # --- bundle: heavy canvas sack hugged against the hip ------------------------------
    objects.append(
        _scaled(
            add_icosphere("bundle", "mat_canvas", radius=0.22, subdivisions=2,
                          location=(-0.33, -0.26, 0.95),
                          rotation=(0.06, 0.04, math.radians(14.0))),
            (0.70, 0.9, 1.05),
        )
    )
    objects.append(
        add_icosphere("bundle_knot", "mat_dark_wood", radius=0.055, subdivisions=1,
                      location=(-0.35, -0.28, 1.16))
    )

    # --- anchors -------------------------------------------------------------------------
    objects.append(add_anchor("carry", (-0.33, -0.26, 0.95), anchor_type="attach"))
    return objects
