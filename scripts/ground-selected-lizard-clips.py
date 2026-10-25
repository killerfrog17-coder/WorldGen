"""Correct measured clip ground clearance on the owner-approved lizards.

Only root translation changes. Sample actual skinned surface every half frame
on native clips and existing blended demos independently. No rig/skin edits.
"""
import argparse, hashlib, json, runpy, sys
from pathlib import Path
import bpy, numpy as np

repo=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser();p.add_argument('--slug',choices=['iguana','monitor-lizard'],required=True)
a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);root=repo/'artifacts/fauna-overnight-20261024'/a.slug
path=root/'Animal - Owner Corrected.blend'
original=hashlib.sha256(path.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(path));s=bpy.context.scene
entries=json.loads(s['deer_four_rigs']);assert len(entries)==1;e=entries[0]
rig=bpy.data.objects[e['rig']];body=bpy.data.objects[e['body']]
body.hide_set(False);rig.hide_set(False)
root_bone=next(b for b in rig.data.bones if b.parent is None);name=root_bone.name
R=np.asarray(root_bone.matrix_local.to_3x3());up=R.T@np.array([0.,0.,1.])
labels={**e['actions'],**e['demos']};proof={'source_sha256':original,'target_floor_m':.005,'actions':{}}
rig.animation_data_create();rig.data.pose_position='POSE'
for label,action_name in labels.items():
    action=bpy.data.actions[action_name];rig.animation_data.action=action
    if action.slots:rig.animation_data.action_slot=action.slots[0]
    first,last=action.frame_range;times=np.unique(np.r_[np.arange(first,last+.01,.5),last])
    rows=[]
    for frame in times:
        s.frame_set(int(frame),subframe=float(frame-int(frame)));bpy.context.view_layer.update()
        ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
        floor=min((ev.matrix_world@v.co).z for v in mesh.vertices);ev.to_mesh_clear()
        correction=.005-floor
        assert abs(correction)<.90,(label,frame,floor,'Clearance exceeds half the review height')
        rows.append((float(frame),np.asarray(rig.pose.bones[name].location)+up*correction,floor))
    for frame,location,_ in rows:
        s.frame_set(int(frame),subframe=float(frame-int(frame)));bone=rig.pose.bones[name];bone.location=location
        bone.keyframe_insert(data_path='location',frame=frame,group=name)
    for layer in action.layers:
        for strip in layer.strips:
            bag=strip.channelbag(action.slots[0])
            if bag:
                for curve in bag.fcurves:
                    if curve.data_path==f'pose.bones["{name}"].location':
                        for key in curve.keyframe_points:key.interpolation='LINEAR'
    floors=[]
    for frame in np.unique(np.r_[times,np.arange(first,last+.01,.25)]):
        s.frame_set(int(frame),subframe=float(frame-int(frame)));bpy.context.view_layer.update()
        ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
        floors.append(min((ev.matrix_world@v.co).z for v in mesh.vertices));ev.to_mesh_clear()
    proof['actions'][label]={'samples':len(rows),'before_floor_min':min(r[2] for r in rows),'before_floor_max':max(r[2] for r in rows),'after_floor_min':min(floors),'after_floor_max':max(floors)}
    assert min(floors)>-.003 and max(floors)<.012,'Clearance interpolation still needs correction'
e['ground_clearance_correction']='Root translation from actual skinned surface, quarter-frame clearance checked; natural stance height retained'
s['deer_four_rigs']=json.dumps(entries);s.frame_set(0);bpy.context.preferences.filepaths.save_version=0
out=root/'Animal - Owner Corrected.ground-rebuild.blend';bpy.ops.wm.save_as_mainfile(filepath=str(out));out.replace(path)
proof['checked_blend_sha256']=hashlib.sha256(path.read_bytes()).hexdigest()
(root/'owner-ground-clearance.json').write_text(json.dumps(proof,indent=2)+'\n')
fix=json.loads((root/'owner-fix.json').read_text());fix.update(output_sha256=proof['checked_blend_sha256'],native_checks_pending=True,owner_requests_no_repeat_review=True,ground_clearance=proof['actions'])
(root/'owner-fix.json').write_text(json.dumps(fix,indent=2)+'\n')
print(a.slug,json.dumps(proof['actions']),flush=True)
