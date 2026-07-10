from __future__ import annotations

import importlib.util
import io
import zipfile
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
TOOL_PATH = REPO_ROOT / "tools" / "acquire_merchant_shade_miniworld.py"
SPEC = importlib.util.spec_from_file_location("merchant_shade_acquire", TOOL_PATH)
assert SPEC and SPEC.loader
acquire = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(acquire)


def valid_page() -> bytes:
    return b"""
    <html><td>Authors</td><td>Shade, octoshrimpy</td>
    <a href="https://itch.io/game-assets/assets-cc0">Creative Commons Zero v1.0 Universal</a>
    <a data-upload_id="7054436"><strong title="MiniWorldSprites.zip"></strong></a>
    <script>init_ViewGame('#x', {"generate_download_url":
    "https:\\/\\/merchant-shade.itch.io\\/16x16-mini-world-sprites\\/download_url",
    "game":{"id":703908}});</script></html>
    """


def zip_bytes(entries: dict[str, bytes]) -> bytes:
    output = io.BytesIO()
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
        for name, value in entries.items():
            archive.writestr(name, value)
    return output.getvalue()


def test_source_page_requires_pinned_game_upload_and_cc0() -> None:
    endpoint = acquire.validate_source_page(valid_page())
    assert endpoint == (
        "https://merchant-shade.itch.io/16x16-mini-world-sprites/download_url"
    )
    for missing in (
        b"7054436",
        b"703908",
        b"Creative Commons Zero",
        b"assets-cc0",
        b"octoshrimpy",
    ):
        with pytest.raises(acquire.AcquisitionError):
            acquire.validate_source_page(valid_page().replace(missing, b"missing"))


@pytest.mark.parametrize(
    "url",
    [
        "http://itchio-mirror.cb031a832f44726753d6267436f3b414.r2.cloudflarestorage.com/upload2/game/703908/7054436",
        "https://evil.invalid/upload2/game/703908/7054436",
        "https://itchio-mirror.cb031a832f44726753d6267436f3b414.r2.cloudflarestorage.com/upload2/game/703908/999",
        "https://itchio-mirror.cb031a832f44726753d6267436f3b414.r2.cloudflarestorage.com/upload2/game/703908/7054436?q=x",
    ],
)
def test_download_url_rejects_non_official_shapes(url: str) -> None:
    with pytest.raises(acquire.AcquisitionError):
        acquire.validate_archive_url(url)


def test_archive_url_accepts_official_signed_upload_identity() -> None:
    query = (
        "X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=x&X-Amz-Date=x&"
        "X-Amz-Expires=60&X-Amz-SignedHeaders=host&X-Amz-Signature=x"
    )
    acquire.validate_archive_url(
        "https://itchio-mirror.cb031a832f44726753d6267436f3b414."
        "r2.cloudflarestorage.com/upload2/game/703908/7054436?" + query
    )


def test_archive_validation_rejects_html_and_identity_drift() -> None:
    with pytest.raises(acquire.AcquisitionError, match="HTML"):
        acquire.validate_archive_bytes(b"<!doctype html><title>Log in</title>")
    with pytest.raises(acquire.AcquisitionError, match="size drift"):
        acquire.validate_archive_bytes(zip_bytes({"safe/a.png": b"png"}))


@pytest.mark.parametrize(
    "member",
    ["../escape.png", "/absolute.png", "C:/drive.png", "safe\\windows.png", "./dot.png"],
)
def test_safe_extract_rejects_path_traversal(tmp_path: Path, member: str) -> None:
    archive_path = tmp_path / "bad.zip"
    archive_path.write_bytes(zip_bytes({member: b"bad"}))
    with pytest.raises(acquire.AcquisitionError):
        acquire.safe_extract(archive_path, tmp_path / "output")


def test_safe_extract_writes_only_valid_members(tmp_path: Path) -> None:
    archive_path = tmp_path / "safe.zip"
    archive_path.write_bytes(
        zip_bytes({"MiniWorldSprites/Nature/tree.png": b"png", "MiniWorldSprites/readme.txt": b"ok"})
    )
    output = tmp_path / "output"
    assert acquire.safe_extract(archive_path, output) == 2
    assert (output / "MiniWorldSprites/Nature/tree.png").read_bytes() == b"png"
    assert (output / "MiniWorldSprites/readme.txt").read_bytes() == b"ok"
