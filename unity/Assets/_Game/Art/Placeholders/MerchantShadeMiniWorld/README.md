# Merchant Shade Mini World sprite placeholders

This folder contains a curated, replaceable subset of Shade and octoshrimpy's
[Free 16x16 Mini World Sprites](https://merchant-shade.itch.io/16x16-mini-world-sprites).
The official itch.io page identifies the pack as **CC0 1.0 Universal**, permits
commercial and noncommercial use and modification, and does not require attribution.
The authors ask that the pack itself not be sold as a standalone asset collection.

`manifest.json` is the source of truth for provenance, hashes, dimensions, exact
bottom-left slice rectangles, import pivots, PPU, stable role mappings, animation
frames, visual scale, sorting, tint participation, authored obstacle footprints, and
honestly unresolved roles. The committed PNGs are byte-identical copies of selected
source sheets; the full archive and guide remain in ignored `third_party_cache/`.

Acquisition pin:

- itch game: `703908`
- itch upload: `7054436` (`MiniWorldSprites.zip`)
- archive size: `2,084,074` bytes
- SHA-256: `79eb000cfd3f64fee8ac8307f02bb867dc8b4fd7ce5a150119c51dedfa563f1f`
- acquired: `2026-07-10`
- authors: Shade and octoshrimpy
- license: [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/)

Reproduce and validate locally:

```sh
uv run --with-requirements tools/requirements-dev.txt \
  python tools/acquire_merchant_shade_miniworld.py
uv run --with pillow python tools/validate_merchant_shade_miniworld.py --write-reports
uv run --with-requirements tools/requirements-dev.txt \
  python tools/validate_merchant_shade_miniworld.py --with-cache
```

`contact-sheet.png` shows every named slice at integer nearest-neighbour scale, and
`inventory.md` lists every selected source sheet, mapped role, and unresolved role.
The contact sheet is inventory evidence, not runtime art.

The final sprite visual gate is intentionally blocked by the unresolved roles in the
manifest. In particular, this pack has no honest Bellkeeper, Black Hound, Stag, ruined
Bell Tower, broken shipwreck, moth, canine nightmare, or several other signature
nightmare proxies. Infrastructure may use the reversible 3D fallback, but canonical
sprite screenshots must not silently substitute unrelated farm animals or champions.
