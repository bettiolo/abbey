#!/usr/bin/env python3
"""Acquire the pinned Merchant Shade Mini World Sprites archive safely.

The tool uses itch.io's public, first-party download flow.  It deliberately does
not accept credentials, cookies supplied by a user, mirrors, or changed uploads.
The downloaded ZIP and extracted source pack stay in the ignored
``third_party_cache`` directory; only a reviewed subset is copied into Unity.
"""

from __future__ import annotations

import argparse
import hashlib
import http.cookiejar
import json
import os
import re
import shutil
import stat
import tempfile
import urllib.parse
import urllib.request
import zipfile
from pathlib import Path, PurePosixPath

SOURCE_PAGE = "https://merchant-shade.itch.io/16x16-mini-world-sprites"
GAME_ID = 703908
UPLOAD_ID = 7054436
ARCHIVE_NAME = "MiniWorldSprites.zip"
ARCHIVE_SIZE = 2_084_074
ARCHIVE_SHA256 = "79eb000cfd3f64fee8ac8307f02bb867dc8b4fd7ce5a150119c51dedfa563f1f"
CC0_URL = "https://creativecommons.org/publicdomain/zero/1.0/"
CACHE_RELATIVE = Path("third_party_cache/MerchantShade/MiniWorldSprites")
MAX_PAGE_BYTES = 4 * 1024 * 1024
MAX_ARCHIVE_BYTES = 4 * 1024 * 1024
MAX_EXTRACTED_BYTES = 64 * 1024 * 1024
USER_AGENT = "AbbeyAssetAcquirer/1.0 (+source-pinned public itch.io download)"


class AcquisitionError(RuntimeError):
    """Raised when the public source or archive violates the acquisition pin."""


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def read_limited(response: object, limit: int) -> bytes:
    """Read at most *limit* bytes plus one byte used to detect overflow."""
    data = response.read(limit + 1)  # type: ignore[attr-defined]
    if len(data) > limit:
        raise AcquisitionError(f"response exceeds safety limit of {limit} bytes")
    return data


def validate_source_page(page: bytes) -> str:
    """Validate official game/upload/license identity and return download endpoint."""
    try:
        html = page.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise AcquisitionError("official source page is not UTF-8 HTML") from exc

    checks = {
        "game identity": rf'"id"\s*:\s*{GAME_ID}\b',
        "upload identity": rf'data-upload_id=["\']{UPLOAD_ID}["\']',
        "archive filename": rf'title=["\']{re.escape(ARCHIVE_NAME)}["\']',
        "authors": r"Shade.*octoshrimpy",
        "CC0 declaration": r"Creative Commons Zero v1\.0 Universal",
        "CC0 category link": r"https://itch\.io/game-assets/assets-cc0",
    }
    for label, pattern in checks.items():
        if re.search(pattern, html, flags=re.IGNORECASE | re.DOTALL) is None:
            raise AcquisitionError(f"official source page is missing {label}")

    match = re.search(r'"generate_download_url"\s*:\s*"([^"]+)"', html)
    if match is None:
        raise AcquisitionError("official source page has no public download endpoint")
    endpoint = match.group(1).replace(r"\/", "/")
    parsed = urllib.parse.urlparse(endpoint)
    if parsed.scheme != "https" or parsed.hostname != "merchant-shade.itch.io":
        raise AcquisitionError(f"unexpected download endpoint: {endpoint}")
    expected_path = "/16x16-mini-world-sprites/download_url"
    if parsed.path != expected_path or parsed.params or parsed.query or parsed.fragment:
        raise AcquisitionError(f"unexpected download endpoint path: {endpoint}")
    return endpoint


def validate_archive_url(url: str) -> None:
    parsed = urllib.parse.urlparse(url)
    hostname = parsed.hostname or ""
    official_r2 = re.fullmatch(
        r"itchio-mirror\.[a-f0-9]{32}\.r2\.cloudflarestorage\.com", hostname
    )
    if parsed.scheme != "https" or official_r2 is None:
        raise AcquisitionError("itch.io returned an unexpected archive storage host")
    if parsed.username or parsed.password or parsed.fragment:
        raise AcquisitionError("itch.io returned an unexpected archive URL shape")
    expected_path = f"/upload2/game/{GAME_ID}/{UPLOAD_ID}"
    if parsed.path != expected_path:
        raise AcquisitionError("itch.io returned an archive URL for another upload")
    query = urllib.parse.parse_qs(parsed.query, keep_blank_values=True)
    required = {
        "X-Amz-Algorithm",
        "X-Amz-Credential",
        "X-Amz-Date",
        "X-Amz-Expires",
        "X-Amz-SignedHeaders",
        "X-Amz-Signature",
    }
    if set(query) != required or any(len(values) != 1 for values in query.values()):
        raise AcquisitionError("itch.io returned malformed archive URL credentials")


def validate_archive_bytes(data: bytes) -> None:
    if data[:4] not in (b"PK\x03\x04", b"PK\x05\x06", b"PK\x07\x08"):
        preview = data[:64].lstrip().lower()
        if preview.startswith((b"<!doctype html", b"<html")):
            raise AcquisitionError("download returned HTML instead of the pinned ZIP")
        raise AcquisitionError("download is not a ZIP archive")
    if len(data) != ARCHIVE_SIZE:
        raise AcquisitionError(
            f"archive size drift: expected {ARCHIVE_SIZE}, received {len(data)}"
        )
    digest = sha256_bytes(data)
    if digest != ARCHIVE_SHA256:
        raise AcquisitionError(
            f"archive hash drift: expected {ARCHIVE_SHA256}, received {digest}"
        )


def safe_member_path(name: str) -> PurePosixPath:
    """Validate and normalize one ZIP member path."""
    if not name or "\\" in name or "\x00" in name:
        raise AcquisitionError(f"unsafe ZIP member path: {name!r}")
    raw_parts = name.split("/")
    if any(part in ("", ".", "..") for part in raw_parts):
        raise AcquisitionError(f"traversing ZIP member path: {name!r}")
    path = PurePosixPath(name)
    if path.is_absolute() or path.parts[0].endswith(":"):
        raise AcquisitionError(f"absolute ZIP member path: {name!r}")
    return path


def validate_zip_members(archive: zipfile.ZipFile) -> list[tuple[zipfile.ZipInfo, PurePosixPath]]:
    members: list[tuple[zipfile.ZipInfo, PurePosixPath]] = []
    total_size = 0
    seen: set[PurePosixPath] = set()
    for info in archive.infolist():
        path = safe_member_path(info.filename.rstrip("/"))
        if path in seen:
            raise AcquisitionError(f"duplicate ZIP member: {info.filename!r}")
        seen.add(path)
        if info.flag_bits & 0x1:
            raise AcquisitionError(f"encrypted ZIP member: {info.filename!r}")
        unix_mode = (info.external_attr >> 16) & 0xFFFF
        if unix_mode and stat.S_ISLNK(unix_mode):
            raise AcquisitionError(f"symbolic-link ZIP member: {info.filename!r}")
        if not info.is_dir():
            total_size += info.file_size
            if info.compress_size == 0 and info.file_size > 0:
                raise AcquisitionError(f"invalid compression size: {info.filename!r}")
            if info.compress_size and info.file_size / info.compress_size > 200:
                raise AcquisitionError(f"excessive ZIP compression ratio: {info.filename!r}")
        if total_size > MAX_EXTRACTED_BYTES:
            raise AcquisitionError("ZIP expands beyond the safety limit")
        members.append((info, path))
    if not members:
        raise AcquisitionError("ZIP contains no members")
    return members


def safe_extract(archive_path: Path, destination: Path) -> int:
    """Extract a validated archive without delegating paths to extractall()."""
    with zipfile.ZipFile(archive_path) as archive:
        members = validate_zip_members(archive)
        for info, relative in members:
            output = destination.joinpath(*relative.parts)
            if info.is_dir():
                output.mkdir(parents=True, exist_ok=True)
                continue
            output.parent.mkdir(parents=True, exist_ok=True)
            with archive.open(info) as source, output.open("wb") as target:
                shutil.copyfileobj(source, target)
    return sum(not info.is_dir() for info, _ in members)


def make_opener() -> urllib.request.OpenerDirector:
    cookie_jar = http.cookiejar.CookieJar()
    return urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cookie_jar))


def http_request(opener: urllib.request.OpenerDirector, request: urllib.request.Request, limit: int) -> bytes:
    try:
        with opener.open(request, timeout=60) as response:
            return read_limited(response, limit)
    except (OSError, urllib.error.URLError) as exc:
        raise AcquisitionError(f"official source request failed: {exc}") from exc


def acquire(repo_root: Path, force: bool = False) -> tuple[Path, Path, int]:
    cache_root = repo_root / CACHE_RELATIVE
    archive_path = cache_root / ARCHIVE_NAME
    extracted_path = cache_root / "extracted"

    if archive_path.exists() and not force:
        data = archive_path.read_bytes()
        validate_archive_bytes(data)
    else:
        opener = make_opener()
        page_request = urllib.request.Request(
            SOURCE_PAGE, headers={"User-Agent": USER_AGENT, "Accept": "text/html"}
        )
        page = http_request(opener, page_request, MAX_PAGE_BYTES)
        validate_source_page(page)

        file_endpoint = (
            f"{SOURCE_PAGE}/file/{UPLOAD_ID}?source=view_game&as_props=1"
        )
        endpoint_request = urllib.request.Request(
            file_endpoint,
            data=b"",
            headers={
                "User-Agent": USER_AGENT,
                "Accept": "application/json",
                "Content-Type": "application/x-www-form-urlencoded",
                "Referer": SOURCE_PAGE,
                "X-Requested-With": "XMLHttpRequest",
            },
            method="POST",
        )
        response_data = http_request(opener, endpoint_request, 64 * 1024)
        try:
            payload = json.loads(response_data.decode("utf-8"))
            download_url = payload["url"]
        except (UnicodeDecodeError, json.JSONDecodeError, KeyError, TypeError) as exc:
            raise AcquisitionError("itch.io download endpoint returned invalid JSON") from exc
        if not isinstance(download_url, str):
            raise AcquisitionError("itch.io download endpoint returned no URL")
        validate_archive_url(download_url)

        archive_request = urllib.request.Request(
            download_url,
            headers={
                "User-Agent": USER_AGENT,
                "Accept": "application/zip, application/octet-stream",
                "Referer": SOURCE_PAGE,
            },
        )
        data = http_request(opener, archive_request, MAX_ARCHIVE_BYTES)
        validate_archive_bytes(data)
        cache_root.mkdir(parents=True, exist_ok=True)
        temporary = archive_path.with_suffix(".zip.partial")
        temporary.write_bytes(data)
        os.replace(temporary, archive_path)

    with tempfile.TemporaryDirectory(prefix="miniworld-extract-", dir=cache_root) as temp:
        staged = Path(temp) / "extracted"
        staged.mkdir()
        count = safe_extract(archive_path, staged)
        if extracted_path.exists():
            shutil.rmtree(extracted_path)
        shutil.move(str(staged), extracted_path)

    audit = {
        "sourcePage": SOURCE_PAGE,
        "gameId": GAME_ID,
        "uploadId": UPLOAD_ID,
        "archiveName": ARCHIVE_NAME,
        "archiveSize": archive_path.stat().st_size,
        "archiveSha256": sha256_bytes(archive_path.read_bytes()),
        "extractedFileCount": count,
    }
    (cache_root / "acquisition-audit.json").write_text(
        json.dumps(audit, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )
    return archive_path, extracted_path, count


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repo-root", type=Path, default=Path(__file__).resolve().parents[1]
    )
    parser.add_argument(
        "--force", action="store_true", help="re-download even when the pinned ZIP is cached"
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        archive, extracted, count = acquire(args.repo_root.resolve(), args.force)
    except AcquisitionError as exc:
        print(f"ERROR: {exc}")
        return 1
    print(f"Verified archive: {archive}")
    print(f"SHA-256: {ARCHIVE_SHA256}")
    print(f"Safely extracted {count} files to: {extracted}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
