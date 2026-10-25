"""Read-only geometry/skin/pose diagnostics for requested selected raw repairs."""
import argparse,json,sys
from pathlib import Path
import bpy,bmesh,numpy as np
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--candidate',type=int,default=2)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);raw=repo/'artifacts/fauna-overnight-20261024'/a.slug
root=repo/'artifacts/fauna-owner-fixes-20261025'/a.slug;root.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(raw/'Animal - Raw Audition.blend'));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[a.candidate-1]
body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']];rig.animation_data_clear();rig.data.pose_position='REST'
p=np.array([v.co[:] for v in body.data.vertices]);faces=np.array([f.vertices[:] for f in body.data.polygons]);names=e['joint_names'];w=np.zeros((len(p),len(names)))
for v in body.data.vertices:
    for g in v.groups:
        name=body.vertex_groups[g.group].name
        if name in names:w[v.index,names.index(name)]=g.weight
np.savez_compressed(root/'repair-baseline.npz',vertices=p,faces=faces,weights=w,joints=np.array(e['generated_joint_heads']),parents=np.array(e['parents']))
bm=bmesh.new();bm.from_mesh(body.data);bm.verts.ensure_lookup_table();boundary=[edge for edge in bm.edges if edge.is_boundary];remaining=set(boundary);loops=[]
while remaining:
    edge=remaining.pop();component={edge};stack=[edge]
    while stack:
        for v in stack.pop().verts:
            for other in v.link_edges:
                if other in remaining:remaining.remove(other);component.add(other);stack.append(other)
    vertices={v for edge in component for v in edge.verts};q=np.array([v.co[:] for v in vertices]);loops.append({'edges':len(component),'vertices':[v.index for v in vertices],'min':q.min(0).tolist(),'max':q.max(0).tolist(),'mean':q.mean(0).tolist(),'perimeter':sum(edge.calc_length() for edge in component)})
report={'slug':a.slug,'candidate':a.candidate,'body_bounds':[p.min(0).tolist(),p.max(0).tolist()],'boundary_components':loops}
if a.slug=='aardvark':
    paw=(p[:,0]>.10)&(p[:,1]>.40)&(p[:,1]<1.20)&(p[:,2]<.43)
    report['right_paw_vertices']=np.flatnonzero(paw).tolist();report['paw_weight_totals']={name:float(w[paw,i].sum()) for i,name in enumerate(names) if w[paw,i].sum()>.001}
    action=bpy.data.actions[e['actions']['Run']];rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0];rig.data.pose_position='POSE';first,last=action.frame_range;poses=[]
    for frame in np.linspace(first,last,17):
        s.frame_set(int(frame),subframe=float(frame-int(frame)));bpy.context.view_layer.update();ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());q=np.array([v.co[:] for v in ev.data.vertices]);poses.append({'frame':float(frame),'paw_min':q[paw].min(0).tolist(),'paw_max':q[paw].max(0).tolist(),'fore':{name:{'head':list(rig.pose.bones[name].head),'tail':list(rig.pose.bones[name].tail),'quaternion':list(rig.pose.bones[name].matrix.to_quaternion())} for name in ['Joint_12','Joint_13','Joint_14']}})
    report['run_poses']=poses
(root/'repair-inspection.json').write_text(json.dumps(report,indent=2)+'\n');print('REPAIR INSPECTION',a.slug,'boundary loops',len(loops),'report',root/'repair-inspection.json',flush=True)
