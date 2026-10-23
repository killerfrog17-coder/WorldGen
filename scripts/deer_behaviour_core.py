"""Small deterministic pose/IK controller shared by the offline builder/viewer.

No bpy, file IO, GPU inference, or hidden scene queries here. Quaternion order is
WXYZ. The Blender wrapper supplies terrain height and actor transforms.
"""
import math
import numpy as np
from mathutils import Matrix, Quaternion, Vector

Z=Vector((0,0,1))
F=Vector((-.91514678,-.40312079,0))
R=Vector((.40312079,-.91514678,0))


def smooth(x):
    x=max(0,min(1,float(x)))
    return x*x*x*(x*(x*6-15)+10)


def wrap_angle(x):
    return (x+math.pi)%(2*math.pi)-math.pi


def matrix(q,location):
    m=q.to_matrix().to_4x4()
    m.translation=location
    return m


def blend_pose(a,b,weight):
    weight=max(0,min(1,float(weight)))
    loc=a[0]*(1-weight)+b[0]*weight
    rot=np.array([Quaternion(qa).slerp(Quaternion(qb),weight)[:] for qa,qb in zip(a[1],b[1])])
    return loc,rot


class Skeleton:
    def __init__(self,meta):
        self.names=meta['bones']
        self.index={n:i for i,n in enumerate(self.names)}
        self.parents=meta['parents']
        self.rest=[Matrix(m) for m in meta['rest']]
        self.inverse=[m.inverted() for m in self.rest]
        self.local=[self.inverse[p]@m if p>=0 else m for p,m in zip(self.parents,self.rest)]
        self.descendants=[]
        for i in range(len(self.names)):
            found=[]
            for j in range(len(self.names)):
                p=j
                while p>=0:
                    if p==i:
                        found.append(j)
                        break
                    p=self.parents[p]
            self.descendants.append(found)
        self.feet=meta.get('feet',[])
        self.nose=Vector(meta.get('nose_local',[0,0,0]))

    def fk(self,pose):
        loc,rot=pose
        result=[]
        for i,p in enumerate(self.parents):
            b=matrix(Quaternion(rot[i]),Vector(loc[i]))
            m=self.local[i]@b
            result.append(result[p]@m if p>=0 else m)
        return result

    def basis(self,globals_m):
        loc=[]
        rot=[]
        for i,p in enumerate(self.parents):
            local=globals_m[p].inverted()@globals_m[i] if p>=0 else globals_m[i]
            b=self.local[i].inverted()@local
            loc.append(b.translation[:])
            rot.append(b.to_quaternion()[:])
        return np.array(loc),np.array(rot)

    def rotate_branch(self,globals_m,index,q):
        old=globals_m[index]
        new=matrix(q@old.to_quaternion(),old.translation)
        delta=new@old.inverted()
        for j in self.descendants[index]:
            globals_m[j]=delta@globals_m[j]

    def translate_all(self,globals_m,offset):
        offset=Vector(offset)
        for i,m in enumerate(globals_m):
            if self.names[i]!='MotionRoot':
                m.translation+=offset

    def solve_chain(self,globals_m,chain,goal,weight=1,iterations=10):
        """FABRIK with fixed lengths; seed from the authored bent-leg pose."""
        if weight<1e-6:
            return
        chain=[self.index[n] if isinstance(n,str) else n for n in chain]
        original=[globals_m[i].translation.copy() for i in chain]
        points=[p.copy() for p in original]
        goal=points[-1].lerp(Vector(goal),max(0,min(1,weight)))
        anchor=points[0].copy()
        lengths=[(b-a).length for a,b in zip(points,points[1:])]
        reach=sum(lengths)
        if (goal-anchor).length>=reach*.999:
            axis=(goal-anchor).normalized()
            for j,length in enumerate(lengths):
                points[j+1]=points[j]+axis*length
        else:
            for _ in range(iterations):
                points[-1]=goal.copy()
                for j in range(len(points)-2,-1,-1):
                    direction=points[j]-points[j+1]
                    if direction.length<1e-6:
                        direction=original[j]-original[j+1]
                    points[j]=points[j+1]+direction.normalized()*lengths[j]
                points[0]=anchor.copy()
                for j in range(len(points)-1):
                    direction=points[j+1]-points[j]
                    if direction.length<1e-6:
                        direction=original[j+1]-original[j]
                    points[j+1]=points[j]+direction.normalized()*lengths[j]
                if (points[-1]-goal).length<.0002:
                    break
        for j,index in enumerate(chain[:-1]):
            current=globals_m[chain[j+1]].translation-globals_m[index].translation
            desired=points[j+1]-points[j]
            if current.length>1e-6 and desired.length>1e-6:
                self.rotate_branch(globals_m,index,current.rotation_difference(desired))

    def sole(self,globals_m,foot):
        return globals_m[self.index[foot['bone']]]@Vector(foot['sole_local'])

    def ground_feet(self,globals_m,contacts,height=lambda x,y:0,actor=Matrix.Identity(4),
                    locks=None,weight=1):
        inverse_actor=actor.inverted()
        diagnostics=[]
        for k,foot in enumerate(self.feet):
            idx=self.index[foot['bone']]
            current=self.sole(globals_m,foot)
            world=actor@current
            floor=height(world.x,world.y)
            contact=float(contacts[k])*weight
            # Swing feet retain their trajectory; only prevent terrain intrusion.
            penetration=max(0,floor+.004-world.z)
            if contact<.001 and penetration<.0005:
                continue
            desired=world.copy()
            if locks and locks[k] is not None and contact>.01:
                desired.x,desired.y=locks[k][0],locks[k][1]
            desired.z=max(floor+.004,world.z*(1-contact)+(floor+.004)*contact)
            desired_local=inverse_actor@desired
            # Keep a planted hoof roughly aligned with the local ground normal.
            if contact>.1:
                h=.025
                dx=(height(desired.x+h,desired.y)-height(desired.x-h,desired.y))/(2*h)
                dy=(height(desired.x,desired.y+h)-height(desired.x,desired.y-h))/(2*h)
                normal_world=Vector((-dx,-dy,1)).normalized()
                normal=inverse_actor.to_3x3()@normal_world
                up=(globals_m[idx].to_3x3()@self.inverse[idx].to_3x3())@Z
                correction=up.normalized().rotation_difference(normal.normalized())
                axis,angle=correction.to_axis_angle()
                correction=Quaternion(axis,angle*contact)
                self.rotate_branch(globals_m,idx,correction)
            offset=globals_m[idx].to_3x3()@Vector(foot['sole_local'])
            ankle=desired_local-offset
            foot_q=globals_m[idx].to_quaternion()
            self.solve_chain(globals_m,foot['chain'],ankle,weight=1)
            self.rotate_branch(globals_m,idx,foot_q@globals_m[idx].to_quaternion().inverted())
            diagnostics.append((k,(actor@self.sole(globals_m,foot)).z-floor))
        return diagnostics

    def reach_ground(self,globals_m,clearance=.08):
        """Head/neck reach on a feeding base; authored limb motion stays intact."""
        head=self.index['Head']
        head_q=globals_m[head].to_quaternion()
        nose=globals_m[head]@self.nose
        goal=nose.copy()
        goal.z=clearance
        chain=['Chest','Neck1','Neck2','Neck3','Head']
        points=[globals_m[self.index[n]].translation for n in chain]
        offset=globals_m[head].to_3x3()@self.nose
        ankle=goal-offset
        reach=sum((b-a).length for a,b in zip(points,points[1:]))
        missing=(ankle-points[0]).length-reach*.98
        if missing>0:
            self.translate_all(globals_m,(0,0,-min(.14,missing)))
        protected={i:globals_m[i].copy() for i in self.descendants[self.index['Chest']]
                   if i not in self.descendants[self.index['Neck1']]}
        self.solve_chain(globals_m,chain,ankle,weight=.9,iterations=14)
        # Chest is the mathematical reach pivot, not a request to rotate the
        # ribcage/shoulders and pull the supporting forelegs off the ground.
        for i,m in protected.items():
            globals_m[i]=m
        self.rotate_branch(globals_m,head,head_q@globals_m[head].to_quaternion().inverted())

    def gaze(self,globals_m,yaw,pitch,weight=1,ears=0,time=0):
        yaw=max(-math.radians(55),min(math.radians(55),yaw))*weight
        pitch=max(-math.radians(30),min(math.radians(25),pitch))*weight
        for name,share in [('Neck1',.2),('Neck2',.35),('Neck3',.45)]:
            q=Quaternion(Z,yaw*share)@Quaternion(R,-pitch*share)
            self.rotate_branch(globals_m,self.index[name],q)
        if ears>0:
            for name,period,offset in [('Ear_L',6.1,0),('Ear_R',7.3,2.4)]:
                phase=(time+offset)%period
                flick=math.sin(math.pi*phase/.5)**2 if phase<.5 else 0
                idx=self.index[name]
                axis=globals_m[idx].to_3x3()@Vector((0,0,1))
                self.rotate_branch(globals_m,idx,Quaternion(axis,ears*math.radians(7)*flick))

    def fold_rest(self,globals_m):
        """Fit the donor's folded legs to the deer's longer lower limbs.

        Keep bone lengths; flatten the tucked segments rather than leaving
        fetlocks beneath the resting chest like little stilts.
        """
        for foot in self.feet:
            chain=[self.index[n] for n in foot['chain']]
            for index,child in zip(chain,chain[1:]):
                direction=globals_m[child].translation-globals_m[index].translation
                desired=Vector((direction.x,direction.y,direction.z*.30))
                if direction.length>.001 and desired.length>.001:
                    self.rotate_branch(globals_m,index,direction.rotation_difference(desired))
            index=self.index[foot['bone']]
            offset=globals_m[index].to_3x3()@Vector(foot['sole_local'])
            desired=Vector((offset.x,offset.y,offset.z*.1))
            if desired.length>.001:
                self.rotate_branch(globals_m,index,offset.rotation_difference(desired))


class Library:
    def __init__(self,meta,arrays):
        self.meta=meta
        self.skeleton=Skeleton(meta)
        self.arrays={k:arrays[k] for k in arrays.files} if hasattr(arrays,'files') else arrays
        self.clips=meta['clips']

    def sample(self,name,time):
        info=self.clips[name]
        frames=info['frames']
        duration=info['duration']
        time=time%duration if info['loop'] and duration>0 else max(0,min(duration,time))
        f=time*info['fps']
        first=min(frames-1,int(f))
        second=min(frames-1,first+1)
        u=f-first
        locs=self.arrays[name+'_loc']
        rots=self.arrays[name+'_rot']
        contacts=self.arrays[name+'_contact']
        pose=blend_pose((locs[first],rots[first]),(locs[second],rots[second]),u)
        contact=contacts[first]*(1-u)+contacts[second]*u
        yaws=self.arrays[name+'_yaw']
        yaw=float(yaws[first]*(1-u)+yaws[second]*u)
        shifts=self.arrays[name+'_shift']
        shift=shifts[first]*(1-u)+shifts[second]*u
        return pose,contact,yaw,shift
