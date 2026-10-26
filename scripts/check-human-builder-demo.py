"""Check the saved comparison and render its initial side-by-side views."""
import hashlib
import importlib.util
import json
import math
from pathlib import Path

import bpy

REPO=Path(__file__).resolve().parent.parent
ROOT=REPO/'artifacts/human-builder-demo'
spec=importlib.util.spec_from_file_location('stage',REPO/'scripts/assemble-human-demo.py')
stage=importlib.util.module_from_spec(spec);spec.loader.exec_module(stage)
native=ROOT/'Human - Character Builder Comparison.blend'
bpy.ops.wm.open_mainfile(filepath=str(native))
report={'native_sha256':hashlib.sha256(native.read_bytes()).hexdigest(),'characters':[],'missing_maps':[]}
for builder in ['MPFB','UMA']:
    for code in 'ABCDEF':
        collection=bpy.data.collections[builder+'_'+code]
        meshes=[o for o in collection.objects if o.type=='MESH']
        vertices=sum(len(o.data.vertices) for o in meshes)
        assert vertices>10000,(builder,code,vertices)
        assert all(math.isfinite(c) for o in meshes for v in o.data.vertices for c in v.co)
        report['characters'].append({'builder':builder,'variant':code,'vertices':vertices,'meshes':len(meshes),'head_center':list(collection['head_center'])})
for image in bpy.data.images:
    if image.source=='FILE' and image.filepath and not image.packed_file:report['missing_maps'].append(image.filepath)
assert not report['missing_maps'],report['missing_maps']
scene=bpy.context.scene
scene.render.resolution_x=1280;scene.render.resolution_y=900
for view in ['face','profile','body']:
    stage.select('MPFB','A',view,compare=True)
    scene.render.filepath=str(ROOT/'renders'/('comparison-'+view+'.png'))
    bpy.ops.render.render(write_still=True)
(ROOT/'native-check.json').write_text(json.dumps(report,indent=2))
print('HUMAN_DEMO_NATIVE_CHECK_PASS',len(report['characters']),flush=True)
