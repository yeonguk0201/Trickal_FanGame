# Splits the user's 2026-11-19 drawings into Unity frames:
#  - 황금 용사 트럭 쓰러짐: four two-frame sheets (crash 1~4) and the two-frame truck sheet (5). Each half is cropped
#    to its drawing. The crash frames are saved in play order (CrayonHero_TruckImpact_0~7); where each one sits
#    against the truck comes from CrayonHeroTruckDefeatSetup (tuned in output/crayon-hero-truck-defeat).
#  - 에르핀 승리: the 3x2 jump grid. Every frame keeps its place in its cell on one shared canvas, so the jump
#    stays a jump; the second row is shifted so both rows' standing frames share one ground line.
# Usage: python scripts/split-truck-defeat-and-erpin-victory.py <crash1> <crash2> <crash3> <crash4> <truck5> <erpin grid>
import shutil
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1] / 'game' / 'Assets'
DEATH = ROOT / 'Art' / 'Bosses' / 'FairyKingdom' / 'Death'
VICTORY = ROOT / 'Resources' / 'Characters' / 'Erpin_Victory'
ERPIN_SOURCE = ROOT / 'Art' / 'Characters' / 'Player' / 'Victory'
ALPHA = 8
# Play order of the crash halves: (sheet 1~4, 0 = left / 1 = right). Must match CrayonHeroTruckDefeatSetup.
IMPACT_ORDER = [(1, 0), (1, 1), (2, 0), (3, 0), (3, 1), (2, 1), (4, 0), (4, 1)]


def halves(path):
    sheet = Image.open(path).convert('RGBA')
    half = sheet.width // 2
    out = []
    for x0, x1 in ((0, half), (half, sheet.width)):
        part = sheet.crop((x0, 0, x1, sheet.height))
        box = part.getchannel('A').point(lambda a: 255 if a > ALPHA else 0).getbbox()
        if box is None:
            raise SystemExit(f'{path}: empty half')
        out.append(part.crop(box))
    return out


def split_truck(crash_paths, truck_path):
    crash = {i + 1: halves(p) for i, p in enumerate(crash_paths)}
    for index, (sheet, side) in enumerate(IMPACT_ORDER):
        crash[sheet][side].save(DEATH / f'CrayonHero_TruckImpact_{index}.png')
    for index, frame in enumerate(halves(truck_path)):
        frame.save(DEATH / f'CrayonHero_TruckDrive_{index}.png')
    for i, p in enumerate(list(crash_paths) + [truck_path]):
        shutil.copyfile(p, DEATH / 'Source' / f'CrayonHero_Truck_Sheet_{i + 1}.png')
    print('truck: 8 impact frames, 2 drive frames')


def split_erpin(grid_path):
    grid = Image.open(grid_path).convert('RGBA')
    xs, ys = [0, 420, 830, grid.width], [0, 356, grid.height]
    cells = [grid.crop((xs[c], ys[r], xs[c + 1], ys[r + 1])) for r in range(2) for c in range(3)]
    boxes = [c.getchannel('A').point(lambda a: 255 if a > ALPHA else 0).getbbox() for c in cells]
    top = min(b[1] for b in boxes) - 4
    width = max(b[2] - b[0] for b in boxes) + 8
    grounds = [b[3] for b in boxes]
    bottom = max(grounds) + 4
    VICTORY.mkdir(parents=True, exist_ok=True)
    ERPIN_SOURCE.mkdir(parents=True, exist_ok=True)
    for i, (cell, box) in enumerate(zip(cells, boxes)):
        row = i // 3
        shift = max(grounds[:3]) - max(grounds[row * 3:row * 3 + 3])
        canvas = Image.new('RGBA', (width, bottom - top + 40), (0, 0, 0, 0))
        piece = cell.crop(box)
        canvas.paste(piece, ((width - piece.width) // 2, box[1] - top + shift))
        canvas.save(VICTORY / f'Erpin_Victory_{i}.png')
    shutil.copyfile(grid_path, ERPIN_SOURCE / 'Erpin_Victory_Sheet.png')
    print(f'erpin: 6 victory frames at {width}x{bottom - top + 40}')


if __name__ == '__main__':
    if len(sys.argv) != 7:
        raise SystemExit('usage: <crash1> <crash2> <crash3> <crash4> <truck5> <erpin grid>')
    split_truck(sys.argv[1:5], sys.argv[5])
    split_erpin(sys.argv[6])
