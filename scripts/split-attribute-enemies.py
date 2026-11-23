# Splits the user's attribute-enemy drawings (2026-11-23) into Unity frames for 불효자손 순수·활발·우울 and
# 산사모 냉정·우울.
#   - Every frame is scaled so the character matches its idle drawing (580 px tall, 500 px per unit = 1.16 units).
#   - Each frame sits on a canvas centred on its feet with the ground PAD px from the bottom, so swapping frames never
#     moves the feet. Heights in a sheet are kept, so the jump sheet's rise stays in the drawing.
#   - Pieces are grouped by connected component into the sheet's four cells, so a long ribbon or a slash that crosses
#     a cell boundary stays with its frame.
# Usage: python -I scripts/split-attribute-enemies.py [downloads folder]
import shutil
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1] / 'game' / 'Assets' / 'Art' / 'Enemies' / 'FairyKingdom'
OUT = ROOT / 'Attributes'
DEATH = ROOT / 'Death'
ALPHA = 16
IDLE_HEIGHT = 580
PAD = 8
GRID = 4  # components are found on a 1/4 size mask

# Key: (Korean name, frame prefix). `reference` is the frame in which the whole standing body is visible.
CHARACTERS = {
    'BulhyojasonPure': ('불효자손 순수', {'Attack': ('공격', 0)}, '패배'),
    'BulhyojasonLively': ('불효자손 활발', {'Attack': ('공격', 1)}, '패배'),
    'BulhyojasonGloomy': ('불효자손 우울', {'Attack': ('공격', 0)}, '패배'),
    'SansamoCold': ('산사모 냉정', {'Walk': ('걷기', 0), 'Attack': ('공격', 0)}, '패배'),
    'SansamoGloomy': ('산사모 우울', {'Walk': ('걷기', 0), 'Burrow': ('파묻히기', 0), 'Emerge': ('튀어나오기', 3)}, '패배'),
}


def components(alpha):
    small = alpha[::GRID, ::GRID] > ALPHA
    h, w = small.shape
    labels = np.zeros(small.shape, dtype=np.int32)
    boxes = []
    for sy, sx in zip(*np.nonzero(small)):
        if labels[sy, sx]:
            continue
        label = len(boxes) + 1
        labels[sy, sx] = label
        queue = deque([(sy, sx)])
        x0, x1, y0, y1, count, total = sx, sx, sy, sy, 0, 0
        while queue:
            y, x = queue.popleft()
            count += 1
            total += x
            x0, x1, y0, y1 = min(x0, x), max(x1, x), min(y0, y), max(y1, y)
            for ny in range(max(0, y - 2), min(h, y + 3)):
                for nx in range(max(0, x - 2), min(w, x + 3)):
                    if small[ny, nx] and not labels[ny, nx]:
                        labels[ny, nx] = label
                        queue.append((ny, nx))
        boxes.append((label, count, total / count * GRID, x0 * GRID, (x1 + 1) * GRID))
    return labels, boxes


def split_cells(path):
    sheet = np.array(Image.open(path).convert('RGBA'))
    labels, boxes = components(sheet[..., 3])
    cell = sheet.shape[1] / 4
    # The biggest piece of each cell is that frame's body; every other piece (sparks, debris) joins the nearest body,
    # because a spark drawn beside a frame can cross the cell line.
    pieces = [box for box in boxes if box[1] >= 3]
    bodies = []
    for index in range(4):
        inside = [box for box in pieces if min(3, int(box[2] // cell)) == index]
        if not inside:
            raise SystemExit(f'{path.name}: cell {index} is empty')
        bodies.append(max(inside, key=lambda box: box[1]))

    def gap(box, body):
        # Horizontal distance to the body at the piece's own height, so a spark under a neighbour's raised head goes
        # to the frame it is actually drawn beside.
        rows = np.nonzero((labels == box[0]).any(1))[0]
        columns = np.nonzero((labels[rows.min():rows.max() + 1] == body[0]).any(0))[0]
        if columns.size == 0:
            return max(body[3] - box[4], box[3] - body[4], 0) + labels.shape[1] * GRID
        return max(columns.min() * GRID - box[4], box[3] - (columns.max() + 1) * GRID, 0)

    owners = [min(range(4), key=lambda index: gap(box, bodies[index])) for box in pieces]
    frames = []
    for index in range(4):
        keep = [box[0] for box, owner in zip(pieces, owners) if owner == index]
        mask = np.isin(labels, keep).repeat(GRID, 0).repeat(GRID, 1)[:sheet.shape[0], :sheet.shape[1]]
        padded = np.zeros(sheet.shape[:2], dtype=bool)
        padded[:mask.shape[0], :mask.shape[1]] = mask
        # Grow by one coarse cell so the anti-aliased rim is not cut.
        grown = padded.copy()
        for axis, shift in ((0, GRID), (0, -GRID), (1, GRID), (1, -GRID)):
            grown |= np.roll(padded, shift, axis)
        frame = sheet.copy()
        frame[~grown] = 0
        frames.append(frame)
    return frames


def bbox(frame):
    ys, xs = np.nonzero(frame[..., 3] > ALPHA)
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1


def feet_x(frame):
    x0, y0, x1, y1 = bbox(frame)
    band = frame[y1 - max(4, (y1 - y0) // 8):y1, :, 3] > ALPHA
    return float(np.nonzero(band.any(0))[0].mean())


def place(frames, scale, ground, name, folder, anchors=None):
    pieces = []
    for i, frame in enumerate(frames):
        x0, y0, x1, y1 = bbox(frame)
        anchor = anchors[i] if anchors else feet_x(frame)
        pieces.append((frame[y0:y1, x0:x1], (x0 - anchor) * scale, (ground - y1) * scale))
    half = max(max(-left, left + p.shape[1] * scale) for p, left, _ in pieces)
    top = max(lift + p.shape[0] * scale for p, _, lift in pieces)
    width, height = int(np.ceil(half)) * 2 + PAD * 2, int(np.ceil(top)) + PAD * 2
    for i, (piece, left, lift) in enumerate(pieces):
        w, h = max(1, round(piece.shape[1] * scale)), max(1, round(piece.shape[0] * scale))
        image = Image.fromarray(piece, 'RGBA').resize((w, h), Image.Resampling.LANCZOS)
        canvas = Image.new('RGBA', (width, height), (0, 0, 0, 0))
        canvas.paste(image, (round(width / 2 + left), round(height - PAD - lift - h)))
        canvas.save(folder / f'{name}_{i}.png')
    return width, height


def main():
    downloads = Path(sys.argv[1]) if len(sys.argv) > 1 else Path.home() / 'Downloads'
    OUT.mkdir(exist_ok=True)
    source = OUT / 'Source'
    source.mkdir(exist_ok=True)
    for key, (korean, sheets, defeat) in CHARACTERS.items():
        idle = np.array(Image.open(downloads / f'{korean}.png').convert('RGBA'))
        x0, y0, x1, y1 = bbox(idle)
        scale = IDLE_HEIGHT / (y1 - y0)
        width, height = place([idle], scale, y1, f'{key}_Idle', OUT)
        (OUT / f'{key}_Idle_0.png').replace(OUT / f'{key}_Idle.png')
        print(key, 'idle', width, height)
        for frame_name, (sheet_name, reference) in sheets.items():
            path = downloads / f'{korean} {sheet_name}.png'
            frames = split_cells(path)
            _, top, _, bottom = bbox(frames[reference])
            sheet_scale = IDLE_HEIGHT / (bottom - top)
            ground = max(bbox(frame)[3] for frame in frames)
            size = place(frames, sheet_scale, ground, f'{key}_{frame_name}', OUT)
            shutil.copyfile(path, source / f'{key}_{frame_name}_Sheet.png')
            print(key, frame_name, size, f'scale {sheet_scale:.3f}')
        path = downloads / f'{korean} {defeat}.png'
        frames = split_cells(path)
        ground = max(bbox(frame)[3] for frame in frames)
        place(frames, 1.0, ground, f'{key}_Death', DEATH, [(bbox(f)[0] + bbox(f)[2]) / 2 for f in frames])
        shutil.copyfile(path, source / f'{key}_Death_Sheet.png')


if __name__ == '__main__':
    main()
