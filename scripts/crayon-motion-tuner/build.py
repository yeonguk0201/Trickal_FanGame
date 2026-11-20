"""Builds the offline 크레용사용 motion tuner (walk stride, transformation timing).

Usage: python -I scripts/crayon-motion-tuner/build.py
Output: output/crayon-motion-tuner/crayon-motion-tuner.html (frames embedded, opens as a plain file).

Frames come straight from the game's movement artwork, scaled so one Unity unit is the same number of
pixels for every sheet: sprites use 400 PPU and the boss root is scaled 3.6.
"""
import base64
import io
import json
import pathlib

from PIL import Image

root = pathlib.Path(__file__).resolve().parents[2]
art = root / "game" / "Assets" / "Art" / "Bosses" / "FairyKingdom" / "Movement"
out_dir = root / "output" / "crayon-motion-tuner"
template = pathlib.Path(__file__).with_name("template.html")

PPU = 400.0
BOSS_SCALE = 3.6
PIXELS_PER_UNIT = 48.0  # preview resolution


def frame(name):
    image = Image.open(art / (name + ".png")).convert("RGBA")
    world = image.width / PPU * BOSS_SCALE
    # World distance from the sprite's centered pivot down to the lowest drawn pixel (the feet).
    bottom = image.getchannel("A").getbbox()[3]
    foot = (bottom - image.height / 2) / image.height * world
    size = max(1, round(world * PIXELS_PER_UNIT))
    image = image.resize((size, size), Image.LANCZOS)
    buffer = io.BytesIO()
    image.save(buffer, "PNG", optimize=True)
    return {"src": "data:image/png;base64," + base64.b64encode(buffer.getvalue()).decode("ascii"),
            "world": world, "foot": foot}


data = {
    "pixelsPerUnit": PIXELS_PER_UNIT,
    "walk": [frame(f"CrayonHero_Walk_{i}") for i in range(8)],
    "goldenWalk": [frame(f"CrayonHero_Golden_Walk_{i}") for i in range(8)],
    "awaken": [frame(f"CrayonHero_Awaken_{i}") for i in range(4)],
    "awakened": frame("CrayonHero_Awakened"),
    # Current game values (CrayonHeroBoss.prefab / Week15Boss3Setup).
    "speed": 3.2, "phaseTwoMultiplier": 1.35, "stride": 2.6,
    "awakeningFrameDurations": [0.3, 0.7, 1.0, 0.25], "stagger": 0.65, "tremble": 0.02,
}
out_dir.mkdir(parents=True, exist_ok=True)
html = template.read_text(encoding="utf-8").replace("/*DATA*/null", json.dumps(data))
out = out_dir / "crayon-motion-tuner.html"
out.write_text(html, encoding="utf-8")
print(out)
