# Splits the user's four-frame sheets into Unity frames (2026-11-06).
#   walk   - 곡사형 걷기 (저혈당 요정-광기): equal cells, shifted into the gaps between drawings when a drawing
#            crosses an even cut, so every frame keeps the sheet's ground line and horizontal place.
#            Defeat1MotionSetup sizes them from frame 0.
#   crouch - 산사모-광기 자폭 예고 웅크리기: equal cells the same way (SelfDestructCrouch sizes them from frame 0).
#   death  - 잠복형 쓰러짐 (불효자손-냉정): cells shifted so frame 0's drawing is centred (it stands where the
#            surfaced mound stood), trimmed to the drawings' height like the other defeat frames (bottom-centre
#            pivot is set by EnemyDefeatArtworkSetup).
# Usage: python scripts/split-lobbing-walk-and-ambush-death.py walk|crouch|death <sheet>
import shutil
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1] / 'game' / 'Assets' / 'Art' / 'Enemies' / 'FairyKingdom'
WALK = ROOT / 'Walking'
ATTACK = ROOT / 'Attacking'
DEATH = ROOT / 'Death'
ALPHA = 16
COUNT = 4


def mask(image):
    return image.split()[3].point(lambda v: 255 if v > ALPHA else 0)


def cells_fit(sheet, offset):
    """True when every cell's drawing lies inside the cell, so the frames do not cut each other."""
    cell = sheet.width / COUNT
    a = mask(sheet)
    for i in range(COUNT):
        left, right = round(i * cell + offset), round((i + 1) * cell + offset)
        for x in (left - 1, right):
            if 0 <= x < sheet.width and a.crop((x, 0, x + 1, sheet.height)).getbbox() is not None:
                return False
    return True


def fitting_offset(sheet):
    """The smallest shift of the even cuts that lands every cut in a gap between drawings."""
    for distance in range(0, round(sheet.width / COUNT / 4)):
        for offset in (distance, -distance):
            if cells_fit(sheet, offset):
                return offset
    raise SystemExit('no even cut fits between the drawings')


def split_cells(sheet_path, folder, name):
    sheet = Image.open(sheet_path).convert('RGBA')
    cell = sheet.width / COUNT
    offset = fitting_offset(sheet)
    for i in range(COUNT):
        left = round(i * cell + offset)
        # Past the sheet's edges the crop is transparent.
        sheet.crop((left, 0, left + round(cell), sheet.height)).save(folder / f'{name}_{i}.png')
    source = folder / 'Source'
    source.mkdir(exist_ok=True)
    shutil.copyfile(sheet_path, source / f'{name}_Sheet.png')
    print(f'{name}: 4 frames at {round(cell)}x{sheet.height}, shifted {offset}px')


def split_death(sheet_path):
    sheet = Image.open(sheet_path).convert('RGBA')
    cell = sheet.width / COUNT
    first = mask(sheet.crop((0, 0, round(cell), sheet.height))).getbbox()
    offset = round((first[0] + first[2]) / 2 - cell / 2)
    if not cells_fit(sheet, offset):
        raise SystemExit('a drawing crosses its cell edge')
    boxes = [mask(sheet.crop((round(i * cell + offset), 0, round((i + 1) * cell + offset), sheet.height))).getbbox()
             for i in range(COUNT)]
    top = max(0, min(b[1] for b in boxes) - 4)
    bottom = max(b[3] for b in boxes) + 4
    for i in range(COUNT):
        left = round(i * cell + offset)
        sheet.crop((left, top, left + round(cell), bottom)).save(DEATH / f'BulhyojasonCold_Death_{i}.png')
    shutil.copyfile(sheet_path, DEATH / 'Source' / 'BulhyojasonCold_Death_Sheet.png')
    print(f'death: 4 frames at {round(cell)}x{bottom - top}, shifted {offset}px')


if __name__ == '__main__':
    if len(sys.argv) != 3 or sys.argv[1] not in ('walk', 'crouch', 'death'):
        raise SystemExit('usage: walk|crouch|death <sheet>')
    kind, path = sys.argv[1], sys.argv[2]
    if kind == 'walk':
        split_cells(path, WALK, 'LowBloodSugarMadness_Walk')
    elif kind == 'crouch':
        split_cells(path, ATTACK, 'SansamoMadness_Crouch')
    else:
        split_death(path)
