"""Read-only registered neutral body views for diffuse paint generation."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Vector
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--views',nargs='+',choices=['front','right','left','top','rear'],default=['right','front','left']);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=repo/'artifacts/fauna-anatomy-revision'/a.slug;source=root/'Animal - Painted Species Review.blend';digest=hashlib.sha256(source.read_bytes()).hexdigest();bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;entry=json.loads(s['deer_four_rigs'])[0];rig=bpy.data.objects[entry['rig']];rig.animation_data_clear();rig.data.pose_position='REST'
dest=root/'body-paint-source';dest.mkdir(exist_ok=True)
for ob in list(s.objects):
 if ob.type=='LIGHT':bpy.data.objects.remove(ob,do_unlink=True)
mat=bpy.data.materials.new('Neutral body paint registration');mat.use_nodes=True;bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.32,.32,.32,1);bs.inputs['Roughness'].default_value=.78
points=[]
for ob in s.objects:
 if ob.type=='MESH' and not ob.hide_render:
  for i in range(len(ob.data.materials)):ob.data.materials[i]=mat
  points.extend([ob.matrix_world@v.co for v in ob.data.vertices])
p=np.array(points);target=Vector(((p.min(0)+p.max(0))*.5).tolist());span=float(np.ptp(p,axis=0).max());camera=bpy.data.objects.new('Registered body paint camera',bpy.data.cameras.new('Registered body paint camera'));s.collection.objects.link(camera);camera.data.type='ORTHO';camera.data.ortho_scale=span*1.18;s.camera=camera
for pos,power in [((3,4,5),700),((-3,1,3),500),((0,-4,4),500),((0,3,-1.5),160)]:
 data=bpy.data.lights.new('Body softbox','AREA');data.energy=power;data.shape='DISK';data.size=5;ob=bpy.data.objects.new(data.name,data);s.collection.objects.link(ob);ob.location=target+Vector(pos);ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler()
s.render.engine='BLENDER_EEVEE_NEXT';s.eevee.taa_render_samples=24;s.render.resolution_x=s.render.resolution_y=768;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG'
world=bpy.data.worlds.new('Neutral body background');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.20,.22,1);world.node_tree.nodes['Background'].inputs[1].default_value=.7;s.world=world
directions={'right':(1,0,.025),'front':(0,1,.025),'left':(-1,0,.025),'top':(0,0,1),'rear':(0,-1,.025)}
for view in a.views:
 direction=directions[view]
 camera.location=target+Vector(direction).normalized()*span*2;camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();path=dest/f'{view}-clay.png';s.render.filepath=str(path);bpy.ops.render.render(write_still=True);(dest/f'{view}.json').write_text(json.dumps({'camera_matrix':[list(row) for row in camera.matrix_world],'scale':camera.data.ortho_scale,'size':[768,768],'source_sha256':digest,'render_sha256':hashlib.sha256(path.read_bytes()).hexdigest()},indent=2)+'\n')
assert hashlib.sha256(source.read_bytes()).hexdigest()==digest
