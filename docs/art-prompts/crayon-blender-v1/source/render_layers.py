"""Render each independent Blender collection at peak and inspect source invariants."""
import argparse
import json
from pathlib import Path
import sys

import bpy

parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
out = args.output.resolve() / 'layers'
out.mkdir(parents=True, exist_ok=True)
source = Path(__file__).resolve().with_name('crayon-slash.blend')
bpy.ops.wm.open_mainfile(filepath=str(source))
scene = bpy.context.scene
collections = [c for c in bpy.data.collections if c.name[:2].isdigit()]
assert len(collections) == 7
assert len([o for o in bpy.data.objects if o.type == 'CAMERA']) == 1
assert not any(i.source == 'FILE' for i in bpy.data.images), 'No external texture dependency expected'
assert scene.render.fps == 40 and scene.frame_end == 56
assert scene.render.film_transparent
names = []
for col in sorted(collections, key=lambda item: item.name):
    for other in collections:
        other.hide_render = other != col
    scene.frame_set(8 if col.name.startswith('01') else 21)
    scene.render.filepath = str(out / f'{col.name[:2]}.png')
    bpy.ops.render.render(write_still=True)
    names.append({'collection': col.name, 'file': f'{col.name[:2]}.png',
                  'frame': scene.frame_current, 'objects': len(col.objects)})
(out / 'layers.json').write_text(json.dumps(names, indent=2)+'\n', encoding='utf-8')
print('PASS: seven independent layer renders; fixed camera, alpha, fps; no external image dependency')
