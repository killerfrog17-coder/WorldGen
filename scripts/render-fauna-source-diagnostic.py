"""CPU inspection of a licensed source in place; never export donor geometry."""
import argparse,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Vector

ap=argparse.ArgumentParser();ap.add_argument('--bank',type=Path,required=True);ap.add_argument('--clip',default='Run');ap.add_argument('--phase',type=float,default=.33);ap.add_argument('--output',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);data=json.loads((a.bank/'source-rig.json').read_text());source=Path(data['source']);assert source.suffix=='.blend'
bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene
rig=max((o for o in s.objects if o.type=='ARMATURE'),key=lambda o:len(o.data.bones))
for collection in bpy.data.collections:collection.hide_viewport=False;collection.hide_render=False
def reveal(layer):
    layer.exclude=False;layer.hide_viewport=False
    for child in layer.children:reveal(child)
reveal(bpy.context.view_layer.layer_collection);rig.hide_viewport=False;rig.hide_set(False)
clip=next(c for c in data['clips'] if c['label']==a.clip);action=bpy.data.actions[clip['source_action']]
rig.animation_data_clear();rig.animation_data_create().action=action
if action.slots:rig.animation_data.action_slot=action.slots[0]
rig.data.pose_position='POSE';first,last=action.frame_range;x=first+(last-first)*a.phase;s.frame_set(int(x),subframe=x-int(x));bpy.context.view_layer.update()
meshes=[o for o in s.objects if o.type=='MESH' and any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers)];assert meshes
points=[]
for o in s.objects:
    if o.type=='MESH':o.hide_render=o not in meshes
for o in meshes:
    o.hide_set(False);ev=o.evaluated_get(bpy.context.evaluated_depsgraph_get());m=ev.to_mesh();points.extend([ev.matrix_world@v.co for v in m.vertices]);ev.to_mesh_clear()
p=np.asarray(points);center=Vector(((p.min(0)+p.max(0))*.5).tolist());span=float(np.ptp(p,axis=0).max())
for o in list(s.objects):
    if o.type in ('CAMERA','LIGHT'):bpy.data.objects.remove(o,do_unlink=True)
# Rabbit source faces -Y, our canonical auditions +Y: opposite X gives the
# matching side profile without modifying the source model or its animation.
cam=bpy.data.objects.new('Source diagnostic camera',bpy.data.cameras.new('Source diagnostic camera'));s.collection.objects.link(cam);cam.location=center+Vector((-1,0,.08)).normalized()*span*2;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=span*1.22;s.camera=cam
for position,power in [((-3,-4,5),650),((3,-1,3),350),((0,4,4),450)]:
    light=bpy.data.lights.new('Source diagnostic softbox','AREA');light.energy=power;light.shape='DISK';light.size=span*2;obj=bpy.data.objects.new(light.name,light);s.collection.objects.link(obj);obj.location=center+Vector(position)*span;obj.rotation_euler=(center-obj.location).to_track_quat('-Z','Y').to_euler()
world=bpy.data.worlds.new('Source diagnostic background');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.19,.21,.23,1);world.node_tree.nodes['Background'].inputs[1].default_value=.65;s.world=world
material=bpy.data.materials.new('Source diagnostic neutral');material.diffuse_color=(.4,.38,.34,1);s.view_layers[0].material_override=material
s.use_nodes=False;s.render.film_transparent=False;s.view_settings.view_transform='Standard';s.view_settings.exposure=0;s.view_settings.gamma=1
s.render.engine='CYCLES';s.cycles.device='CPU';s.cycles.samples=6;s.cycles.use_denoising=True;s.render.resolution_x=s.render.resolution_y=448;s.render.resolution_percentage=100
a.output.parent.mkdir(parents=True,exist_ok=True);s.render.filepath=str(a.output.resolve());s.render.image_settings.file_format='PNG';bpy.ops.render.render(write_still=True)
print('Source CPU pose inspected without saving or exporting donor geometry:',a.clip,a.phase,a.output,flush=True)
