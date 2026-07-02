"""Closed shared material library for The Abbey at World's End.

This is the ONLY place materials are defined (see ART_BIBLE.md). Assets reference
materials strictly by name via :func:`get_material`. Requesting any name outside
:data:`MATERIAL_LIBRARY` raises ``UnknownMaterialError`` — there are no bespoke
one-off materials, ever.

Style: flat painterly Principled-BSDF colors. No textures, no procedural noise.
Emissive materials (flame, ember, warm window, sacred gold) carry a small emission
component so night previews read correctly.
"""

from __future__ import annotations

import bpy

__all__ = [
    "MATERIAL_LIBRARY",
    "MATERIAL_NAMES",
    "UnknownMaterialError",
    "get_material",
    "is_library_material",
]


class UnknownMaterialError(ValueError):
    """Raised when a material name outside the closed library is requested."""


def _srgb(r: int, g: int, b: int) -> tuple[float, float, float, float]:
    """Convert 0-255 sRGB values to linear RGBA as expected by Principled BSDF."""

    def chan(c: int) -> float:
        c_f = c / 255.0
        if c_f <= 0.04045:
            return c_f / 12.92
        return ((c_f + 0.055) / 1.055) ** 2.4

    return (chan(r), chan(g), chan(b), 1.0)


# name -> {base_color, roughness, metallic, emission_color?, emission_strength?}
# The 17 names below are the FULL closed set from ART_BIBLE.md. Do not add names
# here without updating ART_BIBLE.md first.
MATERIAL_LIBRARY: dict[str, dict] = {
    "mat_warm_wood": {
        "base_color": _srgb(166, 111, 62),
        "roughness": 0.85,
        "metallic": 0.0,
    },
    "mat_dark_wood": {
        "base_color": _srgb(84, 55, 34),
        "roughness": 0.9,
        "metallic": 0.0,
    },
    "mat_old_stone": {
        "base_color": _srgb(148, 143, 130),
        "roughness": 0.95,
        "metallic": 0.0,
    },
    "mat_wet_stone": {
        "base_color": _srgb(96, 102, 105),
        "roughness": 0.35,
        "metallic": 0.0,
    },
    "mat_thatch": {
        "base_color": _srgb(189, 156, 78),
        "roughness": 0.95,
        "metallic": 0.0,
    },
    "mat_canvas": {
        "base_color": _srgb(214, 197, 160),
        "roughness": 0.9,
        "metallic": 0.0,
    },
    "mat_iron": {
        "base_color": _srgb(72, 74, 80),
        "roughness": 0.55,
        "metallic": 0.85,
    },
    "mat_warm_window": {
        "base_color": _srgb(255, 190, 92),
        "roughness": 0.4,
        "metallic": 0.0,
        "emission_color": _srgb(255, 176, 64),
        "emission_strength": 3.0,
    },
    "mat_sacred_gold": {
        "base_color": _srgb(226, 178, 74),
        "roughness": 0.35,
        "metallic": 0.9,
        "emission_color": _srgb(226, 178, 74),
        "emission_strength": 0.15,
    },
    "mat_bone": {
        "base_color": _srgb(226, 218, 196),
        "roughness": 0.7,
        "metallic": 0.0,
    },
    "mat_snow": {
        "base_color": _srgb(235, 240, 245),
        "roughness": 0.8,
        "metallic": 0.0,
    },
    "mat_ash": {
        "base_color": _srgb(112, 106, 100),
        "roughness": 1.0,
        "metallic": 0.0,
    },
    "mat_nightmare_black": {
        "base_color": _srgb(12, 10, 16),
        "roughness": 0.9,
        "metallic": 0.0,
    },
    "mat_flame": {
        "base_color": _srgb(255, 147, 41),
        "roughness": 0.5,
        "metallic": 0.0,
        "emission_color": _srgb(255, 128, 32),
        "emission_strength": 8.0,
    },
    "mat_ember": {
        "base_color": _srgb(216, 74, 32),
        "roughness": 0.8,
        "metallic": 0.0,
        "emission_color": _srgb(255, 64, 16),
        "emission_strength": 4.0,
    },
    "mat_foliage": {
        "base_color": _srgb(96, 138, 62),
        "roughness": 0.9,
        "metallic": 0.0,
    },
    "mat_dirt": {
        "base_color": _srgb(121, 92, 62),
        "roughness": 1.0,
        "metallic": 0.0,
    },
}

MATERIAL_NAMES: frozenset[str] = frozenset(MATERIAL_LIBRARY.keys())

assert len(MATERIAL_LIBRARY) == 17, "ART_BIBLE.md defines exactly 17 materials"


def is_library_material(name: str) -> bool:
    """True if *name* is part of the closed shared material library."""
    return name in MATERIAL_LIBRARY


def get_material(name: str) -> bpy.types.Material:
    """Create-or-return the shared material *name*.

    Raises :class:`UnknownMaterialError` for any name outside the closed library.
    """
    if name not in MATERIAL_LIBRARY:
        raise UnknownMaterialError(
            f"Material '{name}' is not in the closed library. "
            f"Allowed: {sorted(MATERIAL_LIBRARY)}. "
            "Update ART_BIBLE.md before adding materials."
        )

    existing = bpy.data.materials.get(name)
    if existing is not None:
        return existing

    props = MATERIAL_LIBRARY[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True

    bsdf = None
    for node in mat.node_tree.nodes:
        if node.type == "BSDF_PRINCIPLED":
            bsdf = node
            break
    if bsdf is None:  # pragma: no cover - factory node tree always has one
        bsdf = mat.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
        output = mat.node_tree.nodes.get("Material Output")
        mat.node_tree.links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])

    bsdf.inputs["Base Color"].default_value = props["base_color"]
    bsdf.inputs["Roughness"].default_value = props["roughness"]
    bsdf.inputs["Metallic"].default_value = props["metallic"]
    if "emission_color" in props:
        bsdf.inputs["Emission Color"].default_value = props["emission_color"]
        bsdf.inputs["Emission Strength"].default_value = props["emission_strength"]

    # Flat viewport color as well, so solid-shaded screenshots still read.
    mat.diffuse_color = props["base_color"]
    return mat
