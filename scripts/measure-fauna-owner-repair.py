"""Read-only sampled edge diagnostics to choose visual inspection frames.

Rigid-region edge preservation measures skin collapse/stretch only; it is not
joint placement, gait, contact, collision, or art acceptance.
"""
import argparse,json,sys
from pathlib import Path
import bpy,numpy as np
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=repo/'artifacts/fauna-overnight-20261024'/a.slug
evidence=json.loads((root/'owner-fix.json').read_text());number=evidence['candidate']
report={'slug':a.slug,'candidate':number,'samples':{},'not_gait_or_collision_acceptance':True}
for version,filename in [('raw','Animal - Raw Audition.blend'),('corrected','Animal - Owner Corrected.blend')]:
    bpy.ops.wm.open_mainfile(filepath=str(root/filename));scene=bpy.context.scene;e=json.loads(scene['deer_four_rigs'])[number-1]
    body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']];p=np.array([v.co[:] for v in body.data.vertices]);edges=np.array([ed.vertices[:] for ed in body.data.edges]);length=np.linalg.norm(p[edges[:,0]]-p[edges[:,1]],axis=1)
    ids=[i for limb in evidence.get('limbs',[]) for i in limb.get('rigid_vertices',[])]
    if not ids and a.slug=='aardvark':ids=np.flatnonzero((p[:,0]>.10)&(p[:,1]>.40)&(p[:,1]<1.20)&(p[:,2]<.22)).tolist()
    selected=np.isin(edges,ids).all(1)&(length>1e-5);assert selected.sum()>2
    labels=[l for l in ('Walk','Run','Flap') if l in e['actions']]
    clips={}
    for label in labels:
        act=bpy.data.actions[e['actions'][label]];rig.data.pose_position='POSE';rig.animation_data_create().action=act;rig.animation_data.action_slot=act.slots[0];first,last=act.frame_range;frames=[]
        for phase in np.linspace(0,1,25):
            frame=first+(last-first)*phase;scene.frame_set(int(frame),subframe=float(frame-int(frame)));bpy.context.view_layer.update()
            ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();q=np.array([v.co[:] for v in mesh.vertices]);ev.to_mesh_clear();assert np.isfinite(q).all()
            ratio=np.linalg.norm(q[edges[:,0]]-q[edges[:,1]],axis=1)[selected]/length[selected]
            distortion=np.maximum(ratio,1/np.maximum(ratio,1e-9))
            frames.append({'phase':float(phase),'p95_distortion':float(np.percentile(distortion,95)),'max_distortion':float(distortion.max())})
        clips[label]={'poses':frames,'worst_phase':max(frames,key=lambda f:f['p95_distortion'])['phase'],'max_p95':max(f['p95_distortion'] for f in frames),'max_distortion':max(f['max_distortion'] for f in frames)}
    report['samples'][version]=clips
report['inspection_clip']='Run' if 'Run' in report['samples']['raw'] else 'Walk'
report['inspection_phase']=report['samples']['raw'][report['inspection_clip']]['worst_phase']
dest=repo/'artifacts/fauna-owner-fixes-20261025'/a.slug;dest.mkdir(exist_ok=True,parents=True);(dest/'edge-diagnostics.json').write_text(json.dumps(report,indent=2)+'\n')
print(a.slug,report['inspection_clip'],'worst raw phase',report['inspection_phase'],[(label,round(report['samples']['raw'][label]['max_p95'],3),round(report['samples']['corrected'][label]['max_p95'],3)) for label in report['samples']['raw']],flush=True)
