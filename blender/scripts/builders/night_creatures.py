"""Builder family: night creatures — pale hound, drowned sailor, lantern moth.

ART_BIBLE night assets: silhouettes first; glowing eyes sparingly; thin limbs;
unnatural posture; shapes visible at the edge of light.

pale_hound_lowpoly: the black hound's wrong, pallid cousin. Deliberately the
black hound's opposite in every line: where the black hound sags (heavy low
chest, lowered head, hanging tail), this thing stretches — stilted too-long
legs, spine arched into a hump, head held unnaturally high on a thin neck,
stiff straight tail. Bone-white so it reads at night and in grayscale as a
pale shape; rib hollows are dark slats (inverting the black hound's pale
ribs); two ember pinprick eyes. Anchor: mouth (interaction).

drowned_sailor_lowpoly: hunched humanoid in a waterlogged glossy coat, head
slumped onto the chest, arms hanging far too long ending in black void hands,
kelp trailing from shoulders and wrists. Anchor: grab (interaction).

lantern_moth_lowpoly: hand-sized moth drawn to the lanterns — fat warm-glowing
body, two big pale swept-back upper wings + two small lower wings in a shallow
V, thin antennae. Wings first: it must read at a few pixels. Anchor: glow.

Budgets: character class, 1500 tris / 3 materials each. Hound faces +X like
black_hound_lowpoly; sailor faces -Y like the human characters.
"""

from __future__ import annotations

import math

import bpy

from asset_framework import add_anchor, register_builder
from builders._shapes import add_box, add_cone


@register_builder("pale_hound_lowpoly")
def build_pale_hound(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []
    bone = "mat_bone"

    # --- stilt_legs: too long, too thin, slightly splayed ----------------------
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"leg_front_{i}", bone, size=(0.11, 0.11, 1.05),
                    location=(0.42, sy * 0.20, 0.525),
                    rotation=(sy * math.radians(-3.0), math.radians(4.0), 0.0))
        )
        objects.append(
            add_box(f"leg_rear_{i}", bone, size=(0.11, 0.11, 1.0),
                    location=(-0.52, sy * 0.20, 0.50),
                    rotation=(sy * math.radians(-3.0), math.radians(-6.0), 0.0))
        )

    # --- arched_body: spine humped UP (the black hound's sag, inverted) --------
    objects.append(
        add_box("body_front", bone, size=(0.55, 0.34, 0.34),
                location=(0.30, 0.0, 1.16), rotation=(0.0, math.radians(18.0), 0.0))
    )
    objects.append(
        add_box("body_hump", bone, size=(0.55, 0.30, 0.40),
                location=(-0.10, 0.0, 1.30))
    )
    objects.append(
        add_box("body_rear", bone, size=(0.50, 0.30, 0.30),
                location=(-0.50, 0.0, 1.16), rotation=(0.0, math.radians(-16.0), 0.0))
    )

    # --- rib_hollows: dark sunken slats on the visible flank --------------------
    for i in range(3):
        objects.append(
            add_box(f"rib_hollow_{i}", "mat_nightmare_black",
                    size=(0.055, 0.02, 0.24),
                    location=(0.30 - i * 0.14, -0.165, 1.16 + i * 0.035),
                    rotation=(0.0, math.radians(10.0), 0.0))
        )

    # --- stretched_neck + high_head: held far too high, nose tipped up ----------
    objects.append(
        add_box("neck", bone, size=(0.14, 0.14, 0.62),
                location=(0.60, 0.0, 1.56), rotation=(0.0, math.radians(24.0), 0.0))
    )
    objects.append(
        add_box("head", bone, size=(0.34, 0.20, 0.16),
                location=(0.76, 0.0, 1.86), rotation=(0.0, math.radians(-12.0), 0.0))
    )
    objects.append(
        add_box("muzzle", bone, size=(0.22, 0.13, 0.10),
                location=(0.94, 0.0, 1.90), rotation=(0.0, math.radians(-16.0), 0.0))
    )
    # pinned-back thin ears
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_cone(f"ear_{i}", bone, radius=0.05, depth=0.20, vertices=4,
                     location=(0.62, sy * 0.09, 1.94),
                     rotation=(sy * math.radians(10.0), math.radians(-55.0), 0.0))
        )

    # --- ember_eyes: two pinpricks, the only light on it -------------------------
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"eye_{i}", "mat_ember", size=(0.035, 0.035, 0.035),
                    location=(0.86, sy * 0.085, 1.90))
        )

    # --- stiff_tail: dead straight, held level — nothing natural wags like that --
    objects.append(
        add_box("stiff_tail", bone, size=(0.70, 0.07, 0.07),
                location=(-1.05, 0.0, 1.24), rotation=(0.0, math.radians(-4.0), 0.0))
    )

    # --- anchors ------------------------------------------------------------------
    objects.append(add_anchor("mouth", (1.05, 0.0, 1.88), anchor_type="interaction"))
    return objects


@register_builder("drowned_sailor_lowpoly")
def build_drowned_sailor(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []
    coat = "mat_wet_stone"
    void = "mat_nightmare_black"
    hunch = math.radians(22.0)  # heavy forward slump toward -Y

    # --- legs: stiff, slightly apart, black and waterlogged ----------------------
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"leg_{i}", void, size=(0.16, 0.18, 0.66),
                    location=(sx * 0.14, 0.02, 0.33),
                    rotation=(math.radians(-3.0), 0.0, sx * math.radians(-2.0)))
        )

    # --- hunched_torso: glossy coat mass, bent forward -----------------------------
    objects.append(
        add_box("hips", coat, size=(0.46, 0.34, 0.30), location=(0.0, 0.0, 0.78))
    )
    objects.append(
        add_box("hunched_torso", coat, size=(0.52, 0.38, 0.62),
                location=(0.0, -0.12, 1.14), rotation=(hunch, 0.0, 0.0))
    )
    objects.append(  # rounded upper back — the hump is the silhouette
        add_box("back_hump", coat, size=(0.44, 0.34, 0.26),
                location=(0.0, -0.16, 1.44), rotation=(hunch * 1.4, 0.0, 0.0))
    )

    # --- slumped_head: dropped onto the chest, face invisible ----------------------
    objects.append(
        add_box("slumped_head", void, size=(0.24, 0.24, 0.24),
                location=(0.0, -0.38, 1.42), rotation=(math.radians(38.0), 0.0, 0.0))
    )
    objects.append(  # sodden hood/collar over it
        add_box("collar", coat, size=(0.34, 0.28, 0.14),
                location=(0.0, -0.30, 1.54), rotation=(math.radians(30.0), 0.0, 0.0))
    )

    # --- long_arms: hanging far past the knees, dead straight ----------------------
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"long_arm_{i}", coat, size=(0.13, 0.14, 0.95),
                    location=(sx * 0.36, -0.14, 0.92),
                    rotation=(math.radians(6.0), 0.0, sx * math.radians(5.0)))
        )
        # --- void_hands: too big, too dark ----------------------------------------
        objects.append(
            add_box(f"void_hand_{i}", void, size=(0.15, 0.16, 0.22),
                    location=(sx * 0.41, -0.18, 0.35))
        )

    # --- kelp_strands: trailing from shoulders and wrists ---------------------------
    for i, (x, y, z, h, rx, ry) in enumerate((
        (0.30, -0.28, 1.20, 0.55, 8.0, 6.0),
        (-0.33, -0.05, 1.25, 0.48, -6.0, -8.0),
        (0.41, -0.24, 0.42, 0.40, 10.0, 4.0),
        (-0.10, 0.16, 1.50, 0.60, -12.0, 0.0),
    )):
        objects.append(
            add_box(f"kelp_{i}", "mat_foliage", size=(0.05, 0.03, h),
                    location=(x, y, z - h / 2.0),
                    rotation=(math.radians(rx), math.radians(ry), 0.0))
        )

    # --- anchors ---------------------------------------------------------------------
    objects.append(add_anchor("grab", (0.0, -0.45, 1.0), anchor_type="interaction"))
    return objects


@register_builder("lantern_moth_lowpoly")
def build_lantern_moth(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []

    # Hovering: the pivot normalization drops the lowest wing tip to z=0;
    # the game parents it to a flight path anyway.
    body_z = 0.30

    # --- glow_body: fat warm segments, the lantern it is named for ---------------
    objects.append(
        add_box("glow_body", "mat_warm_window", size=(0.16, 0.34, 0.16),
                location=(0.0, 0.0, body_z))
    )
    objects.append(
        add_box("glow_tail", "mat_warm_window", size=(0.11, 0.22, 0.11),
                location=(0.0, 0.24, body_z - 0.02), rotation=(math.radians(8.0), 0.0, 0.0))
    )
    objects.append(  # dark little head
        add_box("head", "mat_iron", size=(0.10, 0.10, 0.09),
                location=(0.0, -0.20, body_z + 0.02))
    )

    # --- upper_wings: big swept-back deltas in a shallow V ------------------------
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"upper_wing_{i}", "mat_bone", size=(0.46, 0.30, 0.02),
                    location=(sx * 0.27, -0.02, body_z + 0.13),
                    rotation=(math.radians(-6.0), sx * math.radians(-28.0), sx * math.radians(-18.0)))
        )
        # notched tip: small overlapped quad past the leading edge
        objects.append(
            add_box(f"upper_wing_tip_{i}", "mat_bone", size=(0.18, 0.16, 0.02),
                    location=(sx * 0.47, -0.08, body_z + 0.24),
                    rotation=(math.radians(-6.0), sx * math.radians(-28.0), sx * math.radians(-32.0)))
        )

    # --- lower_wings: smaller, tucked behind --------------------------------------
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"lower_wing_{i}", "mat_bone", size=(0.26, 0.22, 0.02),
                    location=(sx * 0.17, 0.17, body_z + 0.06),
                    rotation=(math.radians(4.0), sx * math.radians(-20.0), sx * math.radians(14.0)))
        )

    # --- antennae: thin forward feelers --------------------------------------------
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"antenna_{i}", "mat_iron", size=(0.02, 0.20, 0.02),
                    location=(sx * 0.05, -0.32, body_z + 0.09),
                    rotation=(math.radians(-28.0), 0.0, sx * math.radians(-14.0)))
        )

    # --- anchors ---------------------------------------------------------------------
    objects.append(add_anchor("glow", (0.0, 0.0, body_z), anchor_type="light"))
    return objects
