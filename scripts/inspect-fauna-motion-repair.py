"""Read-only rig, weight and posed transform evidence for a selected repair."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
ap=argparse.ArgumentParser();ap.add_argument('--blend',type=Path,required=True);ap.add_argument('--out',type=Path,required=True);ap.add_argument('--clip',default='Run')
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);source=a.blend.resolve();digest=hashlib.sha256(source.read_bytes()).hexdigest();a.out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0];body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']];names=[b.name for b in rig.data.bones]
rig.animation_data_clear();rig.data.pose_position='REST';bpy.context.view_layer.update();vertices=np.array([v.co[:] for v in body.data.vertices]);weights=np.zeros((len(vertices),len(names)))
for v in body.data.vertices:
 for g in v.groups:
  label=body.vertex_groups[g.group].name
  if label in names:weights[v.index,names.index(label)]=g.weight
report={'source':str(source),'sha256':digest,'entry':e,'object_matrix':[list(row) for row in rig.matrix_world],'body_matrix':[list(row) for row in body.matrix_world],'bind_bounds':[vertices.min(0).tolist(),vertices.max(0).tolist()],'bones':[],'samples':[]}
for i,b in enumerate(rig.data.bones):
 mask=weights[:,i]>.25
 report['bones'].append({'index':i,'name':b.name,'parent':b.parent.name if b.parent else None,'head':list(b.head_local),'tail':list(b.tail_local),'weighted_vertices':int(mask.sum()),'weighted_centroid':np.average(vertices,weights=weights[:,i],axis=0).tolist() if weights[:,i].sum() else None})
rig.data.pose_position='POSE';action=bpy.data.actions[e['actions'][a.clip]];rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0];first,last=action.frame_range
for phase in np.linspace(0,1,9):
 frame=first+(last-first)*phase;s.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update();transforms=[]
 for b in rig.pose.bones:transforms.append({'name':b.name,'head':list(b.head),'tail':list(b.tail),'local_location':list(b.location),'local_quaternion':list(b.rotation_quaternion),'local_scale':list(b.scale),'world_scale':list(b.matrix.to_scale())})
 ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();p=np.array([v.co[:] for v in mesh.vertices]);ev.to_mesh_clear();report['samples'].append({'phase':float(phase),'frame':float(frame),'bones':transforms,'bounds':[p.min(0).tolist(),p.max(0).tolist()]})
(a.out/'rig-inspection.json').write_text(json.dumps(report,indent=2)+'\n');np.savez_compressed(a.out/'bind-skin.npz',vertices=vertices,weights=weights,names=names)
assert hashlib.sha256(source.read_bytes()).hexdigest()==digest
print('Motion inspection saved',str(a.out),flush=True)
