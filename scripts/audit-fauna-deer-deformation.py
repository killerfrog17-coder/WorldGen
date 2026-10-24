"""Compare non-neighbour leg/torso intersections in selected native poses."""
import argparse
import json
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils.bvhtree import BVHTree

ap=argparse.ArgumentParser();ap.add_argument('--before',type=Path,required=True);ap.add_argument('--after',type=Path,required=True)
ap.add_argument('--root',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
cases={name:sorted(set(map(float,[*np.arange(0,last+.01,.5),*np.arange(.25,last,5)])))
       for name,last in [('Walk',28),('Trot',20),('Run',12)]}
cases.update(Eating=[0,20,40,60],Sleep=[0,20,40,59])
def inspect(path,reference_masks=None):
    bpy.ops.wm.open_mainfile(filepath=str(path.resolve()));s=bpy.context.scene;info=json.loads(s['fauna_stage2'])
    rig=bpy.data.objects[info['rig']];body=bpy.data.objects[info['body']]
    bind=np.array([v.co[:] for v in body.data.vertices]);faces=np.array([p.vertices[:] for p in body.data.polygons])
    _,weld=np.unique(np.round(bind,6),axis=0,return_inverse=True);topology=[set(weld[f]) for f in faces]
    centers=bind[faces].mean(1);leg=np.zeros(len(bind));trunk=np.zeros(len(bind))
    for v in body.data.vertices:
        for g in v.groups:
            name=body.vertex_groups[g.group].name
            if name.startswith(('Elbow_','Wrist_','ForeHoof_','Knee_','Hock_','HindHoof_')):leg[v.index]+=g.weight
            if name in ('Pelvis','Spine1','Spine2','Chest'):trunk[v.index]+=g.weight
    leg_faces=leg[faces].mean(1)>.55;trunk_faces=trunk[faces].mean(1)>.60
    if reference_masks is not None:
        assert len(reference_masks[0])==len(faces)
        leg_faces,trunk_faces=reference_masks
    dg=bpy.context.evaluated_depsgraph_get();result={}
    for label,frames in cases.items():
        action=bpy.data.actions[info['actions'][label]];rig.animation_data_clear();rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0]
        rows=[]
        for frame in frames:
            s.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update();evaluated=body.evaluated_get(dg);mesh=evaluated.to_mesh()
            points=[v.co.copy() for v in mesh.vertices];evaluated.to_mesh_clear();tree=BVHTree.FromPolygons(points,faces.tolist(),all_triangles=True)
            count=0
            for first,second in tree.overlap(tree):
                if first>=second:continue
                if not ((leg_faces[first] and trunk_faces[second]) or (trunk_faces[first] and leg_faces[second])):continue
                if topology[first]&topology[second] or np.linalg.norm(centers[first]-centers[second])<.02:continue
                count+=1
            rows.append(dict(frame=frame,nonadjacent_leg_torso_pairs=count))
        result[label]=rows
    return result,(leg_faces,trunk_faces)
before,masks=inspect(a.before);after,_=inspect(a.after,masks)
report=dict(before=before,after=after,masks='Frozen before-skin binding masks; same triangle identities on both models',
    meaning='Selected triangle intersection diagnostics with binding-weight anatomical masks; not universal collision or soft-tissue acceptance')
(root/'deformation-comparison.json').write_text(json.dumps(report,indent=2))
for label in cases:
    old=sum(r['nonadjacent_leg_torso_pairs'] for r in report['before'][label]);new=sum(r['nonadjacent_leg_torso_pairs'] for r in report['after'][label])
    print(label,old,'->',new,'nonadjacent leg/torso triangle pairs',flush=True)
