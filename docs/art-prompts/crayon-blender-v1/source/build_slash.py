"""Build an editable, layered silver slash in Blender; no reference-image texture."""
import argparse
import json
import math
import os
from pathlib import Path
import random
import sys

import bpy

parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
OUT = args.output.resolve()
OUT.mkdir(parents=True, exist_ok=True)
SOURCE = Path(__file__).resolve().parent
rng = random.Random(231123)
FPS = 40
FRAMES = 56

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 64
scene.cycles.transparent_max_bounces = 64
scene.render.resolution_x = 960
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.render.film_transparent = True
scene.render.fps = FPS
scene.frame_start, scene.frame_end = 1, FRAMES
scene.view_settings.view_transform = 'Standard'
scene.world.color = (0, 0, 0)

root = bpy.data.objects.new('Slash direction - editable angle', None)
scene.collection.objects.link(root)
root.rotation_euler.z = math.radians(-36)
layers = {}
for name in ('01 Small red warning', '02 Silver atmosphere', '03 Torn silver blades',
             '04 Black incision', '05 White cutting edges', '06 Impact glint', '07 Flying splinters'):
    col = bpy.data.collections.new(name)
    scene.collection.children.link(col)
    layers[name] = col


def keyed(socket, keys):
    for frame, value in keys:
        socket.default_value = value
        socket.keyframe_insert(data_path='default_value', frame=frame)


def material(name, color, strength, opacity=1, warning=False, noise=False, flash=False, particle=False, traveling=False):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    n, links = mat.node_tree.nodes, mat.node_tree.links
    n.clear()
    def node(kind, label):
        item = n.new(kind)
        item.label = label
        return item
    def math_node(operation, a, b):
        item = node('ShaderNodeMath', operation)
        item.operation = operation
        for i, value in enumerate((a, b)):
            if isinstance(value, (int, float)):
                item.inputs[i].default_value = value
            else:
                links.new(value, item.inputs[i])
        return item.outputs[0]
    attr = node('ShaderNodeAttribute', 'Feathered ribbon alpha')
    attr.attribute_name = 'coverage'
    path = node('ShaderNodeAttribute', 'Position along cutting path')
    path.attribute_name = 'cut_position'
    reveal = node('ShaderNodeValue', 'Cut front travels left to right')
    keyed(reveal.outputs[0], [(1, -4.3), (16, -4.3), (19, 4.3), (FRAMES, 4.3)])
    front = math_node('LESS_THAN', path.outputs['Fac'], reveal.outputs[0])
    envelope = node('ShaderNodeValue', 'Layer life envelope')
    if warning:
        keys = [(1, 0), (4, opacity), (14, opacity), (16, 0), (FRAMES, 0)]
    elif traveling:
        keys = [(1, 0), (16, 0), (17, opacity), (18, opacity), (19, 0), (FRAMES, 0)]
    elif flash:
        keys = [(1, 0), (18, 0), (20, opacity), (23, opacity*.3), (27, 0), (FRAMES, 0)]
    elif particle:
        keys = [(1, 0), (18, 0), (21, opacity), (29, opacity*.7), (40, 0), (FRAMES, 0)]
    else:
        keys = [(1, 0), (16, opacity), (23, opacity), (29, opacity*.8), (36, opacity*.35), (43, 0), (FRAMES, 0)]
    keyed(envelope.outputs[0], keys)
    alpha = math_node('MULTIPLY', attr.outputs['Fac'], envelope.outputs[0])
    if not (warning or flash or particle or traveling):
        alpha = math_node('MULTIPLY', alpha, front)
        cross = node('ShaderNodeAttribute', 'Ribbon cross section')
        cross.attribute_name = 'cross_position'
        distance = math_node('SUBTRACT', reveal.outputs[0], path.outputs['Fac'])
        tip_width = math_node('MINIMUM', math_node('MULTIPLY', distance, 5), 1.01)
        tip = math_node('LESS_THAN', math_node('ABSOLUTE', cross.outputs['Fac'], 0), tip_width)
        alpha = math_node('MULTIPLY', alpha, tip)
    if noise:
        tex = node('ShaderNodeTexNoise', 'Fine ragged breakup, fixed seed')
        tex.inputs['Scale'].default_value = 21
        tex.inputs['Detail'].default_value = 3.0
        tex.inputs['Roughness'].default_value = .72
        threshold = node('ShaderNodeValue', 'Dissolve threshold')
        keyed(threshold.outputs[0], [(1, .19), (25, .19), (31, .34), (38, .65), (43, 1)])
        mask = math_node('GREATER_THAN', tex.outputs['Fac'], threshold.outputs[0])
        alpha = math_node('MULTIPLY', alpha, mask)
    transparent = node('ShaderNodeBsdfTransparent', 'Transparent background')
    light = node('ShaderNodeEmission', 'Silver light / dark incision')
    light.inputs['Color'].default_value = (*color, 1)
    light.inputs['Strength'].default_value = strength
    mix = node('ShaderNodeMixShader', 'Alpha coverage')
    links.new(alpha, mix.inputs[0])
    links.new(transparent.outputs[0], mix.inputs[1])
    links.new(light.outputs[0], mix.inputs[2])
    surface = node('ShaderNodeOutputMaterial', 'Surface')
    links.new(mix.outputs[0], surface.inputs['Surface'])
    # Predictable timeline seeking; no accumulated particle simulation state.
    if mat.node_tree.animation_data and mat.node_tree.animation_data.action:
        for fc in mat.node_tree.animation_data.action.fcurves:
            for k in fc.keyframe_points:
                k.interpolation = 'LINEAR'
    return mat


warning_mat = material('Warning translucent crimson', (.8, .025, .035), 1, .26, warning=True)
mist_mat = material('Faint silver fringe', (.35, .39, .46), .5, .16, noise=True)
blade_mat = material('Ragged cold silver', (.44, .49, .58), .65, .48, noise=True)
dark_mat = material('Near-black narrow incision', (.001, .002, .004), 1, .98)
white_mat = material('White hot cutting edge', (.85, .94, 1), 2.4)
silver_mat = material('Silver hairline', (.6, .67, .79), .9, .6)
glint_mat = material('Impact cross flash', (.9, .96, 1), 5, flash=True)
spark_mat = material('Straight silver splinters', (.7, .85, 1), 2, .9, particle=True)
front_mat = material('Traveling needle tip', (.9, .97, 1), 5, traveling=True)


def ribbon(name, layer, mat, start, end, width, offset=0, bend=0, z=0, rough=0, phase=0, feather=False):
    verts, faces, coverage, positions, cross_section = [], [], [], [], []
    count = 96
    bands = (-1, -.65, 0, .65, 1) if feather else (-1, 0, 1)
    for i in range(count + 1):
        u = i/count
        x = start + (end-start)*u
        taper = max(0, math.sin(math.pi*u))**.85
        wobble = 1 + rough*(.52*math.sin(u*79+phase)+.29*math.sin(u*173+phase)+.19*math.sin(u*313))
        center = offset*math.sin(math.pi*u) + bend*math.sin(math.pi*u)*math.sin(u*3.4+phase)
        for band in bands:
            verts.append((x, center + band*width*taper*wobble, z))
            coverage.append(max(0, 1-abs(band)) if feather else 1)
            positions.append(x)
            cross_section.append(band)
    stride = len(bands)
    for i in range(count):
        for j in range(stride-1):
            a = i*stride+j
            faces.append((a, a+stride, a+stride+1, a+1))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    for attribute, values in (('coverage', coverage), ('cut_position', positions), ('cross_position', cross_section)):
        data = mesh.attributes.new(attribute, 'FLOAT', 'POINT')
        data.data.foreach_set('value', values)
    obj = bpy.data.objects.new(name, mesh)
    layers[layer].objects.link(obj)
    obj.parent = root
    mesh.materials.append(mat)
    return obj


ribbon('Small fixed warning', '01 Small red warning', warning_mat, -2.25, 2.25, .035, z=.18, rough=.1)
for i in range(6):
    ribbon(f'Soft outer fringe {i:02d}', '02 Silver atmosphere', mist_mat,
           -3.65+i*.025, 3.6-i*.04, .22+i*.035, offset=(i-2.5)*.038,
           bend=.03, z=-.08-i*.005, rough=.27, phase=i*3.2, feather=True)
for side in (-1, 1):
    for i in range(11):
        start = -3.6+rng.uniform(0, 1.3)
        end = 3.6-rng.uniform(0, 1.4)
        ribbon(f'Torn blade {side:+d} {i:02d}', '03 Torn silver blades', blade_mat,
               start, end, rng.uniform(.012, .06), offset=side*rng.uniform(.07, .31),
               bend=side*rng.uniform(-.05, .08), z=-.02+i*.001,
               rough=.25, phase=rng.uniform(0, 8))

ribbon('Black cut - constant thin width', '04 Black incision', dark_mat, -3.7, 3.7, .061,
       bend=.008, z=.14, rough=.21)
for side in (-1, 1):
    ribbon(f'White edge {side:+d}', '05 White cutting edges', white_mat, -3.72, 3.72,
           .013, offset=side*.069, z=.15, rough=.18)
    for i in range(5):
        ribbon(f'Silver parallel streak {side:+d} {i:02d}', '05 White cutting edges', silver_mat,
               -3.6+rng.uniform(0, .65), 3.6-rng.uniform(0, .7), .003+i*.001,
               offset=side*(.105+i*.037), bend=side*.016, z=.151+i*.001,
               rough=.2, phase=i)
    ribbon(f'Long luminous blade {side:+d}', '05 White cutting edges', white_mat,
           -3.3 if side==1 else -2.6, 2.9 if side==1 else 3.5,
           .021, offset=side*.15, bend=side*.07, z=.16, rough=.18, phase=2)

for i, (angle, length, width) in enumerate(((64, 1.15, .018), (-35, .8, .015), (0, .6, .008))):
    obj = ribbon(f'Cross glint {i}', '06 Impact glint', glint_mat, -length/2, length/2,
                 width, z=.2)
    obj.location.x = .65
    obj.rotation_euler.z = math.radians(angle)

tip = ribbon('Traveling white needle - cut frames only', '06 Impact glint', front_mat,
             -.28, .28, .025, z=.22)
for frame, x in ((16, -4.3), (17, -4.3/3), (18, 4.3/3), (19, 4.3)):
    tip.location.x = x
    tip.keyframe_insert(data_path='location', frame=frame)

for i in range(18):
    side = (-1, 1)[i % 2]
    x = rng.uniform(-3, 3)
    length = rng.uniform(.09, .38)
    obj = ribbon(f'Splinter {i:02d}', '07 Flying splinters', spark_mat, -length/2, length/2,
                 rng.uniform(.0025, .009), z=.12)
    obj.rotation_euler.z = side*rng.uniform(.05, .24)
    for frame, distance in ((18, 0), (24, .14), (36, .5), (46, .8)):
        obj.location = (x + (frame-18)*.006, side*(.1+distance), 0)
        obj.keyframe_insert(data_path='location', frame=frame)

bpy.ops.object.camera_add(location=(0, 0, 12))
camera = bpy.context.object
camera.name = 'Fixed orthographic export camera'
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 10.8
scene.camera = camera
scene.use_nodes = True
n = scene.node_tree.nodes
n.clear()
render = n.new('CompositorNodeRLayers')
glow = n.new('CompositorNodeGlare')
glow.glare_type = 'FOG_GLOW'
glow.quality = 'HIGH'
glow.threshold = 1.5
glow.size = 7
glow.mix = 0
scene.node_tree.links.new(render.outputs['Image'], glow.inputs['Image'])
# Keep the dark cut opaque, but give the generated glow its own alpha coverage.
rgb = n.new('CompositorNodeRGBToBW')
scene.node_tree.links.new(glow.outputs['Image'], rgb.inputs[0])
alpha = n.new('CompositorNodeMath')
alpha.operation = 'MAXIMUM'
alpha.use_clamp = True
scene.node_tree.links.new(render.outputs['Alpha'], alpha.inputs[0])
scene.node_tree.links.new(rgb.outputs[0], alpha.inputs[1])
set_alpha = n.new('CompositorNodeSetAlpha')
scene.node_tree.links.new(glow.outputs['Image'], set_alpha.inputs['Image'])
scene.node_tree.links.new(alpha.outputs[0], set_alpha.inputs['Alpha'])
composite = n.new('CompositorNodeComposite')
scene.node_tree.links.new(set_alpha.outputs[0], composite.inputs[0])

scene.frame_set(21)
scene.render.filepath = str(OUT / 'peak.png')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / 'crayon-slash.blend'))
bpy.ops.render.render(write_still=True)
manifest = {
    'version': 'Blender prototype v1', 'blender': bpy.app.version_string, 'seed': 231123,
    'reference_used_as_texture': False, 'unity_imported': False,
    'resolution': [960, 640], 'fps': FPS, 'frames': FRAMES,
    'peak_frame': 21, 'direction_degrees': -36, 'camera_ortho_scale': 10.8,
    'color_management': 'Standard', 'alpha': 'RGBA transparent with composited glow coverage',
    'layers': {name: [o.name for o in col.objects] for name, col in layers.items()},
    'timing_seconds': {'warning': [0, .375], 'cut': [.375, .45], 'peak': [.475, .55],
                       'fade': [.55, 1.05], 'rest': [1.05, 1.4]},
}
(OUT / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
# Render animation only after the representative peak has been inspected.
retakes = os.environ.get('BLENDER_RETAKE_FRAMES')
if retakes:
    frames = OUT / 'frames'
    frames.mkdir(exist_ok=True)
    for frame in (int(value) for value in retakes.split(',')):
        assert 1 <= frame <= FRAMES
        scene.frame_set(frame)
        scene.render.filepath = str(frames / f'frame-{frame:04d}.png')
        bpy.ops.render.render(write_still=True)
    print('CRAYON_BLENDER_RETAKES_OK')
elif os.environ.get('BLENDER_RENDER_SEQUENCE') != '1':
    print('PEAK_READY: set BLENDER_RENDER_SEQUENCE=1 to render the sequence')
else:
    frames = OUT / 'frames'
    frames.mkdir(exist_ok=True)
    scene.render.filepath = str(frames / 'frame-')
    bpy.ops.render.render(animation=True)
    print('CRAYON_BLENDER_SEQUENCE_OK')
