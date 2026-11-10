"""Menu-2 title art: trims the user's logo and start button PNGs into game/Assets/Art/UI/Menu
(MenuArtSetup configures their importers with the other menu parts).

The start button's baked "GAME START" text is painted over with the button's own row colours, so Unity draws the
label with the game font (TMP) and the button keeps the shared hover/focus behaviour.

Usage: python -I tools/artwork/package_title_ui.py <logo.png> <start_button.png>
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parents[2] / "game" / "Assets" / "Art" / "UI" / "Menu"
# Text box in the 2172 x 724 source and the clean columns on each side of it (between the text and the stars).
TEXT_BOX = (530, 340, 1645, 520)  # left, top, right, bottom
CLEAN_LEFT, CLEAN_RIGHT = 505, 1665


def trim(image: Image.Image) -> Image.Image:
    alpha = image.getchannel("A").point(lambda a: 255 if a > 8 else 0)
    return image.crop(alpha.getbbox())


def erase_text(image: Image.Image) -> Image.Image:
    pixels = np.array(image).astype(np.float32)
    left, top, right, bottom = TEXT_BOX
    for y in range(top, bottom):
        a, b = pixels[y, CLEAN_LEFT], pixels[y, CLEAN_RIGHT]
        t = (np.arange(left, right) - CLEAN_LEFT) / (CLEAN_RIGHT - CLEAN_LEFT)
        pixels[y, left:right] = a[None, :] * (1 - t[:, None]) + b[None, :] * t[:, None]
    return Image.fromarray(np.clip(pixels, 0, 255).astype(np.uint8), "RGBA")


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    logo = Image.open(sys.argv[1]).convert("RGBA")
    button = Image.open(sys.argv[2]).convert("RGBA")
    if button.size != (2172, 724):
        raise SystemExit(f"Unexpected start button size {button.size}; update TEXT_BOX first.")
    OUT.mkdir(parents=True, exist_ok=True)
    trim(logo).save(OUT / "UI_Menu_Title_Logo.png")
    trim(erase_text(button)).save(OUT / "UI_Menu_Title_StartButton.png")
    for name in ("UI_Menu_Title_Logo.png", "UI_Menu_Title_StartButton.png"):
        print(name, Image.open(OUT / name).size)


if __name__ == "__main__":
    main()
