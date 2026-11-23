"""Technical connection sample, not approved game artwork. Run inside Blender."""
import argparse
import json
import math
from pathlib import Path
import sys

import bpy


parser = argparse.ArgumentParser()
parser.add_argument("--output", required=True, type=Path)
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
output = args.output.resolve()
output.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 8
scene.render.resolution_x = 512
scene.render.resolution_y = 512
scene.render.resolution_percentage = 100
scene.render.film_transparent = True
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.fps = 24
scene.frame_start = 1
scene.frame_end = 12
scene.view_settings.view_transform = "Standard"

material = bpy.data.materials.new("Sample cyan emission")
material.use_nodes = True
nodes = material.node_tree.nodes
nodes.clear()
emission = nodes.new("ShaderNodeEmission")
emission.inputs["Color"].default_value = (0.08, 0.65, 1.0, 1.0)
emission.inputs["Strength"].default_value = 1.5
surface = nodes.new("ShaderNodeOutputMaterial")
material.node_tree.links.new(emission.outputs[0], surface.inputs["Surface"])

collection = bpy.data.collections.new("Independent effect layers")
scene.collection.children.link(collection)


def add_mesh(name, vertices, faces):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.data.materials.append(material)
    for frame, scale in ((1, 0.15), (5, 0.8), (8, 1.0), (12, 1.25)):
        obj.scale = (scale, scale, scale)
        obj.keyframe_insert(data_path="scale", frame=frame)
    return obj


vertices = []
segments = 96
for index in range(segments):
    angle = index * math.tau / segments
    for radius in (1.28, 1.34):
        vertices.append((radius * math.cos(angle), radius * math.sin(angle), 0))
faces = [(2*i, 2*i+1, (2*i+3) % (2*segments), (2*i+2) % (2*segments))
         for i in range(segments)]
add_mesh("Shockwave ring", vertices, faces)
for index in range(8):
    angle = index * math.tau / 8
    local = ((1.5, -0.045), (1.92, 0), (1.5, 0.045), (1.58, 0))
    points = [(x*math.cos(angle)-y*math.sin(angle),
               x*math.sin(angle)+y*math.cos(angle), 0.01) for x, y in local]
    add_mesh(f"Spark {index + 1:02d}", points, [(0, 1, 2, 3)])

bpy.ops.object.camera_add(location=(0, 0, 10))
camera = bpy.context.object
camera.name = "Orthographic export camera"
camera.data.type = "ORTHO"
camera.data.ortho_scale = 5.6
scene.camera = camera
scene.frame_set(8)
scene.render.filepath = str(output / "preview.png")
bpy.ops.wm.save_as_mainfile(filepath=str(output / "connection-sample.blend"))
bpy.ops.render.render(write_still=True)
(output / "manifest.json").write_text(json.dumps({
    "purpose": "Blender Python connection check; not final game artwork",
    "blender_version": bpy.app.version_string,
    "preview": "preview.png", "source": "connection-sample.blend",
    "resolution": [512, 512], "transparent": True,
    "frame": 8, "frame_range": [1, 12], "fps": 24,
    "layers": [obj.name for obj in collection.objects],
    "unity_imported": False,
}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("BLENDER_CONNECTION_OK")
