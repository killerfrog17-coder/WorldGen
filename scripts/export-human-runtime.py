"""Read-only MPFB audition -> FBX skins/native actions and unlit material maps.

CPU only. Never rewrites the audition or changes source rigs/weights/actions.
Runtime derivatives omit subdivision, animation uses the already fitted CC0 bank.
"""
import argparse, hashlib, json, sys, time
from pathlib import Path
import bpy
import numpy as np
import importlib.util

REPO = Path(__file__).resolve().parent.parent
SOURCE = REPO/'artifacts/human-animation-preview/Human - Animation Comparison.blend'
OUTPUT = REPO/'artifacts/human-runtime/exports'
EVIDENCE = REPO/'artifacts/human-runtime'
CLIPS = {'Idle', 'Walk', 'Run', 'Sprint', 'Crouch', 'Jump', 'Swim','KayKit run','KayKit run B','KayKit large run'}


def bake(ob, name, size, alpha=False):
    scene = bpy.context.scene
    image = bpy.data.images.new(name, width=size, height=size, alpha=True)
    image.colorspace_settings.name = 'Non-Color' if alpha else 'sRGB'
    nodes = []
    for mat in ob.data.materials:
        target = mat.node_tree.nodes.new('ShaderNodeTexImage');target.image = image
        mat.node_tree.nodes.active = target;nodes.append((mat, target))
    if alpha:
        rewires=[]
        for mat,_ in nodes:
            tree=mat.node_tree;output=next(n for n in tree.nodes if n.type=='OUTPUT_MATERIAL')
            previous=output.inputs['Surface'].links[0].from_socket
            p=next((n for n in tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
            emit=tree.nodes.new('ShaderNodeEmission')
            eye=next((n for n in tree.nodes if n.type=='GROUP' and n.node_tree.nodes.get('EyeCoordinateInfo')),None)
            if eye:
                # MPFB has a transmissive outer cornea over an inner pigmented
                # eye. Opaque PBR must not render that black corneal albedo.
                # Retain the exact native inner-eye UV coverage, INCLUDING pupil.
                eye.node_tree=eye.node_tree.copy();inner=eye.node_tree
                inner.interface.new_socket(name='RuntimeInnerCoverage',in_out='OUTPUT',socket_type='NodeSocketFloat')
                group_out=next(n for n in inner.nodes if n.type=='GROUP_OUTPUT')
                inner.links.new(inner.nodes['EyeCoordinateInfo'].outputs['IsInRange'],group_out.inputs['RuntimeInnerCoverage'])
                tree.links.new(eye.outputs['RuntimeInnerCoverage'],emit.inputs['Color'])
            elif p and p.inputs['Alpha'].is_linked:tree.links.new(p.inputs['Alpha'].links[0].from_socket,emit.inputs['Color'])
            else:emit.inputs['Color'].default_value=(1,1,1,1)
            tree.links.new(emit.outputs[0],output.inputs['Surface']);rewires.append((tree,output,previous,emit))
        bpy.ops.object.bake(type='EMIT')
        for tree,output,previous,emit in rewires:
            tree.links.new(previous,output.inputs['Surface']);tree.nodes.remove(emit)
    else:
        bpy.ops.object.bake(type='DIFFUSE',pass_filter={'COLOR'})
    for mat,node in nodes:mat.node_tree.nodes.remove(node)
    return image


def export(letter):
    started=time.monotonic()
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=1
    scene.render.threads_mode='FIXED';scene.render.threads=2
    scene.render.bake.margin=12;scene.render.bake.use_clear=True
    scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
    collection=bpy.data.collections['MPFB_'+letter];collection.hide_viewport=False;collection.hide_render=False
    rig=next(o for o in collection.objects if o.type=='ARMATURE')
    objects=[o for o in collection.objects if o.type=='MESH' and 'suit' not in o.name.lower()]
    clips=json.loads(rig['preview_clips'])
    sys.path.insert(0,str(REPO/'scripts'))
    from human_motion_common import Motion
    spec=importlib.util.spec_from_file_location('human_retarget',REPO/'scripts/build-human-animation-preview.py')
    retarget=importlib.util.module_from_spec(spec);spec.loader.exec_module(retarget)
    bank=REPO/'artifacts/human-animation-sources/kaykit'
    for label,file,clip in [('KayKit run B','Rig_Medium_MovementBasic.glb','Running_B'),('KayKit large run','Rig_Large_MovementBasic.glb','Running_A')]:
        motion=Motion(next(bank.rglob(file)));samples=[motion.sample(clip,min(i/30,motion.duration(clip))) for i in range(round(motion.duration(clip)*30)+1)]
        clips.append(retarget.animate(rig,'MPFB',(label,motion,clip),samples))
    actions={c['action']:c for c in clips if c['label'] in CLIPS}
    for ob in list(bpy.data.objects):
        if ob!=rig and ob not in objects:bpy.data.objects.remove(ob,do_unlink=True)
    rig.hide_set(False);rig.hide_render=False;rig.location=(0,0,0)
    rig.animation_data_clear();rig.data.pose_position='REST'
    for b in rig.pose.bones:b.matrix_basis.identity()
    for action in list(bpy.data.actions):
        if action.name not in actions:bpy.data.actions.remove(action)
    folder=OUTPUT/('human-'+letter.lower());folder.mkdir(parents=True,exist_ok=True)
    records=[]
    for ob in objects:
        ob.hide_set(False);ob.hide_render=False
        for modifier in list(ob.modifiers):
            if modifier.type=='SUBSURF' or modifier.type=='MASK' and modifier.name.startswith('Delete.'):ob.modifiers.remove(modifier)
        name=ob.name.replace('MPFB_'+letter+'.','').replace('.','-')
        # The MPFB RNA extension need not be installed/enabled in the exporter.
        kind=('Basemesh' if name=='body' else 'Eyes' if name=='high-poly' else
              'Eyebrows' if name.startswith('eyebrow') else 'Eyelashes' if name.startswith('eyelashes') else
              'Teeth' if name.startswith('teeth') else 'Clothes' if 'suit' in name else 'Hair')
        # Camera-only head concealment; this attribute is ignored by shadow passes.
        colors=ob.data.color_attributes.new(name='FirstPersonHead',type='FLOAT_COLOR',domain='POINT')
        for v in ob.data.vertices:
            weight=sum(g.weight for g in v.groups if 'head' in ob.vertex_groups[g.group].name.lower() or 'neck' in ob.vertex_groups[g.group].name.lower())
            head=1 if kind in {'Eyes','Hair','Teeth','Eyebrows','Eyelashes'} or weight>.45 else 0
            colors.data[v.index].color=(head,0,0,1)
        ob.data.color_attributes.active_color=colors
        for obj in bpy.context.selected_objects:obj.select_set(False)
        ob.select_set(True);bpy.context.view_layer.objects.active=ob
        size=2048 if kind=='Basemesh' else 512 if kind in {'Eyes','Teeth','Eyebrows','Eyelashes'} else 1024
        diffuse=bake(ob,name+' pigment',size)
        transparent=kind in {'Hair','Eyebrows','Eyelashes','Eyes'}
        if transparent:
            alpha=bake(ob,name+' opacity',size,True)
            pixels=np.empty(size*size*4,dtype=np.float32);diffuse.pixels.foreach_get(pixels)
            coverage=np.empty_like(pixels);alpha.pixels.foreach_get(coverage)
            pixels[3::4]=coverage[0::4];diffuse.pixels.foreach_set(pixels)
            bpy.data.images.remove(alpha)
        diffuse.filepath_raw=str(folder/(name+'.png'));diffuse.file_format='PNG';diffuse.save()
        # Plain FBX material bindings; Unity replaces them with the shared human shader.
        mat=bpy.data.materials.new('Human_'+letter+'_'+name);mat.use_nodes=True
        p=mat.node_tree.nodes.get('Principled BSDF');p.inputs['Roughness'].default_value=.18 if kind=='Eyes' else .72
        tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=diffuse
        mat.node_tree.links.new(tex.outputs['Color'],p.inputs['Base Color'])
        if transparent:mat.node_tree.links.new(tex.outputs['Alpha'],p.inputs['Alpha'])
        ob.data.materials.clear();ob.data.materials.append(mat)
        for polygon in ob.data.polygons:polygon.material_index=0
        records.append({'object':ob.name,'kind':kind,'texture':name+'.png','alpha':transparent,'roughness':.18 if kind=='Eyes' else .72,'vertices':len(ob.data.vertices)})
        print('HUMAN_MATERIAL',letter,name,size,flush=True)
    rig.data.pose_position='POSE'
    for ob in bpy.context.selected_objects:ob.select_set(False)
    for ob in [rig,*objects]:ob.select_set(True)
    bpy.context.view_layer.objects.active=rig
    first=bpy.data.actions[actions[next(iter(actions))]['action']]
    rig.animation_data_create();rig.animation_data.action=first;rig.animation_data.action_slot=first.slots[0]
    scene.frame_set(1)
    bpy.ops.export_scene.fbx(filepath=str(folder/('human-'+letter.lower()+'.fbx')),use_selection=True,
        object_types={'ARMATURE','MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',
        bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0,bake_anim_step=1,use_mesh_modifiers=True,
        use_custom_props=False,path_mode='STRIP',mesh_smooth_type='FACE',use_triangles=True)
    metadata={'variant':letter,'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
        'eye_height':collection['head_center'][2], 'materials':records,
        'clips':[actions[k] for k in actions], 'seconds':time.monotonic()-started}
    (folder/'human.json').write_text(json.dumps(metadata,indent=2))
    print('HUMAN_EXPORTED',letter,round(metadata['seconds'],2),flush=True)
    return metadata


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--variants',default='ABCDEF')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    EVIDENCE.mkdir(parents=True,exist_ok=True)
    reports=[export(letter) for letter in args.variants]
    (EVIDENCE/'export-report.json').write_text(json.dumps(reports,indent=2))


if __name__=='__main__':main()
