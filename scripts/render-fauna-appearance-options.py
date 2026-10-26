"""Read-only full-body coat/attachment inspection; no animation baking or save."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Vector

repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--root',type=Path,required=True);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve();source=root/'Animal - Painted Species Review.blend';sha=hashlib.sha256(source.read_bytes()).hexdigest();bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0];rig=bpy.data.objects[e['rig']];rig.animation_data_clear();rig.data.pose_position='REST';body=bpy.data.objects[e['body']];dest=root/'appearance-options';dest.mkdir(exist_ok=True)
for ob in list(s.objects):
 if ob.type=='LIGHT':bpy.data.objects.remove(ob,do_unlink=True)
camera=bpy.data.objects.new('Appearance inspection camera',bpy.data.cameras.new('Appearance inspection camera'));s.collection.objects.link(camera);camera.data.type='ORTHO';s.camera=camera
center=body.matrix_world@Vector(np.mean([v.co[:] for v in body.data.vertices],axis=0))
for pos,power in [((3,4,5),650),((-3,1,3),350),((0,-4,4),400)]:
 data=bpy.data.lights.new('Appearance softbox','AREA');data.energy=power;data.shape='DISK';data.size=4;ob=bpy.data.objects.new(data.name,data);s.collection.objects.link(ob);ob.location=center+Vector(pos);ob.rotation_euler=(center-ob.location).to_track_quat('-Z','Y').to_euler()
world=bpy.data.worlds.new('Appearance neutral world');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.20,.22,1);world.node_tree.nodes['Background'].inputs[1].default_value=.7;s.world=world;s.render.engine='BLENDER_EEVEE_NEXT';s.eevee.taa_render_samples=24;s.render.resolution_x=s.render.resolution_y=512;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG'
coats=e['painting_coats'];cases=[(i,0) for i in range(len(coats))];seen=set()
for ci,coat in enumerate(coats):
 for vi,variant in enumerate(coat.get('appendage_variants') or [{'objects':coat.get('visible_extras',[])}]):
  signature=tuple(sorted(variant['objects']))
  if signature not in seen:cases.append((ci,vi));seen.add(signature)
cases=list(dict.fromkeys(cases));evidence=[]
for ci,vi in cases:
 coat=coats[ci];variants=coat.get('appendage_variants');variant=variants[vi] if variants else {'objects':coat.get('visible_extras',[])};visible=set(variant['objects'])
 for name in [e['body'],*e.get('coat_objects',[])]:bpy.data.objects[name].data.materials[0]=bpy.data.materials[coat['material']]
 for name in e.get('appendage_objects',[]):
  ob=bpy.data.objects[name];ob.hide_render=name not in visible;ob.hide_set(ob.hide_render)
 if e.get('body_morph_traits') and body.data.shape_keys:
  for key,metadata in e['body_morph_traits'].items():body.data.shape_keys.key_blocks[metadata['shape_key']].value=variant.get('body_morphs',{}).get(key,0.)
 bpy.context.view_layer.update();parts=[];dg=bpy.context.evaluated_depsgraph_get()
 for name in e['objects']:
  ob=bpy.data.objects[name]
  if ob.type!='MESH' or ob.hide_render:continue
  ev=ob.evaluated_get(dg);mesh=ev.to_mesh();parts.extend([ev.matrix_world@v.co for v in mesh.vertices]);ev.to_mesh_clear()
 p=np.array(parts);target=Vector((p.min(0)+p.max(0))*.5);span=float(np.ptp(p,axis=0).max());F=Vector(e.get('review_forward_direction') or (0,1,0));directions={'front':F+Vector((0,0,.08)),'side':Vector((1,0,.08))}
 if (ci,vi)==(0,0):directions['rear']=-F+Vector((0,0,.08))
 for view,direction in directions.items():
  camera.location=target+direction.normalized()*span*2;camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=span*1.18;path=dest/f'coat-{ci:02d}-extra-{vi:02d}-{view}.png';s.render.filepath=str(path);bpy.ops.render.render(write_still=True);evidence.append({'file':str(path.relative_to(repo)),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'coat':ci,'coat_label':coat['label'],'extra':vi,'extra_label':variant.get('label',variant.get('id')),'objects':sorted(visible),'view':view,'camera_matrix':[list(r) for r in camera.matrix_world],'scale':camera.data.ortho_scale,'actually_visually_inspected':False})
assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
(dest/'receipt.json').write_text(json.dumps({'source_sha256':sha,'scope':'All coat examples and every distinct attachment set represented, resting full bodies; actual reference comparison still required. No motion/owner approval.','renders':evidence},indent=2)+'\n');print('APPEARANCE OPTIONS',a.slug,len(evidence),flush=True)
