"""Scoped actual-CC0-track retarget and whole-paw skin repair for three auditions.

Keeps all four generated meshes and rest skeletons. Solves their actual limb
lengths against the licensed rat Walk/Run foot tracks, with bounded body motion.
No generated gait, sped-up Walk substitute, or fixed-radius paw-skin cutoff.
"""
import argparse, hashlib, json, math, sys
from pathlib import Path
import bpy, numpy as np
from mathutils import Matrix, Quaternion, Vector
repo=Path(__file__).resolve().parent.parent;sys.path.insert(0,str(repo/'scripts'))
from deer_behaviour_core import blend_pose,smooth
p=argparse.ArgumentParser();p.add_argument('--slug',choices=['nutria','porcupine','echidna'],required=True)
a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);root=repo/'artifacts/fauna-overnight-20261024'/a.slug
source=root/'Animal - Raw Audition.blend';out=root/'Animal - Motion Revision.blend'
bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene
entries=json.loads(scene['deer_four_rigs']);bank=repo/'artifacts/fauna-motion-sources/rat'
donor=json.loads((bank/'source-rig.json').read_text());arrays=np.load(bank/'source-motions.npz')
dn=[b['name'] for b in donor['bones']];di={n:i for i,n in enumerate(dn)}
drest=np.array([b['rest'] for b in donor['bones']]);droot=di[donor['root']]
report={'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'method':'Actual CC0 Walk/Run endpoint tracks, own fixed-length IK, complete low-paw skin confinement, bounded source body rotation','candidates':[]}

def closest_segments(v,start,end):
    delta=end-start;u=np.clip(np.sum((v[:,None]-start)*delta,axis=2)/np.maximum(np.sum(delta*delta,axis=1),1e-9),0,1)
    dd=np.linalg.norm(v[:,None]-(start+u[:,:,None]*delta),axis=2)
    return dd,u

def solve(seed,target):
    # Full3D fixed-length FABRIK, seeded with this animal's anatomical bend.
    points=seed.copy();anchor=points[0].copy();length=np.linalg.norm(np.diff(points,axis=0),axis=1)
    if np.linalg.norm(target-anchor)>length.sum()*.999:
        target=anchor+(target-anchor)/max(np.linalg.norm(target-anchor),1e-9)*length.sum()*.999
    for _ in range(32):
        points[-1]=target
        for k in range(len(points)-2,-1,-1):
            d=points[k]-points[k+1];points[k]=points[k+1]+d/max(np.linalg.norm(d),1e-9)*length[k]
        points[0]=anchor
        for k,l in enumerate(length):
            d=points[k+1]-points[k];points[k+1]=points[k]+d/max(np.linalg.norm(d),1e-9)*l
        if np.linalg.norm(points[-1]-target)<1e-5:break
    return points

def periodic_track(values,t,duration):
    unique=values[:-1];x=(t/duration*len(unique))%len(unique);k=int(x);u=x-k
    a,b,c,d=[unique[(k+offset)%len(unique)] for offset in (-1,0,1,2)]
    return .5*((2*b)+(-a+c)*u+(2*a-5*b+4*c-d)*u*u+(-a+3*b-3*c+d)*u*u*u)

def ensure_knee_and_palm(e,rig):
    names=list(e['joint_names']);parents=list(e['parents']);j=list(e['generated_joint_heads']);groups=list(e['groups']);binding=list(e['binding']);changed=[]
    children=[[i for i,p in enumerate(parents) if p==k] for k in range(len(names))];h=float(np.ptp(j,axis=0)[2]);additions=[]
    for kind in ('fore','hind'):
        for tip in [i for i in range(len(names)) if groups[i]==kind and not children[i]]:
            chain=[tip];parent=parents[tip]
            while parent>=0 and groups[parent]==kind and len(children[parent])==1:chain.append(parent);parent=parents[parent]
            chain=list(reversed(chain))
            while len(chain)>3 and abs(j[chain[0]][0]-j[0][0])<h*.04:chain=chain[1:]
            if len(chain)>3:continue
            if len(chain)==2:
                upper,foot=chain;knee=(np.array(j[upper])+np.array(j[foot]))*.5;knee[1]+=h*(-.10 if kind=='fore' else .10)
                idx=len(names);name=f'Manual knee {kind} {tip}';names.append(name);parents.append(upper);j.append(knee.tolist());groups.append(kind);binding.append(((np.array(binding[upper])+np.array(binding[foot]))*.5).tolist());parents[foot]=idx
                additions.append((name,knee,np.array(j[foot]),names[upper],names[foot]));changed.append(name)
            idx=len(names);name=f'Manual toe {kind} {tip}';toe=np.array(j[tip])+np.array([0,h*.065,-h*.018]);names.append(name);parents.append(tip);j.append(toe.tolist());groups.append(kind);binding.append(binding[tip]);additions.append((name,toe,toe+np.array([0,h*.04,0]),names[tip],None));changed.append(name)
    if not additions:return
    bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
    for name,head,tail,parent,reparent in additions:
        bone=rig.data.edit_bones.new(name);bone.head=head;bone.tail=tail;bone.parent=rig.data.edit_bones[parent];bone.align_roll(Vector((0,1,0)))
        if reparent:
            rig.data.edit_bones[reparent].parent=bone;rig.data.edit_bones[parent].tail=head
    bpy.ops.object.mode_set(mode='OBJECT');order=[]
    def visit(i):
        order.append(i)
        for child,p in enumerate(parents):
            if p==i:visit(child)
    visit(parents.index(-1));inverse={old:new for new,old in enumerate(order)}
    e.update(joint_names=[names[i] for i in order],parents=[inverse[parents[i]] if parents[i]>=0 else -1 for i in order],generated_joint_heads=[j[i] for i in order],groups=[groups[i] for i in order],binding=[binding[i] for i in order],joints=len(names),manual_added_joints=changed)

def bake(rig,label,poses,names):
    action=bpy.data.actions.new(rig.name+' | Revised '+label);action.use_fake_user=True
    slot=action.slots.new(id_type='OBJECT',name=rig.name);bag=action.layers.new('CC0 foot-track retarget').strips.new(type='KEYFRAME').channelbag(slot,ensure=True)
    loc=np.array([r[0] for r in poses]);rot=np.array([r[1] for r in poses])
    for k in range(1,len(rot)):rot[k,np.sum(rot[k]*rot[k-1],axis=1)<0]*=-1
    for k,n in enumerate(names):
        for path,values in [('location',loc[:,k]),('rotation_quaternion',rot[:,k])]:
            for axis in range(values.shape[1]):
                c=bag.fcurves.new(data_path=f'pose.bones["{n}"].{path}',index=axis);c.keyframe_points.add(len(poses));c.keyframe_points.foreach_set('co',np.c_[np.arange(len(poses)),values[:,axis]].astype(np.float32).ravel())
                for key in c.keyframe_points:key.interpolation='LINEAR'
                c.update()
    return action.name

for number,e in enumerate(entries,1):
    body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']];body.hide_set(False);rig.hide_set(False)
    rig.animation_data_clear();rig.data.pose_position='REST';ensure_knee_and_palm(e,rig);names=e['joint_names'];parents=np.array(e['parents']);j=np.array(e['generated_joint_heads']);groups=e['groups'];n=len(names)
    v=np.array([p.co[:] for p in body.data.vertices]);height=np.ptp(v[:,2]);edges=np.array([edge.vertices[:] for edge in body.data.edges])
    children=[np.flatnonzero(parents==i).tolist() for i in range(n)];legs=[]
    for kind in ['fore','hind']:
        tips=[i for i in range(n) if groups[i]==kind and not any(groups[c]==kind for c in children[i])]
        assert len(tips)==2,(a.slug,number,kind,tips)
        for tip in tips:
            chain=[tip];parent=int(parents[tip])
            while parent>=0 and groups[parent]==kind and len(children[parent])==1:
                chain.append(parent);parent=int(parents[parent])
            chain=list(reversed(chain))
            while len(chain)>3 and abs(j[chain[0],0]-j[0,0])<height*.04:
                chain=chain[1:]
            assert len(chain)>=3
            side='L' if j[chain[-1],0]<j[0,0] else 'R'
            legs.append({'kind':kind,'side':side,'chain':chain,'foot':chain[-2]})
    limb_nodes={i for leg in legs for i in leg['chain']}
    core=[i for i in range(n) if i not in limb_nodes and parents[i]>=0]
    coredd,coreu=closest_segments(v,j[parents[core]],j[core]);nearcore=coredd.argmin(1);core_distance=coredd.min(1)
    weight=np.zeros((len(v),n));rootid=int(np.flatnonzero(parents<0)[0])
    for vi,k in enumerate(nearcore):
        u=coreu[vi,k];weight[vi,int(parents[core[k]])]=1-u;weight[vi,core[k]]+=u
    distances=[];parameters=[]
    for leg in legs:
        chain=leg['chain'];dd,u=closest_segments(v,j[chain[:-1]],j[chain[1:]])
        distances.append(dd);parameters.append(u)
    nearest=np.argmin([dd.min(1) for dd in distances],axis=0)
    for k,leg in enumerate(legs):
        chain=leg['chain'];dd=distances[k];seg=dd.argmin(1);distance=dd.min(1)
        foot=leg['foot'];ceiling=j[foot,2]+height*.12
        own=(nearest==k);rigid=own&(v[:,2]<ceiling)&(distance<core_distance+.04*height)
        # No radius limit through the same paw: all low protrusions closer to
        # their own limb than the body/tail receive the same complete footprint.
        for vi in np.flatnonzero(own):
            mix=float(np.clip((core_distance[vi]-distance[vi]+height*.015)/(height*.15),0,1));mix=smooth(mix)
            mix*=smooth(float(np.clip((height*.20-distance[vi])/(height*.09),0,1)))
            knee_height=j[chain[(len(chain)-1)//2],2]
            mix*=smooth(float(np.clip((knee_height+height*.08-v[vi,2])/(height*.17),0,1)))
            u=float(parameters[k][vi,seg[vi]]);node=chain[seg[vi]];child=chain[seg[vi]+1]
            limb=np.zeros(n);blend=smooth(float(np.clip((u-.4)/.6,0,1)));limb[node]=1-blend;limb[child]=blend
            weight[vi]=weight[vi]*(1-mix)+limb*mix
        weight[rigid]=0;weight[rigid,foot]=1
        ids=np.flatnonzero(rigid);assert len(ids)>3,(a.slug,number,k,len(ids))
        leg['rigid_vertices']=ids;leg['floor_offset']=float((v[ids]-j[foot])[:,2].min())
    # Keep strongest4 without allowing an unrelated leg or fixed core in a paw.
    for group in list(body.vertex_groups):body.vertex_groups.remove(group)
    vgs=[body.vertex_groups.new(name=name) for name in names]
    for vi,row in enumerate(weight):
        best=np.argsort(row)[-4:];values=row[best]/row[best].sum()
        for node,value in zip(best,values):
            if value>1e-8:vgs[node].add([vi],float(value),'REPLACE')
    rest=[rig.data.bones[name].matrix_local.copy() for name in names];local=[rest[int(p)].inverted()@r if p>=0 else r for p,r in zip(parents,rest)]
    cache={};actions={};rig.data.pose_position='POSE'
    for label in ['Idle','Walk','Run']:
        clip=next(c for c in donor['clips'] if c['label']==label);raw=arrays[clip['key']].copy();duration=(len(raw)-1)/30
        # The source Run's last frame differs slightly. Close only its endpoint
        # before continuous periodic interpolation; retain the actual Run track.
        xyz=raw[:,:,:3,3].copy();xyz[-1]=xyz[0]
        frames=max(2,round(duration*30));poses=[];targets=[]
        rootmean=raw[:,droot,:3,3].mean(0)
        for frame in range(frames+1):
            t=frame/frames*duration;positions=periodic_track(xyz,t,duration);g=[]
            root_delta=positions[droot]-rootmean
            # Actual source vertical body bob, scaled to this animal's limbs.
            root_travel=np.array([0.,0.,root_delta[2]*.65])
            poseindex=min(len(raw)-1,round(t*30));donorframe=raw[poseindex]
            for i,parent in enumerate(parents):
                m=g[int(parent)]@local[i] if parent>=0 else rest[i].copy()
                if parent<0:m.translation+=Vector(root_travel)
                if i not in limb_nodes:
                    sourceidx=int(np.argmax(e['binding'][i]));delta=Matrix(donorframe[sourceidx]).to_quaternion()@Matrix(drest[sourceidx]).to_quaternion().inverted()
                    pitch=float(np.clip(delta.to_euler('XYZ').x*.30,-.12,.12))
                    q=Quaternion((1,0,0),pitch)@rest[i].to_quaternion();pos=m.translation.copy();m=q.to_matrix().to_4x4();m.translation=pos
                g.append(m)
            frame_targets=[]
            for leg in legs:
                chain=leg['chain'];solvechain=chain[:-1];ids=[di[x] for x in donor['paths'][leg['kind']+'_'+leg['side']]];foot_source=ids[-2]
                trace=raw[:,foot_source,:3,3];source_lengths=np.linalg.norm(np.diff(drest[ids[1:-1],:3,3],axis=0),axis=1).sum()
                length=np.linalg.norm(np.diff(j[solvechain],axis=0),axis=1).sum();scale=float(np.clip(length/max(source_lengths,1e-5),.30,1.30))
                desired=j[leg['foot']].copy();desired[1]+=(positions[foot_source,1]-trace[:,1].mean())*scale
                desired[2]=.005-leg['floor_offset']+max(0,float(positions[foot_source,2]-trace[:,2].min()))*scale
                anchor=g[int(parents[chain[0]])]@Vector(local[chain[0]].translation)
                seed=j[solvechain]+np.array(anchor)-j[solvechain[0]];solved=solve(seed,desired)
                for k,node in enumerate(solvechain[:-1]):
                    old=Vector(j[solvechain[k+1]]-j[node]);new=Vector(solved[k+1]-solved[k]);q=old.rotation_difference(new)@rest[node].to_quaternion()
                    m=q.to_matrix().to_4x4();m.translation=Vector(solved[k]);g[node]=m
                foot=leg['foot'];m=rest[foot].copy();m.translation=Vector(solved[-1]);g[foot]=m
                tip=chain[-1];g[tip]=g[foot]@local[tip]
                frame_targets.append(float(np.linalg.norm(solved[-1]-desired)))
            loc=[];rot=[]
            for i,parent in enumerate(parents):
                relative=g[int(parent)].inverted()@g[i] if parent>=0 else g[i];basis=local[i].inverted()@relative
                loc.append(basis.translation[:]);rot.append(basis.to_quaternion()[:])
            poses.append((np.array(loc),np.array(rot)));targets.append(max(frame_targets))
        cache[label]=poses;actions[label]=bake(rig,label,poses,names)
    demos={}
    for title,parts in [('Showcase',[('Idle',2),('Walk',4),('Run',3),('Walk',3),('Idle',2)]),('Locomotion',[('Idle',2),('Walk',3),('Run',3),('Walk',2),('Idle',2)])]:
        poses=[];previous=None
        for label,duration in parts:
            for frame in range(duration*30):
                array=cache[label];x=frame%max(1,len(array)-1);pose=array[x]
                if previous is not None and frame<15:pose=blend_pose(previous,pose,smooth(frame/15))
                poses.append(pose)
            previous=poses[-1]
        demos[title]=bake(rig,title,poses,names)
    e.update(actions=actions,demos=demos,manual_repairs=True,motion_repairs={'method':report['method'],'source_clips':{'Walk':'Walk','Run':'Run'},'whole_paw_skin':True},locomotion_sequence=['Idle','Walk','Run','Walk','Idle'],locomotion_durations_s=[2,3,3,2,2])
    report['candidates'].append({'candidate':number,'rigid_paws':[{'bone':names[l['foot']],'vertices':l['rigid_vertices'].tolist()} for l in legs],'bind_geometry_unchanged':True,'max_ik_target_error_m':max(targets)})
    body.hide_set(number!=1);body.hide_render=number!=1;rig.hide_set(True)
scene['deer_four_rigs']=json.dumps(entries);scene['fauna_animal']=entries[0]['animal'];scene.frame_set(0)
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(out))
report['output_sha256']=hashlib.sha256(out.read_bytes()).hexdigest();(root/'rodent-motion-revision.json').write_text(json.dumps(report,indent=2)+'\n')
print('Actual CC0 Walk/Run revision built',a.slug,flush=True)
