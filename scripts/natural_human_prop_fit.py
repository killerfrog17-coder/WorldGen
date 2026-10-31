"""Offline native-character prop rehearsal. Mirrors handling dimensions; final
acceptance also uses independent native-frame exports and real game screenshots.
"""
import math,re
from mathutils import Vector,Quaternion,Matrix
import bpy

UP=Vector((0,0,1));RIGHT=Vector((-1,0,0));FORWARD=Vector((0,-1,0))
def clamp(x,a,b):return max(a,min(b,x))
def smooth(x):x=clamp(x,0,1);return x*x*(3-2*x)

class PropFit:
 def __init__(self,rig,variant):
  self.rig=rig
  text=(__import__('pathlib').Path(__file__).resolve().parent.parent/'unity/WorldGen.Unity/Assets/WorldGen/Resources/Humans/HumanLibrary.asset').read_text()
  self.height=float(re.search(r'Variant: '+variant+r'.*?EyeHeight: ([\d.]+)',text,re.S).group(1));self.s=self.height/1.65
 def p(self,name):return (self.rig.matrix_world@self.rig.pose.bones[name].matrix).translation
 def q(self,name):return (self.rig.matrix_world@self.rig.pose.bones[name].matrix).to_quaternion()
 def rot(self,name,q):
  bone=self.rig.pose.bones[name];world=self.rig.matrix_world@bone.matrix
  bone.matrix=self.rig.matrix_world.inverted()@Matrix.LocRotScale(world.translation,q,world.to_scale());bpy.context.view_layer.update()
 def reach(self,side):return ((self.p('upperarm_'+side)-self.p('lowerarm_'+side)).length+(self.p('lowerarm_'+side)-self.p('hand_'+side)).length)*.98
 def grip(self,side):return self.p('hand_'+side).lerp(self.p('middle_01_'+side),.7)
 def axis(self,side):
  a=self.p('index_01_'+side)-self.p('pinky_01_'+side)
  return (-a if side=='r' else a).normalized()
 def wrist(self,side,shaft):
  shaft=shaft.normalized();width=self.axis(side);self.rot('hand_'+side,width.rotation_difference(shaft)@self.q('hand_'+side))
  fingers=self.p('middle_01_'+side)-self.p('hand_'+side);fingers-=shaft*fingers.dot(shaft)
  arm=self.p('hand_'+side)-self.p('lowerarm_'+side);arm-=shaft*arm.dot(shaft)
  if fingers.length>1e-5 and arm.length>1e-5:
   angle=math.atan2(shaft.dot(fingers.cross(arm)),fingers.dot(arm));self.rot('hand_'+side,Quaternion(shaft,angle)@self.q('hand_'+side))
 def solve(self,side,target,hint=None,extension=.98):
  shoulder=self.p('upperarm_'+side);elbow=self.p('lowerarm_'+side);hand=self.p('hand_'+side)
  a=(shoulder-elbow).length;b=(elbow-hand).length;delta=target-shoulder
  distance=clamp(delta.length,abs(a-b)+.002,(a+b)*extension);axis=delta.normalized()
  bend=hint if hint is not None else RIGHT*(1 if side=='r' else -1)-UP*.8
  bend=(bend-axis*bend.dot(axis)).normalized();along=(a*a+distance*distance-b*b)/(2*distance)
  point=shoulder+axis*along+bend*math.sqrt(max(0,a*a-along*along))
  self.rot('upperarm_'+side,(elbow-shoulder).rotation_difference(point-shoulder)@self.q('upperarm_'+side))
  elbow=self.p('lowerarm_'+side);hand=self.p('hand_'+side)
  self.rot('lowerarm_'+side,(hand-elbow).rotation_difference(shoulder+axis*distance-elbow)@self.q('lowerarm_'+side))
 def fit(self,side,target,axis,hint=None,extension=.98):
  for _ in range(3):self.solve(side,target-(self.grip(side)-self.p('hand_'+side)),hint,extension);self.wrist(side,axis)
 def furthest(self,origin,axis,side):
  delta=origin-self.p('upperarm_'+side);along=delta.dot(axis)
  return -along+math.sqrt(max(0,(self.reach(side)-.015*self.s)**2-max(0,delta.length_squared-along*along)))
 def spear(self,role,t,contact=.45):
  side='l' if role.endswith('_Left') else 'r';other='r' if side=='l' else 'l';sign=1 if side=='r' else -1;s=self.s
  two='2' in role;active='Thrust' in role
  axis=(FORWARD*.76-RIGHT*(.65 if two else .16)*sign+UP*.06).normalized();intent=1 if active else 0
  if active:axis=FORWARD.copy()
  attack=smooth(t/max(.01,contact)) if t<contact else 1-smooth((t-contact)/max(.01,1-contact));extension=.22*attack if active else 0
  height=self.p('spine_03').z-((.27 if two else .31)*(1-intent)+.1*intent)*s
  z=(.16*(1-intent)-.08*intent+extension) if two else .13+extension
  target=RIGHT*((.28 if two else .34)*sign*s)+FORWARD*(z*s)+UP*height
  if two:
   available=self.furthest(target,axis,other)
   if available<.28*s and extension>0:target-=FORWARD*min(extension*s,.28*s-available)
  self.fit(side,target,axis)
  grip=self.grip(side)
  if two:self.fit(other,grip+axis*clamp(self.furthest(grip,axis,other),.24*s,.4*s),axis)
  return {'kind':'spear','grip':tuple(self.grip(side)),'axis':tuple(axis),'length':1.9,'fraction':.35,'main':side,'support':other if two else None}
 def bow(self,role,t):
  s=self.s;raised=smooth(t/.3) if role=='BowDraw' else 1-smooth((t-.12)/.48) if role=='BowRelease' else 0
  draw=smooth((t-.2)/.8) if role=='BowDraw' else 0
  forward=FORWARD.copy()
  if raised>.01:
   # Native runtime turns the whole stance 55 degrees, then only 15 more in
   # the torso. Rehearse that root turn through the pelvis on this fixed stage.
   self.rot('pelvis',Quaternion(UP,math.radians(-55*raised))@self.q('pelvis'))
   shoulders=self.p('upperarm_r')-self.p('upperarm_l');shoulders.z=0;shoulders.normalize();desired=Quaternion(UP,math.radians(-70*raised))@RIGHT
   angle=math.atan2(UP.dot(shoulders.cross(desired)),shoulders.dot(desired));self.rot('spine_01',Quaternion(UP,angle*raised)@self.q('spine_01'))
   self.rot('head',(self.rig.matrix_world@self.rig.data.bones['head'].matrix_local).to_quaternion())
  anchor=self.p('head')+RIGHT*.125*s+UP*.04*s+FORWARD*.08*s;ray=anchor-UP*1.5*.05
  delta=ray-self.p('upperarm_l');along=delta.dot(forward)
  available=-along+math.sqrt(max(0,(self.reach('l')+.08*s)**2-max(0,delta.length_squared-along*along)))
  length=max(.2*s,min(available,.72*s));aim=ray+forward*length
  relaxed=-RIGHT*.32*s+FORWARD*.18*s+UP*(self.p('pelvis').z+.19*s)
  carry_up=(UP+FORWARD*.35-RIGHT*.08).normalized();bow_up=carry_up.lerp(UP,raised).normalized()
  self.fit('l',relaxed.lerp(aim,raised),bow_up,extension=.98+.013*raised)
  if raised>.001:
   brace=self.grip('l')+UP*.075-forward*.35*.38
   right=brace.lerp(anchor,draw)
   if role=='BowRelease':right=(RIGHT*.29*s+FORWARD*.14*s+UP*(self.p('pelvis').z+.15*s)).lerp(anchor-forward*(.045*s*math.sin(clamp(t/.3,0,1)*math.pi))+RIGHT*.025*s,raised)
   self.fit('r',right,UP,RIGHT-FORWARD*.55+UP*.1)
  return {'kind':'bow','grip':tuple(self.grip('l')),'forward':tuple(forward),'up':tuple(bow_up),'draw_grip':tuple(self.grip('r')),'raised':raised,'draw':draw,'length':1.5,'width':.35}
 def tool(self,role,t):
  side='l' if role.endswith('_Left') else 'r';sign=1 if side=='r' else -1;s=self.s
  active=role.startswith(('ToolCut','ToolBlunt','ToolChop','Work'))
  axis=FORWARD.copy() if role.startswith('ToolCutB') else self.axis(side) if active else (FORWARD*.32-UP*.94+RIGHT*.06*sign).normalized()
  if not active:self.fit(side,RIGHT*(.32*sign*s)+FORWARD*.14*s+UP*(self.p('pelvis').z+.15*s),axis)
  else:self.wrist(side,axis)
  length=.65 if role.startswith(('ToolChop','ToolBlunt')) else .25
  return {'kind':'axe' if length>.3 else 'knife','grip':tuple(self.grip(side)),'axis':tuple(axis),'length':length,'fraction':.18}
 def swim(self,role):
  if role=='Swim':return None
  side='l' if role=='SwimCarryLeft' else 'r';sign=1 if side=='r' else -1;s=self.s
  target=self.p('head')+RIGHT*(sign*.42*s)+FORWARD*.10*s-UP*.10*s
  self.fit(side,target,FORWARD,FORWARD+RIGHT*(sign*.25)-UP*.1)
  return {'kind':'spear','grip':tuple(self.grip(side)),'axis':tuple(FORWARD),'length':1.9,'fraction':.35,'main':side}

def apply(rig,variant,role,t,contact=.45):
 fit=PropFit(rig,variant)
 if role.startswith(('SpearReady','SpearThrust')):return fit.spear(role,t,contact)
 if role.startswith('Bow'):return fit.bow(role,t)
 if role.startswith('Tool'):return fit.tool(role,t)
 if role.startswith('Swim'):return fit.swim(role)
 return None
