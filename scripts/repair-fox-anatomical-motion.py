"""Correct Fox3's actual right hind leg/tail roles and stabilize whole paws.

The selected mesh/rest skeleton stay intact. Right-hind and tail directions are
retargeted from the existing CC0 fox bank, with explicit physical correspondence.
"""
import hashlib,json,math,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Quaternion,Vector
repo=Path(__file__).resolve().parent.parent;sys.path.insert(0,str(repo/'scripts'));from deer_behaviour_core import blend_pose,smooth
source=repo/'artifacts/fauna-review-revisions/red-fox/Animal - Painted Species Review.blend';digest=hashlib.sha256(source.read_bytes()).hexdigest();folder=repo/'artifacts/fauna-repairs/red-fox';folder.mkdir(exist_ok=True,parents=True)
bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0];rig=bpy.data.objects[e['rig']];body=bpy.data.objects[e['body']];names=e['joint_names'];parents=e['parents'];rest=[rig.data.bones[n].matrix_local.copy() for n in names];local=[rest[p].inverted()@m if p>=0 else m for p,m in zip(parents,rest)];joints=np.array([m.translation[:] for m in rest]);bind=np.array([v.co[:] for v in body.data.vertices]);rig.animation_data_clear();rig.data.pose_position='POSE';rig.hide_set(False)
assert len(names)==34 and parents[5]==0 and parents[26]==0
bank=repo/'artifacts/fauna-motion-sources/fox';donor=json.loads((bank/'source-rig.json').read_text());motion=np.load(bank/'source-motions.npz');sn=[b['name'] for b in donor['bones']];si={n:i for i,n in enumerate(sn)};reference=motion[next(c['key'] for c in donor['clips'] if c['label']=='Idle')][0,:,:3,3]
def binding(chain,path):
 ids=[si[n] for n in donor['paths'][path]];ss=np.r_[0,np.cumsum(np.linalg.norm(np.diff(reference[ids],axis=0),axis=1))];ss/=ss[-1];tt=np.r_[0,np.cumsum(np.linalg.norm(np.diff(joints[chain],axis=0),axis=1))];tt/=tt[-1];rows=[]
 for t in tt:
  k=min(max(int(np.searchsorted(ss,t,side='right')-1),0),len(ids)-2);w=float(np.clip((t-ss[k])/(ss[k+1]-ss[k]),0,1));row=np.zeros(len(sn));row[ids[k]]=1-w;row[ids[k+1]]+=w;rows.append(row)
 return np.array(rows)
right=[0,5,6,7,8];tail=[0,*range(26,34)];bindings=[(right,binding(right,'hind_R')),(tail,binding(tail,'tail'))]
# Whole footprints are rigid; smoothly confine their adjoining low limb skin.
palm_ids=[4,8,20,25];chains=[[1,2,3,4],[5,6,7,8],[16,17,18,19,20],[21,22,23,24,25]];weights=np.zeros((len(bind),len(names)))
for v in body.data.vertices:
 for g in v.groups:weights[v.index,names.index(body.vertex_groups[g.group].name)]=g.weight
nearest=np.linalg.norm(bind[:,None,:2]-joints[palm_ids][None,:,:2],axis=2).argmin(1);changed=[];rigid={str(i):[] for i in palm_ids}
for vi,p in enumerate(bind):
 row=weights[vi].copy();k=int(nearest[vi]);palm=palm_ids[k];distance=np.linalg.norm(p[:2]-joints[palm,:2])
 if p[2]<.29 and distance<.26:
  confined=np.zeros(len(names));confined[chains[k]]=row[chains[k]]
  if confined.sum()<1e-8:confined[palm]=1
  confined/=confined.sum();blend=float(np.clip((.29-p[2])/.14,0,1));blend=blend*blend*(3-2*blend);row=confined*(1-blend);row[palm]+=blend
  if p[2]<.15:rigid[str(palm)].append(vi)
 if p[1]<-.50 and row[26:34].sum()>.3:
  amount=row[:26].sum();row[:26]=0;row[26:34]*=1+amount/max(row[26:34].sum(),1e-8)
 if np.max(abs(row-weights[vi]))<1e-7:continue
 for g in body.vertex_groups:g.remove([vi])
 keep=np.argsort(row)[-4:];row[[i for i in range(len(row)) if i not in keep]]=0;row/=row.sum()
 for i in np.flatnonzero(row>1e-8):body.vertex_groups[names[i]].add([vi],float(row[i]),'REPLACE')
 changed.append(vi)
def bake(label,poses,times):
 action=bpy.data.actions.new(rig.name+' | '+label+' | Anatomical repair');action.use_fake_user=True;slot=action.slots.new(id_type='OBJECT',name=rig.name);bag=action.layers.new('Correct physical chains').strips.new(type='KEYFRAME').channelbag(slot,ensure=True);locations=np.array([p[0] for p in poses]);rotations=np.array([p[1] for p in poses])
 for i in range(1,len(rotations)):rotations[i,np.sum(rotations[i]*rotations[i-1],axis=1)<0]*=-1
 for i,name in enumerate(names):
  for path,values in [('location',locations[:,i]),('rotation_quaternion',rotations[:,i])]:
   for axis in range(values.shape[1]):
    curve=bag.fcurves.new(data_path=f'pose.bones["{name}"].{path}',index=axis);curve.keyframe_points.add(len(times));curve.keyframe_points.foreach_set('co',np.c_[times,values[:,axis]].astype(np.float32).ravel())
    for key in curve.keyframe_points:key.interpolation='LINEAR'
 return action
cache={};new_actions={};source_actions=e['actions'].copy()
for label,name in source_actions.items():
 original=bpy.data.actions[name];rig.animation_data_create().action=original;rig.animation_data.action_slot=original.slots[0];first,last=map(float,original.frame_range);times=np.unique(np.r_[np.arange(first,last,.25),last]);samples=[];clip=next(c for c in donor['clips'] if c['label']==label);frames=motion[clip['key']]
 for frame in times:
  s.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update();matrices=[rig.pose.bones[n].matrix.copy() for n in names];x=(frame-first)/max(last-first,1e-8)*(len(frames)-1);lo=int(x);hi=min(lo+1,len(frames)-1);positions=frames[lo,:,:3,3]*(1-x+lo)+frames[hi,:,:3,3]*(x-lo)
  for chain,b in bindings:
   ref=b@reference;now=b@positions
   for k,i in enumerate(chain[1:],1):
    previous=k-1 if k==len(chain)-1 else k;next_index=k if k==len(chain)-1 else k+1;rv=Vector(ref[next_index]-ref[previous]);nv=Vector(now[next_index]-now[previous]);delta=rv.normalized().rotation_difference(nv.normalized()) if rv.length>1e-6 and nv.length>1e-6 else Quaternion();q=delta@rest[i].to_quaternion();m=q.to_matrix().to_4x4();m.translation=matrices[parents[i]]@local[i].translation;matrices[i]=m
  # Existing good joint tracks stay, but remove unstable axial roll from all
  # foreleg segments. A bounded palm pitch avoids the two flips per cycle.
  for chain in chains[2:]:
   for i in chain:
    q=rest[i].to_3x3().col[1].normalized().rotation_difference(matrices[i].to_3x3().col[1].normalized())@rest[i].to_quaternion();m=q.to_matrix().to_4x4();m.translation=matrices[i].translation;matrices[i]=m
  for i in palm_ids:
   delta=matrices[i].to_quaternion()@rest[i].to_quaternion().inverted();pitch=float(np.clip(delta.to_euler('XYZ').x,-.72,.72));m=(Quaternion((1,0,0),pitch)@rest[i].to_quaternion()).to_matrix().to_4x4();m.translation=matrices[i].translation;matrices[i]=m
  locations=[];rotations=[]
  for i,parent in enumerate(parents):
   relative=matrices[parent].inverted()@matrices[i] if parent>=0 else matrices[i];basis=local[i].inverted()@relative;locations.append(basis.translation[:]);rotations.append(basis.to_quaternion()[:])
  samples.append((np.array(locations),np.array(rotations)))
 if label in {'Walk','Run'}:
  times=np.r_[times,last+1.];samples.append((samples[0][0].copy(),samples[0][1].copy()))
 repaired=bake(label,samples,times);new_actions[label]=repaired.name
 if label in {'Walk','Run'}:
  rig.animation_data.action=repaired;rig.animation_data.action_slot=repaired.slots[0];floor=float('inf')
  for frame in np.linspace(times[0],times[-1],97):
   s.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update();ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();floor=min(floor,min((ev.matrix_world@v.co).z for v in mesh.vertices));ev.to_mesh_clear()
  shift=rest[0].to_3x3().inverted()@Vector((0,0,max(0,.004-floor)))
  for locations,rotations in samples:locations[0]+=np.array(shift)
  for layer in repaired.layers:
   for strip in layer.strips:
    for bag in strip.channelbags:
     for curve in bag.fcurves:
      if curve.data_path==f'pose.bones["{names[0]}"].location':
       for key in curve.keyframe_points:key.co.y+=shift[curve.array_index];key.handle_left.y+=shift[curve.array_index];key.handle_right.y+=shift[curve.array_index]
 cache[label]=(times,samples)
e['actions']=new_actions
def sample(label,seconds):
 times,poses=cache[label];x=(seconds*30)%max(1,times[-1]-times[0])+times[0];k=min(max(int(np.searchsorted(times,x,side='right')-1),0),len(times)-2);return blend_pose(poses[k],poses[k+1],float((x-times[k])/(times[k+1]-times[k])))
for title,parts in [('Locomotion',[('Idle',2),('Walk',3),('Run',3),('Walk',2),('Idle',2)]),('Showcase',[('Idle',2),*[(label,min(5,max(1.5,(cache[label][0][-1]+1)/30))) for label in cache if label not in {'Idle','Death'}],('Idle',2)])]:
 poses=[];previous=None;last_duration=0
 for label,duration in parts:
  for f in range(round(duration*30)):
   t=f/30;pose=sample(label,t)
   if previous and t<.5:pose=blend_pose(sample(previous,last_duration+t),pose,smooth(t/.5))
   poses.append(pose)
  previous=label;last_duration=duration
 e['demos'][title]=bake(title,poses,np.arange(len(poses))).name
for i in range(5,9):e['groups'][i]='hind'
for i in range(26,34):e['groups'][i]='tail'
e['owner_motion_repair']={'right_hind':[5,6,7,8],'tail':list(range(26,34)),'paw_bones':palm_ids,'source_bank':'Existing CC0 fox bank; explicit physical paths','limits':'Prototype retarget; no collision/contact solver or natural gait approval.'};s['deer_four_rigs']=json.dumps([e]);rig.animation_data.action=bpy.data.actions[e['actions']['Run']];rig.animation_data.action_slot=rig.animation_data.action.slots[0];s.frame_set(0);out=folder/'Animal - Motion Repaired.blend';bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(out));assert np.array_equal(bind,np.array([v.co[:] for v in body.data.vertices]));assert hashlib.sha256(source.read_bytes()).hexdigest()==digest
record={'source':str(source.relative_to(repo)),'source_sha256':digest,'output':str(out.relative_to(repo)),'output_sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'changed_skin_vertices':changed,'rigid_paws':rigid,'source_motion_sha256':hashlib.sha256((bank/'source-motions.npz').read_bytes()).hexdigest(),'actual_anatomical_paths':e['owner_motion_repair'],'owner_approved':False};(folder/'run-repair.json').write_text(json.dumps(record,indent=2)+'\n');print('Fox physical hind leg and tail repaired',len(changed))
