"""Slices the shop decor sheets into aligned frames and builds the single-file layout preview.

Run: python -I docs/art-prompts/shop-decor-layout-v1/build_preview.py
Source sheets stay untouched in the Downloads folder.
"""
import base64
import io
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.stdout.reconfigure(encoding="utf-8")

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
DOWNLOADS = Path.home() / "Downloads"
ARTWORK = ROOT / "game" / "Assets" / "Resources" / "UserArtwork"
FRAMES = HERE / "frames"

ALPHA_FLOOR = 24
PREVIEW_MAX_HEIGHT = 420
PX_PER_UNIT = 100
VIEW_W = 1867
VIEW_H = 1050
# Same registration as FairyVillageRoomGeometry: 1572 source px = 16 units, door axes at source (836, 470.5).
MAP_SCALE = PX_PER_UNIT * 16 / 1572
MAP_CENTER = (836, 470.5)
# FairyVillageRoomGeometry.WalkableFloor for the 16x9 room.
FLOOR_HALF_UNITS = (6.5, 3.5)

PROPS = [
    ("goldi-stall", "골디 상점(가게)", "황금 가게", "goldi"),
    ("goldi-pedestal", "골디 상점(받침대)", "받침대", "goldi"),
    ("goldi-treasure", "골디 상점(상품 뭉텅이)", "상품 뭉텅이", "goldi"),
    ("goldi-crate-gold", "골디 상점 상자2", "금장 상자", "goldi"),
    ("goldi-barrel", "골디 상점 상자3", "금장 통", "goldi"),
    ("sist-stall", "시스트 상점 에셋(가게)", "천막 가게", "sist"),
    ("sist-crates", "시스트 상점 에셋(상자)", "상자 더미", "sist"),
    ("sist-sign", "시스트 상점 에셋(팻말)", "팻말", "sist"),
    ("sist-crate-potion", "시스트 상점 상자", "물약 상자", "sist"),
]
CHARACTERS = [("char-goldi", "goldi", "골디", "goldi"), ("char-sist", "sist", "시스트", "sist")]
MAPS = [("goldi", "shop", "골디 상점 상점문"), ("goldi", "reward", "골디 상점 보상문"), ("goldi", "secret", "골디 상점 비밀문"),
        ("sist", "shop", "시스트 상점 상점문"), ("sist", "reward", "시스트 상점 보상문"), ("sist", "secret", "시스트 상점 비밀문")]


def load_clean(path):
    data = np.array(Image.open(path).convert("RGBA"))
    data[data[..., 3] <= ALPHA_FLOOR] = 0
    return data


def split_columns(data):
    solid = (data[..., 3] > 0).any(axis=0)
    gaps, x = [], 0
    while x < len(solid):
        if solid[x]:
            x += 1
            continue
        start = x
        while x < len(solid) and not solid[x]:
            x += 1
        if start > 0 and x < len(solid):
            gaps.append((x - start, (start + x) // 2))
    cuts = sorted(mid for _, mid in sorted(gaps, reverse=True)[:2])
    if len(cuts) != 2:
        raise ValueError("expected three frames separated by two gaps")
    return [data[:, a:b] for a, b in zip([0, *cuts], [*cuts, data.shape[1]])]


def best_shift(reference, frame, step=4, reach=60):
    """Finds the frame offset that overlaps the intact frame most, so hit stages swap without jumping."""
    ref = reference[::step, ::step, 3] > 0
    cur = frame[::step, ::step, 3] > 0
    height = max(ref.shape[0], cur.shape[0])
    width = max(ref.shape[1], cur.shape[1])
    a = np.zeros((height, width), bool)
    b = np.zeros((height, width), bool)
    a[: ref.shape[0], : ref.shape[1]] = ref
    b[: cur.shape[0], : cur.shape[1]] = cur
    best, best_score = (0, 0), -1
    span = reach // step
    for dy in range(-span, span + 1):
        for dx in range(-span, span + 1):
            score = int((a & np.roll(np.roll(b, dy, axis=0), dx, axis=1)).sum())
            if score > best_score:
                best, best_score = (dx * step, dy * step), score
    return best


def align(frames):
    shifts = [(0, 0)] + [best_shift(frames[0], f) for f in frames[1:]]
    left = min(dx for dx, _ in shifts)
    top = min(dy for _, dy in shifts)
    right = max(dx + f.shape[1] for (dx, _), f in zip(shifts, frames))
    bottom = max(dy + f.shape[0] for (_, dy), f in zip(shifts, frames))
    out = []
    for (dx, dy), f in zip(shifts, frames):
        cell = np.zeros((bottom - top, right - left, 4), np.uint8)
        cell[dy - top: dy - top + f.shape[0], dx - left: dx - left + f.shape[1]] = f
        out.append(cell)
    mask = np.any([c[..., 3] > 0 for c in out], axis=0)
    ys, xs = np.where(mask)
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    return [Image.fromarray(c, "RGBA") for c in ground([c[y0:y1, x0:x1] for c in out])]


def body_bottom(frame):
    """The lowest row that is still part of the main body, ignoring small debris below it."""
    rows = (frame[..., 3] > 0).sum(axis=1)
    return int(np.where(rows >= rows.max() * 0.35)[0].max())


def ground(frames):
    """Puts every hit stage on the intact frame's ground line without changing the shared cell size."""
    target = body_bottom(frames[0])
    out = [frames[0]]
    for frame in frames[1:]:
        ys = np.where((frame[..., 3] > 0).any(axis=1))[0]
        dy = int(np.clip(target - body_bottom(frame), -ys.min(), frame.shape[0] - 1 - ys.max()))
        out.append(np.roll(frame, dy, axis=0))
    return out


def uri(image, quality=90):
    buffer = io.BytesIO()
    image.save(buffer, "WEBP", quality=quality, alpha_quality=95, method=5)
    return "data:image/webp;base64," + base64.b64encode(buffer.getvalue()).decode("ascii")


def fit(image, max_height=PREVIEW_MAX_HEIGHT):
    if image.height <= max_height:
        return image
    return image.resize((round(image.width * max_height / image.height), max_height), Image.LANCZOS)


def main():
    FRAMES.mkdir(exist_ok=True)
    assets = {}
    for asset_id, source, label, theme in PROPS:
        frames = align(split_columns(load_clean(DOWNLOADS / f"{source}.png")))
        for index, frame in enumerate(frames, 1):
            frame.save(FRAMES / f"{asset_id}-{index}.png", optimize=True)
        assets[asset_id] = {"label": label, "theme": theme, "kind": "prop",
                            "aspect": round(frames[0].width / frames[0].height, 4),
                            "frames": [uri(fit(f)) for f in frames]}
    for asset_id, source, label, theme in CHARACTERS:
        image = Image.fromarray(load_clean(ARTWORK / f"{source}.png"), "RGBA")
        image = image.crop(image.getchannel("A").getbbox())
        assets[asset_id] = {"label": label, "theme": theme, "kind": "char",
                            "aspect": round(image.width / image.height, 4), "frames": [uri(fit(image))]}

    maps = {}
    for theme, door, source in MAPS:
        image = Image.open(DOWNLOADS / f"{source}.png").convert("RGBA").resize((1670, 941), Image.LANCZOS)
        maps[f"{theme}/{door}"] = uri(image, 88)

    cx, cy = VIEW_W / 2, VIEW_H / 2
    data = {
        "assets": assets, "maps": maps,
        "layout": json.loads((HERE / "layout-v1.json").read_text(encoding="utf-8")),
        "view": {"w": VIEW_W, "h": VIEW_H, "ppu": PX_PER_UNIT},
        "mapRect": {"x": round(cx - MAP_CENTER[0] * MAP_SCALE, 2), "y": round(cy - MAP_CENTER[1] * MAP_SCALE, 2),
                    "w": round(1670 * MAP_SCALE, 2), "h": round(941 * MAP_SCALE, 2)},
        "floor": {"l": cx - FLOOR_HALF_UNITS[0] * PX_PER_UNIT, "t": cy - FLOOR_HALF_UNITS[1] * PX_PER_UNIT,
                  "r": cx + FLOOR_HALF_UNITS[0] * PX_PER_UNIT, "b": cy + FLOOR_HALF_UNITS[1] * PX_PER_UNIT},
    }
    template = (HERE / "template.html").read_text(encoding="utf-8")
    payload = json.dumps(data, ensure_ascii=False, separators=(",", ":")).replace("</", "<\\/")
    output = HERE / "preview.html"
    output.write_text(template.replace("__SHOP_DECOR_DATA__", payload), encoding="utf-8")
    print(f"preview.html {output.stat().st_size / 1048576:.1f} MB, {len(assets)} assets, {len(maps)} maps")
    for asset_id, asset in assets.items():
        print(asset_id, "aspect", asset["aspect"], "frames", len(asset["frames"]))


if __name__ == "__main__":
    main()
