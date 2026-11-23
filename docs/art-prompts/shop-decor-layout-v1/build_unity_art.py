"""Builds the Unity shop room art: door-free maps, door overlays, foreground strips, and hit-stage frames.

Run after build_preview.py: python -I docs/art-prompts/shop-decor-layout-v1/build_unity_art.py
Replacing <theme>-room-walled.png with a hand-made door-free map needs no code change.
"""
import shutil
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.stdout.reconfigure(encoding="utf-8")

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
DOWNLOADS = Path.home() / "Downloads"
OUTPUT = ROOT / "game" / "Assets" / "Art" / "Shop" / "Decor"
THEMES = {"sist": "시스트 상점 상점문", "goldi": "골디 상점 비밀문"}
# Source rects (x0, y0, x1, y1). ShopDecorSetup uses the same numbers to place them.
DOORS = {"up": (700, 0, 972, 190), "down": (700, 722, 972, 941), "left": (55, 330, 256, 595), "right": (1416, 330, 1617, 595)}
# The strips start above the corner pillars; foreground_alpha then cuts them along the wall's real top edge.
FOREGROUND_TOP = 760
FOREGROUND_DOOR = (700, 972)
# Starts below the floor pattern's own dark lines, which would otherwise poke up as spikes.
WALL_TOP_SEARCH = {"sist": (790, 812), "goldi": (786, 800)}
WALL_OUTLINE_LUMINANCE = 135
# Corner pillar tops as ellipses: center x, base y, half width, height.
PILLARS = {"sist": ((162, 795, 51, 27), (1509, 795, 51, 27)), "goldi": ((158, 796, 51, 24), (1513, 796, 51, 24))}
CENTER_BLEND = 12
# The fill fades back into the untouched floor over this many pixels on the door's room side.
FLOOR_FADE = 18


def floor_fade(name, rect):
    """Weight of the fill inside the rect: 1 over the door, falling to 0 at the edge that faces the floor."""
    x0, y0, x1, y1 = rect
    width, height = x1 - x0, y1 - y0
    ramp = np.clip(np.arange(1, FLOOR_FADE + 1) / FLOOR_FADE, 0, 1)
    weight = np.ones((height, width))
    if name == "up":
        weight[-FLOOR_FADE:, :] = ramp[::-1][:, None]
    elif name == "down":
        weight[:FLOOR_FADE, :] = ramp[:, None]
    elif name == "left":
        weight[:, -FLOOR_FADE:] = ramp[::-1][None, :]
    else:
        weight[:, :FLOOR_FADE] = ramp[None, :]
    return weight[..., None]


def fill_door(image, rect, along_x):
    """Mirrors the wall on each side of the door into it, so both outer seams stay continuous."""
    x0, y0, x1, y1 = rect
    start, end = (x0, x1) if along_x else (y0, y1)
    middle = (start + end) / 2
    positions = np.arange(start, end)
    near = 2 * start - 1 - positions
    far = 2 * end - 1 - positions
    if near.min() < 0 or far.max() >= image.shape[1 if along_x else 0]:
        raise ValueError(f"door rect {rect} has no wall to mirror")
    near_part = np.take(image, near, axis=1 if along_x else 0)
    far_part = np.take(image, far, axis=1 if along_x else 0)
    weight = np.clip((positions - middle + CENTER_BLEND) / (2 * CENTER_BLEND), 0, 1)
    shape = (1, -1, 1) if along_x else (-1, 1, 1)
    blended = near_part * (1 - weight.reshape(shape)) + far_part * weight.reshape(shape)
    out = image.copy()
    if along_x:
        out[y0:y1, x0:x1] = np.rint(blended[y0:y1]).astype(np.uint8)
    else:
        out[y0:y1, x0:x1] = np.rint(blended[:, x0:x1]).astype(np.uint8)
    return out


def foreground_alpha(theme, image):
    """Opacity of the front wall: starts at each column's stone outline and rises over the corner pillars."""
    luminance = image[..., :3].mean(axis=2)
    start, end = WALL_TOP_SEARCH[theme]
    band = luminance[start:end] < WALL_OUTLINE_LUMINANCE
    found = band.any(axis=0)
    tops = np.where(found, band.argmax(axis=0) + start, end).astype(float)
    # A lone speck must not notch the edge, so each column takes its neighbourhood's median.
    padded = np.pad(tops, 2, mode="edge")
    tops = np.median(np.lib.stride_tricks.sliding_window_view(padded, 5), axis=1)
    columns = np.arange(image.shape[1])
    for center, base, half_width, height in PILLARS[theme]:
        ratio = np.clip((columns - center) / half_width, -1, 1)
        inside = np.abs(columns - center) <= half_width
        tops = np.where(inside, np.minimum(tops, base - height * np.sqrt(1 - ratio ** 2)), tops)
    rows = np.arange(image.shape[0])[:, None]
    return np.clip(rows + 1 - tops[None, :], 0, 1)


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for theme, source in THEMES.items():
        original = np.array(Image.open(DOWNLOADS / f"{source}.png").convert("RGBA"))
        original[original[..., 3] <= 24] = 0
        walled = original
        for name, rect in DOORS.items():
            filled = fill_door(walled, rect, name in ("up", "down"))
            x0, y0, x1, y1 = rect
            fade = floor_fade(name, rect)
            walled = walled.copy()
            walled[y0:y1, x0:x1] = np.rint(walled[y0:y1, x0:x1] * (1 - fade) + filled[y0:y1, x0:x1] * fade).astype(np.uint8)
            Image.fromarray(original[y0:y1, x0:x1], "RGBA").save(OUTPUT / f"{theme}-door-{name}.png", optimize=True)
        Image.fromarray(walled, "RGBA").save(OUTPUT / f"{theme}-room-walled.png", optimize=True)
        left, right = FOREGROUND_DOOR
        front = walled.copy()
        front[..., 3] = np.rint(front[..., 3] * foreground_alpha(theme, walled)).astype(np.uint8)
        parts = {"left": front[FOREGROUND_TOP:, :left], "center": front[FOREGROUND_TOP:, left:right],
                 "right": front[FOREGROUND_TOP:, right:]}
        for name, part in parts.items():
            Image.fromarray(part, "RGBA").save(OUTPUT / f"{theme}-foreground-{name}.png", optimize=True)
        print(theme, "size", original.shape[1], original.shape[0])
    copied = 0
    for frame in sorted((HERE / "frames").glob("*.png")):
        shutil.copyfile(frame, OUTPUT / frame.name)
        copied += 1
    print("frames copied", copied)


if __name__ == "__main__":
    main()
