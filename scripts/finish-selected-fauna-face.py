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
from fauna_eye_profiles import dimensions,contour
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--root',type=Path)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);assert a.slug in SUPPORTED
kind=family(a.slug)
from fauna_finish_catalog import SUPPORTED as EXTENDED, category as finish_category
extended=a.slug in EXTENDED
if extended:
    category=finish_category(a.slug)
    kind={'bird':'avian','fish':'fish','squid':'fish','ungulate':'caprine','bear':'ursid','canid':'canid','felid':'canid','reptile':'scaled','amphibian':'scaled','turtle':'scaled','snake':'scaled','marine':'scaled','ground_insect':'scaled','spider':'scaled'}.get(category,'small-carnivore')
if a.slug=='sea-otter':kind='small-carnivore'
surface_only_arthropod=extended and category in {'ground_insect','spider'}
canid=kind=='canid';compact=kind!='lagomorph'
root=(a.root or repo/'artifacts/fauna-painted-20261024'/a.slug).resolve()
state=json.loads((root/'paint-state.json').read_text());assert not state['appearance_approved']
assert state['status'] in {'anatomical_colouring_started','atlas_and_face_fitted_working'}
bake=json.loads((root/'coat-bake.json').read_text());land=json.loads((root/'face-landmarks.json').read_text())
physical_eye=bool(land.get('physical_eye_revision'))
bpy.ops.wm.open_mainfile(filepath=str(root/'Animal - Painted Species Review.blend'))
s=bpy.context.scene;entry=json.loads(s['deer_four_rigs'])[0]
body=bpy.data.objects[entry['body']];rig=bpy.data.objects[entry['rig']]
# JSON references are not Blender datablock users. Retain active repaired clips
# when an appearance save clears the currently playing action.
action_names=list(dict.fromkeys([*entry['actions'].values(),*entry['demos'].values()]))
missing=[name for name in action_names if name not in bpy.data.actions]
if missing:
    provenance=json.loads((root/'revision-source.json').read_text())
    with bpy.data.libraries.load(str(repo/provenance['source']),link=False) as (available,loaded):loaded.actions=missing
for name in action_names:bpy.data.actions[name].use_fake_user=True
for polygon in body.data.polygons:polygon.use_smooth=True
from fauna_surface_components import smooth_split_normals
shading_vertices=smooth_split_normals(body.data)
rig.animation_data_clear();rig.data.pose_position='REST'
for name in entry.get('facial_objects',[]):
    if name in bpy.data.objects:bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
p=np.array([v.co[:] for v in body.data.vertices]);body.data.calc_loop_triangles();faces=np.array([t.vertices[:] for t in body.data.loop_triangles])
if physical_eye and land['physical_eye_revision'].get('sculpt_socket_clearance'):
    # A local manual socket correction is permitted by the owner's visual pass.
    # Clear the opening beneath the cornea rather than floating an entire eye
    # in front of an undisturbed solid face. No skin, bones or actions change.
    # Always sculpt from the retained live source bind coordinates. Applying
    # a feathered displacement repeatedly to the last output would accumulate.
    provenance=json.loads((root/'visual-source.json').read_text())
    with bpy.data.libraries.load(str(repo/provenance['source']),link=False) as (available,loaded):
        assert body.data.name in available.meshes,'Original body mesh missing'
        loaded.meshes=[body.data.name]
    original_mesh=loaded.meshes[0];source_positions=np.array([v.co[:] for v in original_mesh.vertices])
    source_shapes={key.name:np.array([v.co[:] for v in key.data]) for key in original_mesh.shape_keys.key_blocks} if original_mesh.shape_keys else {}
    bpy.data.meshes.remove(original_mesh)
    assert source_positions.shape==p.shape
    p=source_positions.copy();changed=[]
    for index,point in state.get('manual_head_positions',{}).items():p[int(index)]=point
    for side in ['right','left']:
        center=np.array(land[side+'_eye']['point']);n,hu,vu=eye_frame(a.slug,land,side);rx,rz=dimensions(land,bake['eye_radius'],side)
        rel=p-center;u=rel@hu;v=rel@vu;depth=rel@n
        radial=np.sqrt((u/rx)**2+(v/rz)**2)
        selected=(radial<1.22)&(np.abs(depth)<rx*1.4)
        sphere=rx*2.5;edge=math.sqrt(sphere*sphere-rx*rx)
        target=np.sqrt(np.maximum(sphere*sphere-u*u-v*v,0))-edge-.003
        t=np.clip((1.22-radial)/.20,0,1);t=t*t*(3-2*t)
        shift=np.maximum(depth-target,0)*t*selected
        p-=shift[:,None]*n;changed.extend(np.where(shift>1e-8)[0].tolist())
    for vertex,point in zip(body.data.vertices,p):vertex.co=point
    if body.data.shape_keys:
        # These bodies carry lineage morphs. Editing mesh vertices alone leaves
        # evaluated geometry on the unchanged Basis key, so the fix is invisible.
        # Apply exactly the same correction to each key, retaining morph deltas.
        displacement=p-source_positions
        for key in body.data.shape_keys.key_blocks:
            assert key.name in source_shapes
            for vertex,point in zip(key.data,source_shapes[key.name]+displacement):vertex.co=point
    body.data.update()
    smooth_split_normals(body.data)
    original_faces=[f.vertices[:] for f in body.data.polygons]
    polygon_bytes=json.dumps(original_faces).encode() if state.get('polygon_hash_encoding')=='json' else np.array(original_faces,np.int32).tobytes()
    state['geometry_sha256']=hashlib.sha256(p.astype(np.float32).tobytes()+polygon_bytes).hexdigest()
    state['socket_surface_correction']={'vertices':sorted(set(changed)),'max_displacement':float(np.linalg.norm(p-source_positions,axis=1).max()),'scope':'Only vertices inside the two explicit socket apertures; topology/UV/skin/rest/actions unchanged.','appearance_approved':False}
# Generated meshes can retain detached flecks. Preserve those selected body
# vertices, but never fit a lid or sample its weights/UV from a floating fleck.
from fauna_surface_components import components as geometric_components
components=geometric_components(p,faces)
main=set().union(*[part for part in components if len(part)>=12]) if a.slug=='pronghorn' else max(components,key=len);face_ids=[i for i,face in enumerate(faces) if all(int(v) in main for v in face)]
bvh=BVHTree.FromPolygons([Vector(x) for x in p],faces[face_ids].tolist(),all_triangles=True)
body_uv=np.array([[body.data.uv_layers.active.data[i].uv[:] for i in t.loops] for t in body.data.loop_triangles])
names={g.index:g.name for g in body.vertex_groups};features=[];coat_objects=[]

def image(filename,noncolour=False):
    path=root/filename;im=bpy.data.images.load(str(path),check_existing=True)
    # Reload after a CPU re-bake; packed previous bytes must not win.
    if im.packed_file:im.unpack(method='REMOVE')
    im.reload()
    if noncolour:im.colorspace_settings.name='Non-Color'
    im.pack();return im

rough=image('coat-roughness.png',True);normal=image('coat-normal.png',True)
from fauna_hypothetical_coats import rationale
if physical_eye:
    # Bind-space nasal weight is independent of UV seams/visibility. It follows
    # the mesh during animation and also covers the profile of the nasal disc.
    F=np.array(land['front_axis']);S=np.array(land['side_axis']);nose=np.array(land['nose']['point']);rel=p-nose;nw,nh=land['physical_eye_revision']['nose_radius']
    if a.slug in {'red-fox','wolf'}:nw*=1.15;nh*=1.25
    q=((rel@S)/nw)**2+(rel[:,2]/nh)**2+((rel@F)/(.095 if a.slug in {'red-fox','wolf'} else .065))**2;t=np.clip((1.12-q)/.47,0,1);nasal=t*t*(3-2*t)
    attr=body.data.attributes.get('Fauna nasal disc') or body.data.attributes.new('Fauna nasal disc','FLOAT','POINT');attr.data.foreach_set('value',nasal.astype(np.float32))
while len(entry['painting_coats'])<len(bake['coats']):
    record=bake['coats'][len(entry['painting_coats'])];label=record['label']
    mat=bpy.data.materials.new(a.slug+' '+label);mat.use_nodes=True;mat.use_fake_user=True
    attr=body.data.color_attributes.new(name='Working '+label,type='BYTE_COLOR',domain='CORNER')
    attr.data.foreach_set('color',np.tile([.3,.3,.3,1.],len(attr.data)).astype(np.float32).ravel())
    added={'label':label,'material':mat.name,'working_vertex_colour':attr.name}
    if entry.get('appendage_objects'):
        template=next((c for c in entry['painting_coats'] if 'hypothetical' in c['label'].lower()),entry['painting_coats'][0])
        for key in ['appendage_variants','visible_extras','trait_label']:
            if key in template:added[key]=template[key]
    entry['painting_coats'].append(added)
for index,coat in enumerate(entry['painting_coats']):
    coat.update(hypothetical='hypothetical' in coat['label'].lower(),ecological_hypothesis=rationale(coat['label']))
    mat=bpy.data.materials[coat['material']];nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
    output=nodes.new('ShaderNodeOutputMaterial');bsdf=nodes.new('ShaderNodeBsdfPrincipled');links.new(bsdf.outputs['BSDF'],output.inputs['Surface'])
    for filename,socket,noncolour in [(bake['coats'][index]['file'],'Base Color',False),('coat-roughness.png','Roughness',True)]:
        tex=nodes.new('ShaderNodeTexImage');tex.image=image(filename,noncolour)
        if socket=='Base Color' and land.get('paired_eye_revision'):
            # A lid can cross several disconnected body UV islands. Interpolating
            # those UVs produces zebra-like streaks even on plain fur. Sample the
            # body atlas into lid vertex colours and interpolate in lid space.
            weight=nodes.new('ShaderNodeAttribute');weight.attribute_name='Fitted lid weight';colour=nodes.new('ShaderNodeVertexColor');colour.layer_name=f'Fitted lid colour {index}';mix=nodes.new('ShaderNodeMixRGB');links.new(weight.outputs['Fac'],mix.inputs[0]);links.new(tex.outputs['Color'],mix.inputs[1]);links.new(colour.outputs['Color'],mix.inputs[2]);links.new(mix.outputs[0],bsdf.inputs[socket])
        else:links.new(tex.outputs['Color'],bsdf.inputs[socket])
    tex=nodes.new('ShaderNodeTexImage');tex.image=normal;nmap=nodes.new('ShaderNodeNormalMap');nmap.inputs['Strength'].default_value=.65
    links.new(tex.outputs['Color'],nmap.inputs['Color']);links.new(nmap.outputs['Normal'],bsdf.inputs['Normal'])
    if land.get('paired_eye_revision'):
        weight=nodes.new('ShaderNodeAttribute');weight.attribute_name='Fitted lid weight';subtract=nodes.new('ShaderNodeMath');subtract.operation='MULTIPLY_ADD';subtract.inputs[1].default_value=-.65;subtract.inputs[2].default_value=.65;links.new(weight.outputs['Fac'],subtract.inputs[0]);links.new(subtract.outputs[0],nmap.inputs['Strength'])
    if physical_eye:
        nasal=nodes.new('ShaderNodeAttribute');nasal.attribute_name='Fauna nasal disc'
        source_colour=bsdf.inputs['Base Color'].links[0].from_socket;mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='DARKEN';mix.inputs[2].default_value=(.012,.009,.007,1)
        links.new(nasal.outputs['Fac'],mix.inputs[0]);links.new(source_colour,mix.inputs[1]);links.new(mix.outputs[0],bsdf.inputs['Base Color'])
        source_rough=bsdf.inputs['Roughness'].links[0].from_socket;rough_mix=nodes.new('ShaderNodeMixRGB');rough_mix.inputs[2].default_value=(.47,.47,.47,1);links.new(nasal.outputs['Fac'],rough_mix.inputs[0]);links.new(source_rough,rough_mix.inputs[1]);links.new(rough_mix.outputs[0],bsdf.inputs['Roughness'])
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
if land.get('paired_eye_revision'):
    bsdf.inputs['Roughness'].default_value=.30;bsdf.inputs['Specular IOR Level'].default_value=.25;bsdf.inputs['Coat Weight'].default_value=0;bsdf.inputs['Coat Roughness'].default_value=.20
if physical_eye:
    bsdf.inputs['Roughness'].default_value=.24;bsdf.inputs['Specular IOR Level'].default_value=.24
# A pigment-only iris: highlights come from the scene, never a painted white dot.
size=256;y,x=np.mgrid[0:size,0:size];u=(x+.5)/size*2-1;v=(y+.5)/size*2-1;r=np.sqrt(u*u+v*v);theta=np.arctan2(v,u)
fibre=.5+.25*np.sin(theta*91+r*19)+.16*np.sin(theta*143-r*38)
iris=np.stack([.125+fibre*.065,.071+fibre*.045,.032+fibre*.020],axis=2)
pupil=np.clip((r-.45)/.07,0,1);rim=np.clip((1-r)/.14,0,1)
if canid:
    iris=np.stack([.16+fibre*.08,.09+fibre*.05,.025+fibre*.018],axis=2)
    if a.slug=='red-fox':pupil=np.clip((np.sqrt((u/.22)**2+(v/.73)**2)-.86)/.14,0,1)
    else:pupil=np.clip((r-.39)/.065,0,1)
elif kind in {'caprine','bovine','equine','cervid'}:
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
if extended and a.slug in {'owl','snowy-owl','falcon','hawk','fish-eagle','forest-eagle'}:
    iris=np.stack([.47+fibre*.13,.30+fibre*.10,.055+fibre*.035],axis=2);pupil=np.clip((r-.46)/.07,0,1)
if extended and category=='snake':
    iris=np.stack([.25+fibre*.10,.19+fibre*.07,.055+fibre*.025],axis=2);pupil=np.clip((np.sqrt((u/.18)**2+(v/.80)**2)-.88)/.14,0,1)
if land.get('eye_revision') and kind in {'caprine','cervid','equine','bovine'}:
    pupil=np.clip((((u/.70)**4+(v/.25)**4)**.25-.88)/.15,0,1)
if physical_eye:
    # Iris dimensions are physical, independent of the eyelid opening's aspect.
    # The previous radial UV stretched a circular iris over an entire ellipse.
    if a.slug=='wolf':
        iris=np.stack([.34+fibre*.11,.29+fibre*.10,.12+fibre*.06],axis=2)
    elif a.slug=='red-fox':
        iris=np.stack([.38+fibre*.15,.205+fibre*.10,.055+fibre*.04],axis=2)
    elif a.slug=='raccoon':
        iris=np.stack([.055+fibre*.025,.035+fibre*.018,.020+fibre*.012],axis=2)
        pupil=np.clip((r-.72)/.06,0,1)
    source=repo/'artifacts/fauna-assets/textures/iris-neutral-radial-v2.png'
    if source.exists():
        # Read the generated pigment field into circular physical coordinates.
        # Its original circular black centre is excluded before applying each
        # species' own pupil, so fox slits cannot inherit a second round pupil.
        detail=bpy.data.images.load(str(source),check_existing=True)
        pixels=np.empty(len(detail.pixels),np.float32);detail.pixels.foreach_get(pixels);pixels=pixels.reshape(detail.size[1],detail.size[0],4)
        radial=.335+.60*np.minimum(r,1.);dx=np.cos(theta)*radial;dy=np.sin(theta)*radial
        xy=np.stack([(.5+.48*dx)*(detail.size[0]-1),(.5+.48*dy)*(detail.size[1]-1)],axis=2)
        lo=xy.astype(int);hi=np.minimum(lo+1,np.array([detail.size[0]-1,detail.size[1]-1]));fraction=xy-lo
        sampled=pixels[lo[:,:,1],lo[:,:,0],:3]*(1-fraction[:,:,0:1])*(1-fraction[:,:,1:2])+pixels[lo[:,:,1],hi[:,:,0],:3]*fraction[:,:,0:1]*(1-fraction[:,:,1:2])+pixels[hi[:,:,1],lo[:,:,0],:3]*(1-fraction[:,:,0:1])*fraction[:,:,1:2]+pixels[hi[:,:,1],hi[:,:,0],:3]*fraction[:,:,0:1]*fraction[:,:,1:2]
        luminance=sampled@np.array([.2126,.7152,.0722])
        iris=sampled
        if a.slug=='wolf':iris=luminance[:,:,None]*np.array([1.10,1.06,.66])
        elif a.slug=='raccoon':iris=sampled*.27
        elif a.slug=='buffalo':iris=sampled*.35
        state['iris_texture_source']={'path':str(source.relative_to(repo)),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'painted_catchlights':False,'pupil':'species-shaped shader pigment mask'}
iris=iris*pupil[:,:,None]*rim[:,:,None]+.008*(1-pupil[:,:,None]*rim[:,:,None]);rgba=np.dstack([iris,np.ones_like(r)])
im=bpy.data.images.get(a.slug+' iris pigment') or bpy.data.images.new(a.slug+' iris pigment',width=size,height=size,alpha=True)
im.pixels.foreach_set(rgba.astype(np.float32).ravel());im.pack()
for node in list(eye.node_tree.nodes):
    if node.type=='TEX_IMAGE':eye.node_tree.nodes.remove(node)
tex=eye.node_tree.nodes.new('ShaderNodeTexImage');tex.image=im;eye.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
if physical_eye:tex.extension='EXTEND'

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

reviewed=bool(land.get('eye_revision'))
segments=24 if reviewed else 16 if compact else 24;rings=5 if reviewed else 4 if compact else 5;rx,rz=dimensions(land,bake['eye_radius']);eye_reports=[]
if physical_eye:segments=40;rings=8
for side in ([] if surface_only_arthropod else ['right','left']):
    rx,rz=dimensions(land,bake['eye_radius'],side)
    anchor=land[side+'_eye'];center=np.array(anchor['point']);n,hu,vu=eye_frame(a.slug,land,side)
    skin=sample(center)[2]
    def ellipse(t):
        if physical_eye:
            # Upper arch is full; the lower edge is shallower, with corners.
            c=math.cos(t);z=math.sin(t)
            upper=.66 if a.slug=='wolf' else .78 if a.slug=='buffalo' else .90
            slope=.13*c*(1 if side=='right' else -1) if a.slug in {'wolf','red-fox'} else 0
            return hu*rx*c+vu*rz*(z*(upper if z>0 else .84)+slope)
        if reviewed:
            x,z=contour(t,kind);return hu*(rx*x)+vu*(rz*z)
        # Small forward tear corner, fuller upper arch, shallow lower lid.
        if kind in {'fish','scaled'}:return hu*(rx*math.cos(t))+vu*(rz*math.sin(t))
        c=math.cos(t);sn=math.sin(t);return hu*(rx*c*(1+.08*max(c,0)))+vu*(rz*sn*(1.03 if sn>0 else .87))
    bulge=.003 if kind=='scaled' else .006 if kind=='rodent' else .003 if kind=='fish' else .0018 if a.slug=='pheasant' else .004 if kind=='avian' else .008 if kind in {'suine','caprine','ursid','bovine','equine','cervid','small-carnivore','beaver'} else .011 if canid else .018
    eye_margin=.0008 if a.slug=='pheasant' or kind=='fish' else .002
    if reviewed:bulge=min(rx,rz)*(.16 if kind in {'fish','scaled'} else .24);eye_margin=min(rx,rz)*.025
    if land.get('paired_eye_revision'):bulge=min(rx,rz)*.16;eye_margin=min(rx,rz)*.08
    sphere_radius=max(rx*2.5,rz*1.65)
    sphere_edge=math.sqrt(sphere_radius*sphere_radius-rx*rx)
    def cap_height(offset):
        return math.sqrt(max(0,sphere_radius*sphere_radius-float(offset@offset)))-sphere_edge
    iris_radius=rz*land.get('physical_eye_revision',{}).get('iris_radius_factor',.95)
    vertices=[center+n*(eye_margin+bulge)];uvs=[(.5,.5)];polygons=[]
    for ring in range(1,rings+1):
        rad=ring/rings
        for i in range(segments):
            t=i*math.tau/segments;offset=ellipse(t)*rad
            vertices.append(center+offset+n*(eye_margin+bulge*math.sqrt(max(0,1-rad*rad))))
            uvs.append((.5+.5*rad*math.cos(t),.5+.5*rad*math.sin(t)))
            if physical_eye:
                vertices[-1]=center+offset+n*(eye_margin+cap_height(offset))
                uvs[-1]=(.5+.5*float(offset@hu)/iris_radius,.5+.5*float(offset@vu)/iris_radius)
    if physical_eye:vertices[0]=center+n*(eye_margin+cap_height(np.zeros(3)))
    def eye_surface_depth(target):
        distance=max(rx,rz)*3+.04
        hit=bvh.ray_cast(Vector(target+n*distance),Vector(-n),distance*2)[0]
        if hit is None or np.linalg.norm(np.asarray(hit)-target)>max(rx,rz)*1.5:hit=sample(target)[0]
        limit=max(rx,rz)*3 if land.get('paired_eye_revision') else min(rx,rz)*1.2
        return float(np.clip((np.asarray(hit)-target)@n,0,limit))
    paired_depth=0.
    convex=land.get('paired_eye_revision',{}).get('convex_lens',False)
    if convex:
        depths=[eye_surface_depth(center+ellipse(t)*.35) for t in np.linspace(0,math.tau,25)[:-1]];paired_depth=min(float(np.quantile(depths,.60)),min(rx,rz)*.10)+land['paired_eye_revision'].get('lens_outset',0)
    if physical_eye:
        # One rigid corneal surface. Never drape iris vertices over individual
        # body triangles: that made the previous eyes kink and stretch.
        required=[]
        for vertex in vertices:
            offset=vertex-center-n*((vertex-center)@n)
            required.append(eye_surface_depth(center+offset)-eye_margin-cap_height(offset))
        paired_depth=max(0.,min(float(max(required))+.0005,rz*.10))
        vertices=[v+n*paired_depth for v in vertices]
    elif reviewed:
        # A low-poly sculpt can protrude in front of a flat replacement lens.
        # Fit the cap's depth to that actual surface so half an iris cannot be
        # buried in the cheek; its slight corneal bulge remains above it.
        for vi,vertex in enumerate(vertices):
            target=vertex-n*((vertex-center)@n)
            vertices[vi]=vertex+n*(paired_depth if convex else eye_surface_depth(target))
    for i in range(segments):polygons.append((0,1+i,1+(i+1)%segments))
    for j in range(rings-1):
        for i in range(segments):
            a0=1+j*segments+i;b0=1+j*segments+(i+1)%segments;c0=b0+segments;d0=a0+segments
            polygons.append((a0,b0,c0,d0))
    if np.dot(np.cross(vertices[polygons[0][1]]-vertices[0],vertices[polygons[0][2]]-vertices[0]),n)<0:polygons=[tuple(reversed(f)) for f in polygons]
    obj(a.slug+' '+side+' eye',vertices,polygons,[eye],uvs,anchor=skin)
    vertices=[];uvs=[];polygons=[];indices=[]
    lid_radii=[1.,1.04,1.10,1.30,1.60] if land.get('paired_eye_revision') else [1.,1.035,1.08,1.16] if reviewed else [1.,1.10,1.21,1.43]
    if convex:lid_radii=[1.,1.03,1.07,1.18,1.36]
    if physical_eye:lid_radii=[1.,1.025,1.075,1.22]
    for ring,rad in enumerate(lid_radii):
        for i in range(segments):
            t=i*math.tau/segments;target=center+ellipse(t)*rad
            hit=bvh.ray_cast(Vector(target+n*.20),Vector(-n),.4)[0]
            if hit is None or np.linalg.norm(np.array(hit)-target)>max(rx,rz)*.8+.008:hit=sample(target)[0]
            surface=np.asarray(hit);upper=max(0,math.sin(t))
            if physical_eye:
                # First edge matches the smooth lens exactly. Only the outer
                # fur edge follows the source skin; the bare band stays narrow.
                edge_offset=ellipse(t)
                edge=center+edge_offset+n*(eye_margin+cap_height(edge_offset)+paired_depth)
                raised=edge+(target-center-edge_offset)+n*(rz*.035 if ring==1 else 0)
                fraction=[0.,0.,.30,1.][ring]
                point=raised*(1-fraction)+(surface+n*.0006)*fraction
            elif reviewed:
                # Narrow bare inner rim, shaped upper arch, surface-fitted fur
                # beyond it. Avoid a thick, uniformly black goggle-like ring.
                depth=eye_surface_depth(target)
                if land.get('paired_eye_revision'):
                    inner=target+n*((paired_depth if convex else depth)+eye_margin+min(rx,rz)*.04*upper)
                    fraction=max(0,(ring-2)/max(1,len(lid_radii)-3));fraction=fraction*fraction*(3-2*fraction);point=inner*(1-fraction)+(surface+n*.0008)*fraction
                elif ring==0:point=target+n*(depth+eye_margin)
                elif ring==1:point=target+n*(depth+min(rx,rz)*(.055+.045*upper))
                elif ring==2:point=target+n*(depth+min(rx,rz)*(.045+.025*upper))
                else:point=target+n*(depth+min(rx,rz)*.008)
            elif compact and ring<3:
                offsets=[.0008,.0013+.0005*upper,.0005] if a.slug=='pheasant' or kind=='fish' else [.002,.004+.002*upper,.0015]
                point=target+n*offsets[ring]
            else:point=target+n*.002 if ring==0 else surface+n*([0,.003+.004*upper,.002,.0007][ring])
            vertices.append(point);uvs.append(sample(surface)[1])
    for j in range(len(lid_radii)-1):
        for i in range(segments):
            polygons.append((j*segments+i,j*segments+(i+1)%segments,(j+1)*segments+(i+1)%segments,(j+1)*segments+i))
            bare_band=j<2 if physical_eye else j<2 if land.get('paired_eye_revision') else True if reviewed else j<(1 if kind=='fish' else 2)
            indices.append(1 if bare_band else 0)
    if np.dot(np.cross(vertices[polygons[0][1]]-vertices[polygons[0][0]],vertices[polygons[0][2]]-vertices[polygons[0][0]]),n)<0:polygons=[tuple(reversed(f)) for f in polygons]
    lid=obj(a.slug+' '+side+' socket and eyelid',vertices,polygons,[body.data.materials[0],lidmat],uvs,indices,anchor=skin)
    if land.get('paired_eye_revision'):
        marker=lid.data.attributes.new('Fitted lid weight','FLOAT','POINT');marker.data.foreach_set('value',np.ones(len(vertices),np.float32))
        loop_vertices=np.array([loop.vertex_index for loop in lid.data.loops]);sample_uv=np.array(uvs)
        for ci,coat in enumerate(bake['coats']):
            im=image(coat['file']);pixels=np.empty(len(im.pixels),np.float32);im.pixels.foreach_get(pixels);pixels=pixels.reshape(im.size[1],im.size[0],4);uv=np.mod(sample_uv,1);xy=uv*np.array([im.size[0]-1,im.size[1]-1]);lo=xy.astype(int);hi=np.minimum(lo+1,np.array([im.size[0]-1,im.size[1]-1]));t=xy-lo
            values=pixels[lo[:,1],lo[:,0]]*(1-t[:,0:1])*(1-t[:,1:2])+pixels[lo[:,1],hi[:,0]]*t[:,0:1]*(1-t[:,1:2])+pixels[hi[:,1],lo[:,0]]*(1-t[:,0:1])*t[:,1:2]+pixels[hi[:,1],hi[:,0]]*t[:,0:1]*t[:,1:2]
            values[:,:3]=np.where(values[:,:3]<=.04045,values[:,:3]/12.92,((values[:,:3]+.055)/1.055)**2.4)
            attr=lid.data.color_attributes.new(name=f'Fitted lid colour {ci}',type='BYTE_COLOR',domain='CORNER');attr.data.foreach_set('color',values[loop_vertices].astype(np.float32).ravel())
        lid.data.update()
    if not reviewed or land.get('paired_eye_revision'):coat_objects.append(lid.name)
    eye_reports.append({'side':side,'point':center.tolist(),'radius':[rx,rz],'skin':skin,'outer_ring_surface_fitted':True,'physical_round_iris':physical_eye,'uniform_cornea_clearance':paired_depth})
    hairy=kind not in {'avian','fish','scaled'} and not (extended and category=='marine' and a.slug!='sea-otter')
    if reviewed and hairy:
        hair_vertices=[];hair_polygons=[]
        for index,t in enumerate([.48,.96,1.45,1.98,2.50]):
            start=center+ellipse(t)*1.04
            start+=n*(eye_surface_depth(start)+min(rx,rz)*.06)
            direction=vu*rz*(.22+.06*math.sin(t))+n*rz*.15+hu*rx*(.08*math.cos(t))
            base=len(hair_vertices)
            for k in range(3):
                progress=k/2;point=start+direction*progress;width=rx*.006*(1-progress)+rx*.0004
                hair_vertices.extend([point+hu*width,point-hu*width])
            for k in range(2):hair_polygons.append((base+k*2,base+k*2+1,base+k*2+3,base+k*2+2))
        for index in range(2):
            start=center+hu*rx*(.35-index*.9)+vu*rz*1.65
            start=sample(start)[0]+n*rz*.025;direction=hu*rx*(.30-index*.45)+vu*rz*.48+n*rz*.25;base=len(hair_vertices)
            for k in range(3):
                progress=k/2;point=start+direction*progress;width=rx*.005*(1-progress)+rx*.0003
                hair_vertices.extend([point+hu*width,point-hu*width])
            for k in range(2):hair_polygons.append((base+k*2,base+k*2+1,base+k*2+3,base+k*2+2))
        obj(a.slug+' '+side+' subtle lashes and brow hairs',hair_vertices,hair_polygons,[whiskermat],anchor=skin)

# Sparse tapered muzzle whiskers, attached using the local muzzle weights.
nose=np.array(land['nose']['point']);vertices=[];polygons=[]
for side in [-1,1]:
    for j in range(10 if a.slug=='walrus' else 5 if a.slug in {'seal','sea-lion','sea-otter'} else 0 if kind in {'avian','fish','scaled'} or (extended and category in {'thickskin','spiny_mammal','armoured_mammal','primate'}) else 5):
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
if kind in {'suine','caprine','ursid','bovine','avian','fish','rodent','equine','cervid','small-carnivore','beaver'}:
    focus=(np.array(land['right_eye']['point'])+np.array(land['left_eye']['point'])+nose)/3
    entry['paint_head_vertex']=int(np.argmin(np.linalg.norm(p-focus,axis=1)))
    entry['paint_head_span']=.90;entry['paint_head_distance']=1.25
    if a.slug=='pheasant':entry['paint_head_span']=.60;entry['paint_head_distance']=.90
s['deer_four_rigs']=json.dumps([entry]);s['fauna_painting']=json.dumps({'source_candidate':state['source_candidate'],'stage':'UV coats and fitted facial features; appearance review pending','coats':entry['painting_coats']})
state.update(status='atlas_and_face_fitted_working',coats=entry['painting_coats'],landmarks_pending=False,facial_objects=features,facial_eye_fits=eye_reports,uv_atlases_installed=True,appearance_approved=False)
state['facial_fit_ignored_components']=len(components)-1
state['facial_feature_policy']='surface_only_arthropod' if surface_only_arthropod else 'local_eye_anchor_skin_for_globe_and_lid'
if kind=='ursid':state['appearance_scope']='Selected shoulder-humped body only. Near-black/cinnamon are pigment studies; a distinct black-bear archetype and cub/seasonal body proportions have not been modelled.'
if kind=='bovine':state['appearance_scope']='Selected adult bison body only. Horn examples vary on the same adult build; bull/cow body dimorphism and reddish calf proportions are not implemented. Shedding is a colour overlay study, not new inherited coat genetics.'
if a.slug=='duck':state['appearance_scope']='Selected adult duck body. Linked breeding-male/female/eclipse and hypothetical plumages; immature pigment study retains the adult build. Duckling proportions, curled male tail geometry, bill morphs and swimming are not implemented.'
if a.slug=='pheasant':state['appearance_scope']='Selected long-tailed pheasant body. Female/immature pigment studies retain this adult tail/body and do not implement real sex/age proportions. Existing folded-wing Idle/Walk bank retained; flight/takeoff and spur/tuft geometry remain absent.'
if kind=='fish':state['appearance_scope']='Selected fish body only. Five countershaded/pattern studies do not create different fin topology or identified Earth taxa. Fixed ocular rims, no mobile eyelids or whiskers. Fin rays are baked opaque pigment; true membrane transmission and school AI are not implemented. Raw rig and swim bank retained.'
if kind=='rodent':state['appearance_scope']='Selected adult rodent body only. Five labelled pigment studies are not exact species/sex/juvenile body matches. Ear/tail morphology and sparse fur silhouette are retained; no new hair cards, age bodies or tail-fullness morphs. Raw debris and motion defects remain independent of colouring.'
if extended:state['appearance_scope']='Selected owner-approved body and original motion retained. Anatomical '+category+' surface treatment and labelled pigment studies; species/sex/age silhouettes follow the existing plan limitations. Appearance review pending.'
original_faces=[f.vertices[:] for f in body.data.polygons]
polygon_bytes=json.dumps(original_faces).encode() if state.get('polygon_hash_encoding')=='json' else np.array(original_faces,np.int32).tobytes()
geometry=hashlib.sha256(np.array([v.co[:] for v in body.data.vertices],np.float32).tobytes()+polygon_bytes).hexdigest()
assert geometry==state['geometry_sha256'],'Selected body geometry changed'
state['coincident_shading_vertices_smoothed']=shading_vertices
state['body_triangles']=sum(len(f.vertices)-2 for f in body.data.polygons)
state['facial_triangles']=sum(sum(len(f.vertices)-2 for f in bpy.data.objects[name].data.polygons) for name in features)
bpy.context.preferences.filepaths.save_version=0;s.frame_set(0)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'Animal - Painted Species Review.blend'))
(root/'paint-state.json').write_text(json.dumps(state,indent=2)+'\n')
print('ATLAS AND FACE FITTED',a.slug,state['body_triangles'],state['facial_triangles'],'triangles; appearance pending',flush=True)
