"""Read-only close head guides: sculpt sockets versus installed eyes, both sides."""
import argparse,hashlib,json,sys,time
from pathlib import Path
import bpy,numpy as np
from mathutils import Vector

repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024'
ap=argparse.ArgumentParser();ap.add_argument('--slugs',nargs='*');a=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
jobs=json.loads((root/'paint-queue.json').read_text())['jobs'];jobs=[j for j in jobs if not a.slugs or j['slug'] in a.slugs]
progress=root/'inspection/eye-audit-capture.json';status={'total':len(jobs),'completed':[],'errors':{},'current':None}
def save():progress.write_text(json.dumps(status,indent=2)+'\n')
for job in jobs:
 slug=job['slug'];status['current']=slug;save()
 try:
  folder=root/slug;dest=folder/'eye-audit';dest.mkdir(exist_ok=True)
  source=folder/'Animal - Painted Species Review.blend';sha=hashlib.sha256(source.read_bytes()).hexdigest()
  receipt=dest/'capture.json'
  if receipt.exists() and json.loads(receipt.read_text()).get('source_sha256')==sha:
   status['completed'].append(slug);save();continue
  bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0]
  body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']];rig.animation_data_clear();rig.data.pose_position='REST'
  for ob in s.objects:ob.hide_render=ob.type=='MESH' and ob.name not in e['objects']
  rig.hide_render=True
  land=json.loads((folder/'face-landmarks.json').read_text());right=np.array(land['right_eye']['point']);left=np.array(land['left_eye']['point']);nose=np.array(land['nose']['point']);mid=(right+left)/2
  gap=np.linalg.norm(right-left);reach=np.linalg.norm(nose-mid)
  center=Vector((mid*.88+nose*.12).tolist());span=float(np.clip(max(gap*2.2,reach*2.35),.32,1.8))
  front=Vector(land['front_axis']);side=Vector(land['side_axis'])
  camera=bpy.data.objects.new('Eye audit camera',bpy.data.cameras.new('Eye audit camera'));s.collection.objects.link(camera);camera.data.type='ORTHO';camera.data.ortho_scale=span;s.camera=camera
  world=bpy.data.worlds.new('Eye audit neutral world');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.20,.22,1);world.node_tree.nodes['Background'].inputs[1].default_value=.7;s.world=world
  for ob in list(s.objects):
   if ob.type=='LIGHT':bpy.data.objects.remove(ob,do_unlink=True)
  for pos,power in [((3,4,5),600),((-3,1,3),350),((0,-4,4),400)]:
   data=bpy.data.lights.new('Eye audit softbox','AREA');data.energy=power;data.shape='DISK';data.size=4;ob=bpy.data.objects.new(data.name,data);s.collection.objects.link(ob);ob.location=center+Vector(pos);ob.rotation_euler=(center-ob.location).to_track_quat('-Z','Y').to_euler()
  s.render.resolution_x=s.render.resolution_y=512;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG'
  original_visibility={ob.name:ob.hide_render for ob in s.objects if ob.type=='MESH'}
  for view,direction in [('right',side),('left',-side),('front',front)]:
   camera.location=center+direction*4+Vector((0,0,.05));camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
   metadata={'camera_matrix':[list(row) for row in camera.matrix_world],'scale':span,'size':[512,512],'source_sha256':sha,'body_geometry_sha256':json.loads((folder/'paint-state.json').read_text())['geometry_sha256']}
   # Explicit matrix update before serializing guide rays.
   bpy.context.view_layer.update();metadata['camera_matrix']=[list(row) for row in camera.matrix_world]
   (dest/f'{view}.json').write_text(json.dumps(metadata,indent=2)+'\n')
   s.render.engine='BLENDER_WORKBENCH';s.display.shading.light='STUDIO';s.display.shading.color_type='SINGLE';s.display.shading.single_color=(.46,.44,.40);s.display.shading.show_shadows=True;s.display.shading.show_cavity=True;s.display.shading.cavity_type='BOTH';s.display.shading.curvature_ridge_factor=1.5;s.display.shading.curvature_valley_factor=1.7
   for ob in s.objects:
    if ob.type=='MESH':ob.hide_render=ob!=body
   s.render.filepath=str(dest/f'{view}-sculpt.png');bpy.ops.render.render(write_still=True)
   s.render.engine='BLENDER_EEVEE_NEXT';s.eevee.taa_render_samples=16
   for name,hidden in original_visibility.items():bpy.data.objects[name].hide_render=hidden
   s.render.filepath=str(dest/f'{view}-before.png');bpy.ops.render.render(write_still=True)
  assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
  receipt.write_text(json.dumps({'slug':slug,'source_sha256':sha,'scope':'Unmodified selected body; sculpt sockets and old installed eyes; no source save.'},indent=2)+'\n')
  status['completed'].append(slug);print('EYE GUIDES',slug,flush=True)
 except Exception as exc:status['errors'][slug]=str(exc);print('ERROR',slug,repr(exc),flush=True)
 save()
status['current']=None;status['finished']=True;save()
