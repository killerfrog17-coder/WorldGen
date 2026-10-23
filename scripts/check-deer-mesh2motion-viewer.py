"""Native control wiring and finite animation check; no gameplay acceptance."""
import argparse
import importlib.util
import json
from pathlib import Path
import sys
import bpy
import numpy as np

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
bpy.ops.wm.open_mainfile(filepath=str(root/'Deer - Mesh2Motion Review.blend'))
script=Path(__file__).with_name('deer-mesh2motion-viewer.py')
spec=importlib.util.spec_from_file_location('m2m_viewer',script)
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
scene=bpy.context.scene;records=[]
for mode,info in module.variants.items():
    scene.m2m_review.mode=mode
    rig=bpy.data.objects[info['rig']]
    for clip,name in info['actions'].items():
        scene.m2m_review.clip=clip;module.select(bpy.context)
        assert rig.animation_data.action.name==name
        visible=[o.name for o in scene.objects if o.type=='MESH' and o.visible_get()]
        assert set(visible)=={info['mesh'],'Review ground'},visible
        poses=[]
        for frame in (scene.frame_start,(scene.frame_start+scene.frame_end)//2,scene.frame_end):
            scene.frame_set(frame);m=np.array([b.matrix for b in rig.pose.bones])
            assert np.isfinite(m).all();poses.append(m)
        change=float(np.max(np.abs(poses[1]-poses[0])))
        if clip!='Rest_Pose':assert change>1e-5,(mode,clip,'static')
        records.append(dict(mode=mode,clip=clip,frames=[scene.frame_start,scene.frame_end],delta=change))
(root/'viewer-checks.json').write_text(json.dumps({'controls':records,'native_blender':True,
    'game_launched':False,'naturalness_accepted':False},indent=2))
print('Mesh2Motion native controls checked:',len(records),flush=True)
