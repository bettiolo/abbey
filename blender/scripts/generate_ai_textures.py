"""Generate AI texture candidates from the owned abbey-town reference.

This is an opt-in companion to generate_textures.py. The deterministic
procedural textures remain the committed baseline; this script writes Nano
Banana/Gemini candidates to blender/kits/materials/textures_ai/ for review.

Required environment:

    GEMINI_API_KEY=...

Example:

    GEMINI_API_KEY=... uv run --with-requirements tools/requirements-dev.txt python blender/scripts/generate_ai_textures.py
    GEMINI_API_KEY=... uv run --with-requirements tools/requirements-dev.txt python blender/scripts/generate_ai_textures.py --textures tex_roof_tiles tex_stone_blocks

The default model is Gemini's Nano Banana 2 image model. Override with
--model if Google renames preview aliases.
"""

from __future__ import annotations

import argparse
import base64
import json
import mimetypes
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]
REFERENCE_IMAGE = REPO_ROOT / "docs" / "abbey-town-1.png"
OUT_DIR = REPO_ROOT / "blender" / "kits" / "materials" / "textures_ai"
DEFAULT_MODEL = "models/gemini-3.1-flash-image"


TEXTURE_PROMPTS: dict[str, str] = {
    "tex_roof_tiles": "orange terracotta roof tiles, staggered courses, individual hand-pixelled clay tiles, ridge-worn highlights, dark gaps between tile rows",
    "tex_stone_blocks": "warm gray medieval stone blocks, irregular coursed masonry, dark mortar lines, chunky pixel-art block outlines",
    "tex_plaster_timber": "warm cream plaster infill for half-timber buildings, subtle mottling and wear, no timber beams baked in",
    "tex_grass": "bright pastoral grass from the diorama top surface, clustered blades and tiny meadow speckles",
    "tex_dirt_path": "packed tan dirt path with soft medieval village ruts, pebbles, worn foot traffic streaks",
    "tex_paving": "courtyard flagstone paving, irregular square and rectangular stones, dark joints, lightly chipped corners",
    "tex_cliff_soil": "exposed diorama base cliff soil, horizontal brown earth strata, compacted dirt side texture",
    "tex_wood_planks": "barrel and crate wood planks, warm brown timber grain, dark plank seams, medieval village prop style",
    "tex_stained_glass": "small gothic stained glass window texture sheet, blue teal red and warm gold panes, dark lead outlines",
}


def _model_path(model: str) -> str:
    return model if model.startswith("models/") else f"models/{model}"


def _mime_type(path: Path) -> str:
    guessed = mimetypes.guess_type(path.name)[0]
    return guessed or "image/png"


def _load_reference_part(path: Path) -> dict[str, Any]:
    data = base64.b64encode(path.read_bytes()).decode("ascii")
    return {
        "inline_data": {
            "mime_type": _mime_type(path),
            "data": data,
        }
    }


def _prompt_for(name: str) -> str:
    subject = TEXTURE_PROMPTS[name]
    return f"""
The attached image is owned by the user and is the visual reference for their game.

Create one seamless square tileable albedo texture for: {subject}.

Style requirements:
- match the attached abbey-town pixel-art diorama style, palette, saturation, and hand-painted outline discipline
- output only the requested surface as a flat material texture, not a rendered object or scene
- seamless repeat on all four edges
- orthographic/flat texture view with no perspective
- no checkerboard background, no labels, no watermark, no UI, no border
- no baked cast shadows or scene lighting; keep any shading as local pixel-art material detail only
- crisp pixel-art read suitable for nearest-neighbor sampling in Blender and Unity
- prefer PNG output and avoid compression artifacts
- square 1:1 image

Return only the image.
""".strip()


def _request_image(
    *, api_key: str, model: str, reference: Path, texture_name: str, timeout: int
) -> tuple[bytes, str, dict[str, Any]]:
    url = (
        "https://generativelanguage.googleapis.com/v1beta/"
        f"{_model_path(model)}:generateContent?key={api_key}"
    )
    payload = {
        "contents": [
            {
                "role": "user",
                "parts": [
                    {"text": _prompt_for(texture_name)},
                    _load_reference_part(reference),
                ],
            }
        ],
        "generationConfig": {
            "responseModalities": ["IMAGE", "TEXT"],
            "imageConfig": {
                "aspectRatio": "1:1",
                "imageSize": "1K",
            },
        },
    }
    body = json.dumps(payload).encode("utf-8")
    request = urllib.request.Request(
        url,
        data=body,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=timeout) as response:
        result = json.load(response)

    text_notes: list[str] = []
    for candidate in result.get("candidates", []):
        for part in candidate.get("content", {}).get("parts", []):
            inline = part.get("inlineData") or part.get("inline_data")
            if inline and inline.get("data"):
                mime = inline.get("mimeType") or inline.get("mime_type") or "image/png"
                return base64.b64decode(inline["data"]), mime, result
            if "text" in part:
                text_notes.append(part["text"])

    notes = "\n".join(text_notes).strip()
    raise RuntimeError(f"Gemini response contained no image for {texture_name}. {notes}")


def _extension_for(mime: str) -> str:
    if mime == "image/jpeg":
        return ".jpg"
    if mime == "image/webp":
        return ".webp"
    return ".png"


def _existing_output(out_dir: Path, texture_name: str) -> Path | None:
    for extension in (".png", ".jpg", ".jpeg", ".webp"):
        path = out_dir / f"{texture_name}{extension}"
        if path.exists():
            return path
    return None


def _write_manifest(path: Path, manifest: dict[str, Any]) -> None:
    path.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference", type=Path, default=REFERENCE_IMAGE)
    parser.add_argument("--out-dir", type=Path, default=OUT_DIR)
    parser.add_argument("--model", default=os.environ.get("GEMINI_IMAGE_MODEL", DEFAULT_MODEL))
    parser.add_argument("--timeout", type=int, default=180)
    parser.add_argument("--overwrite", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument(
        "--textures",
        nargs="*",
        default=list(TEXTURE_PROMPTS),
        help="Texture ids to generate. Known ids: " + ", ".join(TEXTURE_PROMPTS),
    )
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    api_key = os.environ.get("GEMINI_API_KEY")
    if not api_key and not args.dry_run:
        print("ERROR: GEMINI_API_KEY is not set.", file=sys.stderr)
        return 2
    if not args.reference.is_file():
        print(f"ERROR: reference image not found: {args.reference}", file=sys.stderr)
        return 2

    unknown = [name for name in args.textures if name not in TEXTURE_PROMPTS]
    if unknown:
        print(f"ERROR: unknown texture id(s): {', '.join(unknown)}", file=sys.stderr)
        return 2

    args.out_dir.mkdir(parents=True, exist_ok=True)
    manifest_path = args.out_dir / "ai_texture_manifest.json"
    manifest: dict[str, Any] = {
        "source_reference": str(args.reference.relative_to(REPO_ROOT)),
        "model": _model_path(args.model),
        "generated_at_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "textures": [],
    }

    for texture_name in args.textures:
        prompt = _prompt_for(texture_name)
        if args.dry_run:
            print(f"{texture_name}:\n{prompt}\n")
            continue

        existing = _existing_output(args.out_dir, texture_name)
        if existing is not None and not args.overwrite:
            print(f"skip {existing} (exists; pass --overwrite to replace)")
            manifest["textures"].append({"id": texture_name, "path": str(existing.relative_to(REPO_ROOT)), "skipped": True})
            continue

        print(f"generating {texture_name} with {_model_path(args.model)}")
        try:
            image_bytes, mime, _ = _request_image(
                api_key=api_key or "",
                model=args.model,
                reference=args.reference,
                texture_name=texture_name,
                timeout=args.timeout,
            )
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode("utf-8", errors="replace")
            print(f"ERROR: Gemini request failed for {texture_name}: HTTP {exc.code}\n{detail}", file=sys.stderr)
            return 1

        target = args.out_dir / f"{texture_name}{_extension_for(mime)}"
        if args.overwrite:
            for old_extension in (".png", ".jpg", ".jpeg", ".webp"):
                old_path = args.out_dir / f"{texture_name}{old_extension}"
                if old_path != target and old_path.exists():
                    old_path.unlink()
        target.write_bytes(image_bytes)
        print(f"wrote {target} ({len(image_bytes)} bytes, {mime})")
        manifest["textures"].append(
            {
                "id": texture_name,
                "path": str(target.relative_to(REPO_ROOT)),
                "mime_type": mime,
                "prompt": prompt,
            }
        )

    if not args.dry_run:
        _write_manifest(manifest_path, manifest)
        print(f"wrote {manifest_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
