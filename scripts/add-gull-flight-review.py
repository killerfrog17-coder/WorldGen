"""Add own articulated wing geometry and CC0 bird flight to the four gulls.

Ground body sculpt is retained. Flight angles come from the existing CC0 bird
bank. Own folded/open wing fitting is a prototype, not takeoff/landing mocap.
"""
import hashlib,json,math,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Quaternion,Vector
repo=Path(__file__).resolve().parent.parent;sys.path.insert(0,str(repo/'scripts'))
from deer_behaviour_core import blend_pose,smooth
root=repo/'artifacts/fauna-overnight-20261024/gull';source=root/'Animal - Raw Audition.blend';out=root/'Animal - Motion Revision.blend'
bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;entries=json.loads(s['deer_four_rigs'])
bank=repo/'artifacts/fauna-motion-sources/bird';donor=json.loads((bank/'source-rig.json').read_text());motions=np.load(bank/'source-motions.npz');di={b['name']:i for i,b in enumerate(donor['bones'])}
proof={'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'body_geometry_retained':True,'wing_geometry':'Own low-poly tapered cambered wing panels with shaped primary tips, three articulated sections per side','flight_motion':'CC0 bird Flap and Glide; source angle tracks fitted to own wings','motion_bank':str(bank),'motion_rig_sha256':hashlib.sha256((bank/'source-rig.json').read_bytes()).hexdigest(),'motion_array_sha256':hashlib.sha256((bank/'source-motions.npz').read_bytes()).hexdigest(),'takeoff_landing_supplied':False,'candidates':[]}

def solve(seed,target):
    p=np.array(seed,dtype=float);anchor=p[0].copy();length=np.linalg.norm(np.diff(p,axis=0),axis=1)
    if np.linalg.norm(target-anchor)>length.sum()*.998:target=anchor+(target-anchor)/np.linalg.norm(target-anchor)*length.sum()*.998
    for _ in range(30):
        p[-1]=target
        for i in range(len(p)-2,-1,-1):
            d=p[i]-p[i+1];p[i]=p[i+1]+d/max(np.linalg.norm(d),1e-9)*length[i]
        p[0]=anchor
        for i,l in enumerate(length):
            d=p[i+1]-p[i];p[i+1]=p[i]+d/max(np.linalg.norm(d),1e-9)*l
    return p

def bake(rig,label,poses,names):
    action=bpy.data.actions.new(rig.name+' | Flight revision '+label);action.use_fake_user=True
    slot=action.slots.new(id_type='OBJECT',name=rig.name);bag=action.layers.new('CC0 bird motion with fitted wings').strips.new(type='KEYFRAME').channelbag(slot,ensure=True)
    loc=np.array([p[0] for p in poses]);rot=np.array([p[1] for p in poses])
    for k in range(1,len(rot)):rot[k,np.sum(rot[k]*rot[k-1],axis=1)<0]*=-1
    for i,n in enumerate(names):
        for path,values in [('location',loc[:,i]),('rotation_quaternion',rot[:,i])]:
            for axis in range(values.shape[1]):
                c=bag.fcurves.new(data_path=f'pose.bones["{n}"].{path}',index=axis);c.keyframe_points.add(len(poses));c.keyframe_points.foreach_set('co',np.c_[np.arange(len(poses))*.5,values[:,axis]].astype(np.float32).ravel())
                for key in c.keyframe_points:key.interpolation='LINEAR'
                c.update()
    curve=bag.fcurves.new(data_path='["WingFold"]');curve.keyframe_points.add(len(poses));curve.keyframe_points.foreach_set('co',np.c_[np.arange(len(poses))*.5,[p[2] for p in poses]].astype(np.float32).ravel())
    for key in curve.keyframe_points:key.interpolation='LINEAR'
    return action.name

for number,e in enumerate(entries,1):
    body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']];body.hide_set(False);rig.hide_set(False);rig.data.pose_position='POSE';rig.animation_data_create();oldnames=e['joint_names'];oldcount=len(oldnames)
    # Sample existing own manually repaired ground legs before replacing clips.
    old={}
    for label in ('Idle','Walk'):
        action=bpy.data.actions[e['actions'][label]];rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0];first,last=action.frame_range;frames=[]
        for k in range(round((last-first)*2)+1):
            frame=first+k*.5;s.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update();frames.append([rig.pose.bones[n].matrix.copy() for n in oldnames])
        old[label]=frames
    rig.animation_data_clear();rig.data.pose_position='REST';parents=list(e['parents']);j=list(e['generated_joint_heads']);names=list(oldnames);wingnodes={};centrex=j[0][0]
    # The complete low webbed feet, including peripheral pieces, follow their
    # own palm. A height cut through the membrane leaves stretched ribbons.
    for vertex in body.data.vertices:
        if vertex.co.z<.32:
            foot=6 if vertex.co.x<centrex else 11
            for group in body.vertex_groups:group.remove([vertex.index])
            body.vertex_groups[oldnames[foot]].add([vertex.index],1.,'REPLACE')
    body.shape_key_add(name='Basis');body.shape_key_add(name='Folded')
    bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
    for side in (-1,1):
        nodes=[];points=[(centrex+side*.32,.25,.98),(centrex+side*.93,.03,1.00),(centrex+side*1.48,-.19,.98),(centrex+side*2.25,-.59,.94)]
        for k in range(3):
            name=f'Wing {side} section {k}';b=rig.data.edit_bones.new(name);b.head=points[k];b.tail=points[k+1];b.parent=rig.data.edit_bones[names[nodes[-1] if nodes else 0]];b.align_roll(Vector((0,0,1)));idx=len(names);names.append(name);parents.append(nodes[-1] if nodes else 0);j.append(list(points[k]));nodes.append(idx)
        wingnodes[side]=nodes
    bpy.ops.object.mode_set(mode='OBJECT');rest=[rig.data.bones[n].matrix_local.copy() for n in names];local=[rest[p].inverted()@r if p>=0 else r for p,r in zip(parents,rest)]
    wings=[]
    for side,nodes in wingnodes.items():
        vertices=[];folded=[];faces=[];weights=[]
        # Thin closed wing surface. Distal trailing edge has separated tips.
        stations=31;cross=5
        for k,t in enumerate(np.linspace(0,1,stations)):
            x=.32+1.93*t;y=.35-1.00*t;chord=.70*(1-t)**.60+.012;z=.98-.04*t
            feather=.055*(math.sin(k*math.pi/3)**2) if t>.55 and k<stations-1 else 0
            section=min(2,int(t*3));fraction=min(1,t*3-section)
            for surface in (-1,1):
                for across in np.linspace(0,1,cross):
                    camber=.055*math.sin(math.pi*across)*(1-t)**.4;thickness=.016*(1-t)+.005
                    vertices.append((centrex+side*x,y-(chord+feather)*across,z+camber+surface*thickness))
                    folded.append((centrex+side*(.30+.12*math.sin(math.pi*t)+.10*across),.28-1.25*t,.88-.22*t+.04*math.sin(math.pi*across)+surface*thickness))
                    weights.append((section,fraction))
        for k in range(stations-1):
            a=k*cross*2;b=(k+1)*cross*2
            for surface in range(2):
                for c in range(cross-1):
                    i=a+surface*cross+c;jj=b+surface*cross+c;faces.append((i,jj,jj+1,i+1))
            faces.extend([(a,a+cross,b+cross,b),(a+cross-1,b+cross-1,b+2*cross-1,a+2*cross-1)])
        mesh=bpy.data.meshes.new(f'Own gull wing {number} {side}');mesh.from_pydata(vertices,[],faces);mesh.update();obj=bpy.data.objects.new(mesh.name,mesh);s.collection.objects.link(obj);obj.parent=rig;obj.data.materials.append(body.data.materials[0]);vgs=[obj.vertex_groups.new(name=n) for n in names]
        for vi,(section,fraction) in enumerate(weights):
            i=nodes[section];other=nodes[min(2,section+1)];vgs[i].add([vi],1-fraction if other!=i else 1,'REPLACE')
            if other!=i:vgs[other].add([vi],fraction,'REPLACE')
        obj.shape_key_add(name='Basis');key=obj.shape_key_add(name='Folded')
        for point,position in zip(key.data,folded):point.co=position
        mod=obj.modifiers.new('Own articulated wing','ARMATURE');mod.object=rig
        for polygon in mesh.polygons:polygon.use_smooth=True
        wings.append(obj)
    # Merge appendages into the review body so visibility/controls stay simple.
    bpy.ops.object.select_all(action='DESELECT');body.select_set(True)
    for obj in wings:obj.select_set(True)
    bpy.context.view_layer.objects.active=body;bpy.ops.object.join();rig.data.pose_position='POSE'
    rig['WingFold']=1.;driver=body.data.shape_keys.key_blocks['Folded'].driver_add('value').driver;driver.expression='fold';variable=driver.variables.new();variable.name='fold';variable.targets[0].id=rig;variable.targets[0].data_path='["WingFold"]'
    cache={};actions={}
    for label,seconds in [('Idle',2),('Walk',2),('Flap',1.2),('Glide',2)]:
        poses=[];clip=next(c for c in donor['clips'] if c['label']==label);src=motions[clip['key']]
        for frame in range(round(seconds*60)+1):
            phase=frame/(seconds*60);sourcephase=(frame/60)%1 if label=='Walk' else phase;index=sourcephase*(len(src)-1);lo=min(int(index),len(src)-2);fraction=index-lo;xyz=src[lo,:,:3,3]*(1-fraction)+src[lo+1,:,:3,3]*fraction;flight=label in ('Flap','Glide')
            if flight:
                g=[r.copy() for r in rest[:oldcount]]
                for m in g:m.translation.z+=.65+float(xyz[di[donor['root']],2]-src[:,di[donor['root']],2,3].mean())*.25
                head=names.index(e['head']);offset=Vector(np.array(j[head])-np.array(j[0]));m=g[head];m.translation=g[0].translation+Quaternion((1,0,0),-.30)@offset
                for node in range(3,oldcount):
                    if e['groups'][node]=='hind':
                        hip=3 if node<=7 else 8;rotation=Quaternion((1,0,0),-1.15);q=rotation@rest[node].to_quaternion();position=g[0].translation+Vector(np.array(j[hip])-np.array(j[0]))+rotation@Vector(np.array(j[node])-np.array(j[hip]));g[node]=q.to_matrix().to_4x4();g[node].translation=position
            else:
                g=[m.copy() for m in old[label][frame%(len(old[label])-1)]]
                if label=='Walk':
                    bob=.025*math.sin(phase*math.tau*2);g[0].translation.z+=bob
                    for node in (1,2):g[node].translation.z+=bob
                    for side,chain in [('L',[3,4,5,6]),('R',[8,9,10,11])]:
                        ids=[di[n] for n in donor['paths']['hind_'+side]];foot=ids[4];trace=src[:,foot,:3,3];scale=.65
                        target=np.array(j[chain[-1]],dtype=float);target[1]+=(xyz[foot,1]-trace[:,1].mean())*scale;target[2]=j[chain[-1]][2]+.005+max(0,float(xyz[foot,2]-trace[:,2].min()))*scale
                        anchor=g[0]@local[chain[0]].translation;seed=np.array(j)[chain]+np.array(anchor)-np.array(j[chain[0]]);points=solve(seed,target)
                        for k,node in enumerate(chain[:-1]):
                            q=Vector(np.array(j[chain[k+1]])-np.array(j[node])).rotation_difference(Vector(points[k+1]-points[k]))@rest[node].to_quaternion();g[node]=q.to_matrix().to_4x4();g[node].translation=Vector(points[k])
                        g[chain[-1]]=rest[chain[-1]].copy();g[chain[-1]].translation=Vector(points[-1]);g[chain[-1]+1]=g[chain[-1]]@local[chain[-1]+1]
            for side,nodes in wingnodes.items():
                ids=[di[n] for n in donor['paths']['fore_'+('L' if side<0 else 'R')]]
                for k,node in enumerate(nodes):
                    position=g[parents[node]]@local[node].translation
                    if flight:
                        start=ids[min(k+1,len(ids)-2)];end=ids[min(k+2,len(ids)-1)];vector=xyz[end]-xyz[start];angle=float(np.clip(math.atan2(vector[2],max(abs(vector[0]),1e-5)),-.85,1.20));q=Quaternion((0,1,0),-side*angle)@rest[node].to_quaternion()
                    else:
                        q=rest[node].to_quaternion()
                    m=q.to_matrix().to_4x4();m.translation=position;g.append(m)
            loc=[];rot=[]
            for i,parent in enumerate(parents):
                relative=g[parent].inverted()@g[i] if parent>=0 else g[i];basis=local[i].inverted()@relative;loc.append(basis.translation[:]);rot.append(basis.to_quaternion()[:])
            poses.append((np.array(loc),np.array(rot),0. if flight else 1.))
        cache[label]=poses;actions[label]=bake(rig,label,poses,names)
    demos={}
    for title,parts in [('Showcase',[('Idle',2),('Walk',4),('Flap',4),('Glide',3),('Idle',2)]),('Locomotion',[('Idle',2),('Walk',5),('Idle',2)])]:
        poses=[];previous=None
        for label,seconds in parts:
            for k in range(seconds*60):
                value=cache[label][k%(len(cache[label])-1)]
                if previous and k<30:
                    w=smooth(k/30);value=(*blend_pose(previous[:2],value[:2],w),previous[2]*(1-w)+value[2]*w)
                poses.append(value)
            previous=poses[-1]
        demos[title]=bake(rig,title,poses,names)
    e.update(joint_names=names,parents=parents,generated_joint_heads=j,joints=len(names),triangles=sum(len(p.vertices)-2 for p in body.data.polygons),groups=e['groups']+['wing']*6,actions=actions,demos=demos,manual_repairs=True,rig_origin='manual anatomical rig',motion_repairs={'method':proof['flight_motion']},source_family='CC0 bird with own wing geometry',source_license='CC0-1.0',locomotion_sequence=['Idle','Walk','Idle'],locomotion_durations_s=[2,5,2])
    binding=np.array(e['binding']);assert binding.shape[1]==len(di)
    extra=[]
    for side,nodes in wingnodes.items():
        for k,node in enumerate(nodes):
            row=np.zeros(len(di));row[di[donor['paths']['fore_'+('L' if side<0 else 'R')][k+1]]]=1;extra.append(row.tolist())
    e['binding']=binding.tolist()+extra
    proof['candidates'].append({'candidate':number,'triangles':e['triangles'],'joints':len(names)});body.hide_set(number!=1);body.hide_render=number!=1;rig.hide_set(True)
s['deer_four_rigs']=json.dumps(entries);s.frame_set(0);bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(out));proof['output_sha256']=hashlib.sha256(out.read_bytes()).hexdigest();(root/'gull-flight-revision.json').write_text(json.dumps(proof,indent=2)+'\n');print('Gull ground and flight review built',flush=True)
