#!/usr/bin/env python3
"""Validate the curated Merchant Shade sprite subset and generate review reports."""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path

CURATED_RELATIVE = Path("unity/Assets/_Game/Art/Placeholders/MerchantShadeMiniWorld")
EXPECTED_ARCHIVE = {
    "itchGameId": 703908,
    "itchUploadId": 7054436,
    "archiveSize": 2_084_074,
    "archiveSha256": "79eb000cfd3f64fee8ac8307f02bb867dc8b4fd7ce5a150119c51dedfa563f1f",
}
REQUIRED_ENTRY_FIELDS = {
    "stableRoleId",
    "assetId",
    "roles",
    "componentTypeNames",
    "defaultSprite",
    "directionalSprites",
    "walkAnimation",
    "visualScale",
    "anchorOffset",
    "roleSortOffset",
    "phaseTint",
    "authoredFootprint",
    "fallbackPolicy",
    "temporaryIdentityProxy",
}


class ValidationError(RuntimeError):
    """Raised when curated data violates its deterministic manifest."""


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def png_dimensions(path: Path) -> tuple[int, int]:
    data = path.read_bytes()[:24]
    if len(data) != 24 or data[:8] != b"\x89PNG\r\n\x1a\n" or data[12:16] != b"IHDR":
        raise ValidationError(f"not a PNG: {path}")
    return struct.unpack(">II", data[16:24])


def load_manifest(curated_root: Path) -> dict:
    path = curated_root / "manifest.json"
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValidationError(f"cannot read manifest: {exc}") from exc
    if not isinstance(value, dict):
        raise ValidationError("manifest root must be an object")
    return value


def iter_sprite_refs(entry: dict) -> list[str]:
    refs: list[str] = []
    default = entry.get("defaultSprite")
    if isinstance(default, str):
        refs.append(default)
    directional = entry.get("directionalSprites") or {}
    if isinstance(directional, dict):
        refs.extend(value for value in directional.values() if isinstance(value, str))
    animation = entry.get("walkAnimation")
    if isinstance(animation, dict):
        directions = animation.get("directions") or {}
        if isinstance(directions, dict):
            for frames in directions.values():
                if isinstance(frames, list):
                    refs.extend(frame for frame in frames if isinstance(frame, str))
    return refs


def validate(curated_root: Path, cache_root: Path | None = None) -> dict:
    errors: list[str] = []
    manifest = load_manifest(curated_root)
    source = manifest.get("source") or {}
    for key, expected in EXPECTED_ARCHIVE.items():
        if source.get(key) != expected:
            errors.append(f"source.{key}: expected {expected!r}, got {source.get(key)!r}")
    if source.get("license") != "CC0-1.0" or source.get("cc0Url") != (
        "https://creativecommons.org/publicdomain/zero/1.0/"
    ):
        errors.append("source license is not the pinned CC0-1.0 declaration")

    listed_paths: set[Path] = set()
    all_slices: set[str] = set()
    slice_rects_by_file: dict[str, set[tuple[int, int, int, int]]] = {}
    files = manifest.get("files")
    if not isinstance(files, list) or not files:
        errors.append("manifest.files must be a non-empty list")
        files = []
    file_ids: set[str] = set()
    for record in files:
        if not isinstance(record, dict):
            errors.append("manifest.files contains a non-object")
            continue
        file_id = record.get("fileId")
        if not isinstance(file_id, str) or not file_id:
            errors.append("file has no fileId")
            continue
        if file_id in file_ids:
            errors.append(f"duplicate fileId: {file_id}")
        file_ids.add(file_id)
        abbey_path = record.get("abbeyPath")
        expected_prefix = (
            "unity/Assets/_Game/Art/Placeholders/MerchantShadeMiniWorld/"
        )
        if not isinstance(abbey_path, str) or not abbey_path.startswith(expected_prefix):
            errors.append(f"{file_id}: invalid abbeyPath")
            continue
        relative = Path(abbey_path.removeprefix(expected_prefix))
        if relative.is_absolute() or ".." in relative.parts:
            errors.append(f"{file_id}: unsafe abbeyPath")
            continue
        listed_paths.add(relative)
        path = curated_root / relative
        if not path.is_file():
            errors.append(f"{file_id}: missing {relative}")
            continue
        actual_hash = sha256(path)
        if actual_hash != record.get("sha256"):
            errors.append(f"{file_id}: SHA-256 mismatch")
        try:
            width, height = png_dimensions(path)
        except ValidationError as exc:
            errors.append(str(exc))
            continue
        dimensions = record.get("dimensions") or {}
        expected_dimensions = record.get("expectedDimensions") or {}
        if dimensions != {"width": width, "height": height}:
            errors.append(f"{file_id}: dimensions mismatch")
        if expected_dimensions != dimensions:
            errors.append(f"{file_id}: expectedDimensions drift")
        if width % 16 or height % 16 or record.get("sheetCellSize") != {
            "width": 16,
            "height": 16,
        }:
            errors.append(f"{file_id}: sheet is not on the 16x16 grid")
        if record.get("pixelsPerUnit") != 16:
            errors.append(f"{file_id}: pixelsPerUnit must be 16")
        rects = slice_rects_by_file.setdefault(file_id, set())
        slices = record.get("slices")
        if not isinstance(slices, list) or not slices:
            errors.append(f"{file_id}: no named slices")
            continue
        for sprite_slice in slices:
            name = sprite_slice.get("name") if isinstance(sprite_slice, dict) else None
            rect = sprite_slice.get("rect") if isinstance(sprite_slice, dict) else None
            full_name = f"{file_id}:{name}"
            if not isinstance(name, str) or not name or full_name in all_slices:
                errors.append(f"{file_id}: duplicate/invalid slice name {name!r}")
                continue
            all_slices.add(full_name)
            if not isinstance(rect, dict):
                errors.append(f"{full_name}: missing rect")
                continue
            values = tuple(rect.get(key) for key in ("x", "y", "width", "height"))
            if not all(isinstance(value, int) for value in values):
                errors.append(f"{full_name}: rect values must be integers")
                continue
            x, y, slice_width, slice_height = values
            if (
                x < 0
                or y < 0
                or slice_width <= 0
                or slice_height <= 0
                or x + slice_width > width
                or y + slice_height > height
                or any(value % 16 for value in values)
            ):
                errors.append(f"{full_name}: rect is out of bounds or off-grid")
            if values in rects:
                errors.append(f"{file_id}: duplicate slice rect {values}")
            rects.add(values)
        if cache_root is not None:
            source_path = record.get("sourcePath")
            source_file = cache_root / str(source_path)
            if not source_file.is_file():
                errors.append(f"{file_id}: cached source missing: {source_path}")
            elif sha256(source_file) != record.get("sourceSha256"):
                errors.append(f"{file_id}: cached source SHA-256 mismatch")

    actual_pngs = {
        path.relative_to(curated_root)
        for path in curated_root.rglob("*.png")
        if path.name != "contact-sheet.png"
    }
    if actual_pngs != listed_paths:
        errors.append(
            "curated PNG inventory mismatch; unlisted="
            f"{sorted(str(path) for path in actual_pngs - listed_paths)}, missing="
            f"{sorted(str(path) for path in listed_paths - actual_pngs)}"
        )

    entries = manifest.get("entries")
    if not isinstance(entries, list) or not entries:
        errors.append("manifest.entries must be a non-empty list")
        entries = []
    stable_ids: set[str] = set()
    asset_ids: set[str] = set()
    for entry in entries:
        if not isinstance(entry, dict):
            errors.append("manifest.entries contains a non-object")
            continue
        missing = REQUIRED_ENTRY_FIELDS - set(entry)
        if missing:
            errors.append(f"entry {entry.get('stableRoleId')}: missing {sorted(missing)}")
        stable_id = entry.get("stableRoleId")
        asset_id = entry.get("assetId")
        if not isinstance(stable_id, str) or not stable_id or stable_id in stable_ids:
            errors.append(f"duplicate/invalid stableRoleId: {stable_id!r}")
        else:
            stable_ids.add(stable_id)
        if not isinstance(asset_id, str) or not asset_id or asset_id in asset_ids:
            errors.append(f"duplicate/invalid assetId: {asset_id!r}")
        else:
            asset_ids.add(asset_id)
        for sprite_ref in iter_sprite_refs(entry):
            if sprite_ref not in all_slices:
                errors.append(f"{stable_id}: unknown sprite reference {sprite_ref}")
        scale = entry.get("visualScale")
        if not isinstance(scale, (int, float)) or scale <= 0:
            errors.append(f"{stable_id}: visualScale must be positive")
        anchor = entry.get("anchorOffset")
        if not isinstance(anchor, list) or len(anchor) != 2 or not all(
            isinstance(value, (int, float)) for value in anchor
        ):
            errors.append(f"{stable_id}: invalid anchorOffset")
        footprint = entry.get("authoredFootprint")
        if footprint is not None and (
            not isinstance(footprint, list)
            or len(footprint) != 2
            or not all(isinstance(value, (int, float)) and value > 0 for value in footprint)
        ):
            errors.append(f"{stable_id}: invalid authoredFootprint")

    unresolved = manifest.get("unresolvedRoles")
    if not isinstance(unresolved, list):
        errors.append("unresolvedRoles must be a list")
        unresolved = []
    unresolved_ids: set[str] = set()
    for item in unresolved:
        stable_id = item.get("stableRoleId") if isinstance(item, dict) else None
        if not isinstance(stable_id, str) or stable_id in unresolved_ids:
            errors.append(f"duplicate/invalid unresolved stableRoleId: {stable_id!r}")
            continue
        unresolved_ids.add(stable_id)
        if not item.get("reason") or item.get("temporaryIdentityProxy") is not False:
            errors.append(f"{stable_id}: unresolved role needs an honest reason and no proxy")
    if bool(unresolved) != bool(manifest.get("finalVisualGateBlocked")):
        errors.append("finalVisualGateBlocked must match unresolved-role presence")

    for required in ("README.md", "LICENSE-CC0-1.0.txt", "inventory.md", "contact-sheet.png"):
        if not (curated_root / required).is_file():
            errors.append(f"missing review/provenance file: {required}")
    reports = manifest.get("reports") or {}
    contact = curated_root / "contact-sheet.png"
    if contact.is_file():
        contact_record = reports.get("contactSheet") or {}
        if contact_record.get("sha256") != sha256(contact):
            errors.append("contact-sheet.png SHA-256 mismatch")
        width, height = png_dimensions(contact)
        if contact_record.get("dimensions") != {"width": width, "height": height}:
            errors.append("contact-sheet.png dimensions mismatch")

    if errors:
        raise ValidationError("\n".join(errors))
    return {
        "files": len(files),
        "slices": len(all_slices),
        "mappedRoles": len(entries),
        "unresolvedRoles": len(unresolved),
    }


def write_inventory(curated_root: Path, manifest: dict) -> None:
    lines = [
        "# Merchant Shade Mini World curated inventory",
        "",
        "Generated deterministically from `manifest.json`. The source archive is not committed.",
        "",
        "## Selected source sheets",
        "",
        "| File ID | Category | Dimensions | SHA-256 | Source path |",
        "|---|---|---:|---|---|",
    ]
    for record in manifest["files"]:
        dims = record["dimensions"]
        lines.append(
            f"| `{record['fileId']}` | {record['category']} | "
            f"{dims['width']}×{dims['height']} | `{record['sha256']}` | "
            f"`{record['sourcePath']}` |"
        )
    lines += ["", "## Mapped roles", "", "| Stable role | Asset ID | Sprite | Proxy |", "|---|---|---|---:|"]
    for entry in manifest["entries"]:
        lines.append(
            f"| `{entry['stableRoleId']}` | `{entry['assetId']}` | "
            f"`{entry['defaultSprite']}` | {str(entry['temporaryIdentityProxy']).lower()} |"
        )
    lines += ["", "## Explicitly unresolved roles", "", "| Role | Asset ID | Signature | Reason |", "|---|---|---:|---|"]
    for role in manifest["unresolvedRoles"]:
        lines.append(
            f"| `{role['role']}` | `{role['assetId']}` | "
            f"{str(role['signatureRole']).lower()} | {role['reason']} |"
        )
    (curated_root / "inventory.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_contact_sheet(curated_root: Path, manifest: dict) -> None:
    try:
        from PIL import Image, ImageDraw
    except ImportError as exc:
        raise ValidationError("Pillow is required only for --write-reports") from exc
    tiles: list[tuple[str, object]] = []
    for record in manifest["files"]:
        # abbeyPath includes category subfolder; resolve from the curated root.
        relative = record["abbeyPath"].split("MerchantShadeMiniWorld/", 1)[1]
        image = Image.open(curated_root / relative).convert("RGBA")
        height = record["dimensions"]["height"]
        for sprite_slice in record["slices"]:
            rect = sprite_slice["rect"]
            left = rect["x"]
            top = height - (rect["y"] + rect["height"])
            crop = image.crop((left, top, left + rect["width"], top + rect["height"]))
            tiles.append((sprite_slice["name"], crop))
    columns, tile_width, tile_height, scale = 8, 144, 168, 8
    rows = (len(tiles) + columns - 1) // columns
    sheet = Image.new("RGBA", (columns * tile_width, rows * tile_height), (29, 30, 33, 255))
    draw = ImageDraw.Draw(sheet)
    for index, (name, sprite) in enumerate(tiles):
        x = (index % columns) * tile_width
        y = (index // columns) * tile_height
        enlarged = sprite.resize((16 * scale, 16 * scale), Image.Resampling.NEAREST)
        sheet.alpha_composite(enlarged, (x + 8, y + 4))
        draw.text((x + 4, y + 136), name[:22], fill=(235, 235, 235, 255))
    sheet.save(curated_root / "contact-sheet.png", optimize=False, compress_level=9)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--with-cache", action="store_true", help="also validate ignored source files")
    parser.add_argument("--write-reports", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    repo_root = args.repo_root.resolve()
    curated_root = repo_root / CURATED_RELATIVE
    try:
        manifest = load_manifest(curated_root)
        if args.write_reports:
            write_inventory(curated_root, manifest)
            write_contact_sheet(curated_root, manifest)
            print("Wrote inventory.md and contact-sheet.png")
        cache_root = None
        if args.with_cache:
            cache_root = repo_root / (
                "third_party_cache/MerchantShade/MiniWorldSprites/extracted"
            )
        result = validate(curated_root, cache_root)
    except ValidationError as exc:
        print(f"ERROR:\n{exc}")
        return 1
    print(
        "Validated Merchant Shade subset: "
        f"{result['files']} files, {result['slices']} slices, "
        f"{result['mappedRoles']} mapped roles, "
        f"{result['unresolvedRoles']} explicitly unresolved roles"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
