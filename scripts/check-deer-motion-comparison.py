"""Native Blender control/asset checks, not gait or gameplay acceptance."""
import argparse
import importlib.util
import json
from pathlib import Path
import sys
import bpy
import numpy as np

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
bpy.ops.wm.open_mainfile(filepath=str(root/'Deer - AI Motion Comparison.blend'))
script=Path(__file__).with_name('deer-motion-comparison-viewer.py')
spec=importlib.util.spec_from_file_location('motion_review',script)
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
scene=bpy.context.scene;rig=bpy.data.objects['AI Deer Rig'];records=[]
catalogue=json.loads(scene['motion_comparison_catalogue'])
for clip in catalogue:
    scene.motion_review.family=clip['family']
    if clip['variant']=='baseline':scene.motion_review.baseline=clip['action']
    else:scene.motion_review.variant=clip['variant']
    module.select(bpy.context)
    assert rig.animation_data.action.name==clip['action']
    assert scene.render.fps==clip['fps']
    assert scene.frame_end==clip['frames']-1
    matrices=[]
    for frame in (0,scene.frame_end//2,scene.frame_end):
        scene.frame_set(frame)
        matrix=np.array([pb.matrix for pb in rig.pose.bones])
        assert np.isfinite(matrix).all();matrices.append(matrix)
    # Includes visible baseline and experimental clips; no procedural IK layer.
    moving=float(np.max(np.abs(matrices[1]-matrices[0])))
    assert moving>1e-5,(clip['action'],'static action')
    records.append(dict(action=clip['action'],frame_delta=moving))
assert bpy.data.objects['Locally generated deer'].visible_get()
checks=json.loads((root/'native-comparison-checks.json').read_text())
assert checks['mesh_skin_uv_unchanged'] and checks['image_pixels_unchanged']
source=Path(checks.get('source_path',root.parent/'deer-behaviour-20261023/Deer - Game Ready Prototype.blend'))
import hashlib
assert hashlib.sha256(source.read_bytes()).hexdigest()==checks['source_sha256']
(root/'viewer-control-checks.json').write_text(json.dumps(dict(clips=records,
    native_blender=True,source_unchanged=True,game_launched=False),indent=2))
print('Native comparison controls checked:',len(records),flush=True)
