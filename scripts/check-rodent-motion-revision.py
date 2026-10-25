"""Inspect full-loop deformed surfaces and choose worst frames for visual review.

Edge/rigid-paw diagnostics are deformation checks, not gait/collision approval.
"""
import argparse, hashlib, json, sys
from pathlib import Path
import bpy, numpy as np
repo=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser();p.add_argument('--slug',choices=['nutria','porcupine','echidna'],required=True)
a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);root=repo/'artifacts/fauna-overnight-20261024'/a.slug
path=root/'Animal - Motion Revision.blend';proof=json.loads((root/'rodent-motion-revision.json').read_text())
assert proof['output_sha256']==hashlib.sha256(path.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(path));scene=bpy.context.scene;entries=json.loads(scene['deer_four_rigs']);result=[]
for e,detail in zip(entries,proof['candidates']):
    body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']];body.hide_set(False);rig.hide_set(False);rig.data.pose_position='POSE';rig.animation_data_create()
    vertices=np.array([v.co[:] for v in body.data.vertices]);edges=np.array([edge.vertices[:] for edge in body.data.edges]);length=np.linalg.norm(vertices[edges[:,0]]-vertices[edges[:,1]],axis=1);usable=length>1e-4
    masks=[]
    for paw in detail['rigid_paws']:masks.append(np.isin(edges,paw['vertices']).all(1)&usable)
    phases=[]
    for label in ('Idle','Walk','Run'):
        action=bpy.data.actions[e['actions'][label]];rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0];first,last=action.frame_range
        samples=[]
        for phase in np.linspace(0,1,49):
            frame=first+(last-first)*phase;scene.frame_set(int(frame),subframe=float(frame-int(frame)));bpy.context.view_layer.update()
            ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();posed=np.array([ev.matrix_world@v.co for v in mesh.vertices]);ev.to_mesh_clear();assert np.isfinite(posed).all()
            ratio=np.linalg.norm(posed[edges[:,0]]-posed[edges[:,1]],axis=1)/np.maximum(length,1e-9)
            if e['number']==1 and label=='Run' and abs(phase-.5)<1e-6:
                for vi in np.argsort(posed[:,2])[:6]:
                    print('LOW VERTEX',int(vi),'rest',vertices[vi].round(3).tolist(),'posed',posed[vi].round(3).tolist(),'weights',[(body.vertex_groups[g.group].name,round(g.weight,3)) for g in body.data.vertices[vi].groups],flush=True)
            strain=np.maximum(ratio,1/np.maximum(ratio,1e-9));worst=int(np.argmax(np.where(usable,strain,0)))
            paws=[float(strain[mask].max()) if mask.any() else 1 for mask in masks]
            samples.append({'phase':float(phase),'surface_min_z':float(posed[:,2].min()),'max_edge_distortion':float(strain[worst]),'worst_edge':edges[worst].tolist(),'p99_edge_distortion':float(np.quantile(strain[usable],.99)),'rigid_paw_max_distortion':paws})
        phases.append({'clip':label,'samples':samples,'worst_phase':max(samples,key=lambda x:x['max_edge_distortion'])['phase']})
    result.append({'candidate':detail['candidate'],'clips':phases})
report={'checked_blend_sha256':proof['output_sha256'],'candidates':result,'visual_inspection_pending':True,'not_gait_or_collision_approval':True}
(root/'rodent-deformation-checks.json').write_text(json.dumps(report,indent=2)+'\n')
for row in result:
    print(a.slug,row['candidate'],[(x['clip'],round(max(p['max_edge_distortion'] for p in x['samples']),2),round(max(p['p99_edge_distortion'] for p in x['samples']),2),x['worst_phase']) for x in row['clips']],flush=True)
