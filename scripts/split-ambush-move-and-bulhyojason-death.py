# Splits the user's two four-frame sheets (2026-11-05) into Unity frames.
#   - 잠복형 이동 (moving mound): each frame on the 509x440 canvas of BulhyojasonCold_Emerge_0, as wide as that
#     mound and on its bottom line, so the mound does not jump between the still and moving drawings.
#   - 불효자손 쓰러짐 (defeat): each frame centred on one shared canvas, keeping the sheet's ground line, like the
#     other defeat frames (the bottom-centre pivot is set by EnemyDefeatArtworkSetup).
# Usage: python scripts/split-ambush-move-and-bulhyojason-death.py <move sheet> <defeat sheet>
import shutil
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1] / 'game' / 'Assets' / 'Art' / 'Enemies' / 'FairyKingdom'
AMBUSH = ROOT / 'Ambush'
DEATH = ROOT / 'Death'
ALPHA = 16


def mask(image):
    return image.split()[3].point(lambda v: 255 if v > ALPHA else 0)


def column_runs(image, count):
    a = mask(image)
    w, h = image.size
    filled = [a.crop((x, 0, x + 1, h)).getbbox() is not None for x in range(w)]
    runs, start = [], None
    for x, value in enumerate(filled + [False]):
        if value and start is None:
            start = x
        if not value and start is not None:
            if x - start > 3:
                runs.append((start, x))
            start = None
    if len(runs) != count:
        raise SystemExit(f'expected {count} frames, found {len(runs)}: {runs}')
    return runs


def split_move(sheet_path):
    sheet = Image.open(sheet_path).convert('RGBA')
    emerge = Image.open(AMBUSH / 'BulhyojasonCold_Emerge_0.png').convert('RGBA')
    ex0, _, ex1, ey1 = mask(emerge).getbbox()
    runs = column_runs(sheet, 4)
    rows = [mask(sheet.crop((x0, 0, x1, sheet.height))).getbbox() for x0, x1 in runs]
    scale = (ex1 - ex0) / (runs[0][1] - runs[0][0])
    bottom = rows[0][3]
    w, h = emerge.size
    for i, (x0, _) in enumerate(runs):
        left = x0 - ex0 / scale
        top = bottom - ey1 / scale
        box = (round(left), round(top), round(left + w / scale), round(top + h / scale))
        frame = sheet.crop(box).resize((w, h), Image.Resampling.LANCZOS)
        frame.save(AMBUSH / f'BulhyojasonCold_Move_{i}.png')
    source = AMBUSH / 'Source'
    source.mkdir(exist_ok=True)
    shutil.copyfile(sheet_path, source / 'BulhyojasonCold_Move_Sheet.png')
    print(f'move: 4 frames at {w}x{h}, scale {scale:.4f}')


def split_death(sheet_path):
    sheet = Image.open(sheet_path).convert('RGBA')
    runs = column_runs(sheet, 4)
    boxes = [mask(sheet.crop((x0, 0, x1, sheet.height))).getbbox() for x0, x1 in runs]
    top = min(b[1] for b in boxes) - 4
    bottom = max(b[3] for b in boxes) + 4
    width = max(x1 - x0 for x0, x1 in runs) + 8
    for i, (x0, x1) in enumerate(runs):
        canvas = Image.new('RGBA', (width, bottom - top), (0, 0, 0, 0))
        piece = sheet.crop((x0, top, x1, bottom))
        canvas.paste(piece, ((width - (x1 - x0)) // 2, 0))
        canvas.save(DEATH / f'Bulhyojason_Death_{i}.png')
    shutil.copyfile(sheet_path, DEATH / 'Source' / 'Bulhyojason_Death_Sheet.png')
    print(f'death: 4 frames at {width}x{bottom - top}')


if __name__ == '__main__':
    if len(sys.argv) != 3:
        raise SystemExit(__doc__ if __doc__ else 'usage: <move sheet> <defeat sheet>')
    split_move(sys.argv[1])
    split_death(sys.argv[2])
