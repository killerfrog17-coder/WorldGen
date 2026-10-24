"""Raw four-model audition using each mesh's independently generated SkinTokens rig.

Automatic graph/geometry correspondence and existing authored motion only.
No donor skeleton/weights substitution, painting, IK or manual pose repair.
"""
import argparse,json,sys,time
from pathlib import Path
import bpy
import numpy as np
from mathutils import Matrix,Vector,Quaternion
from mathutils.kdtree import KDTree
sys.path.insert(0,str(Path(__file__).parent))
from deer_behaviour_core import blend_pose,smooth

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
ap.add_argument('--motion-root',type=Path,required=True);ap.add_argument('--fur',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve();started=time.perf_counter()
out=root/'Deer - Four Generated Rigs.blend'
if out.exists():raise FileExistsError(out)
source=json.loads((a.motion_root/'source-rig.json').read_text());motions=np.load(a.motion_root/'source-motions.npz')
sn=[b['name'] for b in source['bones']];si={n:i for i,n in enumerate(sn)}
idle=motions[next(c['key'] for c in source['clips'] if c['label']=='Idle')][0]
reference=idle[:,:3,3];reference_q=[Matrix(m).to_quaternion() for m in idle]
bpy.ops.wm.read_factory_settings(use_empty=True);s=bpy.context.scene
fur=bpy.data.images.load(str(a.fur.resolve()));fur.pack();entries=[]
def canonical(data):
    p=data['source_vertices'].copy();low=p[:,2].min();scale=1.65/(p[:,2].max()-low)
    p[:,2]=(p[:,2]-low)*scale;p[:,:2]*=scale
    torso=p[(p[:,2]>.7)&(p[:,2]<1.2)];head=p[p[:,2]>1.35]
    _,axes=np.linalg.eigh(np.cov(torso[:,:2].T));f=axes[:,-1]
    if (head[:,:2].mean(0)-torso[:,:2].mean(0))@f<0:f=-f
    r=np.array([f[1],-f[0]]);rotation=np.stack([r,f],axis=1)
    p[:,:2]=p[:,:2]@rotation;torso=p[(p[:,2]>.7)&(p[:,2]<1.2)]
    offset=np.array([(np.quantile(torso[:,i],.02)+np.quantile(torso[:,i],.98))*.5 for i in (0,1)])
    p[:,:2]-=offset
    def transform(points):
        q=points.copy()*scale;q[:,2]-=low*scale;q[:,:2]=q[:,:2]@rotation-offset;return q
    return p,transform(data['joints']),transform(data['sample_points']),scale
def path_to(parents,node):
    result=[]
    while node>=0:result.append(int(node));node=int(parents[node])
    return list(reversed(result))
def correspondence(joints,parents):
    count=len(parents);children=[np.flatnonzero(parents==i).tolist() for i in range(count)]
    roots=np.flatnonzero(parents<0);assert len(roots)==1;pelvis=int(roots[0])
    leaves=[i for i in range(count) if not children[i]]
    feet=sorted(leaves,key=lambda i:joints[i,2])[:4]
    if len(feet)!=4:raise ValueError('Generated graph does not have four limb ends')
    legs=[]
    for foot in feet:
        chain=[foot];parent=int(parents[foot])
        while parent>=0 and len(children[parent])==1:chain.append(parent);parent=int(parents[parent])
        legs.append([parent]+list(reversed(chain)))
    legs.sort(key=lambda chain:joints[chain[-1],1]);hind=legs[:2];fore=legs[2:]
    common=set(path_to(parents,fore[0][-1]))&set(path_to(parents,fore[1][-1]))
    chest=max(common,key=lambda i:len(path_to(parents,i)))
    limb_nodes={n for leg in legs for n in leg[1:]}
    head_candidates=[i for i in range(count) if len(children[i])>=2 and i not in limb_nodes and joints[i,2]>.9]
    if not head_candidates:raise ValueError('Generated graph has no recognizable head junction')
    head=max(head_candidates,key=lambda i:joints[i,1])
    bindings=np.zeros((count,len(sn)));assigned=set();group=['extra']*count
    def bind_path(target,names,kind):
        ids=[si[n] for n in names]
        if len(target)==len(ids):rows=np.eye(len(sn))[ids]
        else:
            lengths=np.r_[0,np.cumsum(np.linalg.norm(np.diff(reference[ids],axis=0),axis=1))];lengths/=max(lengths[-1],1e-8)
            t=np.r_[0,np.cumsum(np.linalg.norm(np.diff(joints[target],axis=0),axis=1))];t/=max(t[-1],1e-8)
            rows=[]
            for x in t:
                k=min(max(int(np.searchsorted(lengths,x,side='right')-1),0),len(ids)-2)
                w=np.clip((x-lengths[k])/max(lengths[k+1]-lengths[k],1e-8),0,1)
                row=np.zeros(len(sn));row[ids[k]]=1-w;row[ids[k+1]]+=w;rows.append(row)
        for node,row in zip(target,rows):
            if node not in assigned:bindings[node]=row;group[node]=kind;assigned.add(node)
    bind_path(path_to(parents,chest),['Pelvis','Spine1','Spine2','Chest'],'spine')
    neck=path_to(parents,head);neck=neck[neck.index(chest):]
    bind_path(neck,['Chest','Neck1','Neck2','Neck3','Head'],'neck')
    for legset,source_prefix,kind in [(fore,'Shoulder','fore'),(hind,'Thigh','hind')]:
        sides=sorted(('L','R'),key=lambda side:reference[si[source_prefix+'_'+side],0])
        for chain,side in zip(sorted(legset,key=lambda c:joints[c[-1],0]),sides):
            if kind=='fore':names=['Chest',*[(n+'_'+side) for n in ('Shoulder','UpperFore','Elbow','Wrist','ForeHoof')]]
            else:names=['Pelvis',*[(n+'_'+side) for n in ('Thigh','Knee','Hock','HindHoof')]]
            if kind=='fore' and len(chain)==5:names.remove('Shoulder_'+side)
            bind_path(chain,names,kind)
    for child in children[pelvis]:
        if child in assigned:continue
        chain=[pelvis,child]
        while len(children[chain[-1]])==1:chain.append(children[chain[-1]][0])
        if joints[chain[-1],2]>.45:bind_path(chain,['Pelvis','Tail1','Tail2'],'tail')
    for i in range(count):
        if i not in assigned:bindings[i]=bindings[int(parents[i])] if parents[i]>=0 else np.eye(len(sn))[si['Pelvis']]
    main=[]
    for i in range(count):
        preferred=[c for c in children[i] if group[c] in ('spine','neck')]
        main.append(preferred[0] if preferred else (children[i][0] if children[i] else -1))
    names=[f'Joint_{i:02d}' for i in range(count)];names[pelvis]='Pelvis';names[head]='Head'
    return bindings,children,group,main,names,pelvis,head,legs
def orient(direction,hint):
    y=Vector(direction).normalized();z=Vector(hint)-y*Vector(hint).dot(y)
    if z.length<1e-5:z=Vector((1,0,0))-y*y.x
    z.normalize();return Matrix((y.cross(z).normalized(),y,z)).transposed().to_quaternion()
def bake(rig,label,poses,names):
    action=bpy.data.actions.new(rig.name+' | '+label);action.use_fake_user=True
    slot=action.slots.new(id_type='OBJECT',name=rig.name);bag=action.layers.new('Authored transfer').strips.new(type='KEYFRAME').channelbag(slot,ensure=True)
    loc=np.array([p[0] for p in poses]);rot=np.array([p[1] for p in poses]);times=np.arange(len(poses),dtype=np.float32)
    for k in range(1,len(rot)):rot[k,np.sum(rot[k]*rot[k-1],axis=1)<0]*=-1
    for i,name in enumerate(names):
        rig.pose.bones[name].rotation_mode='QUATERNION'
        for path,values in [('location',loc[:,i]),('rotation_quaternion',rot[:,i])]:
            for axis in range(values.shape[1]):
                curve=bag.fcurves.new(data_path=f'pose.bones["{name}"].{path}',index=axis);curve.keyframe_points.add(len(poses))
                curve.keyframe_points.foreach_set('co',np.c_[times,values[:,axis]].astype(np.float32).ravel())
                for key in curve.keyframe_points:key.interpolation='LINEAR'
                curve.update()
    return action.name
for number in range(1,5):
    folder=root/f'candidate-{number}';data=np.load(folder/'generated-rig.npz');p,joints,points,scale=canonical(data)
    parents=data['parents'].astype(int);binding,children,group,main,names,pelvis,head,legs=correspondence(joints,parents)
    mesh=bpy.data.meshes.new(f'Generated deer {number}');mesh.from_pydata(p.tolist(),[],data['source_faces'].tolist());mesh.update()
    body=bpy.data.objects.new(f'Deer {number}',mesh);s.collection.objects.link(body);bpy.context.view_layer.objects.active=body;body.select_set(True)
    dec=body.modifiers.new('Automatic 6000 triangle budget','DECIMATE');dec.ratio=min(1,6000/len(mesh.polygons));dec.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=dec.name)
    tri=body.modifiers.new('Triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
    for f in body.data.polygons:f.use_smooth=True
    arm=bpy.data.armatures.new(f'Generated SkinTokens rig {number}');rig=bpy.data.objects.new(f'Deer rig {number}',arm);s.collection.objects.link(rig)
    body.select_set(False);rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
    for i,name in enumerate(names):
        bone=arm.edit_bones.new(name);bone.head=joints[i]
        if main[i]>=0:direction=Vector(joints[main[i]]-joints[i])
        elif parents[i]>=0:direction=Vector(joints[i]-joints[parents[i]])*.3
        else:direction=Vector((0,0,.05))
        if i==head:direction=Vector((0,.10,0))
        if direction.length<.01:direction=Vector((0,0,.04))
        bone.tail=bone.head+direction;bone.align_roll(Vector((0,1,0)) if group[i] in ('fore','hind') else Vector((0,0,1)))
        if parents[i]>=0:bone.parent=arm.edit_bones[names[int(parents[i])]]
    bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True;arm.display_type='OCTAHEDRAL'
    tree=KDTree(len(points))
    for index,point in enumerate(points):tree.insert(Vector(point),index)
    tree.balance();weights=data['sample_skin'];groups=[body.vertex_groups.new(name=n) for n in names]
    for vertex in body.data.vertices:
        nearest=tree.find_n(vertex.co,4);ids=[x[1] for x in nearest];d=np.array([x[2] for x in nearest]);blend=1/np.maximum(d,.0001)**2
        row=np.maximum(0,(weights[ids]*blend[:,None]).sum(0)/blend.sum());best=np.argsort(row)[-4:];values=row[best]/max(row[best].sum(),1e-12)
        for index,value in zip(best,values):groups[index].add([vertex.index],float(value),'REPLACE')
    modifier=body.modifiers.new('Fresh SkinTokens skin - linear','ARMATURE');modifier.object=rig;modifier.use_deform_preserve_volume=False;body.parent=rig
    material=bpy.data.materials.new(f'Existing fur {number}');material.use_nodes=True;nodes=material.node_tree.nodes;links=material.node_tree.links
    tex=nodes.new('ShaderNodeTexImage');tex.image=fur;tex.projection='BOX';tex.projection_blend=.25
    coord=nodes.new('ShaderNodeTexCoord');mul=nodes.new('ShaderNodeVectorMath');mul.operation='SCALE';mul.inputs[3].default_value=8
    hsv=nodes.new('ShaderNodeHueSaturation');hsv.name='Review coat variation';bsdf=nodes.get('Principled BSDF');bsdf.inputs['Roughness'].default_value=.85
    links.new(coord.outputs['Generated'],mul.inputs[0]);links.new(mul.outputs[0],tex.inputs['Vector']);links.new(tex.outputs['Color'],hsv.inputs['Color']);links.new(hsv.outputs[0],bsdf.inputs['Base Color']);body.data.materials.append(material)
    rest=[arm.bones[n].matrix_local.copy() for n in names];inverse=[m.inverted() for m in rest]
    local=[inverse[int(parent)]@m if parent>=0 else m for parent,m in zip(parents,rest)]
    refpoints=binding@reference;refdirs=[]
    for i in range(len(names)):
        child=main[i];direction=refpoints[child]-refpoints[i] if child>=0 else np.zeros(3)
        refdirs.append(orient(direction,(0,1,0) if group[i] in ('fore','hind') else (0,0,1)) if np.linalg.norm(direction)>1e-5 else None)
    cache={};actions={}
    for clip in source['clips']:
        label=clip['label'];frames=[]
        for source_frame in motions[clip['key']]:
            virtual=binding@source_frame[:,:3,3];g=[]
            for i,parent in enumerate(parents):
                if group[i]=='extra' and parent>=0:q=g[int(parent)].to_quaternion()@local[i].to_quaternion()
                elif i==pelvis or i==head or main[i]<0:
                    index=int(np.argmax(binding[i]));q=Matrix(source_frame[index]).to_quaternion()@reference_q[index].inverted()@rest[i].to_quaternion()
                elif refdirs[i] is not None:
                    direction=virtual[main[i]]-virtual[i]
                    q=orient(direction,(0,1,0) if group[i] in ('fore','hind') else (0,0,1))@refdirs[i].inverted()@rest[i].to_quaternion()
                else:q=rest[i].to_quaternion()
                location=rest[i].translation.copy() if parent<0 else g[int(parent)]@local[i].translation
                if i==pelvis:location+=Vector(source_frame[si['Pelvis'],:3,3]-reference[si['Pelvis']])
                m=q.to_matrix().to_4x4();m.translation=location;g.append(m)
            loc=[];rot=[]
            for i,parent in enumerate(parents):
                relative=g[int(parent)].inverted()@g[i] if parent>=0 else g[i];basis=local[i].inverted()@relative
                loc.append(basis.translation[:]);rot.append(basis.to_quaternion()[:])
            frames.append((np.array(loc),np.array(rot)))
        cache[label]=frames;actions[label]=bake(rig,label,frames,names)
    def sample(label,seconds):
        array=cache[label];x=seconds*30
        if label=='lay_to_idle':x=min(x,len(array)-1)
        else:x%=max(1,len(array)-1)
        first=int(x);second=min(first+1,len(array)-1);return blend_pose(array[first],array[second],x-first)
    demos={}
    for title,parts in [('Showcase',[('Idle',2),('Walk',3),('Trot',2),('Run',3),('Walk',2),('Idle',2),('Eating',4),('Idle',2),('Sleep',3),('lay_to_idle',len(cache['lay_to_idle'])/30),('Idle',2)]),('Locomotion',[('Idle',2),('Walk',3),('Trot',2),('Run',3),('Trot',2),('Walk',3),('Idle',2)])]:
        poses=[];previous=None;last_duration=0
        for label,duration in parts:
            for f in range(round(duration*30)):
                t=f/30;pose=sample(label,t)
                if previous and t<.5:pose=blend_pose(sample(previous,last_duration+t),pose,smooth(t/.5))
                poses.append(pose)
            previous=label;last_duration=duration
        for f in range(15):poses.append(blend_pose(poses[-1],sample('Idle',0),smooth((f+1)/15)))
        demos[title]=bake(rig,title,poses,names)
    report={'number':number,'rig':rig.name,'body':body.name,'head':'Head','actions':actions,'demos':demos,'material':material.name,
        'joints':len(names),'triangles':len(body.data.polygons),'rig_file':str(folder/'generated-rig.npz'),'mesh_file':str(folder/'deer-raw.glb'),
        'parents':parents.tolist(),'joint_names':names,'generated_joint_heads':joints.tolist(),'binding':binding.tolist(),'groups':group,
        'manual_repairs':False,'skin':'Fresh network sample weights, 4-neighbour interpolation and top4 normalization','motion':'Existing authored source; automatic graph/geometry transfer, fixed learned joint offsets; no IK/contact repair'}
    (folder/'assembly.json').write_text(json.dumps(report,indent=2));entries.append(report)
    body.hide_set(number!=1);rig.hide_set(True);rig.select_set(False)
    print('Assembled candidate',number,'independent joints',len(names),flush=True)
s['deer_four_rigs']=json.dumps(entries);s.render.fps=30;s.sync_mode='FRAME_DROP'
world=bpy.data.worlds.new('Review world');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.2,.22,.25,1);s.world=world
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            space=area.spaces.active;space.shading.type='MATERIAL';space.shading.use_scene_world=False;space.overlay.show_overlays=False;space.show_region_ui=True
            space.region_3d.view_location=(0,0,.8);space.region_3d.view_distance=3.1;space.region_3d.view_rotation=Vector((1,0,.1)).to_track_quat('Z','Y')
bpy.data.orphans_purge(do_recursive=True);bpy.ops.wm.save_as_mainfile(filepath=str(out))
(root/'gallery.json').write_text(json.dumps({'candidates':entries,'assembly_seconds':round(time.perf_counter()-started,2),'new_skeleton_per_mesh':True,'manual_repairs':False,'art_approved':False},indent=2))
print('Four fresh-rig candidates saved.',flush=True)
