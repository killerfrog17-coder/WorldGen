"""Print anatomical motion markers to help trim real generated takes."""
import argparse
from pathlib import Path
import sys
import numpy as np
REPO=Path(__file__).resolve().parent.parent
sys.path.insert(0,str(REPO/'scripts'))
from kimodo_demo_motion import SomaMotion
import importlib.util
spec=importlib.util.spec_from_file_location('bank',REPO/'scripts/build-kimodo-human-bank.py');bank=importlib.util.module_from_spec(spec);spec.loader.exec_module(bank)
parser=argparse.ArgumentParser();parser.add_argument('case');parser.add_argument('--fitted-role');parser.add_argument('--take');args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
motion=SomaMotion(bank.ROOT/'motions'/args.case/args.take if args.take else bank.selected_take(args.case));rows=[motion.sample(None,i/30) for i in range(motion.info['frames'])]
print('frame hip_z left_toe_z right_toe_z right_hand_y right_hand_z')
for i,r in enumerate(rows):
    if i%5==0:print(i,*[round(float(v),3) for v in [r['Hips'].translation.z,r['LeftToeBase'].translation.z,r['RightToeBase'].translation.z,r['RightHand'].translation.y,r['RightHand'].translation.z]])
air=[i for i,r in enumerate(rows) if min(r['LeftToeBase'].translation.z,r['RightToeBase'].translation.z)>.075]
print('AIR',air)
print('NEUTRAL HIP',motion.rest['Hips'].translation.z)
for name in ['LeftHand','RightHand']:
    values=np.array([tuple(r[name].translation-r['Hips'].translation) for r in rows])
    print('HAND EXCURSION',name,np.ptp(values,axis=0).tolist())
if args.fitted_role:
    import bpy
    bpy.ops.wm.open_mainfile(filepath=str(bank.ROOT/'Kimodo bank B.blend'))
    rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    import json
    record=next(c for c in json.loads(rig['kimodo_bank_clips']) if c['role']==args.fitted_role)
    action=bpy.data.actions[record['action']];rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
    for frame in [1,record['frames']//2,record['frames']]:
        bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
        row=rows[record['begin']+frame-1]
        source_delta=row['RightHand'].translation-row['Hips'].translation
        native_delta=rig.pose.bones['hand_r'].head-rig.pose.bones['pelvis'].head
        print('FITTED',frame,'source hand rel hip',tuple(source_delta),'native',tuple(native_delta),'error',(source_delta-native_delta).length)
