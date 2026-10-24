"""Bake CC0 Mesh2Motion node animations into canonical joint matrices."""
import argparse,json,sys,shutil
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix,Vector

ap=argparse.ArgumentParser();ap.add_argument('--family',required=True);ap.add_argument('--output',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);a.output.mkdir(parents=True,exist_ok=True)
repo=Path(__file__).resolve().parent.parent;assets=repo/'artifacts/tools/mesh2motion-cli/node_modules/afk-rig/assets'
family_file={'fish':'shark','human':'human-base'}.get(a.family,a.family);source=assets/'animations'/f'{family_file}-animations.glb'
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(source))
s=bpy.context.scene;objects=[o for o in s.objects if o.type=='EMPTY' and o.name not in ('Armature','Fox-Mesh','Horse-Mesh')]
lookup={o.name:o for o in objects};names=[o.name for o in objects]
for o in objects:o.animation_data_clear()
bpy.context.view_layer.update();rotation=Matrix.Diagonal((-1,-1,1,1))
points=np.array([list(rotation@o.matrix_world.translation) for o in objects]);low=float(points[:,2].min());scale=1.65/max(float(np.ptp(points[:,2])),1e-6)
normalize=Matrix.Diagonal((scale,scale,scale,1));normalize.translation=(0,0,-low*scale);C=normalize@rotation
bones=[]
for o in objects:
    parent=o.parent if o.parent and o.parent.name in names else None;head=C@o.matrix_world.translation
    child=next((c for c in o.children if c.name in names),None);tail=C@child.matrix_world.translation if child else head+Vector((0,0,.03))
    bones.append({'name':o.name,'parent':parent.name if parent else None,'head':list(head),'tail':list(tail),'rest':[list(row) for row in C@o.matrix_world]})
if a.family=='fox':
    paths={'spine':['Hips','Spine_1','Spine_2','Spine_2.001'],'neck':['Spine_2.001','Spine_3','Spine_4','Head'],
      'tail':['Hips','Tail_Base','Tail_Mid','Tail_Mid.001','Tail_End','Tail_Tip']}
    for side in ('L','R'):
        paths['fore_'+side]=['Spine_2.001',*[n+'_'+side for n in ['Front_Leg_Shoulder','Front_Leg_Upper','Front_Leg_Lower','Front_Leg_Ankle','Front_Leg_Foot','Front_Leg_Tip']]]
        paths['hind_'+side]=['Hips',*[n+'_'+side for n in ['Back_Leg_Pelvis','Back_Leg_Upper','Back_Leg_Lower','Back_Leg_Ankle','Back_Leg_Foot','Back_Leg_Foot_1','Back_Leg_Tip']]]
    root='Hips';head='Head'
elif a.family=='human':
    paths={'spine':['pelvis','spine_01','spine_02','spine_03'],'neck':['spine_03','neck_01','head']}
    for side,small in [('L','l'),('R','r')]:
        paths['fore_'+side]=['spine_03',*[n+'_'+small for n in ('clavicle','upperarm','lowerarm','hand','middle_01','middle_02','middle_03','middle_04_leaf')]]
        paths['hind_'+side]=['pelvis',*[n+'_'+small for n in ('thigh','calf','foot','ball','ball_leaf')]]
    root='pelvis';head='head'
elif a.family=='bird':
    paths={'spine':['hips','spine_0','spine_1'],'neck':['spine_1','spine_2','spine_3','head'],'tail':['hips','tail_1','tail_2','tail_3','tail_tip']}
    for side in ('L','R'):
        paths['fore_'+side]=['spine_1',*[f'wing_{i}_{side}' for i in range(1,6)],'wing_tip_'+side]
        paths['hind_'+side]=['hips',*[n+'_'+side for n in ['UpperLeg','LowerLeg','AnkleLeg','Foot','Toes','Toes_tip']]]
    root='hips';head='head'
elif a.family=='fish':
    paths={'spine':['pelvis','neck','head'],'neck':['neck','head'],'tail':['pelvis',*[f'tail_{i}' for i in range(1,9)],'tail_tip']}
    for side in ('l','r'):
        paths['fin_front_'+side.upper()]=['pelvis','tail_1',*[f'front_fin_{i}_{side}' for i in range(3)],'front_fin_tip_'+side]
        paths['fin_back_'+side.upper()]=['pelvis',*[f'tail_{i}' for i in range(1,5)],*[f'back_fin_{i}_{side}' for i in range(3)],'back_fin_tip_'+side]
    root='pelvis';head='head'
elif a.family=='snake':
    paths={'spine':['head','head_tip'],'neck':['head','head_tip'],
      'tail':['head','neck',*[f'tail{i:02d}' for i in range(1,21)],'tail_tip']}
    root='head';head='head'
elif a.family=='spider':
    paths={'spine':['hips','spine_1','spine_2'],'neck':['spine_2','head','head_tip'],
      'tail':['hips','tail_1','tail_2','tail_3','tail_tip']}
    for side in ('l','r'):
        for row in 'abcd':paths['leg_'+row+'_'+side.upper()]=['spine_1','legs_anchor_'+side,'legs_anchor_'+str('abcd'.index(row)+1)+'_'+side,*[f'leg_{row}_{n}_{side}' for n in (1,2,3)],f'leg_{row}_tip_{side}']
    root='hips';head='head'
else:paths={};root=next((n for n in names if n.lower()=='hips'),names[0]);head=next((n for n in names if n.lower()=='head'),None)
arrays={};clips=[]
rest_basis={o.name:o.matrix_basis.copy() for o in objects}
for action in list(bpy.data.actions):
    if 'rest' in action.name.lower():continue
    if a.family=='snake' and action.name=='Dance':continue
    if a.family=='human' and action.name not in {'Idle_A','Walk','Jog'}:continue
    slots={slot.identifier:slot for slot in action.slots}
    for o in objects:
        o.animation_data_clear();o.matrix_basis=rest_basis[o.name]
        slot=slots.get('OB'+o.name)
        if slot:o.animation_data_create().action=action;o.animation_data.action_slot=slot
    first,last=action.frame_range;fps=s.render.fps/s.render.fps_base;count=max(2,round((last-first)/fps*30)+1);frames=[]
    for k in range(count):
        f=first+k/(count-1)*(last-first);s.frame_set(int(f),subframe=f-int(f));bpy.context.view_layer.update()
        frames.append([np.array(C@lookup[n].matrix_world) for n in names])
    label={'Idle_A':'Idle','Jog':'Run'}.get(action.name,action.name) if a.family=='human' else action.name
    key='clip_'+str(len(clips));arrays[key]=np.asarray(frames,np.float32);clips.append({'label':label,'key':key,'source_action':action.name,'fps':30,'frames':count})
np.savez_compressed(a.output/'source-motions.npz',**arrays)
(a.output/'source-rig.json').write_text(json.dumps({'family':a.family,'source':str(source),'bones':bones,'clips':clips,'paths':paths,'root':root,'head':head,'license':'CC0-1.0','donor_mesh_exported':False},indent=2))
shutil.copy2(assets/'LICENSE-CC0.md',a.output/'LICENSE-CC0.md')
(a.output/'provenance.json').write_text(json.dumps({'source':'https://github.com/Mesh2Motion/mesh2motion-app','license':'https://github.com/Mesh2Motion/mesh2motion-app/blob/main/LICENSE-CC0.MD','local_package':'afk-rig0.1.2','animations_only':True},indent=2))
print('Baked',a.family,len(bones),'nodes',len(clips),'clips',flush=True)
