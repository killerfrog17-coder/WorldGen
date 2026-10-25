"""Native continuous-pose diagnostics for the new own snake clips."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
repo=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser();p.add_argument('--slug',choices=['python','venomous-snake'],required=True)
a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);root=repo/'artifacts/fauna-overnight-20261024'/a.slug;path=root/'Animal - Motion Revision.blend'
digest=hashlib.sha256(path.read_bytes()).hexdigest();bpy.ops.wm.open_mainfile(filepath=str(path));s=bpy.context.scene;entries=json.loads(s['deer_four_rigs']);rows=[]
for e in entries:
    body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']];body.hide_set(False);rig.hide_set(False);rig.data.pose_position='POSE';rig.animation_data_create()
    v=np.array([x.co[:] for x in body.data.vertices]);edges=np.array([x.vertices[:] for x in body.data.edges]);length=np.linalg.norm(v[edges[:,0]]-v[edges[:,1]],axis=1);valid=length>1e-5;clips=[]
    for label in ('Slither','Side winding'):
        action=bpy.data.actions[e['actions'][label]];rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0];first,last=action.frame_range;poses=[];before=None;first_surface=None;maximum_step=0
        for phase in np.linspace(0,1,97):
            frame=first+(last-first)*phase;s.frame_set(int(frame),subframe=float(frame-int(frame)));bpy.context.view_layer.update();ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();q=np.array([ev.matrix_world@p.co for p in mesh.vertices]);ev.to_mesh_clear();assert np.isfinite(q).all()
            if first_surface is None:first_surface=q.copy()
            if before is not None:maximum_step=max(maximum_step,float(np.linalg.norm(q-before,axis=1).max()))
            before=q;ratio=np.linalg.norm(q[edges[:,0]]-q[edges[:,1]],axis=1)/np.maximum(length,1e-9);distortion=np.maximum(ratio,1/np.maximum(ratio,1e-9))
            poses.append({'phase':float(phase),'floor':float(q[:,2].min()),'p99_edge_distortion':float(np.quantile(distortion[valid],.99)),'max_edge_distortion':float(distortion[valid].max())})
        clips.append({'clip':label,'samples':poses,'loop_surface_error_m':float(np.linalg.norm(first_surface-q,axis=1).max()),'maximum_1_96_cycle_surface_step_m':maximum_step})
    rows.append({'candidate':e['number'],'clips':clips})
(root/'snake-deformation-checks.json').write_text(json.dumps({'checked_blend_sha256':digest,'candidates':rows,'visual_inspection_pending':True,'not_physics_or_natural_motion_approval':True},indent=2)+'\n')
for e in rows:print(a.slug,e['candidate'],[(c['clip'],round(c['loop_surface_error_m'],6),round(max(p['p99_edge_distortion'] for p in c['samples']),3)) for c in e['clips']],flush=True)
