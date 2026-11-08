"""Prepare bounded-LOD rock faces from the recorded CC0 geometry sources.
Blender --background --disable-autoexec --python scripts/export-cliff-rock-models.py
The original textures are neither downloaded nor exported.
"""
from pathlib import Path
import bpy,bmesh,json,struct,hashlib
import numpy as np
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[1]
sources=json.loads((ROOT/'assets-source/terrain/rocks/polyhaven/sources.json').read_text())['assets']
out=ROOT/'unity/WorldGen.Unity/Assets/WorldGen/Resources/CliffRockV1';out.mkdir(parents=True,exist_ok=True)
records=[]
def close_back(mesh):
    """Split pinched scan vertices, fill small holes and add a flat backing.
    No offset along acute vertex normals, so no solidify spikes.
    """
    points=[v.co.copy() for v in mesh.vertices];faces=[list(p.vertices) for p in mesh.polygons]
    edges={};incident=[[] for _ in points]
    for i,face in enumerate(faces):
        for v in face:incident[v].append(i)
        for a,b in zip(face,face[1:]+face[:1]):edges.setdefault(tuple(sorted((a,b))),[]).append(i)
    original=[f[:] for f in faces]
    for v,around in enumerate(incident):
        unseen=set(around);groups=[]
        while unseen:
            todo=[unseen.pop()];group=set(todo)
            while todo:
                i=todo.pop()
                for other in original[i]:
                    if other==v:continue
                    for neighbour in edges[tuple(sorted((v,other)))]:
                        if neighbour in unseen:unseen.remove(neighbour);group.add(neighbour);todo.append(neighbour)
            groups.append(group)
        for group in groups[1:]:
            index=len(points);points.append(points[v].copy())
            for i in group:faces[i]=[index if a==v else a for a in faces[i]]
    bm=bmesh.new();verts=[bm.verts.new(p) for p in points]
    for face in faces:bm.faces.new([verts[i] for i in face])
    unseen=set(bm.faces);components=[]
    while unseen:
        todo=[unseen.pop()];group=set(todo)
        while todo:
            face=todo.pop()
            for edge in face.edges:
                for other in edge.link_faces:
                    if other in unseen:unseen.remove(other);group.add(other);todo.append(other)
        components.append(group)
    keep=max(components,key=lambda group:sum(f.calc_area() for f in group))
    bmesh.ops.delete(bm,geom=[f for group in components if group is not keep for f in group],context='FACES')
    boundary=[e for e in bm.edges if e.is_boundary];directed={}
    for edge in boundary:
        loop=edge.link_loops[0];a=loop.vert;b=loop.link_loop_next.vert
        assert a not in directed,'Unsplit scan boundary junction';directed[a]=b
    loops=[]
    while directed:
        start=next(iter(directed));loop=[start];v=directed.pop(start)
        while v!=start:loop.append(v);v=directed.pop(v)
        loops.append(loop)
    def area(loop):return abs(sum(a.co.x*b.co.y-b.co.x*a.co.y for a,b in zip(loop,loop[1:]+loop[:1])))
    outer=max(loops,key=area)
    for hole in loops:
        if hole is not outer:bm.faces.new(tuple(reversed(hole)))
    depth=min(v.co.z for v in bm.verts)-.08
    back=[bm.verts.new((v.co.x,v.co.y,depth)) for v in outer]
    for i,a in enumerate(outer):j=(i+1)%len(outer);bm.faces.new((outer[j],a,back[i],back[j]))
    bm.faces.new(tuple(reversed(back)));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bmesh.ops.triangulate(bm,faces=list(bm.faces))
    assert all(e.is_manifold for e in bm.edges),'Backing is not closed'
    assert abs(bm.calc_volume(signed=True))>.005,'Backing lost its volume'
    if bm.calc_volume(signed=True)<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
    bm.to_mesh(mesh);bm.free()
for asset_index,entry in enumerate(sources):
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/entry['file']),load_ui=False,use_scripts=False)
    obj=max((o for o in bpy.data.objects if o.type=='MESH'),key=lambda o:len(o.data.polygons))
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    original=obj.data.copy();world=obj.matrix_world.copy()
    pts=[world@v.co for v in original.vertices]
    bounds=[(min(p[i] for p in pts),max(p[i] for p in pts)) for i in range(3)]
    width=bounds[0][1]-bounds[0][0];height=bounds[2][1]-bounds[2][0];cx=sum(bounds[0])*.5
    for v,p in zip(original.vertices,pts):v.co=((p.x-cx)/width,(p.z-bounds[2][0])/height,.25-(p.y-bounds[1][0])/width)
    original.update()
    samples=[];weights=[]
    for face in original.polygons:
        a,b,c=(original.vertices[i].co for i in face.vertices[:3]);weight=(b-a).cross(c-a).z
        if weight>1e-9:samples.append(tuple(sum((original.vertices[i].co for i in face.vertices),Vector())/len(face.vertices)));weights.append(weight)
    samples=np.asarray(samples);weights=np.asarray(weights);matrix=np.column_stack((samples[:,0],samples[:,1]-.5,np.ones(len(samples))))
    fit=np.linalg.lstsq(matrix*np.sqrt(weights[:,None]),samples[:,2]*np.sqrt(weights),rcond=None)[0]
    residual=samples[:,2]-matrix[:,:2]@fit[:2];order=np.argsort(residual);datum=float(residual[order][np.searchsorted(np.cumsum(weights[order]),weights.sum()*.2)])
    for v in original.vertices:v.co.z-=float(fit[0])*v.co.x+float(fit[1])*(v.co.y-.5)+datum
    original.update()
    reference=bmesh.new();reference.from_mesh(original);reference_tree=BVHTree.FromBMesh(reference);reference.free()
    obj.matrix_world=Matrix.Identity(4)
    for lod,target in enumerate([2400,600,100]):
        obj.modifiers.clear();obj.data=original.copy();obj.data.materials.clear()
        dec=obj.modifiers.new('Bounded rock silhouette','DECIMATE');dec.ratio=min(1,target/sum(len(p.vertices)-2 for p in obj.data.polygons));dec.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=dec.name)
        bm=bmesh.new();bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
        bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-7)
        # Remove disconnected scan scraps; a single mass has one solid boundary.
        unseen=set(bm.verts);components=[]
        while unseen:
            todo=[unseen.pop()];part=set(todo)
            while todo:
                v=todo.pop()
                for e in v.link_edges:
                    other=e.other_vert(v)
                    if other in unseen:unseen.remove(other);part.add(other);todo.append(other)
            components.append(part)
        keep=max(components,key=len);bmesh.ops.delete(bm,geom=[v for v in bm.verts if v not in keep],context='VERTS')
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        # Preserve the scanned front; explicit flat backing below avoids
        # offsets along acute crack normals, which create solidify spikes.
        remove=set()
        for edge in bm.edges:
            faces=sorted((f for f in edge.link_faces if f not in remove),key=lambda f:f.calc_area())
            if len(faces)>2:remove.update(faces[:len(faces)-2])
        if remove:bmesh.ops.delete(bm,geom=list(remove),context='FACES')
        assert all(len(e.link_faces)<=2 for e in bm.edges),'Nonmanifold scan fragment remains'
        bmesh.ops.triangulate(bm,faces=list(bm.faces))
        if sum(f.normal.z*f.calc_area() for f in bm.faces)<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
        deviation=max(reference_tree.find_nearest(v.co)[3] for v in bm.verts)
        assert deviation<[.035,.08,.2][lod],'Prepared geometry deviated from the original rock scan'
        bm.to_mesh(obj.data);bm.free()
        close_back(obj.data)
        limit=[3200,1050,260][lod]
        if len(obj.data.polygons)>limit:
            dec=obj.modifiers.new('Closed silhouette budget','DECIMATE');dec.ratio=limit/len(obj.data.polygons);dec.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=dec.name)
        for p in obj.data.polygons:p.use_smooth=True
        obj.data.update();mesh=obj.data
        lo=[min(v.co[i] for v in mesh.vertices) for i in range(3)];hi=[max(v.co[i] for v in mesh.vertices) for i in range(3)]
        span=max(hi[0]-lo[0],hi[2]-lo[2]);vertical=hi[1]-lo[1];middle=(hi[0]+lo[0])*.5
        for v in mesh.vertices:v.co=((v.co.x-middle)/span,(v.co.y-lo[1])/vertical,v.co.z/span)
        mesh.update()
        blob=bytearray(struct.pack('<III',0x31435257,len(mesh.vertices),len(mesh.polygons)*3))
        for v in mesh.vertices:blob+=struct.pack('<8f',*v.co,*v.normal,v.co.x,v.co.y)
        for p in mesh.polygons:blob+=struct.pack('<3I',*p.vertices)
        target_file=out/f'cliff-{asset_index}-l{lod}.bytes';target_file.write_bytes(blob)
        records.append({'asset':entry['name'],'lod':lod,'file':target_file.relative_to(ROOT).as_posix(),'vertices':len(mesh.vertices),'triangles':len(mesh.polygons),'closed':True,'surface_collision':True,'front_plane_fit':[float(fit[0]),float(fit[1]),datum],'maximum_reference_deviation':deviation,'bytes':len(blob),'sha256':hashlib.sha256(blob).hexdigest()})
        print(json.dumps(records[-1]),flush=True)
    bpy.data.meshes.remove(original)
(out/'sources.json').write_text(json.dumps({'format':'WRC1 little-endian xyz/normal/uv/index','license':'CC0-1.0 geometry; existing generated game textures','interior':'Closed flat backing embedded in terrain; calibrated front preserves scanned ledges','sources':sources,'meshes':records},indent=2))
