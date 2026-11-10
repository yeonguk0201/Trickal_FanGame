"""Menu-1: copy the magic-library menu parts into Unity at UI size.

Source: output/menu-components-v2 (user-made parts, not tracked by Git).
Each part is cropped to its visible pixels, padded, and resized so Unity does not ship 2000px buttons.
9-slice borders and import settings are set in Unity by MenuArtSetup (Assets/Editor/MenuArtSetup.cs).

Run from the repository root:  python tools/artwork/package_menu_ui.py
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "output" / "menu-components-v2"
TARGET = ROOT / "game" / "Assets" / "Art" / "UI" / "Menu"
PAD = 4  # transparent margin kept around each crop so bilinear filtering does not clip the outline

# source file -> (Unity file name, width after crop). Height follows the source aspect.
PARTS: dict[str, tuple[str, int]] = {
    "background-library.png": ("UI_Menu_Background_Library.png", 1672),
    "panel-main.png": ("UI_Menu_Panel_Main_9s.png", 768),
    "panel-character-roster.png": ("UI_Menu_Panel_Roster_9s.png", 480),
    "panel-character-details.png": ("UI_Menu_Panel_Details_9s.png", 480),
    "title-plaque.png": ("UI_Menu_TitlePlaque.png", 640),
    "button-primary.png": ("UI_Menu_Button_Primary_9s.png", 512),
    "button-secondary.png": ("UI_Menu_Button_Secondary_9s.png", 512),
    "section-ribbon.png": ("UI_Menu_SectionRibbon_9s.png", 448),
    "slider-track.png": ("UI_Menu_Slider_Track_9s.png", 512),
    "slider-fill.png": ("UI_Menu_Slider_Fill_9s.png", 512),
    "clover-handle.png": ("UI_Menu_Clover.png", 128),
    "checkbox-empty.png": ("UI_Menu_Checkbox.png", 96),
    "checkmark.png": ("UI_Menu_Checkmark.png", 96),
    "arrow-left.png": ("UI_Menu_ArrowLeft.png", 96),
    "portrait-slot.png": ("UI_Menu_PortraitSlot.png", 192),
    "character-pedestal.png": ("UI_Menu_Pedestal.png", 640),
    "silhouette-coming-soon.png": ("UI_Menu_SilhouetteComingSoon.png", 256),
    "icon-music.png": ("UI_Menu_Icon_Music.png", 128),
    "icon-starburst.png": ("UI_Menu_Icon_Starburst.png", 128),
    "icon-heart.png": ("UI_Menu_Icon_Heart.png", 128),
    "icon-wand.png": ("UI_Menu_Icon_Wand.png", 128),
    "icon-sword.png": ("UI_Menu_Icon_Sword.png", 128),
    "icon-skill-junior.png": ("UI_Menu_Icon_SkillJunior.png", 128),
    "icon-skill-senior.png": ("UI_Menu_Icon_SkillSenior.png", 128),
    "icon-speaker.png": ("UI_Menu_Icon_Speaker.png", 128),
    "icon-sparkles.png": ("UI_Menu_Icon_Sparkles.png", 128),
    "icon-monitor.png": ("UI_Menu_Icon_Monitor.png", 128),
    "icon-fullscreen.png": ("UI_Menu_Icon_Fullscreen.png", 128),
    "icon-list.png": ("UI_Menu_Icon_List.png", 128),
    "icon-crown.png": ("UI_Menu_Icon_Crown.png", 128),
    "icon-lock.png": ("UI_Menu_Icon_Lock.png", 128),
}


def package(source: Path, target: Path, width: int) -> tuple[int, int]:
    image = Image.open(source)
    if image.mode != "RGBA":
        # Opaque art (the background) keeps its full frame.
        image = image.convert("RGB")
        if image.width != width:
            image = image.resize((width, round(image.height * width / image.width)), Image.LANCZOS)
        image.save(target, optimize=True)
        return image.size
    box = image.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
    if box is None:
        raise ValueError(f"{source.name} is fully transparent")
    box = (max(0, box[0] - PAD), max(0, box[1] - PAD), min(image.width, box[2] + PAD), min(image.height, box[3] + PAD))
    crop = image.crop(box)
    # Small parts (icons, checkbox, slot) fit by their longer side so tall and wide ones get the same weight.
    scale = width / (max(crop.size) if width <= 256 else crop.width)
    size = (max(1, round(crop.width * scale)), max(1, round(crop.height * scale)))
    resized = crop.resize(size, Image.LANCZOS)
    resized.save(target, optimize=True)
    return size


def ring(target: Path, width: int, height: int, radius: int, stroke: int) -> tuple[int, int]:
    """White rounded outline for hover/focus. Unity tints it and 9-slices it around any control."""
    from PIL import ImageDraw

    scale = 4
    big = Image.new("RGBA", (width * scale, height * scale), (255, 255, 255, 0))
    draw = ImageDraw.Draw(big)
    inset = stroke * scale / 2
    draw.rounded_rectangle((inset, inset, width * scale - inset - 1, height * scale - inset - 1),
                           radius=radius * scale, outline=(255, 255, 255, 255), width=stroke * scale)
    big.resize((width, height), Image.LANCZOS).save(target, optimize=True)
    return width, height


# Skill art already in the game, cropped for the menu tiles: (source under game/Assets, quadrant or None, target).
# hud-icons.png is a 2x2 sheet; bottom-left is the low-grade skill. The high-grade tile uses erpin-high-grade.png directly.
GAME_ART = [
    ("Resources/UserArtwork/hud-icons.png", (0, 1), "UI_Menu_Skill_LowGrade.png"),
    ("Art/Characters/Projectiles/Erpin_Basic_Orb.png", None, "UI_Menu_Skill_Basic.png"),
]


def package_game_art(source: Path, quadrant, target: Path) -> tuple[int, int]:
    image = Image.open(source).convert("RGBA")
    if quadrant is not None:
        w, h = image.width // 2, image.height // 2
        image = image.crop((quadrant[0] * w, quadrant[1] * h, quadrant[0] * w + w, quadrant[1] * h + h))
    temp = target.with_suffix(".tmp.png")
    image.save(temp)
    try:
        return package(temp, target, 160)
    finally:
        temp.unlink()


def glow(target: Path, size: int, inset: int, radius: int, blur: float) -> tuple[int, int]:
    """Soft white glow drawn behind slots and checkboxes on hover/focus (tinted in Unity)."""
    from PIL import ImageDraw, ImageFilter

    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle((inset, inset, size - inset - 1, size - inset - 1), radius=radius, fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(blur))
    image = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    image.putalpha(mask)
    image.save(target, optimize=True)
    return size, size


def rounded_fill(target: Path, size: int, radius: int) -> tuple[int, int]:
    """Solid rounded square used as a mask so portraits follow the slot's round corners."""
    from PIL import ImageDraw

    scale = 4
    big = Image.new("RGBA", (size * scale, size * scale), (255, 255, 255, 0))
    ImageDraw.Draw(big).rounded_rectangle((0, 0, size * scale - 1, size * scale - 1), radius=radius * scale,
                                          fill=(255, 255, 255, 255))
    big.resize((size, size), Image.LANCZOS).save(target, optimize=True)
    return size, size


def slash(target: Path, size: int) -> tuple[int, int]:
    """Muted mark: a red diagonal with a cream edge, drawn over a volume icon."""
    from PIL import ImageDraw

    scale = 4
    big = Image.new("RGBA", (size * scale, size * scale), (0, 0, 0, 0))
    draw = ImageDraw.Draw(big)
    a, b = (size * 0.18 * scale, size * 0.18 * scale), (size * 0.82 * scale, size * 0.82 * scale)
    for width, colour in ((size * 0.20, (255, 248, 238, 255)), (size * 0.11, (214, 69, 69, 255))):
        w = int(width * scale)
        draw.line((a, b), fill=colour, width=w)
        for x, y in (a, b):
            draw.ellipse((x - w / 2, y - w / 2, x + w / 2, y + w / 2), fill=colour)
    big.resize((size, size), Image.LANCZOS).save(target, optimize=True)
    return size, size


def main() -> int:
    if not SOURCE.is_dir():
        print(f"Missing source folder: {SOURCE}", file=sys.stderr)
        return 1
    TARGET.mkdir(parents=True, exist_ok=True)
    sizes: dict[str, list[int]] = {}
    for source_name, (target_name, width) in PARTS.items():
        source = SOURCE / source_name
        if not source.is_file():
            print(f"Missing part: {source}", file=sys.stderr)
            return 1
        sizes[target_name] = list(package(source, TARGET / target_name, width))
        print(f"{source_name:30s} -> {target_name:36s} {sizes[target_name]}")
    for source_name, quadrant, target_name in GAME_ART:
        sizes[target_name] = list(package_game_art(ROOT / "game" / "Assets" / source_name, quadrant, TARGET / target_name))
    sizes["UI_Menu_Glow_9s.png"] = list(glow(TARGET / "UI_Menu_Glow_9s.png", 128, 22, 24, 9))
    sizes["UI_Menu_RoundedFill_9s.png"] = list(rounded_fill(TARGET / "UI_Menu_RoundedFill_9s.png", 64, 18))
    sizes["UI_Menu_Slash.png"] = list(slash(TARGET / "UI_Menu_Slash.png", 64))
    sizes["UI_Menu_Ring_Pill_9s.png"] = list(ring(TARGET / "UI_Menu_Ring_Pill_9s.png", 128, 64, 32, 4))
    sizes["UI_Menu_Ring_Rounded_9s.png"] = list(ring(TARGET / "UI_Menu_Ring_Rounded_9s.png", 64, 64, 18, 4))
    (ROOT / "output" / "menu-ui-package.json").write_text(json.dumps(sizes, indent=2), encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
