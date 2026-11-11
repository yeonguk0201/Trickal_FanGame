"""Menu-2 nickname screen: copy the nickname card parts into Unity.

Sources (user-made, not tracked by Git):
- output/menu-components-v4: the card without title/section text (nickname-card-textless.png, kept at its full
  1448 x 1086 canvas so layout.json coordinates stay valid), the back and confirm buttons with baked labels.
  The copy in Unity moves the right attention rays to mirror the left ones (fix_rays); the source is untouched.
- output/menu-components-v3: the warning icon.
Import settings are set in Unity by MenuArtSetup.

Run from the repository root:  python -I tools/artwork/package_nickname_ui.py
"""
from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
V3 = ROOT / "output" / "menu-components-v3"
V4 = ROOT / "output" / "menu-components-v4"
TARGET = ROOT / "game" / "Assets" / "Art" / "UI" / "Menu"
PAD = 4  # transparent margin so bilinear filtering does not clip the outline
CARD_SIZE = (1448, 1086)

# source -> (Unity file name, width after crop). The crop follows alpha > 128 (layout.json uses that bound).
PARTS: dict[Path, tuple[str, int]] = {
    V4 / "nickname-button-back.png": ("UI_Menu_Nickname_BackButton.png", 594),
    V4 / "nickname-button-confirm.png": ("UI_Menu_Nickname_ConfirmButton.png", 1022),
    V3 / "icon-warning.png": ("UI_Menu_Icon_Warning.png", 96),
}


def package(source: Path, target: Path, width: int) -> tuple[int, int]:
    image = Image.open(source).convert("RGBA")
    box = image.getchannel("A").point(lambda v: 255 if v > 128 else 0).getbbox()
    if box is None:
        raise ValueError(f"{source.name} is fully transparent")
    box = (max(0, box[0] - PAD), max(0, box[1] - PAD), min(image.width, box[2] + PAD), min(image.height, box[3] + PAD))
    crop = image.crop(box)
    size = (width, max(1, round(crop.height * width / crop.width)))
    crop.resize(size, Image.LANCZOS).save(target, optimize=True)
    return size


# Card pixel boxes (left, top, right, bottom). The right rays pointed at the removed Erpin; they are painted over
# with the mirrored plain paper from the left side, and the left rays are mirrored onto the input's top-right.
RIGHT_RAYS = (1018, 292, 1122, 384)
LEFT_RAYS = (250, 390, 370, 467)
FEATHER = 8  # px of soft edge so the paper patch has no seam


def mirror_box(box: tuple[int, int, int, int]) -> tuple[int, int, int, int]:
    left, top, right, bottom = box
    return CARD_SIZE[0] - right, top, CARD_SIZE[0] - left, bottom


def soft_box_mask(width: int, height: int) -> np.ndarray:
    x = np.minimum(np.arange(width), np.arange(width)[::-1]) / FEATHER
    y = np.minimum(np.arange(height), np.arange(height)[::-1]) / FEATHER
    return np.clip(np.minimum(y[:, None], x[None, :]), 0, 1)


def fix_rays(card: Image.Image) -> Image.Image:
    pixels = np.array(card).astype(np.float32)
    flipped = pixels[:, ::-1]
    # 1. Paper over the right rays.
    l, t, r, b = RIGHT_RAYS
    paper = flipped[t:b, l:r]  # flipped column x is the source column on the mirrored side
    mask = soft_box_mask(r - l, b - t)[..., None]
    pixels[t:b, l:r] = pixels[t:b, l:r] * (1 - mask) + paper * mask
    # 2. Mirror the left rays (and their white outline) onto the right; only pixels unlike the paper are copied.
    l, t, r, b = LEFT_RAYS
    ml, _, mr, _ = mirror_box(LEFT_RAYS)
    source = flipped[t:b, ml:mr]
    target = pixels[t:b, ml:mr]
    background = np.median(target[..., :3].reshape(-1, 3), axis=0)
    distance = np.linalg.norm(source[..., :3] - background, axis=-1)
    alpha = np.clip((distance - 14) / 30, 0, 1)[..., None]
    pixels[t:b, ml:mr] = target * (1 - alpha) + source * alpha
    return Image.fromarray(np.clip(pixels, 0, 255).astype(np.uint8), "RGBA")


def main() -> None:
    TARGET.mkdir(parents=True, exist_ok=True)
    card = Image.open(V4 / "nickname-card-textless.png").convert("RGBA")
    if card.size != CARD_SIZE:
        raise SystemExit(f"Card must stay {CARD_SIZE} so layout.json matches; got {card.size}")
    fix_rays(card).save(TARGET / "UI_Menu_Nickname_Card.png", optimize=True)
    print("UI_Menu_Nickname_Card.png", card.size)
    for source, (name, width) in PARTS.items():
        if not source.exists():
            raise SystemExit(f"Missing {source}")
        print(name, package(source, TARGET / name, width))


if __name__ == "__main__":
    main()
