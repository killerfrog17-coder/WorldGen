"""Assemble actual MPFB and UMA output with common, ordinary real-time lighting."""
import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

REPO=Path(__file__).resolve().parent.parent
ROOT=REPO/'artifacts/human-builder-demo'


def material(info,directory,recipe):
    mat=bpy.data.materials.new('UMA '+info['name']);mat.use_nodes=True
    nodes=mat.node_tree.nodes;links=mat.node_tree.links
    shader=nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.63
    shader.inputs['Specular IOR Level'].default_value=.27
    shader.inputs['Base Color'].default_value=tuple(info['color'][key] for key in ['r','g','b','a'])
    maps={item['property']:directory/item['path'] for item in info.get('maps',[])}
    base=next((maps[name] for name in ['_BaseMap','_MainTex','_Diffuse'] if name in maps),None)
    if base:
        node=nodes.new('ShaderNodeTexImage');node.image=bpy.data.images.load(str(base),check_existing=True)
        color=tuple(info['color'][key] for key in ['r','g','b','a'])
        if 'lashes' in info['name'].lower():
            # UMA puts the independently coloured Brows channel in the lash atlas.
            # Match that channel to this person's selected hair rather than leaving it white.
            color=(*recipe['hair_rgb'],1)
        if any(abs(c-1)>.001 for c in color):
            tint=nodes.new('ShaderNodeMixRGB');tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1;tint.inputs[2].default_value=color
            links.new(node.outputs['Color'],tint.inputs[1]);links.new(tint.outputs[0],shader.inputs['Base Color'])
        else:links.new(node.outputs['Color'],shader.inputs['Base Color'])
        if any(name in info['name'].lower() for name in ['hair','brow','lash']):
            links.new(node.outputs['Alpha'],shader.inputs['Alpha'])
            mat.surface_render_method='DITHERED'
    if 'Eyes' in info['name']:
        shader.inputs['Roughness'].default_value=.27
        shader.inputs['Specular IOR Level'].default_value=.12
    if 'hair' in info['name'].lower():
        shader.inputs['Roughness'].default_value=.82
        shader.inputs['Specular IOR Level'].default_value=.035
    normal=next((maps[name] for name in ['_BumpMap','_NormalMap'] if name in maps),None)
    # Unity's packed normal encoding needs explicit conversion; do not wire it as a plain RGB normal.
    mat['source_shader']=info['shader'];mat['source_texture_maps']=json.dumps({k:str(v) for k,v in maps.items()})
    return mat


def import_uma(recipe):
    directory=ROOT/'uma'/recipe['id']
    source=directory/'character.json'
    if not source.is_file():return
    info=json.loads(source.read_text())
    collection=bpy.data.collections.new('UMA_'+recipe['id']);bpy.context.scene.collection.children.link(collection)
    all_points=[];eye_points=[]
    for part in info['meshes']:
        points=[(v['x'],-v['z'],v['y']) for v in part['vertices']]
        faces=[];indices=[]
        for index,submesh in enumerate(part['submeshes']):
            if '2ndPass' in part['materials'][index]['name']:continue
            triangles=submesh['triangles']
            if '_Eyes' in part['materials'][index]['name']:
                eye_points.extend(points[i] for i in sorted(set(triangles)))
            faces.extend(tuple(triangles[i:i+3]) for i in range(0,len(triangles),3))
            indices.extend([index]*(len(triangles)//3))
        mesh=bpy.data.meshes.new(part['name']);mesh.from_pydata(points,[],faces);mesh.update()
        obj=bpy.data.objects.new('UMA_'+recipe['id']+'_'+part['name'],mesh);collection.objects.link(obj)
        uv=mesh.uv_layers.new(name='UVMap')
        for loop in mesh.loops:
            v=part['uv'][loop.vertex_index];uv.data[loop.index].uv=(v['x'],v['y'])
        for item in part['materials']:mesh.materials.append(material(item,directory,recipe))
        for face,index in zip(mesh.polygons,indices):face.material_index=index;face.use_smooth=True
        if part.get('normals'):
            mesh.normals_split_custom_set_from_vertices([(n['x'],-n['z'],n['y']) for n in part['normals']])
        all_points.extend(points)
        obj['source']='Actual UMA runtime generated mesh; frozen appearance audition'
        obj['source_recipe']=info['recipe'];obj['demo_game_ready']=False
    top=max(v[2] for v in all_points)
    collection['head_center']=list(sum((Vector(p) for p in eye_points),Vector())/len(eye_points)) if eye_points else [0,-.06,top-.14]
    collection['builder']='UMA';collection['recipe']=json.dumps(recipe)


def point_camera(location,target):
    cam=bpy.context.scene.camera;cam.location=location
    cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()


def select(builder,letter,view='face',compare=False,lineup=False):
    current=bpy.data.collections.get(builder+'_'+letter)
    head=Vector(current['head_center'])
    for collection in bpy.data.collections:
        if not collection.name.startswith(('MPFB_','UMA_')):continue
        method,code=collection.name.split('_')[:2]
        visible=(method==builder and (code==letter or lineup)) or (compare and code==letter)
        collection.hide_render=not visible;collection.hide_viewport=not visible
        offset=(ord(code)-ord('C')-.5)*1.15 if lineup else (-.40 if method=='MPFB' else .40) if compare else 0
        vertical=head.z-collection['head_center'][2] if compare and view!='body' and not lineup else 0
        for obj in collection.objects:
            if obj.parent is None:
                obj.location.x=0 if compare and view=='profile' else offset
                obj.location.y=offset if compare and view=='profile' else 0
                obj.location.z=vertical
    if lineup:
        point_camera((0,-11.6,1.4),(0,0,.92))
    focus=head+Vector((0,0,-.035))
    if lineup:pass
    elif view=='body':point_camera((0,-(4.8 if compare else 3.8),1.15),(0,0,.88))
    elif view=='profile':point_camera((.72 if not compare else 2.5,-.05,focus.z),focus)
    elif view=='threequarter':point_camera((.8 if not compare else 1.8,-1.25 if not compare else -2.25,focus.z+.04),focus)
    else:point_camera((0,-(1.9 if compare else .72),focus.z),focus)
    bpy.context.scene['demo_builder']=builder;bpy.context.scene['demo_variant']=letter
    bpy.context.scene['demo_view']=view;bpy.context.scene['demo_compare']=compare
    bpy.context.scene['demo_lineup']=lineup
    for area in bpy.context.screen.areas if bpy.context.screen else []:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'


def setup():
    scene=bpy.context.scene
    scene.render.engine='BLENDER_EEVEE_NEXT'
    scene.render.resolution_x=1000;scene.render.resolution_y=900;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.world=bpy.data.worlds.new('Neutral studio');scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.15,.17,.20,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=.35
    scene.view_settings.view_transform='AgX'
    scene.view_settings.look='AgX - Medium High Contrast'
    scene.render.film_transparent=False
    camera=bpy.data.cameras.new('Review Camera');camera.lens=58;camera.clip_start=.01
    obj=bpy.data.objects.new('Review Camera',camera);scene.collection.objects.link(obj);scene.camera=obj
    for name,location,power,size,color in [
        ('Large key',(-3,-4,5),650,4,(1,.93,.86)),
        ('Soft fill',(3,-2,3),430,3,(.86,.93,1)),
        ('Rim',(1,2,4),700,3,(1,1,1))]:
        lamp=bpy.data.lights.new(name,'AREA');lamp.energy=power;lamp.shape='DISK';lamp.size=size;lamp.color=color
        light=bpy.data.objects.new(name,lamp);scene.collection.objects.link(light);light.location=location
        light.rotation_euler=(Vector((0,0,1))-light.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.012))
    floor=bpy.context.object;floor.name='Neutral floor'
    mat=bpy.data.materials.new('Neutral floor');mat.diffuse_color=(.105,.115,.13,1);floor.data.materials.append(mat)
    scene.render.image_settings.color_mode='RGB'
    for area in bpy.context.screen.areas if bpy.context.screen else []:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.shading.use_scene_world=True
            area.spaces.active.shading.use_scene_lights=True
            area.spaces.active.overlay.show_overlays=False
            area.spaces.active.show_region_ui=True


def main():
    bpy.context.preferences.filepaths.save_version=0
    ap=argparse.ArgumentParser();ap.add_argument('--mpfb-only',action='store_true');args=ap.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/'MPFB - Appearance Candidates.blend'))
    recipes=json.loads((REPO/'scripts/human_demo_recipes.json').read_text())
    if not args.mpfb_only:
        for recipe in recipes:import_uma(recipe)
    setup()
    select('MPFB','A','face',compare=not args.mpfb_only)
    for image in bpy.data.images:
        if image.source=='FILE' and image.filepath:image.pack()
    scene=bpy.context.scene;scene['demo_recipes']=json.dumps(recipes)
    output=ROOT/('MPFB - Diagnostic.blend' if args.mpfb_only else 'Human - Character Builder Comparison.blend')
    bpy.ops.wm.save_as_mainfile(filepath=str(output),compress=True)
    directory=ROOT/'renders';directory.mkdir(exist_ok=True)
    for builder in ['MPFB'] if args.mpfb_only else ['MPFB','UMA']:
        for recipe in recipes:
            for view in ['face','profile']:
                select(builder,recipe['id'],view)
                scene.render.resolution_x=600;scene.render.resolution_y=640
                scene.render.filepath=str(directory/(builder+'-'+recipe['id']+'-'+view+'.png'))
                bpy.ops.render.render(write_still=True)
        select(builder,'A','body',lineup=True)
        scene.render.resolution_x=1600;scene.render.resolution_y=680
        scene.render.filepath=str(directory/(builder+'-lineup.png'));bpy.ops.render.render(write_still=True)
    print('HUMAN_COMPARISON_READY',str(output),flush=True)


if __name__=='__main__':main()
