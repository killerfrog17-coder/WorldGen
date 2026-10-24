"""Automatic raw mesh budgeting/axis normalization for neural rig auditions."""
import argparse,json,sys
from pathlib import Path
import bpy,numpy as np

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True);ap.add_argument('--triangles',type=int,required=True);ap.add_argument('--voxel-resolution',type=int,default=0)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);a.root=a.root.resolve();out=a.root/'model.glb';assert not out.exists()
plan_path=Path(__file__).resolve().parent.parent/'docs/fauna/asset-plan.json'
entry=next((x for x in json.loads(plan_path.read_text())['rows'] if x['slug']==a.root.parent.name),{})
flightless=entry.get('animal') in {'Ostrich','Emu','Cassowary','Kiwi','Penguin'}
winged=entry.get('family') in {'bird','bat'} and not flightless
cleanup=a.root/'mesh-component-cleanup.json';cleanup_data=json.loads(cleanup.read_text()) if cleanup.exists() else {}
source=Path(cleanup_data['prepared_source']) if cleanup_data else a.root/'raw.glb'
voxel_resolution=a.voxel_resolution or (192 if entry.get('family')=='bird' and cleanup_data.get('original_components',0)>32 else 0)
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(source))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert objects
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
if len(objects)>1:bpy.ops.object.join()
body=bpy.context.object;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
p=np.array([v.co[:] for v in body.data.vertices]);low=p[:,2].min();height=np.ptp(p[:,2]);assert height>1e-5
p[:,2]-=low;p*=1.65/height
torso=p[(p[:,2]>.25*1.65)&(p[:,2]<.65*1.65)];upper=p[p[:,2]>.78*1.65]
_,axes=np.linalg.eigh(np.cov(torso[:,:2].T));f=axes[:,0 if winged else -1]
if (upper[:,:2].mean(0)-torso[:,:2].mean(0))@f<0:f=-f
right=np.array([f[1],-f[0]]);rotation=np.stack([right,f],axis=1);p[:,:2]=p[:,:2]@rotation
offset=(np.quantile(p[:,:2],.02,axis=0)+np.quantile(p[:,:2],.98,axis=0))*.5;p[:,:2]-=offset
body.data.vertices.foreach_set('co',p.astype(np.float32).ravel());body.data.update()
if voxel_resolution:
    body.data.remesh_voxel_size=1.65/voxel_resolution;body.data.use_remesh_preserve_volume=True
    bpy.ops.object.voxel_remesh()
body.name='Own generated raw animal';tri=body.modifiers.new('Triangulate','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
dec=body.modifiers.new('Raw audition triangle budget','DECIMATE');dec.ratio=min(1,a.triangles/len(body.data.polygons));dec.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=dec.name)
for polygon in body.data.polygons:polygon.use_smooth=True
body.data.validate(clean_customdata=False);body.data.update()
assert len(body.data.polygons)<=a.triangles*1.05+4,'Reduction cannot meet triangle budget; do not rig a collapsed/fragment-limited result'
body.data.materials.clear()
bpy.ops.export_scene.gltf(filepath=str(out),export_format='GLB',use_selection=True,export_materials='NONE',export_animations=False)
(a.root/'mesh-preparation.json').write_text(json.dumps({'preparation_version':2,'triangles':len(body.data.polygons),'budget':a.triangles,'canonical_height_m':1.65,'orientation':'PCA body axis, high head/ear end +Y; needs visual review','family_axis_method':'minor axis for spread wings' if winged else 'major body axis','manual_sculpt':False,'automatic_voxel_resolution':voxel_resolution,'component_cleanup':bool(cleanup_data),'axis_rotation':rotation.tolist(),'source_height':float(height)},indent=2))
print('Prepared',out,len(body.data.polygons),'triangles',flush=True)
