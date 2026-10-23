"""CPU pose inspection and unchanged-asset checks for the original-rig trial."""
import argparse
import importlib.util
import json
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Vector

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
bpy.ops.wm.open_mainfile(filepath=str(root/'Deer - Original Rig Motion Review.blend'))
scene=bpy.context.scene;info=json.loads(scene['own_rig_review'])
rig=bpy.data.objects[info['rig']];mesh=bpy.data.objects[info['mesh']]
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=12
scene.render.threads_mode='FIXED';scene.render.threads=3
scene.render.resolution_x=700;scene.render.resolution_y=520;scene.render.resolution_percentage=100
F=Vector((-.91514678,-.40312079,0));R=Vector((.40312079,-.91514678,0))
center=Vector((0,0,.8));scene.camera.location=center+R*3.5+F*.4+Vector((0,0,.35))
scene.camera.rotation_euler=(center-scene.camera.location).to_track_quat('-Z','Y').to_euler()
scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=2.75
checks=[]
for bank in ('original','transferred'):
    scene.render.fps=info[bank+'_fps'];rig.data.pose_position='POSE'
    for clip,name in info[bank].items():
        rig.animation_data_clear();rig.animation_data_create().action=bpy.data.actions[name]
        rig.animation_data.action_slot=rig.animation_data.action.slots[0]
        action=rig.animation_data.action;lo,hi=map(int,action.frame_range)
        low=[];previous=None;steps=[]
        for frame in range(lo,hi+1):
            scene.frame_set(frame)
            ev=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
            points=np.empty(len(ev.data.vertices)*3,np.float32)
            ev.data.vertices.foreach_get('co',points);points=points.reshape(-1,3)
            points=points@np.array(ev.matrix_world.to_3x3()).T+np.array(ev.matrix_world.translation)
            assert np.isfinite(points).all()
            low.append(float(points[:,2].min()))
            if previous is not None:steps.append(float(np.linalg.norm(points-previous,axis=1).max()))
            previous=points
            if bank=='transferred':
                for n in ('Ear_L','Ear_R','Jaw','Muzzle_L','Muzzle_R'):
                    assert max(abs(rig.pose.bones[n].matrix_basis[i][j]-(i==j))
                        for i in range(4) for j in range(4))<1e-5,(clip,n)
        checks.append(dict(bank=bank,clip=clip,frames=hi-lo+1,min_height=min(low),max_vertex_step=max(steps,default=0)))
        if clip not in ('Idle','Walk','Run'):continue
        for phase in ((0,.25,.5,.75) if bank=='transferred' and clip=='Walk' else (.25,)):
            scene.frame_set(round(lo+(hi-lo)*phase))
            scene.render.filepath=str(root/f'{bank}-{clip}-{phase}.png');bpy.ops.render.render(write_still=True)
    if bank=='transferred':
        groups={g.name:g.index for g in mesh.vertex_groups};ids={groups[n] for n in ('Head','Jaw','Muzzle_L','Muzzle_R')}
        face=[v.index for v in mesh.data.vertices if sum(g.weight for g in v.groups if g.group in ids)>.5]
        rest=np.array([v.co for v in mesh.data.vertices]);center=Vector(rest[face].mean(0))
        scene.camera.location=center+(F*2+R*1.2).normalized()*3
        scene.camera.rotation_euler=(center-scene.camera.location).to_track_quat('-Z','Y').to_euler()
        scene.camera.data.ortho_scale=.85;scene.render.resolution_x=640;scene.render.resolution_y=640
        rig.animation_data.action=bpy.data.actions[info[bank]['Idle']]
        rig.animation_data.action_slot=rig.animation_data.action.slots[0];scene.frame_set(0)
        scene.render.filepath=str(root/'own-rig-transferred-idle-face.png');bpy.ops.render.render(write_still=True)
(root/'pose-checks.json').write_text(json.dumps(dict(clips=checks,no_game_launch=True,
    unchanged_face_local_bones=True,natural_gait_accepted=False),indent=2))
print('Original-rig pose checks and renders ready.',flush=True)
