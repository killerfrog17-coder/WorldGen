"""Reloaded native Blender geometry/contact/control checks, CPU only."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import bpy
import numpy as np

sys.path.insert(0,str(Path(__file__).parent))
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,Path(__file__).with_name(path))
    m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
builder=module('own_builder','retarget-mesh2motion-own-deer.py')
ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True);ap.add_argument('--source',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
def snapshot():
    rig=bpy.data.objects['AI Deer Rig'];mesh=bpy.data.objects['Locally generated deer']
    images={node.image.name:hashlib.sha256(np.array(node.image.pixels[:],dtype='<f4').tobytes()).hexdigest()
        for node in mesh.data.materials[0].node_tree.nodes if node.type=='TEX_IMAGE'}
    actions={}
    for action in bpy.data.actions:
        h=hashlib.sha256()
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        h.update((curve.data_path+str(curve.array_index)).encode())
                        h.update(np.array([key.co for key in curve.keyframe_points],dtype='<f4').tobytes())
        actions[action.name]=h.hexdigest()
    return builder.digest(mesh,rig),images,actions
bpy.ops.wm.open_mainfile(filepath=str(a.source.resolve()));old=snapshot()
bpy.ops.wm.open_mainfile(filepath=str(root/'Deer - Eating Correction Review.blend'));new=snapshot()
assert old[:2]==new[:2]
assert all(new[2][name]==digest for name,digest in old[2].items())
viewer=module('eating_viewer','deer-own-rig-viewer.py');s=bpy.context.scene
rig=bpy.data.objects['AI Deer Rig'];mesh=bpy.data.objects['Locally generated deer']
assert s.deer_own_review.clip=='Eating'
assert rig.animation_data.action.name=='Corrected | Eating'
names={g.index:g.name for g in mesh.vertex_groups}
def ids_for(wanted):
    return [v.index for v in mesh.data.vertices if sum(g.weight for g in v.groups if names[g.group] in wanted)>.65]
face=ids_for({'Head','Jaw','Muzzle_L','Muzzle_R'})
feet={n:ids_for({n}) for n in ('ForeHoof_L','ForeHoof_R','HindHoof_L','HindHoof_R')}
reports={}
for before in (True,False):
    s.deer_own_review.raw_eating=before;viewer.select(bpy.context)
    expected=viewer.info['uncorrected_eating'] if before else 'Corrected | Eating'
    assert rig.animation_data.action.name==expected
    rows=[];previous=None;max_step=0;first=None;last=None;points0=None;drift=0
    for f in np.arange(s.frame_start,s.frame_end+.01,.5):
        s.frame_set(int(f),subframe=float(f-int(f)))
        ev=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
        points=np.empty(len(ev.data.vertices)*3,np.float32);ev.data.vertices.foreach_get('co',points)
        points=points.reshape(-1,3);assert np.isfinite(points).all()
        if previous is not None:max_step=max(max_step,float(np.linalg.norm(points-previous,axis=1).max()))
        previous=points.copy()
        if first is None:first=points.copy();points0={n:points[ids].copy() for n,ids in feet.items()}
        last=points.copy()
        for n,ids in feet.items():drift=max(drift,float(np.linalg.norm(points[ids]-points0[n],axis=1).max()))
        rows.append(dict(frame=float(f),muzzle_min=float(points[face,2].min()),lowest=float(points[:,2].min()),
            hoof_min={n:float(points[ids,2].min()) for n,ids in feet.items()}))
    if not before:
        assert min(r['lowest'] for r in rows)>-.001
        assert all(.022<r['muzzle_min']<.028 for r in rows)
        assert drift<.001
        assert max_step<.025
    reports['before' if before else 'corrected']=dict(samples=len(rows),muzzle_range=[min(r['muzzle_min'] for r in rows),max(r['muzzle_min'] for r in rows)],
        min_surface_height=min(r['lowest'] for r in rows),max_half_frame_vertex_step=max_step,
        max_hoof_surface_drift=drift,loop_surface_gap=float(np.linalg.norm(last-first,axis=1).max()),frames=rows)
s.deer_own_review.bank='original'
assert bpy.ops.deer_own.clip(clip='Eating')=={'FINISHED'}
assert s.deer_own_review.bank=='transferred' and rig.animation_data.action.name=='Corrected | Eating'
(root/'native-validation.json').write_text(json.dumps(dict(geometry_rig_weights_uv_images_exact=True,
    all_source_actions_unchanged=True,before_after_controls_verified=True,reports=reports,
    game_launched=False,owner_art_approval=False),indent=2))
print('Reloaded eating correction/contact and Before fix controls verified.',flush=True)
