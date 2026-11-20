# Splits the user's 2026-11-20 drawings of 에르핀 with 시스트의 가짜 날개 into Unity frames:
#  - the 4x3 flight sheet: row 1 front (Down), row 2 side facing left (Side), row 3 back (Up), four frames each.
#    Every frame keeps its place in its cell so the wing beat stays a bob; each row is shifted so its lowest feet
#    sit on one shared ground line and its frames are centred together, and all twelve share one canvas.
#  - the standing drawing: shown while she hovers in place. Cropped to the drawing and scaled to the frames' size.
# The sheet's background carries faint red speckles (alpha < 32); they are cleared before cropping.
# Scale and ground line are set in Unity by ErpinFlightArtworkSetup.
# Usage: python scripts/split-erpin-flight.py <flight sheet> <standing drawing>
import shutil
import sys
from pathlib import Path

from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[1] / 'game' / 'Assets'
OUT = ROOT / 'Resources' / 'Characters' / 'Erpin_Flying'
SOURCE = ROOT / 'Art' / 'Characters' / 'Player' / 'Flight'
ALPHA = 32
DIRECTIONS = ['Down', 'Side', 'Up']
XS = [0, 373, 733, 1093, 1448]
YS = [0, 347, 701, 1086]
PAD = 6


def clean(image):
    # Faint pixels stay only as the outline's soft edge: next to the solid drawing and not red speckle.
    solid = image.getchannel('A').point(lambda a: 255 if a >= 128 else 0).filter(ImageFilter.MaxFilter(7))
    near = solid.load()
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            if 0 < a < ALPHA and (not near[x, y] or (r > g + 60 and r > b + 60)):
                pixels[x, y] = (0, 0, 0, 0)
    return image


def visible(image):
    box = image.getchannel('A').point(lambda a: 255 if a >= ALPHA else 0).getbbox()
    if box is None:
        raise SystemExit('empty drawing')
    return box


def split_sheet(path):
    sheet = clean(Image.open(path).convert('RGBA'))
    if sheet.size != (XS[-1], YS[-1]):
        raise SystemExit(f'{path}: expected {XS[-1]}x{YS[-1]}, got {sheet.size}')
    rows = []
    for r in range(3):
        cells = [sheet.crop((XS[c], YS[r], XS[c + 1], YS[r + 1])) for c in range(4)]
        boxes = [visible(cell) for cell in cells]
        rows.append((cells, boxes))
    # Each row's union box; frames are placed relative to it, so the bob between frames survives.
    unions = [(min(b[0] for b in boxes), min(b[1] for b in boxes), max(b[2] for b in boxes), max(b[3] for b in boxes))
              for _, boxes in rows]
    width = max(u[2] - u[0] for u in unions) + PAD * 2
    height = max(u[3] - u[1] for u in unions) + PAD * 2
    OUT.mkdir(parents=True, exist_ok=True)
    for r, (cells, boxes) in enumerate(rows):
        union = unions[r]
        left = (width - (union[2] - union[0])) // 2 - union[0]
        top = height - PAD - union[3]
        for c, (cell, box) in enumerate(zip(cells, boxes)):
            canvas = Image.new('RGBA', (width, height), (0, 0, 0, 0))
            piece = cell.crop(box)
            canvas.paste(piece, (box[0] + left, box[1] + top))
            canvas.save(OUT / f'Erpin_Fly_{DIRECTIONS[r]}_{c}.png')
    SOURCE.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(path, SOURCE / 'Erpin_Flight_Sheet.png')
    print(f'flight: 12 frames at {width}x{height}')
    return height


def split_standing(path, frame_height):
    image = clean(Image.open(path).convert('RGBA'))
    piece = image.crop(visible(image))
    # About as many pixels as a flight frame, so both read equally sharp without mipmaps.
    scale = (frame_height - PAD * 2) / piece.height
    piece = piece.resize((round(piece.width * scale), round(piece.height * scale)), Image.LANCZOS)
    canvas = Image.new('RGBA', (piece.width + PAD * 2, piece.height + PAD * 2), (0, 0, 0, 0))
    canvas.paste(piece, (PAD, PAD))
    canvas.save(OUT / 'Erpin_FlyIdle.png')
    shutil.copyfile(path, SOURCE / 'Erpin_FlyIdle_Source.png')
    print(f'standing: {canvas.width}x{canvas.height}')


if __name__ == '__main__':
    if len(sys.argv) != 3:
        raise SystemExit('usage: <flight sheet> <standing drawing>')
    split_standing(sys.argv[2], split_sheet(sys.argv[1]))
