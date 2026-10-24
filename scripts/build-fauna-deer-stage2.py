"""Fit accepted rig/motions to the approved fresh deer, keeping its bind art."""
import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Matrix,Quaternion,Vector

sys.path.insert(0,str(Path(__file__).parent))
from deer_behaviour_core import Skeleton,matrix,blend_pose,smooth
spec=importlib.util.spec_from_file_location('deer_library',Path(__file__).with_name('build-deer-behaviour-library.py'))
lib=importlib.util.module_from_spec(spec);spec.loader.exec_module(lib)
ap=argparse.ArgumentParser();ap.add_argument('--model',type=Path,required=True);ap.add_argument('--root',type=Path,required=True)
ap.add_argument('--pilot',action='store_true');ap.add_argument('--replace-unopened',action='store_true')
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
out=root/('Deer - Unpublished Motion Pilot.blend' if a.pilot else 'Deer - Stage2 Animation Review.blend')
if out.exists() and not a.replace_unopened:raise FileExistsError(out)
source=json.loads((root/'source-rig.json').read_text());target=json.loads((root/'target-rig.json').read_text())
motion=np.load(root/'source-motions.npz');fit=np.load(root/'fitted-skin.npz')
bpy.ops.wm.open_mainfile(filepath=str(a.model.resolve()));s=bpy.context.scene;info=json.loads(s['fauna_stage1'])
body=bpy.data.objects[info['body']];basis=np.array([v.co[:] for v in body.data.vertices]);source_names=[b['name'] for b in source['bones']]
bpy.ops.object.select_all(action='DESELECT');arm=bpy.data.armatures.new('Accepted deer skeleton - fitted');rig=bpy.data.objects.new('AI Deer Rig',arm)
s.collection.objects.link(rig);rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
for bone in source['bones']:
    eb=arm.edit_bones.new(bone['name']);t=target[bone['name']];eb.head=t['head'];eb.tail=t['tail']
    eb.align_roll(Vector(np.array(bone['rest'])[:3,2]))
for bone in source['bones']:
    if bone['parent']:arm.edit_bones[bone['name']].parent=arm.edit_bones[bone['parent']]
bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True;rig.hide_render=True
skel=Skeleton(lib.metadata(rig));source_index=[source_names.index(n) for n in skel.names]
def bind(obj,weights):
    obj.vertex_groups.clear()
    for i,name in enumerate(source_names):
        group=obj.vertex_groups.new(name=name)
        for index in np.flatnonzero(weights[:,i]>1e-7):group.add([int(index)],float(weights[index,i]),'REPLACE')
    modifier=obj.modifiers.new('Fauna armature - linear skin','ARMATURE');modifier.object=rig;modifier.use_deform_preserve_volume=False
    obj.parent=rig;obj.matrix_parent_inverse=Matrix.Identity(4)
bind(body,fit['weights'])
for name in [*info['eyes'],info['whiskers'],*[x['object'] for x in info['antlers']]]:
    obj=bpy.data.objects[name];weights=np.zeros((len(obj.data.vertices),len(source_names)),np.float32)
    weights[:,source_names.index('Head')]=1;bind(obj,weights)
weights=fit['weights'][:,source_index];hom=np.c_[basis,np.ones(len(basis))]
top=np.argpartition(weights,-4,axis=1)[:,-4:];compact=np.take_along_axis(weights,top,axis=1)
def skin(globals_m,ids=None):
    transforms=np.array([g@inv for g,inv in zip(globals_m,skel.inverse)])
    ids=np.arange(len(basis)) if ids is None else ids
    return np.einsum('vk,vkij,vj->vi',compact[ids],transforms[top[ids],:3,:],hom[ids],optimize=True)
feet=[]
for name,chain in [('ForeHoof_L',['UpperFore_L','Elbow_L','Wrist_L','ForeHoof_L']),('ForeHoof_R',['UpperFore_R','Elbow_R','Wrist_R','ForeHoof_R']),
                   ('HindHoof_L',['Thigh_L','Knee_L','Hock_L','HindHoof_L']),('HindHoof_R',['Thigh_R','Knee_R','Hock_R','HindHoof_R'])]:
    index=skel.index[name];ids=np.flatnonzero(weights[:,index]>.45)
    assert len(ids)>3,(name,'missing hoof weights')
    lowest=basis[ids,2].min();sole=basis[ids[basis[ids,2]<lowest+.018]].mean(0)
    feet.append(dict(bone=name,chain=chain,sole_local=list(skel.inverse[index]@Vector(sole)),ids=ids.tolist()))
skel.feet=feet
idle_meta=next(c for c in source['clips'] if c['label']=='Idle');reference=motion[idle_meta['key']][0,source_index]
reference_q=[Matrix(m).to_quaternion() for m in reference]
head_ids=np.flatnonzero(weights[:,skel.index['Head']]>.65)
def transfer(frame):
    g=[]
    for i,name in enumerate(skel.names):
        q=Matrix(frame[source_index[i]]).to_quaternion()@reference_q[i].inverted()@skel.rest[i].to_quaternion()
        parent=skel.parents[i]
        location=skel.rest[i].translation.copy() if parent<0 else g[parent]@skel.local[i].translation
        if name=='Pelvis':location+=Vector(frame[source_index[i],:3,3]-reference[i,:3,3])
        g.append(matrix(q,location))
    return g
def ground(g,standing=True):
    if standing:
        contacts=[1-smooth(max(0,(skel.sole(g,f).z-.008)/.05)) for f in feet]
        skel.ground_feet(g,contacts)
    return g
def surface_feet(g):
    for _ in range(3):
        for foot in feet:
            lowest=float(skin(g,np.array(foot['ids']))[:,2].min())
            if lowest>=.003:continue
            i=skel.index[foot['bone']];q=g[i].to_quaternion()
            goal=g[i].translation+Vector((0,0,.004-lowest))
            skel.solve_chain(g,foot['chain'],goal,iterations=24)
            skel.rotate_branch(g,i,q@g[i].to_quaternion().inverted())
    return g
raw={};poses={};reports={};labels=['Idle','Walk','Run','Eating'] if a.pilot else [c['label'] for c in source['clips']]
for clip in source['clips']:
    if clip['label'] not in labels:continue
    raw[clip['label']]=[transfer(f) for f in motion[clip['key']]]
standing=ground([m.copy() for m in raw['Idle'][0]])
def feeding(original):
    g=[m.copy() for m in standing];skel.translate_all(g,(0,0,-.070))
    for name in ('Neck1','Neck2','Neck3','Head'):
        i=skel.index[name];g[i]=matrix(original[i].to_quaternion(),g[skel.parents[i]]@skel.local[i].translation)
    for name in ('Ear_L','Ear_R','Jaw','Muzzle_L','Muzzle_R'):
        i=skel.index[name];g[i]=g[skel.parents[i]]@skel.local[i]
    for foot in feet:
        i=skel.index[foot['bone']];q=g[i].to_quaternion();target_sole=skel.sole(standing,foot);target_sole.z=.004
        if foot['bone'].startswith('Fore'):target_sole+=Vector((.01 if target_sole.x>0 else -.01,.050,0))
        goal=target_sole-q@Vector(foot['sole_local']);skel.solve_chain(g,foot['chain'],goal,iterations=32)
        skel.rotate_branch(g,i,q@g[i].to_quaternion().inverted())
    hi=skel.index['Head'];q=g[hi].to_quaternion()
    local=np.array([skel.inverse[hi]@Vector(point) for point in basis[head_ids]])
    lowest=(local@np.array(q.to_matrix()).T)[:,2].min()
    goal=Vector((0,.70,.025-float(lowest)))
    chain=['Neck1','Neck2','Neck3','Head'];anchor=g[skel.index['Neck1']].translation
    reach=sum((skel.rest[skel.index[b]].translation-skel.rest[skel.index[c]].translation).length for c,b in zip(chain,chain[1:]))*.997
    dy=math.sqrt(max(.01,reach*reach-(goal.z-anchor.z)**2))
    goal.y=min(goal.y,anchor.y+dy)
    skel.solve_chain(g,chain,goal,iterations=48)
    skel.rotate_branch(g,hi,q@g[hi].to_quaternion().inverted())
    return g
def action_for(label,frames):
    action=bpy.data.actions.new('Fauna | '+label);action.use_fake_user=True
    slot=action.slots.new(id_type='OBJECT',name=rig.name);layer=action.layers.new('Motion')
    strip=layer.strips.new(type='KEYFRAME');bag=strip.channelbag(slot,ensure=True)
    previous=None;locations=[];rotations=[];dense=[]
    for index,g in enumerate(frames):
        dense.append(g)
        if index+1<len(frames):
            middle=skel.fk(blend_pose(skel.basis(g),skel.basis(frames[index+1]),.5))
            if label not in ('Death','Sleep','lay_to_idle','Lie Down'):middle=surface_feet(middle)
            dense.append(middle)
    for frame,g in enumerate(dense):
        loc,rot=skel.basis(g)
        if previous is not None:rot[np.sum(rot*previous,axis=1)<0]*=-1
        previous=rot.copy();locations.append(loc);rotations.append(rot)
    locations=np.array(locations);rotations=np.array(rotations);times=np.arange(len(dense),dtype=np.float32)*.5
    linear=bpy.types.Keyframe.bl_rna.properties['interpolation'].enum_items['LINEAR'].value
    for i,name in enumerate(skel.names):
        rig.pose.bones[name].rotation_mode='QUATERNION'
        for path,values in [('location',locations[:,i]),('rotation_quaternion',rotations[:,i])]:
            for component in range(values.shape[1]):
                curve=bag.fcurves.new(data_path=f'pose.bones["{name}"].{path}',index=component)
                curve.keyframe_points.add(len(dense));curve.keyframe_points.foreach_set('co',np.c_[times,values[:,component]].astype(np.float32).ravel())
                curve.keyframe_points.foreach_set('interpolation',np.full(len(dense),linear,np.int32));curve.update()
    return action
actions={}
for label in labels:
    frames=[];rows=[]
    for original in raw[label]:
        g=feeding(original) if label=='Eating' else ground([m.copy() for m in original],label not in ('Death','Sleep','lay_to_idle'))
        if label not in ('Death','Sleep','lay_to_idle'):g=surface_feet(g)
        vertices=skin(g)
        if label in ('Death','Sleep','lay_to_idle') and vertices[:,2].min()<.003:
            skel.translate_all(g,(0,0,.003-float(vertices[:,2].min())));vertices=skin(g)
        frames.append(g);rows.append(dict(lowest=float(vertices[:,2].min()),head_lowest=float(vertices[head_ids,2].min())))
    if label not in ('Death','Sleep','lay_to_idle'):
        # Bound toe-off's orientation change before baking contact corrections.
        # The borrowed ankle channels can swing ~90deg in one source frame on
        # this different bind; rigid fresh hooves expose what old blended skin hid.
        previous={f['bone']:frames[-1][skel.index[f['bone']]].to_quaternion() for f in feet}
        for _ in range(2):
            for g in frames:
                for foot in feet:
                    index=skel.index[foot['bone']];q=g[index].to_quaternion();prior=previous[foot['bone']]
                    angle=prior.rotation_difference(q).angle
                    if angle>math.pi:angle=2*math.pi-angle
                    limited=prior.slerp(q,min(1,math.radians(15)/max(angle,1e-8)))
                    skel.rotate_branch(g,index,limited@q.inverted());previous[foot['bone']]=limited
                surface_feet(g)
        rows=[dict(lowest=float(skin(g)[:,2].min()),head_lowest=float(skin(g,head_ids)[:,2].min())) for g in frames]
    poses[label]=frames;actions[label]=action_for(label,frames).name
    reports[label]=dict(frames=len(frames),lowest=min(r['lowest'] for r in rows),head_lowest=min(r['head_lowest'] for r in rows))
    print('Fitted and baked',label,reports[label],flush=True)
if not a.pilot:
    poses['Lie Down']=list(reversed(poses['lay_to_idle']));actions['Lie Down']=action_for('Lie Down',poses['Lie Down']).name
    cached={name:[skel.basis(g) for g in frames] for name,frames in poses.items()}
    periods={name:len(frames)/30 for name,frames in poses.items()}
    for name,frames in poses.items():
        if name in ('Idle','Walk','Trot','Run','Eating','Sleep'):
            if max(np.max(np.abs(np.array(a)-np.array(b))) for a,b in zip(frames[0],frames[-1]))<.002:
                cached[name]=cached[name][:-1];periods[name]=len(cached[name])/30
    loops={'Idle','Walk','Trot','Run','Eating','Sleep'}
    def sample(name,seconds):
        array=cached[name];frame=seconds*30
        if name in loops:frame%=len(array)
        else:frame=min(max(frame,0),len(array)-1)
        first=int(frame);second=(first+1)%len(array) if name in loops else min(first+1,len(array)-1)
        return blend_pose(array[first],array[second],frame-first)
    def sequence(name,parts):
        result=[];previous=None;previous_offset=0;previous_duration=0;offset=0
        for label,duration in parts:
            if previous in ('Walk','Trot','Run') and label in ('Walk','Trot','Run'):
                phase=((previous_duration+previous_offset)/periods[previous])%1
                offset=phase*periods[label]
            else:offset=0
            for frame in range(round(duration*30)):
                t=frame/30;pose=sample(label,t+offset)
                if previous is not None and t<.45:
                    pose=blend_pose(sample(previous,previous_duration+previous_offset+t),pose,smooth(t/.45))
                g=surface_feet(skel.fk(pose));result.append(g)
            previous=label;previous_duration=duration;previous_offset=offset
        # Blend the end back to the opening Idle for a continuous demonstration.
        for frame in range(15):
            pose=blend_pose(skel.basis(result[-1]),sample('Idle',0),smooth((frame+1)/15))
            result.append(surface_feet(skel.fk(pose)))
        poses[name]=result;actions[name]=action_for(name,result).name
        print('Baked smooth demonstration',name,len(result),'frames',flush=True)
    sequence('Locomotion demo',[('Idle',2),('Walk',3),('Trot',2),('Run',3),('Trot',1.5),('Walk',3),('Idle',2)])
    sequence('Behaviour showcase',[('Idle',2),('Walk',2.5),('Trot',2),('Run',2.5),('Walk',2),('Idle',1.5),
        ('Eating',5),('Idle',2),('Turn_Left',periods['Turn_Left']),('Idle',1),('Turn_Right',periods['Turn_Right']),
        ('Idle',1),('Head_But',periods['Head_But']),('Idle',1),('Kick',periods['Kick']),('Idle',2)])
info.update(stage=2,rig=rig.name,actions=actions,motion_bank='Accepted Mesh2Motion authored motions',motion_fps=30,
    skeleton=lib.metadata(rig),feet=feet,source_rig_sha256=source['source_sha256'],model_sha256=hashlib.sha256(a.model.read_bytes()).hexdigest())
s['fauna_stage2']=json.dumps(info);s.render.fps=30;s.render.fps_base=1
rig.animation_data_clear();rig.data.pose_position='POSE';idle=bpy.data.actions[actions['Idle']]
rig.animation_data_create().action=idle;rig.animation_data.action_slot=idle.slots[0]
s.frame_start=0;s.frame_end=len(poses['Idle'])-1;s.frame_set(0)
if not bpy.data.objects.get('Review floor'):
    bpy.ops.mesh.primitive_plane_add(size=12,location=(0,0,-.002));floor=bpy.context.object;floor.name='Review floor'
    mat=bpy.data.materials.new('Review ground');mat.diffuse_color=(.16,.17,.15,1);floor.data.materials.append(mat)
rig.hide_set(True)
bpy.ops.wm.save_as_mainfile(filepath=str(out))
(root/'motion-fit-checks.json').write_text(json.dumps(dict(clips=reports,model_bind_unchanged=np.array_equal(basis,np.array([v.co[:] for v in body.data.vertices])),
    motion_origin=source['motion_origin'],gpu_inference=False,game_launched=False),indent=2))
for label,frame in [('Idle',0),('Walk',7),('Run',6),('Eating',40)]:
    if label not in actions:continue
    action=bpy.data.actions[actions[label]];rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0];s.frame_set(frame)
    t=Vector((0,.05,.82));s.camera.location=t+Vector((1,.10,.13)).normalized()*4
    s.camera.rotation_euler=(t-s.camera.location).to_track_quat('-Z','Y').to_euler();s.camera.data.ortho_scale=2.8
    s.render.filepath=str(root/('motion-'+label.lower()+'.png'));bpy.ops.render.render(write_still=True)
print('Stage2 fitted-motion review built.',flush=True)
