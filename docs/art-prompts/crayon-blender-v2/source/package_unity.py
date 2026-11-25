"""Pack export_unity.py output into the two game textures and fail on clipped or empty artwork (Boss-Pattern-1)."""
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[4]
SOURCE = ROOT / 'output' / 'crayon-blender-v2' / 'unity'
TARGET = ROOT / 'game' / 'Assets' / 'Resources' / 'CombatEffects'
COLUMNS, ROWS = 7, 4
FRAME_COUNT = 26  # The last two cells of the sheet stay empty.


def check_inside(image, label):
    box = image.getchannel('A').point(lambda value: 255 if value > 8 else 0).getbbox()
    if box is None:
        return None
    if box[0] <= 0 or box[1] <= 0 or box[2] >= image.width or box[3] >= image.height:
        raise SystemExit(f'{label} touches its cell border: {box}')
    return box


export = json.loads((SOURCE / 'export.json').read_text(encoding='utf-8'))
cell = tuple(export['cell'])
if len(export['strike_frames']) != FRAME_COUNT:
    raise SystemExit(f"Expected {FRAME_COUNT} strike frames, export lists {len(export['strike_frames'])}")
paths = [SOURCE / 'strike' / f'{index:02d}.png' for index in range(FRAME_COUNT)]

warning = Image.open(SOURCE / 'warning.png').convert('RGBA')
if warning.size != cell or check_inside(warning, 'warning') is None:
    raise SystemExit('Warning render is empty or has the wrong size')

sheet = Image.new('RGBA', (cell[0] * COLUMNS, cell[1] * ROWS), (0, 0, 0, 0))
for index, path in enumerate(paths):
    frame = Image.open(path).convert('RGBA')
    if frame.size != cell:
        raise SystemExit(f'{path.name} has the wrong size')
    box = check_inside(frame, path.name)
    if box is None:
        raise SystemExit(f'{path.name} is empty')
    sheet.paste(frame, (index % COLUMNS * cell[0], index // COLUMNS * cell[1]))

TARGET.mkdir(parents=True, exist_ok=True)
warning.save(TARGET / 'crayon-rift-warning.png', optimize=True)
sheet.save(TARGET / 'crayon-rift-strike.png', optimize=True)
print(f'CRAYON_UNITY_PACKAGE_OK warning={warning.size} strike={sheet.size}')
