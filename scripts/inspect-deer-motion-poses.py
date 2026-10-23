"""Small numerical inspection of source clips before choosing motion segments."""
import json
from pathlib import Path
import sys
import bpy

root=Path(sys.argv[-1]).resolve()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.render.fps=24
bpy.ops.import_scene.gltf(filepath=str(root/'sources/horse-animations.glb'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
rig.animation_data_clear()
for name in ('Sleep','lay_to_idle','Turn_Left','Turn_Right','Eating','Trot'):
    a=bpy.data.actions[name]
    rig.animation_data_create().action=a
    if a.slots:
        rig.animation_data.action_slot=a.slots[0]
    samples=[]
    for u in (0,.25,.5,.75,1):
        f=a.frame_range[0]+u*(a.frame_range[1]-a.frame_range[0])
        bpy.context.scene.frame_set(int(f),subframe=f-int(f))
        samples.append({n:list(rig.matrix_world@rig.pose.bones[n].head) for n in
            ('hips','head','nose','front_leg_foot_l','back_leg_foot_l')})
    print('POSES',name,json.dumps(samples),flush=True)
