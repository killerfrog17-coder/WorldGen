"""Bound the selected hare's transferred root pitch; preserve its approved bind."""
import hashlib,json,math,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix,Quaternion,Vector
repo=Path(__file__).resolve().parent.parent;folder=repo/'artifacts/fauna-repairs/hare';folder.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(repo/'scripts'));from deer_behaviour_core import blend_pose,smooth as blend_smooth
source=repo/'artifacts/fauna-painted-20261024/hare/Animal - Painted Species Review.blend';digest=hashlib.sha256(source.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0];rig=bpy.data.objects[e['rig']];body=bpy.data.objects[e['body']]
rig.animation_data_clear();rig.data.pose_position='POSE';original=bpy.data.actions[e['actions']['Run']];rig.animation_data_create().action=original;rig.animation_data.action_slot=original.slots[0]
names=e['joint_names'];root=rig.pose.bones[names[0]];rest_root=rig.data.bones[names[0]].matrix_local.copy();rest_axis=rig.data.bones[names[4]].head_local-rig.data.bones[names[0]].head_local
# Upper rear torso should retain its volume when the thighs fold. Confine
# this patch above the legs, with a smooth spatial falloff into each haunch.
def smooth(value):
 value=float(np.clip(value,0,1));return value*value*(3-2*value)
skin_changed=[]
for vertex in body.data.vertices:
 x,y,z=vertex.co;blend=smooth((z-.58)/.30)*smooth((.42-abs(x-.045))/.15)*smooth((y+.77)/.12)*smooth((.14-y)/.18)
 if blend<1e-5:continue
 weights=np.zeros(len(names))
 for group in vertex.groups:weights[names.index(body.vertex_groups[group.group].name)]=group.weight
 amount=weights[27:35].sum()*blend
 if amount<1e-5:continue
 weights[27:35]*=1-blend;distance=np.array([abs(y-rig.data.bones[names[i]].head_local.y) for i in range(5)]);closest=np.argsort(distance)[:2];share=1/np.maximum(distance[closest],.025);share/=share.sum();weights[closest]+=amount*share
 keep=np.argsort(weights)[-4:];weights[[i for i in range(len(names)) if i not in keep]]=0;weights/=weights.sum()
 for group in body.vertex_groups:group.remove([vertex.index])
 for i in np.flatnonzero(weights>1e-8):body.vertex_groups[names[i]].add([vertex.index],float(weights[i]),'REPLACE')
 skin_changed.append(vertex.index)
rest_pitch=math.atan2(rest_axis.z,rest_axis.y);first,last=original.frame_range;times=np.linspace(first,last,81);samples=[]
for frame in times:
 s.frame_set(int(frame),subframe=float(frame-int(frame)));bpy.context.view_layer.update();axis=rig.pose.bones[names[4]].head-root.head
 samples.append((float(frame),root.matrix.copy(),math.atan2(axis.z,axis.y),{i:rig.pose.bones[names[i]].matrix.copy() for i in [5,6,7]}))
mean_pitch=float(np.mean([v[2] for v in samples]));mean_z=float(np.mean([v[1].translation.z for v in samples]));action=original.copy();action.name=original.name+' | Owner Run Repair'
rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
for layer in action.layers:
 for strip in layer.strips:
  for bag in strip.channelbags:
   for curve in list(bag.fcurves):
    if any(curve.data_path.startswith('pose.bones["'+names[i]+'"]') for i in [0,5,6,7]):bag.fcurves.remove(curve)
previous=None;corrected=[];neck_previous={}
for frame,old,pitch,neck in samples:
 s.frame_set(int(frame),subframe=float(frame-int(frame)));bpy.context.view_layer.update()
 target=rest_pitch+float(np.clip((pitch-mean_pitch)*.15,-math.radians(5),math.radians(5)))
 turn=Quaternion((1,0,0),target-pitch).to_matrix().to_4x4();new=turn@old;new.translation=old.translation.copy();new.translation.z=rest_root.translation.z+.10+(old.translation.z-mean_z)*.35
 basis=rest_root.inverted()@new;q=basis.to_quaternion()
 if previous is not None and q.dot(previous)<0:q.negate()
 previous=q.copy();root.rotation_mode='QUATERNION';root.location=basis.translation;root.rotation_quaternion=q;root.scale=(1,1,1)
 root.keyframe_insert('location',frame=frame,group=names[0]);root.keyframe_insert('rotation_quaternion',frame=frame,group=names[0]);bpy.context.view_layer.update()
 # Distribute the compensating neck bend so levelling the torso does not make
 # the nose point into the sky. Keep the original world-space gaze direction.
 for i,factor in [(5,.30),(6,.60),(7,1.0)]:
  pb=rig.pose.bones[names[i]];rest=rig.data.bones[names[i]];parent=pb.parent
  local=rest.parent.matrix_local.inverted()@rest.matrix_local
  desired=(Quaternion((1,0,0),(target-pitch)*(1-factor))@neck[i].to_quaternion()).to_matrix().to_4x4();desired.translation=parent.matrix@local.translation
  basis=local.inverted()@parent.matrix.inverted()@desired;q=basis.to_quaternion()
  if i in neck_previous and q.dot(neck_previous[i])<0:q.negate()
  neck_previous[i]=q.copy();pb.location=basis.translation;pb.rotation_mode='QUATERNION';pb.rotation_quaternion=q;pb.scale=(1,1,1)
  pb.keyframe_insert('location',frame=frame,group=names[i]);pb.keyframe_insert('rotation_quaternion',frame=frame,group=names[i]);bpy.context.view_layer.update()
 corrected.append({'frame':frame,'before_body_pitch_degrees':math.degrees(pitch),'after_body_pitch_degrees':math.degrees(target)})
# Preserve a continuous hop; shift the entire repaired cycle by one constant
# height so its lowest actual skinned contact reaches the review floor.
floor=float('inf')
for frame in times:
 s.frame_set(int(frame),subframe=float(frame-int(frame)));bpy.context.view_layer.update();ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();floor=min(floor,min((ev.matrix_world@v.co).z for v in mesh.vertices));ev.to_mesh_clear()
local_shift=rest_root.to_3x3().inverted()@Vector((0,0,-floor+.004))
for layer in action.layers:
 for strip in layer.strips:
  for bag in strip.channelbags:
   for curve in bag.fcurves:
    if curve.data_path=='pose.bones["'+names[0]+'"].location':
     for key in curve.keyframe_points:key.co.y+=local_shift[curve.array_index];key.handle_left.y+=local_shift[curve.array_index];key.handle_right.y+=local_shift[curve.array_index]
    if any(curve.data_path.startswith('pose.bones["'+names[i]+'"]') for i in [0,5,6,7]):
     for key in curve.keyframe_points:key.interpolation='LINEAR'
e['actions']['Run']=action.name
def sample(label,seconds):
 clip=bpy.data.actions[e['actions'][label]];rig.animation_data.action=clip;rig.animation_data.action_slot=clip.slots[0];first,last=clip.frame_range;frame=first+(seconds*30)%max(1,last-first);s.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()
 return np.array([rig.pose.bones[n].location[:] for n in names]),np.array([rig.pose.bones[n].rotation_quaternion[:] for n in names])
for title,parts in [('Locomotion',[('Idle',2),('Run',3),('Idle',2)]),('Showcase',[('Idle',2),('Rest',5),('Run',1.5),('Jump',1.5),('Idle',2)])]:
 poses=[];previous=None;last_duration=0
 for label,duration in parts:
  for f in range(round(duration*30)):
   t=f/30;pose=sample(label,t)
   if previous and t<.5:pose=blend_pose(sample(previous,last_duration+t),pose,blend_smooth(t/.5))
   poses.append(pose)
  previous=label;last_duration=duration
 demo=bpy.data.actions.new(rig.name+' | '+title+' | Owner Run Repair');demo.use_fake_user=True;slot=demo.slots.new(id_type='OBJECT',name=rig.name);bag=demo.layers.new('Repaired review sequence').strips.new(type='KEYFRAME').channelbag(slot,ensure=True)
 locations=np.array([p[0] for p in poses]);rotations=np.array([p[1] for p in poses]);frames=np.arange(len(poses))
 for i in range(1,len(rotations)):rotations[i,np.sum(rotations[i]*rotations[i-1],axis=1)<0]*=-1
 for i,name in enumerate(names):
  for path,values in [('location',locations[:,i]),('rotation_quaternion',rotations[:,i])]:
   for axis in range(values.shape[1]):
    curve=bag.fcurves.new(data_path=f'pose.bones["{name}"].{path}',index=axis);curve.keyframe_points.add(len(frames));curve.keyframe_points.foreach_set('co',np.c_[frames,values[:,axis]].astype(np.float32).ravel())
    for key in curve.keyframe_points:key.interpolation='LINEAR'
 e['demos'][title]=demo.name
e['owner_run_repair']={'source_sha256':digest,'method':'Anatomical torso-pitch correction, gaze-preserving neck compensation, bounded source body bob and confined rear torso skin','appearance_approved':False};s['deer_four_rigs']=json.dumps([e]);rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0];s.frame_set(0)
out=folder/'Animal - Run Repaired.blend';bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(out))
assert hashlib.sha256(source.read_bytes()).hexdigest()==digest
(folder/'run-repair.json').write_text(json.dumps({'source':str(source.relative_to(repo)),'source_sha256':digest,'output':str(out.relative_to(repo)),'output_sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'floor_translation':-floor+.004,'samples':corrected,'upper_rear_torso_skin_vertices':skin_changed,'owner_approved':False},indent=2)+'\n');print('Hare run repaired',str(out),flush=True)
