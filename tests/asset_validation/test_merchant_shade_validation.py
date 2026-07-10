from __future__ import annotations

import importlib.util
import json
import shutil
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
TOOL_PATH = REPO_ROOT / "tools" / "validate_merchant_shade_miniworld.py"
CURATED = REPO_ROOT / "unity/Assets/_Game/Art/Placeholders/MerchantShadeMiniWorld"
SPEC = importlib.util.spec_from_file_location("merchant_shade_validate", TOOL_PATH)
assert SPEC and SPEC.loader
validator = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(validator)


def copy_curated(tmp_path: Path) -> Path:
    destination = tmp_path / "MerchantShadeMiniWorld"
    shutil.copytree(CURATED, destination)
    return destination


def test_committed_curated_subset_is_self_consistent() -> None:
    result = validator.validate(CURATED)
    assert result == {
        "files": 32,
        "slices": 341,
        "mappedRoles": 61,
        "unresolvedRoles": 23,
    }


def test_validator_rejects_unlisted_png(tmp_path: Path) -> None:
    curated = copy_curated(tmp_path)
    (curated / "Nature/unlisted.png").write_bytes(
        (curated / "Nature/abbey_placeholder_miniworld_trees.png").read_bytes()
    )
    with pytest.raises(validator.ValidationError, match="inventory mismatch"):
        validator.validate(curated)


def test_validator_rejects_binary_hash_drift(tmp_path: Path) -> None:
    curated = copy_curated(tmp_path)
    path = curated / "Terrain/abbey_placeholder_miniworld_grass.png"
    path.write_bytes(path.read_bytes() + b"drift")
    with pytest.raises(validator.ValidationError, match="SHA-256 mismatch"):
        validator.validate(curated)


def test_validator_rejects_unknown_sprite_reference(tmp_path: Path) -> None:
    curated = copy_curated(tmp_path)
    path = curated / "manifest.json"
    manifest = json.loads(path.read_text(encoding="utf-8"))
    manifest["entries"][0]["defaultSprite"] = "missing:missing"
    path.write_text(json.dumps(manifest), encoding="utf-8")
    with pytest.raises(validator.ValidationError, match="unknown sprite reference"):
        validator.validate(curated)


def test_validator_rejects_non_positive_authored_footprint(tmp_path: Path) -> None:
    curated = copy_curated(tmp_path)
    path = curated / "manifest.json"
    manifest = json.loads(path.read_text(encoding="utf-8"))
    building = next(
        entry for entry in manifest["entries"] if entry["authoredFootprint"] is not None
    )
    building["authoredFootprint"] = [0, 2]
    path.write_text(json.dumps(manifest), encoding="utf-8")
    with pytest.raises(validator.ValidationError, match="invalid authoredFootprint"):
        validator.validate(curated)


def test_contact_sheet_rejects_manifest_path_traversal(tmp_path: Path) -> None:
    curated = copy_curated(tmp_path)
    with pytest.raises(validator.ValidationError, match="unsafe report abbeyPath"):
        validator.resolve_under(curated, "../../outside.png", "report abbeyPath")


def test_unresolved_signature_roles_are_explicit_and_block_final_gate() -> None:
    manifest = validator.load_manifest(CURATED)
    assert manifest["finalVisualGateBlocked"] is True
    unresolved = {item["role"]: item for item in manifest["unresolvedRoles"]}
    for role in (
        "actor.bellkeeper",
        "actor.blackHound",
        "actor.stag",
        "building.ruinedBellTower",
        "prop.shipwreckHull",
    ):
        assert unresolved[role]["reason"]
        assert unresolved[role]["temporaryIdentityProxy"] is False
