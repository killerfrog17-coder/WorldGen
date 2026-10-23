"""Offline Blender motion/controller checks and portable native-clip export."""
import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import struct
import sys
import time
import bpy
import numpy as np

sys.path.insert(0,str(Path(__file__).parent))
from deer_behaviour_core import Library,Quaternion,Z


def glb_json(path):
    data=path.read_bytes()
    assert data[:4]==b'glTF'
    size,kind=struct.unpack_from('<II',data,12)
    assert kind==0x4e4f534a
    return json.loads(data[20:20+size])


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--root',type=Path,required=True)
    parser.add_argument('--blend',default='Deer - Behaviour Prototype v3.blend')
    parser.add_argument('--export',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    root=args.root.resolve()
    bpy.ops.wm.open_mainfile(filepath=str(root/args.blend))
    scene=bpy.context.scene
    lib=Library(json.loads((root/'deer-motion-library.json').read_text()),
                np.load(root/'deer-motion-library.npz'))
    sk=lib.skeleton
    rig=bpy.data.objects['AI Deer Rig']
    result={'clips':{},'meshes':{},'controller':{},'exports':{}}
    assert len(sk.names)==34 and 'MotionRoot' in sk.names
    assert len(lib.clips)==16
    for name,info in lib.clips.items():
        a=lib.arrays
        assert np.isfinite(a[name+'_loc']).all() and np.isfinite(a[name+'_rot']).all()
        assert np.max(np.abs(np.linalg.norm(a[name+'_rot'],axis=-1)-1))<1e-4
        if info['loop']:
            assert np.max(np.abs(a[name+'_loc'][0]-a[name+'_loc'][-1]))<1e-5,name
            dots=np.abs(np.sum(a[name+'_rot'][0]*a[name+'_rot'][-1],axis=-1))
            assert np.min(dots)>.99999,name
        yaw=info['root_yaw_end']
        if name.startswith('Turn '):
            assert abs(abs(yaw)-math.pi/2)<.01
            assert yaw<0 if name=='Turn Left' else yaw>0
        action=bpy.data.actions[name]
        rig.animation_data_create().action=action
        rig.animation_data.action_slot=action.slots[0]
        floor=1e6
        maximum_basis_error=0
        # Every native keyed frame must reproduce the library, including body
        # floor correction. Root motion is deliberately its own exported node.
        for f in range(info['frames']):
            scene.frame_set(f)
            bpy.context.view_layer.update()
            for i,n in enumerate(sk.names):
                if n=='MotionRoot':
                    continue
                maximum_basis_error=max(maximum_basis_error,
                    float(np.max(np.abs(np.array(rig.pose.bones[n].location)-a[name+'_loc'][f,i]))))
            if f%4==0 or f==info['frames']-1:
                mesh=bpy.data.objects[lib.meta['lods'][0]]
                evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
                data=evaluated.to_mesh()
                floor=min(floor,min((evaluated.matrix_world@v.co).z for v in data.vertices))
                evaluated.to_mesh_clear()
        assert maximum_basis_error<1e-5,(name,maximum_basis_error)
        assert floor>-.018,(name,floor)
        result['clips'][name]={'frames':info['frames'],'duration':info['duration'],
            'loop':info['loop'],'sampled_surface_min_z':floor,'native_location_error':maximum_basis_error}
    for n in lib.meta['lods']:
        obj=bpy.data.objects[n]
        data=obj.data
        assert len(data.materials)==1
        data.calc_loop_triangles()
        counts=[len([g for g in v.groups if g.weight>1e-6]) for v in data.vertices]
        assert max(counts)<=4
        assert max(abs(sum(g.weight for g in v.groups)-1) for v in data.vertices)<1e-5
        result['meshes'][n]={'triangles':len(data.loop_triangles),'vertices':len(data.vertices),
                              'max_influences':max(counts)}
    images=[i for i in bpy.data.images if i.source=='FILE' and i.size[0]>100]
    result['textures']={i.name:list(i.size) for i in images}
    assert any(i.size[0]==2048 for i in images)
    assert any(i.size[0]==1024 for i in images)
    if args.export:
        rig.animation_data.action=bpy.data.actions['Idle']
        rig.animation_data.action_slot=rig.animation_data.action.slots[0]
        scene.frame_start=0
        scene.frame_end=80
        scene.frame_set(0)
        export_root=root/'game-assets'
        export_root.mkdir(exist_ok=True)
        for i,n in enumerate(lib.meta['lods']):
            for obj in scene.objects:
                obj.select_set(False)
            mesh=bpy.data.objects[n]
            mesh.hide_set(False)
            mesh.hide_render=False
            mesh.select_set(True)
            rig.select_set(True)
            bpy.context.view_layer.objects.active=rig
            path=export_root/f'Deer-LOD{i}.glb'
            bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,
                export_animations=True,export_animation_mode='ACTIONS',export_frame_range=False,
                export_force_sampling=True,export_def_bones=False,export_skins=True,
                export_influence_nb=4,export_all_influences=False,export_yup=True,
                export_materials='EXPORT',export_cameras=False,export_lights=False,
                export_extras=True)
            glb=glb_json(path)
            names={a['name'] for a in glb.get('animations',[])}
            assert names==set(lib.clips),(path,names)
            assert any(n.get('name')=='MotionRoot' for n in glb['nodes'])
            assert len(glb['skins'])==1
            assert len(glb['skins'][0]['joints'])==34
            assert len(glb['materials'])==1
            assert len(glb['images'])==2
            result['exports'][path.name]={'bytes':path.stat().st_size,
                'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
                'animations':sorted(names),'joints':34}
            mesh.hide_set(i!=0)
            mesh.hide_render=i!=0
        for obj in scene.objects:
            obj.select_set(False)
        for n in lib.meta['lods']:
            obj=bpy.data.objects[n]
            obj.hide_set(False)
            obj.select_set(True)
        rig.select_set(True)
        path=export_root/'Deer-Game-Prototype.glb'
        bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,
            export_animations=True,export_animation_mode='ACTIONS',export_frame_range=False,
            export_force_sampling=True,export_def_bones=False,export_skins=True,
            export_influence_nb=4,export_all_influences=False,export_yup=True,
            export_materials='EXPORT',export_cameras=False,export_lights=False,export_extras=True)
        data=glb_json(path)
        assert len(data['meshes'])==4 and len(data['images'])==2 and len(data['materials'])==1
        assert {a['name'] for a in data['animations']}==set(lib.clips)
        result['exports'][path.name]={'bytes':path.stat().st_size,
            'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'meshes':4,
            'materials':1,'images':2,'animations':16}
    # Import the exact interactive wrapper in background: it skips autoplay.
    spec=importlib.util.spec_from_file_location('deer_preview',Path(__file__).with_name('deer-behaviour-viewer.py'))
    viewer=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(viewer)
    c=viewer.get_controller(scene)
    p=scene.deer_preview
    total=0
    start=time.perf_counter()
    # Run the whole routine plus one cycle boundary. The actual frame handler,
    # not a mock sampler, sets poses and exercises the mode transitions.
    p.mode='ROUTINE'
    scene.frame_end=24*90-1
    for f in range(24*73):
        scene.frame_set(f)
        if f%24==0:
            bpy.context.view_layer.update()
            assert all(np.isfinite(np.array(b.matrix)).all() for b in c.bones)
        total+=1
    result['controller']['routine_frames']=total
    p.mode='SLOPE'
    p.speed=1.05
    scene.frame_set(0)
    max_contact_error=0
    for f in range(1,24*10):
        scene.frame_set(f)
        for k,error in c.foot_diagnostics:
            if c.contacts[k]>.9:
                max_contact_error=max(max_contact_error,abs(error-.004))
    assert max_contact_error<.12,max_contact_error
    result['controller']['slope_max_contact_height_error_m']=max_contact_error
    result['controller']['slope_distance_m']=c.position.length
    assert c.position.length>4
    p.mode='MANUAL'
    scene.frame_set(0)
    c.request('Death')
    for f in range(1,24*2):
        scene.frame_set(f)
    assert c.active=='Death'
    p.mode='NATIVE'
    viewer.native_clip(scene,'Run')
    assert rig.animation_data.action.name=='Run'
    p.mode='MANUAL'
    assert not rig.animation_data or rig.animation_data.action is None
    result['controller']['death_holds']=True
    result['controller']['native_mode_binds_actual_action']=True
    result['controller']['seconds']=time.perf_counter()-start
    (root/'behaviour-validation.json').write_text(json.dumps(result,indent=2))
    print(json.dumps(result,indent=2),flush=True)


if __name__=='__main__':
    main()
