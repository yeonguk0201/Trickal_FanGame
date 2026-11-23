"""Assemble Blender renders for review; no generated or painted effect imagery."""
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageOps

ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT / 'output/crayon-blender-v1'
KEEP = Path(__file__).resolve().parent.parent
manifest = json.loads((OUT / 'manifest.json').read_text(encoding='utf-8'))
paths = sorted((OUT / 'frames').glob('frame-*.png'))
assert len(paths) == manifest['frames'], f'Expected {manifest["frames"]} frames, got {len(paths)}'
frames = [Image.open(p).convert('RGBA') for p in paths]
assert all(im.size == (960, 640) for im in frames)
assert frames[-1].getchannel('A').getbbox() is None, 'Last frame must be empty'
peak = Image.open(OUT / 'peak.png').convert('RGBA')
assert peak.getchannel('A').getextrema() == (0, 255)
assert not any(frame.getchannel('A').getbbox() and
               (frame.getchannel('A').getbbox()[0] == 0 or frame.getchannel('A').getbbox()[1] == 0 or
                frame.getchannel('A').getbbox()[2] == 960 or frame.getchannel('A').getbbox()[3] == 640)
               for frame in frames), 'Effect touches frame border'

floor = Image.open(ROOT / 'game/Assets/Rooms/Artwork/FairyVillage/floor.png').convert('RGBA')
# Interior of an existing dirt tile, used only as a scale/background reference.
tile = floor.crop((218, 495, 350, 627)).resize((160, 160), Image.Resampling.LANCZOS)
background = Image.new('RGBA', (960, 640), '#82735b')
for y in range(0, 640, 160):
    for x in range(0, 960, 160):
        background.alpha_composite(tile, (x, y))
character = Image.open(ROOT / 'game/Assets/Resources/Characters/Erpin_Walking.png').convert('RGBA')
character = character.crop((0, 0, 362, 362)).resize((125, 125), Image.Resampling.LANCZOS)
background.alpha_composite(character, (265, 250))
draw = ImageDraw.Draw(background)
draw.rounded_rectangle((16, 16, 520, 54), radius=8, fill=(24, 26, 30, 210))
draw.text((30, 30), 'BLENDER v1  /  SCALE REFERENCE COMPOSITE  /  NOT A UNITY CAPTURE', fill='white')


def composite(frame, bg):
    result = bg.copy()
    result.alpha_composite(frame)
    return result.convert('RGB')


dark = Image.new('RGBA', (960, 640), '#14171f')
review = [composite(frame, background) for frame in frames]
solo = [composite(frame, dark) for frame in frames]
# GIF durations use centiseconds; alternate 20/30 ms to preserve 40 fps average.
durations = [20 if i % 2 == 0 else 30 for i in range(len(frames))]
for name, sequence, speed in (('scene-preview.gif', review, 1), ('effect-preview.gif', solo, 1),
                               ('slow-preview.gif', review, 3)):
    sequence[0].save(OUT / name, save_all=True, append_images=sequence[1:], loop=0,
                     duration=[d*speed for d in durations], disposal=2, optimize=False)

composite(peak, background).save(OUT / 'peak-in-scene.png')
composite(peak, dark).save(OUT / 'peak-on-dark.png')
# Fixed grid; temporal samples have unequal spacing to show the important stages.
selected = (8, 17, 19, 21, 25, 30, 36, 42)
sheet = Image.new('RGB', (1920, 704), '#14171f')
sd = ImageDraw.Draw(sheet)
for i, f in enumerate(selected):
    cell = solo[f-1].resize((480, 320), Image.Resampling.LANCZOS)
    x, y = i % 4 * 480, i // 4 * 352
    sheet.paste(cell, (x, y+32))
    sd.text((x+16, y+12), f'{i+1:02d}   FRAME {f:02d}   {(f-1)/40:.3f}s', fill='#c6d4e7')
sheet.save(OUT / 'contact-sheet.png')
rgba_sheet = Image.new('RGBA', (3840, 1280))
for i, f in enumerate(selected):
    rgba_sheet.alpha_composite(frames[f-1], ((i % 4)*960, (i // 4)*640))
rgba_sheet.save(OUT / 'sample-sheet-rgba.png')
(OUT / 'sample-sheet-timing.json').write_text(json.dumps({
    'purpose': 'Uneven temporal samples for visual comparison, not a fixed-fps runtime sheet',
    'columns': 4, 'rows': 2, 'cell': [960, 640], 'source_fps': 40,
    'source_frames': list(selected), 'source_seconds': [(f-1)/40 for f in selected],
}, indent=2)+'\n', encoding='utf-8')
# Preserve a compact review and transparent peak alongside the editable source.
peak.save(KEEP / 'peak-transparent.png')
sheet.save(KEEP / 'contact-sheet.png')
layer_sheet = Image.new('RGB', (1920, 704), '#14171f')
ld = ImageDraw.Draw(layer_sheet)
layer_data = json.loads((OUT / 'layer-review/layers/layers.json').read_text(encoding='utf-8'))
for i, item in enumerate(layer_data):
    layer = Image.open(OUT / 'layer-review/layers' / item['file']).convert('RGBA')
    assert layer.getchannel('A').getbbox(), f'Empty layer: {item["collection"]}'
    bg = Image.new('RGBA', (960, 640), '#697485') if i == 3 else dark
    cell = composite(layer, bg).resize((480, 320), Image.Resampling.LANCZOS)
    x, y = (i % 4)*480, (i // 4)*352
    layer_sheet.paste(cell, (x, y+32))
    ld.text((x+16, y+12), item['collection'], fill='#c6d4e7')
layer_sheet.save(OUT / 'layer-sheet.png')
report = {'frames': len(frames), 'size': [960, 640], 'fps': 40, 'final_frame_empty': True,
          'all_frames_inside_canvas': True, 'peak_alpha_range': [0, 255],
          'composition_only': True, 'unity_capture': False, 'art_approval': 'pending'}
(OUT / 'verification.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
print('PASS: 56 RGBA frames, fixed canvas, transparent final frame, peak alpha, no clipped borders')
