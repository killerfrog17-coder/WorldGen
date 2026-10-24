"""Bake licensed donor skeleton motions only; no donor mesh is exported."""
import argparse,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix

ap=argparse.ArgumentParser();ap.add_argument('--source',type=Path,required=True);ap.add_argument('--output',type=Path,required=True);ap.add_argument('--family',required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);a.output.mkdir(exist_ok=True,parents=True)
if a.source.suffix=='.blend':bpy.ops.wm.open_mainfile(filepath=str(a.source.resolve()))
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
    key='clip_'+str(len(clips));arrays[key]=np.asarray(poses,np.float32);clips.append({'label':label,'key':key,'source_action':name,'fps':30,'frames':frames})
np.savez_compressed(a.output/'source-motions.npz',**arrays)
(a.output/'source-rig.json').write_text(json.dumps({'family':a.family,'source':str(a.source.resolve()),'bones':bones,'clips':clips,'paths':paths,'root':names[0],'head':'Head' if 'Head' in names else None,'license':'CC0-1.0 verified at source page','donor_mesh_exported':False},indent=2))
print('Exported motion source',a.family,len(bones),'bones',[(c['label'],c['frames']) for c in clips],flush=True)
