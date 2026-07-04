"""Phase 2 aesthetic-gate proof: the four canonical First-White-Night beats.

Human review gate #4 (the vertical slice, AGENTS.md) needs to SEE the slice now,
before the Unity in-engine canonical capture (which only runs in CI). This is the
local Blender stand-in: it assembles the SAME committed GLBs as
``render_scene_proof.py`` (the same bytes Unity imports) with the same fixed
orthographic pitch-30/yaw-45 camera (ART_BIBLE camera contract), and renders the
four story beats of VERTICAL_SLICE_SPEC.md §3 under four lighting rigs:

  1. previews/phase2_day_camp.png    — bucolic daylight over the meadow camp.
  2. previews/phase2_dusk_recall.png — colour drains, warm low sun, the fire
     pools just beginning to read: the bell rings, villagers hurry home.
  3. previews/phase2_night_attack.png — near-dark blue ambient, ONLY firelight
     pools; pale hounds test the edge of the light and the Black Hound is
     readable at the ruined tower: darkness as hostile territory.
  4. previews/phase2_morning_after.png — soft first light over the settlement
     that endured the night.

This reuses ``render_scene_proof`` as a library (its ground/placement/camera/
renderer helpers) so the two proofs can never drift; only the lighting rigs and
the night's extra pale-hound silhouettes are new here. It is a standalone
render_scene_proof-style script — NOT part of the asset batch
(blender/scripts/build_asset_batch.py) and it touches no asset GLB.

Usage (either environment, like the other pipeline scripts)::

    uv run --with-requirements tools/requirements-dev.txt --with bpy python blender/scripts/render_phase2_proof.py
    uv run --with-requirements tools/requirements-dev.txt --with bpy python blender/scripts/render_phase2_proof.py --samples 16 --scale 0.5
    blender -b -P blender/scripts/render_phase2_proof.py -- --samples 16

Outputs::

    blender/generated/previews/phase2_day_camp.png
    blender/generated/previews/phase2_dusk_recall.png
    blender/generated/previews/phase2_night_attack.png
    blender/generated/previews/phase2_morning_after.png
    blender/generated/metadata/phase2_proof.meta.json
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

import asset_framework as fw
import render_scene_proof as rsp

# ---------------------------------------------------------------------------
# Dusk + dawn lighting rigs (day/night rigs are reused from render_scene_proof)
# ---------------------------------------------------------------------------

DUSK_WORLD = {"color": (0.20, 0.13, 0.17), "strength": 0.30}
DUSK_SUN = {
    "color": (1.0, 0.52, 0.30),
    "energy": 1.4,
    "rotation_deg": (8.0, -8.0, 65.0),  # low, raking, warm
}
# At dusk the fire pools are just starting to matter: the same warm points as
# night, dimmer, so the camp reads as "getting dangerous" not yet "dark".
DUSK_POINT_SCALE = 0.5

DAWN_WORLD = {"color": (0.34, 0.30, 0.34), "strength": 0.55}
DAWN_SUN = {
    "color": (1.0, 0.80, 0.66),
    "energy": 2.2,
    "rotation_deg": (9.0, 6.0, 250.0),  # low first light from the opposite side
}

# Pale hounds testing the edge of the campfire pool (camp fire at ~0.5,0.5).
# Placed just inside the Edge band so the warm firelight rims their pale bodies
# (ART_BIBLE: "shapes visible at the edge of light") — close enough to read as a
# threat pressing the light, not lost in the dark beyond it.
NIGHT_MONSTERS: list[tuple[str, float, float, float, float, float]] = [
    ("pale_hound_lowpoly", 2.3, -0.4, 0.0, -110.0, 1.0),   # SE edge of the campfire pool
    ("pale_hound_lowpoly", -1.7, 2.4, 0.0, 35.0, 1.0),     # NW edge of the campfire pool
    ("pale_hound_lowpoly", 3.9, -1.9, 0.0, 205.0, 0.95),   # prowling by the SE lantern
]


def apply_dusk_lighting(anchors: dict[str, list[Vector]]) -> list[dict]:
    rsp._clear_lights()
    rsp._dim_sacred_gold_for_night()
    rsp._set_world(DUSK_WORLD["color"], DUSK_WORLD["strength"])
    rsp._add_sun("dusk_sun", DUSK_SUN["color"], DUSK_SUN["energy"],
                 DUSK_SUN["rotation_deg"])
    placed = []
    for spec in rsp.NIGHT_POINTS:
        rsp._add_point(f"dusk_{spec['name']}", spec["pos"], spec["color"],
                       spec["energy"] * DUSK_POINT_SCALE, spec["radius"])
        placed.append(dict(spec))
    for base, cfg in rsp.NIGHT_ANCHOR_LIGHTS.items():
        for i, pos in enumerate(anchors.get(base, [])):
            p = pos + Vector(cfg["offset"])
            rsp._add_point(f"dusk_{base}_{i}", tuple(p), cfg["color"],
                           cfg["energy"] * DUSK_POINT_SCALE, cfg["radius"])
    return placed


def apply_dawn_lighting() -> None:
    rsp._clear_lights()
    rsp._set_world(DAWN_WORLD["color"], DAWN_WORLD["strength"])
    rsp._add_sun("dawn_sun", DAWN_SUN["color"], DAWN_SUN["energy"],
                 DAWN_SUN["rotation_deg"])


def add_night_monsters() -> list[bpy.types.Object]:
    """Import + place the pale hounds for the night beat; return their roots."""
    roots = []
    for asset_id, x, y, z, rot_deg, scale in NIGHT_MONSTERS:
        root = rsp.import_glb_asset(asset_id)
        root.location = Vector((x, y, z))
        root.rotation_mode = "XYZ"
        root.rotation_euler = Euler((0.0, 0.0, math.radians(rot_deg)), "XYZ")
        root.scale = (scale, scale, scale)
        roots.append(root)
    bpy.context.view_layer.update()
    return roots


def remove_objects(objs: list[bpy.types.Object]) -> None:
    for obj in list(objs):
        for child in list(obj.children_recursive):
            bpy.data.objects.remove(child, do_unlink=True)
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    bpy.context.view_layer.update()


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------


def _cli_args(argv: list[str]) -> argparse.Namespace:
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
    else:
        argv = argv[1:]
    parser = argparse.ArgumentParser(description="Render the four Phase 2 story-beat proofs")
    parser.add_argument("--samples", type=int, default=48)
    parser.add_argument("--scale", type=float, default=1.0,
                        help="resolution multiplier for fast iteration (e.g. 0.5)")
    parser.add_argument("--only",
                        choices=["day_camp", "dusk_recall", "night_attack", "morning_after"],
                        default=None, help="render just one beat (iteration helper)")
    return parser.parse_args(argv)


def _render(scene: bpy.types.Scene, name: str) -> Path:
    out = fw.PREVIEW_DIR / f"phase2_{name}.png"
    scene.render.filepath = str(out)
    bpy.ops.render.render(write_still=True)
    print(f"rendered {out}")
    return out


def main(argv: list[str]) -> int:
    args = _cli_args(argv)

    fw.reset_scene()
    scene = bpy.context.scene
    rsp.build_ground()
    placements = rsp.place_assets()
    anchors = rsp.anchor_world_positions()
    cam = rsp.setup_camera(scene)  # fit to the base vignette (monsters added later)
    rsp.configure_renderer(scene, args.samples, int(args.scale * 100))

    fw.PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    outputs: dict[str, Path] = {}

    def want(beat: str) -> bool:
        return args.only in (None, beat)

    if want("day_camp"):
        rsp.apply_day_lighting()
        outputs["day_camp"] = _render(scene, "day_camp")

    if want("dusk_recall"):
        apply_dusk_lighting(anchors)
        outputs["dusk_recall"] = _render(scene, "dusk_recall")

    if want("night_attack"):
        monsters = add_night_monsters()
        rsp.apply_night_lighting(anchors)
        outputs["night_attack"] = _render(scene, "night_attack")
        remove_objects(monsters)

    if want("morning_after"):
        apply_dawn_lighting()
        outputs["morning_after"] = _render(scene, "morning_after")

    metadata = {
        "id": "phase2_proof",
        "generated_at": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "description": (
            "The four canonical First-White-Night beats (VERTICAL_SLICE_SPEC.md "
            "section 3) assembled from the committed GLBs; the Phase 2 vertical-slice "
            "aesthetic-gate proof renders. Standalone stand-in for the Unity in-engine "
            "canonical capture (ScreenshotCapture.CaptureCanonicalShots)."
        ),
        "beats": {
            "phase2_day_camp.png": "Bucolic daylight over the meadow camp.",
            "phase2_dusk_recall.png": "Colour drains, warm low sun, fire pools rising.",
            "phase2_night_attack.png": (
                "Near-dark; only firelight pools; pale hounds at the edge of the "
                "light, the Black Hound readable at the ruined tower."
            ),
            "phase2_morning_after.png": "Soft first light over the settlement that endured.",
        },
        "reuses": "render_scene_proof.py (ground/placements/camera/renderer/day+night rigs)",
        "camera": {
            "type": "ORTHO",
            "pitch_deg": rsp.CAMERA_PITCH_DEG,
            "yaw_deg": rsp.CAMERA_YAW_DEG,
            "ortho_scale": round(cam.data.ortho_scale, 4),
            "identical_for_all_beats": True,
        },
        "render": {
            "engine": "CYCLES (CPU)",
            "samples": args.samples,
            "resolution": [rsp.RESOLUTION_X, rsp.RESOLUTION_Y],
            "resolution_percentage": int(args.scale * 100),
        },
        "night_monsters": [m[0] for m in NIGHT_MONSTERS],
        "placement_count": len(placements),
        "files": {k: rsp._relpath(v) for k, v in outputs.items()},
    }
    fw.METADATA_DIR.mkdir(parents=True, exist_ok=True)
    meta_path = fw.METADATA_DIR / "phase2_proof.meta.json"
    with open(meta_path, "w", encoding="utf-8") as fh:
        json.dump(metadata, fh, indent=2)
        fh.write("\n")
    print(f"wrote {meta_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
