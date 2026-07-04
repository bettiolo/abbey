"""Render the 4 isometric previews for an asset: day / night / winter / grayscale.

Camera contract (ART_BIBLE.md): orthographic, pitch 30°, yaw 45° — identical to
the in-game camera so previews predict how assets read in Unity.

Renderer: CYCLES on CPU with very low samples (headless containers usually have
no GPU, which EEVEE requires — we detect that and fall back). Previews are
512x512, 16 samples + denoise: fast (~5 s each) and good enough for validation.

The grayscale preview is the day render converted to Rec.709 luminance in a
pixel post-process — it is the hard "readable in grayscale" check from the art
bible.
"""

from __future__ import annotations

import filecmp
import math
import os
import sys
import tempfile
from pathlib import Path

_SCRIPTS_DIR = str(Path(__file__).resolve().parent)
if _SCRIPTS_DIR not in sys.path:
    sys.path.insert(0, _SCRIPTS_DIR)

import bpy
from mathutils import Euler, Vector

import asset_framework as fw

PREVIEW_VARIANTS = ("day", "night", "winter", "grayscale")
RESOLUTION = 512
SAMPLES = 16
RENDER_SEED = 1729
VISUAL_DIFF_MEAN_THRESHOLD = 0.0025
VISUAL_DIFF_P99_THRESHOLD = 0.025

CAMERA_PITCH_DEG = 30.0  # below horizontal
CAMERA_YAW_DEG = 45.0


def pick_render_engine() -> str:
    """EEVEE needs a GPU; headless CPU containers fall back to CYCLES."""
    try:
        import gpu

        gpu.platform.backend_type_get()  # raises in background/no-GPU mode
        return "BLENDER_EEVEE"
    except Exception:
        return "CYCLES"


def _configure_renderer(scene: bpy.types.Scene) -> None:
    engine = pick_render_engine()
    scene.frame_set(1)
    scene.render.engine = engine
    scene.render.resolution_x = RESOLUTION
    scene.render.resolution_y = RESOLUTION
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    if engine == "CYCLES":
        scene.cycles.device = "CPU"
        scene.cycles.samples = SAMPLES
        scene.cycles.seed = RENDER_SEED
        if hasattr(scene.cycles, "use_animated_seed"):
            scene.cycles.use_animated_seed = False
        scene.cycles.use_denoising = True
        scene.cycles.max_bounces = 4


def _temp_png_path(final_path: Path) -> Path:
    fd, temp_path = tempfile.mkstemp(
        prefix=f".{final_path.stem}.", suffix=".tmp.png", dir=final_path.parent
    )
    os.close(fd)
    return Path(temp_path)


def _replace_if_changed(temp_path: Path, final_path: Path) -> None:
    if final_path.exists() and filecmp.cmp(temp_path, final_path, shallow=False):
        temp_path.unlink()
        return
    if final_path.exists() and _pngs_visually_equivalent(temp_path, final_path):
        temp_path.unlink()
        return
    temp_path.replace(final_path)


def _pngs_visually_equivalent(path_a: Path, path_b: Path) -> bool:
    import numpy as np

    img_a = bpy.data.images.load(str(path_a))
    img_b = bpy.data.images.load(str(path_b))
    try:
        if tuple(img_a.size) != tuple(img_b.size):
            return False
        px_a = np.empty(img_a.size[0] * img_a.size[1] * 4, dtype=np.float32)
        px_b = np.empty(img_b.size[0] * img_b.size[1] * 4, dtype=np.float32)
        img_a.pixels.foreach_get(px_a)
        img_b.pixels.foreach_get(px_b)
        diff = np.abs(px_a.reshape(-1, 4)[:, :3] - px_b.reshape(-1, 4)[:, :3])
        return (
            float(diff.mean()) <= VISUAL_DIFF_MEAN_THRESHOLD
            and float(np.quantile(diff, 0.99)) <= VISUAL_DIFF_P99_THRESHOLD
        )
    finally:
        bpy.data.images.remove(img_a)
        bpy.data.images.remove(img_b)


def _setup_iso_camera(scene: bpy.types.Scene, root: bpy.types.Object) -> None:
    dims = fw.measure_dimensions(root)
    lo = Vector(dims["min"])
    hi = Vector(dims["max"])
    center = (lo + hi) / 2.0

    cam_data = bpy.data.cameras.new("preview_camera")
    cam_data.type = "ORTHO"
    max_dim = max(dims["width"], dims["depth"], dims["height"])
    cam_data.ortho_scale = max_dim * 1.7
    cam_data.clip_start = 0.01
    cam_data.clip_end = 100.0

    cam = bpy.data.objects.new("preview_camera", cam_data)
    rot = Euler(
        (
            math.radians(90.0 - CAMERA_PITCH_DEG),  # tilt: 30° below horizontal
            0.0,
            math.radians(CAMERA_YAW_DEG),
        ),
        "XYZ",
    )
    cam.rotation_euler = rot
    forward = rot.to_matrix() @ Vector((0.0, 0.0, -1.0))
    cam.location = center - forward * max(10.0, max_dim * 6.0)
    fw.link_object(cam)
    scene.camera = cam


def _clear_rig() -> None:
    """Remove previous preview lights/camera/world tint (keeps the asset)."""
    for obj in list(bpy.context.scene.objects):
        if obj.name.startswith("preview_"):
            bpy.data.objects.remove(obj, do_unlink=True)


def _set_world(color: tuple[float, float, float], strength: float) -> None:
    world = bpy.context.scene.world
    if world is None:
        world = bpy.data.worlds.new("preview_world")
        bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg is None:
        bg = world.node_tree.nodes.new("ShaderNodeBackground")
        out = world.node_tree.nodes.new("ShaderNodeOutputWorld")
        world.node_tree.links.new(bg.outputs["Background"], out.inputs["Surface"])
    bg.inputs["Color"].default_value = (*color, 1.0)
    bg.inputs["Strength"].default_value = strength


def _add_sun(color: tuple[float, float, float], energy: float) -> bpy.types.Object:
    data = bpy.data.lights.new("preview_sun", "SUN")
    data.color = color
    data.energy = energy
    data.angle = math.radians(5.0)
    sun = bpy.data.objects.new("preview_sun", data)
    # Fixed sun roughly camera-relative (art bible: silhouettes must always read).
    sun.rotation_euler = Euler(
        (math.radians(50.0), math.radians(-8.0), math.radians(65.0)), "XYZ"
    )
    fw.link_object(sun)
    return sun


def _add_point(
    location: Vector, color: tuple[float, float, float], energy: float
) -> bpy.types.Object:
    data = bpy.data.lights.new("preview_point", "POINT")
    data.color = color
    data.energy = energy
    data.shadow_soft_size = 0.2
    light = bpy.data.objects.new("preview_point", data)
    light.location = location
    fw.link_object(light)
    return light


def _apply_lighting(variant: str, root: bpy.types.Object) -> None:
    dims = fw.measure_dimensions(root)
    height = max(dims["height"], 0.3)
    if variant == "day":
        # Warm mid-morning sun over a green coast.
        _set_world((0.55, 0.70, 0.85), 0.55)
        _add_sun((1.0, 0.94, 0.82), 4.0)
    elif variant == "night":
        # Dark blue ambient; warm firelight point close to the asset.
        _set_world((0.010, 0.020, 0.055), 0.35)
        _add_point(
            Vector((0.45, -0.45, height * 0.85 + 0.25)), (1.0, 0.55, 0.22), 60.0
        )
    elif variant == "winter":
        # Cool, pale, desaturated overcast light.
        _set_world((0.62, 0.66, 0.72), 0.7)
        _add_sun((0.80, 0.86, 1.0), 2.2)
    else:
        raise ValueError(f"No lighting rig for variant '{variant}'")


def _convert_to_grayscale(src: Path, dst: Path) -> None:
    """Pixel post-process: Rec.709 luminance of the day render."""
    import numpy as np

    temp = _temp_png_path(dst)
    img = bpy.data.images.load(str(src))
    try:
        n = img.size[0] * img.size[1]
        px = np.empty(n * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        px = px.reshape(-1, 4)
        lum = px[:, 0] * 0.2126 + px[:, 1] * 0.7152 + px[:, 2] * 0.0722
        px[:, 0] = px[:, 1] = px[:, 2] = lum
        img.pixels.foreach_set(px.reshape(-1))
        img.filepath_raw = str(temp)
        img.file_format = "PNG"
        img.save()
    except Exception:
        temp.unlink(missing_ok=True)
        raise
    finally:
        bpy.data.images.remove(img)
    _replace_if_changed(temp, dst)


def render_previews(root: bpy.types.Object, asset_id: str) -> dict:
    """Render all 4 previews; returns {variant: path}."""
    fw.PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    _configure_renderer(scene)

    paths: dict[str, Path] = {}
    for variant in ("day", "night", "winter"):
        _clear_rig()
        _setup_iso_camera(scene, root)
        _apply_lighting(variant, root)
        out = fw.PREVIEW_DIR / f"{asset_id}_preview_{variant}.png"
        temp = _temp_png_path(out)
        scene.render.filepath = str(temp)
        try:
            bpy.ops.render.render(write_still=True)
        except Exception:
            temp.unlink(missing_ok=True)
            raise
        else:
            _replace_if_changed(temp, out)
        paths[variant] = out

    gray = fw.PREVIEW_DIR / f"{asset_id}_preview_grayscale.png"
    _convert_to_grayscale(paths["day"], gray)
    paths["grayscale"] = gray

    _clear_rig()
    return paths
