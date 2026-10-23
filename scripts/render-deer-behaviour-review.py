"""Selected CPU pose views to catch art/retarget failures before publishing."""
from pathlib import Path
import sys
import bpy
from mathutils import Vector

values=sys.argv[sys.argv.index('--')+1:]
root=Path(values[0]).resolve()
bpy.ops.wm.open_mainfile(filepath=str(root/(values[1] if len(values)>1 else 'Deer - Behaviour Prototype.blend')))
scene=bpy.context.scene
rig=bpy.data.objects['AI Deer Rig']
scene.render.engine='CYCLES'
scene.cycles.device='CPU'
scene.cycles.samples=8
scene.render.threads_mode='FIXED'
scene.render.threads=4
scene.render.resolution_x=720
scene.render.resolution_y=540
scene.render.resolution_percentage=100
scene.camera.location=(2.7,-5,2.1)
scene.camera.rotation_euler=(Vector((0,0,.75))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
for name,f in [('Idle',0),('Graze',36),('Drink',24),('Rest',12),('Lie Down',32),
               ('Get Up',30),('Trot',6),('Death',20)]:
    action=bpy.data.actions[name]
    rig.animation_data.action=action
    if action.slots:
        rig.animation_data.action_slot=action.slots[0]
    scene.frame_set(min(int(action.frame_range[1]),f))
    scene.render.filepath=str(root/('pose-'+name.lower().replace(' ','-')+'.png'))
    bpy.ops.render.render(write_still=True)
