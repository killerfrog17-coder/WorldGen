"""Current close-up eyes using the exact original audit cameras, no native save."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix,Vector
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024'
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--bodies',action='store_true');ap.add_argument('--root',type=Path);ap.add_argument('--sculpt',action='store_true');ap.add_argument('--blend',type=Path);ap.add_argument('--output-root',type=Path);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);folder=(a.root or root/a.slug).resolve();dest=(a.output_root or folder/'eye-audit').resolve();dest.mkdir(parents=True,exist_ok=True)
source=(a.blend or folder/'Animal - Painted Species Review.blend').resolve();digest=hashlib.sha256(source.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0];body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']];rig.animation_data_clear();rig.data.pose_position='REST'
for ob in list(s.objects):
 if ob.type=='LIGHT':bpy.data.objects.remove(ob,do_unlink=True)
camera=bpy.data.objects.new('Eye result camera',bpy.data.cameras.new('Eye result camera'));s.collection.objects.link(camera);camera.data.type='ORTHO';s.camera=camera
world=bpy.data.worlds.new('Eye audit neutral world');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.20,.22,1);world.node_tree.nodes['Background'].inputs[1].default_value=.7;s.world=world
land=json.loads((folder/'face-landmarks.json').read_text());center=(Vector(land['right_eye']['point'])+Vector(land['left_eye']['point']))*.5
for pos,power in [((3,4,5),600),((-3,1,3),350),((0,-4,4),400)]:
 data=bpy.data.lights.new('Eye audit softbox','AREA');data.energy=power;data.shape='DISK';data.size=4;ob=bpy.data.objects.new(data.name,data);s.collection.objects.link(ob);ob.location=center+Vector(pos);ob.rotation_euler=(center-ob.location).to_track_quat('-Z','Y').to_euler()
s.render.engine='BLENDER_EEVEE_NEXT';s.eevee.taa_render_samples=24;s.render.resolution_x=s.render.resolution_y=512;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG'
if a.sculpt:
 for ob in s.objects:
  if ob.type=='MESH':ob.hide_render=ob!=body
 s.render.engine='BLENDER_WORKBENCH';s.display.shading.light='STUDIO';s.display.shading.color_type='SINGLE';s.display.shading.single_color=(.46,.44,.40);s.display.shading.show_shadows=True;s.display.shading.show_cavity=True;s.display.shading.cavity_type='BOTH';s.display.shading.curvature_ridge_factor=1.5;s.display.shading.curvature_valley_factor=1.7
for view in ['right','left','front']:
 guide=json.loads((folder/'eye-audit'/f'{view}.json').read_text());camera.matrix_world=Matrix(guide['camera_matrix']);camera.data.ortho_scale=guide['scale'];s.render.filepath=str(dest/(f'{view}-'+('sculpt' if a.sculpt else 'after')+'.png'));bpy.ops.render.render(write_still=True)
if a.bodies:
 previews=folder/'previews';state=json.loads((folder/'paint-state.json').read_text())
 alternate=next((i for i,c in reversed(list(enumerate(state['coats']))) if 'hypothetical' in c['label'].lower()),len(state['coats'])-1)
 for view,coat in [('side',0),('front',0),('rear',0),('side',alternate)]:
  profile=e['painting_coats'][coat]
  for name in [e['body'],*e.get('coat_objects',[])]:bpy.data.objects[name].data.materials[0]=bpy.data.materials[profile['material']]
  choices=profile.get('appendage_variants');visible=choices[0]['objects'] if choices else profile.get('visible_extras',[])
  for name in e.get('appendage_objects',[]):
   ob=bpy.data.objects[name];ob.hide_render=name not in visible;ob.hide_set(ob.hide_render)
  if e.get('body_morph_traits') and body.data.shape_keys:
   values=choices[0].get('body_morphs',{}) if choices else {}
   for trait,metadata in e['body_morph_traits'].items():body.data.shape_keys.key_blocks[metadata['shape_key']].value=values.get(trait,0.)
  bpy.context.view_layer.update();parts=[]
  for name in e['objects']:
   ob=bpy.data.objects[name]
   if ob.type!='MESH' or ob.hide_render:continue
   ev=ob.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();parts.extend([ev.matrix_world@v.co for v in mesh.vertices]);ev.to_mesh_clear()
  p=np.array(parts);target=Vector(((p.min(0)+p.max(0))*.5).tolist());span=float(np.ptp(p,axis=0).max())
  direction=Vector((1,0,.08)) if view=='side' else Vector(e.get('review_forward_direction') or (0,1,0))*(1 if view=='front' else -1)+Vector((0,0,.08))
  camera.location=target+direction.normalized()*span*2;camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=span*1.22
  suffix=f'-coat{coat}-final-lineage' if coat else '-painted';path=previews/f'1-Bind-{view}{suffix}.png';s.render.filepath=str(path);bpy.ops.render.render(write_still=True)
  path.with_suffix('.json').write_text(json.dumps({'source_sha256':digest,'render_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'coat':coat,'view':view,'camera_matrix':[list(row) for row in camera.matrix_world],'scale':camera.data.ortho_scale,'size':[512,512]},indent=2)+'\n')
  if coat:
   motion=json.loads((folder/'paint-motion-checks.json').read_text());assert motion['read_blend_sha256']==digest
   (folder/'final-qa.json').write_text(json.dumps({'checked_blend_sha256':digest,'all_sampled_source_clips_finite':motion['all_sampled_surfaces_finite'],'motion_warnings':motion['warnings'],'lineage_coat':coat,'lineage_label':state['coats'][coat]['label'],'lineage_render':str(path.relative_to(repo)),'lineage_render_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'actually_visually_inspected':False,'appearance_approved':False},indent=2)+'\n')
assert hashlib.sha256(source.read_bytes()).hexdigest()==digest
(dest/'after-renders.json').write_text(json.dumps({'source_sha256':digest,'views':['right','left','front'],'actual_vision_pending':True},indent=2)+'\n')
