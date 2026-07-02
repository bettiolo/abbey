"""Tiny shared low-poly shape helpers for builders.

Chunky, readable, strong silhouette (ART_BIBLE shape language): everything is
built from a handful of fat primitives, painterly flat materials, no textures.
"""

from __future__ import annotations

import bpy

from abbey_materials import get_material


def _finish(name: str, material: str) -> bpy.types.Object:
    obj = bpy.context.active_object
    obj.name = name
    obj.data.name = name
    obj.data.materials.append(get_material(material))
    return obj


def add_box(
    name: str,
    material: str,
    size: tuple[float, float, float],
    location: tuple[float, float, float] = (0, 0, 0),
    rotation: tuple[float, float, float] = (0, 0, 0),
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = _finish(name, material)
    obj.scale = size
    return obj


def add_cylinder(
    name: str,
    material: str,
    radius: float,
    depth: float,
    vertices: int = 7,
    location: tuple[float, float, float] = (0, 0, 0),
    rotation: tuple[float, float, float] = (0, 0, 0),
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
        rotation=rotation,
    )
    return _finish(name, material)


def add_cone(
    name: str,
    material: str,
    radius: float,
    depth: float,
    vertices: int = 8,
    location: tuple[float, float, float] = (0, 0, 0),
    rotation: tuple[float, float, float] = (0, 0, 0),
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices,
        radius1=radius,
        radius2=0.0,
        depth=depth,
        location=location,
        rotation=rotation,
    )
    return _finish(name, material)
