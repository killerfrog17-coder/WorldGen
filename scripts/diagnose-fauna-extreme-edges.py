"""Read-only rest/pose/weight evidence for stretched triangles in native files."""
import argparse,json,sys
from pathlib import Path
import bpy,numpy as np
repo=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser();p.add_argument('--slug',required=True);p.add_argument('--clip',default='Run');p.add_argument('--phase',type=float,default=.9583333333);a=p.parse_args(sys.argv[sys.argv.index('--')+1:])
root=repo/'artifacts/fauna-overnight-20261024'/a.slug;proof=json.loads((root/'owner-fix.json').read_text());number=proof.get('review_model_number',proof['candidate'])
bpy.ops.wm.open_mainfile(filepath=str(root/'Animal - Owner Corrected.blend'));scene=bpy.context.scene;e=json.loads(scene['deer_four_rigs'])[number-1];body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']]
action=bpy.data.actions[e['actions'][a.clip]];rig.data.pose_position='POSE';rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0];first,last=action.frame_range;frame=first+(last-first)*a.phase;scene.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()
v=np.array([x.co[:] for x in body.data.vertices]);ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());m=ev.to_mesh();q=np.array([x.co[:] for x in m.vertices]);ev.to_mesh_clear();edges=np.array([ed.vertices[:] for ed in body.data.edges]);ratio=np.linalg.norm(q[edges[:,0]]-q[edges[:,1]],axis=1)/np.maximum(np.linalg.norm(v[edges[:,0]]-v[edges[:,1]],axis=1),1e-6)
result=[]
for index in np.argsort(ratio)[-12:][::-1]:
    pair=[]
    for i in edges[index]:pair.append({'vertex':int(i),'rest':v[i].tolist(),'posed':q[i].tolist(),'skin':{body.vertex_groups[g.group].name:g.weight for g in body.data.vertices[i].groups}})
    result.append({'ratio':float(ratio[index]),'vertices':pair})
dest=repo/'artifacts/fauna-owner-fixes-20261025'/a.slug/'extreme-edges.json';dest.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result[:4],indent=2),flush=True)
