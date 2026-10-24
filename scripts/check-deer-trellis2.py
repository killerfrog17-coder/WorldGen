"""Reload and validate the static native-material review and its A/B controls."""
import argparse
import hashlib
import json
from pathlib import Path
import runpy
import sys
import bpy
import numpy as np

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
blend=root/'Deer - TRELLIS2 Material Review.blend'
bpy.ops.wm.open_mainfile(filepath=str(blend));s=bpy.context.scene
info=json.loads(s['trellis_review']);new=bpy.data.objects[info['new'][0]]
vv=np.array([v.co for v in new.data.vertices]);uv=np.array([v.uv[:] for v in new.data.uv_layers.active.data])
assert np.isfinite(vv).all() and np.isfinite(uv).all()
assert len(new.data.polygons)==13187
mat=new.data.materials[0];assert mat.use_nodes
images=[n.image for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
assert len(images)==2 and all(list(im.size)==[2048,2048] and im.packed_file for im in images)
bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
assert bsdf.inputs['Base Color'].is_linked and bsdf.inputs['Roughness'].is_linked
runpy.run_path(str(Path(__file__).with_name('deer-trellis2-viewer.py')))
assert bpy.ops.deer_trellis.show(previous=True)=={'FINISHED'}
assert new.hide_get() and not bpy.data.objects[info['previous']].hide_get()
assert bpy.ops.deer_trellis.show(previous=False)=={'FINISHED'}
assert not new.hide_get() and bpy.data.objects[info['previous']].hide_get()
area=next(ar for ar in bpy.context.screen.areas if ar.type=='VIEW_3D')
with bpy.context.temp_override(area=area):
    for direction in range(3):assert bpy.ops.deer_trellis.view(direction=direction)=={'FINISHED'}
    for clay in (True,False):assert bpy.ops.deer_trellis.material(clay=clay)=={'FINISHED'}
    assert area.spaces.active.shading.type=='MATERIAL'
result=dict(reloaded=True,triangles=len(new.data.polygons),finite_geometry_uv=True,
    packed_native_material_maps=True,roughness_connected=True,ab_operators=True,
    view_material_operators=True,rigging=False,animation=False,game_launch=False,
    owner_art_acceptance=False,glb_sha256=hashlib.sha256((root/'Deer-TRELLIS2-native.glb').read_bytes()).hexdigest())
(root/'native-validation.json').write_text(json.dumps(result,indent=2))
print('Native TRELLIS2 static review validation passed.',flush=True)
