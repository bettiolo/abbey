"""Core framework for the headless Blender asset factory.

Responsibilities:
- locate and load JSON asset specs (``blender/asset_specs/**/<id>.json``)
- reset the scene to a clean state (works under the ``bpy`` module and the
  ``blender`` binary alike)
- builder registry: asset builders register themselves with
  ``@register_builder('<asset_id>')`` and receive the parsed spec
- geometry helpers: center-bottom pivot, named anchor empties, collision proxy
- measurement helpers: bounds/footprint, triangle count, material usage

Conventions (other pipeline stages rely on these):
- every asset has a single root empty named exactly ``<asset_id>``; all meshes,
  anchors and the collision proxy are parented to it
- anchor empties are named exactly as in the spec (e.g. ``ember_glow``)
- the collision proxy is a box mesh named ``<asset_id>_collision`` with no
  material; it is excluded from triangle/material budgets but included in the GLB
"""

from __future__ import annotations

import json
import math
from pathlib import Path
from typing import Callable, Iterable

import bpy
from mathutils import Vector

BLENDER_DIR = Path(__file__).resolve().parent.parent
SPECS_DIR = BLENDER_DIR / "asset_specs"
GENERATED_DIR = BLENDER_DIR / "generated"
GLB_DIR = GENERATED_DIR / "glb"
BLEND_DIR = GENERATED_DIR / "blend"
PREVIEW_DIR = GENERATED_DIR / "previews"
METADATA_DIR = GENERATED_DIR / "metadata"

COLLISION_SUFFIX = "_collision"

# ---------------------------------------------------------------------------
# Spec loading
# ---------------------------------------------------------------------------

REQUIRED_SPEC_KEYS = ("id", "type", "footprint", "limits", "pivot")


def list_spec_ids() -> list[str]:
    """All asset ids that have a spec file under blender/asset_specs/."""
    return sorted(p.stem for p in SPECS_DIR.glob("**/*.json"))


def find_spec_path(asset_id: str) -> Path:
    matches = sorted(SPECS_DIR.glob(f"**/{asset_id}.json"))
    if not matches:
        raise FileNotFoundError(
            f"No spec found for asset '{asset_id}' under {SPECS_DIR} "
            f"(known specs: {list_spec_ids()})"
        )
    if len(matches) > 1:
        raise RuntimeError(f"Ambiguous spec for '{asset_id}': {matches}")
    return matches[0]


def load_spec(asset_id: str) -> dict:
    path = find_spec_path(asset_id)
    with open(path, "r", encoding="utf-8") as fh:
        spec = json.load(fh)
    for key in REQUIRED_SPEC_KEYS:
        if key not in spec:
            raise ValueError(f"Spec {path} is missing required key '{key}'")
    if spec["id"] != asset_id:
        raise ValueError(f"Spec id '{spec['id']}' does not match filename '{asset_id}'")
    return spec


# ---------------------------------------------------------------------------
# Scene management
# ---------------------------------------------------------------------------


def reset_scene() -> None:
    """Wipe everything and start from an empty scene (metric, 1 unit = 1 m)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    # Purge any datablocks the factory file might carry.
    for _ in range(3):
        bpy.ops.outliner.orphans_purge(do_recursive=True)


# ---------------------------------------------------------------------------
# Builder registry
# ---------------------------------------------------------------------------

BuilderFn = Callable[[dict], list[bpy.types.Object]]
_BUILDERS: dict[str, BuilderFn] = {}


def register_builder(asset_id: str) -> Callable[[BuilderFn], BuilderFn]:
    """Decorator: register *fn* as the builder for *asset_id*.

    A builder receives the parsed spec dict and returns the list of created
    objects (meshes; anchors/root/collision are handled by the framework, but a
    builder may create them itself if it needs special shapes).
    """

    def wrap(fn: BuilderFn) -> BuilderFn:
        if asset_id in _BUILDERS:
            raise RuntimeError(f"Duplicate builder registration for '{asset_id}'")
        _BUILDERS[asset_id] = fn
        return fn

    return wrap


def get_builder(asset_id: str) -> BuilderFn:
    if asset_id not in _BUILDERS:
        raise KeyError(
            f"No builder registered for '{asset_id}'. "
            f"Registered: {sorted(_BUILDERS)}. "
            "Builders live in blender/scripts/builders/ and self-register via "
            "@register_builder."
        )
    return _BUILDERS[asset_id]


def registered_builder_ids() -> list[str]:
    return sorted(_BUILDERS)


# ---------------------------------------------------------------------------
# Object helpers
# ---------------------------------------------------------------------------


def link_object(obj: bpy.types.Object) -> bpy.types.Object:
    """Link *obj* into the active scene collection if not already linked."""
    coll = bpy.context.scene.collection
    if obj.name not in coll.objects:
        coll.objects.link(obj)
    return obj


def make_root(asset_id: str, children: Iterable[bpy.types.Object]) -> bpy.types.Object:
    """Create the single root empty named *asset_id* and parent *children* to it."""
    root = bpy.data.objects.new(asset_id, None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.25
    link_object(root)
    for child in children:
        if child.parent is None and child is not root:
            child.parent = root
    return root


def add_anchor(
    name: str,
    location: tuple[float, float, float],
    parent: bpy.types.Object | None = None,
    anchor_type: str = "generic",
) -> bpy.types.Object:
    """Add a named anchor empty (exported into the GLB as a node)."""
    empty = bpy.data.objects.new(name, None)
    empty.empty_display_type = "PLAIN_AXES"
    empty.empty_display_size = 0.1
    empty.location = Vector(location)
    empty["abbey_anchor"] = anchor_type
    link_object(empty)
    if parent is not None:
        empty.parent = parent
    return empty


def is_collision_object(obj: bpy.types.Object) -> bool:
    return obj.name.endswith(COLLISION_SUFFIX)


def iter_asset_objects(root: bpy.types.Object) -> list[bpy.types.Object]:
    """Root plus all its descendants."""
    out = [root]
    stack = list(root.children)
    while stack:
        obj = stack.pop()
        out.append(obj)
        stack.extend(obj.children)
    return out


def mesh_objects(root: bpy.types.Object, include_collision: bool = False):
    return [
        o
        for o in iter_asset_objects(root)
        if o.type == "MESH" and (include_collision or not is_collision_object(o))
    ]


def anchor_objects(root: bpy.types.Object) -> list[bpy.types.Object]:
    return [
        o for o in iter_asset_objects(root) if o.type == "EMPTY" and o is not root
    ]


# ---------------------------------------------------------------------------
# Measurement helpers
# ---------------------------------------------------------------------------


def world_bounds(
    objects: Iterable[bpy.types.Object],
) -> tuple[Vector, Vector]:
    """Combined world-space AABB (min, max) of the given mesh objects."""
    lo = Vector((math.inf, math.inf, math.inf))
    hi = Vector((-math.inf, -math.inf, -math.inf))
    found = False
    for obj in objects:
        if obj.type != "MESH":
            continue
        found = True
        for corner in obj.bound_box:
            world = obj.matrix_world @ Vector(corner)
            lo.x, lo.y, lo.z = min(lo.x, world.x), min(lo.y, world.y), min(lo.z, world.z)
            hi.x, hi.y, hi.z = max(hi.x, world.x), max(hi.y, world.y), max(hi.z, world.z)
    if not found:
        raise ValueError("world_bounds: no mesh objects given")
    return lo, hi


def measure_dimensions(root: bpy.types.Object) -> dict:
    """Width/depth/height plus raw bounds of the visual meshes (no collision)."""
    lo, hi = world_bounds(mesh_objects(root))
    return {
        "width": round(hi.x - lo.x, 5),
        "depth": round(hi.y - lo.y, 5),
        "height": round(hi.z - lo.z, 5),
        "min": [round(v, 5) for v in lo],
        "max": [round(v, 5) for v in hi],
    }


def count_triangles(root: bpy.types.Object) -> int:
    """Triangle count of visual meshes (collision proxy excluded), after modifiers."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    total = 0
    for obj in mesh_objects(root):
        eval_obj = obj.evaluated_get(depsgraph)
        mesh = eval_obj.to_mesh()
        mesh.calc_loop_triangles()
        total += len(mesh.loop_triangles)
        eval_obj.to_mesh_clear()
    return total


def used_materials(root: bpy.types.Object) -> list[str]:
    """Sorted unique material names used by visual meshes."""
    names: set[str] = set()
    for obj in mesh_objects(root):
        for slot in obj.material_slots:
            if slot.material is not None:
                names.add(slot.material.name)
    return sorted(names)


# ---------------------------------------------------------------------------
# Pivot + collision proxy
# ---------------------------------------------------------------------------


def apply_center_bottom_pivot(root: bpy.types.Object) -> None:
    """Shift the asset so the visual-mesh AABB is XY-centered on the root origin
    and its lowest point sits exactly at Z = 0 (root stays at world origin)."""
    lo, hi = world_bounds(mesh_objects(root))
    offset = Vector(((lo.x + hi.x) / 2.0, (lo.y + hi.y) / 2.0, lo.z))
    if offset.length < 1e-9:
        return
    root.location = Vector((0.0, 0.0, 0.0))
    for child in root.children:
        child.location = Vector(child.location) - offset
    bpy.context.view_layer.update()


def add_collision_proxy(root: bpy.types.Object, asset_id: str) -> bpy.types.Object:
    """Add a simple AABB box mesh named '<id>_collision' matching visual bounds."""
    lo, hi = world_bounds(mesh_objects(root))
    center = (lo + hi) / 2.0
    size = hi - lo
    mesh = bpy.data.meshes.new(f"{asset_id}{COLLISION_SUFFIX}")
    hx, hy, hz = size.x / 2.0, size.y / 2.0, size.z / 2.0
    verts = [
        (-hx, -hy, -hz), (hx, -hy, -hz), (hx, hy, -hz), (-hx, hy, -hz),
        (-hx, -hy, hz), (hx, -hy, hz), (hx, hy, hz), (-hx, hy, hz),
    ]
    faces = [
        (0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1),
        (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0),
    ]
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(f"{asset_id}{COLLISION_SUFFIX}", mesh)
    obj.location = center
    obj.display_type = "WIRE"
    obj.hide_render = True
    link_object(obj)
    obj.parent = root
    return obj
