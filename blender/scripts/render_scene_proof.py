"""Aesthetic-gate scene proof: assemble the Map 1 vignette and render it twice.

This is the visual evidence for human review gate #1/#2 (AGENTS.md): the same
orthographic pitch-30/yaw-45 camera (ART_BIBLE.md camera contract) renders

  1. blender/generated/previews/scene_proof_day.png   — warm bucolic daylight,
     palette target docs/abbey-town-1.png (docs/ART_REFERENCE_ABBEY.md);
  2. blender/generated/previews/scene_proof_night.png — near-dark blue ambient
     where the ONLY meaningful light is firelight pools (campfire, lanterns,
     warm windows, one sacred candle glow at the tower door). Darkness must
     read as hostile territory: lit pools clearly delimited, the Black Hound
     silhouette readable near the ruined bell tower.

The scene ASSEMBLES THE COMMITTED GLBs from blender/generated/glb/ — the same
bytes Unity imports — rather than rebuilding via the builder registry, so the
proof also exercises the committed export artifacts (materials, pixel textures
and emissive strengths must survive the GLB round-trip). Placements are a
hardcoded deterministic table (no RNG anywhere).

The diorama ground slab (grass top / cliff-soil skirt, ART_REFERENCE "Ground
kit") is built in-scene. It is the first consumer of ``tex_cliff_soil``; the
skirt material is a SCENE-LOCAL material (``scene_cliff_soil``) wired exactly
like the library materials — it is never exported into any asset GLB, so the
closed 17-material library stays closed.

Usage (both environments, like the other pipeline scripts)::

    python3 blender/scripts/render_scene_proof.py            # full quality
    python3 blender/scripts/render_scene_proof.py --samples 8 --scale 0.5
    blender -b -P blender/scripts/render_scene_proof.py -- --samples 8

Outputs::

    blender/generated/previews/scene_proof_day.png
    blender/generated/previews/scene_proof_night.png
    blender/generated/metadata/scene_proof.meta.json
"""

from __future__ import annotations

import argparse
import datetime
import json
import math
import sys
from pathlib import Path

_SCRIPTS_DIR = str(Path(__file__).resolve().parent)
if _SCRIPTS_DIR not in sys.path:
    sys.path.insert(0, _SCRIPTS_DIR)

import bpy
from mathutils import Euler, Vector

import abbey_materials as am
import asset_framework as fw

# ---------------------------------------------------------------------------
# Render settings (task: 1280 wide, Cycles CPU, 32-64 samples)
# ---------------------------------------------------------------------------

RESOLUTION_X = 1280
RESOLUTION_Y = 960
SAMPLES = 64

CAMERA_PITCH_DEG = 30.0  # ART_BIBLE camera contract — never break this
CAMERA_YAW_DEG = 45.0
CAMERA_MARGIN = 1.05

# Compass convention: +X = east, +Y = north. The iso camera sits to the
# south-east looking north-west, so: NE = screen right, NW = screen up/away,
# SW = screen left, SE = foreground.

# ---------------------------------------------------------------------------
# Ground slab (diorama look: grass top, cliff-soil skirt)
# ---------------------------------------------------------------------------

SLAB = {"x": (-9.5, 9.5), "y": (-9.5, 9.5), "z": (-1.8, 0.0)}
# Abbey hill NE: two grass terraces with cliff-soil risers.
TERRACE_1 = {"x": (3.0, 9.5), "y": (3.0, 9.5), "z": (0.0, 0.8)}
TERRACE_2 = {"x": (4.6, 9.5), "y": (4.6, 9.5), "z": (0.8, 1.6)}
# Beach SW: thin sand shelf on top of the grass.
SAND = {"x": (-9.5, -1.8), "y": (-9.5, -1.8), "z": (-0.01, 0.045)}

# ---------------------------------------------------------------------------
# Placement table — the whole vignette, fully deterministic.
# (asset_id, x, y, z, rot_z_deg, uniform_scale)
# ---------------------------------------------------------------------------

PLACEMENTS: list[tuple[str, float, float, float, float, float]] = [
    # --- abbey hill NE (on the terraces) ---
    ("bell_tower_ruined", 7.6, 7.8, 1.6, 0.0, 1.0),
    ("abbey_wall_broken", 5.3, 9.0, 1.6, 0.0, 1.0),
    ("abbey_wall_broken", 9.0, 5.4, 1.6, 90.0, 1.0),
    ("abbey_wall_broken", 4.2, 3.9, 0.8, 45.0, 1.0),
    ("hound_chain", 6.4, 5.8, 1.6, 25.0, 1.0),
    ("black_hound_lowpoly", 6.0, 5.5, 1.6, -105.0, 1.0),
    # --- camp center (plaza of paving patches, campfire on top) ---
    ("paving_patch", 0.0, 0.0, 0.0, 0.0, 1.0),
    ("paving_patch", 1.0, 0.0, 0.004, 0.0, 1.0),
    ("paving_patch", 0.0, 1.0, 0.008, 0.0, 1.0),
    ("paving_patch", 1.0, 1.0, 0.012, 0.0, 1.0),
    ("campfire_t1", 0.5, 0.5, 0.112, 0.0, 1.0),
    ("shelter_t1", -2.6, 2.3, 0.0, 20.0, 1.0),
    ("shelter_t1", 2.8, 3.0, 0.0, -15.0, 1.0),
    ("storage_pile_t1", 2.6, -0.6, 0.0, 10.0, 1.0),
    ("shipwreck_crate_closed", 3.3, 0.5, 0.0, 30.0, 1.0),
    ("shipwreck_barrel", 1.9, -0.7, 0.0, 0.0, 1.0),
    ("well_t1", -2.9, -1.0, 0.0, 0.0, 1.0),
    ("lantern_post_t1", 1.7, 1.7, 0.0, -45.0, 1.0),
    ("lantern_post_t1", -3.2, -3.0, 0.045, 0.0, 1.0),
    ("lantern_post_t1", 4.2, -2.2, 0.0, 90.0, 1.0),
    ("lantern_post_t1", 4.4, 2.4, 0.0, 0.0, 1.0),
    ("pennant_pole", -0.9, 1.4, 0.0, 0.0, 1.0),
    # --- town flavour SE (town_hall_t1 chosen over bell_tower_t2: the ruined
    #     tower already owns the skyline; the guildhall adds half-timber mass
    #     by day and warm windows by night) ---
    ("town_hall_t1", 7.6, -6.9, 0.0, 0.0, 1.0),
    ("pennant_pole", 5.9, -5.7, 0.0, -30.0, 1.0),
    ("field_plot_t1", 2.2, -6.0, 0.0, 0.0, 1.0),
    ("field_plot_t1", 4.5, -7.2, 0.0, 90.0, 1.0),
    ("field_plot_t1", 2.6, -8.2, 0.0, 0.0, 1.0),
    ("shipwreck_barrel", 5.3, -7.8, 0.0, 20.0, 1.0),
    ("shipwreck_crate_closed", 6.2, -8.2, 0.0, 65.0, 1.0),
    ("rock_cluster_01", 8.7, -8.6, 0.0, 200.0, 0.9),
    # --- beach SW (on the sand shelf) ---
    ("shipwreck_hull", -6.2, -6.2, 0.045, 40.0, 1.0),
    ("shipwreck_crate_closed", -4.0, -4.9, 0.045, 15.0, 1.0),
    ("shipwreck_crate_closed", -4.9, -3.9, 0.045, 40.0, 1.0),
    ("shipwreck_crate_closed", -3.4, -6.0, 0.045, 70.0, 1.0),
    ("shipwreck_barrel", -4.5, -6.1, 0.045, 0.0, 1.0),
    ("shipwreck_barrel", -2.7, -4.2, 0.045, 55.0, 1.0),
    ("shipwreck_barrel", -5.9, -4.6, 0.045, 110.0, 1.0),
    # --- forest edge NW (dense band along the top edge) ---
    ("forest_tree_01", -8.4, 8.5, 0.0, 10.0, 1.5),
    ("forest_tree_01", -7.0, 8.9, 0.0, 80.0, 1.25),
    ("forest_tree_01", -9.0, 7.0, 0.0, 150.0, 1.4),
    ("forest_tree_01", -6.0, 7.7, 0.0, 210.0, 1.6),
    ("forest_tree_01", -7.7, 6.2, 0.0, 300.0, 1.2),
    ("forest_tree_01", -5.3, 8.9, 0.0, 45.0, 1.35),
    ("forest_tree_01", -8.9, 9.2, 0.0, 120.0, 1.7),
    ("forest_tree_01", -4.3, 6.4, 0.0, 260.0, 1.1),
    ("forest_tree_01", -6.7, 5.6, 0.0, 330.0, 1.0),
    ("forest_tree_01", -5.6, 7.0, 0.0, 175.0, 1.45),
    ("forest_tree_01", -8.3, 4.9, 0.0, 60.0, 1.15),
    ("forest_tree_01", -4.6, 8.2, 0.0, 285.0, 1.55),
    ("forest_tree_01", -2.9, 7.9, 0.0, 135.0, 1.2),
    ("rock_cluster_01", -4.6, 4.6, 0.0, 30.0, 1.0),
    ("rock_cluster_01", -9.0, 4.2, 0.0, 120.0, 1.1),
    ("forest_tree_01", -1.5, 8.8, 0.0, 95.0, 1.3),
    ("forest_tree_01", 0.6, 8.3, 0.0, 240.0, 1.5),
    ("forest_tree_01", 2.0, 8.9, 0.0, 15.0, 1.2),
    ("rock_cluster_01", 0.4, 6.4, 0.0, 75.0, 0.95),
    # lone meadow trees framing the east edge behind the town hall
    ("forest_tree_01", 8.9, -0.8, 0.0, 220.0, 1.3),
    ("forest_tree_01", 8.2, -2.3, 0.0, 20.0, 1.15),
    # --- dirt road: beach -> plaza -> abbey hill base; spur to the town hall.
    #     Tiny per-tile z offsets kill coplanar z-fighting. ---
    ("dirt_road_segment", -4.0, -4.0, 0.046, 0.0, 1.0),
    ("dirt_road_segment", -3.0, -3.0, 0.048, 0.0, 1.0),
    ("dirt_road_segment", -2.0, -2.0, 0.002, 0.0, 1.0),
    ("dirt_road_segment", -1.1, -1.1, 0.004, 0.0, 1.0),
    ("dirt_road_segment", 2.0, 1.3, 0.002, 0.0, 1.0),
    ("dirt_road_segment", 2.9, 2.1, 0.004, 0.0, 1.0),
    ("dirt_road_segment", 3.7, 2.8, 0.006, 0.0, 1.0),
    ("dirt_road_segment", 2.0, -1.8, 0.006, 0.0, 1.0),
    ("dirt_road_segment", 2.9, -2.6, 0.008, 0.0, 1.0),
    ("dirt_road_segment", 3.8, -3.4, 0.01, 0.0, 1.0),
    ("dirt_road_segment", 4.7, -4.2, 0.012, 0.0, 1.0),
    ("dirt_road_segment", 5.5, -5.0, 0.014, 0.0, 1.0),
    ("dirt_road_segment", 6.3, -5.8, 0.016, 0.0, 1.0),
]

# ---------------------------------------------------------------------------
# Lighting rigs
# ---------------------------------------------------------------------------

DAY_WORLD = {"color": (0.55, 0.70, 0.85), "strength": 0.55}
DAY_SUN = {
    "color": (1.0, 0.94, 0.82),
    "energy": 4.0,
    "rotation_deg": (50.0, -8.0, 65.0),  # same fixed sun as render_preview.py
}

NIGHT_WORLD = {"color": (0.010, 0.020, 0.055), "strength": 0.11}
NIGHT_MOON = {
    "color": (0.55, 0.65, 1.0),
    "energy": 0.10,
    "rotation_deg": (55.0, 0.0, 160.0),  # cold rim from the north, silhouettes only
}
# Hand-placed warm pools (positions in world space; anchors add more below).
NIGHT_POINTS: list[dict] = [
    # campfire core (backs up the ember_glow anchor light — the main pool)
    {"name": "fire_core", "pos": (0.5, 0.5, 0.75), "color": (1.0, 0.52, 0.20),
     "energy": 280.0, "radius": 0.25},
    # sacred candle glow at the ruined tower door — silhouettes the hound
    {"name": "tower_candle", "pos": (6.7, 6.2, 1.7), "color": (1.0, 0.60, 0.28),
     "energy": 120.0, "radius": 0.30},
    # warm spill outside the town hall's lit windows
    {"name": "townhall_window", "pos": (7.6, -8.1, 2.6), "color": (1.0, 0.65, 0.30),
     "energy": 60.0, "radius": 0.40},
]
# One narrow cold moon-spot on the hound's spot by the tower: keeps the beast
# readable at the edge of the candle pool without adding a second warm pool
# ("shapes visible at the edge of light", ART_BIBLE night assets).
NIGHT_SPOT = {
    "name": "moon_spot_hound",
    "pos": (11.0, 1.0, 7.5),
    "target": (6.0, 5.5, 2.2),
    "color": (0.45, 0.55, 0.95),
    "energy": 180.0,
    "spot_size_deg": 32.0,
    "blend": 0.6,
}
# Anchor-driven lights: every imported anchor whose name starts with a key
# below gets a point light at its world position (+ offset).
NIGHT_ANCHOR_LIGHTS = {
    "flame": {"color": (1.0, 0.60, 0.25), "energy": 70.0, "radius": 0.10,
              "offset": (0.0, 0.0, 0.05)},          # lantern_post_t1 flames
    "ember_glow": {"color": (1.0, 0.45, 0.15), "energy": 60.0, "radius": 0.15,
                   "offset": (0.0, 0.0, 0.15)},     # campfire embers
}

# ---------------------------------------------------------------------------
# Scene assembly
# ---------------------------------------------------------------------------


def _scene_cliff_material() -> bpy.types.Material:
    """Scene-local diorama skirt material sampling tex_cliff_soil.

    First consumer of tex_cliff_soil (reserved for the ground tile in
    docs/ART_REFERENCE_ABBEY.md). Wired identically to the library materials
    but deliberately NOT added to the closed library — it never ships in an
    asset GLB. Base color = cliff_soil ramp mid #573828.
    """
    name = "scene_cliff_soil"
    existing = bpy.data.materials.get(name)
    if existing is not None:
        return existing
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = am._hex("#573828")
    bsdf.inputs["Roughness"].default_value = 1.0
    bsdf.inputs["Metallic"].default_value = 0.0
    am._wire_texture(mat, bsdf, "tex_cliff_soil")
    mat.diffuse_color = am._hex("#573828")
    return mat


def _make_box(
    name: str,
    bounds: dict,
    mat_top: bpy.types.Material,
    mat_side: bpy.types.Material | None = None,
) -> bpy.types.Object:
    """Axis-aligned box; top face gets mat_top, other faces mat_side."""
    x0, x1 = bounds["x"]
    y0, y1 = bounds["y"]
    z0, z1 = bounds["z"]
    verts = [
        (x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
        (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1),
    ]
    faces = [
        (0, 3, 2, 1),  # bottom (normal -Z)
        (4, 5, 6, 7),  # top (normal +Z)
        (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7),  # sides
    ]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    fw.link_object(obj)
    mesh.materials.append(mat_top)
    if mat_side is not None:
        mesh.materials.append(mat_side)
        for poly in mesh.polygons:
            if poly.normal.z < 0.5:  # everything but the top face
                poly.material_index = 1
    return obj


def build_ground() -> bpy.types.Object:
    """Diorama ground: grass slab with cliff-soil skirt, hill terraces, sand."""
    grass = am.get_material("mat_foliage")   # tex_grass
    sand = am.get_material("mat_dirt")       # tex_dirt_path as wet beach sand
    cliff = _scene_cliff_material()          # tex_cliff_soil skirt

    parts = [
        _make_box("ground_slab", SLAB, grass, cliff),
        _make_box("ground_terrace_1", TERRACE_1, grass, cliff),
        _make_box("ground_terrace_2", TERRACE_2, grass, cliff),
        _make_box("ground_sand", SAND, sand),
    ]
    root = bpy.data.objects.new("ground_root", None)
    fw.link_object(root)
    for part in parts:
        part.parent = root
    bpy.context.view_layer.update()
    fw.apply_box_uvs(root)  # world-space 1 repeat / metre, same as the assets
    return root


def import_glb_asset(asset_id: str) -> bpy.types.Object:
    """Import blender/generated/glb/<id>.glb; return its root object.

    Collision proxy meshes (exported into the GLB for Unity) are deleted —
    glTF does not carry Blender's hide_render flag, so they would render.
    """
    glb_path = fw.GLB_DIR / f"{asset_id}.glb"
    if not glb_path.is_file():
        raise FileNotFoundError(f"Committed GLB missing: {glb_path}")
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(glb_path))
    new_objs = [o for o in bpy.data.objects if o not in before]

    for obj in list(new_objs):
        if obj.type == "MESH" and fw.COLLISION_SUFFIX in obj.name:
            new_objs.remove(obj)
            bpy.data.objects.remove(obj, do_unlink=True)

    roots = [o for o in new_objs if o.parent is None]
    if len(roots) != 1:
        raise RuntimeError(
            f"Expected 1 root in {glb_path.name}, got {[o.name for o in roots]}"
        )
    return roots[0]


def place_assets() -> list[dict]:
    """Import + place every entry of PLACEMENTS; returns placement records."""
    records = []
    for asset_id, x, y, z, rot_deg, scale in PLACEMENTS:
        root = import_glb_asset(asset_id)
        root.location = Vector((x, y, z))
        root.rotation_mode = "XYZ"
        root.rotation_euler = Euler((0.0, 0.0, math.radians(rot_deg)), "XYZ")
        root.scale = (scale, scale, scale)
        records.append(
            {
                "asset": asset_id,
                "object": root.name,
                "position": [x, y, z],
                "rotation_z_deg": rot_deg,
                "scale": scale,
            }
        )
    bpy.context.view_layer.update()
    return records


def anchor_world_positions() -> dict[str, list[Vector]]:
    """World positions of imported anchor empties, grouped by base anchor name."""
    out: dict[str, list[Vector]] = {}
    for obj in bpy.data.objects:
        if obj.type != "EMPTY" or obj.get("abbey_anchor") is None:
            continue
        base = obj.name.split(".")[0]
        out.setdefault(base, []).append(obj.matrix_world.translation.copy())
    return out


# ---------------------------------------------------------------------------
# Camera + renderer
# ---------------------------------------------------------------------------


def setup_camera(scene: bpy.types.Scene) -> bpy.types.Object:
    """Fixed iso camera (pitch 30, yaw 45), ortho scale fit to the whole scene."""
    rot = Euler(
        (math.radians(90.0 - CAMERA_PITCH_DEG), 0.0, math.radians(CAMERA_YAW_DEG)),
        "XYZ",
    )
    mat = rot.to_matrix()
    right = mat @ Vector((1.0, 0.0, 0.0))
    up = mat @ Vector((0.0, 1.0, 0.0))
    back = mat @ Vector((0.0, 0.0, 1.0))  # -forward

    meshes = [o for o in scene.objects if o.type == "MESH"]
    lo_u = lo_v = lo_w = math.inf
    hi_u = hi_v = hi_w = -math.inf
    for obj in meshes:
        for corner in obj.bound_box:
            p = obj.matrix_world @ Vector(corner)
            u, v, w = p.dot(right), p.dot(up), p.dot(back)
            lo_u, hi_u = min(lo_u, u), max(hi_u, u)
            lo_v, hi_v = min(lo_v, v), max(hi_v, v)
            lo_w, hi_w = min(lo_w, w), max(hi_w, w)

    aspect = RESOLUTION_X / RESOLUTION_Y
    width = hi_u - lo_u
    height = hi_v - lo_v
    ortho_scale = max(width, height * aspect) * CAMERA_MARGIN

    cam_data = bpy.data.cameras.new("scene_proof_camera")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = ortho_scale
    cam_data.clip_start = 0.1
    cam_data.clip_end = 500.0
    cam = bpy.data.objects.new("scene_proof_camera", cam_data)
    cam.rotation_euler = rot
    cam.location = (
        right * ((lo_u + hi_u) / 2.0)
        + up * ((lo_v + hi_v) / 2.0)
        + back * (hi_w + 30.0)
    )
    fw.link_object(cam)
    scene.camera = cam
    return cam


def configure_renderer(scene: bpy.types.Scene, samples: int, scale_pct: int) -> None:
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 6
    scene.render.resolution_x = RESOLUTION_X
    scene.render.resolution_y = RESOLUTION_Y
    scene.render.resolution_percentage = scale_pct
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False


# ---------------------------------------------------------------------------
# Lighting
# ---------------------------------------------------------------------------


def _clear_lights() -> None:
    for obj in list(bpy.context.scene.objects):
        if obj.type == "LIGHT":
            bpy.data.objects.remove(obj, do_unlink=True)


def _set_world(color: tuple[float, float, float], strength: float) -> None:
    world = bpy.context.scene.world
    if world is None:
        world = bpy.data.worlds.new("scene_proof_world")
        bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg is None:
        bg = world.node_tree.nodes.new("ShaderNodeBackground")
        out = world.node_tree.nodes.new("ShaderNodeOutputWorld")
        world.node_tree.links.new(bg.outputs["Background"], out.inputs["Surface"])
    bg.inputs["Color"].default_value = (*color, 1.0)
    bg.inputs["Strength"].default_value = strength


def _add_sun(name: str, color, energy: float, rotation_deg) -> None:
    data = bpy.data.lights.new(name, "SUN")
    data.color = color
    data.energy = energy
    data.angle = math.radians(5.0)
    sun = bpy.data.objects.new(name, data)
    sun.rotation_euler = Euler(tuple(math.radians(a) for a in rotation_deg), "XYZ")
    fw.link_object(sun)


def _add_point(name: str, pos, color, energy: float, radius: float) -> None:
    data = bpy.data.lights.new(name, "POINT")
    data.color = color
    data.energy = energy
    data.shadow_soft_size = radius
    light = bpy.data.objects.new(name, data)
    light.location = Vector(pos)
    fw.link_object(light)


def _add_spot(spec: dict) -> None:
    data = bpy.data.lights.new(spec["name"], "SPOT")
    data.color = spec["color"]
    data.energy = spec["energy"]
    data.spot_size = math.radians(spec["spot_size_deg"])
    data.spot_blend = spec["blend"]
    data.shadow_soft_size = 0.4
    light = bpy.data.objects.new(spec["name"], data)
    light.location = Vector(spec["pos"])
    direction = Vector(spec["target"]) - Vector(spec["pos"])
    light.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    fw.link_object(light)


def apply_day_lighting() -> None:
    _clear_lights()
    _set_world(DAY_WORLD["color"], DAY_WORLD["strength"])
    _add_sun("day_sun", DAY_SUN["color"], DAY_SUN["energy"], DAY_SUN["rotation_deg"])


NIGHT_SACRED_GOLD_EMISSION = 0.03


def _dim_sacred_gold_for_night() -> None:
    """Cap mat_sacred_gold* emission so bells glint instead of glowing."""
    for mat in bpy.data.materials:
        if not mat.name.startswith("mat_sacred_gold") or not mat.use_nodes:
            continue
        for node in mat.node_tree.nodes:
            if node.type == "BSDF_PRINCIPLED":
                strength = node.inputs["Emission Strength"]
                strength.default_value = min(
                    strength.default_value, NIGHT_SACRED_GOLD_EMISSION
                )


def apply_night_lighting(anchors: dict[str, list[Vector]]) -> list[dict]:
    _clear_lights()
    _dim_sacred_gold_for_night()
    _set_world(NIGHT_WORLD["color"], NIGHT_WORLD["strength"])
    _add_sun("night_moon", NIGHT_MOON["color"], NIGHT_MOON["energy"],
             NIGHT_MOON["rotation_deg"])
    placed = []
    for spec in NIGHT_POINTS:
        _add_point(f"night_{spec['name']}", spec["pos"], spec["color"],
                   spec["energy"], spec["radius"])
        placed.append(dict(spec))
    _add_spot(NIGHT_SPOT)
    placed.append(dict(NIGHT_SPOT))
    for base, cfg in NIGHT_ANCHOR_LIGHTS.items():
        for i, pos in enumerate(anchors.get(base, [])):
            p = pos + Vector(cfg["offset"])
            _add_point(f"night_{base}_{i}", tuple(p), cfg["color"],
                       cfg["energy"], cfg["radius"])
            placed.append(
                {"name": f"{base}_{i}", "pos": [round(v, 4) for v in p],
                 "color": list(cfg["color"]), "energy": cfg["energy"],
                 "radius": cfg["radius"]}
            )
    return placed


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------


def _cli_args(argv: list[str]) -> argparse.Namespace:
    if "--" in argv:
        argv = argv[argv.index("--") + 1 :]
    else:
        argv = argv[1:]
    parser = argparse.ArgumentParser(description="Render the Map 1 scene proof")
    parser.add_argument("--samples", type=int, default=SAMPLES)
    parser.add_argument(
        "--scale", type=float, default=1.0,
        help="resolution multiplier for fast iteration (e.g. 0.5)",
    )
    parser.add_argument(
        "--only", choices=["day", "night"], default=None,
        help="render just one variant (iteration helper)",
    )
    return parser.parse_args(argv)


def _relpath(path: Path) -> str:
    try:
        return str(Path(path).resolve().relative_to(fw.BLENDER_DIR.parent))
    except ValueError:
        return str(path)


def main(argv: list[str]) -> int:
    args = _cli_args(argv)

    fw.reset_scene()
    scene = bpy.context.scene
    build_ground()
    placements = place_assets()
    anchors = anchor_world_positions()
    cam = setup_camera(scene)
    configure_renderer(scene, args.samples, int(args.scale * 100))

    fw.PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    outputs: dict[str, Path] = {}
    night_lights: list[dict] = []

    if args.only in (None, "day"):
        apply_day_lighting()
        out = fw.PREVIEW_DIR / "scene_proof_day.png"
        scene.render.filepath = str(out)
        bpy.ops.render.render(write_still=True)
        outputs["day"] = out
        print(f"rendered {out}")

    if args.only in (None, "night"):
        night_lights = apply_night_lighting(anchors)
        out = fw.PREVIEW_DIR / "scene_proof_night.png"
        scene.render.filepath = str(out)
        bpy.ops.render.render(write_still=True)
        outputs["night"] = out
        print(f"rendered {out}")

    metadata = {
        "id": "scene_proof",
        "generated_at": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "description": (
            "Map 1 vignette (VERTICAL_SLICE_SPEC.md section 2) assembled from the "
            "committed GLBs; the two aesthetic-gate proof renders."
        ),
        "source_glbs": sorted({p[0] for p in PLACEMENTS}),
        "ground": {
            "slab": SLAB, "terrace_1": TERRACE_1, "terrace_2": TERRACE_2,
            "sand": SAND,
            "materials": {
                "grass_top": "mat_foliage (tex_grass)",
                "cliff_skirt": "scene_cliff_soil (tex_cliff_soil, scene-local "
                               "material — first consumer of that texture)",
                "beach": "mat_dirt (tex_dirt_path)",
            },
        },
        "placements": placements,
        "camera": {
            "type": "ORTHO",
            "pitch_deg": CAMERA_PITCH_DEG,
            "yaw_deg": CAMERA_YAW_DEG,
            "ortho_scale": round(cam.data.ortho_scale, 4),
            "location": [round(v, 4) for v in cam.location],
            "identical_for_both_shots": True,
        },
        "render": {
            "engine": "CYCLES (CPU)",
            "samples": args.samples,
            "resolution": [RESOLUTION_X, RESOLUTION_Y],
            "resolution_percentage": int(args.scale * 100),
            "denoising": True,
        },
        "lighting": {
            "day": {"world": DAY_WORLD, "sun": DAY_SUN},
            "night": {
                "world": NIGHT_WORLD,
                "moon": NIGHT_MOON,
                "points": night_lights,
            },
        },
        "aesthetic_gate": {
            "scene_proof_day.png": (
                "Gate question 1: does the day read bucolic and palette-faithful "
                "to docs/abbey-town-1.png (warm terracotta/plaster/grass diorama, "
                "safe and inviting)?"
            ),
            "scene_proof_night.png": (
                "Gate question 2: does darkness read as hostile territory — "
                "near-dark blue ambient, ONLY firelight pools (campfire, "
                "lanterns, warm windows, tower candle), pools clearly delimited, "
                "Black Hound silhouette readable near the ruined tower?"
            ),
        },
        "files": {k: _relpath(v) for k, v in outputs.items()},
    }
    fw.METADATA_DIR.mkdir(parents=True, exist_ok=True)
    meta_path = fw.METADATA_DIR / "scene_proof.meta.json"
    with open(meta_path, "w", encoding="utf-8") as fh:
        json.dump(metadata, fh, indent=2)
        fh.write("\n")
    print(f"wrote {meta_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
