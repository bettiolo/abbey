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
from builders._shapes import add_box, add_cone, add_torus


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


# ===========================================================================
# P3-11 consequence nightmares — armed by the settlement's moral state.
# Silhouettes first; each must read as a distinct dark shape at iso distance.
# Character budget: 1500 tris / 3 materials each.
# ===========================================================================


@register_builder("hunger_wight_lowpoly")
def build_hunger_wight(spec: dict) -> list[bpy.types.Object]:
    """The starved dead: ash-grey, ribs over a caved chest, a swollen belly,
    spindle limbs, a lolling hollow head, both arms reaching. Faces -Y like the
    human characters. Anchor: mouth (interaction)."""
    objects: list[bpy.types.Object] = []
    ash = "mat_ash"
    black = "mat_nightmare_black"

    # --- spindle_legs: too thin, knees slightly bent -----------------------------
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"thigh_{i}", ash, size=(0.13, 0.15, 0.55),
                    location=(sx * 0.15, 0.0, 0.62),
                    rotation=(math.radians(4.0), 0.0, sx * math.radians(-3.0)))
        )
        objects.append(
            add_box(f"shin_{i}", ash, size=(0.11, 0.12, 0.55),
                    location=(sx * 0.17, 0.03, 0.28),
                    rotation=(math.radians(-6.0), 0.0, 0.0))
        )

    # --- distended_belly: the one full-looking mass on a starved frame ------------
    objects.append(
        add_box("distended_belly", ash, size=(0.44, 0.42, 0.40),
                location=(0.0, 0.06, 1.02))
    )
    # --- caved_chest: narrow, sunken, tipped forward ------------------------------
    objects.append(
        add_box("caved_chest", ash, size=(0.40, 0.24, 0.40),
                location=(0.0, -0.04, 1.42), rotation=(math.radians(10.0), 0.0, 0.0))
    )
    objects.append(
        add_box("shoulders", ash, size=(0.50, 0.20, 0.16),
                location=(0.0, -0.02, 1.64))
    )

    # --- ribs: pale bars across the sunken chest ----------------------------------
    for i in range(3):
        objects.append(
            add_box(f"rib_{i}", "mat_bone", size=(0.42, 0.03, 0.05),
                    location=(0.0, -0.20, 1.30 + i * 0.11),
                    rotation=(math.radians(8.0), 0.0, 0.0))
        )

    # --- hollow_head: sunken, tipped, black pits for eyes -------------------------
    objects.append(
        add_box("hollow_head", ash, size=(0.24, 0.26, 0.28),
                location=(0.0, 0.0, 1.86), rotation=(math.radians(14.0), 0.0, 0.0))
    )
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"eye_pit_{i}", black, size=(0.06, 0.05, 0.07),
                    location=(sx * 0.07, -0.12, 1.88))
        )
    objects.append(
        add_box("jaw", ash, size=(0.16, 0.14, 0.09),
                location=(0.0, -0.10, 1.74), rotation=(math.radians(20.0), 0.0, 0.0))
    )

    # --- reaching_arms: hanging forward, long, grasping ---------------------------
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"upper_arm_{i}", ash, size=(0.11, 0.12, 0.44),
                    location=(sx * 0.30, -0.14, 1.44),
                    rotation=(math.radians(38.0), 0.0, sx * math.radians(6.0)))
        )
        objects.append(
            add_box(f"forearm_{i}", ash, size=(0.10, 0.11, 0.44),
                    location=(sx * 0.34, -0.40, 1.18),
                    rotation=(math.radians(64.0), 0.0, 0.0))
        )
        objects.append(
            add_box(f"hand_{i}", black, size=(0.11, 0.13, 0.12),
                    location=(sx * 0.35, -0.52, 0.98))
        )

    objects.append(add_anchor("mouth", (0.0, -0.16, 1.78), anchor_type="interaction"))
    return objects


@register_builder("grave_crawler_lowpoly")
def build_grave_crawler(spec: dict) -> list[bpy.types.Object]:
    """What claws out of a mass grave: low and long, dragging flat on splayed
    clawing limbs, grave-dirt over a broken back, a split low head, pale bone
    spurs. Crawls toward -Y. Anchor: mouth (interaction)."""
    objects: list[bpy.types.Object] = []
    dirt = "mat_dirt"
    black = "mat_nightmare_black"

    # --- low_body: long spine kept close to the ground ---------------------------
    objects.append(
        add_box("low_body", black, size=(0.52, 0.90, 0.28), location=(0.0, 0.10, 0.30))
    )
    objects.append(
        add_box("dirt_back", dirt, size=(0.50, 0.70, 0.14), location=(0.0, 0.12, 0.44))
    )
    # --- dragged_hindquarters: trailing behind (+Y), sagging ----------------------
    objects.append(
        add_box("dragged_hindquarters", black, size=(0.40, 0.52, 0.22),
                location=(0.0, 0.72, 0.22), rotation=(math.radians(-14.0), 0.0, 0.0))
    )
    objects.append(
        add_box("stub_tail", dirt, size=(0.14, 0.40, 0.12),
                location=(0.0, 1.05, 0.15), rotation=(math.radians(-24.0), 0.0, 0.0))
    )

    # --- clawing_limbs: four splayed forelimbs pulling the body forward -----------
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"fore_upper_{i}", black, size=(0.14, 0.16, 0.34),
                    location=(sx * 0.34, -0.18, 0.30),
                    rotation=(0.0, 0.0, sx * math.radians(48.0)))
        )
        objects.append(
            add_box(f"fore_lower_{i}", black, size=(0.10, 0.30, 0.10),
                    location=(sx * 0.52, -0.42, 0.10),
                    rotation=(math.radians(20.0), 0.0, sx * math.radians(20.0)))
        )
        objects.append(  # claw_hands
            add_box(f"claw_hand_{i}", "mat_bone", size=(0.13, 0.18, 0.06),
                    location=(sx * 0.55, -0.60, 0.05))
        )
        objects.append(
            add_box(f"mid_limb_{i}", black, size=(0.12, 0.14, 0.30),
                    location=(sx * 0.34, 0.30, 0.26),
                    rotation=(0.0, 0.0, sx * math.radians(52.0)))
        )
        objects.append(
            add_box(f"mid_claw_{i}", "mat_bone", size=(0.11, 0.14, 0.06),
                    location=(sx * 0.52, 0.44, 0.05))
        )

    # --- split_head: low, forward, jaw hanging open -------------------------------
    objects.append(
        add_box("split_head", black, size=(0.30, 0.34, 0.22), location=(0.0, -0.46, 0.30))
    )
    objects.append(
        add_box("lower_jaw", black, size=(0.24, 0.24, 0.08),
                location=(0.0, -0.56, 0.16), rotation=(math.radians(28.0), 0.0, 0.0))
    )

    # --- bone_spurs: pale spikes breaking through the back ------------------------
    for i in range(3):
        objects.append(
            add_cone(f"bone_spur_{i}", "mat_bone", radius=0.05, depth=0.20, vertices=4,
                     location=(0.0, 0.36 - i * 0.30, 0.52))
        )

    objects.append(add_anchor("mouth", (0.0, -0.62, 0.24), anchor_type="interaction"))
    return objects


@register_builder("chain_hound_lowpoly")
def build_chain_hound(spec: dict) -> list[bpy.types.Object]:
    """The doctrine of the chain made flesh: the black hound's heavy build, head
    slung low and forward, a massive iron collar with a broken chain dragging
    back, two ember eyes. Faces +X like the black hound. Anchors: mouth, collar."""
    objects: list[bpy.types.Object] = []
    black = "mat_nightmare_black"

    # --- heavy_torso / chest / haunches ------------------------------------------
    objects.append(
        add_box("heavy_torso", black, size=(0.92, 0.58, 0.50), location=(-0.12, 0.0, 0.86))
    )
    objects.append(
        add_box("chest", black, size=(0.54, 0.66, 0.62), location=(0.30, 0.0, 0.82),
                rotation=(0.0, math.radians(10.0), 0.0))
    )
    objects.append(
        add_box("haunches", black, size=(0.46, 0.58, 0.56), location=(-0.58, 0.0, 0.82),
                rotation=(0.0, math.radians(-8.0), 0.0))
    )

    # --- low_head: slung forward and down -----------------------------------------
    objects.append(
        add_box("neck", black, size=(0.44, 0.40, 0.40), location=(0.52, 0.0, 0.98),
                rotation=(0.0, math.radians(-40.0), 0.0))
    )
    objects.append(
        add_box("low_head", black, size=(0.42, 0.38, 0.30), location=(0.74, 0.0, 0.80),
                rotation=(0.0, math.radians(14.0), 0.0))
    )
    objects.append(
        add_box("snout", black, size=(0.28, 0.22, 0.16), location=(0.92, 0.0, 0.72),
                rotation=(0.0, math.radians(16.0), 0.0))
    )
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_cone(f"ear_{i}", black, radius=0.09, depth=0.22, vertices=4,
                     location=(0.60, sy * 0.13, 1.02),
                     rotation=(sy * math.radians(14.0), math.radians(-10.0), 0.0))
        )
        # --- ember_eyes ----------------------------------------------------------
        objects.append(
            add_box(f"ember_eye_{i}", "mat_ember", size=(0.05, 0.05, 0.05),
                    location=(0.82, sy * 0.10, 0.84))
        )

    # --- legs: heavy, planted ------------------------------------------------------
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"leg_front_{i}", black, size=(0.19, 0.19, 0.66),
                    location=(0.30, sy * 0.22, 0.33))
        )
        objects.append(
            add_box(f"paw_front_{i}", black, size=(0.24, 0.20, 0.10),
                    location=(0.34, sy * 0.22, 0.05))
        )
        objects.append(
            add_box(f"leg_rear_{i}", black, size=(0.26, 0.20, 0.60),
                    location=(-0.60, sy * 0.22, 0.32),
                    rotation=(0.0, math.radians(-10.0), 0.0))
        )
        objects.append(
            add_box(f"paw_rear_{i}", black, size=(0.22, 0.18, 0.09),
                    location=(-0.62, sy * 0.22, 0.05))
        )

    # --- iron_collar: massive, studded -------------------------------------------
    objects.append(
        add_torus("iron_collar", "mat_iron", major_radius=0.30, minor_radius=0.07,
                  major_segments=8, minor_segments=4, location=(0.50, 0.0, 0.94),
                  rotation=(0.0, math.radians(58.0), 0.0))
    )
    # --- dragging_chain: broken length trailing back along the ground -------------
    for i in range(4):
        objects.append(
            add_torus(f"chain_link_{i}", "mat_iron", major_radius=0.08, minor_radius=0.028,
                      major_segments=6, minor_segments=4,
                      location=(-0.86 - i * 0.24, 0.0, 0.10),
                      rotation=(0.0, 0.0, math.radians(90.0 * (i % 2))))
        )

    objects.append(add_anchor("mouth", (1.05, 0.0, 0.70), anchor_type="interaction"))
    objects.append(add_anchor("collar", (0.50, 0.0, 0.94), anchor_type="attach"))
    return objects


@register_builder("faceless_saint_lowpoly")
def build_faceless_saint(spec: dict) -> list[bpy.types.Object]:
    """The old faith's answer to forbidden rites: an unnaturally tall robed
    column, wide draped sleeves, a smooth blank golden face, a thin gold halo,
    the void of the hood. Faces -Y. Anchor: blessing (interaction)."""
    objects: list[bpy.types.Object] = []
    robe = "mat_canvas"
    gold = "mat_sacred_gold"
    void = "mat_nightmare_black"

    # --- robe_column + hem: a heavy pale column, widening to the floor ------------
    objects.append(
        add_box("hem", robe, size=(0.72, 0.62, 0.34), location=(0.0, 0.0, 0.17))
    )
    objects.append(
        add_box("robe_lower", robe, size=(0.60, 0.52, 0.70), location=(0.0, 0.0, 0.66))
    )
    objects.append(
        add_box("robe_column", robe, size=(0.50, 0.44, 0.86), location=(0.0, 0.0, 1.44))
    )
    objects.append(
        add_box("shoulders", robe, size=(0.66, 0.40, 0.24), location=(0.0, 0.0, 1.92))
    )

    # --- wide_sleeves: draped, hanging low ----------------------------------------
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"sleeve_upper_{i}", robe, size=(0.20, 0.30, 0.30),
                    location=(sx * 0.36, 0.0, 1.78),
                    rotation=(0.0, 0.0, sx * math.radians(14.0)))
        )
        objects.append(
            add_box(f"sleeve_drape_{i}", robe, size=(0.22, 0.32, 0.52),
                    location=(sx * 0.42, 0.0, 1.34))
        )
        # --- hidden_hands: dark, barely emerging from the sleeves ----------------
        objects.append(
            add_box(f"hidden_hand_{i}", void, size=(0.13, 0.16, 0.14),
                    location=(sx * 0.42, -0.06, 1.04))
        )

    # --- faceless_head: void hood + a blank golden oval where a face should be ----
    objects.append(
        add_box("hood", void, size=(0.30, 0.30, 0.34), location=(0.0, 0.0, 2.22))
    )
    objects.append(
        add_box("faceless_head", gold, size=(0.20, 0.06, 0.28),
                location=(0.0, -0.15, 2.22))
    )
    # --- halo: a thin gold ring hovering behind the head --------------------------
    objects.append(
        add_torus("halo", gold, major_radius=0.26, minor_radius=0.025,
                  major_segments=12, minor_segments=4,
                  location=(0.0, 0.14, 2.34), rotation=(math.radians(90.0), 0.0, 0.0))
    )

    objects.append(add_anchor("blessing", (0.0, -0.20, 1.60), anchor_type="interaction"))
    return objects
