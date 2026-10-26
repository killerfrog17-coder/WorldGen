"""Scoped final motion corrections, preserving selected mesh/rest/appearance.

Capuchin: explicit physical limb IK from the existing CC0 fox Run tracks,
with stable core and real tail. Tiger: confine actual tail skin/animation.
These are prototype transfers, not new species mocap or contact simulation.
"""
import argparse,hashlib,json,math,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix,Quaternion,Vector
repo=Path(__file__).resolve().parent.parent;sys.path.insert(0,str(repo/'scripts'));from deer_behaviour_core import blend_pose
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True,choices=['capuchin','tiger']);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);slug=a.slug;source=repo/'artifacts/fauna-review-revisions'/slug/'Animal - Painted Species Review.blend';digest=hashlib.sha256(source.read_bytes()).hexdigest();folder=repo/'artifacts/fauna-repairs'/slug;folder.mkdir(exist_ok=True,parents=True)
bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0];rig=bpy.data.objects[e['rig']];body=bpy.data.objects[e['body']];names=e['joint_names'];parents=e['parents'];rest=[rig.data.bones[n].matrix_local.copy() for n in names];local=[rest[p].inverted()@m if p>=0 else m for p,m in zip(parents,rest)];heads=np.array([m.translation[:] for m in rest]);bind=np.array([v.co[:] for v in body.data.vertices]);rig.hide_set(False);rig.animation_data_clear();rig.data.pose_position='POSE';source_actions=e['actions'].copy();changed=[];cache={};changes={}
def bake(label,poses,times):
 action=bpy.data.actions.new(rig.name+' | '+label+' | Final motion repair');action.use_fake_user=True;slot=action.slots.new(id_type='OBJECT',name=rig.name);bag=action.layers.new('Physical motion correction').strips.new(type='KEYFRAME').channelbag(slot,ensure=True);locations=np.array([p[0] for p in poses]);rotations=np.array([p[1] for p in poses])
 for k in range(1,len(rotations)):rotations[k,np.sum(rotations[k]*rotations[k-1],axis=1)<0]*=-1
 for i,name in enumerate(names):
  for prop,values in [('location',locations[:,i]),('rotation_quaternion',rotations[:,i])]:
   for axis in range(values.shape[-1]):
    curve=bag.fcurves.new(data_path=f'pose.bones["{name}"].{prop}',index=axis);curve.keyframe_points.add(len(times));curve.keyframe_points.foreach_set('co',np.c_[times,values[:,axis]].astype(np.float32).ravel())
    for key in curve.keyframe_points:key.interpolation='LINEAR'
 return action
def bases(matrices):
 locations=[];rotations=[]
 for i,parent in enumerate(parents):
  rel=matrices[parent].inverted()@matrices[i] if parent>=0 else matrices[i];m=local[i].inverted()@rel;locations.append(m.translation[:]);rotations.append(m.to_quaternion()[:])
 return np.array(locations),np.array(rotations)
if slug=='capuchin':
 assert len(names)==76
 bank=repo/'artifacts/fauna-motion-sources/fox';donor=json.loads((bank/'source-rig.json').read_text());motion=np.load(bank/'source-motions.npz');si={b['name']:i for i,b in enumerate(donor['bones'])};reference=motion[next(c['key'] for c in donor['clips'] if c['label']=='Idle')][0,:,:3,3];clip=next(c for c in donor['clips'] if c['label']=='Run');frames=motion[clip['key']];turn=np.diag([-1.,-1.,1.]);configs=[([9,10,11],'fore_R'),([28,29,30],'fore_L'),([55,56,57],'hind_R'),([66,67,68],'hind_L')];palms=[11,30,57,68]
 # Existing selected skin is retained: correcting the actual physical bone
 # tracks avoids disturbing the already selected Walk deformation.
 action=bpy.data.actions[source_actions['Run']];first,last=map(float,action.frame_range);times=np.linspace(first,last,97);poses=[]
 for phase in np.linspace(0,1,97):
  at=phase*(len(frames)-1);lo=int(at);hi=min(lo+1,len(frames)-1);positions=frames[lo,:,:3,3]*(1-at+lo)+frames[hi,:,:3,3]*(at-lo);bob=float(np.clip((positions[si['Hips'],2]-reference[si['Hips'],2])*.7,-.06,.09));offset=Vector((0,0,bob));matrices=[m.copy() for m in rest]
  for m in matrices:m.translation+=offset
  overrides={}
  for chain,path in configs:
   i,j,k=chain;source_path=[si[n] for n in donor['paths'][path]];ss=source_path[2];end=source_path[-2];source_len=np.linalg.norm(reference[end]-reference[ss]);target_len=np.linalg.norm(heads[j]-heads[i])+np.linalg.norm(heads[k]-heads[j]);anchor=Vector(heads[i])+offset;delta=turn@(positions[end]-positions[ss]);target=anchor+Vector(delta*target_len/max(source_len,.001));l1=float(np.linalg.norm(heads[j]-heads[i]));l2=float(np.linalg.norm(heads[k]-heads[j]));axis=target-anchor;distance=float(np.clip(axis.length,abs(l1-l2)+1e-4,l1+l2-1e-4));axis.normalize();restbend=Vector(heads[j]-(heads[i]+heads[k])*.5);bend=restbend-axis*restbend.dot(axis)
   if bend.length<1e-5:bend=Vector((0,1,0))-axis*axis.y
   bend.normalize();along=(l1*l1+distance*distance-l2*l2)/(2*distance);elbow=anchor+axis*along+bend*math.sqrt(max(0,l1*l1-along*along));palm=anchor+axis*distance
   for bone,start,endpos,refvec in [(i,anchor,elbow,heads[j]-heads[i]),(j,elbow,palm,heads[k]-heads[j])]:
    q=Vector(refvec).normalized().rotation_difference((endpos-start).normalized())@rest[bone].to_quaternion();m=q.to_matrix().to_4x4();m.translation=start;overrides[bone]=m
   palm_matrix=rest[k].copy();palm_matrix.translation=palm;overrides[k]=palm_matrix
  # Recompose descendants in hierarchy order; fingers stay attached to palms.
  for i,parent in enumerate(parents):
   if i in overrides:matrices[i]=overrides[i]
   elif parent>=0:matrices[i]=matrices[parent]@local[i]
  poses.append(bases(matrices))
 poses[-1]=(poses[0][0].copy(),poses[0][1].copy());new=bake('Run',poses,times);e['actions']['Run']=new.name;cache['Run']=(times,poses);changes['Run']='Explicit actual four limbs, fixed-length IK from CC0 fox Run, stable core/head/tail, rigid complete palms; no backward somersault transfer.'
else:
 assert len(names)==37;tail=list(range(2,7));weights=np.zeros((len(bind),len(names)))
 for v in body.data.vertices:
  for g in v.groups:
   name=body.vertex_groups[g.group].name
   if name in names:weights[v.index,names.index(name)]=g.weight
 start=heads[tail[:-1]];vectors=np.diff(heads[tail],axis=0);distance=np.full(len(bind),1e6);station=np.zeros(len(bind));length=np.linalg.norm(vectors,axis=1);arc=np.r_[0,np.cumsum(length)]
 for k,(origin,delta) in enumerate(zip(start,vectors)):
  t=np.clip(((bind-origin)@delta)/max(float(delta@delta),1e-8),0,1);dist=np.linalg.norm(bind-origin-t[:,None]*delta,axis=1);near=dist<distance;distance[near]=dist[near];station[near]=k+t[near]
 for vi,point in enumerate(bind):
  if not (point[1]<-.88 and distance[vi]<.115):continue
  t=float(station[vi]);i=min(int(t),len(tail)-2);fraction=t-i;row=np.zeros(len(names));row[tail[i]]=1-fraction;row[tail[i+1]]=fraction
  for g in body.vertex_groups:g.remove([vi])
  for index in np.flatnonzero(row>1e-8):body.vertex_groups[names[index]].add([vi],float(row[index]),'REPLACE')
  changed.append(vi)
 for label in ['Walk','Run']:
  action=bpy.data.actions[source_actions[label]];rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0];first,last=map(float,action.frame_range);times=np.linspace(first,last,97);poses=[]
  for phase,frame in zip(np.linspace(0,1,97),times):
   s.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update();matrices=[rig.pose.bones[n].matrix.copy() for n in names]
   for k,i in enumerate(tail):
    m=local[i].copy();q=Quaternion((0,0,1),.035*math.sin(phase*math.tau-k*.35));m=m@q.to_matrix().to_4x4();matrices[i]=matrices[parents[i]]@m
   poses.append(bases(matrices))
  poses[-1]=(poses[0][0].copy(),poses[0][1].copy());new=bake(label,poses,times);e['actions'][label]=new.name;cache[label]=(times,poses);changes[label]='Actual tail chain2..6 gentle trailing sway and adjacent skin; loop and constant floor offset corrected. Existing tail skin was already mostly on this chain.'
# Ground offset is constant through each loop; no per-frame height snapping.
for label,(times,poses) in cache.items():
 action=bpy.data.actions[e['actions'][label]];rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0];floor=float('inf')
 for frame in np.linspace(times[0],times[-1],97):
  s.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update();ob=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ob.to_mesh();floor=min(floor,min(v.co.z for v in mesh.vertices));ob.to_mesh_clear()
 shift=rest[0].to_3x3().inverted()@Vector((0,0,max(0,.004-floor)))
 for locations,rotations in poses:locations[0]+=np.array(shift)
 corrected=bake(label,poses,times);e['actions'][label]=corrected.name
# Rebuild demos from corrected clips; preserve all untouched individual actions.
def sample(label,seconds):
 if label not in cache:
  action=bpy.data.actions[e['actions'][label]];rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0];first,last=action.frame_range;t=first+(seconds*30)%max(last-first,1);s.frame_set(int(t),subframe=t-int(t));return np.array([b.location[:] for b in rig.pose.bones]),np.array([b.rotation_quaternion[:] for b in rig.pose.bones])
 times,poses=cache[label];t=times[0]+(seconds*30)%max(times[-1]-times[0],1);k=min(len(times)-2,max(0,int(np.searchsorted(times,t)-1)));return blend_pose(poses[k],poses[k+1],float((t-times[k])/(times[k+1]-times[k])))
for title,parts in [('Locomotion',[('Idle',2),('Walk',3),('Run',3),('Walk',2),('Idle',2)]),('Showcase',[('Idle',2),('Walk',3),('Run',3),('Idle',2)])]:
 poses=[];previous=None
 for label,duration in parts:
  for k in range(int(duration*30)):
   pose=sample(label,k/30)
   if previous is not None and k<12:pose=blend_pose(previous,pose,k/12)
   poses.append(pose)
  previous=poses[-1]
 e['demos'][title]=bake(title,poses,np.arange(len(poses))).name
e['final_motion_repairs']=changes;s['deer_four_rigs']=json.dumps([e]);rig.animation_data_create().action=bpy.data.actions[e['actions']['Walk']];rig.animation_data.action_slot=rig.animation_data.action.slots[0];s.frame_set(0);bpy.context.preferences.filepaths.save_version=0;out=folder/'Animal - Final Motion.blend';bpy.ops.wm.save_as_mainfile(filepath=str(out));assert hashlib.sha256(source.read_bytes()).hexdigest()==digest;(folder/'run-repair.json').write_text(json.dumps({'source':str(source.relative_to(repo)),'source_sha256':digest,'output':str(out.relative_to(repo)),'output_sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'changes':changes,'skin_changed_vertices':changed,'owner_approved':False},indent=2)+'\n');print('FINAL MOTION',slug,len(changed),changes)
