"""Copy approved PNGs unchanged and measure integer frame rectangles (Pillow required)."""
import json
import shutil
import uuid
import argparse
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
parser = argparse.ArgumentParser()
parser.add_argument("--fire-v2", action="store_true", help="Import only the approved fire damage/extinction sheets")
args = parser.parse_args()
SOURCE = Path(__file__).resolve().parent
if args.fire_v2:
    SOURCE = SOURCE.parent / "environment-hazards-v2"
DEST = ROOT / "game/Assets/Resources/EnvironmentHazards"


def meta(path, importer):
    target = Path(str(path) + ".meta")
    if not target.exists():
        target.write_text("fileFormatVersion: 2\nguid: " + uuid.uuid4().hex + "\n" + importer, encoding="utf-8")


DEST.mkdir(parents=True, exist_ok=True)
meta(DEST, "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n")
template = (ROOT / "game/Assets/Resources/StatusEffects/poison-loop-v2.png.meta").read_text(encoding="utf-8")
assets = [("fire-hit-1-medium-sheet", 4, 2), ("fire-hit-2-low-sheet", 4, 2),
          ("fire-hit-3-extinguish-sheet", 4, 2)] if args.fire_v2 else [
                            ("tree-burning-sheet", 4, 2), ("fire-trap-sheet", 4, 2),
                            ("poison-gas-sheet", 4, 2), ("spikes-retracted", 1, 1),
                            ("mushroom-hit-preburst", 1, 1)]
for name, columns, rows in assets:
    image = Image.open(SOURCE / (name + ".png"))
    alpha = image.getchannel("A")
    frames = []
    for row in range(rows):
        for column in range(columns):
            cell = (round(column * image.width / columns), round(row * image.height / rows),
                    round((column + 1) * image.width / columns), round((row + 1) * image.height / rows))
            bounds = alpha.crop(cell).point(lambda a: 255 if a >= 16 else 0).getbbox()
            if bounds is None:
                raise ValueError(f"Empty frame: {name} {row} {column}")
            left = max(cell[0], cell[0] + bounds[0] - 2)
            top = max(cell[1], cell[1] + bounds[1] - 2)
            right = min(cell[2], cell[0] + bounds[2] + 2)
            bottom = min(cell[3], cell[1] + bounds[3] + 2)
            frames.append(dict(x=left, y=image.height-bottom, width=right-left, height=bottom-top))
    data = dict(width=image.width, height=image.height,
                frameWidth=max(f["width"] for f in frames), frames=frames)
    png = DEST / (name + ".png")
    shutil.copyfile(SOURCE / png.name, png)
    png_meta = Path(str(png) + ".meta")
    if not png_meta.exists():
        lines = template.splitlines()
        lines[1] = "guid: " + uuid.uuid4().hex
        lines = ["    spriteID: " + uuid.uuid4().hex if "spriteID:" in line else
                 "  userData: Environment hazard animation" if "userData:" in line else line for line in lines]
        png_meta.write_text("\n".join(lines) + "\n", encoding="utf-8")
    manifest = DEST / (name + ".json")
    manifest.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    meta(manifest, "TextScriptImporter:\n  externalObjects: {}\n")
    print(name, image.size, len(frames), "frames", "max width", data["frameWidth"])
