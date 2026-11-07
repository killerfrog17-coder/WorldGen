"""Prepare bounded-LOD rock faces from the recorded CC0 geometry sources.
Blender --background --disable-autoexec --python scripts/export-cliff-rock-models.py
The original textures are neither downloaded nor exported.
"""
from pathlib import Path
import bpy,bmesh,json,struct,hashlib
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[1]
sources=json.loads((ROOT/'assets-source/terrain/rocks/polyhaven/sources.json').read_text())['assets']
out=ROOT/'unity/WorldGen.Unity/Assets/WorldGen/Resources/CliffRockV1';out.mkdir(parents=True,exist_ok=True)
records=[]
for asset_index,entry in enumerate(sources):
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/entry['file']),load_ui=False,use_scripts=False)
    obj=max((o for o in bpy.data.objects if o.type=='MESH'),key=lambda o:len(o.data.polygons))
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    original=obj.data.copy();world=obj.matrix_world.copy()
    pts=[world@v.co for v in original.vertices]
    bounds=[(min(p[i] for p in pts),max(p[i] for p in pts)) for i in range(3)]
    width=bounds[0][1]-bounds[0][0];height=bounds[2][1]-bounds[2][0];cx=sum(bounds[0])*.5
    for v,p in zip(original.vertices,pts):v.co=((p.x-cx)/width,(p.z-bounds[2][0])/height,.25-(p.y-bounds[1][0])/width)
    reference=bmesh.new();reference.from_mesh(original);reference_tree=BVHTree.FromBMesh(reference);reference.free()
    obj.matrix_world=Matrix.Identity(4)
    for lod,target in enumerate([3200,1050,260]):
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
        # Keep the scanned surface. A thick solidify modifier on a scan's
        # acute cracks creates enormous spikes; the ground behind the face
        # already provides the solid interior. Boundary edges embed in terrain.
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
        for p in obj.data.polygons:p.use_smooth=True
        obj.data.update();mesh=obj.data
        lo=[min(v.co[i] for v in mesh.vertices) for i in range(3)];hi=[max(v.co[i] for v in mesh.vertices) for i in range(3)]
        span=max(hi[0]-lo[0],hi[2]-lo[2]);vertical=hi[1]-lo[1];middle=(hi[0]+lo[0])*.5
        for v in mesh.vertices:v.co=((v.co.x-middle)/span,(v.co.y-lo[1])/vertical,.25+(v.co.z-hi[2])/span)
        mesh.update()
        blob=bytearray(struct.pack('<III',0x31435257,len(mesh.vertices),len(mesh.polygons)*3))
        for v in mesh.vertices:blob+=struct.pack('<8f',*v.co,*v.normal,v.co.x,v.co.y)
        for p in mesh.polygons:blob+=struct.pack('<3I',*p.vertices)
        target_file=out/f'cliff-{asset_index}-l{lod}.bytes';target_file.write_bytes(blob)
        records.append({'asset':entry['name'],'lod':lod,'file':target_file.relative_to(ROOT).as_posix(),'vertices':len(mesh.vertices),'triangles':len(mesh.polygons),'closed':False,'surface_collision':True,'maximum_reference_deviation':deviation,'bytes':len(blob),'sha256':hashlib.sha256(blob).hexdigest()})
        print(json.dumps(records[-1]),flush=True)
    bpy.data.meshes.remove(original)
(out/'sources.json').write_text(json.dumps({'format':'WRC1 little-endian xyz/normal/uv/index','license':'CC0-1.0 geometry; existing generated game textures','interior':'Embedded terrain supplies the rock interior; front mesh preserves real ledges','sources':sources,'meshes':records},indent=2))
