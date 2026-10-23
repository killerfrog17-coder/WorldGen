"""Compare true bind vs idle on the existing and replacement rig, CPU only."""
import argparse
import json
import math
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Matrix,Vector

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
ap.add_argument('--source',type=Path,required=True);ap.add_argument('--replacement',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve();root.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(a.source.resolve()))
scene=bpy.context.scene;rig=bpy.data.objects['AI Deer Rig'];mesh=bpy.data.objects['Locally generated deer']
rig.animation_data_clear();rig.parent=None;rig.matrix_world.identity()
for pb in rig.pose.bones:pb.matrix_basis.identity()
for obj in scene.objects:
    if obj.type=='MESH':obj.hide_render=obj!=mesh
body=np.array([v.co for v in mesh.data.vertices])
groups={g.name:g.index for g in mesh.vertex_groups}
face_ids={groups[n] for n in ('Head','Jaw','Muzzle_L','Muzzle_R')}
selected=[v.index for v in mesh.data.vertices if sum(g.weight for g in v.groups if g.group in face_ids)>.5]
center=Vector(body[selected].mean(0))
F=Vector((-.91514678,-.40312079,0));R=Vector((.40312079,-.91514678,0));Z=Vector((0,0,1))
scene.camera.location=center+(F*2+R*1.2).normalized()*3
scene.camera.rotation_euler=(center-scene.camera.location).to_track_quat('-Z','Y').to_euler()
scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=.85
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=16
scene.render.threads_mode='FIXED';scene.render.threads=3
scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100
def render(name):
    scene.render.filepath=str(root/(name+'.png'));bpy.ops.render.render(write_still=True)
rig.data.pose_position='REST';render('own-rig-bind-face')
rig.data.pose_position='POSE';rig.animation_data_create().action=bpy.data.actions['Idle']
rig.animation_data.action_slot=rig.animation_data.action.slots[0];scene.frame_set(0)
render('own-rig-idle-face')
mesh.hide_render=True
before=set(bpy.data.objects);old_actions=set(bpy.data.actions)
bpy.ops.import_scene.gltf(filepath=str(a.replacement.resolve()))
objects=set(bpy.data.objects)-before
newrig=next(o for o in objects if o.type=='ARMATURE')
newmesh=next(o for o in objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers))
newrig.animation_data_clear()
for pb in newrig.pose.bones:pb.matrix_basis.identity()
rotation=Matrix.Rotation(math.atan2(F.y,F.x)+math.pi/2,4,'Z')
for obj in objects:
    if obj.parent not in objects:obj.matrix_world=rotation@obj.matrix_world
    if obj.type=='MESH':obj.hide_render=obj!=newmesh
newrig.data.pose_position='REST';render('replacement-bind-face')
newrig.data.pose_position='POSE'
idle=next(x for x in set(bpy.data.actions)-old_actions if x.name.split('.')[0]=='Idle')
newrig.animation_data_create().action=idle;newrig.animation_data.action_slot=idle.slots[0]
scene.render.fps=30;scene.frame_set(int(idle.frame_range[0]));render('replacement-idle-face')
(root/'face-comparison.json').write_text(json.dumps({'source':str(a.source.resolve()),
    'replacement':str(a.replacement.resolve()),'same_camera_lighting':True,
    'no_source_file_saved':True,'game_launched':False},indent=2))
print('CPU face comparison ready.',flush=True)
