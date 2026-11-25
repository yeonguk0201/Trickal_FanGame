"""Export the Erpin standing PSD layers as cropped PNG parts for the Unity 2D Animation rig.

Usage (psd-tools is required, e.g. `pip install psd-tools`):
    python tools/artwork/export_erpin_standing_parts.py [path/to/Character_Erpin_parts.psd]

Writes one PNG per pixel layer plus Erpin_Standing_Layers.json (layer order and PSD bounding boxes)
into game/Assets/Art/Characters/Player/Standing. Afterwards run
`Trickal Fan Game/Artwork/Setup Erpin Standing Rig` in Unity.
"""

import json
import re
import sys
from pathlib import Path

from psd_tools import PSDImage

REPO = Path(__file__).resolve().parents[2]
DEFAULT_PSD = REPO / "output/art/erpin-standing-parts/Character_Erpin_parts.psd"
OUT_DIR = REPO / "game/Assets/Art/Characters/Player/Standing"


def repair_name(name: str) -> str:
    """Some tools save ASCII layer names as byte-swapped UTF-16 ("䉡捫" for "Back"); decode those back."""
    if name and all(ord(c) >= 0x2000 for c in name):
        raw = name.encode("utf-16-be").decode("latin-1").replace("\x00", "")
        if raw.isascii() and raw.isprintable():
            return raw.strip()
    return name


def file_stem(layer_name: str) -> str:
    words = re.findall(r"[A-Za-z0-9]+", layer_name)
    return "Erpin_Standing_" + "".join(w[:1].upper() + w[1:] for w in words)


def main() -> None:
    psd_path = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_PSD
    psd = PSDImage.open(psd_path)
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    layers = []
    for layer in psd.descendants():
        if layer.kind != "pixel" or not layer.visible:
            continue
        image = layer.topil()
        if image is None:
            continue
        name = repair_name(layer.name)
        stem = file_stem(name)
        image.convert("RGBA").save(OUT_DIR / f"{stem}.png")
        x0, y0, x1, y1 = layer.bbox
        layers.append({"name": name,"file": f"{stem}.png", "order": len(layers), "bbox": [x0, y0, x1, y1]})

    manifest = {
        "source": psd_path.name,
        "canvas": [psd.width, psd.height],
        "layers": layers,
    }
    (OUT_DIR / "Erpin_Standing_Layers.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Exported {len(layers)} layers to {OUT_DIR}")


if __name__ == "__main__":
    main()
