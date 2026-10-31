"""Mechanical bank checks complement the separate moving/rendered visual review."""
import hashlib
import json
import math
from pathlib import Path
import sys
import argparse
import bpy
from mathutils import Vector

REPO=Path(__file__).resolve().parent.parent
ROOT=REPO/'artifacts/kimodo-human-bank'
p=argparse.ArgumentParser();p.add_argument('--root');p.add_argument('--variant',default='B');opts=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
if opts.root:ROOT=REPO/opts.root
bpy.ops.wm.open_mainfile(filepath=str(ROOT/f'Kimodo bank {opts.variant}.blend'))
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
clips=json.loads(rig['kimodo_bank_clips'])
records=[]
def bag(action):return action.layers[0].strips[0].channelbag(action.slots[0])
for clip in clips:
    action=bpy.data.actions[clip['action']];rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
    for bone in rig.pose.bones:bone.location=(0,0,0);bone.scale=(1,1,1)
    previous={};worst=0;worst_bone='';worst_frame=0;pelvis=[];first=None;last=None
    for frame in range(1,clip['frames']+1):
        bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
        pose={b.name:b.rotation_quaternion.copy() for b in rig.pose.bones}
        assert all(math.isfinite(v) for q in pose.values() for v in q),clip['role']
        for name,q in pose.items():
            if name in previous:
                angle=math.degrees(2*math.acos(min(1,abs(q.normalized().dot(previous[name].normalized())))))
                if angle>worst:worst,worst_bone,worst_frame=angle,name,frame
        previous=pose
        if first is None:first=pose
        last=pose
        pelvis.append(tuple(rig.pose.bones['pelvis'].head))
    seam=max(math.degrees(2*math.acos(min(1,abs(first[n].normalized().dot(last[n].normalized()))))) for n in first)
    horizontal=max(max(p[0] for p in pelvis)-min(p[0] for p in pelvis),max(p[1] for p in pelvis)-min(p[1] for p in pelvis))
    assert horizontal<1e-4,(clip['role'],'controller-owned horizontal root',horizontal)
    if clip['loop']:assert seam<.1,(clip['role'],'loop seam',seam)
    assert clip['contact']==-1 or 0<=clip['contact']<=1
    assert clip['release']==-1 or 0<=clip['release']<=clip['duration']+.001
    records.append({'role':clip['role'],'frames':clip['frames'],'max_angle_per_frame_deg':worst,'bone':worst_bone,'frame':worst_frame,'loop_seam_deg':seam,'horizontal_root_range':horizontal})
boundaries=[]
for side in ['', '_Left']:
    for stem in ['Throw','SpearThrow']:
        a=next(c for c in clips if c['role']==stem+'Charge'+side);b=next(c for c in clips if c['role']==stem+'Release'+side)
        ac={ (f.data_path,f.array_index):f.evaluate(a['frames']) for f in bag(bpy.data.actions[a['action']]).fcurves }
        bc={ (f.data_path,f.array_index):f.evaluate(1) for f in bag(bpy.data.actions[b['action']]).fcurves }
        # q and -q are equivalent rotations; compare the complete quaternion.
        error=0
        for name in rig.pose.bones.keys():
            path=f'pose.bones["{name}"].rotation_quaternion'
            x=Vector([ac[path,i] for i in range(4)]);y=Vector([bc[path,i] for i in range(4)])
            error=max(error,min((x-y).length,(x+y).length))
        assert error<1e-4,(stem,side,'charge/release continuity',error)
        boundaries.append({'role':stem+side,'boundary_quaternion_error':error})
source=REPO/'artifacts/human-animation-preview/Human - Animation Comparison.blend'
assert hashlib.sha256(source.read_bytes()).hexdigest()=='0946d797f3c620492444f2ec65d2a88280053fbda2c882dde3dfaf826942d229'
(ROOT/f'bank-mechanical-audit-{opts.variant}.json').write_text(json.dumps({'clips':records,'boundaries':boundaries,'source_unchanged':True},indent=2))
print('KIMODO BANK MECHANICAL AUDIT PASS',len(records),flush=True)
for r in sorted(records,key=lambda r:r['max_angle_per_frame_deg'],reverse=True)[:12]:print(r,flush=True)
