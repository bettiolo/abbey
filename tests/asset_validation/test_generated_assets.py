"""Validate generated Blender assets against their specs and the ART_BIBLE gates.

Parametrized over every asset spec in blender/asset_specs/**/*.json. Specs whose
asset has not been generated yet (no metadata) are skipped, not failed — the
pipeline (tools/run_blender_asset_pipeline.py) is the thing that generates them.
"""

from __future__ import annotations

import json
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
SPEC_DIR = REPO_ROOT / "blender" / "asset_specs"
GENERATED = REPO_ROOT / "blender" / "generated"
GLB_DIR = GENERATED / "glb"
PREVIEW_DIR = GENERATED / "previews"
METADATA_DIR = GENERATED / "metadata"

PREVIEW_VARIANTS = ("day", "night", "winter", "grayscale")

# Closed shared material library — the single allowed set. Source of truth:
# ART_BIBLE.md "Shared material library" (implemented in
# blender/scripts/abbey_materials.py). 17 materials; do not extend this list
# without updating ART_BIBLE.md first.
MATERIAL_LIBRARY = frozenset({
    "mat_warm_wood",
    "mat_dark_wood",
    "mat_old_stone",
    "mat_wet_stone",
    "mat_thatch",
    "mat_canvas",
    "mat_iron",
    "mat_warm_window",
    "mat_sacred_gold",
    "mat_bone",
    "mat_snow",
    "mat_ash",
    "mat_nightmare_black",
    "mat_flame",
    "mat_ember",
    "mat_foliage",
    "mat_dirt",
})

SPEC_PATHS = sorted(SPEC_DIR.rglob("*.json")) if SPEC_DIR.is_dir() else []


def spec_id(path: Path) -> str:
    return path.stem


if not SPEC_PATHS:

    def test_no_asset_specs_yet() -> None:
        pytest.skip(f"no asset specs under {SPEC_DIR}; nothing to validate yet")


@pytest.fixture(params=SPEC_PATHS, ids=spec_id, scope="module")
def asset(request: pytest.FixtureRequest) -> dict:
    """Load one spec + its generated metadata; skip if not generated yet."""
    spec_path: Path = request.param
    asset_id = spec_path.stem
    try:
        spec = json.loads(spec_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        pytest.fail(f"spec {spec_path} is not valid JSON: {exc}")

    meta_path = METADATA_DIR / f"{asset_id}.meta.json"
    if not meta_path.is_file():
        pytest.skip(f"{asset_id}: no generated metadata at {meta_path}; "
                    "run tools/run_blender_asset_pipeline.py first")
    try:
        meta = json.loads(meta_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        pytest.fail(f"metadata {meta_path} is not valid JSON: {exc}")

    return {"id": asset_id, "spec": spec, "meta": meta}


def test_glb_exists(asset: dict) -> None:
    glb = GLB_DIR / f"{asset['id']}.glb"
    assert glb.is_file(), f"missing GLB export: {glb}"
    assert glb.stat().st_size > 0, f"GLB export is empty: {glb}"


def test_all_four_previews_exist(asset: dict) -> None:
    missing = [
        variant
        for variant in PREVIEW_VARIANTS
        if not (PREVIEW_DIR / f"{asset['id']}_preview_{variant}.png").is_file()
    ]
    assert not missing, f"{asset['id']}: missing preview renders: {missing}"


def test_metadata_validation_passed(asset: dict) -> None:
    validation = asset["meta"].get("validation")
    assert isinstance(validation, dict), f"{asset['id']}: metadata has no validation block"
    assert validation.get("passed") is True, (
        f"{asset['id']}: pipeline validation did not pass; "
        f"checks: {validation.get('checks')}"
    )


def test_triangle_count_within_spec_limit(asset: dict) -> None:
    limits = asset["spec"].get("limits") or {}
    max_triangles = limits.get("max_triangles")
    if max_triangles is None:
        pytest.skip(f"{asset['id']}: spec declares no max_triangles limit")
    tris = asset["meta"].get("triangle_count")
    assert isinstance(tris, int), f"{asset['id']}: metadata missing triangle_count"
    assert tris <= max_triangles, (
        f"{asset['id']}: {tris} triangles exceeds spec limit {max_triangles}"
    )


def test_material_count_within_spec_limit(asset: dict) -> None:
    limits = asset["spec"].get("limits") or {}
    max_materials = limits.get("max_materials")
    if max_materials is None:
        pytest.skip(f"{asset['id']}: spec declares no max_materials limit")
    count = asset["meta"].get("material_count")
    assert isinstance(count, int), f"{asset['id']}: metadata missing material_count"
    assert count <= max_materials, (
        f"{asset['id']}: {count} materials exceeds spec limit {max_materials}"
    )


def test_materials_within_closed_library(asset: dict) -> None:
    materials = asset["meta"].get("materials")
    assert isinstance(materials, list), f"{asset['id']}: metadata missing materials list"
    rogue = sorted(set(materials) - MATERIAL_LIBRARY)
    assert not rogue, (
        f"{asset['id']}: materials outside the closed 17-material library "
        f"(ART_BIBLE.md): {rogue}"
    )


def _anchor_names(anchors: object) -> set[str]:
    names: set[str] = set()
    for anchor in anchors or []:
        if isinstance(anchor, dict) and anchor.get("name"):
            names.add(anchor["name"])
        elif isinstance(anchor, str):
            names.add(anchor)
    return names


def test_metadata_anchors_cover_spec_anchors(asset: dict) -> None:
    spec_anchors = _anchor_names(asset["spec"].get("anchors"))
    if not spec_anchors:
        pytest.skip(f"{asset['id']}: spec declares no anchors")
    meta_anchors = _anchor_names(asset["meta"].get("anchors"))
    missing = sorted(spec_anchors - meta_anchors)
    assert not missing, (
        f"{asset['id']}: anchors declared in spec but absent from generated "
        f"metadata: {missing}"
    )
