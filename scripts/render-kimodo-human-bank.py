"""Render a sampled whole-sequence contact set on the real native character."""
import argparse
import json
from pathlib import Path
import sys
import bpy
from mathutils import Vector

REPO=Path(__file__).resolve().parent.parent
ROOT=REPO/'artifacts/kimodo-human-bank'
parser=argparse.ArgumentParser();parser.add_argument('--variant',default='B');parser.add_argument('--roles',default='');parser.add_argument('--steps',type=int,default=8)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
bpy.ops.wm.open_mainfile(filepath=str(ROOT/('Kimodo bank '+args.variant+'.blend')))
scene=bpy.context.scene
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
clips=json.loads(rig['kimodo_bank_clips'])
for obj in bpy.data.objects:
    obj.hide_set(False);obj.hide_render=False
    if obj.type=='ARMATURE':obj.hide_render=True
    for modifier in obj.modifiers:
        if modifier.type=='SUBSURF':modifier.show_render=False;modifier.show_viewport=False
bpy.ops.mesh.primitive_plane_add(size=20)
floor=bpy.context.object;mat=bpy.data.materials.new('Review floor');mat.diffuse_color=(.18,.2,.16,1);floor.data.materials.append(mat)
bpy.ops.object.camera_add(location=(3,-5,2.6));camera=bpy.context.object
camera.rotation_euler=(Vector((0,0,.95))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.lens=58;scene.camera=camera
bpy.ops.object.light_add(type='AREA',location=(2,-4,6));light=bpy.context.object;light.data.energy=550;light.data.shape='DISK';light.data.size=5
light.rotation_euler=(Vector((0,0,1))-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.light_add(type='SUN');bpy.context.object.rotation_euler=(.4,-.3,-.5);bpy.context.object.data.energy=1.2
scene.render.engine='BLENDER_EEVEE_NEXT';scene.render.resolution_x=384;scene.render.resolution_y=512;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
folder=ROOT/'review';folder.mkdir(exist_ok=True)
records=[]
for clip in clips:
    if clip.get('mirrored'):continue
    if args.roles and clip['role'] not in args.roles.split(','):continue
    action=bpy.data.actions[clip['action']];rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
    for bone in rig.pose.bones:bone.location=(0,0,0);bone.scale=(1,1,1)
    for i in range(args.steps):
        frame=1+round((clip['frames']-1)*i/(args.steps-1));scene.frame_set(frame);bpy.context.view_layer.update()
        name=f'{args.variant}-{clip["role"]}-{i:02d}.png';scene.render.filepath=str(folder/name);bpy.ops.render.render(write_still=True)
        records.append({'role':clip['role'],'frame':frame,'file':name})
    print('KIMODO REVIEW',clip['role'],flush=True)
(folder/('index-'+args.variant+'.json')).write_text(json.dumps(records,indent=2))
