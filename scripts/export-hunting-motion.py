"""Read-only append-only export of native non-locomotion clips. Never save source Blender files."""
import argparse,gzip,hashlib,json,math,struct,sys,time
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix,Vector

root=Path(__file__).resolve().parents[1]
ap=argparse.ArgumentParser();ap.add_argument('--slugs',nargs='*');a=ap.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
inventory=json.loads((root/'artifacts/fauna-runtime-20261026/native-inventory.json').read_text())['animals']
destination=root/'assets/fauna-motion';destination.mkdir(parents=True,exist_ok=True)
records=[];failures=[]
def role(label):
    key=label.lower().replace('_',' ').replace('-',' ')
    if key in ['eat','eating','graze','grazing','feed','feeding']:return 'eating'
    if key in ['sleep','sleeping','rest','resting','sit','sitting','coiled']:return 'sleep' if 'sleep' in key or key.startswith('rest') else 'sit'
    if 'idle alert' in key or key in ['alert','look','look around']:return 'alert'
    if key in ['drink','drinking']:return 'drink'
    if key in ['bite','attack','head but','head butt','kick','claw','peck']:return 'attack'
    return None
for animal in inventory:
    slug=animal['slug']
    if a.slugs and slug not in a.slugs:continue
    selected=[(label,name,role(label)) for label,name in animal['entry']['actions'].items() if role(label)]
    if not selected:continue
    source=root/animal['source'];digest=hashlib.sha256(source.read_bytes()).hexdigest();assert digest==animal['sha256']
    try:
        model=json.loads((root/'artifacts/fauna-runtime-20261026/exports'/slug/'model.json').read_text())
        bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene;entry=animal['entry'];rig=bpy.data.objects[entry['rig']];body=bpy.data.objects[entry['body']]
        C=Matrix(model['source_to_runtime']);inverseC=C.inverted();inverseRig=rig.matrix_world.inverted();bones=list(rig.data.bones);assert len(bones)==model['bones'] if 'bones' in model else len(bones)<=128
        inverseBind=[b.matrix_local.inverted() for b in bones];root_bone=next(b for b in bones if b.parent is None)
        morph=None
        if model.get('hasMorph') and body.data.shape_keys and body.data.shape_keys.animation_data:
            paths={d.data_path for d in body.data.shape_keys.animation_data.drivers};morph=next((k for k in list(body.data.shape_keys.key_blocks)[1:] if k.path_from_id('value') in paths),None)
        fps=scene.render.fps/scene.render.fps_base;clips=[];frames=[];morphs=[]
        rig.data.pose_position='POSE'
        for label,name,kind in selected:
            act=bpy.data.actions.get(name)
            if act is None:continue
            rig.animation_data_clear()
            for bone in rig.pose.bones:bone.matrix_basis=Matrix.Identity(4)
            rig.animation_data_create().action=act
            if act.slots:rig.animation_data.action_slot=act.slots[0]
            begin,end=act.frame_range;duration=(end-begin)/fps
            if not .01<duration<90:continue
            count=max(2,math.ceil(duration*30)+1);start=len(frames);anchor=None
            for fi in range(count):
                frame=begin+(end-begin)*fi/(count-1);scene.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()
                point=rig.matrix_world@rig.pose.bones[root_bone.name].head
                if anchor is None:anchor=point.copy()
                delta=point-anchor;delta.z=0
                left=C@Matrix.Translation(-delta)@rig.matrix_world
                palette=np.asarray([left@rig.pose.bones[b.name].matrix@inverseBind[i]@inverseRig@inverseC for i,b in enumerate(bones)],dtype='<f4')
                assert np.isfinite(palette).all();frames.append(palette);morphs.append(float(morph.value) if morph else 0)
            clips.append(dict(name=label,role=kind,start=start,frames=count,duration=duration))
        if not clips:continue
        meta=dict(slug=slug,source_sha256=digest,bones=len(bones),frames=len(frames),clips=clips)
        header=json.dumps(meta,separators=(',',':')).encode()
        payload=b'WGEXTRA1'+bytes.fromhex(digest)+struct.pack('<III',len(bones),len(frames),len(header))+header+np.asarray(frames,dtype='<f4').tobytes()+np.asarray(morphs,dtype='<f4').tobytes()
        path=destination/(slug+'.bin.gz');path.write_bytes(gzip.compress(payload,compresslevel=6,mtime=0))
        assert hashlib.sha256(source.read_bytes()).hexdigest()==digest
        records.append(dict(slug=slug,file=path.name,source_sha256=digest,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),bytes=path.stat().st_size,roles=[c['role'] for c in clips]))
        print('MOTION',slug,len(clips),len(frames),path.stat().st_size,flush=True)
    except Exception as exc:
        failures.append(dict(slug=slug,error=repr(exc)));print('FAIL',slug,repr(exc),flush=True)
index=destination/'index.json'
if a.slugs and index.exists():records=[r for r in json.loads(index.read_text())['animals'] if r['slug'] not in a.slugs]+records
index.write_text(json.dumps(dict(version=1,policy='Additional original authored motion only; approved body/skin/textures/locomotion bundles unchanged',animals=sorted(records,key=lambda r:r['slug']),failures=failures),indent=2)+'\n')
print('FINISHED',len(records),'banks;',len(failures),'failures',flush=True)
