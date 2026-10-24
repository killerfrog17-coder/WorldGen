"""Read the accepted rig/CC0-authored motion bank into canonical +Y coordinates."""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Matrix,Vector

ap=argparse.ArgumentParser();ap.add_argument('--source',type=Path,required=True);ap.add_argument('--root',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve();root.mkdir(exist_ok=True,parents=True)
source=a.source.resolve();digest=hashlib.sha256(source.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;info=json.loads(s['own_rig_review'])
rig=bpy.data.objects[info['rig']];mesh=bpy.data.objects[info['mesh']]
forward=Vector((-.91514678,-.40312079,0)).normalized();right=Vector((.40312079,-.91514678,0)).normalized()
C=Matrix((-right,forward,Vector((0,0,1))));assert C.determinant()>.999
rig.animation_data_clear();rig.data.pose_position='REST'
vertices=np.array([C@(mesh.matrix_world@v.co) for v in mesh.data.vertices],np.float32)
names=[b.name for b in rig.data.bones];groups={g.index:g.name for g in mesh.vertex_groups}
weights=np.zeros((len(vertices),len(names)),np.float32)
for vertex in mesh.data.vertices:
    for g in vertex.groups:
        if groups[g.group] in names:weights[vertex.index,names.index(groups[g.group])]=g.weight
bones=[dict(name=b.name,parent=b.parent.name if b.parent else None,
    head=list(C@(rig.matrix_world@b.head_local)),tail=list(C@(rig.matrix_world@b.tail_local)),
    rest=[list(row) for row in C.to_4x4()@rig.matrix_world@b.matrix_local]) for b in rig.data.bones]
np.savez_compressed(root/'source-anatomy.npz',vertices=vertices,faces=np.array([p.vertices[:] for p in mesh.data.polygons]),weights=weights)
rig.data.pose_position='POSE';arrays={};clips=[]
for index,(label,action_name) in enumerate(info['transferred'].items()):
    rig.animation_data_clear();action=bpy.data.actions[action_name]
    rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0]
    first,last=action.frame_range;poses=[]
    for frame in range(round(first),round(last)+1):
        s.frame_set(frame)
        poses.append([np.array(C.to_4x4()@rig.matrix_world@rig.pose.bones[name].matrix) for name in names])
    key='clip_'+str(index);arrays[key]=np.array(poses,np.float32)
    clips.append(dict(label=label,key=key,source_action=action_name,fps=info['transferred_fps'],frames=len(poses)))
    print('Read accepted motion:',label,len(poses),'frames',flush=True)
np.savez_compressed(root/'source-motions.npz',**arrays)
report=dict(source=str(source),source_sha256=digest,bones=bones,clips=clips,canonical=[list(row) for row in C],
    motion_origin='Existing accepted Mesh2Motion CC0-authored motion transfer; accepted Eating correction retained as source',
    source_unchanged=hashlib.sha256(source.read_bytes()).hexdigest()==digest,gpu_inference=False)
assert report['source_unchanged'];(root/'source-rig.json').write_text(json.dumps(report,indent=2))
