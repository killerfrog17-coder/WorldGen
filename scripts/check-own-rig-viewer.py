"""Reload/verify the unchanged asset and direct native clip controls."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import bpy
import numpy as np

def load_module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod);return mod

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
ap.add_argument('--source',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
builder=load_module('own_builder',Path(__file__).with_name('retarget-mesh2motion-own-deer.py'))
def snapshot():
    rig=bpy.data.objects['AI Deer Rig'];mesh=bpy.data.objects['Locally generated deer']
    images={node.image.name:hashlib.sha256(np.array(node.image.pixels[:],dtype='<f4').tobytes()).hexdigest()
        for node in mesh.data.materials[0].node_tree.nodes if node.type=='TEX_IMAGE'}
    return builder.digest(mesh,rig),images
bpy.ops.wm.open_mainfile(filepath=str(a.source.resolve()));original=snapshot()
bpy.ops.wm.open_mainfile(filepath=str(root/'Deer - Original Rig Motion Review.blend'))
assert snapshot()==original,'Mesh, rig, weights, UV or paint changed on save/reload'
viewer=load_module('own_viewer',Path(__file__).with_name('deer-own-rig-viewer.py'))
s=bpy.context.scene;rig=bpy.data.objects['AI Deer Rig'];records=[]
assert rig.data.pose_position=='REST' and rig.animation_data is None
for bank in ('original','transferred'):
    s.deer_own_review.bank=bank
    for clip,name in viewer.info[bank].items():
        s.deer_own_review.clip=clip;viewer.select(bpy.context)
        assert rig.data.pose_position=='POSE' and rig.animation_data.action.name==name
        assert s.render.fps==viewer.info[bank+'_fps']
        for frame in (s.frame_start,(s.frame_start+s.frame_end)//2,s.frame_end):
            s.frame_set(frame);assert np.isfinite(np.array([b.matrix for b in rig.pose.bones])).all()
        records.append([bank,clip,s.frame_start,s.frame_end])
    s.deer_own_review.clip='Bind pose'
    assert rig.data.pose_position=='REST' and rig.animation_data is None
(root/'viewer-checks.json').write_text(json.dumps(dict(original_asset_exact_after_reload=True,
    image_pixel_hashes=original[1],controls=records,true_bind_pose_verified=True,
    game_launched=False,natural_gait_accepted=False),indent=2))
print('Own rig, skin, rest mesh, UV, texture pixels exact; controls checked:',len(records),flush=True)
