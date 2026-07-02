"""Builder family: the abbey gate, ruined and repaired.

The two states are a swap pair: the game replaces abbey_gate_ruined with
abbey_gate_repaired in place, so BOTH builders take their plan measurements
from the shared module constants below and keep their extreme X/Y extents on
the pier plinths. That guarantees an identical XY bounding box, which is what
apply_center_bottom_pivot centers on — same footprint, same pivot. Only the
height differs (the ruin is shorter), which the validator allows.

abbey_gate_repaired: two warm-stone piers, pointed arch span with a small
terracotta gable cap and a gold cross finial, heavy closed double doors with
iron bands.

abbey_gate_ruined: same piers — left still tall with a snapped arch stub,
right sheared low with jagged teeth; empty doorway, one fallen door slab and
rubble across the threshold (all rubble stays inside the shared extents).

Anchors (both): door (door) at the threshold center front.
Budgets: building class, 2500 tris / 4 materials.
"""

from __future__ import annotations

import math
import random

import bpy

from asset_framework import add_anchor, register_builder
from builders._shapes import add_box, add_cone

# --- shared gate plan (do not change one side without the other) -------------
PIER_X = 1.35      # pier center offset from gate center
PIER_W = 1.10      # pier block width (X)
PIER_D = 1.10      # pier block depth (Y)
PLINTH_PAD = 0.15  # plinth overhang -> extreme extents live on the plinths
PIER_H = 3.30      # full pier block height (repaired / surviving pier)
OPENING_HALF = PIER_X - PIER_W / 2.0   # 0.8 -> 1.6 m wide doorway
# extreme half-extents (both variants MUST touch these and never exceed them)
HALF_W = PIER_X + PIER_W / 2.0 + PLINTH_PAD   # 2.05... clamped below
HALF_D = PIER_D / 2.0 + PLINTH_PAD


def _pier(objects: list, tag: str, sx: int, block_h: float) -> None:
    """One gate pier: plinth (owns the extreme extents) + main block."""
    objects.append(
        add_box(f"pier_plinth_{tag}", "mat_old_stone",
                size=(PIER_W + 2 * PLINTH_PAD, PIER_D + 2 * PLINTH_PAD, 0.55),
                location=(sx * PIER_X, 0.0, 0.275))
    )
    objects.append(
        add_box(f"pier_block_{tag}", "mat_old_stone",
                size=(PIER_W, PIER_D, block_h),
                location=(sx * PIER_X, 0.0, block_h / 2.0))
    )


def _pier_cap(objects: list, tag: str, sx: int) -> None:
    objects.append(
        add_box(f"pier_cap_{tag}", "mat_old_stone",
                size=(PIER_W + 0.18, PIER_D + 0.18, 0.28),
                location=(sx * PIER_X, 0.0, PIER_H + 0.14))
    )


@register_builder("abbey_gate_repaired")
def build_abbey_gate_repaired(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []

    # --- piers (shared plan) --------------------------------------------------
    for tag, sx in (("l", -1), ("r", 1)):
        _pier(objects, tag, sx, PIER_H)
        _pier_cap(objects, tag, sx)

    # --- arch_span: wall over the opening + pointed arch soffit ----------------
    objects.append(
        add_box("arch_span", "mat_old_stone", size=(2.1, 0.85, 0.95),
                location=(0.0, 0.0, 3.0))
    )
    for i, sx in enumerate((1, -1)):
        objects.append(  # corbel steps at the opening's top corners (gothic reveal)
            add_box(f"arch_corbel_{i}", "mat_old_stone", size=(0.30, 0.80, 0.30),
                    location=(sx * (OPENING_HALF - 0.15), 0.0, 2.40))
        )

    # --- gable_cap: stone saddle coping over the span (material budget: the
    # 4 slots are stone/dark_wood/iron/gold, so no terracotta here) -------------
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"gable_slab_{i}", "mat_old_stone", size=(2.4, 0.62, 0.10),
                    location=(0.0, sy * 0.26, 3.72),
                    rotation=(-sy * math.radians(32.0), 0.0, 0.0))
        )
    objects.append(
        add_box("gable_ridge", "mat_dark_wood", size=(2.45, 0.14, 0.10),
                location=(0.0, 0.0, 3.92))
    )

    # --- cross_finial: gold cross on the ridge center ---------------------------
    objects.append(
        add_box("cross_post", "mat_sacred_gold", size=(0.07, 0.07, 0.52),
                location=(0.0, 0.0, 4.22))
    )
    objects.append(
        add_box("cross_arm", "mat_sacred_gold", size=(0.30, 0.07, 0.07),
                location=(0.0, 0.0, 4.30))
    )

    # --- double_doors: heavy closed doors filling the opening -------------------
    door_w = OPENING_HALF - 0.02
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"door_leaf_{i}", "mat_dark_wood",
                    size=(door_w, 0.12, 2.5),
                    location=(sx * (door_w / 2.0 + 0.01), 0.0, 1.25))
        )
    # --- iron_bands: two horizontal straps per leaf + center boss ---------------
    for i, sx in enumerate((1, -1)):
        for j, z in enumerate((0.65, 1.75)):
            objects.append(
                add_box(f"iron_band_{i}_{j}", "mat_iron",
                        size=(door_w - 0.06, 0.05, 0.10),
                        location=(sx * (door_w / 2.0 + 0.01), -0.075, z))
            )
    objects.append(
        add_box("iron_boss", "mat_iron", size=(0.14, 0.06, 0.28),
                location=(0.0, -0.075, 1.2))
    )

    # --- anchors -----------------------------------------------------------------
    objects.append(add_anchor("door", (0.0, -0.75, 0.0), anchor_type="door"))
    return objects


@register_builder("abbey_gate_ruined")
def build_abbey_gate_ruined(spec: dict) -> list[bpy.types.Object]:
    rng = random.Random(17)
    objects: list[bpy.types.Object] = []

    # --- piers (shared plan): left survives tall, right sheared low -------------
    _pier(objects, "l", -1, PIER_H)
    _pier_cap(objects, "l", -1)
    broken_h = 1.9
    _pier(objects, "r", 1, broken_h)

    # --- broken_crown: jagged teeth on the sheared right pier -------------------
    for i in range(3):
        h = 0.30 + rng.uniform(0.0, 0.35)
        objects.append(
            add_box(f"crown_tooth_{i}", "mat_old_stone",
                    size=(0.32, 0.34, h),
                    location=(PIER_X - 0.32 + i * 0.32, rng.uniform(-0.25, 0.25),
                              broken_h + h / 2.0),
                    rotation=(rng.uniform(-0.06, 0.06), rng.uniform(-0.08, 0.08), 0.0))
        )

    # --- arch_stubs: the snapped arch reaching from the tall pier ---------------
    objects.append(
        add_box("arch_stub_long", "mat_old_stone", size=(0.95, 0.75, 0.30),
                location=(-0.55, 0.0, 2.55),
                rotation=(0.0, math.radians(-24.0), rng.uniform(-0.02, 0.02)))
    )
    objects.append(
        add_box("arch_stub_tip", "mat_old_stone", size=(0.34, 0.6, 0.26),
                location=(-0.12, 0.02, 2.72),
                rotation=(0.04, math.radians(-18.0), 0.06))
    )
    # a last tooth of the span still clinging to the right pier stump
    objects.append(
        add_box("arch_stub_right", "mat_old_stone", size=(0.4, 0.7, 0.3),
                location=(0.78, 0.0, broken_h + 0.12),
                rotation=(0.0, math.radians(12.0), -0.03))
    )

    # --- fallen_door: one charred leaf leaning against the tall pier ------------
    objects.append(
        add_box("fallen_door", "mat_dark_wood", size=(0.78, 0.11, 2.1),
                location=(-0.85, -0.28, 0.95),
                rotation=(math.radians(-16.0), math.radians(6.0), math.radians(8.0)))
    )
    objects.append(
        add_box("door_plank", "mat_dark_wood", size=(0.2, 0.09, 1.1),
                location=(0.35, -0.30, 0.14),
                rotation=(0.0, math.radians(86.0), math.radians(-24.0)))
    )

    # --- rubble across the threshold (kept inside the shared extents) -----------
    for i in range(7):
        s = rng.uniform(0.16, 0.36)
        x = rng.uniform(-0.6, 0.9)
        y = rng.uniform(-0.35, 0.35)
        objects.append(
            add_box(f"rubble_{i}", "mat_old_stone",
                    size=(s, s * 0.8, s * 0.6),
                    location=(x, y, s * 0.25),
                    rotation=(rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2),
                              rng.uniform(0.0, math.tau)))
        )

    # --- anchors (same threshold point as the repaired gate) --------------------
    objects.append(add_anchor("door", (0.0, -0.75, 0.0), anchor_type="door"))
    return objects
