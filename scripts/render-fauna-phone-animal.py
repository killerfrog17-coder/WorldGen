"""Render current selected art/motion read-only, with complete coat/extra coverage."""
import argparse,hashlib,json,math,re,sys,time
from pathlib import Path
import bpy,numpy as np
from mathutils import Vector,Matrix
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-phone-media'
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--clip');ap.add_argument('--stills-only',action='store_true');ap.add_argument('--plan',type=Path);ap.add_argument('--output-root',type=Path);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
item=next(x for x in json.loads((a.plan or root/'plan.json').read_text())['animals'] if x['slug']==a.slug);source=repo/item['source'];digest=hashlib.sha256(source.read_bytes()).hexdigest();assert digest==item['source_sha256']
out=((a.output_root or root)/a.slug).resolve();out.mkdir(exist_ok=True,parents=True);receipt_path=out/'receipt.json';signature=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
receipt=json.loads(receipt_path.read_text()) if receipt_path.exists() else {}
if receipt.get('source_sha256')!=digest or receipt.get('renderer_sha256')!=signature:receipt={'source_sha256':digest,'renderer_sha256':signature,'stills':[],'videos':[],'source_modified':False}
def save():receipt_path.write_text(json.dumps(receipt,indent=2)+'\n')
def valid(file,kind):
 entry=next((r for r in receipt[kind] if r['file']==file),None);path=out/file
 return bool(entry and path.exists() and hashlib.sha256(path.read_bytes()).hexdigest()==entry['sha256'])
def record(kind,value):
 receipt[kind]=[r for r in receipt[kind] if r['file']!=value['file']]+[value];save()
bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;entries=json.loads(s['deer_four_rigs']);e=entries[item['entry']];body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']]
for row in entries:
 for name in row['objects']:
  ob=bpy.data.objects[name];ob.hide_set(row!=e);ob.hide_render=row!=e
rig.hide_set(False);rig.hide_render=True;rig.animation_data_clear();rig.data.pose_position='REST'
for ob in list(s.objects):
 if ob.type in {'LIGHT','CAMERA'}:bpy.data.objects.remove(ob,do_unlink=True)
def appearance(coat=0,variant=0,rack=None):
 if a.slug=='deer':
  art=e['approved_art'];profile=art['coats'][coat];body.data.materials[0]=bpy.data.materials[profile['material']]
  keys=body.data.shape_keys.key_blocks
  for name,value in [('Compact ears',max(0,(1-profile['ear_scale'])/.28)),('Large ears',max(0,(profile['ear_scale']-1)/.08)),('Short tail',max(0,(1-profile['tail_scale'])/.30)),('Long tail',max(0,(profile['tail_scale']-1)/.20))]:keys[name].value=value
  rack=2 if rack is None else rack
  for i,row in enumerate(art['antlers']):ob=bpy.data.objects[row['object']];ob.hide_render=i!=rack;ob.hide_set(i!=rack)
 else:
  profile=e['painting_coats'][coat]
  for name in [e['body'],*e.get('coat_objects',[])]:bpy.data.objects[name].data.materials[0]=bpy.data.materials[profile['material']]
  choices=profile.get('appendage_variants');choice=choices[variant%len(choices)] if choices else {};visible=choice.get('objects',profile.get('visible_extras',[]))
  for name in e.get('appendage_objects',[]):ob=bpy.data.objects[name];ob.hide_render=name not in visible;ob.hide_set(ob.hide_render)
  if e.get('body_morph_traits') and body.data.shape_keys:
   for trait,metadata in e['body_morph_traits'].items():body.data.shape_keys.key_blocks[metadata['shape_key']].value=choice.get('body_morphs',{}).get(trait,0.)
 bpy.context.view_layer.update()
def points():
 deps=bpy.context.evaluated_depsgraph_get();parts=[]
 for name in e['objects']:
  ob=bpy.data.objects[name]
  if ob.type!='MESH' or ob.hide_render:continue
  ev=ob.evaluated_get(deps);mesh=ev.to_mesh();p=np.empty(len(mesh.vertices)*3,np.float32);mesh.vertices.foreach_get('co',p);p=p.reshape(-1,3);m=np.asarray(ev.matrix_world);parts.append(p@m[:3,:3].T+m[:3,3]);ev.to_mesh_clear()
 p=np.vstack(parts);assert np.isfinite(p).all();return p
appearance();s.frame_set(15 if item['procedural'] else 0)
p=points();center=Vector(((p.min(0)+p.max(0))*.5).tolist());scale=max(float(np.ptp(p,axis=0).max()),.1)
camera=bpy.data.objects.new('Phone review camera',bpy.data.cameras.new('Phone review camera'));s.collection.objects.link(camera);camera.data.type='ORTHO';s.camera=camera
lights=[]
for offset,power,size in [((3,4,5),650,5),((-3,1,3),350,4),((0,-4,4),450,3),((0,3,-1.5),140,4)]:
 data=bpy.data.lights.new('Phone softbox','AREA');data.energy=power;data.shape='DISK';data.size=size;ob=bpy.data.objects.new(data.name,data);s.collection.objects.link(ob);ob.location=center+Vector(offset);ob.rotation_euler=(center-ob.location).to_track_quat('-Z','Y').to_euler();lights.append(ob)
world=bpy.data.worlds.new('Phone neutral background');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.20,.22,1);world.node_tree.nodes['Background'].inputs[1].default_value=.7;s.world=world
s.render.engine='BLENDER_EEVEE_NEXT';s.eevee.taa_render_samples=12;s.render.threads_mode='FIXED';s.render.threads=2;s.render.resolution_percentage=100
forward=Vector(e.get('review_forward_direction') or (0,1,0));side=Vector((1,0,0))
def view(direction,p=None):
 p=points() if p is None else p;target=Vector(((p.min(0)+p.max(0))*.5).tolist());camera.location=target+Vector(direction).normalized()*8;camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
 # Orthographic framing accounts for image aspect and actual projected geometry.
 rotation=camera.rotation_euler.to_matrix();axes=np.asarray(rotation)[:,:2];xy=p@axes;extent=np.ptp(xy,axis=0)
 # A world-space box centre can be off-centre after projecting an asymmetric
 # pose. Centre the actual projected bounds, including long spider legs.
 shift=(xy.min(0)+xy.max(0))*.5-np.asarray(target)@axes;target+=Vector((axes@shift).tolist());camera.location=target+Vector(direction).normalized()*8
 camera.data.ortho_scale=max(float(extent[0]),float(extent[1])*s.render.resolution_x/s.render.resolution_y)*1.16
 return target
if not a.clip:
 s.render.resolution_x=768;s.render.resolution_y=576;s.render.image_settings.file_format='JPEG';s.render.image_settings.quality=92
 stills=[{'file':viewname+'.jpg','label':label,'view':viewname,'coat':0,'direction':direction} for viewname,label,direction in [('side','Side',(1,0,.08)),('front','Front',forward+Vector((0,0,.08))),('rear','Rear',-forward+Vector((0,0,.08)))]]
 if item['procedural']:stills.append({'file':'top.jpg','label':'Top','view':'top','coat':0,'direction':(0,0,1)})
 for coat in item['coats']:stills.append({'file':f"coat-{coat['index']:02}.jpg",'label':coat['label'],'view':'coat','coat':coat['index'],'direction':(1,0,.1) if not item['procedural'] else (0,0,1) if a.slug=='butterflies-and-moths-guild' else (1,1,.35)})
 for i,extra in enumerate(item['extras']):stills.append({'file':f'extra-{i:02}.jpg','view':'extra','direction':(.8,1,.17),**extra})
 for spec in stills:
  if valid(spec['file'],'stills'):continue
  appearance(spec['coat'],spec.get('variant',0),spec.get('rack'));rig.data.pose_position='REST';s.frame_set(15 if item['procedural'] else 0);view(spec['direction']);s.render.filepath=str(out/spec['file']);bpy.ops.render.render(write_still=True)
  record('stills',{k:v for k,v in spec.items() if k!='direction'}|{'sha256':hashlib.sha256((out/spec['file']).read_bytes()).hexdigest()})
appearance();s.render.resolution_x=640;s.render.resolution_y=480;s.render.fps=24;s.render.fps_base=1.;s.render.image_settings.file_format='FFMPEG';s.render.ffmpeg.format='MPEG4';s.render.ffmpeg.codec='H264';s.render.ffmpeg.constant_rate_factor='MEDIUM';s.render.ffmpeg.ffmpeg_preset='GOOD'
if not a.stills_only:
 for ci,clip in enumerate(item['clips']):
  if a.clip and clip['label']!=a.clip:continue
  filename=f"clip-{ci:02}-"+re.sub('[^a-z0-9]+','-',clip['label'].lower()).strip('-')+'.mp4'
  if valid(filename,'videos'):continue
  started=time.monotonic();original=bpy.data.actions[clip['action']];rig.data.pose_position='POSE';rig.animation_data_create().action=original;rig.animation_data.action_slot=original.slots[0]
  rootbone=next(b for b in rig.data.bones if b.parent is None).name
  bounds=[];anchor=None
  for frame in np.linspace(clip['first'],clip['last'],9):
   s.frame_set(int(frame),subframe=float(frame-int(frame)));bpy.context.view_layer.update();q=rig.matrix_world@rig.pose.bones[rootbone].head
   if anchor is None:anchor=q.copy()
   delta=q-anchor;delta.z=0;bounds.append(points()-np.asarray(delta))
  direction=forward*.7+side if clip['label'].lower() in {'run','trot','eating','sleep','attack','death','rest'} else forward*.35+side
  if item['procedural']:direction=Vector((.5,.8,1.2))
  target=view(direction+Vector((0,0,.12)),np.vstack(bounds));camera.data.ortho_scale*=1.4 if item['procedural'] else 1.05
  for ob in [camera,*lights]:
   for constraint in list(ob.constraints):ob.constraints.remove(constraint)
   con=ob.constraints.new('COPY_LOCATION');con.target=rig;con.subtarget=rootbone;con.use_offset=True;con.use_z=False;ob.location.x-=anchor.x;ob.location.y-=anchor.y
  temp=original.copy();temp.name='Phone preview '+clip['label'];ratio=24/item['source_fps']
  looping=clip['label'].lower() in {'idle','walk','trot','run','swim','flight','flap','glide','slither','side winding','sidewinding'}
  for layer in temp.layers:
   for strip in layer.strips:
    for bag in strip.channelbags:
     for curve in bag.fcurves:
      keys=[(key,key.co.copy(),key.handle_left.copy(),key.handle_right.copy()) for key in curve.keyframe_points]
      for key,co,left,right in keys:
       key.co.x=(co.x-clip['first'])*ratio+1;key.handle_left.x=(left.x-clip['first'])*ratio+1;key.handle_right.x=(right.x-clip['first'])*ratio+1
      curve.update()
      if looping and not any(m.type=='CYCLES' for m in curve.modifiers):curve.modifiers.new('CYCLES')
  drivers=[]
  if item['procedural']:
   # Preserve the original elapsed-time pulse while outputting 24fps rather than
   # the native 30fps. This is a temporary preview expression, never saved.
   for material in bpy.data.materials:
    tree=material.node_tree
    if tree and tree.animation_data:
     for driver in tree.animation_data.drivers:
      expression=driver.driver.expression
      if re.search(r'\bframe\b',expression):drivers.append((driver.driver,expression));driver.driver.expression=re.sub(r'\bframe\b',f'((frame-1)/{ratio}+{clip["first"]})',expression)
  rig.animation_data.action=temp;rig.animation_data.action_slot=temp.slots[0];s.frame_start=1;s.frame_end=max(2,int(math.ceil(clip['seconds']*24)));s.render.filepath=str(out/filename)
  bpy.ops.render.render(animation=True)
  for driver,expression in drivers:driver.expression=expression
  for ob in [camera,*lights]:
   ob.location.x+=anchor.x;ob.location.y+=anchor.y
   for constraint in list(ob.constraints):ob.constraints.remove(constraint)
  record('videos',{'file':filename,'label':clip['label'],'seconds':s.frame_end/24,'native_seconds':clip['native_seconds'],'fps':24,'width':640,'height':480,'sha256':hashlib.sha256((out/filename).read_bytes()).hexdigest(),'render_seconds':round(time.monotonic()-started,2),'root_xy_camera_follow':True,'temporary_time_remap':ratio,'looped_to_minimum':looping and clip['seconds']>clip['native_seconds']})
  print('PHONE CLIP',a.slug,clip['label'],round(time.monotonic()-started,2),'seconds render',flush=True)
  rig.animation_data.action=original;rig.animation_data.action_slot=original.slots[0];bpy.data.actions.remove(temp)
assert hashlib.sha256(source.read_bytes()).hexdigest()==digest
receipt['complete']=len(receipt['stills'])==3+int(item['procedural'])+len(item['coats'])+len(item['extras']) and len(receipt['videos'])==len(item['clips']);save()
print('PHONE MEDIA',a.slug,'complete',receipt['complete'],flush=True)
