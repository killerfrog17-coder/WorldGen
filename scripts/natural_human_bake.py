"""Native-rig authoring corrections shared by all six bodies. No skin edits."""
import math
from mathutils import Quaternion,Vector
import bpy
import numpy as np

LEG_PREFIX=('thigh_','calf_','foot_','ball_')
def curves(action):return action.layers[0].strips[0].channelbag(action.slots[0]).fcurves
def mapping(action):return {(f.data_path,f.array_index):f for f in curves(action)}

def is_upper(role,loco):
 return role not in loco and role not in {'Gather','Gather_Left','WorkDig'} and not role.startswith(('Throw','SpearThrow'))

def pin_ordinary_legs(rig,action,idle,frames):
 dst=mapping(action);src=mapping(idle);idle_end=int(idle.frame_range[1]);saved=[]
 def q(name,frame):
  path=f'pose.bones["{name}"].rotation_quaternion'
  return Quaternion([dst[path,i].evaluate(frame) for i in range(4)]).normalized()
 for frame in range(1,frames+1):saved.append((q('pelvis',frame),q('spine_01',frame)))
 for f in curves(action):
  bone=f.data_path.split('"')[1]
  if bone!='pelvis' and not bone.startswith(LEG_PREFIX):continue
  original=src.get((f.data_path,f.array_index))
  if original is None:continue
  for key in f.keyframe_points:key.co.y=original.evaluate(1)
 # Retain a restrained contribution from the original weight-shift/torso turn,
 # transferred to the spine without importing its knees or vertical squat.
 pelvis_frame=(rig.matrix_world@rig.data.bones['pelvis'].matrix_local).to_quaternion()
 spine_frame=(rig.matrix_world@rig.data.bones['spine_01'].matrix_local).to_quaternion()
 path='pose.bones["spine_01"].rotation_quaternion'
 for frame,(hip,spine) in enumerate(saved,1):
  ip='pose.bones["pelvis"].rotation_quaternion';reference=Quaternion([dst[ip,i].evaluate(frame) for i in range(4)]).normalized()
  delta=hip@reference.inverted()
  if delta.w<0:delta.negate()
  angle=delta.angle;strength=min(.25,math.radians(10)/max(.001,angle));delta=Quaternion().slerp(delta,strength)
  correction=spine_frame.inverted()@pelvis_frame@delta@pelvis_frame.inverted()@spine_frame
  result=(correction@spine).normalized()
  for i in range(4):
   f=dst[path,i]
   key=next((k for k in f.keyframe_points if abs(k.co.x-frame)<.01),None)
   if key:key.co.y=result[i]

def restrain_ordinary_torso(action,idle,role):
 dst=mapping(action);src=mapping(idle);end=max(1,int(idle.frame_range[1])-1)
 calm=role.startswith(('SpearReady','ToolReady','ToolWalk','Bow','SlingReady','CarryLarge'))
 spear=role.startswith('SpearThrust')
 limits={'spine_01':4 if calm else 8 if spear else 12,'spine_02':3 if calm else 7 if spear else 10,'spine_03':3 if calm else 6 if spear else 8,
         'clavicle_l':5 if calm or spear else 12,'clavicle_r':5 if calm or spear else 12,
         'neck_01':8 if calm else 12,'head':8 if calm else 12}
 for name,degrees in limits.items():
  path=f'pose.bones["{name}"].rotation_quaternion'
  if (path,0) not in dst:continue
  rows=[]
  for key in dst[path,0].keyframe_points:
   frame=key.co.x;q=Quaternion([dst[path,i].evaluate(frame) for i in range(4)]).normalized()
   reference=Quaternion([src[path,i].evaluate(1) for i in range(4)]).normalized()
   delta=reference.inverted()@q
   if delta.w<0:delta.negate()
   bounded=reference@Quaternion().slerp(delta,min(1,math.radians(degrees)/max(.00001,delta.angle)))
   rows.append(bounded)
  for i in range(4):
   for key,q in zip(dst[path,i].keyframe_points,rows):key.co.y=q[i]

def smooth_ordinary_arms(action,loop):
 dst=mapping(action)
 for name in ['upperarm_l','lowerarm_l','hand_l','upperarm_r','lowerarm_r','hand_r']:
  path=f'pose.bones["{name}"].rotation_quaternion';fc=dst[path,0];count=len(fc.keyframe_points)
  source=[Quaternion([dst[path,i].keyframe_points[k].co.y for i in range(4)]).normalized() for k in range(count)]
  if loop:source=source[:-1]
  filtered=[]
  for k,q in enumerate(source):
   total=Vector((0,0,0,0));weight=0
   for offset,w in [(-2,1),(-1,2),(0,3),(1,2),(2,1)]:
    index=(k+offset)%len(source) if loop else max(0,min(len(source)-1,k+offset));other=source[index]
    if q.dot(other)<0:other=-other
    total+=Vector(tuple(other))*w;weight+=w
   average=Quaternion(tuple(total/weight)).normalized()
   strength=1 if loop else min(1,k/2,(len(source)-1-k)/2)
   filtered.append(q.slerp(average,strength))
  if loop:filtered.append(filtered[0])
  for i in range(4):
   for key,q in zip(dst[path,i].keyframe_points,filtered):key.co.y=q[i]

def ground_action(rig,action,frames):
 rest={name:(rig.matrix_world@rig.data.bones[name].head_local).z for name in ['foot_l','foot_r','ball_l','ball_r']}
 rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
 heights=[]
 for frame in range(1,frames+1,3):
  bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
  heights.append(min((rig.matrix_world@rig.pose.bones[name].matrix).translation.z-rest[name] for name in rest))
 offset=float(np.percentile(heights,8))
 if abs(offset)>.25:raise RuntimeError(f'Unreasonable native ground offset: {action.name}: {offset}')
 local_up=(rig.matrix_world.to_3x3()@rig.data.bones['pelvis'].matrix_local.to_3x3()).inverted()@Vector((0,0,1))
 for f in curves(action):
  if f.data_path=='pose.bones["pelvis"].location':
   for key in f.keyframe_points:key.co.y-=offset*local_up[f.array_index]
 return offset
