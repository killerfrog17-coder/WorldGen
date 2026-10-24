"""Bake licensed donor skeleton motions only; no donor mesh is exported."""
import argparse,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix,Vector

ap=argparse.ArgumentParser();ap.add_argument('--source',type=Path,required=True);ap.add_argument('--output',type=Path,required=True);ap.add_argument('--family',required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);a.output.mkdir(exist_ok=True,parents=True)
if a.source.suffix=='.blend':bpy.ops.wm.open_mainfile(filepath=str(a.source.resolve()))
elif a.source.suffix.lower()=='.fbx':
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(a.source.resolve()))
else:
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(a.source.resolve()))
s=bpy.context.scene;rig=max((o for o in s.objects if o.type=='ARMATURE'),key=lambda o:len(o.data.bones));rig.animation_data_clear()
if a.family=='rabbit':
    names=['Hips','Spine','Spine.001','Neck','Head']
    for side in ['L','R']:names += ['FrontUpLeg_'+side,'FrontLeg_'+side,'FrontPaw_'+side,'BackUpLeg_'+side,'BackLeg_'+side,'BackLeg_'+side+'.001','BackPaw_'+side]
    names+=['Ear_L','Ear_R','Tail'];rotation=Matrix.Diagonal((-1,-1,1,1))
    paths={'spine':['Hips','Spine','Spine.001'],'neck':['Spine.001','Neck','Head'],'fore_L':['Spine.001','FrontUpLeg_L','FrontLeg_L','FrontPaw_L'],'fore_R':['Spine.001','FrontUpLeg_R','FrontLeg_R','FrontPaw_R'],
        'hind_L':['Hips','BackUpLeg_L','BackLeg_L','BackLeg_L.001','BackPaw_L'],'hind_R':['Hips','BackUpLeg_R','BackLeg_R','BackLeg_R.001','BackPaw_R'],'tail':['Hips','Tail']}
    wanted={'Idle':'Guarding','Rest':'Sitting.000','Run':'Running','Jump':'Jump','Death':'Dying.000'}
elif a.family in ('crocodile','rat','squirrel'):
    if a.family=='crocodile':
        paths={'spine':['base','spine1'],'neck':['spine1','neck','head'],'tail':['base','spine2','tail1','tail2','tail3','tail4']}
        for side in ('L','R'):
            paths['fore_'+side]=['spine1','shoulder_'+side,*[f'arm{i}_{side}' for i in range(1,4)]]
            paths['hind_'+side]=['base','spine2','hip_'+side,*[f'leg{i}_{side}' for i in range(1,4)]]
        wanted={'Idle':'idle-loop','Walk':'walk-loop','Attack':'attack','Death':'death'};head_name='head';root_name='base'
    elif a.family=='rat':
        paths={'spine':['Hip','Backbone','Backbone.001'],'neck':['Backbone.001','Backbone.002','Head'],'tail':['Hip','Tail','Tail.001','Tail.002','Tail.003']}
        for side in ('L','R'):
            paths['fore_'+side]=['Backbone.001','FrontLeg_'+side,*[f'FrontLeg_{side}.{i:03d}' for i in range(1,4)]]
            paths['hind_'+side]=['Hip','BackLeg_'+side,*[f'BackLeg_{side}.{i:03d}' for i in range(1,4)]]
        wanted={'Idle':'Idle.001','Idle variation':'Idle.002','Walk':'Walk','Run':'Run','Attack':'Attack.000','Hit':'Hit','Death':'Die'};head_name='Head';root_name='Hip'
    else:
        paths={'spine':['Pan','Backbone','Backbone.001'],'neck':['Backbone.001','Neck','Head'],'tail':['Pan','Tail',*[f'Tail.{i:03d}' for i in range(1,6)]]}
        for side in ('L','R'):
            paths['fore_'+side]=['Backbone.001','Arm_'+side,f'Arm_{side}.001',f'Arm_{side}.002']
            paths['hind_'+side]=['Pan','Leg_'+side,*[f'Leg_{side}.{i:03d}' for i in range(1,4)]]
        wanted={'Idle':'Idle.001','Idle variation':'Idle.003','Run':'Run','Jump start':'Jump.Begin','Jump landing':'Jump.End','Hit':'Pain.000','Death':'Die.000'};head_name='Head';root_name='Pan'
    names=list(dict.fromkeys(n for path in paths.values() for n in path))
    direction=rig.matrix_world.to_3x3()@(rig.data.bones[head_name].head_local-rig.data.bones[root_name].head_local);direction.z=0
    direction.normalize();right=Vector((direction.y,-direction.x,0));rotation=Matrix((right,direction,Vector((0,0,1)))).to_4x4()
elif a.family=='primate':
    paths={'spine':['b_Hip','b_Spine01','b_Spine02','b_Spine03'],'neck':['b_Spine03','b_Neck','b_Head'],
      'tail':['b_Hip',*[f'b_Tail{i:02d}' for i in range(1,8)]]}
    for side,long in [('L','Left'),('R','Right')]:
        paths['fore_'+side]=['b_Spine03',*[f'b_{long}_{n}' for n in ('Shoulder','UpperArm','ForeArm','Hand','Hand2')]]
        paths['hind_'+side]=['b_Hip',*[f'b_{long}_{n}' for n in ('Leg01','Leg02','Foot01','Foot02')]]
    names=list(dict.fromkeys(n for path in paths.values() for n in path));wanted={'Idle':'Armature|Take 001|BaseLayer','Source movement':'Armature|Take 001|BaseLayer'};rotation=Matrix.Diagonal((-1,-1,1,1))
elif a.family=='lemur':
    paths={'spine':['hips','spine','chest'],'neck':['chest','neck','head'],'tail':['hips',*[f'tail.{i:03d}' for i in range(1,7)]]}
    for side in ('L','R'):
        paths['fore_'+side]=['chest',*[n+'.'+side for n in ('shoulder','upper_arm','forearm','hand')]]
        paths['hind_'+side]=['hips',*[n+'.'+side for n in ('thigh','shin','foot','toe')]]
    names=list(dict.fromkeys(n for path in paths.values() for n in path));wanted={'Idle':'idle','Run':'run','Attack':'AttackMelee'};rotation=Matrix.Diagonal((-1,-1,1,1))
elif a.family=='turtle':
    paths={'spine':['Bone','Bone.001'],'neck':['Bone.001','Bone.015','Bone.016','Bone.017','Bone.018'],'tail':['Bone','Bone.005','Bone.006'],
      'fore_L':['Bone.001','Bone.013','Bone.014'],'fore_R':['Bone.002','Bone.011','Bone.012'],
      'hind_L':['Bone.003','Bone.007','Bone.008'],'hind_R':['Bone.004','Bone.009','Bone.010']}
    names=list(dict.fromkeys(n for path in paths.values() for n in path));wanted={'Idle':'Walking.001','Walk':'Walking-loop'};rotation=Matrix.Diagonal((-1,-1,1,1))
elif a.family=='ground_insect':
    paths={'spine':['Bone','Bone.001'],'neck':['Bone.001','Bone.031'],'tail':['Bone','Bone.002']}
    for side,starts in [('L',(7,11,15)),('R',(19,27,23))]:
        for row,start in zip('abc',starts):paths['leg_'+row+'_'+side]=['Bone',*[f'Bone.{i:03d}' for i in range(start,start+4)]]
    names=list(dict.fromkeys(n for path in paths.values() for n in path));wanted={'Idle':'bite','Walk':'walk','Bite':'bite'};rotation=Matrix.Identity(4)
else:
    names=[b.name for b in rig.data.bones];rotation=Matrix.Diagonal((-1,-1,1,1));paths={}
    wanted={act.name:act.name for act in bpy.data.actions if act.frame_range[1]>act.frame_range[0]}
rig.data.pose_position='REST'
rest=[rotation@rig.matrix_world@rig.data.bones[n].matrix_local for n in names]
head=np.array([m.translation[:] for m in rest]);ends=np.array([list(rotation@(rig.matrix_world@rig.data.bones[n].tail_local)) for n in names])
extent=np.concatenate([head,ends]);lowest=float(extent[:,2].min());h=float(np.ptp(extent[:,2]));scale=1.65/max(h,1e-6)
normalize=Matrix.Diagonal((scale,scale,scale,1));normalize.translation=(0,0,-lowest*scale)
C=normalize@rotation
bones=[]
for n in names:
    bone=rig.data.bones[n];parent=bone.parent
    while parent and parent.name not in names:parent=parent.parent
    bones.append({'name':n,'parent':parent.name if parent else None,'head':list(C@(rig.matrix_world@bone.head_local)),
                  'tail':list(C@(rig.matrix_world@bone.tail_local)),'rest':[list(row) for row in C@rig.matrix_world@bone.matrix_local]})
rig.data.pose_position='POSE';arrays={};clips=[]
for label,name in wanted.items():
    action=bpy.data.actions.get(name)
    if not action:continue
    rig.animation_data_clear();rig.animation_data_create().action=action
    if action.slots:rig.animation_data.action_slot=action.slots[0]
    first,last=action.frame_range;fps=s.render.fps/s.render.fps_base;frames=max(2,round((last-first)/fps*30)+1)
    poses=[]
    for k in range(frames):
        f=first+k/(frames-1)*(last-first);s.frame_set(int(f),subframe=f-int(f));bpy.context.view_layer.update()
        poses.append([np.array(C@rig.matrix_world@rig.pose.bones[n].matrix) for n in names])
    if a.family in ('ground_insect','primate') and label=='Idle':poses=[poses[0],poses[0]];frames=2
    key='clip_'+str(len(clips));arrays[key]=np.asarray(poses,np.float32);clips.append({'label':label,'key':key,'source_action':name+' (first pose held)' if a.family=='ground_insect' and label=='Idle' else name,'fps':30,'frames':frames})
np.savez_compressed(a.output/'source-motions.npz',**arrays)
head_name='Bone.018' if a.family=='turtle' else 'Bone.031' if a.family=='ground_insect' else 'b_Head' if a.family=='primate' else 'Head' if 'Head' in names else 'head' if 'head' in names else None
(a.output/'source-rig.json').write_text(json.dumps({'family':a.family,'source':str(a.source.resolve()),'bones':bones,'clips':clips,'paths':paths,'root':names[0],'head':head_name,'license':'CC0-1.0 verified at source page','donor_mesh_exported':False},indent=2))
print('Exported motion source',a.family,len(bones),'bones',[(c['label'],c['frames']) for c in clips],flush=True)
