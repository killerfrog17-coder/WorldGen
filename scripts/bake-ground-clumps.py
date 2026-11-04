"""Blender offline grass-card authoring from the pinned CC0 source model."""
import bpy
import json
import sys
import runpy
from mathutils import Vector
from pathlib import Path

root = Path(__file__).resolve().parent.parent
if "--inspect" in sys.argv:
    bpy.ops.wm.open_mainfile(filepath=str(root / sys.argv[sys.argv.index("--inspect") + 1]))
    for obj in bpy.data.objects:
        if obj.type == "MESH":
            print("CLUMP SOURCE", obj.name, tuple(obj.dimensions), len(obj.data.vertices))
    raise SystemExit(0)
source = root / "assets-source/vegetation/ground-clumps/grass_medium_01/grass_medium_01_1k.blend"
bpy.ops.wm.open_mainfile(filepath=str(source))
selected = [bpy.data.objects["grass_medium_01_" + name + "_LOD0"] for name in ("mid_a", "mid_c", "tall_a")]
nettle = source.parent.parent / "nettle_plant/nettle_plant_1k.blend"
with bpy.data.libraries.load(str(nettle), link=False) as (available, loaded):
    loaded.objects = ["nettle_plant_medium_a_LOD0"]
selected.append(loaded.objects[0])
copies = []
for obj in selected:
    copy = obj.copy()
    copy.data = obj.data.copy()
    bpy.context.scene.collection.objects.link(copy)
    copies.append(copy)
for obj in list(bpy.data.objects):
    if obj not in copies:
        bpy.data.objects.remove(obj, do_unlink=True)

def make_material(asset):
    material = bpy.data.materials.new("CC0 scanned " + asset + " / unlit albedo bake")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    diffuse = nodes.new("ShaderNodeTexImage")
    diffuse.image = bpy.data.images.load(str(source.parent.parent / asset / ("textures/" + asset + "_diff_1k.jpg")))
    alpha = nodes.new("ShaderNodeTexImage")
    alpha.image = bpy.data.images.load(str(source.parent.parent / asset / ("textures/" + asset + "_alpha_1k.png")))
    alpha.image.colorspace_settings.name = "Non-Color"
    emission = nodes.new("ShaderNodeEmission")
    transparent = nodes.new("ShaderNodeBsdfTransparent")
    mix = nodes.new("ShaderNodeMixShader")
    output = nodes.new("ShaderNodeOutputMaterial")
    links = material.node_tree.links
    links.new(diffuse.outputs["Color"], emission.inputs["Color"])
    links.new(alpha.outputs["Color"], mix.inputs[0])
    links.new(transparent.outputs[0], mix.inputs[1])
    links.new(emission.outputs[0], mix.inputs[2])
    links.new(mix.outputs[0], output.inputs["Surface"])
    return material
materials = [make_material("grass_medium_01"), make_material("nettle_plant")]
receipt = []
for index, obj in enumerate(copies):
    obj.hide_render = False
    obj.hide_set(False)
    obj.parent = None
    obj.location = (0, 0, 0)
    obj.rotation_euler = (0, 0, index * .73)
    bpy.context.view_layer.update()
    bounds = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    width = max(p.x for p in bounds) - min(p.x for p in bounds)
    height = max(p.z for p in bounds) - min(p.z for p in bounds)
    obj.scale *= .91 / max(width, height)
    bpy.context.view_layer.update()
    bounds = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    obj.location.x = (index % 2 - .5) - (max(p.x for p in bounds) + min(p.x for p in bounds)) * .5
    obj.location.z = index // 2 + .025 - min(p.z for p in bounds)
    obj.data.materials.clear()
    obj.data.materials.append(materials[index == 3])
    for polygon in obj.data.polygons:
        polygon.material_index = 0
    receipt.append({"tile": index, "source_object": obj.name, "source_width": width, "source_height": height})

scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 24
scene.render.threads_mode = "FIXED"
scene.render.threads = 8
scene.render.film_transparent = True
scene.render.resolution_x = scene.render.resolution_y = 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.view_transform = "Standard"
scene.view_settings.look = "None"
scene.view_settings.exposure = 0
scene.view_settings.gamma = 1
camera = bpy.data.objects.new("Clump atlas orthographic camera", bpy.data.cameras.new("Atlas camera"))
scene.collection.objects.link(camera)
camera.location = (0, -4, 1)
camera.rotation_euler = (Vector((0, 0, 1)) - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 2
scene.camera = camera
destination = root / "unity/WorldGen.Unity/Assets/WorldGen/Resources/GroundClumpAtlasV1.png"
scene.render.filepath = str(destination)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.file.make_paths_relative()
bpy.ops.wm.save_as_mainfile(filepath=str(source.parent.parent / "ground-clump-bake.blend"))
bpy.ops.render.render(write_still=True)
(source.parent.parent / "bake.json").write_text(json.dumps({"generator": "scripts/bake-ground-clumps.py", "output": destination.relative_to(root).as_posix(), "tiles": receipt}, indent=2) + "\n")
runpy.run_path(str(root / "scripts/ground-clump-rects.py"), run_name="__main__")
print("CLUMP ATLAS BAKED", destination)
