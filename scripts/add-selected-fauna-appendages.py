"""Small skinned horn/tusk libraries on the chosen boar/mouflon colour copies.

The selected body/rig/actions remain untouched. Options are illustrative linked
age/sex review configurations, not implemented population or growth rules.
"""
import argparse,hashlib,json,math,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);assert a.slug in {'wild-boar','mouflon'}
root=repo/'artifacts/fauna-painted-20261024'/a.slug
state=json.loads((root/'paint-state.json').read_text());assert not state['appearance_approved']
land=json.loads((root/'face-landmarks.json').read_text());bpy.ops.wm.open_mainfile(filepath=str(root/'Animal - Painted Species Review.blend'))
s=bpy.context.scene;entry=json.loads(s['deer_four_rigs'])[0];body=bpy.data.objects[entry['body']];rig=bpy.data.objects[entry['rig']]
for name in entry.get('appendage_objects',[]):
    if name in bpy.data.objects:bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
p=np.array([v.co[:] for v in body.data.vertices]);faces=np.array([f.vertices[:] for f in body.data.polygons])
bvh=BVHTree.FromPolygons([Vector(x) for x in p],faces.tolist(),all_triangles=True)
names={g.index:g.name for g in body.vertex_groups};objects=[];libraries={};evidence=[]
front=np.array(land['front_axis']);side_axis=np.array(land['side_axis']);up=np.array([0.,0,1])
eye_mid=(np.array(land['right_eye']['point'])+np.array(land['left_eye']['point']))/2
nose=np.array(land['nose']['point'])

def surface(target):
    q,n,tri,_=bvh.find_nearest(Vector(target));v0,v1,v2=p[faces[tri]];e0=v1-v0;e1=v2-v0;delta=np.array(q)-v0
    d00=e0@e0;d01=e0@e1;d11=e1@e1;den=d00*d11-d01*d01
    if abs(den)<1e-14:w=np.array([1.,0,0])
    else:
        b=(d11*(delta@e0)-d01*(delta@e1))/den;c=(d00*(delta@e1)-d01*(delta@e0))/den;w=np.maximum([1-b-c,b,c],0);w/=w.sum()
    skin={}
    for vertex,factor in zip(faces[tri],w):
        for g in body.data.vertices[int(vertex)].groups:skin[names[g.group]]=skin.get(names[g.group],0)+g.weight*factor
    skin=dict(sorted(skin.items(),key=lambda x:-x[1])[:4]);total=sum(skin.values())
    return np.array(q),{key:value/total for key,value in skin.items()}

def texture_material():
    count=256;y,x=np.mgrid[0:count,0:count];u=x/count;v=y/count
    ivory=a.slug=='wild-boar'
    pigment=(.5+.5*np.sin(v*math.tau*18+np.sin(u*math.tau)*.3)) if not ivory else (.5+.5*np.sin(u*math.tau*12+v*4))
    base=np.array([.83,.78,.64] if ivory else [.34,.265,.175]);rgb=base[None,None,:]*(.88+.16*pigment[:,:,None])
    material=bpy.data.materials.new(a.slug+' appendage keratin');material.use_nodes=True
    shader=material.node_tree.nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.38 if ivory else .64
    im=bpy.data.images.new(a.slug+' keratin colour',width=count,height=count,alpha=True)
    im.pixels.foreach_set(np.dstack([rgb,np.ones_like(u)]).astype(np.float32).ravel());im.pack()
    tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=im;material.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
    if not ivory:
        h=pigment*.0007;dy,dx=np.gradient(h);normal=np.dstack([-dx/(.15/count),-dy/(.5/count),np.ones_like(dx)]);normal/=np.linalg.norm(normal,axis=2,keepdims=True)
        im=bpy.data.images.new(a.slug+' keratin normal',width=count,height=count,alpha=True);im.colorspace_settings.name='Non-Color'
        im.pixels.foreach_set(np.dstack([normal*.5+.5,np.ones_like(u)]).astype(np.float32).ravel());im.pack()
        tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=im;node=material.node_tree.nodes.new('ShaderNodeNormalMap')
        material.node_tree.links.new(tex.outputs['Color'],node.inputs['Color']);material.node_tree.links.new(node.outputs['Normal'],shader.inputs['Normal'])
    return material

material=texture_material()
def tube(name,path,radius,skin,horn=False):
    path=np.asarray(path);vertices=[];polys=[];uv=[];segments=6;last=None
    for i,point in enumerate(path):
        t=i/(len(path)-1);tangent=path[min(i+1,len(path)-1)]-path[max(0,i-1)];tangent/=np.linalg.norm(tangent)
        normal=up if last is None else last;normal=normal-tangent*(normal@tangent)
        if np.linalg.norm(normal)<1e-4:normal=side_axis-tangent*(side_axis@tangent)
        normal/=np.linalg.norm(normal);last=normal;binormal=np.cross(tangent,normal)
        taper=(1-.80*t**1.7)*min(1,(1-t)/.12) if horn else (1-t)**.85
        r=radius*taper+.0006
        for j in range(segments):
            angle=math.tau*j/segments;vertices.append(point+r*(normal*math.cos(angle)+binormal*math.sin(angle)));uv.append((j/segments,t))
    for i in range(len(path)-1):
        for j in range(segments):polys.append((i*segments+j,i*segments+(j+1)%segments,(i+1)*segments+(j+1)%segments,(i+1)*segments+j))
    polys.extend([tuple(reversed(range(segments))),tuple((len(path)-1)*segments+j for j in range(segments))])
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],polys);mesh.update();ob=bpy.data.objects.new(name,mesh);s.collection.objects.link(ob)
    ob.matrix_world=body.matrix_world.copy();mesh.materials.append(material);layer=mesh.uv_layers.new(name='UVMap')
    for poly in mesh.polygons:
        poly.use_smooth=True
        seam=any(uv[mesh.loops[k].vertex_index][0]==0 for k in poly.loop_indices) and any(uv[mesh.loops[k].vertex_index][0]>.8 for k in poly.loop_indices)
        for k in poly.loop_indices:
            a0,b0=uv[mesh.loops[k].vertex_index];layer.data[k].uv=(1 if seam and a0==0 else a0,b0)
    for key,weight in skin.items():group=ob.vertex_groups.new(name=key);group.add(list(range(len(vertices))),float(weight),'REPLACE')
    mod=ob.modifiers.new('Selected rig','ARMATURE');mod.object=rig;mod.use_deform_preserve_volume=False
    objects.append(name);return name

if a.slug=='wild-boar':
    for style,length in [('small',.045),('medium',.075),('large',.11)]:
        pair=[]
        for sign in [-1,1]:
            target=nose-front*.11+side_axis*sign*.085-up*.035;root_point,skin=surface(target)
            path=[root_point+side_axis*sign*(length*.95*math.sin(t*math.pi*.6))+front*(length*.12*t)+up*(length*(t*.60+t*t*.20)) for t in np.linspace(0,1,8)]
            name=f'{a.slug} {style} tusk {sign}';pair.append(tube(name,path,.014*(length/.085)**.6,skin))
            evidence.append({'object':name,'root':root_point.tolist(),'max_root_target_distance':float(np.linalg.norm(target-root_point)),'skin':skin})
        libraries[style]=pair
    adults=[('medium','adult male example'),('small','adult female example'),('large','mature male example')]
    assignment=[adults,adults,adults,[(None,'juvenile pigment study; adult body')],adults]
else:
    for style,radius,angle,count in [('young-ram',.14,3.2,14),('adult-ram',.19,4.4,20),('mature-ram',.23,5.2,24),('small-ewe',.07,1.3,10)]:
        pair=[]
        for sign in [-1,1]:
            target=eye_mid-front*.12+side_axis*sign*.11+up*.11
            hit=bvh.ray_cast(Vector(target+up*.8),Vector(-up),1.6)[0]
            root_point,skin=surface(hit if hit is not None else target)
            root_normal=np.array(bvh.find_nearest(Vector(root_point))[1]);root_normal/=np.linalg.norm(root_normal)
            path=[]
            for theta in np.linspace(0,angle,count):
                lateral=.035*math.sin(min(theta,math.pi/2))+.07*(1-math.exp(-theta))
                path.append(root_point+side_axis*sign*lateral-front*(radius*math.sin(theta))+up*(radius*(math.cos(theta)-1)+theta*.020)-root_normal*.065*math.exp(-theta*8))
            name=f'{a.slug} {style} horn {sign}';pair.append(tube(name,path,(.014 if style=='small-ewe' else .050)*(radius/.19)**.7,skin,horn=True))
            evidence.append({'object':name,'root':root_point.tolist(),'skin':skin,'coil_radius':radius,'coil_angle':angle})
        libraries[style]=pair
    rams=[('adult-ram','adult ram example'),('young-ram','younger ram horn example'),('mature-ram','mature ram horn example')]
    ewes=[('small-ewe','small-horned ewe lineage'),(None,'hornless ewe lineage')]
    assignment=[rams,rams,ewes,[(None,'juvenile pigment study; adult body')],rams+ewes]
assert len(assignment)==len(entry['painting_coats'])
for coat,variants in zip(entry['painting_coats'],assignment):
    options=[{'objects':libraries[style] if style else [],'label':label} for style,label in variants]
    coat.update(visible_extras=options[0]['objects'],trait_label=options[0]['label'],appendage_variants=options)
entry['appendage_objects']=objects;entry['objects']=[entry['body'],*entry.get('facial_objects',[]),*objects]
visible=set(entry['painting_coats'][0]['visible_extras'])
for name in objects:bpy.data.objects[name].hide_set(name not in visible);bpy.data.objects[name].hide_render=name not in visible
s['deer_four_rigs']=json.dumps([entry])
def triangles(name):return sum(len(poly.vertices)-2 for poly in bpy.data.objects[name].data.polygons)
state.update(coats=entry['painting_coats'],appendage_objects=objects,appendage_libraries=libraries,appendage_fits=evidence,appendage_library_triangles=sum(triangles(name) for name in objects))
state['max_visible_triangles']=state['body_triangles']+state['facial_triangles']+max(sum(triangles(name) for name in names) for names in libraries.values())
geometry=hashlib.sha256(np.array([v.co[:] for v in body.data.vertices],np.float32).tobytes()+np.array([f.vertices[:] for f in body.data.polygons],np.int32).tobytes()).hexdigest();assert geometry==state['geometry_sha256']
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(root/'Animal - Painted Species Review.blend'))
(root/'paint-state.json').write_text(json.dumps(state,indent=2)+'\n')
print('APPENDAGES FITTED',a.slug,len(libraries),'options',state['max_visible_triangles'],'max visible triangles; owner art pending',flush=True)
