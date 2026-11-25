"""Render the game assets from crayon-slash.blend without saving it (Boss-Pattern-1).

The slash is laid flat (0 degrees) so Unity can rotate it. The warning and the strike are separate renders because
the game places, times and fades them independently. Both use one world scale: a cell is 8 x 8/3 Blender units.
"""
import argparse
import json
from pathlib import Path
import sys

import bpy

# Every layer has faded out by source frame 43.
STRIKE_FRAMES = range(17, 43)
WARNING_FRAME = 8
CELL = (480, 160)
WORLD_WIDTH = 8.0

parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
out = args.output.resolve()
(out / 'strike').mkdir(parents=True, exist_ok=True)

bpy.ops.wm.open_mainfile(filepath=str(Path(__file__).resolve().with_name('crayon-slash.blend')))
scene = bpy.context.scene
bpy.data.objects['Slash direction - editable angle'].rotation_euler.z = 0
scene.render.resolution_x, scene.render.resolution_y = CELL
scene.camera.data.ortho_scale = WORLD_WIDTH
collections = [c for c in bpy.data.collections if c.name[:2].isdigit()]
assert len(collections) == 8


def show(warning):
    for collection in collections:
        collection.hide_render = collection.name.startswith('01') != warning


show(True)
scene.frame_set(WARNING_FRAME)
scene.render.filepath = str(out / 'warning.png')
bpy.ops.render.render(write_still=True)

show(False)
for index, frame in enumerate(STRIKE_FRAMES):
    scene.frame_set(frame)
    scene.render.filepath = str(out / 'strike' / f'{index:02d}.png')
    bpy.ops.render.render(write_still=True)

(out / 'export.json').write_text(json.dumps({
    'cell': CELL, 'world_width': WORLD_WIDTH, 'fps': scene.render.fps,
    'warning_frame': WARNING_FRAME, 'strike_frames': list(STRIKE_FRAMES),
}, indent=2) + '\n', encoding='utf-8')
print('CRAYON_UNITY_EXPORT_OK')
