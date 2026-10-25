"""Put the owner's permitted accepted-deer fallback in pronghorn candidate4."""
import hashlib,json,shutil,sys
from pathlib import Path
import bpy,numpy as np
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-overnight-20261024/pronghorn';folder=root/'candidate-4';deer=repo/'artifacts/fauna-assets/deer'
choices=json.loads((repo/'docs/fauna/OWNER_SELECTIONS.json').read_text());assert 'pronghorn' not in choices['animals']
protected={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in deer.iterdir() if p.is_file()}
archive=root/'rejected-new-head-turn-candidate4';assert not archive.exists();archive.mkdir()
manifest=[]
for p in list(folder.iterdir()):
    assert p.is_file() and not p.is_symlink();manifest.append({'name':p.name,'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()});p.rename(archive/p.name)
(archive/'manifest.json').write_text(json.dumps(manifest,indent=2))
shutil.copy2(deer/'generated-rig.npz',folder/'generated-rig.npz');data=np.load(folder/'generated-rig.npz')
bpy.ops.wm.read_factory_settings(use_empty=True);mesh=bpy.data.meshes.new('Owner accepted own deer fallback');mesh.from_pydata(data['source_vertices'].tolist(),[],data['source_faces'].tolist());mesh.update()
obj=bpy.data.objects.new(mesh.name,mesh);bpy.context.scene.collection.objects.link(obj);obj.select_set(True);bpy.context.view_layer.objects.active=obj
bpy.ops.export_scene.gltf(filepath=str(folder/'model.glb'),export_format='GLB',use_selection=True,export_materials='NONE',export_animations=False)
meta={'method':'Owner-permitted accepted deer model reuse','mesh':str(folder/'model.glb'),'source_asset':'artifacts/fauna-assets/deer','source_rig_sha256':protected['generated-rig.npz'],'owner_permission':'Pronghorn: try straight reference again or just use deer model.','joints':len(data['parents']),'no_stock_geometry':True}
(folder/'generated-rig.json').write_text(json.dumps(meta,indent=2));(folder/'mesh-preparation.json').write_text(json.dumps({'triangles':len(data['source_faces']),'budget':6000,'canonical_height_m':1.65,'family_axis_method':'accepted deer axis','manual_sculpt':False,'owner_permitted_fallback':True},indent=2))
(root/'review-rebuild.json').write_text(json.dumps({'reason':'Owner permitted deer fallback; fresh pronghorn candidates still turn head. Candidate4 reuses accepted own deer mesh/rig without painting.','art_approved':False},indent=2))
assert all(hashlib.sha256((deer/n).read_bytes()).hexdigest()==h for n,h in protected.items())
print('Candidate4 accepted own deer fallback; protected deer unchanged')
