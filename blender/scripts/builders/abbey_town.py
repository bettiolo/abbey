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
    """Pointed-arch stained-glass window: body slab + diamond cap (mat_wet_stone).

    facing: 'y' = window on a +-Y wall (slab thin in Y), 'x' = on a +-X wall.
    """
    x, y, z = location
    if facing == "y":
        size = (width, 0.07, height)
        cap_scale = (1.0, 0.3, 1.0)
    else:
        size = (0.07, width, height)
        cap_scale = (0.3, 1.0, 1.0)
    objects.append(add_box(f"win_{tag}_body", "mat_wet_stone", size=size, location=(x, y, z)))
    cap = add_cone(f"win_{tag}_cap", "mat_wet_stone", radius=width * 0.72, depth=width * 1.4,
                   vertices=4, location=(x, y, z + height / 2.0 + width * 0.55))
    cap.scale = (cap.scale.x * cap_scale[0], cap.scale.y * cap_scale[1], cap.scale.z * cap_scale[2])
    objects.append(cap)


@register_builder("abbey_church_t1")
def build_abbey_church(spec: dict) -> list[bpy.types.Object]:
    objects: list[bpy.types.Object] = []

    # --- nave + crossing masses (warm stone) -------------------------------------
    objects.append(add_box("plinth", "mat_old_stone", size=(3.9, 1.85, 0.24), location=(-0.03, 0, 0.12)))
    objects.append(add_box("nave", "mat_old_stone", size=(2.86, 1.5, 1.7), location=(-0.5, 0, 1.09)))
    objects.append(add_box("crossing", "mat_old_stone", size=(1.1, 1.7, 2.7), location=(1.40, 0, 1.59)))
    objects.append(add_box("crossing_cornice", "mat_old_stone", size=(1.2, 1.8, 0.12),
                           location=(1.40, 0, 2.95)))

    # --- gable_roof: steep terracotta slabs + ridge cap ---------------------------
    pitch = DEG(50.0)
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"nave_roof_{i}", "mat_thatch", size=(2.95, 1.3, 0.12),
                    location=(-0.5, sy * 0.42, 2.4), rotation=(-sy * pitch, 0.0, 0.0))
        )
    objects.append(add_box("ridge_cap", "mat_thatch", size=(2.95, 0.17, 0.12),
                           location=(-0.5, 0.0, 2.94)))

    # --- west gable (stepped warm stone) + stone cross finial ---------------------
    for i, (w, z) in enumerate(((1.5, 2.05), (1.05, 2.35), (0.62, 2.65), (0.3, 2.92))):
        objects.append(add_box(f"west_gable_{i}", "mat_old_stone", size=(0.15, w, 0.32),
                               location=(-1.84, 0.0, z)))
    objects.append(add_box("cross_post", "mat_old_stone", size=(0.07, 0.07, 0.42),
                           location=(-1.84, 0.0, 3.25)))
    objects.append(add_box("cross_arm", "mat_old_stone", size=(0.07, 0.26, 0.07),
                           location=(-1.84, 0.0, 3.32)))

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
                                vertices=12, location=(-1.88, 0.0, 2.3),
                                rotation=(0.0, DEG(90.0), 0.0)))
    objects.append(add_cylinder("rose_glass", "mat_wet_stone", radius=0.26, depth=0.09,
                                vertices=12, location=(-1.88, 0.0, 2.3),
                                rotation=(0.0, DEG(90.0), 0.0)))

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
                           location=(-1.89, 0.0, 0.89)))
    objects.append(add_box("archivolt_inner", "mat_old_stone", size=(0.13, 0.78, 1.15),
                           location=(-1.925, 0.0, 0.815)))
    objects.append(add_box("portal_door", "mat_wet_stone", size=(0.09, 0.56, 0.95),
                           location=(-1.94, 0.0, 0.715)))
    objects.append(add_box("portal_step", "mat_old_stone", size=(0.2, 1.0, 0.13),
                           location=(-1.89, 0.0, 0.065)))
    objects.append(
        add_box("portal_gable", "mat_old_stone", size=(0.14, 0.22, 0.22),
                location=(-1.89, 0.0, 1.62), rotation=(DEG(45.0), 0.0, 0.0))
    )

    # --- crossing_spire: octagonal drum + spire + gold finial -----------------------
    # (spire = mat_wet_stone: 4-material budget compromise, doc'd in ART_REFERENCE)
    objects.append(add_cylinder("spire_drum", "mat_old_stone", radius=0.58, depth=0.5,
                                vertices=8, location=(1.40, 0.0, 3.2)))
    objects.append(add_cone("spire", "mat_wet_stone", radius=0.52, depth=1.9, vertices=8,
                            location=(1.40, 0.0, 4.4)))
    objects.append(add_cylinder("finial_rod", "mat_sacred_gold", radius=0.03, depth=0.3,
                                vertices=5, location=(1.40, 0.0, 5.4)))
    objects.append(add_cone("finial_tip", "mat_sacred_gold", radius=0.08, depth=0.18,
                            vertices=6, location=(1.40, 0.0, 5.6)))

    # --- anchors ---------------------------------------------------------------------
    objects.append(add_anchor("door", (-1.97, 0.0, 0.0), anchor_type="door"))
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
    objects.append(add_box("plinth", "mat_old_stone", size=(2.85, 1.75, 0.2), location=(0, 0, 0.1)))
    objects.append(add_box("ground_floor", "mat_old_stone", size=(2.7, 1.6, 1.2),
                           location=(0, 0, 0.8)))

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

    # --- jettied_storeys: two overhanging plaster floors --------------------------------
    objects.append(add_box("storey_1", "mat_canvas", size=(2.85, 1.75, 0.9), location=(0, 0, 1.85)))
    objects.append(add_box("storey_2", "mat_canvas", size=(2.95, 1.85, 0.9), location=(0, 0, 2.75)))

    # jetty sill / plate beams + corner posts (dark timber)
    for level, (z, w, d) in enumerate(((1.42, 2.9, 1.8), (2.32, 2.96, 1.86), (3.22, 2.96, 1.86))):
        for i, sy in enumerate((1, -1)):
            objects.append(add_box(f"beam_y_{level}_{i}", "mat_dark_wood", size=(w, 0.13, 0.13),
                                   location=(0.0, sy * (d / 2.0 - 0.04), z)))
        for i, sx in enumerate((1, -1)):
            objects.append(add_box(f"beam_x_{level}_{i}", "mat_dark_wood",
                                   size=(0.13, d - 0.05, 0.13),
                                   location=(sx * (w / 2.0 - 0.04), 0.0, z)))
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        objects.append(add_box(f"post_s1_{i}", "mat_dark_wood", size=(0.13, 0.13, 0.92),
                               location=(sx * 1.4, sy * 0.85, 1.85)))
        objects.append(add_box(f"post_s2_{i}", "mat_dark_wood", size=(0.13, 0.13, 0.92),
                               location=(sx * 1.43, sy * 0.9, 2.75)))

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

    # shuttered windows (dark) on the upper storeys
    for i, x in enumerate((-0.72, 0.0, 0.72)):
        objects.append(add_box(f"window_s2_{i}", "mat_dark_wood", size=(0.3, 0.06, 0.4),
                               location=(x, -0.94, 2.78)))
    for i, x in enumerate((-0.72, 0.72)):
        objects.append(add_box(f"window_s1_{i}", "mat_dark_wood", size=(0.3, 0.06, 0.38),
                               location=(x, -0.89, 1.88)))

    # --- multi_gable_roof: steep main gable + gablet + dormers + ridge -------------------
    pitch = DEG(52.0)
    for i, sy in enumerate((1, -1)):
        objects.append(
            add_box(f"roof_slab_{i}", "mat_thatch", size=(2.95, 1.65, 0.14),
                    location=(0.0, sy * 0.44, 3.85), rotation=(-sy * pitch, 0.0, 0.0))
        )
    objects.append(add_box("ridge_cap", "mat_thatch", size=(2.95, 0.18, 0.12),
                           location=(0.0, 0.0, 4.52)))
    # gable end walls (stepped plaster) at +-X
    for i, sx in enumerate((1, -1)):
        for j, (w, z) in enumerate(((1.7, 3.4), (1.2, 3.74), (0.72, 4.06), (0.3, 4.34))):
            objects.append(add_box(f"gable_{i}_{j}", "mat_canvas", size=(0.14, w, 0.34),
                                   location=(sx * 1.42, 0.0, z)))

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
    objects.append(add_box("gablet_body", "mat_canvas", size=(0.72, 0.5, 0.75),
                           location=(0.95, -0.68, 3.55)))
    objects.append(add_box("gablet_brace_l", "mat_dark_wood", size=(0.08, 0.05, 0.6),
                           location=(0.78, -0.94, 3.5), rotation=(0.0, DEG(35.0), 0.0)))
    objects.append(add_box("gablet_brace_r", "mat_dark_wood", size=(0.08, 0.05, 0.6),
                           location=(1.12, -0.94, 3.5), rotation=(0.0, DEG(-35.0), 0.0)))
    for i, sx in enumerate((1, -1)):
        objects.append(
            add_box(f"gablet_roof_{i}", "mat_thatch", size=(0.52, 0.66, 0.07),
                    location=(0.95 + sx * 0.19, -0.66, 4.1), rotation=(0.0, -sx * DEG(42.0), 0.0))
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
