"""Fit small textured adult horns/antlers/tusks to selected painted native copies.

The approved body, original skeleton, skin and actions remain unchanged. Roots
sample their actual local surface skin. Whole appendages share root influences;
they do not receive a new retargeted limb chain. Native checks follow every save.
"""
import argparse,json,math,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
repo=Path(__file__).resolve().parent.parent;sys.path.insert(0,str(repo/'scripts'))
from fauna_trait_catalog import TRAITS,SOURCES,configurations
from fauna_surface_components import components
ap=argparse.ArgumentParser();ap.add_argument('--slug',choices=sorted(TRAITS),required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=repo/'artifacts/fauna-painted-20261024'/a.slug
state=json.loads((root/'paint-state.json').read_text());assert not state['appearance_approved']
land=json.loads((root/'face-landmarks.json').read_text());blend=root/'Animal - Painted Species Review.blend'
bpy.ops.wm.open_mainfile(filepath=str(blend));s=bpy.context.scene;entry=json.loads(s['deer_four_rigs'])[0]
body=bpy.data.objects[entry['body']];rig=bpy.data.objects[entry['rig']];rig.animation_data_clear();rig.data.pose_position='REST'
for name in entry.get('appendage_objects',[]):
    if name in bpy.data.objects:bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
p=np.array([v.co[:] for v in body.data.vertices]);body.data.calc_loop_triangles();faces=np.array([t.vertices[:] for t in body.data.loop_triangles])
main=max(components(p,faces),key=len);faces=faces[[all(int(i) in main for i in f) for f in faces]]
bvh=BVHTree.FromPolygons([Vector(v) for v in p],faces.tolist(),all_triangles=True)
front=np.array(land['front_axis']);side=np.array(land['side_axis']);up=np.array([0.,0.,1.])
mid=(np.array(land['right_eye']['point'])+land['left_eye']['point'])/2;nose=np.array(land['nose']['point'])
gap=np.linalg.norm(np.array(land['right_eye']['point'])-land['left_eye']['point'])
names={g.index:g.name for g in body.vertex_groups};objects=[];libraries={};evidence=[]
material=bpy.data.materials.new(a.slug+' fitted horn/antler texture');material.use_nodes=True
nodes=material.node_tree.nodes;links=material.node_tree.links;shader=nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.61
image=bpy.data.images.load(str(repo/'artifacts/fauna-assets/textures/keratin-hard-v1.png'),check_existing=True).copy();image.name=a.slug+' compact keratin texture';image.scale(256,256);image.pack()
tex=nodes.new('ShaderNodeTexImage');tex.image=image
tint=nodes.new('ShaderNodeHueSaturation');tint.inputs['Saturation'].default_value=.35;tint.inputs['Value'].default_value=.75 if TRAITS[a.slug][0] in {'antler','palmate','tusk'} else .35
links.new(tex.outputs['Color'],tint.inputs['Color']);links.new(tint.outputs['Color'],shader.inputs['Base Color'])
bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.12;bump.inputs['Distance'].default_value=.0007;links.new(tex.outputs['Color'],bump.inputs['Height']);links.new(bump.outputs['Normal'],shader.inputs['Normal'])

def fit(target,from_above=False):
    hit=bvh.ray_cast(Vector(target+up*.55),Vector(-up),1.1)[0] if from_above else None
    q,n,tri,_=bvh.find_nearest(Vector(hit if hit is not None else target));q=np.array(q);normal=np.array(n)
    v0,v1,v2=p[faces[tri]];u=v1-v0;v=v2-v0;delta=q-v0
    gram=np.array([[u@u,u@v],[u@v,v@v]]);rhs=np.array([delta@u,delta@v])
    bc=np.linalg.lstsq(gram,rhs,rcond=None)[0];factors=np.maximum([1-bc.sum(),*bc],0);factors/=factors.sum()
    skin={}
    for vi,w in zip(faces[tri],factors):
        for group in body.data.vertices[int(vi)].groups:skin[names[group.group]]=skin.get(names[group.group],0)+group.weight*w
    skin=dict(sorted(skin.items(),key=lambda x:-x[1])[:4]);total=sum(skin.values());assert total>0
    return q,normal,{k:float(v/total) for k,v in skin.items()}

def mesh_object(name,verts,polys,skin,uv):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],polys);mesh.update();ob=bpy.data.objects.new(name,mesh);s.collection.objects.link(ob)
    ob.matrix_world=body.matrix_world.copy();mesh.materials.append(material);layer=mesh.uv_layers.new(name='UVMap')
    for poly in mesh.polygons:
        poly.use_smooth=True
        for i in poly.loop_indices:layer.data[i].uv=uv[mesh.loops[i].vertex_index]
    for bone,w in skin.items():ob.vertex_groups.new(name=bone).add(list(range(len(verts))),w,'REPLACE')
    mod=ob.modifiers.new('Original selected head skin','ARMATURE');mod.object=rig
    objects.append(name);return name

def tube(name,path,radius,skin):
    path=np.asarray(path);verts=[];polys=[];uv=[];segments=5;last=side.copy()
    for i,q in enumerate(path):
        t=i/(len(path)-1);tangent=path[min(i+1,len(path)-1)]-path[max(i-1,0)];tangent/=np.linalg.norm(tangent)
        cross=last-tangent*(last@tangent)
        if np.linalg.norm(cross)<.01:cross=up-tangent*(up@tangent)
        cross/=np.linalg.norm(cross);last=cross;other=np.cross(tangent,cross);r=radius*(1-t)**.72+.0003
        for j in range(segments):verts.append(q+r*(cross*math.cos(j*math.tau/segments)+other*math.sin(j*math.tau/segments)));uv.append((j/segments,t))
    for i in range(len(path)-1):
        for j in range(segments):polys.append((i*segments+j,i*segments+(j+1)%segments,(i+1)*segments+(j+1)%segments,(i+1)*segments+j))
    polys += [tuple(reversed(range(segments))),tuple((len(path)-1)*segments+j for j in range(segments))]
    return mesh_object(name,verts,polys,skin,uv)

def paddle(name,root,sign,size,skin,compact=False):
    # Shallow solid palm, not a flat translucent card. Peripheral tines are tubes.
    outline=np.array([[.08,-.02,.08],[.31,-.15,.22],[.64,-.17,.38],[.84,.02,.48],[.71,.26,.44],[.40,.34,.29],[.13,.16,.12]])*size
    centers=[root+side*sign*x+front*y+up*z for x,y,z in outline];center=np.mean(centers,axis=0)
    verts=[];uv=[]
    for h in [-.009,.009]:
        for i,q in enumerate([center,*centers]):verts.append(q+front*h);uv.append((.5+(q-center)@side/max(size,.01),.5+(q-center)@up/max(size,.01)))
    polys=[];n=len(centers);offset=n+1
    for i in range(n):
        j=(i+1)%n;polys.extend([(0,i+1,j+1),(offset,offset+j+1,offset+i+1),(i+1,offset+i+1,offset+j+1,j+1)])
    mesh_object(name,verts,polys,skin,uv)
    for i in ([1,3,5] if compact else [1,2,3,4,5]):
        q=centers[i];direction=(q-center)/np.linalg.norm(q-center)
        tube(name+' tine'+str(i),[q,q+direction*size*.10+up*.02,q+direction*size*.25+up*.07],.012,skin)

def horn_boss(name,point,normal,skin):
    direction=front-normal*(front@normal);direction/=max(np.linalg.norm(direction),1e-6);across=np.cross(normal,direction)
    width=min(.145,max(.075,gap*.34));verts=[];uv=[];polys=[];segments=8;rings=4
    for row in range(rings+1):
        theta=math.pi*row/rings
        for col in range(segments):
            phi=math.tau*col/segments
            verts.append(point-normal*.004+across*width*math.sin(theta)*math.cos(phi)+direction*.075*math.sin(theta)*math.sin(phi)+normal*.026*math.cos(theta));uv.append((col/segments,row/rings))
    for row in range(rings):
        for col in range(segments):polys.append((row*segments+col,row*segments+(col+1)%segments,(row+1)*segments+(col+1)%segments,(row+1)*segments+col))
    mesh_object(name,verts,polys,skin,uv)

variants=configurations(a.slug)
for conf in variants:
    before=len(objects);shape=conf['shape'];length=conf['size'];width=1.18 if conf['id']=='hyp-open' else .78 if conf['id']=='hyp-compact' else 1.
    if shape!='none':
        for sign in [-1,1]:
            if shape=='tusk':target=nose-front*.12+side*sign*.075-up*.025
            else:target=mid-front*.09+side*sign*max(.045,min(gap*.27,.09))+up*.10
            q,normal,skin=fit(target,shape!='tusk');start=q-normal*.018
            name=f'{a.slug} {conf["id"]} {sign}';t=np.linspace(0,1,9)
            if a.slug in {'buffalo','musk-ox'} and conf['sex']!='female' and conf['id']!='water-sweep':horn_boss(name+' broad root',q,normal,skin)
            radial=(.018 if shape in {'antler','palmate','spike','pronghorn','tusk'} else .033)*conf['radius']
            if shape in {'antler','palmate'}:
                path=[start+side*sign*length*width*(.45*x+.10*x*x)-front*length*.35*x*x+up*length*(.86*x-.18*x*x) for x in t]
                if a.slug=='reindeer':
                    path=[start+side*sign*length*width*.55*math.sin(x*math.pi*.70)-front*length*.52*math.sin(x*math.pi*.85)+front*length*.22*x**3+up*length*.95*x for x in t]
                tube(name+' beam',path,radial,skin)
                if shape=='palmate':paddle(name+' palm',start,sign,length*width,skin,conf['id']=='hyp-compact')
                else:
                    phases=[.30,.56,.83] if conf['id']=='hyp-compact' else [.28,.48,.69,.85]
                    for j,phase in enumerate(phases):
                        at=np.array([np.interp(phase,t,np.array(path)[:,axis]) for axis in range(3)])
                        size=length*(.31 if j==0 else .38 if j==1 else .30 if j==2 else .22)
                        tube(name+' tine'+str(j),[at,at+front*size*.20+up*size*.55,at+front*size*.25+up*size],radial*.55,skin)
                    if a.slug=='reindeer':
                        at=np.array(path[1]);end=at+front*length*.46+up*length*.06
                        tube(name+' brow beam',[at,(at+end)/2,end],radial*.65,skin)
                        for j in range(2 if conf['id']=='hyp-compact' else 3):
                            q0=at+(end-at)*(.45+j*.20)
                            tube(name+' brow point'+str(j),[q0,q0+up*length*.10,q0+up*length*.16+side*sign*.015],radial*.30,skin)
            else:
                path=[]
                for x in t:
                    if shape=='tusk':off=side*sign*length*.42*math.sin(x*math.pi*.7)+front*length*.18*x+up*length*(x*.55+x*x*.45)
                    elif shape=='spear':off=up*length*x-front*length*.23*x+side*sign*length*.07*x
                    elif shape=='scimitar':off=up*length*(x-.24*x*x)-front*length*.70*x*x+side*sign*.025*x
                    elif shape=='spike':off=up*length*x-front*length*.28*x
                    elif shape=='pronghorn':off=up*length*(x-.22*x**4)-front*length*.34*x**4+side*sign*.025*x
                    elif shape in {'buffalo','muskox','wildebeest','water-buffalo'}:off=side*sign*length*width*math.sin(x*1.8)-up*length*(.16 if shape=='water-buffalo' else .30)*math.sin(x*math.pi)+up*length*(.46 if shape=='water-buffalo' else .85)*x*x-front*length*.05*x
                    else:off=side*sign*length*width*.50*math.sin(x*2.6)+up*length*(.28*x+.69*x*x)-front*length*.13*x
                    path.append(start+off)
                tube(name+' horn',path,radial,skin)
                if shape=='pronghorn':
                    at=path[4];tube(name+' prong',[at,at+front*length*.20+up*.025,at+front*length*.38+up*.035],radial*.65,skin)
            evidence.append({'id':conf['id'],'side':sign,'root':q.tolist(),'root_normal':normal.tolist(),'skin':skin,'target_distance':float(np.linalg.norm(q-target))})
    libraries[conf['id']]=objects[before:]

for coat in entry['painting_coats']:
    low=coat['label'].lower();hyp='hypothetical' in low
    options=[]
    for conf in variants:
        if a.slug=='buffalo' and ('water-buffalo' in low)!=(conf['id']=='water-sweep'):continue
        if 'female' in low and conf['sex'] not in {'female','both'}:continue
        if 'male' in low and 'female' not in low and 'biased' not in low and conf['sex'] not in {'male','both','seasonal'}:continue
        if a.slug=='oryx' and conf['id']=='scimitar' and 'scimitar' not in low:continue
        if a.slug=='oryx' and 'scimitar' in low and conf['id']!='scimitar':continue
        if any(word in low for word in ['calf','juvenile','young']) and conf['id'] not in {'shed','female','compact','female-hornless'}:continue
        options.append({**conf,'objects':libraries[conf['id']]})
    assert options
    # Hypothetical coat starts with the matching compact/open lineage trait.
    if hyp:options.sort(key=lambda x:not x['hypothetical'])
    coat.update(appendage_variants=options,visible_extras=options[0]['objects'],trait_label=options[0]['label'])
entry.update(appendage_objects=objects,objects=[entry['body'],*entry.get('facial_objects',[]),*objects])
entry['paint_body_distance']=max(entry.get('paint_body_distance',3.2),3.5 if TRAITS[a.slug][0] in {'antler','palmate','spear'} else 3.2)
visible=set(entry['painting_coats'][0]['visible_extras'])
for name in objects:bpy.data.objects[name].hide_set(name not in visible);bpy.data.objects[name].hide_render=name not in visible
s['deer_four_rigs']=json.dumps([entry])
def tri(name):return sum(len(p.vertices)-2 for p in bpy.data.objects[name].data.polygons)
state.update(coats=entry['painting_coats'],appendage_objects=objects,appendage_libraries=libraries,appendage_fits=evidence,appendage_sources=SOURCES,appendage_library_triangles=sum(tri(n) for n in objects))
state['max_visible_triangles']=sum(len(poly.vertices)-2 for poly in body.data.polygons)+state['facial_triangles']+max(sum(tri(n) for n in names) for names in libraries.values())
(root/'paint-state.json').write_text(json.dumps(state,indent=2)+'\n')
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(blend))
print('Linked traits fitted',a.slug,len(variants),'options',state['max_visible_triangles'],'max visible triangles; source body/rig/motion unchanged')
