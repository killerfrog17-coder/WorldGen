"""Export only approved neutral body geometry for independent rig inference."""
import argparse,json,sys,hashlib
from pathlib import Path
import bpy

ap=argparse.ArgumentParser();ap.add_argument('--blend',type=Path,required=True);ap.add_argument('--root',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);a.root.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(a.blend.resolve()))
s=bpy.context.scene;info=json.loads(s['fauna_stage1']);body=bpy.data.objects[info['body']]
print(json.dumps(info,indent=2),flush=True)
# Explicit base coordinates: no evaluated phenotype, modifiers or old weights.
mesh=bpy.data.meshes.new('Approved neutral rig input')
mesh.from_pydata([v.co[:] for v in body.data.vertices],[],[list(p.vertices) for p in body.data.polygons]);mesh.update()
obj=bpy.data.objects.new('Approved deer body only',mesh);s.collection.objects.link(obj)
bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
path=a.root/'approved-body.glb';assert not path.exists()
bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_materials='NONE',export_animations=False)
(a.root/'approved-input.json').write_text(json.dumps({'blend':str(a.blend.resolve()),'blend_sha256':hashlib.sha256(a.blend.read_bytes()).hexdigest(),'mesh':str(path),'mesh_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'metadata':info,'vertices':len(mesh.vertices),'triangles':sum(len(p.vertices)-2 for p in mesh.polygons)},indent=2))
print('Approved neutral body exported without changing original blend.',flush=True)
