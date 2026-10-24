"""Small file/control checks for the raw gallery; no art or motion acceptance."""
import argparse,json,runpy,sys,hashlib
from pathlib import Path
import bpy
import numpy as np

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
bpy.ops.wm.open_mainfile(filepath=str(root/'Deer - Four Generated Rigs.blend'))
models=json.loads(bpy.context.scene['deer_four_rigs']);assert len(models)==4
hashes=[];rows=[]
for entry in models:
    r=bpy.data.objects[entry['rig']];b=bpy.data.objects[entry['body']]
    actual=np.array([r.data.bones[n].head_local[:] for n in entry['joint_names']])
    error=float(np.abs(actual-np.array(entry['generated_joint_heads'])).max());assert error<1e-5
    parents=[r.data.bones[n].parent.name if r.data.bones[n].parent else None for n in entry['joint_names']]
    expected=[entry['joint_names'][p] if p>=0 else None for p in entry['parents']];assert parents==expected
    for vertex in b.data.vertices:
        weights=[g.weight for g in vertex.groups if g.weight>1e-7]
        assert len(weights)<=4 and abs(sum(weights)-1)<1e-5
    hashes.append(hashlib.sha256(Path(entry['rig_file']).read_bytes()).hexdigest())
    rows.append({'model':entry['number'],'joints':entry['joints'],'generated_head_max_error_m':error,'triangles':len(b.data.polygons)})
assert len(set(hashes))==4
viewer=runpy.run_path(str(Path(__file__).with_name('deer-four-rigs-viewer.py')))
for index in range(4):
    assert bpy.ops.deer_four.model(number=index)=={'FINISHED'}
    assert sum(not bpy.data.objects[m['body']].hide_get() for m in models)==1
    assert bpy.ops.deer_four.skeleton()=={'FINISHED'} and not bpy.data.objects[models[index]['rig']].hide_get()
    assert bpy.ops.deer_four.skeleton()=={'FINISHED'} and bpy.data.objects[models[index]['rig']].hide_get()
    for label in ('Bind','Idle','Walk','Trot','Run','Eating','Sleep','lay_to_idle'):assert bpy.ops.deer_four.clip(clip=label)=={'FINISHED'}
for _ in range(3):assert bpy.ops.deer_four.view()=={'FINISHED'}
for _ in range(2):assert bpy.ops.deer_four.mode()=={'FINISHED'}
assert bpy.ops.deer_four.species()=={'FINISHED'}
(root/'structural-checks.json').write_text(json.dumps({'models':rows,'distinct_rig_hashes':hashes,'controls_pass':True,'art_checked':False,'natural_motion_approved':False},indent=2))
print('Four independent skeletons and review controls verified; no art acceptance.',flush=True)
