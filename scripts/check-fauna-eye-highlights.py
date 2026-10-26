"""Read-only lighting test to distinguish painted dots from corneal reflections."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix,Vector
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);repo=Path(__file__).resolve().parent.parent;folder=repo/'artifacts/fauna-painted-20261024'/a.slug;out=repo/'artifacts/fauna-review-revisions'/a.slug/'highlight-audit';out.mkdir(parents=True,exist_ok=True);source=folder/'Animal - Painted Species Review.blend';digest=hashlib.sha256(source.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0];rig=bpy.data.objects[e['rig']];rig.animation_data_clear();rig.data.pose_position='REST'
for ob in list(s.objects):
 if ob.type=='LIGHT':bpy.data.objects.remove(ob,do_unlink=True)
guide=json.loads((folder/'eye-audit/front.json').read_text());camera=bpy.data.objects.new('Glint audit camera',bpy.data.cameras.new('Glint audit camera'));s.collection.objects.link(camera);camera.data.type='ORTHO';camera.data.ortho_scale=guide['scale'];camera.matrix_world=Matrix(guide['camera_matrix']);s.camera=camera
land=json.loads((folder/'face-landmarks.json').read_text());target=(Vector(land['right_eye']['point'])+Vector(land['left_eye']['point']))*.5;lights=[]
for offset,power in [((3,4,5),600),((-3,1,3),350),((0,-4,4),400)]:
 data=bpy.data.lights.new('Glint audit softbox','AREA');data.energy=power;data.shape='DISK';data.size=4;ob=bpy.data.objects.new(data.name,data);s.collection.objects.link(ob);ob.location=target+Vector(offset);ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler();lights.append(ob)
world=bpy.data.worlds.new('Glint audit world');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.20,.22,1);world.node_tree.nodes['Background'].inputs[1].default_value=.7;s.world=world;s.render.engine='BLENDER_EEVEE_NEXT';s.eevee.taa_render_samples=24;s.render.resolution_x=s.render.resolution_y=512;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG';materials=set()
for name in e['facial_objects']:
 if name.endswith(' eye'):materials.update(bpy.data.objects[name].data.materials)
report={'source_sha256':digest,'materials':[]}
for material in materials:
 bsdf=material.node_tree.nodes.get('Principled BSDF');textures=[]
 for node in material.node_tree.nodes:
  if node.type=='TEX_IMAGE' and node.image:
   pixels=np.array(node.image.pixels[:]).reshape(-1,4)[:,:3];textures.append({'image':node.image.name,'maximum_rgb':float(pixels.max()),'white_pixels':int((pixels.min(1)>.8).sum())})
 report['materials'].append({'material':material.name,'roughness':bsdf.inputs['Roughness'].default_value,'coat':bsdf.inputs['Coat Weight'].default_value,'textures':textures})
for test in ['original','shifted-light','diffuse-only']:
 if test=='shifted-light':
  for ob in lights:ob.location.x+=2;ob.location.z-=1.5;ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler()
 if test=='diffuse-only':
  for material in materials:
   b=material.node_tree.nodes.get('Principled BSDF');b.inputs['Specular IOR Level'].default_value=0;b.inputs['Coat Weight'].default_value=0
 s.render.filepath=str(out/(test+'.png'));bpy.ops.render.render(write_still=True)
assert hashlib.sha256(source.read_bytes()).hexdigest()==digest;(out/'check.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report))
