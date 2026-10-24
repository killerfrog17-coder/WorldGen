"""Native bind-art/skin/pose/preview checks, offline and without game launches."""
import argparse
import hashlib
import json
from pathlib import Path
import runpy
import sys
import bpy
import numpy as np

ap=argparse.ArgumentParser();ap.add_argument('--model',type=Path,required=True);ap.add_argument('--root',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve();model=a.model.resolve()
bpy.ops.wm.open_mainfile(filepath=str(root/'Deer - Stage2 Animation Review.blend'))
s=bpy.context.scene;info=json.loads(s['fauna_stage2']);rig=bpy.data.objects[info['rig']];body=bpy.data.objects[info['body']]
assert len(rig.data.bones)==34 and len(info['actions'])==16
assert hashlib.sha256(model.read_bytes()).hexdigest()==info['model_sha256']
names=[info['body'],*info['eyes'],info['whiskers'],*[x['object'] for x in info['antlers']]]
with bpy.data.libraries.load(str(model),link=False) as (available,loaded):loaded.objects=list(names)
original=dict(zip(names,loaded.objects))
def coords(obj):return np.array([v.co[:] for v in obj.data.vertices])
def packed_images(obj):
    return sorted(hashlib.sha256(n.image.packed_file.data).hexdigest() for m in obj.data.materials if m and m.use_nodes
                  for n in m.node_tree.nodes if n.type=='TEX_IMAGE')
skin_checks={}
for name in names:
    obj=bpy.data.objects[name];prior=original[name]
    assert np.array_equal(coords(obj),coords(prior)),(name,'bind geometry')
    assert [tuple(p.vertices) for p in obj.data.polygons]==[tuple(p.vertices) for p in prior.data.polygons]
    assert np.array_equal(np.array([v.uv[:] for v in obj.data.uv_layers.active.data]),np.array([v.uv[:] for v in prior.data.uv_layers.active.data])) if obj.data.uv_layers.active else not prior.data.uv_layers.active
    assert packed_images(obj)==packed_images(prior),(name,'packed texture changed')
    counts=[];error=0
    for v in obj.data.vertices:
        entries=[g for g in v.groups if obj.vertex_groups[g.group].name in rig.data.bones and g.weight>1e-7]
        counts.append(len(entries));error=max(error,abs(sum(g.weight for g in entries)-1))
    assert max(counts)<=4 and min(counts)>0 and error<1e-5
    assert obj.modifiers and any(m.type=='ARMATURE' and m.object==rig and not m.use_deform_preserve_volume for m in obj.modifiers)
    skin_checks[name]=dict(max_weights=max(counts),normalization_error=error)
assert set(body.data.shape_keys.key_blocks.keys())==set(original[info['body']].data.shape_keys.key_blocks.keys())
for key in body.data.shape_keys.key_blocks:
    assert np.array_equal(np.array([v.co[:] for v in key.data]),np.array([v.co[:] for v in original[info['body']].data.shape_keys.key_blocks[key.name].data]))
rig.data.pose_position='POSE';dg=bpy.context.evaluated_depsgraph_get();pose_checks={}
head_ids=np.array([v.index for v in body.data.vertices if v.co.y>.80 and
    any(body.vertex_groups[g.group].name=='Head' and g.weight>.99 for g in v.groups)],np.int32)
for label,action_name in info['actions'].items():
    action=bpy.data.actions[action_name];rig.animation_data_clear();rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0]
    first,last=action.frame_range;lowest=100;maximum=0;samples=0;head_lowest=100;head_highest=-100
    frames=sorted(set([float(first),float(last),*np.arange(first,last+.01,5),*np.arange(first+.5,last,17),*np.arange(first+.25,last,23)]))
    if info.get('pose_corrections') and label in ('Walk','Trot','Run'):
        frames=sorted(set([*frames,*np.arange(first+.125,last,.25),*np.arange(first+.0625,last,.125)]))
    for frame in frames:
        s.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()
        assert all(np.isfinite(np.array(b.matrix)).all() for b in rig.pose.bones)
        if info.get('pose_corrections') and label in ('Walk','Trot','Run'):
            pelvis=rig.pose.bones['Pelvis'].matrix.translation.x
            for name in ('Elbow_L','Elbow_R','Wrist_L','Wrist_R','Knee_L','Knee_R','Hock_L','Hock_R',
                         'ForeHoof_L','ForeHoof_R','HindHoof_L','HindHoof_R'):
                rest=rig.data.bones[name].matrix_local.translation.x-rig.data.bones['Pelvis'].matrix_local.translation.x
                assert (rig.pose.bones[name].matrix.translation.x-pelvis)*rest>0,(label,frame,name,'crossed leg track')
        evaluated=body.evaluated_get(dg);mesh=evaluated.to_mesh();points=np.array([v.co[:] for v in mesh.vertices]);evaluated.to_mesh_clear()
        assert np.isfinite(points).all() and np.abs(points).max()<5,(label,frame,'unbounded pose')
        lowest=min(lowest,float(points[:,2].min()));maximum=max(maximum,float(np.abs(points).max()));samples+=1
        if label=='Eating':
            value=float(points[head_ids,2].min());head_lowest=min(head_lowest,value);head_highest=max(head_highest,value)
    if label in ('Idle','Walk','Run','Trot','Eating','Behaviour showcase','Locomotion demo'):
        assert lowest>-.008,(label,lowest,'floor penetration')
    pose_checks[label]=dict(samples=samples,lowest=lowest,max_coordinate=maximum)
    if label=='Eating':
        assert .020<head_lowest<=head_highest<.035,(head_lowest,head_highest,'grazing muzzle clearance')
        pose_checks[label].update(muzzle_min=head_lowest,muzzle_max=head_highest)
viewer=runpy.run_path(str(Path(__file__).with_name('fauna-stage2-viewer.py')))
assert bpy.ops.fauna_review.model()=={'FINISHED'}
for _ in range(40):assert bpy.ops.fauna_review.species()=={'FINISHED'}
for _ in range(3):assert bpy.ops.fauna_review.view()=={'FINISHED'}
assert bpy.ops.fauna_review.free_orbit()=={'FINISHED'}
assert bpy.ops.fauna_motion.mode()=={'FINISHED'} and viewer['state']['clip']=='Locomotion demo'
assert bpy.ops.fauna_motion.mode()=={'FINISHED'} and viewer['state']['clip']=='Behaviour showcase'
for label in info['actions']:
    assert bpy.ops.fauna_motion.clip(clip=label)=={'FINISHED'}
    assert rig.animation_data.action.name==info['actions'][label]
report=dict(bind_art_exact=True,packed_images_exact=True,shape_keys_exact=True,skin=skin_checks,poses=pose_checks,
    controls_pass=True,bones=34,actions=len(info['actions']),gpu_inference=False,game_launched=False,
    owner_animation_approved=False,limits='Selected native poses/half-frames; not universal intersection, foot-slide, natural gait or game acceptance')
(root/'native-validation.json').write_text(json.dumps(report,indent=2));print('Stage2 bind/skin/poses/controls checks passed.',flush=True)
