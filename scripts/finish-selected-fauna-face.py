"""Install CPU coat atlases and fitted, separately skinned facial features.

Unpublished supported mammal working copies only. The selected body, weights,
hierarchy and source actions stay unchanged; appearance still needs review.
"""
import argparse, hashlib, json, math, sys
from pathlib import Path
import bpy, numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

repo=Path(__file__).resolve().parent.parent
sys.path.insert(0,str(repo/'scripts'))
from fauna_paint_recipes import SUPPORTED, family, eye_frame
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);assert a.slug in SUPPORTED
kind=family(a.slug);canid=kind=='canid';compact=kind!='lagomorph'
root=repo/'artifacts/fauna-painted-20261024'/a.slug
state=json.loads((root/'paint-state.json').read_text());assert not state['appearance_approved']
assert state['status'] in {'anatomical_colouring_started','atlas_and_face_fitted_working'}
bake=json.loads((root/'coat-bake.json').read_text());land=json.loads((root/'face-landmarks.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(root/'Animal - Painted Species Review.blend'))
s=bpy.context.scene;entry=json.loads(s['deer_four_rigs'])[0]
body=bpy.data.objects[entry['body']];rig=bpy.data.objects[entry['rig']]
rig.animation_data_clear();rig.data.pose_position='REST'
for name in entry.get('facial_objects',[]):
    if name in bpy.data.objects:bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
p=np.array([v.co[:] for v in body.data.vertices]);faces=np.array([f.vertices[:] for f in body.data.polygons])
# Generated meshes can retain detached flecks. Preserve those selected body
# vertices, but never fit a lid or sample its weights/UV from a floating fleck.
adj=[set() for _ in p]
for face in faces:
    for vertex in face:adj[vertex].update(int(i) for i in face if i!=vertex)
remaining=set(range(len(p)));components=[]
while remaining:
    seed=remaining.pop();component={seed};stack=[seed]
    while stack:
        for vertex in adj[stack.pop()]:
            if vertex in remaining:remaining.remove(vertex);component.add(vertex);stack.append(vertex)
    components.append(component)
main=max(components,key=len);face_ids=[i for i,face in enumerate(faces) if all(int(v) in main for v in face)]
bvh=BVHTree.FromPolygons([Vector(x) for x in p],faces[face_ids].tolist(),all_triangles=True)
body_uv=np.array([x.uv[:] for x in body.data.uv_layers.active.data]).reshape(-1,3,2)
names={g.index:g.name for g in body.vertex_groups};features=[];coat_objects=[]

def image(filename,noncolour=False):
    path=root/filename;im=bpy.data.images.load(str(path),check_existing=True)
    # Reload after a CPU re-bake; packed previous bytes must not win.
    if im.packed_file:im.unpack(method='REMOVE')
    im.reload()
    if noncolour:im.colorspace_settings.name='Non-Color'
    im.pack();return im

rough=image('coat-roughness.png',True);normal=image('coat-normal.png',True)
for index,coat in enumerate(entry['painting_coats']):
    mat=bpy.data.materials[coat['material']];nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
    output=nodes.new('ShaderNodeOutputMaterial');bsdf=nodes.new('ShaderNodeBsdfPrincipled');links.new(bsdf.outputs['BSDF'],output.inputs['Surface'])
    for filename,socket,noncolour in [(bake['coats'][index]['file'],'Base Color',False),('coat-roughness.png','Roughness',True)]:
        tex=nodes.new('ShaderNodeTexImage');tex.image=image(filename,noncolour);links.new(tex.outputs['Color'],bsdf.inputs[socket])
    tex=nodes.new('ShaderNodeTexImage');tex.image=normal;nmap=nodes.new('ShaderNodeNormalMap');nmap.inputs['Strength'].default_value=.65
    links.new(tex.outputs['Color'],nmap.inputs['Color']);links.new(nmap.outputs['Normal'],bsdf.inputs['Normal'])
    coat.update(colour_atlas=bake['coats'][index]['file'],roughness_atlas='coat-roughness.png',normal_atlas='coat-normal.png')
body.data.materials[0]=bpy.data.materials[entry['painting_coats'][0]['material']]

def plain(name,colour,roughness):
    mat=bpy.data.materials.get(name) or bpy.data.materials.new(name);mat.use_nodes=True
    bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(*colour,1);bsdf.inputs['Roughness'].default_value=roughness
    return mat

lidmat=plain(a.slug+' bare eyelid',(.027,.017,.012),.46)
if kind=='fish':lidmat=plain(a.slug+' fixed ocular rim',(.16,.15,.095),.30)
whiskermat=plain(a.slug+' whiskers',(.25,.23,.18),.7)
eye=plain(a.slug+' living eye',(.025,.014,.007),.12);bsdf=eye.node_tree.nodes.get('Principled BSDF');bsdf.inputs['IOR'].default_value=1.38
bsdf.inputs['Coat Weight'].default_value=.30;bsdf.inputs['Coat Roughness'].default_value=.08
# A pigment-only iris: highlights come from the scene, never a painted white dot.
size=256;y,x=np.mgrid[0:size,0:size];u=(x+.5)/size*2-1;v=(y+.5)/size*2-1;r=np.sqrt(u*u+v*v);theta=np.arctan2(v,u)
fibre=.5+.25*np.sin(theta*91+r*19)+.16*np.sin(theta*143-r*38)
iris=np.stack([.125+fibre*.065,.071+fibre*.045,.032+fibre*.020],axis=2)
pupil=np.clip((r-.45)/.07,0,1);rim=np.clip((1-r)/.14,0,1)
if canid:
    iris=np.stack([.16+fibre*.08,.09+fibre*.05,.025+fibre*.018],axis=2)
    if a.slug=='red-fox':pupil=np.clip((np.sqrt((u/.22)**2+(v/.73)**2)-.86)/.14,0,1)
    else:pupil=np.clip((r-.39)/.065,0,1)
elif kind in {'caprine','bovine'}:
    iris=np.stack([.22+fibre*.08,.14+fibre*.05,.035+fibre*.02],axis=2)
    pupil=np.clip((np.sqrt((u/.80)**2+(v/.20)**2)-.86)/.14,0,1)
elif kind=='avian':
    iris=np.stack([.055+fibre*.025,.031+fibre*.015,.012+fibre*.008],axis=2)
    pupil=np.clip((r-.64)/.07,0,1)
    if a.slug=='pheasant':
        iris=np.stack([.22+fibre*.06,.12+fibre*.035,.025+fibre*.015],axis=2)
        pupil=np.clip((r-.43)/.07,0,1)
elif kind=='fish':
    iris=np.stack([.30+fibre*.07,.27+fibre*.06,.13+fibre*.04],axis=2)
    pupil=np.clip((r-.56)/.065,0,1)
elif kind=='rodent':
    iris=np.stack([.055+fibre*.025,.030+fibre*.014,.012+fibre*.008],axis=2)
    pupil=np.clip((r-.66)/.07,0,1)
iris=iris*pupil[:,:,None]*rim[:,:,None]+.008*(1-pupil[:,:,None]*rim[:,:,None]);rgba=np.dstack([iris,np.ones_like(r)])
im=bpy.data.images.get(a.slug+' iris pigment') or bpy.data.images.new(a.slug+' iris pigment',width=size,height=size,alpha=True)
im.pixels.foreach_set(rgba.astype(np.float32).ravel());im.pack()
for node in list(eye.node_tree.nodes):
    if node.type=='TEX_IMAGE':eye.node_tree.nodes.remove(node)
tex=eye.node_tree.nodes.new('ShaderNodeTexImage');tex.image=im;eye.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])

def bary(point,tri):
    v0,v1,v2=p[faces[tri]];e0=v1-v0;e1=v2-v0;v=np.array(point)-v0
    d00=e0@e0;d01=e0@e1;d11=e1@e1;d20=v@e0;d21=v@e1;den=d00*d11-d01*d01
    if abs(den)<1e-14:return np.array([1.,0,0])
    b=(d11*d20-d01*d21)/den;c=(d00*d21-d01*d20)/den
    w=np.maximum([1-b-c,b,c],0);return w/max(w.sum(),1e-8)

def sample(point):
    q,normal,tri,_=bvh.find_nearest(Vector(point));tri=face_ids[tri];w=bary(q,tri);skin={}
    for vertex,factor in zip(faces[tri],w):
        for g in body.data.vertices[int(vertex)].groups:skin[names[g.group]]=skin.get(names[g.group],0)+factor*g.weight
    skin=dict(sorted(skin.items(),key=lambda x:-x[1])[:4]);total=sum(skin.values());skin={k:v/total for k,v in skin.items()}
    return np.asarray(q),w@body_uv[tri],skin

def obj(name,vertices,polygons,materials,uvs=None,indices=None,anchor=None):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],polygons);mesh.update()
    ob=bpy.data.objects.new(name,mesh);s.collection.objects.link(ob);ob.matrix_world=body.matrix_world.copy()
    for mat in materials:mesh.materials.append(mat)
    for i,poly in enumerate(mesh.polygons):poly.use_smooth=True;poly.material_index=indices[i] if indices else 0
    if uvs is not None:
        layer=mesh.uv_layers.new(name='UVMap')
        for loop in mesh.loops:layer.data[loop.index].uv=uvs[loop.vertex_index]
    for i,point in enumerate(vertices):
        skin=anchor if anchor is not None else sample(point)[2]
        for key,weight in skin.items():
            group=ob.vertex_groups.get(key) or ob.vertex_groups.new(name=key);group.add([i],float(weight),'REPLACE')
    mod=ob.modifiers.new('Selected rig','ARMATURE');mod.object=rig;mod.use_deform_preserve_volume=False
    features.append(ob.name);return ob

segments=16 if compact else 24;rings=4 if compact else 5;rx,rz=bake['eye_radius'];eye_reports=[]
for side in ['right','left']:
    anchor=land[side+'_eye'];center=np.array(anchor['point']);n,hu,vu=eye_frame(a.slug,land,side)
    skin=sample(center)[2]
    def ellipse(t):
        # Small forward tear corner, fuller upper arch, shallow lower lid.
        if kind=='fish':return hu*(rx*math.cos(t))+vu*(rz*math.sin(t))
        c=math.cos(t);sn=math.sin(t);return hu*(rx*c*(1+.08*max(c,0)))+vu*(rz*sn*(1.03 if sn>0 else .87))
    bulge=.006 if kind=='rodent' else .003 if kind=='fish' else .0018 if a.slug=='pheasant' else .004 if kind=='avian' else .008 if kind in {'suine','caprine','ursid','bovine'} else .011 if canid else .018
    eye_margin=.0008 if a.slug=='pheasant' or kind=='fish' else .002
    vertices=[center+n*(eye_margin+bulge)];uvs=[(.5,.5)];polygons=[]
    for ring in range(1,rings+1):
        rad=ring/rings
        for i in range(segments):
            t=i*math.tau/segments;offset=ellipse(t)*rad
            vertices.append(center+offset+n*(eye_margin+bulge*math.sqrt(max(0,1-rad*rad))))
            uvs.append((.5+.5*rad*math.cos(t),.5+.5*rad*math.sin(t)))
    for i in range(segments):polygons.append((0,1+i,1+(i+1)%segments))
    for j in range(rings-1):
        for i in range(segments):
            a0=1+j*segments+i;b0=1+j*segments+(i+1)%segments;c0=b0+segments;d0=a0+segments
            polygons.append((a0,b0,c0,d0))
    if np.dot(np.cross(vertices[polygons[0][1]]-vertices[0],vertices[polygons[0][2]]-vertices[0]),n)<0:polygons=[tuple(reversed(f)) for f in polygons]
    obj(a.slug+' '+side+' eye',vertices,polygons,[eye],uvs,anchor=skin)
    vertices=[];uvs=[];polygons=[];indices=[]
    for ring,rad in enumerate([1.,1.10,1.21,1.43]):
        for i in range(segments):
            t=i*math.tau/segments;target=center+ellipse(t)*rad
            hit=bvh.ray_cast(Vector(target+n*.20),Vector(-n),.4)[0]
            if hit is None:hit=sample(target)[0]
            surface=np.asarray(hit);upper=max(0,math.sin(t))
            if compact and ring<3:
                offsets=[.0008,.0013+.0005*upper,.0005] if a.slug=='pheasant' or kind=='fish' else [.002,.004+.002*upper,.0015]
                point=target+n*offsets[ring]
            else:point=target+n*.002 if ring==0 else surface+n*([0,.003+.004*upper,.002,.0007][ring])
            vertices.append(point);uvs.append(sample(surface)[1])
    for j in range(3):
        for i in range(segments):
            polygons.append((j*segments+i,j*segments+(i+1)%segments,(j+1)*segments+(i+1)%segments,(j+1)*segments+i));indices.append(1 if j<(1 if kind=='fish' else 2) else 0)
    if np.dot(np.cross(vertices[polygons[0][1]]-vertices[polygons[0][0]],vertices[polygons[0][2]]-vertices[polygons[0][0]]),n)<0:polygons=[tuple(reversed(f)) for f in polygons]
    lid=obj(a.slug+' '+side+' socket and eyelid',vertices,polygons,[body.data.materials[0],lidmat],uvs,indices);coat_objects.append(lid.name)
    eye_reports.append({'side':side,'point':center.tolist(),'radius':[rx,rz],'skin':skin,'outer_ring_surface_fitted':True})

# Sparse tapered muzzle whiskers, attached using the local muzzle weights.
nose=np.array(land['nose']['point']);vertices=[];polygons=[]
for side in [-1,1]:
    for j in range(0 if kind in {'avian','fish'} else 5):
        if compact:
            start=nose+np.array(land['side_axis'])*side*(.018+j*.002)-np.array(land['front_axis'])*(.022+j*.005)+np.array([0,0,-.009+j*.007])
            direction=np.array(land['side_axis'])*side*(.090+j*.008)-np.array(land['front_axis'])*(.028+j*.012)+np.array([0,0,(j-2)*.014])
        else:
            start=nose+np.array([side*(.018+j*.002),-.022-j*.005,-.009+j*.007])
            direction=np.array([side*(.090+j*.008),-.028-j*.012,(j-2)*.014])
        start=sample(start)[0]
        for k in range(4):
            t=k/3;point=start+direction*t+np.array([0,0,.008*math.sin(t*math.pi)])
            width=.00065*(1-t)+.00004;vertices.extend([point+[0,0,width],point-[0,0,width]])
        base=len(vertices)-8
        for k in range(3):polygons.append((base+k*2,base+k*2+1,base+k*2+3,base+k*2+2))
if vertices:obj(a.slug+' muzzle whiskers',vertices,polygons,[whiskermat],anchor=sample(nose)[2])

entry.update(facial_objects=features,coat_objects=coat_objects,objects=[entry['body'],*features,*entry.get('appendage_objects',[])])
entry['paint_face_direction']=land['front_axis']
if kind in {'suine','caprine','ursid','bovine','avian','fish','rodent'}:
    focus=(np.array(land['right_eye']['point'])+np.array(land['left_eye']['point'])+nose)/3
    entry['paint_head_vertex']=int(np.argmin(np.linalg.norm(p-focus,axis=1)))
    entry['paint_head_span']=.90;entry['paint_head_distance']=1.25
    if a.slug=='pheasant':entry['paint_head_span']=.60;entry['paint_head_distance']=.90
s['deer_four_rigs']=json.dumps([entry]);s['fauna_painting']=json.dumps({'source_candidate':state['source_candidate'],'stage':'UV coats and fitted facial features; appearance review pending','coats':entry['painting_coats']})
state.update(status='atlas_and_face_fitted_working',coats=entry['painting_coats'],landmarks_pending=False,facial_objects=features,facial_eye_fits=eye_reports,uv_atlases_installed=True,appearance_approved=False)
state['facial_fit_ignored_components']=len(components)-1
if kind=='ursid':state['appearance_scope']='Selected shoulder-humped body only. Near-black/cinnamon are pigment studies; a distinct black-bear archetype and cub/seasonal body proportions have not been modelled.'
if kind=='bovine':state['appearance_scope']='Selected adult bison body only. Horn examples vary on the same adult build; bull/cow body dimorphism and reddish calf proportions are not implemented. Shedding is a colour overlay study, not new inherited coat genetics.'
if a.slug=='duck':state['appearance_scope']='Selected adult duck body. Linked breeding-male/female/eclipse and hypothetical plumages; immature pigment study retains the adult build. Duckling proportions, curled male tail geometry, bill morphs and swimming are not implemented.'
if a.slug=='pheasant':state['appearance_scope']='Selected long-tailed pheasant body. Female/immature pigment studies retain this adult tail/body and do not implement real sex/age proportions. Existing folded-wing Idle/Walk bank retained; flight/takeoff and spur/tuft geometry remain absent.'
if kind=='fish':state['appearance_scope']='Selected fish body only. Five countershaded/pattern studies do not create different fin topology or identified Earth taxa. Fixed ocular rims, no mobile eyelids or whiskers. Fin rays are baked opaque pigment; true membrane transmission and school AI are not implemented. Raw rig and swim bank retained.'
if kind=='rodent':state['appearance_scope']='Selected adult rodent body only. Five labelled pigment studies are not exact species/sex/juvenile body matches. Ear/tail morphology and sparse fur silhouette are retained; no new hair cards, age bodies or tail-fullness morphs. Raw debris and motion defects remain independent of colouring.'
geometry=hashlib.sha256(np.array([v.co[:] for v in body.data.vertices],np.float32).tobytes()+np.array([f.vertices[:] for f in body.data.polygons],np.int32).tobytes()).hexdigest()
assert geometry==state['geometry_sha256'],'Selected body geometry changed'
state['body_triangles']=sum(len(f.vertices)-2 for f in body.data.polygons)
state['facial_triangles']=sum(sum(len(f.vertices)-2 for f in bpy.data.objects[name].data.polygons) for name in features)
bpy.context.preferences.filepaths.save_version=0;s.frame_set(0)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'Animal - Painted Species Review.blend'))
(root/'paint-state.json').write_text(json.dumps(state,indent=2)+'\n')
print('ATLAS AND FACE FITTED',a.slug,state['body_triangles'],state['facial_triangles'],'triangles; appearance pending',flush=True)
