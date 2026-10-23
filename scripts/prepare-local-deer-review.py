"""Stage a newly inferred shape for Blender inspection. CPU rendering only.

No downloaded mesh, existing animal rig, animation or game integration.
The optional photographic coat is a simple mirrored projection of OUR input
image, not an AI-generated 360-degree texture or production UV unwrap.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import math

import bpy
from mathutils import Vector


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--mesh', type=Path, required=True)
    ap.add_argument('--reference', type=Path, required=True)
    ap.add_argument('--output', type=Path, required=True)
    ap.add_argument('--render', action='store_true')
    args = ap.parse_args(sys.argv[sys.argv.index('--')+1:])
    if args.output.exists():
        raise FileExistsError(args.output)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(args.mesh.resolve()))
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if not meshes:
        raise ValueError('No generated mesh')
    bpy.ops.object.select_all(action='DESELECT')
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = 'Locally generated deer'
    obj.parent = None
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    low = Vector([min(v.co[i] for v in obj.data.vertices) for i in range(3)])
    high = Vector([max(v.co[i] for v in obj.data.vertices) for i in range(3)])
    factor = 1.65/(high.z-low.z)
    for vertex in obj.data.vertices:
        vertex.co = (vertex.co-Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z)))*factor
    obj.data.update()
    for face in obj.data.polygons:
        face.use_smooth = True
    # A small non-applied reduction keeps the original generated shape intact.
    if len(obj.data.polygons) > 50000:
        reduction = obj.modifiers.new('Review mesh budget (original retained)', 'DECIMATE')
        reduction.ratio = 50000/len(obj.data.polygons)
    coat = bpy.data.materials.new('Deer coat - procedural study')
    coat.use_nodes = True
    coat.diffuse_color = (.24,.12,.057,1)
    nodes = coat.node_tree.nodes
    links = coat.node_tree.links
    shader = nodes.get('Principled BSDF')
    shader.inputs['Roughness'].default_value = .86
    texcoord = nodes.new('ShaderNodeTexCoord')
    noise = nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 190
    noise.inputs['Detail'].default_value = 2
    links.new(texcoord.outputs['Generated'], noise.inputs['Vector'])
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (.12,.054,.024,1)
    ramp.color_ramp.elements[1].color = (.29,.16,.08,1)
    links.new(noise.outputs['Fac'], ramp.inputs[0])
    links.new(ramp.outputs['Color'], shader.inputs['Base Color'])
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = .12
    bump.inputs['Distance'].default_value = .001
    links.new(noise.outputs['Fac'], bump.inputs['Height'])
    links.new(bump.outputs['Normal'], shader.inputs['Normal'])
    obj.data.materials.clear()
    obj.data.materials.append(coat)
    # TripoSG reconstructs the reference-facing projection in X/Z after glTF's
    # Y-up conversion. Mirror that same reference over the reverse side for an
    # honest, cheap material study, retaining a brown fallback outside alpha.
    reference = bpy.data.images.load(str(args.reference.resolve()))
    reference.pack()
    import numpy as np
    pixels = np.asarray(reference.pixels[:], dtype=np.float32).reshape(reference.size[1],reference.size[0],4)
    rows, cols = np.where(pixels[:,:,3] > .03)
    uvmin = (cols.min()/reference.size[0], rows.min()/reference.size[1])
    uvmax = ((cols.max()+1)/reference.size[0], (rows.max()+1)/reference.size[1])
    bounds_min = [min(v.co[i] for v in obj.data.vertices) for i in range(3)]
    bounds_max = [max(v.co[i] for v in obj.data.vertices) for i in range(3)]
    uv = obj.data.uv_layers.new(name='Reference projection (not final unwrap)')
    for loop in obj.data.loops:
        p = obj.data.vertices[loop.vertex_index].co
        u = (p.x-bounds_min[0])/(bounds_max[0]-bounds_min[0])
        v = (p.z-bounds_min[2])/(bounds_max[2]-bounds_min[2])
        uv.data[loop.index].uv = (uvmin[0]+u*(uvmax[0]-uvmin[0]),uvmin[1]+v*(uvmax[1]-uvmin[1]))
    texture = nodes.new('ShaderNodeTexImage')
    texture.image = reference
    texture.extension = 'EXTEND'
    mix = nodes.new('ShaderNodeMixRGB')
    links.new(texture.outputs['Alpha'], mix.inputs[0])
    links.new(ramp.outputs['Color'], mix.inputs[1])
    links.new(texture.outputs['Color'], mix.inputs[2])
    # A single photo cannot describe the back of the head. Suppress its face
    # detail on surfaces pointing away from the source view, retaining the
    # procedural coat rather than painting a second face behind the skull.
    geometry = nodes.new('ShaderNodeNewGeometry')
    normal = nodes.new('ShaderNodeSeparateXYZ')
    links.new(geometry.outputs['Normal'], normal.inputs[0])
    facing = nodes.new('ShaderNodeMapRange')
    facing.inputs['From Min'].default_value = .15
    facing.inputs['From Max'].default_value = -.55
    facing.clamp = True
    links.new(normal.outputs['Y'], facing.inputs['Value'])
    position = nodes.new('ShaderNodeSeparateXYZ')
    links.new(texcoord.outputs['Generated'], position.inputs[0])
    head = nodes.new('ShaderNodeMapRange')
    head.inputs['From Min'].default_value = .73
    head.inputs['From Max'].default_value = .81
    head.clamp = True
    links.new(position.outputs['Z'], head.inputs['Value'])
    mask_head = nodes.new('ShaderNodeMixRGB')
    mask_head.inputs[1].default_value = (1,1,1,1)
    links.new(head.outputs['Result'], mask_head.inputs[0])
    links.new(facing.outputs['Result'], mask_head.inputs[2])
    alpha_mask = nodes.new('ShaderNodeMath')
    alpha_mask.operation = 'MULTIPLY'
    links.new(texture.outputs['Alpha'], alpha_mask.inputs[0])
    links.new(mask_head.outputs[0], alpha_mask.inputs[1])
    links.new(alpha_mask.outputs[0], mix.inputs[0])
    links.new(mix.outputs[0], shader.inputs['Base Color'])
    scene = bpy.context.scene
    scene.name = 'LOCAL AI DEER REVIEW'
    scene['shape_origin'] = 'New local TripoSG inference from imagegen reference'
    scene['review_status'] = 'Generated shape; materials/rigging separate'
    scene.render.fps = 24
    scene.world = bpy.data.worlds.new('Neutral studio')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.22,.24,.27,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .5
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0,0,-.012))
    floor = bpy.context.object
    floor.name = 'Studio floor (not part of animal)'
    floor_material = bpy.data.materials.new('Neutral floor')
    floor_material.diffuse_color = (.13,.15,.17,1)
    floor.data.materials.append(floor_material)
    for location, energy, size in [((-3,-4,5),650,4),((2,3,4),900,3),((4,-1,2),350,3)]:
        bpy.ops.object.light_add(type='AREA', location=location)
        light = bpy.context.object
        light.data.energy = energy
        light.data.shape = 'DISK'
        light.data.size = size
        light.rotation_euler = (Vector((0,0,.85))-light.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.camera_add(location=(2.7,-5,2.1))
    scene.camera = bpy.context.object
    scene.camera.rotation_euler = (Vector((0,0,.8))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
    scene.camera.data.type = 'ORTHO'
    scene.camera.data.ortho_scale = 2.8
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 24
    scene.render.threads_mode = 'FIXED'
    scene.render.threads = 4
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 850
    scene.render.resolution_percentage = 100
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                space = area.spaces.active
                space.shading.type = 'SOLID'
                space.shading.color_type = 'MATERIAL'
                space.overlay.show_overlays = False
                space.show_region_ui = True
                space.region_3d.view_location = Vector((0,0,.85))
                space.region_3d.view_distance = 3.7
                space.region_3d.view_rotation = Vector((2.7,-5,1.3)).to_track_quat('Z','Y')
                space.region_3d.view_perspective = 'ORTHO'
    report = {
        'source': str(args.mesh.resolve()),
        'source_sha256': hashlib.sha256(args.mesh.read_bytes()).hexdigest(),
        'reference': str(args.reference.resolve()),
        'vertices': len(obj.data.vertices), 'triangles': len(obj.data.polygons),
        'bounds': [[min(v.co[i] for v in obj.data.vertices) for i in range(3)],
                   [max(v.co[i] for v in obj.data.vertices) for i in range(3)]],
        'shape_origin': scene['shape_origin'], 'existing_animal_assets_used': False,
        'animation': 'None yet', 'materials': 'Mirrored reference projection with procedural fallback (not final UV texture)',
    }
    args.output.with_suffix('.json').write_text(json.dumps(report,indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(args.output.resolve()))
    if args.render:
        scene.render.filepath = str(args.output.with_suffix('.png').resolve())
        bpy.ops.render.render(write_still=True)
    print(json.dumps(report,indent=2))


if __name__ == '__main__':
    main()
