"""Reloaded static model, texture variants and review controls; no game launch."""
import argparse
import hashlib
import json
from pathlib import Path
import runpy
import sys
from types import SimpleNamespace
import bpy
import numpy as np
from mathutils import Vector
from mathutils.kdtree import KDTree
from mathutils.bvhtree import BVHTree
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
antler_counts=[]
for antler in info.get('antlers',[]):
    obj=bpy.data.objects[antler['object']];points=np.array([v.co[:] for v in obj.data.vertices]);assert np.isfinite(points).all()
    count=sum(len(p.vertices)-2 for p in obj.data.polygons);assert count==antler['triangles']
    images=[n.image for n in obj.data.materials[0].node_tree.nodes if n.type=='TEX_IMAGE']
    assert len(images)==3 and all(im.packed_file and list(im.size)==[512,512] for im in images)
    antler_counts.append(count)
maximum_visible=total+max(antler_counts,default=0)
assert maximum_visible<=6000,(maximum_visible,'visible triangles')
assert info['triangle_count']==maximum_visible
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
hair_checks={}
if info.get('face_revision',0)>=4:
    anatomical=np.load(root/'anatomy.npz');assert 'eye_regions' in anatomical
    assert all((anatomical['eye_regions'][:,i]>.9).sum()>=48 for i in range(3))
    # Surface masks, not merely another dark texture near the aperture.
    bake=json.loads((root/'bake-report.json').read_text())
    assert bake['mask_pixels']['lid_bare_skin']>100
    assert bake['mask_pixels']['eye_short_fur']>100
    assert info['eye_hairs']==dict(upper_lashes=16,brow_whiskers=6)
    with bpy.data.libraries.load(str(root/'Deer - Stage1 Face and Species Review.blend'),link=False) as (source,destination):
        destination.meshes=[name for name in source.meshes if name.startswith('Curved eye ')]
    old_eyes=destination.meshes
    for name in info['eyes']:
        obj=bpy.data.objects[name];old=next(x for x in old_eyes if x.name.startswith('Curved eye '+name.split()[-1]))
        assert np.array_equal(np.array([v.co[:] for v in obj.data.vertices]),np.array([v.co[:] for v in old.vertices]))
    surface=BVHTree.FromPolygons([v.co for v in body.data.vertices],[p.vertices for p in body.data.polygons],all_triangles=True)
    hairs=np.array([v.co[:] for v in bpy.data.objects[info['whiskers']].data.vertices])
    landmarks=json.loads((root/'landmarks.json').read_text());clearances=[];root_distances=[]
    for side,label in enumerate(('right','left')):
        normal=np.array(landmarks['eye_'+label]['normal'])
        for k in range(11):
            first=48+side*55+k*5;root_point=(hairs[first]+hairs[first+1])*.5
            nearest,_,_,distance=surface.find_nearest(Vector(root_point));assert distance<.0005
            root_distances.append(distance)
            for point in ((hairs[first+2]+hairs[first+3])*.5,hairs[first+4]):
                hit,_,_,_=surface.ray_cast(Vector(point+normal*.10),Vector(-normal),.20)
                if hit is not None:
                    clearance=float((point-np.array(hit))@normal)
                    assert clearance>0,(label,k,clearance,'buried eye hair')
                    clearances.append(clearance)
    hair_checks=dict(max_root_distance_m=max(root_distances),min_mid_tip_clearance_m=min(clearances),eyeballs_unchanged=True)
for coat in info['coats']:
    material=bpy.data.materials[coat['material']]
    images=[n.image for n in material.node_tree.nodes if n.type=='TEX_IMAGE']
    assert len(images)==3 and all(im.packed_file and list(im.size)==[2048,2048] for im in images)
    maps.append([im.name for im in images])
viewer=runpy.run_path(str(Path(__file__).with_name('fauna-stage1-viewer.py')))
assert bpy.ops.fauna_review.model()=={'FINISHED'}
seen=set()
sexes=set();racks=set()
for _ in range(40):
    assert bpy.ops.fauna_review.species()=={'FINISHED'}
    seen.add(bpy.data.objects[info['body']].data.materials[0].name)
    if info.get('antlers'):
        active=[x for x in info['antlers'] if not bpy.data.objects[x['object']].hide_render]
        sexes.add(viewer['state']['sex'])
        assert len(active)==(1 if viewer['state']['sex']=='Male' else 0)
        for x in active:racks.add(x['label'])
assert len(seen)==len(info['coats'])
if info.get('antlers'):assert sexes=={'Male','Female'} and len(racks)==len(info['antlers'])
for _ in range(3):assert bpy.ops.fauna_review.view()=={'FINISHED'}
assert bpy.ops.fauna_review.view(rear=True)=={'FINISHED'}
assert bpy.ops.fauna_review.view(front=True)=={'FINISHED'}
assert bpy.ops.fauna_review.free_orbit()=={'FINISHED'}
assert viewer['state']['view']==5
region=next(area.spaces.active.region_3d for window in bpy.context.window_manager.windows
            for area in window.screen.areas if area.type=='VIEW_3D')
before=region.view_rotation.copy();drag=SimpleNamespace(region=region,previous=(100,100))
context=SimpleNamespace(area=SimpleNamespace(tag_redraw=lambda:None))
event=SimpleNamespace(type='MOUSEMOVE',value='NOTHING',mouse_x=110,mouse_y=103)
assert viewer['F1_OT_free_orbit'].modal(drag,context,event)=={'RUNNING_MODAL'}
assert .01<before.rotation_difference(region.view_rotation).angle<.15,'Mouse orbit quantized or stalled'
assert viewer['F1_OT_free_orbit'].modal(drag,context,SimpleNamespace(type='MIDDLEMOUSE',value='RELEASE'))=={'FINISHED'}
report=dict(triangles=maximum_visible,antlerless_triangles=total,antler_library_triangles=antler_counts,eye_hair_checks=hair_checks,finite=True,unrigged=True,coats=len(info['coats']),packed_maps=maps,
    symmetry=sym,local_morphs=traits,nonfacial_body_distance_m=body_distance,review_file=a.blend_name,review_operators_pass=True,stage2_approved=False,owner_art_approved=False,
    generated_shape_sha256=hashlib.sha256((root/'deer-raw.glb').read_bytes()).hexdigest(),game_launch=False)
(root/'stage1-validation.json').write_text(json.dumps(report,indent=2));print('Stage1 model/coat/controls validation passed.',flush=True)
