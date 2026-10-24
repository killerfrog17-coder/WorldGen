"""Offline native interpolation contact bake for an unopened review candidate."""
import argparse
import json
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0,str(Path(__file__).parent))
from deer_behaviour_core import Skeleton

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
ap.add_argument('--replace-unopened',action='store_true',required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
path=root/'Deer - Stage2 Animation Review.blend'
bpy.ops.wm.open_mainfile(filepath=str(path));s=bpy.context.scene
info=json.loads(s['fauna_stage2']);assert info.get('pose_corrections')
rig=bpy.data.objects[info['rig']];body=bpy.data.objects[info['body']]
skel=Skeleton(info['skeleton']);pelvis=skel.index['Pelvis'];dg=bpy.context.evaluated_depsgraph_get()
report={};coordinates=np.empty(len(body.data.vertices)*3,dtype=np.float32)
for label in ('Walk','Trot','Run'):
    action=bpy.data.actions[info['actions'][label]]
    rig.animation_data_clear();rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0]
    bag=action.layers[0].strips[0].channelbag(action.slots[0])
    curves=[next(c for c in bag.fcurves if c.data_path=='pose.bones["Pelvis"].location' and c.array_index==axis) for axis in range(3)]
    first,last=action.frame_range;inserted=0;maximum=0
    for subdivisions in (32,64):
        for time in np.arange(first,last+1e-6,1/subdivisions):
            s.frame_set(int(time),subframe=time%1);evaluated=body.evaluated_get(dg);mesh=evaluated.to_mesh()
            mesh.vertices.foreach_get('co',coordinates);evaluated.to_mesh_clear()
            lift=.003-float(coordinates[2::3].min())
            if lift<.0001:continue
            if lift>.04:raise ValueError((label,float(time),'contact bake needs excessive lift',lift))
            globals_m=[rig.pose.bones[name].matrix.copy() for name in skel.names]
            old=skel.basis(globals_m)[0][pelvis]
            skel.translate_all(globals_m,Vector((0,0,lift)))
            delta=skel.basis(globals_m)[0][pelvis]-old
            values=rig.pose.bones['Pelvis'].location.copy()
            for axis,curve in enumerate(curves):
                key=curve.keyframe_points.insert(float(time),float(values[axis]+delta[axis]),options={'FAST'})
                key.interpolation='LINEAR';curve.update()
            action.update_tag();inserted+=1;maximum=max(maximum,lift)
    report[label]={'corrected_samples':inserted,'max_increment_m':maximum}
    print(label,report[label],flush=True)
rig.animation_data.action=bpy.data.actions[info['actions']['Idle']]
rig.animation_data.action_slot=rig.animation_data.action.slots[0];s.frame_set(0)
info['native_contact_bake']=report;s['fauna_stage2']=json.dumps(info)
bpy.ops.wm.save_as_mainfile(filepath=str(path))
(root/'native-contact-bake.json').write_text(json.dumps(report,indent=2))
