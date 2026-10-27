"""CMU/cgspeed BVH -> the existing semantic retargeter's motion interface.

Captured body motion is retained. Remove world travel, select a complete stride
and distribute the small loop seam correction; never rebuild the target rig.
"""
import math
from pathlib import Path
import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

NAMES = {'pelvis':'Hips','spine_01':'LowerBack','spine_02':'Spine','spine_03':'Spine1',
         'neck_01':'Neck','head':'Head'}
for side, word in [('l','Left'),('r','Right')]:
    for key, name in [('clavicle','Shoulder'),('upperarm','Arm'),('lowerarm','ForeArm'),
                      ('hand','Hand'),('thigh','UpLeg'),('calf','Leg'),('foot','Foot'),('ball','ToeBase')]:
        NAMES[key+'_'+side] = word+name

def mix(a,b,t):
    return Matrix.LocRotScale(a.translation.lerp(b.translation,t),
                             a.to_quaternion().slerp(b.to_quaternion(),t),Vector((1,1,1)))

class CmuMotion:
    license='CMU motion capture: free for all uses; cgspeed BVH conversion'
    use_idle_fingers=True
    def __init__(self,path):
        self.path=Path(path)
        text=self.path.read_text()
        count=int(text.split('Frames:')[1].split()[0]);self.dt=float(text.split('Frame Time:')[1].split()[0])
        bpy.ops.preferences.addon_enable(module='io_anim_bvh')
        bpy.ops.import_anim.bvh(filepath=str(self.path),global_scale=.056444,rotate_mode='QUATERNION',
            axis_forward='-Z',axis_up='Y',frame_start=1,use_fps_scale=False,update_scene_fps=True)
        rig=bpy.context.object;scene=bpy.context.scene
        def pose(frame):
            scene.frame_set(frame);return {key:rig.matrix_world@rig.pose.bones[name].matrix.copy() for key,name in NAMES.items()}
        # The converter inserts a calibrated T-pose as frame one. It is not gait.
        self.rest=pose(1)
        origin=self.rest['pelvis'].translation.copy();origin.z=0
        for matrix in self.rest.values():matrix.translation-=origin
        self.rest['Head']=self.rest['head'].copy()
        raw=[pose(frame) for frame in range(2,count+1)]
        travel=raw[-2]['pelvis'].translation-raw[1]['pelvis'].translation;travel.z=0
        yaw=-math.pi/2-math.atan2(travel.y,travel.x);rotation=Matrix.Rotation(yaw,4,'Z')
        converted=[]
        for frame in raw:
            origin=frame['pelvis'].translation.copy();origin.z=0
            result={}
            for key,matrix in frame.items():
                m=rotation@matrix;m.translation=rotation.to_3x3()@(matrix.translation-origin)
                result[key]=m
            converted.append(result)
        forward=np.array([-(f['foot_l'].translation-f['pelvis'].translation).y for f in converted])
        pairs=[]
        for sign in (1,-1):
            signal=forward*sign;peaks=[]
            for i in range(3,len(signal)-3):
                if signal[i]>=max(signal[i-3:i]) and signal[i]>max(signal[i+1:i+4]):
                    if peaks and (i-peaks[-1])*self.dt<.25:
                        if signal[i]>signal[peaks[-1]]:peaks[-1]=i
                    else:peaks.append(i)
            pairs.extend((a,b) for a,b in zip(peaks,peaks[1:]) if .35<=(b-a)*self.dt<=1.25)
        if not pairs:raise ValueError('No complete running stride: '+str(self.path))
        self.start,self.end=min(pairs,key=lambda p:abs((p[0]+p[1])/2-len(raw)/2))
        self.seconds=(self.end-self.start)*self.dt
        displacement=raw[self.end]['pelvis'].translation-raw[self.start]['pelvis'].translation;displacement.z=0
        self.speed=displacement.length/self.seconds
        foot_z=[min(f['ball_l'].translation.z,f['ball_r'].translation.z) for f in converted[self.start:self.end+1]]
        offset=float(np.percentile(foot_z,10))-min(self.rest['ball_l'].translation.z,self.rest['ball_r'].translation.z)
        for frame in converted:
            for matrix in frame.values():matrix.translation.z-=offset
            frame['Head']=frame['head'].copy()
        self.frames=converted
        self.first=converted[self.start];self.last=converted[self.end]
        self.phase=0
        bpy.data.objects.remove(rig,do_unlink=True)
        self.provenance={'source_file':self.path.name,'source_frames':count,'frame_time':self.dt,
            'cycle_start':self.start+2,'cycle_end':self.end+2,'duration':self.seconds,'source_speed_mps':self.speed}

    def duration(self,name):return self.seconds

    def align_to(self,reference,name='Walk_Loop'):
        def forward(sample):return -(sample['foot_l'].translation-sample['foot_r'].translation).y
        target=max(range(120),key=lambda i:forward(reference.sample(name,i*reference.duration(name)/120)))
        current=max(range(120),key=lambda i:forward(self.sample(None,i*self.seconds/120)))
        self.phase=((current-target)/120)%1
        self.provenance['phase_offset']=self.phase

    def sample(self,name,time):
        u=(max(0,min(1,time/self.seconds))+self.phase)%1;at=self.start+u*(self.end-self.start)
        lo=int(at);hi=min(self.end,lo+1);alpha=at-lo;weight=u*u*(3-2*u)
        result={}
        for key in self.rest:
            m=mix(self.frames[lo][key],self.frames[hi][key],alpha)
            correction=self.first[key].to_quaternion()@self.last[key].to_quaternion().inverted()
            q=Quaternion().slerp(correction,weight)@m.to_quaternion()
            p=m.translation+(self.first[key].translation-self.last[key].translation)*weight
            result[key]=Matrix.LocRotScale(p,q,Vector((1,1,1)))
        return result
