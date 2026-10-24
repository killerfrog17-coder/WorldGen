"""Stage1 only: symmetric eye beds, fitted eyeballs/lids and anatomical tags."""
import argparse
import json
import math
from pathlib import Path
import sys
import bpy
import bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
ap.add_argument('--replace-unopened',action='store_true')
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
out=root/'Deer - Stage1 Features.blend'
if out.exists() and not a.replace_unopened:raise FileExistsError(out)
bpy.ops.wm.open_mainfile(filepath=str(root/'Deer - Stage1 Shape.blend'))
body=bpy.data.objects['Deer body'];s=bpy.context.scene
cams=json.loads((root/'shape-cameras.json').read_text())
def tree_for(obj):return BVHTree.FromPolygons([v.co for v in obj.data.vertices],[p.vertices for p in obj.data.polygons],all_triangles=True)
tree=tree_for(body)
def pick(view,pixel):
    c=cams[view];m=np.array(c['camera_matrix']);w,h=c['size'];span=c['scale']
    local=np.array([((pixel[0]+.5)/w-.5)*span,(.5-(pixel[1]+.5)/h)*span*h/w,0])
    origin=m[:3,:3]@local+m[:3,3];direction=-m[:3,2]
    hit,n,face,dist=tree.ray_cast(Vector(origin),Vector(direction),10)
    if hit is None:raise ValueError((view,pixel))
    return np.array(hit),np.array(n)
eye,_=pick('head',(590,488));eye[0]=abs(eye[0]);nose,nn=pick('face',(500,648));nose[0]=0
lip,ln=pick('face',(500,679));lip[0]=0
land={};eyes=[];lid_objects=[]
lid_groups=[body.vertex_groups.new(name=name).index for name in
            ('SurfaceLidSkin','SurfaceEyeSurround','SurfaceLidMargin')]
# Replace only the folded ear blades with closed, shallow curved shells.
bm=bmesh.new();bm.from_mesh(body.data)
bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-6,
    plane_co=(0,0,1.44),plane_no=(0,0,1),clear_outer=True,clear_inner=False)
cap_edges=[e for e in bm.edges if e.is_boundary and all(abs(v.co.z-1.44)<1e-4 for v in e.verts)]
cap_vertices=set(v for e in cap_edges for v in e.verts)
if cap_vertices:
    center=sum((v.co for v in cap_vertices),Vector())/len(cap_vertices)
    outer=sorted(cap_vertices,key=lambda v:math.atan2(v.co.y-center.y,v.co.x-center.x))
    inner=[bm.verts.new((v.co+center)*.5+Vector((0,0,.0375))) for v in outer]
    crown=bm.verts.new(center+Vector((0,0,.05)))
    for i in range(len(outer)):
        j=(i+1)%len(outer)
        for vertices in ((outer[i],outer[j],inner[i]),(outer[j],inner[j],inner[i]),(inner[i],inner[j],crown)):
            face=bm.faces.new(vertices);face.normal_update()
            if face.normal.z<0:face.normal_flip()
# Refine the eye neighborhoods before depressing them, so large triangles
# cannot bridge across the eyeball and form angular clipping planes.
near=[]
for e in bm.edges:
    mid=(e.verts[0].co+e.verts[1].co)*.5
    if abs(abs(mid.x)-eye[0])<.075 and abs(mid.y-eye[1])<.075 and abs(mid.z-eye[2])<.065:near.append(e)
if near:bmesh.ops.subdivide_edges(bm,edges=near,cuts=2,use_grid_fill=True)
for _ in range(6):
    selected=[v for v in bm.verts if v.co.z>1.425]
    relaxed={v:sum((e.other_vert(v).co for e in v.link_edges),Vector())/max(1,len(v.link_edges)) for v in selected}
    for v,target in relaxed.items():v.co=v.co.lerp(target,.25)
for _ in range(3):
    selected=[v for v in bm.verts if abs(abs(v.co.x)-eye[0])<.055 and abs(v.co.y-eye[1])<.06 and abs(v.co.z-eye[2])<.045]
    relaxed={v:sum((e.other_vert(v).co for e in v.link_edges),Vector())/max(1,len(v.link_edges)) for v in selected}
    for v,target in relaxed.items():v.co=v.co.lerp(target,.2)
bmesh.ops.triangulate(bm,faces=list(bm.faces))
bm.to_mesh(body.data);bm.free()
ear_objs=[]
for sign in (-1,1):
    root_ear=np.array([sign*.065,eye[1]-.16,1.415]);tip=np.array([sign*.225,eye[1]-.12,1.65])
    axis=tip-root_ear;axis/=np.linalg.norm(axis);across=np.cross(axis,[0,1,0]);across/=np.linalg.norm(across)
    vv=[];ff=[];rows=9;cols=7
    for layer in range(2):
        for row in range(rows):
            t=row/(rows-1);center=root_ear*(1-t)+tip*t;width=.061*math.sin(math.pi*t)**.72+.022*(1-t)**4
            for col in range(cols):
                u=col/(cols-1)*2-1;point=center+across*(u*width)
                point[1]+=.022*u*u*math.sin(math.pi*t)-.020*math.sin(math.pi*t)
                if layer:point[1]-=.006
                vv.append(point)
    for layer in range(2):
        offset=layer*rows*cols
        for row in range(rows-1):
            for col in range(cols-1):
                i=offset+row*cols+col
                ff.extend([(i,i+1,i+cols),(i+1,i+cols+1,i+cols)])
    for row in range(rows-1):
        for col in (0,cols-1):
            i=row*cols+col;j=(row+1)*cols+col;k=rows*cols
            ff.extend([(i,j,i+k),(j,j+k,i+k)])
    ed=bpy.data.meshes.new('Refined ear shell');ed.from_pydata(vv,[],ff);ed.update()
    eb=bmesh.new();eb.from_mesh(ed);bmesh.ops.remove_doubles(eb,verts=list(eb.verts),dist=1e-6)
    bmesh.ops.dissolve_degenerate(eb,edges=list(eb.edges),dist=1e-7);bmesh.ops.recalc_face_normals(eb,faces=list(eb.faces));eb.to_mesh(ed);eb.free()
    eo=bpy.data.objects.new('Refined ear '+str(sign),ed);s.collection.objects.link(eo);ear_objs.append(eo)
    group=eo.vertex_groups.new(name='SurfaceEar_'+('L' if sign==1 else 'R'))
    group.add(list(range(len(ed.vertices))),1.0,'REPLACE')
    for poly in ed.polygons:poly.use_smooth=True
for sign,label in [(1,'right'),(-1,'left')]:
    center=eye.copy();center[0]*=sign
    normal=np.array([sign*.92,.392,0.0]);normal/=np.linalg.norm(normal)
    horizontal=np.cross([0,0,1],normal);horizontal/=np.linalg.norm(horizontal)
    vertical=np.cross(normal,horizontal)
    land['eye_'+label]=dict(point=center.tolist(),normal=normal.tolist(),placement='mirrored anatomical fit')
    dp=np.array([v.co[:] for v in body.data.vertices])-center
    u=dp@horizontal;v=dp@vertical;depth=dp@normal
    q=(u/.036)**2+(v/.024)**2
    bed=np.clip(1-q,0,1)**2*(np.abs(depth)<.04)
    for vertex,amount in zip(body.data.vertices,bed):vertex.co-=Vector(normal)*float(amount*.006)
    # True curved eye, buried in the shaped bed; no painted eyeball on the head.
    ev=[];ef=[];euv=[];rows=9;cols=16
    for row in range(rows):
        theta=math.pi*row/(rows-1)
        for col in range(cols):
            phi=2*math.pi*col/cols;xx=math.sin(theta)*math.cos(phi);yy=math.sin(theta)*math.sin(phi);zz=math.cos(theta)
            pos=center+horizontal*(xx*.020)+vertical*(yy*.014)+normal*(zz*.011-.008)
            ev.append(pos);euv.append((xx*.5+.5,yy*.5+.5))
    for row in range(rows-1):
        for col in range(cols):
            j=(col+1)%cols;i=row*cols+col;k=row*cols+j
            ef.extend([(i,k,i+cols),(k,k+cols,i+cols)])
    ef=[tuple(reversed(f)) for f in ef]
    data=bpy.data.meshes.new('Curved eye '+label);data.from_pydata(ev,[],ef);data.update()
    obj=bpy.data.objects.new('Eye '+label,data);s.collection.objects.link(obj);eyes.append(obj)
    uv=data.uv_layers.new(name='EyeUV')
    for face in data.polygons:
        face.use_smooth=True
        for li in face.loop_indices:uv.data[li].uv=euv[data.loops[li].vertex_index]
    eb=bmesh.new();eb.from_mesh(data);bmesh.ops.remove_doubles(eb,verts=list(eb.verts),dist=1e-6)
    bmesh.ops.dissolve_degenerate(eb,edges=list(eb.edges),dist=1e-7);bmesh.ops.recalc_face_normals(eb,faces=list(eb.faces));eb.to_mesh(data);eb.free()
    # Open the eye and stitch the eyelid directly into its boundary. This avoids
    # overlapping floating rings, coplanar seams and skin across the cornea.
    cb=bmesh.new();cb.from_mesh(body.data);remove=[]
    deform=cb.verts.layers.deform.verify()
    for face in cb.faces:
        dp=np.array(face.calc_center_median())-center
        q=((dp@horizontal-sign*.004)/.040)**2+(dp@vertical/.028)**2
        if q<1.3 and abs(dp@normal)<.045:remove.append(face)
    if remove:bmesh.ops.delete(cb,geom=remove,context='FACES')
    adjacency={}
    for edge in cb.edges:
        if edge.is_boundary and all((v.co-Vector(center)).length<.065 for v in edge.verts):
            for vertex in edge.verts:
                adjacency.setdefault(vertex,[]).append(edge.other_vert(vertex))
    boundary=set(adjacency)
    assert len(boundary)>=5,'Eye opening has no usable boundary'
    angle=lambda vertex:math.atan2((np.array(vertex.co)-center)@vertical,(np.array(vertex.co)-center)@horizontal)%(2*math.pi)
    start=min(boundary,key=angle);outer=[start];previous=None;current=start
    for _ in range(len(boundary)+1):
        options=[v for v in adjacency[current] if v!=previous]
        if not options:raise ValueError('Open eye boundary chain')
        nxt=options[0]
        if nxt==start:break
        outer.append(nxt);previous,current=current,nxt
    assert len(outer)==len(boundary),'Multiple eye boundary components'
    signed=sum(((np.array(outer[i].co)-center)@horizontal)*((np.array(outer[(i+1)%len(outer)].co)-center)@vertical)-
        ((np.array(outer[(i+1)%len(outer)].co)-center)@horizontal)*((np.array(outer[i].co)-center)@vertical) for i in range(len(outer)))
    if signed<0:outer=[outer[0],*reversed(outer[1:])]
    lengths=[(outer[(i+1)%len(outer)].co-v.co).length for i,v in enumerate(outer)]
    oa=np.r_[0,np.cumsum(lengths)];oa=oa/oa[-1]*2*math.pi
    inner=[];segments=24
    for i in range(segments):
        t=2*math.pi*i/segments;sn=math.sin(t)
        u=.018*math.cos(t);v=(.0094 if sn>=0 else .0075)*math.copysign(abs(sn)**(.95 if sn>=0 else 1.2),sn)
        depth=.011*math.sqrt(max(0,1-(u/.020)**2-(v/.014)**2))-.008+.0006
        inner.append(cb.verts.new(center+horizontal*u+vertical*v+normal*depth))
    lip_ring=[]
    for i,vertex in enumerate(inner):
        t=2*math.pi*i/segments;sn=math.sin(t)
        u=.0230*math.cos(t);v=(.0152 if sn>=0 else .0118)*math.copysign(abs(sn)**(1.0 if sn>=0 else 1.25),sn)
        medial=max(0,math.cos(t)*sign)**6
        u+=sign*.006*medial;v-=.002*medial
        # Full upper hood and flatter, thinner lower lid. The upper crest
        # projects above the cornea instead of reading as a painted outline.
        depth=.011*math.sqrt(max(0,1-(u/.020)**2-(v/.014)**2))-.008+.0040+.0080*max(sn,0)
        lip_ring.append(cb.verts.new(center+horizontal*u+vertical*v+normal*depth))
    land['eye_'+label]['upper_lid_roots']=[list(lip_ring[i].co) for i in (1,2,3,5,6,8,9,11)]
    middle=[]
    for i,vertex in enumerate(inner):
        fraction=i/segments;k=min(len(outer)-1,int(np.searchsorted(oa/(2*math.pi),fraction,side='right')-1))
        f0=oa[k]/(2*math.pi);f1=oa[k+1]/(2*math.pi)
        edge_fraction=(fraction-f0)/max(f1-f0,1e-6)
        outer_point=outer[k].co.lerp(outer[(k+1)%len(outer)].co,edge_fraction)
        t=2*math.pi*i/segments
        sn=math.sin(t);u=.028*math.cos(t)
        v=(.019 if sn>=0 else .0155)*math.copysign(abs(sn)**1.1,sn)
        medial=max(0,math.cos(t)*sign)**6
        u+=sign*.008*medial;v-=.003*medial
        depth=((vertex.co-Vector(center)).dot(Vector(normal))*.45+
               (outer_point-Vector(center)).dot(Vector(normal))*.55+.0018)
        middle.append(cb.verts.new(center+horizontal*u+vertical*v+normal*depth))
    # Material regions follow the constructed skin, not a second approximate
    # ellipse that can end up painting the empty aperture instead of the lid.
    for ring,values in [(inner,(1,1,1)),(lip_ring,(1,1,0)),(middle,(0,1,0))]:
        for vertex in ring:
            for index,value in zip(lid_groups,values):vertex[deform][index]=value
    for first,second in [(inner,lip_ring),(lip_ring,middle)]:
        for i in range(segments):
            j=(i+1)%segments
            for points in ((first[i],second[i],first[j]),(first[j],second[i],second[j])):
                face=cb.faces.new(points);face.normal_update()
                if face.normal.dot(Vector(normal))<0:face.normal_flip()
    inner=middle
    oi=ii=0
    while oi<len(outer) or ii<segments:
        on=oa[oi+1] if oi<len(outer) else float('inf')
        it=2*math.pi*(ii+1)/segments if ii<segments else float('inf')
        if on<it:
            verts=(outer[oi%len(outer)],outer[(oi+1)%len(outer)],inner[ii%segments]);oi+=1
        else:
            verts=(outer[oi%len(outer)],inner[(ii+1)%segments],inner[ii%segments]);ii+=1
        try:
            face=cb.faces.new(verts);face.normal_update()
            if face.normal.dot(Vector(normal))<0:face.normal_flip()
        except ValueError:pass
    cb.to_mesh(body.data);cb.free()
land['nose']=dict(point=(nose+np.array([0,.005,.011])).tolist(),normal=[0,1,0])
land['lip']=dict(point=lip.tolist(),normal=ln.tolist())
(root/'landmarks.json').write_text(json.dumps(land,indent=2))
# Lid/body share one UV atlas/material so colour stays continuous at the joins.
bpy.ops.object.select_all(action='DESELECT');body.select_set(True)
for obj in [*lid_objects,*ear_objs]:obj.select_set(True)
bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
for p in body.data.polygons:p.use_smooth=True
body.data.update();bpy.context.view_layer.update()
surface=tree_for(body)
for label in ('right','left'):
    landmark=land['eye_'+label];center=np.array(landmark['point']);normal=np.array(landmark['normal'])
    horizontal=np.cross([0,0,1],normal);horizontal/=np.linalg.norm(horizontal)
    vertical=np.cross(normal,horizontal);brow=[]
    for u,v in [(-.015,.024),(0,.027),(.015,.024)]:
        start=center+horizontal*u+vertical*v+normal*.08
        hit,_,_,_=surface.ray_cast(Vector(start),Vector(-normal),.16)
        if hit is None:raise ValueError('Brow hair has no skin attachment')
        brow.append(list(hit+Vector(normal)*.00025))
    landmark['brow_roots']=brow
(root/'landmarks.json').write_text(json.dumps(land,indent=2))
verts=np.array([v.co[:] for v in body.data.vertices],np.float32);faces=np.array([p.vertices[:] for p in body.data.polygons],np.int32)
normals=np.array([v.normal[:] for v in body.data.vertices],np.float32)
def smooth(a,b,x):
    t=np.clip((x-a)/(b-a),0,1);return t*t*(3-2*t)
x,y,z=verts.T
# These are unrigged material regions, deliberately independent of skin weights.
head=smooth(eye[1]-.22,eye[1]+.05,y)*smooth(1.05,1.26,z)
neck=smooth(.90,1.18,z)*smooth(.10,.45,y)*(1-head)
body_w=(1-head)*(1-neck)
ear_l=smooth(.07,.12,x)*smooth(1.40,1.48,z)
ear_r=smooth(-.07,-.12,x)*smooth(1.40,1.48,z)
for label,target in [('L',ear_l),('R',ear_r)]:
    target[:]=0
    index=body.vertex_groups['SurfaceEar_'+label].index
    for vertex in body.data.vertices:
        if any(g.group==index and g.weight>.5 for g in vertex.groups):target[vertex.index]=1
tail=smooth(-.60,-.83,y)*smooth(.85,1.02,z)
feet=[];anchors={}
for name,side,front in [('ForeHoof_L',-1,True),('ForeHoof_R',1,True),('HindHoof_L',-1,False),('HindHoof_R',1,False)]:
    region=smooth(0,.08,x*side)*(smooth(-.05,.20,y) if front else smooth(.0,-.35,y))*smooth(.20,.12,z)
    feet.append(region)
    points=verts[region>.5];point=np.median(points,axis=0) if len(points) else np.array([side*.1,.3 if front else -.7,.1])
    point[2]=.12;anchors[name]=dict(head=point.tolist(),tail=(point+[0,0,-.08]).tolist())
for label,ew in [('L',ear_l),('R',ear_r)]:
    points=verts[ew>.35];base=points[np.argmin(points[:,2])]
    anchors['Ear_'+label]=dict(head=base.tolist(),tail=points[np.argmax(points[:,2])].tolist())
regions=np.stack([body_w,head,neck,ear_l,ear_r,tail,*feet],axis=1).astype(np.float32)
eye_regions=np.zeros((len(verts),3),np.float32)
for column,name in enumerate(('SurfaceLidSkin','SurfaceEyeSurround','SurfaceLidMargin')):
    index=body.vertex_groups[name].index
    for vertex in body.data.vertices:
        eye_regions[vertex.index,column]=next((g.weight for g in vertex.groups if g.group==index),0)
np.savez_compressed(root/'body-tagged.npz',vertices=verts,faces=faces,normals=normals,region_weights=regions,eye_regions=eye_regions)
(root/'anchors.json').write_text(json.dumps(anchors,indent=2))
ear_frames={label:dict(root=[sign*.065,float(eye[1]-.16),1.415],tip=[sign*.225,float(eye[1]-.12),1.65]) for sign,label in [(1,'L'),(-1,'R')]}
(root/'coat-settings.json').write_text(json.dumps(dict(fresh_model=True,separate_eyes=True,center_x=0,eye_rx=.018,eye_ry=.0085,nose_ry=.023,rhinarium_revision=2,ear_frames=ear_frames,face_revision=6)))
info=dict(body=body.name,eyes=[o.name for o in eyes],model=1,stage=1,rigged=False,
    eye_center=eye.tolist(),head_target=[0,float(eye[1]),float(eye[2])],root=str(root))
s['fauna_stage1']=json.dumps(info)
bpy.ops.wm.save_as_mainfile(filepath=str(out));print('Symmetric sockets, eye meshes and lids prepared.',flush=True)
