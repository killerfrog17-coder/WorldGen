"""Export approved appearance skins without changing original rigs or motion."""
import argparse,hashlib,importlib.util,json,sys,time
from pathlib import Path
import bpy
REPO=Path(__file__).resolve().parent.parent
SOURCE=REPO/'assets-source/humans/appearance-review/Human physique comparison.blend'
OUTPUT=REPO/'assets-source/humans/appearance-runtime/exports'
spec=importlib.util.spec_from_file_location('appearance_bake',REPO/'scripts/export-human-runtime.py')
old=importlib.util.module_from_spec(spec);spec.loader.exec_module(old)

def export(letter):
    start=time.monotonic();bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=1
    scene.render.threads_mode='FIXED';scene.render.threads=8;scene.render.bake.margin=16;scene.render.bake.use_clear=True
    scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
    entry=next(r for r in json.loads(scene['physique_recipes']) if r['source']==letter)
    col=bpy.data.collections[entry['id']+'_after'];col.hide_viewport=False;col.hide_render=False
    rig=next(o for o in col.objects if o.type=='ARMATURE');meshes=[o for o in col.objects if o.type=='MESH']
    for obj in list(bpy.data.objects):
        if obj!=rig and obj not in meshes:bpy.data.objects.remove(obj,do_unlink=True)
    rig.name='MPFB_'+letter;rig.location=(0,0,0);rig.hide_set(False);rig.hide_render=False;rig.animation_data_clear();rig.data.pose_position='REST'
    for b in rig.pose.bones:b.matrix_basis.identity()
    for action in list(bpy.data.actions):bpy.data.actions.remove(action)
    folder=OUTPUT/('human-'+letter.lower());folder.mkdir(parents=True,exist_ok=True);records=[]
    for obj in meshes:
        stem=obj.name.split('.',1)[-1];obj.name='MPFB_'+letter+'.'+stem
        obj.hide_set(False);obj.hide_render=False
        for modifier in list(obj.modifiers):
            if modifier.type=='SUBSURF' or modifier.type=='MASK' and modifier.name.startswith('Delete.'):obj.modifiers.remove(modifier)
        kind='Basemesh' if obj.get('physique_body') else 'Eyes' if stem.startswith('high-poly') else 'Teeth' if stem.startswith('teeth') else 'Eyebrows' if stem.startswith('eyebrow') else 'Eyelashes' if stem.startswith('eyelashes') else 'Hair'
        colors=obj.data.color_attributes.new(name='FirstPersonHead',type='FLOAT_COLOR',domain='POINT')
        for vertex in obj.data.vertices:
            head=sum(g.weight for g in vertex.groups if obj.vertex_groups[g.group].name in {'head','neck_01'})
            colors.data[vertex.index].color=(1 if kind!='Basemesh' or head>.45 else 0,0,0,1)
        obj.data.color_attributes.active_color=colors
        for selected in bpy.context.selected_objects:selected.select_set(False)
        obj.select_set(True);bpy.context.view_layer.objects.active=obj
        size=4096 if kind=='Basemesh' else 512 if kind in {'Eyes','Teeth','Eyebrows','Eyelashes'} else 1024
        color=old.bake(obj,letter+' '+stem+' appearance',size)
        alpha=kind in {'Eyes','Hair','Eyebrows','Eyelashes'}
        if alpha:
            opacity=old.bake(obj,letter+' '+stem+' opacity',size,True)
            import numpy as np
            pixels=np.empty(size*size*4,dtype=np.float32);coverage=np.empty_like(pixels)
            color.pixels.foreach_get(pixels);opacity.pixels.foreach_get(coverage);pixels[3::4]=coverage[0::4];color.pixels.foreach_set(pixels);bpy.data.images.remove(opacity)
        color.filepath_raw=str(folder/(stem+'.png'));color.file_format='PNG';color.save()
        material=bpy.data.materials.new('Appearance '+letter+' '+stem);material.use_nodes=True
        p=material.node_tree.nodes.get('Principled BSDF');tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=color
        material.node_tree.links.new(tex.outputs['Color'],p.inputs['Base Color'])
        if alpha:material.node_tree.links.new(tex.outputs['Alpha'],p.inputs['Alpha'])
        obj.data.materials.clear();obj.data.materials.append(material)
        for polygon in obj.data.polygons:polygon.material_index=0
        records.append(dict(object=obj.name,kind=kind,texture=stem+'.png',alpha=alpha,roughness=.18 if kind=='Eyes' else .72,texture_size=size))
        print('APPEARANCE_MATERIAL',letter,stem,size,flush=True)
    rig.data.pose_position='POSE'
    for selected in bpy.context.selected_objects:selected.select_set(False)
    for obj in [rig,*meshes]:obj.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=str(folder/('human-'+letter.lower()+'.fbx')),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=False,use_mesh_modifiers=True,use_custom_props=False,path_mode='STRIP',mesh_smooth_type='FACE',use_triangles=True)
    record=dict(variant=letter,preview_variant=entry['id'],source_sha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),original_source_sha256='0946d797f3c620492444f2ec65d2a88280053fbda2c882dde3dfaf826942d229',eye_height=col['head_center'][2],materials=records,recipe=entry['after'],seconds=time.monotonic()-start)
    (folder/'human.json').write_text(json.dumps(record,indent=2));print('APPEARANCE_EXPORTED',letter,record['seconds'],flush=True)
    return record

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--variants',default='ABCDEF');args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    rows=[export(letter) for letter in args.variants]
    (OUTPUT.parent/'export-report.json').write_text(json.dumps(rows,indent=2))
