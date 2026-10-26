"""Read-only Blender -> compact skinned meshes, native pose palettes and baked materials.

No rig fitting, retargeting, motion repair, neural inference or source saving.
Run with Blender --background --disable-autoexec --threads 2 --python ... -- ...
"""
import argparse,hashlib,json,math,struct,sys,time
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix,Vector

repo=Path(__file__).resolve().parent.parent
root=repo/'artifacts/fauna-runtime-20261026'
ap=argparse.ArgumentParser();ap.add_argument('--slugs',nargs='*');ap.add_argument('--partition',type=int,default=0);ap.add_argument('--partitions',type=int,default=1)
ap.add_argument('--surface',choices=['cpu','cycles'],default='cpu');ap.add_argument('--reuse-bakes',action='store_true')
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
plan=json.loads((root/'export-plan.json').read_text())['animals']
sys.path.insert(0,str(repo/'scripts'))
import fauna_runtime_surface as cpu_surface
signature=hashlib.sha256(Path(__file__).read_bytes()+(repo/'scripts/fauna_runtime_surface.py').read_bytes()).hexdigest()

def floats(file,values):file.write(np.asarray(values,dtype='<f4').tobytes())
def ints(file,values):file.write(np.asarray(values,dtype='<i4').tobytes())
def curves_off(rig):
    rig.animation_data_clear();rig.data.pose_position='REST'
    for bone in rig.pose.bones:
        bone.matrix_basis=Matrix.Identity(4)

def export(item):
    started=time.monotonic();source=Path(item['path']);digest=hashlib.sha256(source.read_bytes()).hexdigest();assert digest==item['source_sha256']
    out=root/'exports'/item['slug'];out.mkdir(parents=True,exist_ok=True)
    receipt=out/'receipt.json'
    reusable={}
    if receipt.exists():
        old=json.loads(receipt.read_text())
        if old.get('source_sha256')==digest and old.get('exporter_sha256')==signature and all((out/f).exists() and hashlib.sha256((out/f).read_bytes()).hexdigest()==h for f,h in old.get('files',{}).items()):
            print('CACHED',item['slug'],flush=True);return
        if a.reuse_bakes and old.get('source_sha256')==digest:
            reusable={name:value for name,value in old.get('files',{}).items() if name.endswith('.png') and (out/name).exists() and hashlib.sha256((out/name).read_bytes()).hexdigest()==value}
    bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0]
    rig=bpy.data.objects[e['rig']];body=bpy.data.objects[e['body']];rig.hide_set(False);curves_off(rig)
    profiles=e.get('painting_coats') or e['approved_art']['coats']
    # A standing gull folds its authored wing surface with a driven shape key.
    # Preserve that native pre-skin morph, rather than treating its folded pose
    # as the common bind mesh for both standing and flight clips.
    shape_drivers=list(body.data.shape_keys.animation_data.drivers) if body.data.shape_keys and body.data.shape_keys.animation_data else []
    driven_keys=[key for key in list(body.data.shape_keys.key_blocks)[1:] if any(d.data_path==key.path_from_id('value') for d in shape_drivers)] if body.data.shape_keys else []
    assert len(driven_keys)<=1,('Unsupported multiple motion morphs',item['slug'])
    morph=driven_keys[0] if driven_keys else None
    driver_mutes=[d.mute for d in shape_drivers]
    for d in shape_drivers:d.mute=True
    # The accepted first body form is retained. Geometry-changing review morphs
    # are not randomized independently of its animation in this first runtime.
    if body.data.shape_keys:
        for key in list(body.data.shape_keys.key_blocks)[1:]:key.value=0
    s.frame_set(0);bpy.context.view_layer.update()
    original=[bpy.data.objects[n] for n in e['objects'] if bpy.data.objects.get(n) and bpy.data.objects[n].type=='MESH']
    assert len(original)<=64
    original.sort(key=lambda ob:0 if ob==body else 1)
    names=[ob.name for ob in original];body_index=names.index(body.name)
    bones=list(rig.data.bones);bone_ids={b.name:i for i,b in enumerate(bones)};assert len(bones)<=128
    maximum_weights=max(sum(g.weight>0 and ob.vertex_groups[g.group].name in bone_ids for g in v.groups) for ob in original for v in ob.data.vertices)
    influences=4 if maximum_weights<=4 else 8
    assert maximum_weights<=8,(item['slug'],maximum_weights)
    # +Z up in Blender -> +Y up in Unity; facing is authored per native.
    forward=Vector(item.get('forward') or [0,1,0]);yaw=math.atan2(forward.x,forward.y)
    turn=Matrix.Rotation(yaw,4,'Z')
    points=[body.matrix_world@Vector(p) for p in body.bound_box]
    low=Vector([min(p[i] for p in points) for i in range(3)]);high=Vector([max(p[i] for p in points) for i in range(3)])
    span=max(high-low);assert span>.001
    axis=Matrix(((1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)))
    C=axis@turn@Matrix.Scale(1/span,4)@Matrix.Translation(Vector((-(low.x+high.x)*.5,-(low.y+high.y)*.5,-low.z)))
    inverseC=C.inverted();inverseRig=rig.matrix_world.inverted();inverseBind=[b.matrix_local.inverted() for b in bones]
    temp=[];part_uv=[]
    # Keep every part, including optional racks, but separate their destination
    # atlas rectangles. All original source UV/attribute layers remain inputs.
    count=max(1,len(original)-1);columns=2;rows=math.ceil(count/columns)
    for pi,ob in enumerate(original):
        ev=ob.evaluated_get(bpy.context.evaluated_depsgraph_get());data=ev.to_mesh(preserve_all_data_layers=True,depsgraph=bpy.context.evaluated_depsgraph_get()).copy();ev.to_mesh_clear()
        copy=ob.copy();copy.data=data;copy.name='EXPORT '+ob.name;s.collection.objects.link(copy)
        world=ob.matrix_world.copy();copy.parent=None;copy.matrix_world=world
        for mod in list(copy.modifiers):copy.modifiers.remove(mod)
        copy.hide_render=False;copy.hide_set(False)
        if data.shape_keys:copy.shape_key_clear()
        default_uv=data.uv_layers.active
        colour_uv=data.uv_layers.get('Fauna Paint UV') or default_uv
        uv=data.uv_layers.new(name='RuntimeUV')
        if pi==0:rect=(.002,.002,.746,.996) if count>0 else (.002,.002,.996,.996)
        else:
            at=pi-1;rect=(.752+(at%columns)*.124,.002+(at//columns)*.996/rows,.120,.996/rows-.004)
        for li,target in enumerate(uv.data):
            source_uv=colour_uv.data[li].uv if colour_uv else (0.5,0.5)
            target.uv=(rect[0]+max(0,min(1,source_uv[0]))*rect[2],rect[1]+max(0,min(1,source_uv[1]))*rect[3])
        if default_uv:default_uv.active_render=True
        temp.append(copy);part_uv.append(rect)
    for ob in original:ob.hide_render=True;ob.hide_set(True)
    # Native surface nodes are evaluated into an unlit colour atlas. This keeps
    # the painted sockets, pigment mixes and source attributes, not just the
    # first image texture encountered in a material graph.
    s.render.engine='CYCLES';s.cycles.device='CPU';s.cycles.samples=1;s.cycles.use_denoising=False
    s.render.threads_mode='FIXED';s.render.threads=2;s.render.bake.margin=2;s.render.bake.use_clear=False
    s.view_settings.view_transform='Standard';s.view_settings.look='None';s.view_settings.exposure=0;s.view_settings.gamma=1
    originals={ob.name:list(ob.data.materials) for ob in temp}
    material_copies=[]
    def prepare_materials(coat,kind,image):
        cache={}
        for pi,ob in enumerate(temp):
            source_materials=originals[ob.name]
            is_coat=names[pi] in [e['body'],*e.get('coat_objects',[])]
            for mi,source_material in enumerate(source_materials):
                if is_coat and mi==0:source_material=bpy.data.materials[profiles[coat]['material']]
                key=source_material.name if source_material else 'empty'
                if key in cache:ob.data.materials[mi]=cache[key];continue
                m=source_material.copy() if source_material else bpy.data.materials.new('Export fallback')
                m.use_nodes=True;material_copies.append(m);cache[key]=m;nodes=m.node_tree.nodes;links=m.node_tree.links
                bsdf=next((n for n in nodes if n.type=='BSDF_PRINCIPLED'),None)
                output=next((n for n in nodes if n.type=='OUTPUT_MATERIAL'),None) or nodes.new('ShaderNodeOutputMaterial')
                if kind in {'colour','mask'}:
                    emission=nodes.new('ShaderNodeEmission')
                    if kind=='colour':
                        source_socket=bsdf.inputs.get('Base Color') if bsdf else None
                        if source_socket and source_socket.is_linked:links.new(source_socket.links[0].from_socket,emission.inputs['Color'])
                        else:emission.inputs['Color'].default_value=tuple(source_socket.default_value) if source_socket else tuple(m.diffuse_color)
                    else:
                        combine=nodes.new('ShaderNodeCombineColor');combine.mode='RGB'
                        for channel,socket_name,default in [('Red','Roughness',.65),('Green','Alpha',1),('Blue','Emission Strength',0)]:
                            socket=bsdf.inputs.get(socket_name) if bsdf else None
                            if socket and socket.is_linked:links.new(socket.links[0].from_socket,combine.inputs[channel])
                            else:combine.inputs[channel].default_value=float(socket.default_value) if socket else default
                        links.new(combine.outputs[0],emission.inputs['Color'])
                    links.new(emission.outputs[0],output.inputs['Surface'])
                node=nodes.new('ShaderNodeTexImage');node.image=image;nodes.active=node
                ob.data.materials[mi]=m
        bpy.ops.object.select_all(action='DESELECT')
        for ob in temp:ob.select_set(True)
        bpy.context.view_layer.objects.active=temp[0]
    files=[]
    cpu_surfaces={}
    def bake(coat,kind,size):
        name=f'coat-{coat}.png' if kind=='colour' else kind+'.png'
        if name in reusable:files.append(name);return
        if a.surface=='cpu':
            if size not in cpu_surfaces:cpu_surfaces[size]=cpu_surface.Surface(temp,size)
            materials=[]
            for pi,ob in enumerate(temp):
                choices=list(originals[ob.name])
                if names[pi] in [e['body'],*e.get('coat_objects',[])]:choices[0]=bpy.data.materials[profiles[coat]['material']]
                materials.append(choices)
            cpu_surfaces[size].bake(kind,out/name,materials);files.append(name)
            print('CPU BAKED',item['slug'],kind,coat,flush=True);return
        image=bpy.data.images.new('Runtime '+kind,width=size,height=size,alpha=True)
        image.colorspace_settings.name='sRGB' if kind=='colour' else 'Non-Color'
        prepare_materials(coat,kind,image)
        bpy.ops.object.bake(type='NORMAL' if kind=='normal' else 'EMIT',normal_space='TANGENT',uv_layer='RuntimeUV',use_clear=False,margin=2)
        image.filepath_raw=str(out/name);image.file_format='PNG';image.save()
        files.append(name)
        # Keep temporary images/materials alive until this Blender process ends.
        # Freeing them between Cycles bakes can invalidate OIIO shader strings.
        print('BAKED',item['slug'],kind,coat,flush=True)
    for coat in range(len(profiles)):bake(coat,'colour',item['textureSize'])
    bake(0,'mask',512);bake(0,'normal',512)
    vertices=[];triangles=[];vertex_origins=[];lookup={};minP=np.full(3,np.inf);maxP=-minP
    for pi,ob in enumerate(temp):
        mesh=ob.data;mesh.calc_loop_triangles();uv=mesh.uv_layers['RuntimeUV']
        group_names={g.index:g.name for g in ob.vertex_groups};W=ob.matrix_world;N=(C@W).to_3x3().inverted().transposed()
        skin=[]
        for v in mesh.vertices:
            pairs=sorted(((bone_ids[group_names[g.group]],g.weight) for g in v.groups if group_names.get(g.group) in bone_ids and g.weight>0),key=lambda x:-x[1])
            if not pairs:pairs=[(0,1.)]
            total=sum(w for _,w in pairs);pairs=[(i,w/total) for i,w in pairs]
            assert len(pairs)<=influences
            pairs+= [(0,0.)]*(influences-len(pairs));skin.append(pairs)
        for triangle in mesh.loop_triangles:
            face=[]
            for li in triangle.loops:
                vi=mesh.loops[li].vertex_index;p=C@W@mesh.vertices[vi].co;n=(N@mesh.corner_normals[li].vector).normalized();q=uv.data[li].uv
                key=(pi,vi,*[round(float(x),7) for x in (*n,*q)])
                if key not in lookup:
                    delta=(C@W).to_3x3()@(morph.data[vi].co-morph.relative_key.data[vi].co) if morph and pi==body_index else Vector((0,0,0))
                    lookup[key]=len(vertices);vertices.append([*p,*n,*q,*[float(i) for i,w in skin[vi]],*[float(w) for i,w in skin[vi]],float(pi),*([*delta] if morph else [])]);vertex_origins.append((pi,vi))
                    minP=np.minimum(minP,p);maxP=np.maximum(maxP,p)
                face.append(lookup[key])
            triangles.extend([face[0],face[2],face[1]])
    assert vertices and np.isfinite(vertices).all() and np.isfinite(triangles).all()
    with (out/'mesh.bin').open('wb') as f:
        f.write(b'WGFAUNA1');ints(f,[len(vertices),len(triangles),len(bones),len(original)])
        floats(f,vertices);ints(f,triangles)
    files.append('mesh.bin')
    for ob in temp:bpy.data.objects.remove(ob,do_unlink=True)
    for ob in original:ob.hide_render=False;ob.hide_set(False)
    fps=s.render.fps/s.render.fps_base;root_bone=next(b for b in bones if b.parent is None)
    for d,mute in zip(shape_drivers,driver_mutes):d.mute=mute
    clips=[];frames=[];morph_frames=[];rig.data.pose_position='POSE';bounds_low=minP.copy();bounds_high=maxP.copy();validation=[]
    sample_indices=[]
    for pi in range(len(original)):
        indices=[i for i,(part,vi) in enumerate(vertex_origins) if part==pi]
        if indices:sample_indices.extend(indices[int(j)] for j in np.linspace(0,len(indices)-1,min(12,len(indices))))
    # Bake original global deformation matrices. No foreign skeleton or local
    # Euler-axis assumptions, and no loss of inherited scale/shear at import.
    for clip in item['clips']:
        act=bpy.data.actions[clip['name']];rig.animation_data_create().action=act;rig.animation_data.action_slot=act.slots[0]
        duration=(clip['frames'][1]-clip['frames'][0])/fps;assert .01<duration<90
        count=max(2,int(math.ceil(duration*30))+1);first_frame=len(frames);anchor=None
        for fi in range(count):
            frame=clip['frames'][0]+(clip['frames'][1]-clip['frames'][0])*fi/(count-1)
            s.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()
            point=rig.matrix_world@rig.pose.bones[root_bone.name].head
            if anchor is None:anchor=point.copy()
            delta=point-anchor;delta.z=0
            left=C@Matrix.Translation(-delta)@rig.matrix_world
            palettes=np.asarray([left@rig.pose.bones[b.name].matrix@inverseBind[bi]@inverseRig@inverseC for bi,b in enumerate(bones)],dtype=np.float32)
            assert np.isfinite(palettes).all();frames.append(palettes)
            morph_value=float(morph.value) if morph else 0
            if morph:morph_frames.append(morph_value)
            if fi in {0,count//2,count-1}:
                v=np.asarray(vertices,np.float32);positions=v[:,:3]+(v[:,-3:]*morph_value if morph else 0);p=np.c_[positions,np.ones(len(v))];outp=np.zeros((len(v),4))
                for slot in range(influences):outp+=np.einsum('nij,nj->ni',palettes[v[:,8+slot].astype(int)],p)*v[:,8+influences+slot,None]
                assert np.isfinite(outp).all()
                bounds_low=np.minimum(bounds_low,outp[:,:3].min(0));bounds_high=np.maximum(bounds_high,outp[:,:3].max(0))
                expected={}
                for pi,ob in enumerate(original):
                    ev=ob.evaluated_get(bpy.context.evaluated_depsgraph_get());evaluated=ev.to_mesh()
                    for index in sample_indices:
                        part,vi=vertex_origins[index]
                        if part==pi:expected[index]=np.asarray(C@(ev.matrix_world@evaluated.vertices[vi].co-delta))
                    ev.to_mesh_clear()
                error=max(float(np.linalg.norm(outp[index,:3]-expected[index])) for index in sample_indices)
                assert error<.0001,('Native skinning mismatch',item['slug'],clip['label'],fi,error)
                validation.append({'clip':clip['label'],'frame':fi,'surface_samples':len(sample_indices),'normalized_error_max':error})
        clips.append({'name':clip['label'],'role':clip['role'],'start':first_frame,'frames':count,'duration':duration})
    with (out/'motion.bin').open('wb') as f:
        f.write(b'WGPOSES1');ints(f,[len(frames),len(bones)]);floats(f,frames)
    files.append('motion.bin')
    if morph:
        with (out/'morph.bin').open('wb') as f:floats(f,morph_frames)
        files.append('morph.bin')
    def mask(visible):
        bits=[0,0,0,0]
        for i,name in enumerate(names):
            if name in visible:bits[i//16]|=1<<(i%16)
        return bits
    optional=set(e.get('appendage_objects',[]))
    if item['slug']=='deer':optional={r['object'] for r in e['approved_art']['antlers']}
    base=set(names)-optional;coats=[]
    for i,profile in enumerate(profiles):
        if item['slug']=='deer':
            options=[{'label':'Antlerless','objects':[]},*[{'label':r.get('label','Antlers'),'objects':[r['object']]} for r in e['approved_art']['antlers']]]
        else:options=profile.get('appendage_variants') or [{'label':profile.get('trait_label','Adult'),'objects':profile.get('visible_extras',[])}]
        coats.append({'label':profile['label'],'options':[{'label':r.get('label','Adult'),'parts':mask(base|set(r.get('objects',[])))} for r in options]})
    manifest={'version':1,'slug':item['slug'],'name':item['name'],'bones':len(bones),'parts':len(names),'vertices':len(vertices),'indices':len(triangles),
              'boundsMin':bounds_low.tolist(),'boundsMax':bounds_high.tolist(),'height':float(maxP[1]-minP[1]),'clips':clips,'coats':coats,
              'textureSize':item['textureSize'],'source_sha256':digest,'source':str(source.relative_to(repo)),'sourceLicense':e.get('source_license','Existing owner deer; CC0 authored motion; generated geometry/materials'),
              'geometryPolicy':'Same selected surface/skin; UV atlas transfer and split seam vertices only. First body morph fixed; optional appendage visibility retained.',
              'motionPolicy':'Original global deformation matrices at 30Hz; planar root travel removed; only approved movement roles exported.'}
    manifest['nativeSkinChecks']=validation;manifest['influences']=influences
    manifest['hasMorph']=bool(morph)
    manifest['surface_method']=a.surface;manifest['reused_native_bakes']=sorted(reusable)
    (out/'model.json').write_text(json.dumps(manifest,indent=2)+'\n');files.append('model.json')
    assert hashlib.sha256(source.read_bytes()).hexdigest()==digest
    receipt.write_text(json.dumps({'source_sha256':digest,'exporter_sha256':signature,'seconds':round(time.monotonic()-started,2),'files':{f:hashlib.sha256((out/f).read_bytes()).hexdigest() for f in files},'complete':True},indent=2)+'\n')
    print('EXPORTED',item['slug'],len(vertices),'vertices',len(clips),'clips',round(time.monotonic()-started,2),'seconds',flush=True)

for i,item in enumerate(plan):
    if a.slugs and item['slug'] not in a.slugs:continue
    if i%a.partitions!=a.partition:continue
    export(item)
