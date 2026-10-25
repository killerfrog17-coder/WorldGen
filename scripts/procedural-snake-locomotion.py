"""Owner-requested smooth longitudinal snake locomotion on existing40-bone rigs.

Project-authored traveling curvature with exact segment lengths and adjacent skin.
Two clips: planar lateral slither and a lifted sidewinding-style preview. These
are visual prototypes, not a friction/contact simulation or verified snake mocap.
"""
import argparse,hashlib,json,math,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Quaternion,Vector
repo=Path(__file__).resolve().parent.parent;sys.path.insert(0,str(repo/'scripts'))
from deer_behaviour_core import blend_pose,smooth
p=argparse.ArgumentParser();p.add_argument('--slug',choices=['python','venomous-snake'],required=True)
a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);root=repo/'artifacts/fauna-overnight-20261024'/a.slug
source=root/'Animal - Raw Audition.blend';out=root/'Animal - Motion Revision.blend'
bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;entries=json.loads(s['deer_four_rigs'])
proof={'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'method':'Project-authored smooth traveling tangent wave; exact rest segment lengths, existing adjacent skin,60Hz keys','mocap':False,'physics_contact_simulation':False,'candidates':[]}

def bake(rig,label,poses,names,step=.5):
    action=bpy.data.actions.new(rig.name+' | Smooth '+label);action.use_fake_user=True
    slot=action.slots.new(id_type='OBJECT',name=rig.name);bag=action.layers.new('Own procedural snake curve').strips.new(type='KEYFRAME').channelbag(slot,ensure=True)
    loc=np.array([p[0] for p in poses]);rot=np.array([p[1] for p in poses])
    for k in range(1,len(rot)):rot[k,np.sum(rot[k]*rot[k-1],axis=1)<0]*=-1
    for i,n in enumerate(names):
        for path,values in [('location',loc[:,i]),('rotation_quaternion',rot[:,i])]:
            for axis in range(values.shape[1]):
                c=bag.fcurves.new(data_path=f'pose.bones["{n}"].{path}',index=axis);c.keyframe_points.add(len(poses));c.keyframe_points.foreach_set('co',np.c_[np.arange(len(poses))*step,values[:,axis]].astype(np.float32).ravel())
                for key in c.keyframe_points:key.interpolation='LINEAR'
                c.update()
    return action.name

for number,e in enumerate(entries,1):
    body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']];body.hide_set(False);rig.hide_set(False);rig.animation_data_clear();rig.data.pose_position='POSE'
    names=e['joint_names'];parents=e['parents'];j=np.array(e['generated_joint_heads']);assert parents==list(range(-1,len(j)-1))
    # The old slice-median centreline contains small alternating kinks. Those
    # kinks become visible axial skin distortions when bent. Replace only the
    # rest centreline/skin with a smooth straight chain; keep bind vertices exact.
    vertices=np.array([p.co[:] for p in body.data.vertices]);j[:,0]=np.median(vertices[:,0]);j[:,1]=np.linspace(j[0,1],j[-1,1],len(j));j[:,2]=np.median(vertices[:,2])
    rig.data.pose_position='REST';bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
    for i,n in enumerate(names):
        b=rig.data.edit_bones[n];b.head=j[i];b.tail=j[i+1] if i+1<len(j) else j[i]+(j[i]-j[i-1]);b.align_roll(Vector((0,0,1)))
    bpy.ops.object.mode_set(mode='OBJECT');rig.data.pose_position='POSE';e['generated_joint_heads']=j.tolist()
    for vg in list(body.vertex_groups):body.vertex_groups.remove(vg)
    vgs=[body.vertex_groups.new(name=n) for n in names]
    parameter=np.clip((j[0,1]-vertices[:,1])/(j[0,1]-j[-1,1])*(len(j)-1),0,len(j)-1)
    for vi,t in enumerate(parameter):
        k=min(int(t),len(j)-2);u=float(t-k);vgs[k].add([vi],1-u,'REPLACE');vgs[k+1].add([vi],u,'REPLACE')
    rest=[rig.data.bones[n].matrix_local.copy() for n in names];local=[rest[p].inverted()@r if p>=0 else r for p,r in zip(parents,rest)]
    lengths=np.linalg.norm(np.diff(j,axis=0),axis=1);arc=np.r_[0,np.cumsum(lengths)];arc/=arc[-1];total=lengths.sum();cache={};actions={};floors=[];segment_error=0
    def pose(phase,mode):
        if mode=='Idle':points=j.copy()
        else:
            thick=a.slug=='venomous-snake';waves=1.20 if thick else 1.5
            angle=math.tau*(waves*arc-phase);envelope=np.clip(arc/(.20 if thick else .12),0,1)
            if thick:envelope=envelope*envelope*(3-2*envelope)
            amplitude=(.55 if mode=='Slither' else .72) if thick else (.70 if mode=='Slither' else .95)
            yaw=amplitude*np.sin(angle)*envelope
            lift=np.zeros(len(j)) if mode=='Slither' else total*.025*np.maximum(0,np.sin(angle+math.pi/2))**2
            points=[np.array([0.,0.,j[0,2]+lift[0]])]
            for k,length in enumerate(lengths):
                dz=float(np.clip(j[k+1,2]-j[k,2]+lift[k+1]-lift[k],-length*.65,length*.65));flat=math.sqrt(max(0,length*length-dz*dz));theta=(yaw[k]+yaw[k+1])*.5
                points.append(points[-1]+np.array([math.sin(theta)*flat,-math.cos(theta)*flat,dz]))
            points=np.array(points);points[:,:2]+=j[:,:2].mean(0)-points[:,:2].mean(0)
        g=[]
        for i in range(len(j)):
            k=min(i,len(j)-2);old=Vector(j[k+1]-j[k]);new=Vector(points[k+1]-points[k]);q=old.rotation_difference(new)@rest[i].to_quaternion();m=q.to_matrix().to_4x4();m.translation=Vector(points[i]);g.append(m)
        def basis():
            b=[local[i].inverted()@(g[p].inverted()@g[i] if p>=0 else g[i]) for i,p in enumerate(parents)]
            return np.array([m.translation[:] for m in b]),np.array([m.to_quaternion()[:] for m in b])
        value=basis()
        for n,l,q in zip(names,*value):rig.pose.bones[n].location=l;rig.pose.bones[n].rotation_mode='QUATERNION';rig.pose.bones[n].rotation_quaternion=q
        bpy.context.view_layer.update();ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();floor=min((ev.matrix_world@v.co).z for v in mesh.vertices);ev.to_mesh_clear()
        for m in g:m.translation.z+=.003-floor
        return basis(),float(np.max(np.abs(np.linalg.norm(np.diff(points,axis=0),axis=1)-lengths)))
    stats={}
    for label,seconds in [('Idle',2),('Slither',3),('Side winding',2.4)]:
        poses=[];errors=[]
        for k in range(round(seconds*60)+1):
            value,error=pose(k/(seconds*60),label);poses.append(value);errors.append(error)
        cache[label]=poses;actions[label]=bake(rig,label,poses,names);stats[label]={'keys_hz':60,'seconds':seconds,'max_segment_length_error_m':max(errors),'loop_location_error':float(np.max(np.abs(poses[0][0]-poses[-1][0])))}
    demos={}
    for title,parts in [('Showcase',[('Idle',2),('Slither',5),('Side winding',5),('Idle',2)]),('Locomotion',[('Idle',2),('Slither',6),('Idle',2)])]:
        poses=[];previous=None
        for label,seconds in parts:
            for k in range(seconds*60):
                value=cache[label][k%(len(cache[label])-1)]
                if previous and k<30:value=blend_pose(previous,value,smooth(k/30))
                poses.append(value)
            previous=poses[-1]
        demos[title]=bake(rig,title,poses,names)
    e.update(actions=actions,demos=demos,source_license='Project-authored procedural motion',source_family='Own procedural snake',manual_repairs=True,motion_repairs={'method':proof['method']},locomotion_sequence=['Idle','Slither','Idle'],locomotion_durations_s=[2,6,2])
    proof['candidates'].append({'candidate':number,'clips':stats});body.hide_set(number!=1);body.hide_render=number!=1;rig.hide_set(True)
s['deer_four_rigs']=json.dumps(entries);s.frame_set(0);bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(out))
proof['output_sha256']=hashlib.sha256(out.read_bytes()).hexdigest();(root/'procedural-snake-revision.json').write_text(json.dumps(proof,indent=2)+'\n');print('Smooth snake clips built',a.slug,flush=True)
