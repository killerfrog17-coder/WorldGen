"""Bounded native format/control checks, not an automatic art acceptance gate."""
import argparse,json,runpy,sys
from pathlib import Path
import bpy,numpy as np

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
bpy.ops.wm.open_mainfile(filepath=str(root/'Animal - Raw Audition.blend'));scene=bpy.context.scene
models=json.loads(scene['deer_four_rigs']);assert len(models)==4
viewer=runpy.run_path(str(Path(__file__).with_name('deer-four-rigs-viewer.py')));checks=[]
for i,entry in enumerate(models):
    bpy.ops.deer_four.model(number=i);rig=bpy.data.objects[entry['rig']];body=bpy.data.objects[entry['body']]
    actual=np.array([rig.data.bones[n].head_local[:] for n in entry['joint_names']]);error=float(np.abs(actual-np.array(entry['generated_joint_heads'])).max());assert error<1e-5
    for v in body.data.vertices:assert len(v.groups)<=4 and abs(sum(g.weight for g in v.groups)-1)<1e-4
    for label in entry['actions']:
        bpy.ops.deer_four.clip(clip=label)
        for t in (0,.5,1):
            scene.frame_set(round(scene.frame_start+(scene.frame_end-scene.frame_start)*t));bpy.context.view_layer.update()
            assert all(np.isfinite(np.array(b.matrix)).all() for b in rig.pose.bones)
    bpy.ops.deer_four.skeleton();assert not rig.hide_get();bpy.ops.deer_four.skeleton();assert rig.hide_get()
    checks.append({'candidate':i+1,'joints':entry['joints'],'triangles':entry['triangles'],'joint_error_m':error,'all_clip_samples_finite':True})
for _ in range(3):bpy.ops.deer_four.view()
for _ in range(2):bpy.ops.deer_four.mode()
bpy.ops.deer_four.species()
(root/'native-checks.json').write_text(json.dumps({'candidates':checks,'controls':True,'art_approved':False,'collision_free_claim':False},indent=2))
print('Raw file, fresh rig positions/weights and controls pass; art review pending.',flush=True)
