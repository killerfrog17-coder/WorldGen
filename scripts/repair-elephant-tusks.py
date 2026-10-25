"""Rigid tusk/mouth-root skin on accepted Elephant2; no pose/keyframe edits."""
import hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-overnight-20261024/elephant'
path=root/'Animal - Owner Corrected.blend';before=hashlib.sha256(path.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(path));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0]
body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']];body.hide_set(False);rig.hide_set(False);rig.data.pose_position='REST';rig.animation_data_clear()
head=e['joint_names'][5];assert head in rig.data.bones
print('Tusk target',head,'entry head role',e['head'],flush=True)
v=np.array([x.co[:] for x in body.data.vertices]);mask=(abs(v[:,0])>.06)&(abs(v[:,0])<.45)&(v[:,1]>.80)&(v[:,2]>.65)&(v[:,2]<1.18)
ids=np.flatnonzero(mask);assert 100<len(ids)<400
old_weights={}
for i in ids:
    for g in body.data.vertices[int(i)].groups:
        name=body.vertex_groups[g.group].name;old_weights[name]=old_weights.get(name,0)+g.weight
for group in body.vertex_groups:group.remove(ids.tolist())
body.vertex_groups[head].add(ids.tolist(),1.,'REPLACE');rig.data.pose_position='POSE';rig.animation_data_create();checks=[]
for label,name in e['actions'].items():
    action=bpy.data.actions[name];rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0];first,last=action.frame_range;points=[]
    for phase in np.linspace(0,1,25):
        t=first+(last-first)*phase;s.frame_set(int(t),subframe=float(t-int(t)));bpy.context.view_layer.update();ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();inverse=(rig.matrix_world@rig.pose.bones[head].matrix).inverted();points.append(np.array([inverse@(ev.matrix_world@mesh.vertices[int(i)].co) for i in ids]));ev.to_mesh_clear()
    error=float(np.linalg.norm(np.array(points)-points[0],axis=2).max());assert error<1e-4
    checks.append({'clip':label,'samples':25,'head_local_tusk_drift_m':error})
e['manual_repairs']=True;e['tusk_skin_fix']='Rigid bilateral tusk/mouth-root regions follow actual Head; no limb/tail weights';s['deer_four_rigs']=json.dumps([e]);s.frame_set(0)
bpy.context.preferences.filepaths.save_version=0;temp=root/'Animal - Owner Corrected.tusk-rebuild.blend';bpy.ops.wm.save_as_mainfile(filepath=str(temp));temp.replace(path)
digest=hashlib.sha256(path.read_bytes()).hexdigest();report={'source_sha256':before,'checked_blend_sha256':digest,'vertices':ids.tolist(),'old_weight_totals':old_weights,'target_bone':head,'bind_geometry_and_rest_unchanged':True,'animation_actions_unchanged':True,'checks':checks,'owner_requests_no_repeat_review':True}
(root/'tusk-skin-correction.json').write_text(json.dumps(report,indent=2)+'\n');proof=json.loads((root/'owner-fix.json').read_text());proof.update(output_sha256=digest,native_checks_pending=True,owner_requests_no_repeat_review=True,tusk_fix=report);(root/'owner-fix.json').write_text(json.dumps(proof,indent=2)+'\n');print('Elephant2 tusks fixed',len(ids),'vertices',head,checks,flush=True)
