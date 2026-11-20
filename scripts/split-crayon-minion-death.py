# Splits the user's crayon minion defeat sheets (2026-11-11: 마법사, 도끼병, 궁병; 2026-11-19: 방패병) into Unity frames.
# A staff, arrow or broken-off half can reach into the next frame's columns, so the sheet is not cut at column
# gaps: each large connected drawing piece goes to the quarter of the sheet that holds its centre, and each small
# mark (motion lines, sparks) to the frame of the large piece nearest to it, since marks can sit on the quarter line. Every frame is
# centred on one shared canvas and keeps the sheet's ground line, like the other defeat frames (the bottom-centre
# pivot is set by EnemyDefeatArtworkSetup).
# Usage: python scripts/split-crayon-minion-death.py <mage sheet> <axe sheet> <archer sheet>
#    or: python scripts/split-crayon-minion-death.py CrayonShield=<shield sheet> [Name=<sheet> ...]
import shutil
import sys
from pathlib import Path

from PIL import Image

DEATH = Path(__file__).resolve().parents[1] / 'game' / 'Assets' / 'Art' / 'Enemies' / 'FairyKingdom' / 'Death'
NAMES = ('CrayonMage', 'CrayonAxe', 'CrayonArcher', 'CrayonShield')
ALPHA = 16
FRAMES = 4
# Pixels; below this a piece is a mark that follows its nearest large piece.
MARK_AREA = 12000


def components(alpha):
    w, h = alpha.size
    px = alpha.load()
    label = [[0] * w for _ in range(h)]
    pieces = []
    for y in range(h):
        for x in range(w):
            if px[x, y] <= ALPHA or label[y][x]:
                continue
            n = len(pieces) + 1
            label[y][x] = n
            stack = [(x, y)]
            x0 = x1 = x
            y0 = y1 = y
            total = sx = 0
            while stack:
                cx, cy = stack.pop()
                total += 1
                sx += cx
                x0, x1, y0, y1 = min(x0, cx), max(x1, cx), min(y0, cy), max(y1, cy)
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if 0 <= nx < w and 0 <= ny < h and not label[ny][nx] and px[nx, ny] > ALPHA:
                        label[ny][nx] = n
                        stack.append((nx, ny))
            pieces.append((n, sx / total, (x0, y0, x1 + 1, y1 + 1), total))
    return label, pieces


def gap(a, b):
    dx = max(a[0] - b[2], b[0] - a[2], 0)
    dy = max(a[1] - b[3], b[1] - a[3], 0)
    return dx * dx + dy * dy


def split(name, sheet_path):
    sheet = Image.open(sheet_path).convert('RGBA')
    w, h = sheet.size
    label, pieces = components(sheet.split()[3])
    large = [p for p in pieces if p[3] >= MARK_AREA]
    slot = {n: min(FRAMES - 1, int(cx * FRAMES / w)) for n, cx, _, _ in large}
    for n, _, box, area in pieces:
        if area < MARK_AREA:
            slot[n] = slot[min(large, key=lambda p: gap(box, p[2]))[0]]
    layers = []
    for i in range(FRAMES):
        layer = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        src, dst = sheet.load(), layer.load()
        ids = {n for n, s in slot.items() if s == i}
        for y in range(h):
            row = label[y]
            for x in range(w):
                if row[x] in ids:
                    dst[x, y] = src[x, y]
        box = layer.getbbox()
        if box is None:
            raise SystemExit(f'{name}: frame {i} is empty')
        layers.append((layer, box))
    top = min(b[1] for _, b in layers) - 4
    bottom = max(b[3] for _, b in layers) + 4
    width = max(b[2] - b[0] for _, b in layers) + 8
    for i, (layer, (x0, _, x1, _)) in enumerate(layers):
        canvas = Image.new('RGBA', (width, bottom - top), (0, 0, 0, 0))
        canvas.paste(layer.crop((x0, top, x1, bottom)), ((width - (x1 - x0)) // 2, 0))
        canvas.save(DEATH / f'{name}_Death_{i}.png')
    shutil.copyfile(sheet_path, DEATH / 'Source' / f'{name}_Death_Sheet.png')
    print(f'{name}: 4 frames at {width}x{bottom - top}, {len(large)} large pieces, {len(pieces) - len(large)} marks')


if __name__ == '__main__':
    args = sys.argv[1:]
    if args and all('=' in a for a in args):
        jobs = [a.split('=', 1) for a in args]
        unknown = [n for n, _ in jobs if n not in NAMES]
        if unknown:
            raise SystemExit(f'unknown names {unknown}; expected one of {NAMES}')
    elif len(args) == 3:
        jobs = list(zip(NAMES[:3], args))
    else:
        raise SystemExit('usage: <mage sheet> <axe sheet> <archer sheet> | Name=<sheet> ...')
    for name, path in jobs:
        split(name, path)
