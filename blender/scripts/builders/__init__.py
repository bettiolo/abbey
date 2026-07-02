"""Asset builders. Importing this package registers every builder with the
framework registry (asset_framework.register_builder).

One module per asset family. Add new builder modules to the import list below.
"""

from __future__ import annotations

import sys
from pathlib import Path

_SCRIPTS_DIR = str(Path(__file__).resolve().parent.parent)
if _SCRIPTS_DIR not in sys.path:
    sys.path.insert(0, _SCRIPTS_DIR)

from builders import abbey_ruins  # noqa: E402,F401
from builders import beasts  # noqa: E402,F401
from builders import camp_buildings  # noqa: E402,F401
from builders import campfire  # noqa: E402,F401
from builders import shipwreck_crates  # noqa: E402,F401
from builders import shipwreck_hull  # noqa: E402,F401
from builders import terrain_features  # noqa: E402,F401
