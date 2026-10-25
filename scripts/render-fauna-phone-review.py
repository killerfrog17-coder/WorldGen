"""Render the retained approved deer without modifying its native file."""
import argparse, hashlib, json, math, sys, time
from pathlib import Path
import bpy
from mathutils import Vector

ap=argparse.ArgumentParser()
ap.add_argument('--output',type=Path,required=True)
ap.add_argument('--stills-only',action='store_true')
args=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
repo=Path(__file__).resolve().parent.parent
source=repo/'artifacts/fauna-assets/deer/Deer - Prototype.blend'
digest=hashlib.sha256(source.read_bytes()).hexdigest()
assert digest=='0e2ff46b0b0860750084bed3af94d84f636144230550e9e80f753b3e415418a1'
out=args.output.resolve();out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source))
s=bpy.context.scene;entry=json.loads(s['deer_four_rigs'])[0]
rig=bpy.data.objects[entry['rig']];body=bpy.data.objects[entry['body']]
art=entry['approved_art'];rig.hide_render=True
for obj in list(s.objects):
    if obj.type in {'CAMERA','LIGHT'}:bpy.data.objects.remove(obj,do_unlink=True)
for name in entry['objects']:
    bpy.data.objects[name].hide_set(False);bpy.data.objects[name].hide_render=False

def appearance(coat=0,rack=2):
    profile=art['coats'][coat];body.data.materials[0]=bpy.data.materials[profile['material']]
    keys=body.data.shape_keys.key_blocks
    for name,value in [('Compact ears',max(0,(1-profile['ear_scale'])/.28)),('Large ears',max(0,(profile['ear_scale']-1)/.08)),('Short tail',max(0,(1-profile['tail_scale'])/.30)),('Long tail',max(0,(profile['tail_scale']-1)/.20))]:keys[name].value=value
    for i,row in enumerate(art['antlers']):
        obj=bpy.data.objects[row['object']];obj.hide_render=i!=rack;obj.hide_set(i!=rack)

def pose(clip,frame):
    rig.data.pose_position='POSE'
    action=bpy.data.actions[entry['actions'][clip]]
    rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0]
    s.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()

appearance();pose('Idle',0)
camera=bpy.data.objects.new('Phone review camera',bpy.data.cameras.new('Phone review camera'))
s.collection.objects.link(camera);camera.data.type='ORTHO';s.camera=camera
def view(direction=(1,0,.12),center=(0,0,.93),span=2.55):
    center=Vector(center);camera.location=center+Vector(direction).normalized()*5
    camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale=span

for position,power,size in [((3,4,5),600,5),((-3,1,3),350,4),((0,-4,4),450,3)]:
    data=bpy.data.lights.new('Phone softbox','AREA');data.energy=power;data.shape='DISK';data.size=size
    obj=bpy.data.objects.new(data.name,data);s.collection.objects.link(obj);obj.location=position
    obj.rotation_euler=(Vector((0,0,.8))-obj.location).to_track_quat('-Z','Y').to_euler()
world=bpy.data.worlds.new('Phone neutral background');world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.18,.20,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.65;s.world=world
s.render.engine='CYCLES';s.cycles.device='CPU';s.cycles.samples=12;s.cycles.use_denoising=True
s.render.threads_mode='FIXED';s.render.threads=4
s.render.resolution_x=960;s.render.resolution_y=720;s.render.resolution_percentage=100
s.render.image_settings.file_format='PNG'
stills=[('A-side',(1,0,.10),(0,0,1.0),3.0,0,2),
        ('B-front',(0,1,.08),(0,0,1.0),3.0,0,2),
        ('C-rear',(0,-1,.08),(0,0,1.0),3.0,0,2),
        ('D-face',(1,.8,.12),(0,.80,1.40),.84,0,2),
        ('E-spotted',(1,0,.10),(0,0,1.0),3.0,1,-1),
        ('F-striped',(1,.55,.10),(0,0,1.0),3.0,5,1)]
receipt={'source_sha256':digest,'stills':[],'videos':[],'source_modified':False}
for label,direction,center,span,coat,rack in stills:
    appearance(coat,rack);pose('Idle',0);view(direction,center,span)
    s.render.filepath=str(out/(label+'.png'));bpy.ops.render.render(write_still=True)
    receipt['stills'].append({'name':label,'file':label+'.png','coat':art['coats'][coat]['label'],'rack':rack,'sha256':hashlib.sha256(Path(s.render.filepath).read_bytes()).hexdigest()})
    print('STILL',label,flush=True)
(out/'render-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
if not args.stills_only:
    # Eevee is used only for a lightweight preview, not model inference or baking.
    s.render.engine='BLENDER_EEVEE_NEXT';s.eevee.taa_render_samples=16
    s.render.resolution_x=640;s.render.resolution_y=480;s.render.fps=24
    s.render.image_settings.file_format='FFMPEG';s.render.ffmpeg.format='MPEG4'
    s.render.ffmpeg.codec='H264';s.render.ffmpeg.constant_rate_factor='MEDIUM';s.render.ffmpeg.ffmpeg_preset='GOOD'
    appearance(0,2)
    # Render exact existing action keys at the scene's native time; never interpolate
    # an AI-generated image or alter the source rig, skin weights or action curves.
    for clip,seconds,direction in [('Walk',3,(1,0,.10)),('Run',3,(1,.45,.13)),('Eating',6,(1,.35,.13))]:
        action=bpy.data.actions[entry['actions'][clip]];first,last=map(float,action.frame_range)
        rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0]
        # A temporary scene strip remaps repeated cycles at native speed. This is
        # a preview-only action copy in memory; the approved file is never saved.
        temp=action.copy();temp.name='Phone preview '+clip
        rig.animation_data.action=temp;rig.animation_data.action_slot=temp.slots[0]
        for layer in temp.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        mod=curve.modifiers.new('CYCLES');mod.mode_before='REPEAT';mod.mode_after='REPEAT'
        view(direction,(0,0,1.0),3.05)
        s.frame_start=int(first);s.frame_end=s.frame_start+int(seconds*24)-1
        s.render.filepath=str(out/(clip.lower()+'.mp4'))
        print('VIDEO',clip,'frames',s.frame_start,s.frame_end,flush=True)
        bpy.ops.render.render(animation=True)
        path=Path(s.render.filepath)
        receipt['videos'].append({'file':path.name,'clip':clip,'seconds':seconds,'fps':24,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
        (out/'render-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
assert hashlib.sha256(source.read_bytes()).hexdigest()==digest
print('Approved deer preserved; media ready',flush=True)
