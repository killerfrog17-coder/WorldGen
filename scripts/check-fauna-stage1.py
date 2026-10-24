"""Reloaded static model, texture variants and review controls; no game launch."""
import argparse
import hashlib
import json
from pathlib import Path
import runpy
import sys
import bpy
import numpy as np
from mathutils import Vector
from mathutils.kdtree import KDTree
ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
ap.add_argument('--blend-name',default='Deer - Stage1 Model Review.blend')
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
bpy.ops.wm.open_mainfile(filepath=str(root/a.blend_name))
s=bpy.context.scene;info=json.loads(s['fauna_stage1'])
assert not any(o.type=='ARMATURE' for o in s.objects),'Stage 1 must remain unrigged'
names=[info['body'],*info['eyes'],info['whiskers']];total=0;sym={}
for name in names:
    obj=bpy.data.objects[name];p=np.array([v.co[:] for v in obj.data.vertices]);assert np.isfinite(p).all()
    total+=sum(len(poly.vertices)-2 for poly in obj.data.polygons)
    if name==info['body']:
        kd=KDTree(len(p))
        for i,point in enumerate(p):kd.insert(Vector(point),i)
        kd.balance();maximum=max(kd.find(Vector((-v[0],v[1],v[2])))[2] for v in p)
        sym['max_mirrored_vertex_distance_m']=maximum
        assert maximum<.006,'Unexpected bilateral shape mismatch'
        uv=np.array([x.uv[:] for x in obj.data.uv_layers.active.data]);assert np.isfinite(uv).all()
assert total<=6000,(total,'triangles')
body=bpy.data.objects[info['body']]
traits={}
neutral=np.array([v.co[:] for v in body.data.vertices])
source=np.load(root/'body-low.npz')['vertices'];lower=neutral[neutral[:,2]<1.30]
source_kd=KDTree(len(source))
for i,p in enumerate(source):source_kd.insert(Vector(p),i)
source_kd.balance()
body_distance=max(source_kd.find(Vector(p))[2] for p in lower)
assert body_distance<1e-6,'Non-facial neutral body changed'
if body.data.shape_keys:
    keys=body.data.shape_keys.key_blocks
    base=np.array([v.co[:] for v in keys['Basis'].data])
    for name in ('Compact ears','Large ears','Short tail','Long tail'):
        points=np.array([v.co[:] for v in keys[name].data]);delta=np.linalg.norm(points-base,axis=1)
        assert np.isfinite(points).all() and 0<delta.max()<.12,(name,delta.max())
        # Ear changes stay above the face; tail changes stay at the rear centre.
        altered=base[delta>1e-7]
        if 'ears' in name:assert altered[:,2].min()>1.40
        else:assert altered[:,1].max()<-.63 and np.abs(altered[:,0]).max()<.065
        traits[name]=dict(max_displacement_m=float(delta.max()),vertices=int((delta>1e-7).sum()))
assert abs(bpy.data.objects[info['eyes'][0]].data.vertices[0].co.z-bpy.data.objects[info['eyes'][1]].data.vertices[0].co.z)<1e-5
maps=[]
for coat in info['coats']:
    material=bpy.data.materials[coat['material']]
    images=[n.image for n in material.node_tree.nodes if n.type=='TEX_IMAGE']
    assert len(images)==3 and all(im.packed_file and list(im.size)==[2048,2048] for im in images)
    maps.append([im.name for im in images])
viewer=runpy.run_path(str(Path(__file__).with_name('fauna-stage1-viewer.py')))
assert bpy.ops.fauna_review.model()=={'FINISHED'}
seen=set()
for _ in range(40):
    assert bpy.ops.fauna_review.species()=={'FINISHED'}
    seen.add(bpy.data.objects[info['body']].data.materials[0].name)
assert len(seen)==len(info['coats'])
for _ in range(3):assert bpy.ops.fauna_review.view()=={'FINISHED'}
assert bpy.ops.fauna_review.view(rear=True)=={'FINISHED'}
assert bpy.ops.fauna_review.view(front=True)=={'FINISHED'}
report=dict(triangles=total,finite=True,unrigged=True,coats=len(info['coats']),packed_maps=maps,
    symmetry=sym,local_morphs=traits,nonfacial_body_distance_m=body_distance,review_file=a.blend_name,review_operators_pass=True,stage2_approved=False,owner_art_approved=False,
    generated_shape_sha256=hashlib.sha256((root/'deer-raw.glb').read_bytes()).hexdigest(),game_launch=False)
(root/'stage1-validation.json').write_text(json.dumps(report,indent=2));print('Stage1 model/coat/controls validation passed.',flush=True)
