"""Four own generated meshes/fresh rigs with suitable licensed source motion.

Automatic graph correspondence, fixed learned joint offsets and network skin.
No donor geometry, anatomical painting, invented mammal gait or manual repair.
"""
import argparse,json,sys,time
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix,Vector
from mathutils.kdtree import KDTree
sys.path.insert(0,str(Path(__file__).parent))
from deer_behaviour_core import blend_pose,smooth

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True);ap.add_argument('--motion-root',type=Path,required=True)
ap.add_argument('--animal',required=True);ap.add_argument('--fur',type=Path,required=True)
ap.add_argument('--neutral-material',action='store_true')
ap.add_argument('--rebuild-unreviewed',action='store_true')
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve();out=root/'Animal - Raw Audition.blend';started=time.perf_counter()
rebuild=out.exists()
if rebuild:
    assert a.rebuild_unreviewed and not json.loads((root/'audition.json').read_text()).get('art_approved')
    assert root.is_relative_to(Path(__file__).resolve().parent.parent/'artifacts/fauna-overnight-20261024')
source=json.loads((a.motion_root/'source-rig.json').read_text());motions=np.load(a.motion_root/'source-motions.npz')
if source['family'] in ('bird','fish','snake','spider','ground_insect','turtle','crocodile') or a.animal in {'Elephant','Rhinoceros','Hippopotamus','Pangolin','Armadillo'}:a.neutral_material=True
sn=[b['name'] for b in source['bones']];si={n:i for i,n in enumerate(sn)};paths=source['paths']
flightless=a.animal in {'Ostrich','Emu','Cassowary','Kiwi','Penguin'}
clips=[c for c in source['clips'] if not (source['family']=='bird' and ((flightless and c['label'] in {'Flap','Glide'}) or ('bat' in a.animal.lower() and c['label']=='Walk')))]
idle=motions[next(c['key'] for c in source['clips'] if c['label']=='Idle')][0]
reference=idle[:,:3,3];refq=[Matrix(m).to_quaternion() for m in idle]
bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;entries=[]
fur=bpy.data.images.load(str(a.fur.resolve()));fur.pack()

def path_to(parents,node):
    result=[]
    while node>=0:result.append(int(node));node=int(parents[node])
    return list(reversed(result))

def correspondence(joints,parents):
    children=[np.flatnonzero(parents==i).tolist() for i in range(len(parents))]
    roots=np.flatnonzero(parents<0);assert len(roots)==1;pelvis=int(roots[0])
    leaves=[i for i,c in enumerate(children) if not c];bird=source['family']=='bird';fish=source['family']=='fish'
    if fish:return fish_correspondence(joints,parents,children,leaves,pelvis)
    if source['family'] in ('snake','spider','ground_insect'):return chain_correspondence(joints,parents,children,leaves,pelvis)
    feet=([min([i for i in leaves if joints[i,0]*side>0],key=lambda i:joints[i,2]) for side in (-1,1)] if bird else sorted(leaves,key=lambda i:joints[i,2])[:4]);legs=[]
    for foot in feet:
        chain=[foot];parent=int(parents[foot])
        while parent>=0 and len(children[parent])==1:chain.append(parent);parent=int(parents[parent])
        legs.append([parent]+list(reversed(chain)))
    if bird:
        hind=legs;fore=[]
        for foot in ([] if flightless else [min(leaves,key=lambda i:joints[i,0]),max(leaves,key=lambda i:joints[i,0])]):
            chain=[foot];parent=int(parents[foot])
            while parent>=0 and len(children[parent])==1:chain.append(parent);parent=int(parents[parent])
            fore.append([parent]+list(reversed(chain)))
    else:legs.sort(key=lambda chain:joints[chain[-1],1]);hind=legs[:2];fore=legs[2:]
    common=(set(path_to(parents,fore[0][-1]))&set(path_to(parents,fore[1][-1]))) if fore else {pelvis};chest=max(common,key=lambda i:len(path_to(parents,i)))
    limb_nodes={n for leg in [*fore,*hind] for n in leg[1:]}
    if bird:
        halfwidth=max(abs(joints[:,0]))
        candidates=[i for i in leaves if i not in limb_nodes and abs(joints[i,0])<halfwidth*.4 and joints[i,2]>.4]
        if not candidates:candidates=[i for i in range(len(parents)) if i not in limb_nodes and joints[i,2]>.4]
    else:candidates=[i for i,c in enumerate(children) if len(c)>=2 and i not in limb_nodes and joints[i,2]>.40]
    head=max(candidates,key=lambda i:joints[i,1]);binding=np.zeros((len(parents),len(sn)));assigned=set();groups=['extra']*len(parents)
    def bind(target,names,kind):
        ids=[si[n] for n in names]
        if len(target)==len(ids):rows=np.eye(len(sn))[ids]
        else:
            lens=np.r_[0,np.cumsum(np.linalg.norm(np.diff(reference[ids],axis=0),axis=1))];lens/=max(lens[-1],1e-8)
            tt=np.r_[0,np.cumsum(np.linalg.norm(np.diff(joints[target],axis=0),axis=1))];tt/=max(tt[-1],1e-8);rows=[]
            for x in tt:
                k=min(max(int(np.searchsorted(lens,x,side='right')-1),0),len(ids)-2);w=np.clip((x-lens[k])/max(lens[k+1]-lens[k],1e-8),0,1)
                row=np.zeros(len(sn));row[ids[k]]=1-w;row[ids[k+1]]+=w;rows.append(row)
        for node,row in zip(target,rows):
            if node not in assigned:binding[node]=row;groups[node]=kind;assigned.add(node)
    bind(path_to(parents,chest),paths['spine'],'spine')
    neck=path_to(parents,head)
    # Learned hierarchies can put the forelimb attachment below a different
    # trunk branch. Their shared ancestor still connects the head to the body;
    # do not require a particular donor-style chest hierarchy.
    connection=max(set(path_to(parents,chest))&set(neck),key=lambda i:len(path_to(parents,i)))
    bind(neck[neck.index(connection):],paths['neck'],'neck')
    for legset,kind in [(fore,'fore'),(hind,'hind')]:
        sides=sorted(('L','R'),key=lambda side:reference[si[paths[kind+'_'+side][-1]],0])
        for chain,side in zip(sorted(legset,key=lambda c:joints[c[-1],0]),sides):bind(chain,paths[kind+'_'+side],'wing' if bird and kind=='fore' else kind)
    for child in children[pelvis]:
        if child in assigned:continue
        chain=[pelvis,child]
        while len(children[chain[-1]])==1:chain.append(children[chain[-1]][0])
        if 'tail' in paths and joints[chain[-1],2]>.2:bind(chain,paths['tail'],'tail')
    ears=[c for c in children[head] if c not in assigned and joints[c,2]>=joints[head,2]-.05]
    if len(ears)>=2 and 'Ear_L' in si:
        source_ears=sorted(['Ear_L','Ear_R'],key=lambda n:reference[si[n],0])
        for child,name in zip(sorted(ears,key=lambda i:joints[i,0]),source_ears):
            chain=[head,child]
            while len(children[chain[-1]])==1:chain.append(children[chain[-1]][0])
            bind(chain,[source['head'],name],'ear')
    for i in range(len(parents)):
        if i not in assigned:binding[i]=binding[int(parents[i])] if parents[i]>=0 else np.eye(len(sn))[si[source['root']]]
    main=[]
    for i in range(len(parents)):
        pref=[c for c in children[i] if groups[c] in ('spine','neck')];main.append(pref[0] if pref else children[i][0] if children[i] else -1)
    names=[f'Joint_{i:02d}' for i in range(len(parents))];names[pelvis]='Pelvis';names[head]='Head'
    return binding,children,groups,main,names,pelvis,head

def fish_correspondence(joints,parents,children,leaves,pelvis):
    binding=np.zeros((len(parents),len(sn)));assigned=set();groups=['extra']*len(parents)
    central=[i for i in leaves if abs(joints[i,0]-joints[pelvis,0])<max(.12,np.ptp(joints[:,0])*.25)] or leaves
    nose=max(central,key=lambda i:joints[i,1]);head=int(parents[nose]) if parents[nose]>=0 and parents[nose]!=pelvis else nose
    tail=min(central,key=lambda i:joints[i,1])
    def bind(chain,src,kind):
        ids=[si[n] for n in src];ss=np.r_[0,np.cumsum(np.linalg.norm(np.diff(reference[ids],axis=0),axis=1))];ss/=max(ss[-1],1e-8)
        tt=np.r_[0,np.cumsum(np.linalg.norm(np.diff(joints[chain],axis=0),axis=1))];tt/=max(tt[-1],1e-8)
        for node,t in zip(chain,tt):
            if node in assigned:continue
            k=min(max(int(np.searchsorted(ss,t,side='right')-1),0),len(ids)-2);w=np.clip((t-ss[k])/max(ss[k+1]-ss[k],1e-8),0,1)
            binding[node,ids[k]]=1-w;binding[node,ids[k+1]]+=w;groups[node]=kind;assigned.add(node)
    bind(path_to(parents,head),paths['spine'],'spine');bind(path_to(parents,tail),paths['tail'],'tail')
    for side,suffix in [(-1,'L'),(1,'R')]:
        lateral=[i for i in leaves if i not in assigned and (joints[i,0]-joints[pelvis,0])*side>.05]
        lateral=sorted(lateral,key=lambda i:joints[i,1],reverse=True)
        for node,label in zip(lateral[:2],['front','back']):bind(path_to(parents,node),paths['fin_'+label+'_'+suffix],'fin')
    for i in range(len(parents)):
        if i not in assigned:binding[i]=binding[int(parents[i])] if parents[i]>=0 else np.eye(len(sn))[si[source['root']]]
    main=[]
    for i in range(len(parents)):
        pref=[c for c in children[i] if groups[c]==groups[i] or groups[c]=='spine'];main.append(pref[0] if pref else children[i][0] if children[i] else -1)
    names=[f'Joint_{i:02d}' for i in range(len(parents))];names[pelvis]='Pelvis';names[head]='Head'
    return binding,children,groups,main,names,pelvis,head


def chain_correspondence(joints,parents,children,leaves,pelvis):
    """Arc-length mapping for a serpentine chain or eight explicit leg paths.

    A four-legged prediction is not silently presented as a spider skeleton.
    """
    binding=np.zeros((len(parents),len(sn)));assigned=set();groups=['extra']*len(parents)
    def bind(chain,src,kind):
        ids=[si[n] for n in src];ss=np.r_[0,np.cumsum(np.linalg.norm(np.diff(reference[ids],axis=0),axis=1))];ss/=max(ss[-1],1e-8)
        tt=np.r_[0,np.cumsum(np.linalg.norm(np.diff(joints[chain],axis=0),axis=1))];tt/=max(tt[-1],1e-8)
        for node,t in zip(chain,tt):
            if node in assigned:continue
            k=min(max(int(np.searchsorted(ss,t,side='right')-1),0),len(ids)-2);w=np.clip((t-ss[k])/max(ss[k+1]-ss[k],1e-8),0,1)
            binding[node,ids[k]]=1-w;binding[node,ids[k+1]]+=w;groups[node]=kind;assigned.add(node)
    centre_width=max(.12,np.ptp(joints[:,0])*.18)
    central_endpoints=[i for i in leaves if abs(joints[i,0]-joints[pelvis,0])<centre_width] or leaves
    head=max(central_endpoints,key=lambda i:joints[i,1]);tail=min(central_endpoints,key=lambda i:joints[i,1])
    if source['family'] in ('spider','ground_insect'):
        lateral=[i for i in leaves if i not in (head,tail) and abs(joints[i,0]-joints[pelvis,0])>.10]
        sides={side:[i for i in lateral if (joints[i,0]-joints[pelvis,0])*sign>0] for side,sign in [('L',-1),('R',1)]}
        leg_pairs=4 if source['family']=='spider' else 3
        if any(len(v)<leg_pairs for v in sides.values()):raise ValueError(f'{leg_pairs*2}-legged rig unavailable: fewer than {leg_pairs} distinct lateral endpoints per side')
        limb_nodes={n for v in sides.values() for i in v for n in path_to(parents,i)[-2:]}
        central=[i for i in leaves if i not in limb_nodes and abs(joints[i,0]-joints[pelvis,0])<.25]
        if central:head=max(central,key=lambda i:joints[i,1]);tail=min(central,key=lambda i:joints[i,1])
        bind(path_to(parents,head),paths['spine']+paths['neck'][1:],'spine')
        for side,endpoints in sides.items():
            for row,node in zip('abcd'[:leg_pairs],sorted(endpoints,key=lambda i:joints[i,1],reverse=True)[:leg_pairs]):bind(path_to(parents,node),paths['leg_'+row+'_'+side],'fore')
    else:bind(path_to(parents,head),paths['spine'],'spine')
    bind(path_to(parents,tail),paths['tail'],'tail')
    for i in range(len(parents)):
        if i not in assigned:binding[i]=binding[int(parents[i])] if parents[i]>=0 else np.eye(len(sn))[si[source['root']]]
    main=[]
    for i in range(len(parents)):
        pref=[c for c in children[i] if groups[c]==groups[i]];main.append(pref[0] if pref else children[i][0] if children[i] else -1)
    names=[f'Joint_{i:02d}' for i in range(len(parents))];names[pelvis]='Pelvis';names[head]='Head'
    if head==pelvis:names[head]='Head'
    return binding,children,groups,main,names,pelvis,head

def orient(direction,hint):
    y=Vector(direction).normalized();z=Vector(hint)-y*Vector(hint).dot(y)
    if z.length<1e-5:z=Vector((1,0,0))-y*y.x
    z.normalize();return Matrix((y.cross(z).normalized(),y,z)).transposed().to_quaternion()

def bake(rig,label,poses,names):
    action=bpy.data.actions.new(rig.name+' | '+label);action.use_fake_user=True
    slot=action.slots.new(id_type='OBJECT',name=rig.name);bag=action.layers.new('Licensed motion transfer').strips.new(type='KEYFRAME').channelbag(slot,ensure=True)
    loc=np.array([p[0] for p in poses]);rot=np.array([p[1] for p in poses]);times=np.arange(len(poses),dtype=np.float32)
    for k in range(1,len(rot)):rot[k,np.sum(rot[k]*rot[k-1],axis=1)<0]*=-1
    for i,name in enumerate(names):
        rig.pose.bones[name].rotation_mode='QUATERNION'
        for path,values in [('location',loc[:,i]),('rotation_quaternion',rot[:,i])]:
            for axis in range(values.shape[1]):
                vals=values[:,axis];keep=np.array([0,len(vals)-1]) if np.ptp(vals)<1e-6 else np.arange(len(vals))
                curve=bag.fcurves.new(data_path=f'pose.bones["{name}"].{path}',index=axis);curve.keyframe_points.add(len(keep))
                curve.keyframe_points.foreach_set('co',np.c_[times[keep],vals[keep]].astype(np.float32).ravel())
                for key in curve.keyframe_points:key.interpolation='LINEAR'
                curve.update()
    return action.name

for number in range(1,5):
    folder=root/f'candidate-{number}';raw=np.load(folder/'generated-rig.npz');data={k:raw[k].copy() for k in raw.files}
    preparation=json.loads((folder/'mesh-preparation.json').read_text());axis_correction=None
    if source['family']=='bird' and not flightless and preparation.get('family_axis_method')!='minor axis for spread wings':
        p=data['source_vertices'];upper=p[p[:,2]>.78*1.65];torso=p[(p[:,2]>.25*1.65)&(p[:,2]<.65*1.65)]
        sign=1 if upper[:,0].mean()>=torso[:,0].mean() else -1
        C=np.array([[0,-sign,0],[sign,0,0],[0,0,1]])
        for k in ('source_vertices','joints','sample_points'):data[k]=data[k]@C.T
        axis_correction=C.tolist()
    joints=data['joints'];parents=data['parents'].astype(int);points=data['sample_points']
    binding,children,group,main,names,pelvis,head=correspondence(joints,parents)
    bpy.ops.object.select_all(action='DESELECT');mesh=bpy.data.meshes.new(f'{a.animal} candidate{number}')
    mesh.from_pydata(data['source_vertices'].tolist(),[],data['source_faces'].tolist());mesh.validate();mesh.update()
    body=bpy.data.objects.new(f'{a.animal} {number}',mesh);scene.collection.objects.link(body)
    for f in mesh.polygons:f.use_smooth=True
    arm=bpy.data.armatures.new(f'Fresh generated rig {number}');rig=bpy.data.objects.new(f'{a.animal} rig {number}',arm);scene.collection.objects.link(rig)
    rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
    for i,name in enumerate(names):
        bone=arm.edit_bones.new(name);bone.head=joints[i]
        direction=Vector(joints[main[i]]-joints[i]) if main[i]>=0 else Vector(joints[i]-joints[parents[i]])*.3 if parents[i]>=0 else Vector((0,0,.05))
        if i==head:direction=Vector((0,.10,0))
        if direction.length<.01:direction=Vector((0,0,.04))
        bone.tail=bone.head+direction;bone.align_roll(Vector((0,1,0)) if group[i] in ('fore','hind') else Vector((0,0,1)))
        if parents[i]>=0:bone.parent=arm.edit_bones[names[int(parents[i])]]
    bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True;arm.display_type='OCTAHEDRAL'
    tree=KDTree(len(points))
    for k,p in enumerate(points):tree.insert(Vector(p),k)
    tree.balance();weights=data['sample_skin'];vgs=[body.vertex_groups.new(name=n) for n in names]
    for v in mesh.vertices:
        near=tree.find_n(v.co,4);ids=[x[1] for x in near];d=np.array([x[2] for x in near]);w=1/np.maximum(d,.0001)**2
        row=np.maximum(0,(weights[ids]*w[:,None]).sum(0)/w.sum());best=np.argsort(row)[-4:];vals=row[best]/max(row[best].sum(),1e-12)
        for i,w in zip(best,vals):vgs[i].add([v.index],float(w),'REPLACE')
    modifier=body.modifiers.new('Fresh neural skin - linear','ARMATURE');modifier.object=rig;modifier.use_deform_preserve_volume=False;body.parent=rig
    mat=bpy.data.materials.new(f'Reused raw fur {number}');mat.use_nodes=True;nodes=mat.node_tree.nodes;links=mat.node_tree.links
    tex=nodes.new('ShaderNodeTexImage');tex.image=fur;tex.projection='BOX';tex.projection_blend=.25
    coord=nodes.new('ShaderNodeTexCoord');mul=nodes.new('ShaderNodeVectorMath');mul.operation='SCALE';mul.inputs[3].default_value=8
    hsv=nodes.new('ShaderNodeHueSaturation');hsv.name='Review coat variation';bsdf=nodes.get('Principled BSDF');bsdf.inputs['Roughness'].default_value=.85
    if not a.neutral_material:
        links.new(coord.outputs['Generated'],mul.inputs[0]);links.new(mul.outputs[0],tex.inputs['Vector']);links.new(tex.outputs['Color'],hsv.inputs['Color'])
    else:hsv.inputs['Color'].default_value=(.38,.36,.32,1)
    links.new(hsv.outputs[0],bsdf.inputs['Base Color']);mesh.materials.append(mat)
    rest=[arm.bones[n].matrix_local.copy() for n in names];inverse=[m.inverted() for m in rest];local=[inverse[int(p)]@m if p>=0 else m for p,m in zip(parents,rest)]
    refpoints=binding@reference;refdirs=[];bone_refq=[refq[int(np.argmax(row))] for row in binding]
    for i in range(len(names)):
        child=main[i];direction=refpoints[child]-refpoints[i] if child>=0 else np.zeros(3)
        refdirs.append(orient(direction,(0,1,0) if group[i] in ('fore','hind') else (0,0,1)) if np.linalg.norm(direction)>1e-5 else None)
    if source['family']=='bird' and not flightless:
        # A generated bird has open, visible wing geometry. Match each learned
        # wing direction to the closest real authored flap pose, so using Idle
        # as the zero pose does not leave its wings permanently half open.
        flap=next((c for c in source['clips'] if c['label']=='Flap'),None)
        if flap:
            candidates=motions[flap['key']]
            for i in range(len(names)):
                if group[i]!='wing' or main[i]<0:continue
                target=joints[main[i]]-joints[i];target/=max(np.linalg.norm(target),1e-8)
                best=None;score=-2
                for frame in candidates:
                    virtual=binding@frame[:,:3,3];direction=virtual[main[i]]-virtual[i];length=np.linalg.norm(direction)
                    cosine=float(direction@target/max(length,1e-8))
                    if length>1e-5 and cosine>score:score=cosine;best=(direction,frame)
                if best:
                    refdirs[i]=orient(best[0],(0,0,1));bone_refq[i]=Matrix(best[1][int(np.argmax(binding[i]))]).to_quaternion()
    cache={};actions={}
    for clip in clips:
        label=clip['label'];frames=[]
        for frame in motions[clip['key']]:
            virtual=binding@frame[:,:3,3];g=[]
            for i,parent in enumerate(parents):
                if group[i]=='extra' and parent>=0:q=g[int(parent)].to_quaternion()@local[i].to_quaternion()
                elif i==pelvis or i==head or main[i]<0:
                    index=int(np.argmax(binding[i]));q=Matrix(frame[index]).to_quaternion()@bone_refq[i].inverted()@rest[i].to_quaternion()
                elif refdirs[i] is not None:q=orient(virtual[main[i]]-virtual[i],(0,1,0) if group[i] in ('fore','hind') else (0,0,1))@refdirs[i].inverted()@rest[i].to_quaternion()
                else:q=rest[i].to_quaternion()
                location=rest[i].translation.copy() if parent<0 else g[int(parent)]@local[i].translation
                if i==pelvis:location+=Vector(frame[si[source['root']],:3,3]-reference[si[source['root']]])
                m=q.to_matrix().to_4x4();m.translation=location;g.append(m)
            loc=[];rot=[]
            for i,parent in enumerate(parents):
                relative=g[int(parent)].inverted()@g[i] if parent>=0 else g[i];basis=local[i].inverted()@relative;loc.append(basis.translation[:]);rot.append(basis.to_quaternion()[:])
            frames.append((np.array(loc),np.array(rot)))
        cache[label]=frames;actions[label]=bake(rig,label,frames,names)
    def sample(label,seconds):
        array=cache[label];x=seconds*30%max(1,len(array)-1);first=int(x);return blend_pose(array[first],array[min(first+1,len(array)-1)],x-first)
    demos={};move=next((n for n in ('Walk','Run','Swim','Swim Horizontal','Swim Vertical','Side winding','Flap') if n in cache),'Idle')
    locomotion=[('Idle',2),(move,3),('Idle',2)];showcase=[('Idle',2),*[(n,min(5,max(1.5,len(cache[n])/30))) for n in cache if n not in {'Idle','Death'}],('Idle',2)]
    for title,parts in [('Showcase',showcase),('Locomotion',locomotion)]:
        poses=[];previous=None;last_duration=0
        for label,duration in parts:
            for f in range(round(duration*30)):
                t=f/30;pose=sample(label,t)
                if previous and t<.5:pose=blend_pose(sample(previous,last_duration+t),pose,smooth(t/.5))
                poses.append(pose)
            previous=label;last_duration=duration
        demos[title]=bake(rig,title,poses,names)
    entry={'number':number,'rig':rig.name,'body':body.name,'head':'Head','actions':actions,'demos':demos,'material':mat.name,'joints':len(names),'triangles':len(mesh.polygons),
        'rig_file':str(folder/'generated-rig.npz'),'parents':parents.tolist(),'joint_names':names,'generated_joint_heads':joints.tolist(),'binding':binding.tolist(),'groups':group,
        'animal':a.animal,'source_family':source['family'],'source_license':'CC0-1.0','manual_repairs':False,'painted':False,
        'canonical_review_height_m':1.65,'runtime_species_scale_pending_approval':True,'surface_material':'plain neutral' if a.neutral_material else 'reused fur swatch','canonical_axis_correction':axis_correction}
    entries.append(entry);body.hide_set(number!=1);body.hide_render=number!=1;rig.hide_set(True);rig.select_set(False)
    print('Assembled',a.animal,number,len(names),'fresh joints',flush=True)
scene['deer_four_rigs']=json.dumps(entries);scene['fauna_animal']=a.animal;scene.render.fps=30;scene.sync_mode='FRAME_DROP'
world=bpy.data.worlds.new('Raw review world');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.2,.22,.25,1);scene.world=world
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            sp=area.spaces.active;sp.shading.type='MATERIAL';sp.shading.use_scene_world=False;sp.overlay.show_overlays=False;sp.show_region_ui=True
bpy.data.orphans_purge(do_recursive=True)
save_path=root/'Animal - Raw Audition.rebuild.blend' if rebuild else out
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(save_path))
if rebuild:
    bpy.ops.wm.open_mainfile(filepath=str(save_path));assert len(json.loads(bpy.context.scene['deer_four_rigs']))==4
    assert save_path.resolve().parent==root and out.resolve().parent==root
    save_path.replace(out)
labels=[c['label'].lower() for c in clips];gaps=[]
if not any(any(w in label for w in ('eat','feed','forage','graz')) for label in labels):gaps.append('No authored feeding/foraging clip supplied')
if not any(any(w in label for w in ('sleep','rest','sit','coiled')) for label in labels):gaps.append('No dedicated resting/sleep clip supplied')
if not any('dea' in label or 'dying' in label for label in labels):gaps.append('No death clip supplied')
if source.get('adaptation'):gaps.append(source['adaptation'])
if source['family'] in ('rabbit','squirrel') and 'walk' not in labels:gaps.append('No dedicated walk clip in '+source['family']+' source')
if a.animal=='Sea otter':gaps.append('Terrestrial raw-motion trial only; no suitable swimming clip supplied')
(root/'audition.json').write_text(json.dumps({'animal':a.animal,'candidates':entries,'seconds':round(time.perf_counter()-started,2),'status':'ready_for_owner_review','motion_gaps':gaps,'painted':False,'art_approved':False},indent=2))
print('Raw audition ready',a.animal,flush=True)
