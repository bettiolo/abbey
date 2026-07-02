"""Validation step: enforce every ART_BIBLE hard gate on a generated asset.

Checks (each becomes an entry in metadata['validation']['checks']):
- glb_exists            GLB file written and non-empty
- blend_saved           .blend build artifact written
- previews_exist        all 4 preview PNGs written
- pivot_center_bottom   mesh AABB min Z ~ 0 and XY-centered on origin
- footprint_fits        dimensions within the spec footprint (small tolerance)
- triangle_budget       visual triangle count <= limits.max_triangles
- material_budget       unique material count <= limits.max_materials
- materials_in_library  every material name is in the closed shared library
- anchors_present       every spec anchor exists as an empty in the hierarchy
- collision_proxy       '<id>_collision' box mesh exists

The collision proxy is excluded from triangle/material budgets by convention
(see asset_framework.py).
"""

from __future__ import annotations

import sys
from pathlib import Path

_SCRIPTS_DIR = str(Path(__file__).resolve().parent)
if _SCRIPTS_DIR not in sys.path:
    sys.path.insert(0, _SCRIPTS_DIR)

import bpy

import asset_framework as fw
from abbey_materials import MATERIAL_NAMES

PIVOT_Z_TOLERANCE = 0.001          # metres
PIVOT_XY_TOLERANCE = 0.01          # metres off-center allowed
FOOTPRINT_TOLERANCE = 1.01         # 1% overhang allowed


def _check(name: str, passed: bool, detail: str) -> dict:
    return {"name": name, "passed": bool(passed), "detail": detail}


def validate_asset(
    root: bpy.types.Object,
    spec: dict,
    files: dict,
    preview_paths: dict,
) -> dict:
    """Run all checks; returns {'passed': bool, 'checks': [...]}."""
    asset_id = spec["id"]
    checks: list[dict] = []

    glb = Path(files["glb"])
    checks.append(
        _check(
            "glb_exists",
            glb.is_file() and glb.stat().st_size > 0,
            f"{glb} ({glb.stat().st_size if glb.is_file() else 0} bytes)",
        )
    )

    blend = Path(files["blend"])
    checks.append(_check("blend_saved", blend.is_file(), str(blend)))

    missing = [
        v
        for v in ("day", "night", "winter", "grayscale")
        if v not in preview_paths or not Path(preview_paths[v]).is_file()
    ]
    checks.append(
        _check(
            "previews_exist",
            not missing,
            "all 4 previews rendered" if not missing else f"missing: {missing}",
        )
    )

    dims = fw.measure_dimensions(root)
    lo = dims["min"]
    hi = dims["max"]
    center_x = (lo[0] + hi[0]) / 2.0
    center_y = (lo[1] + hi[1]) / 2.0
    pivot_ok = (
        abs(lo[2]) <= PIVOT_Z_TOLERANCE
        and abs(center_x) <= PIVOT_XY_TOLERANCE
        and abs(center_y) <= PIVOT_XY_TOLERANCE
    )
    checks.append(
        _check(
            "pivot_center_bottom",
            pivot_ok,
            f"minZ={lo[2]:.4f} centerXY=({center_x:.4f}, {center_y:.4f})",
        )
    )

    fp = spec["footprint"]
    fits = (
        dims["width"] <= fp["width"] * FOOTPRINT_TOLERANCE
        and dims["depth"] <= fp["depth"] * FOOTPRINT_TOLERANCE
        and dims["height"] <= fp["height"] * FOOTPRINT_TOLERANCE
    )
    checks.append(
        _check(
            "footprint_fits",
            fits,
            f"asset {dims['width']:.3f}x{dims['depth']:.3f}x{dims['height']:.3f} "
            f"vs footprint {fp['width']}x{fp['depth']}x{fp['height']}",
        )
    )

    tris = fw.count_triangles(root)
    max_tris = spec["limits"]["max_triangles"]
    checks.append(
        _check("triangle_budget", tris <= max_tris, f"{tris}/{max_tris} triangles")
    )

    mats = fw.used_materials(root)
    max_mats = spec["limits"]["max_materials"]
    checks.append(
        _check(
            "material_budget",
            len(mats) <= max_mats,
            f"{len(mats)}/{max_mats} materials: {mats}",
        )
    )

    rogue = [m for m in mats if m not in MATERIAL_NAMES]
    checks.append(
        _check(
            "materials_in_library",
            not rogue,
            "all in closed library" if not rogue else f"NOT in library: {rogue}",
        )
    )

    anchor_names = {o.name for o in fw.anchor_objects(root)}
    required = [a["name"] for a in spec.get("anchors", [])]
    missing_anchors = [a for a in required if a not in anchor_names]
    checks.append(
        _check(
            "anchors_present",
            not missing_anchors,
            f"required={required} present={sorted(anchor_names)}"
            + (f" MISSING={missing_anchors}" if missing_anchors else ""),
        )
    )

    collision = bpy.data.objects.get(f"{asset_id}{fw.COLLISION_SUFFIX}")
    checks.append(
        _check(
            "collision_proxy",
            collision is not None and collision.type == "MESH",
            f"{asset_id}{fw.COLLISION_SUFFIX} "
            + ("present" if collision is not None else "MISSING"),
        )
    )

    return {"passed": all(c["passed"] for c in checks), "checks": checks}
