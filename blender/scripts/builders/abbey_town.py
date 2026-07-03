"""Builder family: the abbey / town-center kit from docs/ART_REFERENCE_ABBEY.md.

bell_tower_t2: the settlement's signature landmark — cool-stone base with slit
windows, jettied half-timber storey with diagonal bracing, open belfry with a
gold bell, steep pyramidal terracotta roof, pennant pole at the apex.
Anchors: bell, flag (pennant attach), door.

abbey_church_t1 (landmark): warm-stone gothic nave, steep terracotta roof,
pointed stained-glass windows, stepped buttresses with pinnacles, west portal
with archivolts + rose window, octagonal crossing spire (mat_wet_stone — the
stained-glass slot stands in for the reference's slate; the 4-material landmark
budget is full, see "Known closed-library compromises" in
docs/ART_REFERENCE_ABBEY.md) with a gold finial, stone cross finial on the
west gable.
Anchors: door, altar, spire_finial.

abbey_cloister_t1: round-arched arcade colonnade wrapping a grassy garth with
benches and a table, terracotta lean-to roof. Anchors: garth_center, gate.

town_hall_t1 (landmark): stone ground floor with arched door, two jettied
half-timber storeys with dark diagonal bracing, steep gable roof with dormers
and a front gablet, massive external stone chimney, market awning + crates.
Anchors: door, smoke, stall_1.

field_plot_t1: raised soil rows with tomato / leaf / pumpkin crop clusters.
Anchors: work_slot_1, work_slot_2.

Budgets: tower 2500/4, church 6000/4 (landmark), cloister 2500/4,
town hall 6000/4 (landmark), field 800/4.
"""

from __future__ import annotations

import math
import random

import bpy
from mathutils import Vector

from asset_framework import add_anchor, register_builder
from builders._shapes import add_box, add_cone, add_cylinder, add_icosphere

DEG = math.radians


# ---------------------------------------------------------------------------
# bell_tower_t2
# ---------------------------------------------------------------------------


@register_builder("bell_tower_t2")
def build_bell_tower_t2(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []

    # --- stone_base: plinth + shaft + corner piers + string course -------------
    objects.append(add_box("plinth", "mat_ash", size=(1.9, 1.9, 0.3), location=(0, 0, 0.15)))
    objects.append(add_box("base_shaft", "mat_ash", size=(1.6, 1.6, 2.7), location=(0, 0, 1.65)))
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(
            add_box(f"corner_pier_{i}", "mat_ash", size=(0.24, 0.24, 2.7),
                    location=(sx * 0.76, sy * 0.76, 1.65))
        )
    objects.append(add_box("string_course", "mat_ash", size=(1.8, 1.8, 0.14), location=(0, 0, 3.0)))

    # --- slit_windows: narrow dark slits on the stone shaft ---------------------
    objects.append(add_box("slit_front_low", "mat_dark_wood", size=(0.11, 0.06, 0.55),
                           location=(0.0, -0.81, 1.6)))
    objects.append(add_box("slit_front_high", "mat_dark_wood", size=(0.11, 0.06, 0.55),
                           location=(0.0, -0.81, 2.45)))
    for i, sx in enumerate((1, -1)):
        objects.append(add_box(f"slit_side_{i}", "mat_dark_wood", size=(0.06, 0.11, 0.55),
                               location=(sx * 0.81, 0.0, 2.0)))

    # --- door (front -Y): stone jambs + lintel, dark slab -----------------------
    objects.append(add_box("door_slab", "mat_dark_wood", size=(0.5, 0.08, 0.95),
                           location=(0.0, -0.93, 0.775)))
    for i, sx in enumerate((1, -1)):
        objects.append(add_box(f"door_jamb_{i}", "mat_ash", size=(0.15, 0.1, 1.05),
                               location=(sx * 0.34, -0.94, 0.825)))
    objects.append(add_box("door_lintel", "mat_ash", size=(0.8, 0.1, 0.17),
                           location=(0.0, -0.94, 1.42)))

    # --- jettied_storey: overhanging plaster-grey infill on dark joists ---------
    objects.append(add_box("storey_infill", "mat_ash", size=(1.86, 1.86, 0.8),
                           location=(0, 0, 3.47)))
    for i, x in enumerate((-0.72, -0.36, 0.0, 0.36, 0.72)):
        objects.append(add_box(f"jetty_joist_{i}", "mat_dark_wood", size=(0.12, 1.94, 0.12),
                               location=(x, 0.0, 3.05)))
    # storey timber frame: corner posts + sill + top plate
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(add_box(f"storey_post_{i}", "mat_dark_wood", size=(0.14, 0.14, 0.86),
                               location=(sx * 0.9, sy * 0.9, 3.48)))
    for i, sy in enumerate((1, -1)):
        objects.append(add_box(f"storey_sill_y{i}", "mat_dark_wood", size=(1.94, 0.12, 0.12),
                               location=(0.0, sy * 0.91, 3.1)))
        objects.append(add_box(f"storey_plate_y{i}", "mat_dark_wood", size=(1.94, 0.12, 0.12),
                               location=(0.0, sy * 0.91, 3.88)))
    for i, sx in enumerate((1, -1)):
        objects.append(add_box(f"storey_sill_x{i}", "mat_dark_wood", size=(0.12, 1.94, 0.12),
                               location=(sx * 0.91, 0.0, 3.1)))
        objects.append(add_box(f"storey_plate_x{i}", "mat_dark_wood", size=(0.12, 1.94, 0.12),
                               location=(sx * 0.91, 0.0, 3.88)))

    # --- bracing: dark diagonals on every storey face ----------------------------
    for i, sy in enumerate((1, -1)):
        for j, (x, a) in enumerate(((-0.45, 38.0), (0.45, -38.0))):
            objects.append(
                add_box(f"brace_y{i}_{j}", "mat_dark_wood", size=(0.1, 0.05, 0.85),
                        location=(x, sy * 0.94, 3.48), rotation=(0.0, DEG(a), 0.0))
            )
    for i, sx in enumerate((1, -1)):
        for j, (y, a) in enumerate(((-0.45, -38.0), (0.45, 38.0))):
            objects.append(
                add_box(f"brace_x{i}_{j}", "mat_dark_wood", size=(0.05, 0.1, 0.85),
                        location=(sx * 0.94, y, 3.48), rotation=(DEG(a), 0.0, 0.0))
            )

    # --- belfry: open frame under the roof, arches suggested by keystone drops ---
    objects.append(add_box("belfry_floor", "mat_ash", size=(1.7, 1.7, 0.12), location=(0, 0, 3.96)))
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(add_box(f"belfry_post_{i}", "mat_dark_wood", size=(0.18, 0.18, 0.8),
                               location=(sx * 0.66, sy * 0.66, 4.4)))
    for i, sy in enumerate((1, -1)):
        objects.append(add_box(f"belfry_header_y{i}", "mat_dark_wood", size=(1.56, 0.15, 0.22),
                               location=(0.0, sy * 0.66, 4.72)))
        objects.append(add_box(f"belfry_key_y{i}", "mat_dark_wood", size=(0.2, 0.13, 0.16),
                               location=(0.0, sy * 0.66, 4.55)))
    for i, sx in enumerate((1, -1)):
        objects.append(add_box(f"belfry_header_x{i}", "mat_dark_wood", size=(0.15, 1.56, 0.22),
                               location=(sx * 0.66, 0.0, 4.72)))
        objects.append(add_box(f"belfry_key_x{i}", "mat_dark_wood", size=(0.13, 0.2, 0.16),
                               location=(sx * 0.66, 0.0, 4.55)))

    # --- bell: gold, hanging from a beam in the open belfry ----------------------
    objects.append(add_box("bell_beam", "mat_dark_wood", size=(1.45, 0.14, 0.14),
                           location=(0.0, 0.0, 4.68)))
    objects.append(add_cone("bell_body", "mat_sacred_gold", radius=0.34, radius_top=0.17,
                            depth=0.5, vertices=10, location=(0.0, 0.0, 4.35)))
    objects.append(add_cylinder("bell_crown", "mat_sacred_gold", radius=0.1, depth=0.14,
                                vertices=8, location=(0.0, 0.0, 4.65)))
    objects.append(add_cone("bell_clapper", "mat_sacred_gold", radius=0.07, depth=0.18,
                            vertices=6, location=(0.0, 0.0, 4.06)))

    # --- pyramid_roof: steep 4-sided terracotta pyramid + eave trim --------------
    # (rotation applied to the mesh so the local AABB stays tight — world_bounds
    # measures object AABBs, which inflate under object-level rotation)
    pyramid = add_cone("pyramid_roof", "mat_thatch", radius=1.32, depth=1.05, vertices=4,
                       location=(0.0, 0.0, 5.325), rotation=(0.0, 0.0, DEG(45.0)))
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    objects.append(pyramid)
    for i, sy in enumerate((1, -1)):
        objects.append(add_box(f"eave_trim_y{i}", "mat_dark_wood", size=(1.9, 0.1, 0.09),
                               location=(0.0, sy * 0.9, 4.82)))
    for i, sx in enumerate((1, -1)):
        objects.append(add_box(f"eave_trim_x{i}", "mat_dark_wood", size=(0.1, 1.9, 0.09),
                               location=(sx * 0.9, 0.0, 4.82)))

    # --- flag_pole at the apex (pennant_pole prop attaches at the 'flag' anchor) --
    objects.append(add_cylinder("flag_pole", "mat_dark_wood", radius=0.03, depth=0.36,
                                vertices=5, location=(0.0, 0.0, 5.8)))

    # --- anchors ------------------------------------------------------------------
    objects.append(add_anchor("bell", (0.0, 0.0, 4.35), anchor_type="bell"))
    objects.append(add_anchor("flag", (0.0, 0.0, 5.98), anchor_type="flag"))
    objects.append(add_anchor("door", (0.0, -0.97, 0.0), anchor_type="door"))
    return objects


# ---------------------------------------------------------------------------
# abbey_church_t1
# ---------------------------------------------------------------------------


def _pointed_window(objects: list, tag: str, location: tuple[float, float, float],
                    facing: str, width: float = 0.2, height: float = 0.6) -> None:
    """Pointed-arch stained-glass window with simple stone tracery.

    facing: 'y' = window on a +-Y wall (slab thin in Y), 'x' = on a +-X wall.
    """
    x, y, z = location
    if facing == "y":
        size = (width, 0.07, height)
        cap_scale = (1.0, 0.3, 1.0)
        frame_size = (width + 0.09, 0.05, 0.06)
        mullion_size = (0.035, 0.052, height * 0.85)
        accent_size = (width * 0.42, 0.054, height * 0.18)
    else:
        size = (0.07, width, height)
        cap_scale = (0.3, 1.0, 1.0)
        frame_size = (0.05, width + 0.09, 0.06)
        mullion_size = (0.052, 0.035, height * 0.85)
        accent_size = (0.054, width * 0.42, height * 0.18)
    objects.append(add_box(f"win_{tag}_body", "mat_wet_stone", size=size, location=(x, y, z)))
    cap = add_cone(f"win_{tag}_cap", "mat_wet_stone", radius=width * 0.72, depth=width * 1.4,
                   vertices=4, location=(x, y, z + height / 2.0 + width * 0.55))
    cap.scale = (cap.scale.x * cap_scale[0], cap.scale.y * cap_scale[1], cap.scale.z * cap_scale[2])
    objects.append(cap)
    objects.append(add_box(f"win_{tag}_sill", "mat_old_stone", size=frame_size,
                           location=(x, y, z - height * 0.5 - 0.03)))
    objects.append(add_box(f"win_{tag}_mullion", "mat_old_stone", size=mullion_size,
                           location=(x, y, z - height * 0.02)))
    objects.append(add_box(f"win_{tag}_red_glass", "mat_thatch", size=accent_size,
                           location=(x, y, z - height * 0.18)))


@register_builder("abbey_church_t1")
def build_abbey_church(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []

    # --- nave + crossing masses (warm stone) -------------------------------------
    objects.append(add_box("plinth", "mat_old_stone", size=(4.36, 2.04, 0.24), location=(-0.03, 0, 0.12)))
    objects.append(add_box("nave", "mat_old_stone", size=(3.05, 1.5, 1.78), location=(-0.48, 0, 1.13)))
    objects.append(add_box("nave_string_course", "mat_old_stone", size=(3.18, 1.58, 0.11), location=(-0.48, 0, 1.98)))
    objects.append(add_box("transept", "mat_old_stone", size=(0.9, 2.12, 1.65), location=(1.03, 0, 1.08)))
    objects.append(add_box("crossing", "mat_old_stone", size=(1.12, 1.72, 2.78), location=(1.40, 0, 1.63)))
    objects.append(add_box("crossing_cornice", "mat_old_stone", size=(1.2, 1.8, 0.12),
                           location=(1.40, 0, 2.95)))
    objects.append(add_box("apse", "mat_old_stone", size=(0.68, 1.18, 1.52), location=(2.12, 0, 1.0)))
    objects.append(add_cone("apse_cap", "mat_old_stone", radius=0.72, depth=0.5, vertices=8,
                            location=(2.12, 0, 1.98), rotation=(0.0, DEG(90.0), 0.0)))

    # --- gable_roof: steep terracotta slabs + ridge cap ---------------------------
    pitch = DEG(50.0)
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"nave_roof_{i}", "mat_thatch", size=(3.2, 1.36, 0.12),
                    location=(-0.52, sy * 0.44, 2.4), rotation=(-sy * pitch, 0.0, 0.0))
        )
        for j, z in enumerate((2.22, 2.43, 2.64)):
            objects.append(add_box(f"nave_roof_course_{i}_{j}", "mat_thatch", size=(3.22, 0.08, 0.06),
                                   location=(-0.52, sy * (0.79 - j * 0.18), z),
                                   rotation=(-sy * pitch, 0.0, 0.0)))
    objects.append(add_box("ridge_cap", "mat_thatch", size=(3.32, 0.18, 0.12),
                           location=(-0.52, 0.0, 2.94)))
    _gable_roof_pair(objects, "transept", (1.03, 0.0, 2.28), 0.95, 2.18, yaw_deg=90.0)

    # --- west gable (stepped warm stone) + stone cross finial ---------------------
    for i, (w, z) in enumerate(((1.58, 2.1), (1.16, 2.42), (0.72, 2.72), (0.34, 2.98))):
        objects.append(add_box(f"west_gable_{i}", "mat_old_stone", size=(0.15, w, 0.32),
                               location=(-1.98, 0.0, z)))
    for i, sy in enumerate((1, -1)):
        objects.append(add_box(f"west_flanking_tower_{i}", "mat_old_stone", size=(0.28, 0.28, 1.15),
                               location=(-1.98, sy * 0.68, 2.15)))
        objects.append(add_cone(f"west_flanking_spire_{i}", "mat_old_stone", radius=0.19,
                                depth=0.62, vertices=6, location=(-1.98, sy * 0.68, 3.03)))
    objects.append(add_box("cross_post", "mat_old_stone", size=(0.07, 0.07, 0.42),
                           location=(-1.98, 0.0, 3.32)))
    objects.append(add_box("cross_arm", "mat_old_stone", size=(0.07, 0.26, 0.07),
                           location=(-1.98, 0.0, 3.39)))

    # --- stained_windows: pointed arches along the nave + crossing ----------------
    for i, x in enumerate((-1.45, -0.75, -0.05)):
        _pointed_window(objects, f"s{i}", (x, -0.78, 1.3), facing="y")
        _pointed_window(objects, f"n{i}", (x, 0.78, 1.3), facing="y")
    for i, sy in enumerate((1, -1)):
        _pointed_window(objects, f"cross_y{i}", (1.40, sy * 0.88, 1.85), facing="y",
                        width=0.24, height=0.75)
    _pointed_window(objects, "east", (1.925, 0.0, 1.85), facing="x", width=0.3, height=0.85)

    # --- rose window over the portal ----------------------------------------------
    objects.append(add_cylinder("rose_frame", "mat_old_stone", radius=0.33, depth=0.07,
                                vertices=12, location=(-2.04, 0.0, 2.34),
                                rotation=(0.0, DEG(90.0), 0.0)))
    objects.append(add_cylinder("rose_glass", "mat_wet_stone", radius=0.26, depth=0.09,
                                vertices=12, location=(-2.04, 0.0, 2.34),
                                rotation=(0.0, DEG(90.0), 0.0)))
    for i, angle in enumerate((0.0, 45.0, 90.0, 135.0)):
        objects.append(add_box(f"rose_tracery_{i}", "mat_old_stone", size=(0.04, 0.52, 0.035),
                               location=(-2.095, 0.0, 2.34), rotation=(DEG(angle), 0.0, 0.0)))

    # --- buttresses: stepped, with sloped caps and pinnacles ------------------------
    for i, x in enumerate((-1.75, -1.0, -0.25, 0.5)):
        for j, sy in enumerate((1, -1)):
            objects.append(add_box(f"buttress_{i}_{j}", "mat_old_stone", size=(0.2, 0.18, 1.5),
                                   location=(x, sy * 0.82, 0.99)))
            objects.append(
                add_box(f"buttress_cap_{i}_{j}", "mat_old_stone", size=(0.2, 0.24, 0.18),
                        location=(x, sy * 0.79, 1.82), rotation=(sy * DEG(28.0), 0.0, 0.0))
            )
            objects.append(add_cone(f"pinnacle_{i}_{j}", "mat_old_stone", radius=0.08,
                                    depth=0.28, vertices=4, location=(x, sy * 0.84, 2.02)))

    # --- west_portal: nested archivolts + recessed blue door + step -----------------
    objects.append(add_box("archivolt_outer", "mat_old_stone", size=(0.14, 1.0, 1.3),
                           location=(-2.04, 0.0, 0.89)))
    objects.append(add_box("archivolt_inner", "mat_old_stone", size=(0.13, 0.78, 1.15),
                           location=(-2.075, 0.0, 0.815)))
    objects.append(add_box("portal_door", "mat_wet_stone", size=(0.09, 0.56, 0.95),
                           location=(-2.09, 0.0, 0.715)))
    objects.append(add_box("portal_warm_threshold", "mat_thatch", size=(0.1, 0.5, 0.12),
                           location=(-2.145, 0.0, 0.35)))
    objects.append(add_box("portal_step", "mat_old_stone", size=(0.2, 1.0, 0.13),
                           location=(-2.04, 0.0, 0.065)))
    objects.append(add_box("portal_step_lower", "mat_old_stone", size=(0.3, 1.16, 0.08),
                           location=(-2.14, 0.0, 0.02)))
    objects.append(
        add_box("portal_gable", "mat_old_stone", size=(0.14, 0.22, 0.22),
                location=(-2.04, 0.0, 1.62), rotation=(DEG(45.0), 0.0, 0.0))
    )

    # --- crossing_spire: octagonal drum + spire + gold finial -----------------------
    # (spire = mat_wet_stone: 4-material budget compromise, doc'd in ART_REFERENCE)
    objects.append(add_cylinder("spire_drum", "mat_old_stone", radius=0.5, depth=0.58,
                                vertices=8, location=(1.40, 0.0, 3.22)))
    objects.append(add_box("spire_drum_louver_y0", "mat_wet_stone", size=(0.38, 0.06, 0.26),
                           location=(1.40, -0.51, 3.24)))
    objects.append(add_box("spire_drum_louver_y1", "mat_wet_stone", size=(0.38, 0.06, 0.26),
                           location=(1.40, 0.51, 3.24)))
    objects.append(add_box("spire_roof_skirt", "mat_thatch", size=(1.16, 1.16, 0.12),
                           location=(1.40, 0.0, 3.54), rotation=(0.0, 0.0, DEG(45.0))))
    objects.append(add_cone("spire", "mat_wet_stone", radius=0.42, depth=1.68, vertices=8,
                            location=(1.40, 0.0, 4.32)))
    objects.append(add_cylinder("finial_rod", "mat_sacred_gold", radius=0.03, depth=0.3,
                                vertices=5, location=(1.40, 0.0, 5.4)))
    objects.append(add_cone("finial_tip", "mat_sacred_gold", radius=0.08, depth=0.18,
                            vertices=6, location=(1.40, 0.0, 5.6)))

    # --- anchors ---------------------------------------------------------------------
    objects.append(add_anchor("door", (-2.12, 0.0, 0.0), anchor_type="door"))
    objects.append(add_anchor("altar", (1.40, 0.0, 0.3), anchor_type="work"))
    objects.append(add_anchor("spire_finial", (1.40, 0.0, 5.7), anchor_type="finial"))
    return objects


# ---------------------------------------------------------------------------
# abbey_cloister_t1
# ---------------------------------------------------------------------------


@register_builder("abbey_cloister_t1")
def build_abbey_cloister(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []
    gate_half = 0.4  # opening in the front arcade

    # --- arcade_walls: low stone parapet ring (gate gap at front center) ----------
    for i, sx in enumerate((1, -1)):  # front wall halves (gate .. corner pier)
        cx = sx * (gate_half + (1.24 - gate_half) / 2.0)
        objects.append(add_box(f"wall_front_{i}", "mat_old_stone",
                               size=(1.24 - gate_half, 0.16, 0.4), location=(cx, -0.88, 0.2)))
    objects.append(add_box("wall_back", "mat_old_stone", size=(2.48, 0.16, 0.4),
                           location=(0.0, 0.88, 0.2)))
    for i, sx in enumerate((1, -1)):
        objects.append(add_box(f"wall_side_{i}", "mat_old_stone", size=(0.16, 1.54, 0.4),
                               location=(sx * 1.37, 0.0, 0.2)))
    # corner piers rise flush with the header ring (they cap the arcade corners)
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(add_box(f"corner_pier_{i}", "mat_old_stone", size=(0.22, 0.22, 1.3),
                               location=(sx * 1.36, sy * 0.87, 0.65)))

    # --- columns: colonnade rhythm on the parapet ----------------------------------
    col_z, col_h = 0.75, 0.72
    for i, x in enumerate((-1.15, -0.72, 0.72, 1.15)):  # front (gate gap in middle)
        objects.append(add_cylinder(f"col_front_{i}", "mat_old_stone", radius=0.07,
                                    depth=col_h, vertices=6, location=(x, -0.88, col_z)))
    for i, x in enumerate((-1.05, -0.52, 0.0, 0.52, 1.05)):  # back
        objects.append(add_cylinder(f"col_back_{i}", "mat_old_stone", radius=0.07,
                                    depth=col_h, vertices=6, location=(x, 0.88, col_z)))
    for i, sx in enumerate((1, -1)):  # sides
        for j, y in enumerate((-0.45, 0.0, 0.45)):
            objects.append(add_cylinder(f"col_side_{i}_{j}", "mat_old_stone", radius=0.07,
                                        depth=col_h, vertices=6, location=(sx * 1.37, y, col_z)))

    # --- arches: continuous header ring + gate lintel arch ---------------------------
    for i, sx in enumerate((1, -1)):
        cx = sx * (gate_half + (1.24 - gate_half) / 2.0)
        objects.append(add_box(f"header_front_{i}", "mat_old_stone",
                               size=(1.24 - gate_half, 0.18, 0.2), location=(cx, -0.88, 1.2)))
    objects.append(add_box("header_back", "mat_old_stone", size=(2.48, 0.18, 0.2),
                           location=(0.0, 0.88, 1.2)))
    for i, sx in enumerate((1, -1)):
        objects.append(add_box(f"header_side_{i}", "mat_old_stone", size=(0.18, 1.54, 0.2),
                               location=(sx * 1.37, 0.0, 1.2)))
    # proud lintel over the gate (offset dims so no face is coincident with headers)
    objects.append(add_box("gate_arch", "mat_old_stone", size=(0.98, 0.22, 0.16),
                           location=(0.0, -0.88, 1.24)))

    # --- lean_to_roof: terracotta slabs sloping up toward the garth ------------------
    slope = DEG(20.0)
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"roof_y{i}", "mat_thatch", size=(2.96, 0.6, 0.09),
                    location=(0.0, sy * 0.70, 1.45), rotation=(sy * slope, 0.0, 0.0))
        )
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"roof_x{i}", "mat_thatch", size=(0.6, 1.9, 0.09),
                    location=(sx * 1.2, 0.0, 1.45), rotation=(0.0, -sx * slope, 0.0))
        )
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(add_box(f"roof_corner_{i}", "mat_thatch", size=(0.56, 0.56, 0.08),
                               location=(sx * 1.2, sy * 0.7, 1.44),
                               rotation=(sy * slope * 0.7, -sx * slope * 0.7, 0.0)))

    # --- garth: grass, benches, table --------------------------------------------------
    objects.append(add_box("garth_grass", "mat_foliage", size=(1.5, 0.78, 0.1),
                           location=(0.0, 0.05, 0.05)))
    for i, (x, y, yaw) in enumerate(((-0.45, 0.25, 8.0), (0.45, -0.05, -12.0))):
        objects.append(add_box(f"bench_{i}", "mat_warm_wood", size=(0.55, 0.16, 0.09),
                               location=(x, y, 0.28), rotation=(0.0, 0.0, DEG(yaw))))
        objects.append(add_box(f"bench_legs_{i}", "mat_warm_wood", size=(0.42, 0.1, 0.18),
                               location=(x, y, 0.19), rotation=(0.0, 0.0, DEG(yaw))))
    objects.append(add_box("table_top", "mat_warm_wood", size=(0.44, 0.34, 0.06),
                           location=(0.0, 0.18, 0.45)))
    objects.append(add_box("table_leg", "mat_warm_wood", size=(0.12, 0.12, 0.34),
                           location=(0.0, 0.18, 0.25)))

    # --- anchors -------------------------------------------------------------------------
    objects.append(add_anchor("garth_center", (0.0, 0.05, 0.11), anchor_type="work"))
    objects.append(add_anchor("gate", (0.0, -0.95, 0.0), anchor_type="door"))
    return objects


# ---------------------------------------------------------------------------
# town_hall_t1
# ---------------------------------------------------------------------------


@register_builder("town_hall_t1")
def build_town_hall(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []

    # --- stone_ground_floor -----------------------------------------------------------
    objects.append(add_box("plinth", "mat_old_stone", size=(3.3, 1.95, 0.2), location=(-0.12, 0, 0.1)))
    objects.append(add_box("ground_floor", "mat_old_stone", size=(2.9, 1.62, 1.2),
                           location=(0, 0, 0.8)))
    objects.append(add_box("west_wing_ground", "mat_old_stone", size=(1.05, 1.18, 1.0),
                           location=(-1.78, 0.18, 0.7)))

    # --- arched_door (front -Y) ---------------------------------------------------------
    objects.append(add_box("door_slab", "mat_dark_wood", size=(0.6, 0.1, 1.0),
                           location=(0.0, -0.82, 0.7)))
    for i, sx in enumerate((1, -1)):
        objects.append(add_box(f"door_jamb_{i}", "mat_old_stone", size=(0.17, 0.12, 1.1),
                               location=(sx * 0.42, -0.83, 0.75)))
    objects.append(add_box("door_arch", "mat_old_stone", size=(0.95, 0.12, 0.2),
                           location=(0.0, -0.83, 1.32)))
    objects.append(add_box("door_keystone", "mat_old_stone", size=(0.2, 0.14, 0.24),
                           location=(0.0, -0.835, 1.38)))
    for i, x in enumerate((-0.95, 0.95)):
        objects.append(add_box(f"ground_arch_shadow_{i}", "mat_dark_wood", size=(0.42, 0.07, 0.48),
                               location=(x, -0.83, 0.72)))
        objects.append(add_box(f"ground_arch_head_{i}", "mat_dark_wood", size=(0.5, 0.075, 0.12),
                               location=(x, -0.835, 1.03)))

    # --- jettied_storeys: two overhanging plaster floors --------------------------------
    objects.append(add_box("storey_1", "mat_canvas", size=(3.05, 1.78, 0.9), location=(-0.03, 0, 1.85)))
    objects.append(add_box("storey_2", "mat_canvas", size=(3.18, 1.92, 0.9), location=(-0.06, 0, 2.75)))
    objects.append(add_box("west_wing_storey", "mat_canvas", size=(1.22, 1.28, 0.82),
                           location=(-1.78, 0.18, 1.62)))

    # jetty sill / plate beams + corner posts (dark timber)
    for level, (z, w, d) in enumerate(((1.42, 3.12, 1.86), (2.32, 3.22, 1.98), (3.22, 3.22, 1.98))):
        for i, sy in enumerate((1, -1)):
            objects.append(add_box(f"beam_y_{level}_{i}", "mat_dark_wood", size=(w, 0.13, 0.13),
                                   location=(0.0, sy * (d / 2.0 - 0.04), z)))
        for i, sx in enumerate((1, -1)):
            objects.append(add_box(f"beam_x_{level}_{i}", "mat_dark_wood",
                                   size=(0.13, d - 0.05, 0.13),
                                   location=(sx * (w / 2.0 - 0.04), 0.0, z)))
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(add_box(f"post_s1_{i}", "mat_dark_wood", size=(0.13, 0.13, 0.92),
                               location=(-0.03 + sx * 1.5, sy * 0.87, 1.85)))
        objects.append(add_box(f"post_s2_{i}", "mat_dark_wood", size=(0.13, 0.13, 0.92),
                               location=(-0.06 + sx * 1.56, sy * 0.94, 2.75)))
    for i, sy in enumerate((1, -1)):
        objects.append(add_box(f"west_wing_plate_{i}", "mat_dark_wood", size=(1.28, 0.11, 0.1),
                               location=(-1.78, 0.18 + sy * 0.66, 2.05)))
    for i, x in enumerate((-2.32, -1.78, -1.24)):
        objects.append(add_box(f"west_wing_post_{i}", "mat_dark_wood", size=(0.1, 0.1, 0.82),
                               location=(x, -0.5, 1.62)))

    # --- diagonal_bracing: dark and diagonal-heavy against pale plaster ------------------
    brace_specs = [  # (storey_z, face_y, xs)
        (1.85, 0.9, ((-1.0, 38.0), (-0.45, -38.0), (0.45, 38.0), (1.0, -38.0))),
        (2.75, 0.95, ((-1.05, -38.0), (-0.45, 38.0), (0.45, -38.0), (1.05, 38.0))),
    ]
    for si, (z, fy, xs) in enumerate(brace_specs):
        for j, (x, a) in enumerate(xs):
            for k, sy in enumerate((1, -1)):
                objects.append(
                    add_box(f"brace_{si}_{j}_{k}", "mat_dark_wood", size=(0.1, 0.05, 0.82),
                            location=(x, sy * fy, z), rotation=(0.0, DEG(a), 0.0))
                )
    for si, (z, fx) in enumerate(((1.85, 1.44), (2.75, 1.49))):
        for j, (y, a) in enumerate(((-0.42, 40.0), (0.42, -40.0))):
            for k, sx in enumerate((1, -1)):
                objects.append(
                    add_box(f"brace_side_{si}_{j}_{k}", "mat_dark_wood", size=(0.05, 0.1, 0.8),
                            location=(sx * fx, y, z), rotation=(DEG(a), 0.0, 0.0))
                )
    for i, (x, a) in enumerate(((-2.12, 35.0), (-1.44, -35.0))):
        objects.append(add_box(f"west_wing_brace_{i}", "mat_dark_wood", size=(0.08, 0.05, 0.72),
                               location=(x, -0.52, 1.64), rotation=(0.0, DEG(a), 0.0)))

    # shuttered windows (dark) on the upper storeys
    for i, x in enumerate((-0.88, -0.16, 0.56)):
        objects.append(add_box(f"window_s2_{i}", "mat_dark_wood", size=(0.3, 0.06, 0.4),
                               location=(x, -0.94, 2.78)))
    for i, x in enumerate((-0.82, 0.72)):
        objects.append(add_box(f"window_s1_{i}", "mat_dark_wood", size=(0.3, 0.06, 0.38),
                               location=(x, -0.89, 1.88)))
    objects.append(add_box("west_wing_window", "mat_dark_wood", size=(0.27, 0.06, 0.34),
                           location=(-1.78, -0.49, 1.64)))

    # --- multi_gable_roof: steep main gable + gablet + dormers + ridge -------------------
    pitch = DEG(52.0)
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"roof_slab_{i}", "mat_thatch", size=(3.28, 1.72, 0.14),
                    location=(-0.06, sy * 0.45, 3.85), rotation=(-sy * pitch, 0.0, 0.0))
        )
    objects.append(add_box("ridge_cap", "mat_thatch", size=(3.28, 0.18, 0.12),
                           location=(-0.06, 0.0, 4.52)))
    for i, sy in enumerate((1, -1)):
        objects.append(add_box(f"roof_tile_band_{i}_low", "mat_dark_wood", size=(3.32, 0.05, 0.05),
                               location=(-0.06, sy * 0.72, 3.58), rotation=(-sy * pitch, 0.0, 0.0)))
        objects.append(add_box(f"roof_tile_band_{i}_high", "mat_dark_wood", size=(3.02, 0.05, 0.05),
                               location=(-0.06, sy * 0.32, 4.05), rotation=(-sy * pitch, 0.0, 0.0)))
    # gable end walls (stepped plaster) at +-X
    for i, sx in enumerate((1, -1)):
        for j, (w, z) in enumerate(((1.7, 3.4), (1.2, 3.74), (0.72, 4.06), (0.3, 4.34))):
            objects.append(add_box(f"gable_{i}_{j}", "mat_canvas", size=(0.14, w, 0.34),
                                   location=(-0.06 + sx * 1.58, 0.0, z)))

    _gable_roof_pair(objects, "west_wing", (-1.78, 0.18, 2.7), 1.28, 1.35)

    # dormers: two on the front slope
    for i, x in enumerate((-0.85, 0.15)):
        objects.append(add_box(f"dormer_body_{i}", "mat_canvas", size=(0.42, 0.5, 0.42),
                               location=(x, -0.62, 3.72)))
        objects.append(add_box(f"dormer_window_{i}", "mat_dark_wood", size=(0.2, 0.06, 0.24),
                               location=(x, -0.88, 3.72)))
        for j, sx2 in enumerate((1, -1)):
            objects.append(
                add_box(f"dormer_roof_{i}_{j}", "mat_thatch", size=(0.34, 0.56, 0.06),
                        location=(x + sx2 * 0.12, -0.6, 3.99), rotation=(0.0, -sx2 * DEG(38.0), 0.0))
            )
    # front gablet (wall dormer) breaking the eave — the multi-gable silhouette
    objects.append(add_box("gablet_body", "mat_canvas", size=(0.82, 0.56, 0.86),
                           location=(0.92, -0.7, 3.6)))
    objects.append(add_box("gablet_brace_l", "mat_dark_wood", size=(0.08, 0.05, 0.6),
                           location=(0.72, -0.98, 3.54), rotation=(0.0, DEG(35.0), 0.0)))
    objects.append(add_box("gablet_brace_r", "mat_dark_wood", size=(0.08, 0.05, 0.6),
                           location=(1.12, -0.98, 3.54), rotation=(0.0, DEG(-35.0), 0.0)))
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"gablet_roof_{i}", "mat_thatch", size=(0.52, 0.66, 0.07),
                    location=(0.92 + sx * 0.22, -0.68, 4.18), rotation=(0.0, -sx * DEG(42.0), 0.0))
        )

    # --- stone_chimney: massive, external, on the east wall ------------------------------
    objects.append(add_box("chimney_base", "mat_old_stone", size=(0.4, 0.66, 1.7),
                           location=(1.29, 0.0, 1.05)))
    objects.append(add_box("chimney_mid", "mat_old_stone", size=(0.34, 0.52, 1.9),
                           location=(1.29, 0.0, 2.8)))
    objects.append(add_box("chimney_top", "mat_old_stone", size=(0.3, 0.44, 1.1),
                           location=(1.30, 0.0, 4.3)))
    objects.append(add_box("chimney_cap", "mat_old_stone", size=(0.36, 0.56, 0.12),
                           location=(1.30, 0.0, 4.9)))

    # --- market_awning + crates at the front-left ----------------------------------------
    objects.append(
        add_box("awning", "mat_canvas", size=(1.15, 0.62, 0.05),
                location=(-0.85, -0.68, 1.35), rotation=(DEG(-20.0), 0.0, 0.0))
    )
    for i, x in enumerate((-1.3, -0.4)):
        objects.append(add_box(f"awning_post_{i}", "mat_dark_wood", size=(0.08, 0.08, 1.2),
                               location=(x, -0.93, 0.6)))
    objects.append(add_box("crate_big", "mat_dark_wood", size=(0.32, 0.32, 0.3),
                           location=(-0.65, -0.72, 0.35), rotation=(0.0, 0.0, DEG(12.0))))
    objects.append(add_box("crate_small", "mat_dark_wood", size=(0.26, 0.26, 0.24),
                           location=(-1.05, -0.68, 0.32), rotation=(0.0, 0.0, DEG(-18.0))))

    # --- anchors ---------------------------------------------------------------------------
    objects.append(add_anchor("door", (0.0, -0.95, 0.0), anchor_type="door"))
    objects.append(add_anchor("smoke", (1.30, 0.0, 5.05), anchor_type="particle"))
    objects.append(add_anchor("stall_1", (-0.85, -0.85, 0.0), anchor_type="work"))
    return objects


# ---------------------------------------------------------------------------
# field_plot_t1
# ---------------------------------------------------------------------------


@register_builder("field_plot_t1")
def build_field_plot(spec: dict) -> list[bpy.types.Object]:
    rng = random.Random(41)
    objects: list[bpy.types.Object] = []

    # --- frame + soil_rows ------------------------------------------------------------
    for i, sy in enumerate((1, -1)):
        objects.append(add_box(f"frame_y{i}", "mat_warm_wood", size=(1.95, 0.09, 0.14),
                               location=(0.0, sy * 0.93, 0.07)))
    for i, sx in enumerate((1, -1)):
        objects.append(add_box(f"frame_x{i}", "mat_warm_wood", size=(0.09, 1.76, 0.14),
                               location=(sx * 0.93, 0.0, 0.07)))
    row_ys = (-0.66, -0.22, 0.22, 0.66)
    for i, y in enumerate(row_ys):
        objects.append(
            add_box(f"soil_row_{i}", "mat_dirt", size=(1.7, 0.3, 0.2),
                    location=(rng.uniform(-0.04, 0.04), y, 0.1),
                    rotation=(0.0, 0.0, DEG(rng.uniform(-2.0, 2.0))))
        )

    # --- crops: tomato (mat_thatch = closest non-emissive red in the closed library,
    # doc'd in ART_REFERENCE), leaf (grass), pumpkin (warm wood) clusters ---------------
    def tomato(tag: str, x: float, y: float) -> None:
        objects.append(add_icosphere(f"tomato_{tag}", "mat_thatch", radius=0.09,
                                     location=(x, y, 0.27),
                                     rotation=(0.0, 0.0, rng.uniform(0, math.tau))))

    def leaf(tag: str, x: float, y: float, r: float = 0.11) -> None:
        objects.append(add_icosphere(f"leaf_{tag}", "mat_foliage", radius=r,
                                     location=(x, y, 0.24), scale=(1.0, 1.0, 0.65),
                                     rotation=(0.0, 0.0, rng.uniform(0, math.tau))))

    def pumpkin(tag: str, x: float, y: float) -> None:
        objects.append(add_icosphere(f"pumpkin_{tag}", "mat_warm_wood", radius=0.13,
                                     location=(x, y, 0.25), scale=(1.0, 1.0, 0.6),
                                     rotation=(0.0, 0.0, rng.uniform(0, math.tau))))
        objects.append(add_box(f"pumpkin_stem_{tag}", "mat_foliage",
                               size=(0.03, 0.03, 0.07), location=(x, y, 0.33)))

    xs = (-0.6, -0.3, 0.0, 0.3, 0.6)
    for i, x in enumerate(xs):  # row 0: tomatoes with leaves between
        tomato(f"r0_{i}", x + rng.uniform(-0.03, 0.03), row_ys[0] + rng.uniform(-0.03, 0.03))
    for i, x in enumerate((-0.55, -0.18, 0.18, 0.55)):  # row 1: leafy greens
        leaf(f"r1_{i}", x, row_ys[1] + rng.uniform(-0.03, 0.03))
    for i, x in enumerate((-0.55, -0.05, 0.5)):  # row 2: pumpkins
        pumpkin(f"r2_{i}", x, row_ys[2] + rng.uniform(-0.03, 0.03))
    # row 3: mixed leaf + tomato
    leaf("r3_0", -0.5, row_ys[3])
    tomato("r3_1", -0.1, row_ys[3] + 0.02)
    leaf("r3_2", 0.28, row_ys[3] - 0.02, r=0.1)
    tomato("r3_3", 0.62, row_ys[3])

    # --- anchors --------------------------------------------------------------------------
    objects.append(add_anchor("work_slot_1", (-0.6, -0.95, 0.0), anchor_type="work"))
    objects.append(add_anchor("work_slot_2", (0.6, 0.95, 0.0), anchor_type="work"))
    return objects


# ---------------------------------------------------------------------------
# abbey_town_diorama_t1
# ---------------------------------------------------------------------------


def _place_group(
    objects: list[bpy.types.Object],
    prefix: str,
    location: tuple[float, float, float],
    yaw_deg: float = 0.0,
    scale: float = 1.0,
) -> list[bpy.types.Object]:
    """Move a just-built mini-asset into the diorama composition.

    Builders create objects around the origin. This helper keeps their internal
    silhouette intact while placing them on the reference tile.
    """
    yaw = DEG(yaw_deg)
    cos_y = math.cos(yaw)
    sin_y = math.sin(yaw)
    offset = Vector(location)
    for obj in objects:
        obj.name = f"{prefix}_{obj.name}"
        p = Vector(obj.location) * scale
        obj.location = Vector((p.x * cos_y - p.y * sin_y, p.x * sin_y + p.y * cos_y, p.z)) + offset
        obj.rotation_euler.z += yaw
        obj.scale = (obj.scale.x * scale, obj.scale.y * scale, obj.scale.z * scale)
    return objects


def _gable_roof_pair(
    objects: list[bpy.types.Object],
    tag: str,
    center: tuple[float, float, float],
    length: float,
    depth: float,
    yaw_deg: float = 0.0,
) -> None:
    """Two terracotta slabs and a ridge, used by the diorama annexes."""
    x, y, z = center
    yaw = DEG(yaw_deg)
    pitch = DEG(50.0)
    for i, sy in enumerate((1, -1)):
        objects.append(add_box(
            f"{tag}_roof_{i}", "mat_thatch", size=(length, depth * 0.72, 0.11),
            location=(x, y + sy * depth * 0.18, z), rotation=(-sy * pitch, 0.0, yaw)
        ))
    objects.append(add_box(
        f"{tag}_ridge", "mat_thatch", size=(length, 0.14, 0.1),
        location=(x, y, z + depth * 0.47), rotation=(0.0, 0.0, yaw)
    ))


def _small_crate(objects: list[bpy.types.Object], tag: str, x: float, y: float, yaw: float) -> None:
    objects.append(add_box(f"crate_{tag}", "mat_dark_wood", size=(0.34, 0.34, 0.28),
                           location=(x, y, 0.19), rotation=(0.0, 0.0, DEG(yaw))))
    objects.append(add_box(f"crate_strap_{tag}", "mat_warm_wood", size=(0.38, 0.05, 0.04),
                           location=(x, y, 0.35), rotation=(0.0, 0.0, DEG(yaw))))


def _barrel(objects: list[bpy.types.Object], tag: str, x: float, y: float) -> None:
    objects.append(add_cylinder(f"barrel_{tag}", "mat_warm_wood", radius=0.18, depth=0.45,
                                vertices=8, location=(x, y, 0.33)))
    objects.append(add_cylinder(f"barrel_hoop_{tag}", "mat_dark_wood", radius=0.185, depth=0.05,
                                vertices=8, location=(x, y, 0.52)))


@register_builder("abbey_town_diorama_t1")
def build_abbey_town_diorama(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []

    # --- square diorama slab: grass top with exposed dirt sides ----------------------
    objects.append(add_box("grass_top", "mat_foliage", size=(12.0, 12.0, 0.18), location=(0, 0, 0.0)))
    objects.append(add_box("cliff_front", "mat_dirt", size=(12.0, 0.34, 0.72), location=(0, -6.17, -0.36)))
    objects.append(add_box("cliff_right", "mat_dirt", size=(0.34, 12.0, 0.72), location=(6.17, 0, -0.36)))
    objects.append(add_box("cliff_back_lip", "mat_dirt", size=(12.0, 0.18, 0.38), location=(0, 6.09, -0.19)))
    objects.append(add_box("cliff_left_lip", "mat_dirt", size=(0.18, 12.0, 0.38), location=(-6.09, 0, -0.19)))

    # --- paths and plaza: tan roads wrapping a gray stone court ----------------------
    objects.append(add_box("central_plaza", "mat_ash", size=(3.55, 3.05, 0.06), location=(1.3, -0.65, 0.12)))
    objects.append(add_box("path_left_to_plaza", "mat_dirt", size=(4.2, 0.72, 0.07),
                           location=(-2.75, -1.55, 0.14), rotation=(0, 0, DEG(-10))))
    objects.append(add_box("path_front_curve", "mat_dirt", size=(5.0, 0.74, 0.07),
                           location=(-1.4, -4.0, 0.14), rotation=(0, 0, DEG(11))))
    objects.append(add_box("path_to_church", "mat_dirt", size=(0.78, 3.2, 0.07),
                           location=(-3.7, 0.4, 0.14), rotation=(0, 0, DEG(-20))))
    objects.append(add_box("path_to_town_gate", "mat_dirt", size=(0.75, 2.9, 0.07),
                           location=(3.7, -2.2, 0.14), rotation=(0, 0, DEG(32))))

    # --- left abbey complex: church with attached cloister --------------------------
    church = build_abbey_church({})
    objects.extend(_place_group(church, "church", location=(-3.45, 1.2, 0.16), yaw_deg=0.0, scale=1.22))
    cloister = build_abbey_cloister({})
    objects.extend(_place_group(cloister, "cloister", location=(-4.05, -1.0, 0.16), yaw_deg=0.0, scale=1.2))

    # Extra enclosed garth edge so the abbey reads as one connected compound.
    objects.append(add_box("abbey_link_wall", "mat_old_stone", size=(0.28, 1.2, 0.82), location=(-2.1, -0.05, 0.57)))
    objects.append(add_box("abbey_link_roof", "mat_thatch", size=(0.5, 1.36, 0.12),
                           location=(-2.1, -0.05, 1.12), rotation=(DEG(16), 0, 0)))

    # --- right town-hall complex: enlarged with annexes and attached belltower -------
    town = build_town_hall({})
    objects.extend(_place_group(town, "town", location=(3.0, 0.15, 0.16), yaw_deg=0.0, scale=1.28))
    tower = build_bell_tower_t2({})
    tower_x, tower_y, tower_scale = 4.25, 2.15, 1.26
    objects.extend(_place_group(tower, "tower", location=(tower_x, tower_y, 0.16), yaw_deg=0.0, scale=tower_scale))

    # Annex masses make the town hall read like the large clustered reference building.
    for tag, x, y, sx, sy, h in (
        ("east_annex", 4.85, 0.55, 1.55, 1.45, 1.25),
        ("front_annex", 3.55, -1.95, 1.55, 1.05, 1.05),
        ("rear_annex", 4.35, 2.0, 1.35, 1.0, 1.15),
    ):
        objects.append(add_box(f"{tag}_stone", "mat_old_stone", size=(sx, sy, h), location=(x, y, 0.16 + h / 2)))
        objects.append(add_box(f"{tag}_plaster", "mat_canvas", size=(sx * 1.08, sy * 1.08, 0.75),
                               location=(x, y, 0.16 + h + 0.38)))
        _gable_roof_pair(objects, tag, (x, y, 0.16 + h + 1.1), sx * 1.18, sy * 1.28)
        objects.append(add_box(f"{tag}_beam", "mat_dark_wood", size=(sx * 1.12, 0.09, 0.1),
                               location=(x, y - sy * 0.55, 0.16 + h + 0.45)))
        objects.append(add_box(f"{tag}_brace_l", "mat_dark_wood", size=(0.08, 0.08, 0.72),
                               location=(x - sx * 0.25, y - sy * 0.57, 0.16 + h + 0.43),
                               rotation=(0, DEG(34), 0)))
        objects.append(add_box(f"{tag}_brace_r", "mat_dark_wood", size=(0.08, 0.08, 0.72),
                               location=(x + sx * 0.25, y - sy * 0.57, 0.16 + h + 0.43),
                               rotation=(0, DEG(-34), 0)))

    # Chimney smoke and blue/yellow pennant echoes from the reference.
    for i, (r, z, dx) in enumerate(((0.12, 5.2, 0.0), (0.16, 5.55, 0.08), (0.11, 5.88, -0.02))):
        objects.append(add_icosphere(f"smoke_{i}", "mat_ash", radius=r, location=(4.55 + dx, 1.85, z), scale=(1.0, 1.0, 0.7)))
    objects.append(add_cylinder("pennant_pole", "mat_dark_wood", radius=0.025, depth=0.95,
                                vertices=5, location=(tower_x, tower_y, 7.85)))
    objects.append(add_box("pennant_blue", "mat_wet_stone", size=(0.92, 0.05, 0.22),
                           location=(tower_x + 0.45, tower_y, 8.12), rotation=(0, 0, DEG(-7))))
    objects.append(add_box("pennant_yellow", "mat_sacred_gold", size=(0.74, 0.055, 0.12),
                           location=(tower_x + 0.38, tower_y, 8.03), rotation=(0, 0, DEG(-7))))

    # --- village ground details ------------------------------------------------------
    field = build_field_plot({})
    objects.extend(_place_group(field, "field", location=(-1.7, -4.3, 0.16), yaw_deg=7.0, scale=1.1))

    # Well in the front-left quadrant.
    objects.append(add_cylinder("well_ring", "mat_old_stone", radius=0.42, depth=0.42,
                                vertices=10, location=(-4.2, -4.1, 0.38)))
    objects.append(add_cylinder("well_water", "mat_wet_stone", radius=0.31, depth=0.05,
                                vertices=10, location=(-4.2, -4.1, 0.61)))
    objects.append(add_box("well_post_l", "mat_dark_wood", size=(0.09, 0.09, 0.9), location=(-4.55, -4.1, 0.78)))
    objects.append(add_box("well_post_r", "mat_dark_wood", size=(0.09, 0.09, 0.9), location=(-3.85, -4.1, 0.78)))
    _gable_roof_pair(objects, "well", (-4.2, -4.1, 1.42), 0.95, 0.75)

    for i, (x, y) in enumerate(((2.0, -2.45), (2.55, -2.55), (4.55, -2.2), (5.05, -1.75), (-5.0, -2.0))):
        _barrel(objects, str(i), x, y)
    for i, (x, y, yaw) in enumerate(((4.85, -2.85, 8), (5.3, -2.65, -12), (2.1, -3.0, 22), (-5.0, -3.1, -8))):
        _small_crate(objects, str(i), x, y, yaw)

    # Tiny animal silhouettes sell the bucolic day read without adding new materials.
    for i, (x, y) in enumerate(((-4.9, -4.85), (-4.45, -4.65), (-2.8, -4.95))):
        objects.append(add_icosphere(f"chicken_body_{i}", "mat_sacred_gold", radius=0.09,
                                     location=(x, y, 0.27), scale=(1.2, 0.8, 0.7)))
    objects.append(add_icosphere("dog_body", "mat_warm_wood", radius=0.16, location=(2.0, -4.35, 0.32),
                                 scale=(1.6, 0.75, 0.7)))
    objects.append(add_icosphere("dog_head", "mat_warm_wood", radius=0.09, location=(2.23, -4.35, 0.38)))

    # --- public anchors for Unity/proof inspection -----------------------------------
    objects.append(add_anchor("church_door", (-5.85, 1.2, 0.16), anchor_type="door"))
    objects.append(add_anchor("town_hall_door", (3.0, -1.05, 0.16), anchor_type="door"))
    objects.append(add_anchor("bell", (tower_x, tower_y, 5.65), anchor_type="bell"))
    objects.append(add_anchor("plaza_center", (1.3, -0.65, 0.18), anchor_type="work"))
    objects.append(add_anchor("well", (-4.2, -4.1, 0.64), anchor_type="water"))
    objects.append(add_anchor("field_work", (-1.7, -4.3, 0.2), anchor_type="work"))

    return objects
