"""Procedural tileable pixel-texture generator for the abbey/town/field kit.

Regenerates the shared 64x64 pixel-art texture set described in
docs/ART_REFERENCE_ABBEY.md ("Texture extraction plan") from nothing but numpy —
no external image inputs. Every texture uses EXACTLY the 4-step transcribed
ALBEDO hex ramps from the reference doc and a fixed seed, so output is
byte-for-byte deterministic.

Palette authority (see the decision note under "Measured palette" in
docs/ART_REFERENCE_ABBEY.md): the measured clusters sampled from
docs/abbey-town-1.png are a LIT render — sun, AO and outline pass baked in —
so they are the verification target for the rendered previews, NOT this
generator's input. Using them as albedo would double-apply lighting under the
game's own rig. This generator deliberately consumes the transcribed albedo
ramps instead.

Textures (written to blender/kits/materials/textures/, committed):

    tex_roof_tiles      offset course pattern, per-tile terracotta ramp shading
    tex_stone_blocks    irregular coursed blocks + dark mortar (stone_warm ramp)
    tex_plaster_timber  plaster field (timber beams stay geometry)
    tex_grass           speckled 4-tone grass
    tex_dirt_path       4-tone dirt with horizontal wear streaks
    tex_paving          wrapped-voronoi flagstones + dark joints
    tex_cliff_soil      horizontal soil strata (diorama cliff sides)

Runnable standalone (no bpy required):

    uv run --with-requirements tools/requirements-dev.txt python blender/scripts/generate_textures.py

The shared material library (abbey_materials.py) samples these with 'Closest'
interpolation so the pixel-art read survives into Unity.
"""

from __future__ import annotations

import struct
import zlib
from pathlib import Path

import numpy as np

SIZE = 64
TEXTURES_DIR = Path(__file__).resolve().parent.parent / "kits" / "materials" / "textures"

# ---------------------------------------------------------------------------
# Palette ramps — EXACT hex values from the "Transcribed palette (albedo
# ramps)" table in docs/ART_REFERENCE_ABBEY.md (highlight / mid / shadow /
# deep). Intentionally NOT the measured clusters — those include baked
# lighting and are the preview verification target (see doc decision note).
# ---------------------------------------------------------------------------

RAMP_ROOF_TERRACOTTA = ("#d97b4a", "#b5532e", "#7e3520", "#5f2718")
RAMP_STONE_WARM = ("#c2b49a", "#a89a80", "#7d715c", "#5c5344")
RAMP_PLASTER = ("#efe3c4", "#e8d9b8", "#d9c8a4", "#b8a988")
RAMP_GRASS = ("#7fb43c", "#5f9430", "#47762a", "#35601f")
RAMP_DIRT_PATH = ("#cfae7e", "#b3925f", "#8f7148", "#6e5738")
RAMP_CLIFF_SOIL = ("#6f4a33", "#573828", "#3f2719", "#2e1c12")
RAMP_PAVING = ("#b0aba0", "#918c80", "#6e6a5e", "#4f4c44")


def _hex_to_rgb(h: str) -> tuple[int, int, int]:
    h = h.lstrip("#")
    return int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)


def _ramp_lut(ramp: tuple[str, str, str, str]) -> np.ndarray:
    """(4, 3) uint8 lookup: index 0=highlight, 1=mid, 2=shadow, 3=deep."""
    return np.array([_hex_to_rgb(h) for h in ramp], dtype=np.uint8)


# ---------------------------------------------------------------------------
# Minimal PNG writer (RGB8, no dependencies beyond zlib)
# ---------------------------------------------------------------------------


def write_png(path: Path, rgb: np.ndarray) -> None:
    """Write an (H, W, 3) uint8 array as an RGB PNG."""
    assert rgb.dtype == np.uint8 and rgb.ndim == 3 and rgb.shape[2] == 3
    height, width = rgb.shape[:2]

    def chunk(tag: bytes, payload: bytes) -> bytes:
        return (
            struct.pack(">I", len(payload))
            + tag
            + payload
            + struct.pack(">I", zlib.crc32(tag + payload) & 0xFFFFFFFF)
        )

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)  # 8-bit RGB
    raw = b"".join(b"\x00" + rgb[y].tobytes() for y in range(height))
    png = (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", ihdr)
        + chunk(b"IDAT", zlib.compress(raw, 9))
        + chunk(b"IEND", b"")
    )
    path.write_bytes(png)


# ---------------------------------------------------------------------------
# Shared helpers — everything works on an int index map (values 0..3) that is
# converted to RGB through the ramp LUT at the very end.
# ---------------------------------------------------------------------------


def _speckle(idx: np.ndarray, rng: np.random.Generator, moves: list[tuple[int, float]]) -> None:
    """Randomly shift tone indices: for each (delta, probability), matching
    pixels move `delta` ramp steps (clamped 0..3). In-place."""
    for delta, prob in moves:
        mask = rng.random(idx.shape) < prob
        idx[mask] = np.clip(idx[mask] + delta, 0, 3)


def _to_rgb(idx: np.ndarray, ramp: tuple[str, str, str, str]) -> np.ndarray:
    return _ramp_lut(ramp)[np.clip(idx, 0, 3)]


# ---------------------------------------------------------------------------
# Texture builders — each returns a (64, 64, 3) uint8 array
# ---------------------------------------------------------------------------


def tex_roof_tiles() -> np.ndarray:
    """Offset course pattern: 8 courses of 8px, tiles 16px wide, half-tile
    offset per course. Per-tile 4-step terracotta ramp shading: lit top edge,
    shadowed bottom lap, dark gaps between tiles."""
    rng = np.random.default_rng(101)
    course_h, tile_w = 8, 16
    idx = np.full((SIZE, SIZE), 1, dtype=np.int16)

    n_courses = SIZE // course_h
    n_tiles = SIZE // tile_w
    # Per-tile base tone: mostly mid, some highlight / shadow tiles.
    tile_tone = rng.choice([0, 1, 2], size=(n_courses, n_tiles), p=(0.20, 0.62, 0.18))

    for y in range(SIZE):
        course = y // course_h
        y_in = y % course_h
        offset = (course % 2) * (tile_w // 2)
        for x in range(SIZE):
            tile = ((x + offset) % SIZE) // tile_w
            x_in = (x + offset) % tile_w
            tone = int(tile_tone[course, tile])
            if y_in == course_h - 1:
                tone = 3  # dark lap shadow under the next course
            elif y_in == course_h - 2:
                tone = min(tone + 1, 2)  # curvature shadow
            elif y_in == 0:
                tone = max(tone - 1, 0)  # lit top edge of the tile course
            if x_in == 0:
                tone = min(tone + 2, 3)  # gap between tiles in a course
            idx[y, x] = tone

    _speckle(idx, rng, [(1, 0.03), (-1, 0.03)])
    return _to_rgb(idx, RAMP_ROOF_TERRACOTTA)


def tex_stone_blocks() -> np.ndarray:
    """Irregular coursed masonry: courses of varying height, blocks of varying
    width with per-course offset, 1px dark mortar joints. stone_warm ramp."""
    rng = np.random.default_rng(102)
    course_heights = [10, 9, 11, 10, 8, 9, 7]  # sums to 64, wraps seamlessly
    assert sum(course_heights) == SIZE
    idx = np.full((SIZE, SIZE), 1, dtype=np.int16)

    y0 = 0
    for course_heights_i, ch in enumerate(course_heights):
        # Block widths for this course (wrap around at 64 for tileability).
        widths: list[int] = []
        while sum(widths) < SIZE:
            widths.append(int(rng.integers(11, 20)))
        widths[-1] = SIZE - sum(widths[:-1])  # exact wrap
        if widths[-1] < 6:  # avoid slivers at the seam
            widths[-2] += widths[-1] - 8
            widths[-1] = 8
        offset = int(rng.integers(0, SIZE))

        x0 = 0
        for w in widths:
            tone = int(rng.choice([0, 1, 2], p=(0.22, 0.58, 0.20)))
            for dx in range(w):
                x = (x0 + dx + offset) % SIZE
                for dy in range(ch):
                    y = y0 + dy
                    t = tone
                    if dy == ch - 1 or dx == w - 1:
                        t = 3  # mortar joint
                    elif dy == 0 and tone > 0:
                        t = tone - 1  # top edge catches light
                    idx[y, x] = t
            x0 += w
        y0 += ch

    _speckle(idx, rng, [(1, 0.05), (-1, 0.03)])
    return _to_rgb(idx, RAMP_STONE_WARM)


def tex_plaster_timber() -> np.ndarray:
    """Plaster infill field (timber beams stay geometry): quiet mid field with
    subtle mottling and a few deep flecks of wear."""
    rng = np.random.default_rng(103)
    idx = np.full((SIZE, SIZE), 1, dtype=np.int16)
    _speckle(idx, rng, [(1, 0.10), (-1, 0.08), (2, 0.015)])
    # Smear a few horizontal wear patches near the bottom-ish rows.
    for _ in range(6):
        y = int(rng.integers(0, SIZE))
        x = int(rng.integers(0, SIZE))
        w = int(rng.integers(3, 8))
        for dx in range(w):
            idx[y, (x + dx) % SIZE] = 2
    return _to_rgb(idx, RAMP_PLASTER)


def tex_grass() -> np.ndarray:
    """Speckled 4-tone grass: mid base, clumped shadow/deep tufts, sparse
    highlight blades."""
    rng = np.random.default_rng(104)
    idx = np.full((SIZE, SIZE), 1, dtype=np.int16)
    # Clumped darker tufts: seed points smeared 1-2px down.
    for _ in range(90):
        y = int(rng.integers(0, SIZE))
        x = int(rng.integers(0, SIZE))
        tone = int(rng.choice([2, 3], p=(0.75, 0.25)))
        length = int(rng.integers(1, 3))
        for dy in range(length):
            idx[(y + dy) % SIZE, x] = tone
    # Bright blades.
    for _ in range(60):
        y = int(rng.integers(0, SIZE))
        x = int(rng.integers(0, SIZE))
        idx[y, x] = 0
    _speckle(idx, rng, [(1, 0.06)])
    return _to_rgb(idx, RAMP_GRASS)


def tex_dirt_path() -> np.ndarray:
    """Packed dirt: mid base, horizontal wear streaks, pebbles and dark pits."""
    rng = np.random.default_rng(105)
    idx = np.full((SIZE, SIZE), 1, dtype=np.int16)
    # Horizontal wear streaks (ruts) — light and dark.
    for _ in range(26):
        y = int(rng.integers(0, SIZE))
        x = int(rng.integers(0, SIZE))
        w = int(rng.integers(4, 12))
        tone = int(rng.choice([0, 2, 2, 3]))
        for dx in range(w):
            idx[y, (x + dx) % SIZE] = tone
    # Pebbles: 2px light dots with a dark pixel below.
    for _ in range(14):
        y = int(rng.integers(0, SIZE))
        x = int(rng.integers(0, SIZE))
        idx[y, x] = 0
        idx[y, (x + 1) % SIZE] = 0
        idx[(y + 1) % SIZE, x] = 2
    _speckle(idx, rng, [(1, 0.05), (-1, 0.04)])
    return _to_rgb(idx, RAMP_DIRT_PATH)


def tex_paving() -> np.ndarray:
    """Flagstones: wrapped (toroidal) voronoi cells, per-stone ramp tone, deep
    joints between stones, light chipped corners."""
    rng = np.random.default_rng(106)
    # Jittered 4x4 grid of cell centers -> 16 flagstones, tileable via
    # torus distance.
    cells = []
    step = SIZE // 4
    for gy in range(4):
        for gx in range(4):
            cx = gx * step + step // 2 + int(rng.integers(-4, 5))
            cy = gy * step + step // 2 + int(rng.integers(-4, 5))
            cells.append((cx % SIZE, cy % SIZE))
    centers = np.array(cells, dtype=np.float32)  # (16, 2) as (x, y)
    tones = rng.choice([0, 1, 2], size=len(cells), p=(0.25, 0.55, 0.20))

    ys, xs = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32)
    dx = np.abs(xs[..., None] - centers[:, 0])
    dy = np.abs(ys[..., None] - centers[:, 1])
    dx = np.minimum(dx, SIZE - dx)  # wrap -> seamless tiling
    dy = np.minimum(dy, SIZE - dy)
    # Slight anisotropy so stones read wider than tall.
    dist = np.sqrt((dx * 1.0) ** 2 + (dy * 1.3) ** 2)
    order = np.argsort(dist, axis=-1)
    d1 = np.take_along_axis(dist, order[..., :1], axis=-1)[..., 0]
    d2 = np.take_along_axis(dist, order[..., 1:2], axis=-1)[..., 0]
    nearest = order[..., 0]

    idx = tones[nearest].astype(np.int16)
    idx[(d2 - d1) < 1.6] = 3  # joints between flagstones
    _speckle(idx, rng, [(1, 0.05), (-1, 0.04)])
    return _to_rgb(idx, RAMP_PAVING)


def tex_cliff_soil() -> np.ndarray:
    """Horizontal soil strata for the diorama cliff sides: bands of varying
    height with jittered edges, sparse embedded stones."""
    rng = np.random.default_rng(107)
    band_heights = [7, 9, 6, 8, 10, 7, 9, 8]  # sums to 64
    assert sum(band_heights) == SIZE
    band_tones = [1, 2, 1, 0, 2, 1, 3, 2]  # strata alternation, wraps ok

    # Per-column jitter of each band boundary (+-1px), tileable in x.
    idx = np.zeros((SIZE, SIZE), dtype=np.int16)
    boundaries = np.cumsum([0] + band_heights)  # 0..64
    jitter = rng.integers(-1, 2, size=(len(band_heights), SIZE))
    for x in range(SIZE):
        for b, tone in enumerate(band_tones):
            y_start = boundaries[b] + int(jitter[b, x])
            y_end = boundaries[b + 1] + (int(jitter[b + 1, x]) if b + 1 < len(band_heights) else 0)
            for y in range(y_start, y_end):
                idx[y % SIZE, x] = tone
    # Dark seam line at each stratum boundary.
    for b in range(len(band_heights)):
        for x in range(SIZE):
            y = (boundaries[b] + int(jitter[b, x])) % SIZE
            idx[y, x] = min(band_tones[b] + 2, 3)
    # Embedded stones: small light lumps.
    for _ in range(10):
        y = int(rng.integers(0, SIZE))
        x = int(rng.integers(0, SIZE))
        idx[y, x] = 0
        idx[y, (x + 1) % SIZE] = 1
    _speckle(idx, rng, [(1, 0.05), (-1, 0.04)])
    return _to_rgb(idx, RAMP_CLIFF_SOIL)


TEXTURE_BUILDERS = {
    "tex_roof_tiles": tex_roof_tiles,
    "tex_stone_blocks": tex_stone_blocks,
    "tex_plaster_timber": tex_plaster_timber,
    "tex_grass": tex_grass,
    "tex_dirt_path": tex_dirt_path,
    "tex_paving": tex_paving,
    "tex_cliff_soil": tex_cliff_soil,
}


def generate_all(out_dir: Path = TEXTURES_DIR) -> list[Path]:
    out_dir.mkdir(parents=True, exist_ok=True)
    written: list[Path] = []
    for name, builder in TEXTURE_BUILDERS.items():
        rgb = builder()
        assert rgb.shape == (SIZE, SIZE, 3), f"{name}: bad shape {rgb.shape}"
        path = out_dir / f"{name}.png"
        write_png(path, rgb)
        written.append(path)
        print(f"wrote {path} ({path.stat().st_size} bytes)")
    return written


if __name__ == "__main__":
    generate_all()
