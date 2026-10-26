"""Render multiple actual run phases before/after without saving either native."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Vector
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--clip',default='Run');a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);repo=Path(__file__).resolve().parent.parent;folder=repo/'artifacts/fauna-repairs'/a.slug;record=json.loads((folder/'run-repair.json').read_text());dest=folder/('contact' if a.clip=='Run' else 'contact-'+a.clip.lower());dest.mkdir(exist_ok=True)
for version,path in [('before',record['source']),('after',record['output'])]:
 source=repo/path;digest=hashlib.sha256(source.read_bytes()).hexdigest();bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0];rig=bpy.data.objects[e['rig']];body=bpy.data.objects[e['body']];rig.hide_set(False);rig.hide_render=True;rig.animation_data_clear();rig.data.pose_position='POSE';action=bpy.data.actions[e['actions'][a.clip]];rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0]
 for ob in list(s.objects):
  if ob.type in {'LIGHT','CAMERA'}:bpy.data.objects.remove(ob,do_unlink=True)
 cam=bpy.data.objects.new('Motion comparison',bpy.data.cameras.new('Motion comparison'));s.collection.objects.link(cam);s.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.6
 world=bpy.data.worlds.new('Neutral comparison');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.18,.20,1);world.node_tree.nodes['Background'].inputs[1].default_value=.7;s.world=world
 for xyz,power in [((3,4,5),650),((-3,1,3),350)]:
  light=bpy.data.lights.new('Softbox','AREA');light.energy=power;light.shape='DISK';light.size=4;ob=bpy.data.objects.new(light.name,light);s.collection.objects.link(ob);ob.location=xyz;ob.rotation_euler=(Vector((0,0,.7))-ob.location).to_track_quat('-Z','Y').to_euler()
 s.render.engine='BLENDER_EEVEE_NEXT';s.eevee.taa_render_samples=16;s.render.resolution_x=512;s.render.resolution_y=384;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG'
 first,last=action.frame_range
 for index,phase in enumerate(np.arange(8)/8):
  frame=first+(last-first)*phase;s.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update();ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();p=np.array([ev.matrix_world@v.co for v in mesh.vertices]);ev.to_mesh_clear();target=Vector((0,float((p[:,1].min()+p[:,1].max())/2),.72))
  for view,direction in [('side',(1,0,.07)),('front',(0,1,.07))]:
   cam.location=target+Vector(direction)*5;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();s.render.filepath=str(dest/f'{version}-{view}-{index}.png');bpy.ops.render.render(write_still=True)
 assert hashlib.sha256(source.read_bytes()).hexdigest()==digest
print('Actual eight-phase side/front comparison rendered',a.slug)
