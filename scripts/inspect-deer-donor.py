"""Offline inspection of the already-downloaded CC0 animation control."""
from pathlib import Path
import bpy
import json
import sys

source=Path(sys.argv[-1]).resolve()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
rig.animation_data_clear()
bpy.context.view_layer.update()
print('DONOR',json.dumps({
 'matrix': [list(r) for r in rig.matrix_world],
 'bones': [dict(name=b.name,parent=b.parent.name if b.parent else None,
     deform=b.use_deform,head=list(rig.matrix_world@b.head_local),
     tail=list(rig.matrix_world@b.tail_local)) for b in rig.data.bones],
 'fps':bpy.context.scene.render.fps,
 'actions':[dict(name=a.name,range=list(a.frame_range)) for a in bpy.data.actions]
}))
for name in ('Idle','Walk','Gallop'):
    action=bpy.data.actions[name]
    rig.animation_data_create().action=action
    if action.slots:
        rig.animation_data.action_slot=action.slots[0]
    snapshots=[]
    for f in (int(action.frame_range[0]),int(action.frame_range[1])):
        bpy.context.scene.frame_set(f)
        snapshots.append({b.name:(rig.matrix_world@b.matrix).copy() for b in rig.pose.bones})
    deltas=sorted([(n,(snapshots[0][n].translation-snapshots[1][n].translation).length,
        snapshots[0][n].to_quaternion().rotation_difference(snapshots[1][n].to_quaternion()).angle)
        for n in snapshots[0]],key=lambda v:v[1],reverse=True)
    print('ENDPOINT',name,json.dumps(deltas[:5]))
