"""CPU Blender heat skin on welded body; preserve approved rigid attachments."""
import argparse
import json
from pathlib import Path
import sys
import bpy
import bmesh
import numpy as np
from mathutils import Vector
from mathutils.kdtree import KDTree

ap=argparse.ArgumentParser();ap.add_argument('--pilot',type=Path,required=True);ap.add_argument('--model-root',type=Path,required=True);ap.add_argument('--root',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
bpy.ops.wm.open_mainfile(filepath=str(a.pilot.resolve()));s=bpy.context.scene;info=json.loads(s['fauna_stage2'])
rig=bpy.data.objects[info['rig']];body=bpy.data.objects[info['body']];rig.animation_data_clear();rig.data.pose_position='REST';rig.hide_set(False)
meta=json.loads((root/'source-rig.json').read_text());names=[b['name'] for b in meta['bones']]
anatomy=np.load(a.model_root.resolve()/'anatomy.npz');p=anatomy['vertices'];regions=anatomy['region_weights']
ear=regions[:,3:5].max(1)>.5;faces=anatomy['faces'];faces=faces[~np.any(ear[faces],axis=1)]
mesh=bpy.data.meshes.new('Heat skin welded working surface');mesh.from_pydata(p.tolist(),[],faces.tolist());mesh.update()
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
isolated=[v for v in bm.verts if not v.link_faces]
if isolated:bmesh.ops.delete(bm,geom=isolated,context='VERTS')
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
obj=bpy.data.objects.new('Heat skin temporary body',mesh);s.collection.objects.link(obj)
bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
mesh.remesh_voxel_size=.012;mesh.remesh_voxel_adaptivity=0
bpy.ops.object.voxel_remesh();mesh=obj.data
for bone in rig.data.bones:bone.use_deform=not bone.name.startswith(('MotionRoot','Ear_','Muzzle_','Jaw'))
bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
print('Computing automatic heat weights',len(mesh.vertices),'vertices',flush=True)
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
heat=np.zeros((len(mesh.vertices),len(names)))
for v in mesh.vertices:
    for group in v.groups:
        name=obj.vertex_groups[group.group].name
        if name in names:heat[v.index,names.index(name)]=group.weight
assert np.all(heat.sum(1)>1e-6),'Heat solver left unweighted body vertices'
tree=KDTree(len(mesh.vertices))
for v in mesh.vertices:tree.insert(v.co,v.index)
tree.balance();nearest=[tree.find(Vector(point)) for point in p]
distance=np.array([item[2] for item in nearest]);index=np.array([item[1] for item in nearest])
assert distance[~ear].max()<.05,('Heat proxy too far from approved surface',distance[~ear].max())
weights=heat[index];prior=np.load(root/'fitted-skin.npz')['weights']
for bone in ['Head','Ear_L','Ear_R','ForeHoof_L','ForeHoof_R','HindHoof_L','HindHoof_R']:
    column=names.index(bone);mask=prior[:,column]>.999
    weights[mask]=0;weights[mask,column]=1
weights[:,names.index('MotionRoot')]=0
top=np.argpartition(weights,-4,axis=1)[:,-4:];compact=np.take_along_axis(weights,top,axis=1);compact/=compact.sum(1,keepdims=True)
result=np.zeros_like(weights);np.put_along_axis(result,top,compact,axis=1)
assert np.isfinite(result).all() and np.max(abs(result.sum(1)-1))<1e-6
np.savez_compressed(root/'fitted-skin-heat.npz',weights=result.astype(np.float32))
(root/'heat-skin-report.json').write_text(json.dumps(dict(method='Blender CPU bone heat on temporary solid voxel proxy; anatomical rigid attachment overrides/top4',vertices=len(p),heat_vertices=len(mesh.vertices),proxy_max_distance_m=float(distance[~ear].max()),game_launched=False,gpu_inference=False),indent=2))
print('Heat weights ready.',flush=True)
