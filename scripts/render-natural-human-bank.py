"""Prop-aware whole-sequence review, on actual unmodified native character skins."""
import argparse,json,sys,math
from pathlib import Path
import bpy
from mathutils import Vector,Matrix

REPO=Path(__file__).resolve().parent.parent;ROOT=REPO/'artifacts/human-natural-20261031'
sys.path.insert(0,str(REPO/'scripts'))
from natural_human_prop_fit import apply,UP,FORWARD

p=argparse.ArgumentParser();p.add_argument('--variant',default='B');p.add_argument('--roles',default='');p.add_argument('--steps',type=int,default=7);p.add_argument('--folder',default='review-first');p.add_argument('--inspect',action='store_true');p.add_argument('--no-fit',action='store_true');a=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
bpy.ops.wm.open_mainfile(filepath=str(ROOT/f'Kimodo bank {a.variant}.blend'))
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');clips=json.loads(rig['kimodo_bank_clips'])
structure=[{'name':o.name,'type':o.type,'vertices':len(o.data.vertices) if o.type=='MESH' else 0,'props':list(o.keys()),'modifiers':[{'name':m.name,'type':m.type,'group':getattr(m,'vertex_group',''),'invert':getattr(m,'invert_vertex_group',False)} for m in o.modifiers]} for o in bpy.data.objects]
(ROOT/f'structure-{a.variant}.json').write_text(json.dumps(structure,indent=2))
if a.inspect:
 print(json.dumps(structure,indent=2));sys.exit(0)
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE_NEXT';scene.render.resolution_x=384;scene.render.resolution_y=512;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
for o in bpy.data.objects:
 o.hide_set(False);o.hide_render=o.type=='ARMATURE'
 for modifier in o.modifiers:
  if modifier.type=='SUBSURF' or modifier.type=='MASK' and modifier.name.startswith('Delete.'):
   modifier.show_viewport=False;modifier.show_render=False
# Clothing is review-only in the original comparison; do not let it conceal
# deformation/prop intersections absent from the actual in-game character.
for o in list(bpy.data.objects):
 if o.type=='MESH' and any(word in o.name.lower() for word in ['shirt','jeans','pants','trouser','shoe','dress','tshirt','suit']):bpy.data.objects.remove(o,do_unlink=True)
def mat(name,color):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);return m
wood=mat('Review actual-size wood',(.22,.11,.035));stone=mat('Review stone',(.27,.3,.33));cord=mat('Review string',(.75,.67,.44))
bpy.ops.mesh.primitive_plane_add(size=30);floor=bpy.context.object;floor.data.materials.append(mat('Neutral floor',(.19,.22,.18)))
bpy.ops.object.camera_add();camera=bpy.context.object;camera.data.lens=58;scene.camera=camera
for location,energy,size in [((3,-4,5),700,5),((-3,-1,3),400,4),((1,3,4),450,3)]:
 bpy.ops.object.light_add(type='AREA',location=location);o=bpy.context.object;o.data.energy=energy;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
props=[]
def line(points,radius,material):
 curve=bpy.data.curves.new('Physical prop section','CURVE');curve.dimensions='3D';curve.bevel_depth=radius;curve.bevel_resolution=0;sp=curve.splines.new('POLY');sp.points.add(len(points)-1)
 for pt,co in zip(sp.points,points):pt.co=(*co,1)
 ob=bpy.data.objects.new('Actual-size held prop',curve);scene.collection.objects.link(ob);ob.data.materials.append(material);props.append(ob)
def draw_prop(prop):
 if prop is None:return
 grip=Vector(prop['grip']);length=prop['length']
 if prop['kind']=='bow':
  fwd=Vector(prop['forward']);up=Vector(prop['up']);z=(-fwd).cross(up).normalized();x=up.cross(z).normalized();y=z.cross(x).normalized();rotation=Matrix((x,y,z)).transposed();origin=grip-y*(length*.5)
  def point(px,py):return origin+rotation@Vector((px*.35,py*length,0))
  line([point(.38*(i/16*2-1)**2,i/16) for i in range(17)],.0095,wood)
  middle=Vector(prop['draw_grip']) if prop['raised']>.01 and prop['draw']>.01 else point(.38,.5)
  line([point(.38,0),middle,point(.38,1)],.0018,cord)
  if prop['draw']>.2:line([middle,middle+fwd*.75],.0035,wood)
 else:
  axis=Vector(prop['axis']);base=grip-axis*length*prop['fraction'];tip=base+axis*length
  line([base,tip],.0133 if prop['kind']=='spear' else .012,wood)
  if prop['kind']=='spear':line([tip-axis*.16,tip],.025,stone)
folder=ROOT/a.folder;folder.mkdir(exist_ok=True);records=[]
for clip in clips:
 if clip.get('mirrored') and clip['role']!='SwimCarryLeft':continue
 if a.roles and clip['role'] not in a.roles.split(','):continue
 action=bpy.data.actions[clip['action']];rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
 for i in range(a.steps):
  t=i/max(1,a.steps-1);frame=1+round((clip['frames']-1)*t)
  for bone in rig.pose.bones:bone.location=(0,0,0);bone.scale=(1,1,1)
  scene.frame_set(frame);bpy.context.view_layer.update()
  # A render re-evaluates keyed actions. Detach the source action while fitting
  # this sampled pose so it cannot silently undo the rehearsal constraints.
  pose={b.name:(b.location.copy(),b.rotation_quaternion.copy(),b.scale.copy()) for b in rig.pose.bones}
  rig.animation_data.action=None
  for bone in rig.pose.bones:bone.location,bone.rotation_quaternion,bone.scale=pose[bone.name]
  bpy.context.view_layer.update()
  prop=None if a.no_fit else apply(rig,a.variant,clip['role'],t,clip.get('contact',.45))
  draw_prop(prop)
  for view,location in [('front',(2.8,-4.5,2.35)),('side',(4.5,.4,2.1))]:
   camera.location=location;camera.rotation_euler=(Vector((0,0,.9))-camera.location).to_track_quat('-Z','Y').to_euler()
   name=f'{a.variant}-{clip["role"]}-{i:02d}-{view}.png';scene.render.filepath=str(folder/name);bpy.ops.render.render(write_still=True)
   records.append({'role':clip['role'],'frame':frame,'sample':i,'view':view,'file':name,'prop':prop})
  for ob in props:
   data=ob.data;bpy.data.objects.remove(ob,do_unlink=True)
   if data.users==0:bpy.data.curves.remove(data)
  props.clear()
  rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
 print('NATURAL PROP REVIEW',a.variant,clip['role'],flush=True)
(folder/f'index-{a.variant}.json').write_text(json.dumps(records,indent=2))
