"""Offline CC0 motion adaptation, neutral bind pose, grounding and native export.

No animal mesh substitution, ML inference, or Unity/player launch. New motions
come from Quaternius deer and Mesh2Motion horse, with documented pose adaptation.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Matrix,Quaternion,Vector

sys.path.insert(0,str(Path(__file__).parent))
from deer_behaviour_core import Skeleton,F,R,Z,matrix,blend_pose,smooth,wrap_angle


def ordered_bones(rig):
    result=[]
    def visit(b):
        result.append(b)
        for child in b.children:
            visit(child)
    for b in rig.data.bones:
        if b.parent is None:
            visit(b)
    return result


def metadata(rig):
    bones=ordered_bones(rig)
    names=[b.name for b in bones]
    return dict(bones=names,parents=[names.index(b.parent.name) if b.parent else -1 for b in bones],
                rest=[[list(row) for row in b.matrix_local] for b in bones])


def assign_pose(rig,skel,globals_m):
    loc,rot=skel.basis(globals_m)
    for i,n in enumerate(skel.names):
        pb=rig.pose.bones[n]
        pb.rotation_mode='QUATERNION'
        pb.location=loc[i]
        pb.rotation_quaternion=rot[i]
        pb.scale=(1,1,1)
    bpy.context.view_layer.update()


def neutralize(rig,meshes):
    """Correct the model's turned-head bind, measured from textured nose points."""
    mesh=meshes[0]
    rig.animation_data_clear()
    rig.data.pose_position='POSE'
    for pb in rig.pose.bones:
        pb.matrix_basis.identity()
    meta=metadata(rig)
    skel=Skeleton(meta)
    vertices=np.array([v.co[:] for v in mesh.data.vertices])
    group={g.name:g.index for g in mesh.vertex_groups}
    weights=np.zeros((len(vertices),len(group)),np.float32)
    for v in mesh.data.vertices:
        for g in v.groups:
            weights[v.index,g.group]=g.weight
    head_weight=weights[:,[group[n] for n in ('Head','Jaw','Muzzle_R','Muzzle_L')]].sum(1)
    image=next(n.image for n in mesh.data.materials[0].node_tree.nodes if
               n.type=='TEX_IMAGE' and n.image.colorspace_settings.name=='sRGB')
    pix=np.array(image.pixels[:]).reshape(image.size[1],image.size[0],4)
    uv=np.zeros((len(vertices),2))
    for loop in mesh.data.loops:
        uv[loop.vertex_index]=mesh.data.uv_layers.active.data[loop.index].uv
    xy=np.clip((uv*np.array(image.size)).astype(int),0,np.array(image.size)-1)
    rgb=pix[xy[:,1],xy[:,0],:3]
    nose_ids=np.flatnonzero((rgb.max(1)<.23)&(head_weight>.65))
    if len(nose_ids)<10:
        raise ValueError('Cannot identify the inspected deer nose')
    head_w=weights[:,group['Head']]
    def angle(coords):
        h=(coords*head_w[:,None]).sum(0)/head_w.sum()
        nose=coords[nose_ids].mean(0)
        direction=Vector(nose-h)
        return wrap_angle(math.atan2(direction.y,direction.x)-math.atan2(F.y,F.x))
    original_angle=angle(vertices)
    total=-original_angle
    final_angle=original_angle
    for _ in range(8):
        globals_m=[m.copy() for m in skel.rest]
        for n,share in [('Neck1',.2),('Neck2',.35),('Neck3',.45)]:
            skel.rotate_branch(globals_m,skel.index[n],Quaternion(Z,total*share))
        assign_pose(rig,skel,globals_m)
        ev=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
        coords=np.array([v.co[:] for v in ev.data.vertices])
        final_angle=angle(coords)
        if abs(final_angle)<math.radians(.75):
            break
        total-=final_angle*.9
        total=max(-math.radians(100),min(math.radians(100),total))
    if abs(final_angle)>math.radians(4):
        raise ValueError('Neutral head calibration did not converge')
    # Bake only the head correction to each mesh; preserve UVs and weights.
    for obj in meshes:
        obj.hide_set(False)
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active=obj
        mod=next(m for m in obj.modifiers if m.type=='ARMATURE')
        bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    # A non-deforming root gives engines an explicit root-motion extraction node.
    bpy.ops.object.mode_set(mode='EDIT')
    root=rig.data.edit_bones.new('MotionRoot')
    root.head=(0,0,0)
    root.tail=(0,.2,0)
    root.use_deform=False
    rig.data.edit_bones['Pelvis'].parent=root
    bpy.ops.object.mode_set(mode='OBJECT')
    for i,obj in enumerate(meshes):
        mod=obj.modifiers.new('Game linear skinning','ARMATURE')
        mod.object=rig
        mod.use_deform_preserve_volume=False
        obj.hide_set(i!=0)
        obj.hide_render=i!=0
    verts=np.array([v.co[:] for v in mesh.data.vertices])
    nose=verts[nose_ids].mean(0)
    report=dict(original_yaw_deg=math.degrees(original_angle),
                applied_neck_yaw_deg=math.degrees(total),final_yaw_deg=math.degrees(final_angle))
    return nose,report


def source_mapping(kind):
    if kind=='deer':
        mapping={'Pelvis':'Back','Spine1':'Torso','Spine2':'Torso2','Chest':'Torso3',
            'Neck1':'Neck1','Neck2':'Neck2','Neck3':'Neck3','Head':'Head','Tail1':'Tail1','Tail2':'Tail3'}
        for s in ('L','R'):
            mapping.update({f'Shoulder_{s}':f'FrontShoulder.{s}',f'UpperFore_{s}':f'FrontUpperLeg.{s}',
                f'Elbow_{s}':f'FrontLowerLeg.{s}',f'Wrist_{s}':f'FrontLowerLeg.{s}',
                f'ForeHoof_{s}':f'IKFrontLeg.{s}',f'Thigh_{s}':f'BackLeg.{s}',
                f'Knee_{s}':f'BackUpperLeg.{s}',f'Hock_{s}':f'BackLowerLeg.{s}',
                f'HindHoof_{s}':f'IKBackLeg.{s}'})
        feet=['FF.L','FF.R','FFB.L','FFB.R']
    else:
        mapping={'Pelvis':'hips','Spine1':'spine_1','Spine2':'spine_2','Chest':'spine_3',
            'Neck1':'spine_4','Neck2':'spine_5','Neck3':'spine_6','Head':'head',
            'Tail1':'tail_1','Tail2':'tail_2'}
        for s in ('L','R'):
            ss=s.lower()
            mapping.update({f'Shoulder_{s}':f'front_scapula_{ss}',f'UpperFore_{s}':f'front_leg_upper_{ss}',
                f'Elbow_{s}':f'front_leg_lower_{ss}',f'Wrist_{s}':f'front_leg_ankle_{ss}',
                f'ForeHoof_{s}':f'front_leg_foot_{ss}',f'Thigh_{s}':f'back_leg_upper_{ss}',
                f'Knee_{s}':f'back_leg_lower_{ss}',f'Hock_{s}':f'back_leg_ankle_{ss}',
                f'HindHoof_{s}':f'back_leg_foot_{ss}'})
        feet=['front_leg_leaf_l','front_leg_leaf_r','back_leg_leaf_l','back_leg_leaf_r']
    for n in ('Ear_L','Ear_R','Jaw','Muzzle_R','Muzzle_L'):
        mapping[n]=mapping['Head']
    return mapping,feet


def sample_sources(kind,path,skel):
    """World bind-axis-correct deformation rotations, no source mesh retained."""
    scene=bpy.context.scene
    before=set(scene.objects)
    before_actions=set(bpy.data.actions)
    bpy.ops.import_scene.gltf(filepath=str(path))
    imported=[o for o in scene.objects if o not in before]
    donor=next(o for o in imported if o.type=='ARMATURE')
    donor.animation_data_clear()
    mapping,feet=source_mapping(kind)
    canonical=Matrix((R,-F,Z)).transposed().to_quaternion()
    rest={n:(donor.matrix_world@donor.data.bones[n].matrix_local).copy()
          for n in set(mapping.values())|set(feet)}
    hip=mapping['Pelvis']
    scale=skel.rest[skel.index['Pelvis']].translation.z/rest[hip].translation.z
    wanted=(['Idle','Walk','Gallop','Eating','Idle_2','Idle_HitReact_Left','Idle_HitReact_Right','Death','Gallop_Jump']
            if kind=='deer' else ['Trot','Sleep','lay_to_idle','Eating','Turn_Left','Turn_Right'])
    result={}
    for clip in wanted:
        action=bpy.data.actions[clip]
        donor.animation_data_create().action=action
        if action.slots:
            donor.animation_data.action_slot=action.slots[0]
        begin,end=action.frame_range
        count=max(2,round(end-begin)+1)
        frames=[]
        source_sole=[]
        for f in np.linspace(begin,end,count):
            scene.frame_set(int(f),subframe=float(f-int(f)))
            qs={}
            for n in set(mapping.values()):
                q=(donor.matrix_world@donor.pose.bones[n].matrix).to_quaternion()
                qs[n]=canonical@(q@rest[n].to_quaternion().inverted())@canonical.inverted()
            delta=canonical@((donor.matrix_world@donor.pose.bones[hip].head)-rest[hip].translation)*scale
            g=[]
            for i,n in enumerate(skel.names):
                p=skel.parents[i]
                if n=='MotionRoot':
                    g.append(skel.rest[i].copy())
                    continue
                q=qs[mapping[n]]@skel.rest[i].to_quaternion()
                if n in ('Ear_L','Ear_R','Jaw','Muzzle_R','Muzzle_L','Tail2'):
                    q=g[p].to_quaternion()@(skel.rest[p].to_quaternion().inverted()@skel.rest[i].to_quaternion())
                pos=(g[p]@skel.inverse[p]@skel.rest[i].translation if p>=0 else skel.rest[i].translation)
                if n=='Pelvis':
                    pos=skel.rest[i].translation+delta
                g.append(matrix(q,pos))
            frames.append(g)
            source_sole.append([(donor.matrix_world@donor.pose.bones[n].head).z*scale for n in feet])
        result[clip]=dict(globals=frames,source_height=np.array(source_sole),
            source=f'{kind}:{clip}',duration=(end-begin)/24)
    for obj in imported:
        bpy.data.objects.remove(obj,do_unlink=True)
    for a in list(bpy.data.actions):
        if a not in before_actions:
            bpy.data.actions.remove(a)
    return result


def resample_pose_frames(frames,count,skel):
    local=[skel.basis(g) for g in frames]
    result=[]
    for u in np.linspace(0,len(frames)-1,count):
        i=min(len(frames)-1,int(u))
        j=min(len(frames)-1,i+1)
        result.append(skel.fk(blend_pose(local[i],local[j],u-i)))
    return result


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--asset',type=Path,required=True)
    ap.add_argument('--deer',type=Path,required=True)
    ap.add_argument('--root',type=Path,required=True)
    ap.add_argument('--output-name',default='Deer - Behaviour Prototype.blend')
    args=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
    args.asset,args.deer,args.root=[p.resolve() for p in (args.asset,args.deer,args.root)]
    output=args.root/args.output_name
    if output.exists():
        raise FileExistsError(output)
    bpy.ops.wm.open_mainfile(filepath=str(args.asset))
    scene=bpy.context.scene
    scene.render.fps=24
    rig=bpy.data.objects['AI Deer Rig']
    meshes=[bpy.data.objects[n] for n in scene['review_lods'].split('|')]
    rig.animation_data_clear()
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    nose,neutral=neutralize(rig,meshes)
    meta=metadata(rig)
    skel=Skeleton(meta)
    meta['nose_local']=list(skel.inverse[skel.index['Head']]@Vector(nose))
    meta['feet']=[]
    for n,chain in [('ForeHoof_L',['UpperFore_L','Elbow_L','Wrist_L','ForeHoof_L']),
                     ('ForeHoof_R',['UpperFore_R','Elbow_R','Wrist_R','ForeHoof_R']),
                     ('HindHoof_L',['Thigh_L','Knee_L','Hock_L','HindHoof_L']),
                     ('HindHoof_R',['Thigh_R','Knee_R','Hock_R','HindHoof_R'])]:
        idx=meshes[0].vertex_groups[n].index
        points=[v.co.copy() for v in meshes[0].data.vertices if any(g.group==idx and g.weight>.45 for g in v.groups)]
        points=sorted(points,key=lambda v:v.z)
        low=points[:max(3,len(points)//12)]
        sole=sum(low,Vector())/len(low)
        sole.z=min(p.z for p in points)
        meta['feet'].append(dict(bone=n,chain=chain,sole_local=list(skel.inverse[skel.index[n]]@sole)))
    skel=Skeleton(meta)
    # Hooves should be firm end-effectors, not average the ankle and shin
    # transforms. The inferred skin blended these too far down into the hoof.
    sole_points=[skel.rest[skel.index[f['bone']]]@Vector(f['sole_local']) for f in skel.feet]
    for obj in meshes:
        foot_groups=[obj.vertex_groups[f['bone']] for f in skel.feet]
        for v in obj.data.vertices:
            if v.co.z>.25:
                continue
            distances=[(Vector((v.co.x,v.co.y))-Vector((p.x,p.y))).length for p in sole_points]
            k=int(np.argmin(distances))
            if distances[k]>.20:
                continue
            ankle_z=skel.rest[skel.index[skel.feet[k]['bone']]].translation.z
            amount=smooth((ankle_z+.055-v.co.z)/.085)
            if amount<=0:
                continue
            old={g.group:g.weight for g in v.groups}
            target=foot_groups[k].index
            for idx,w in old.items():
                obj.vertex_groups[idx].add([v.index],w*(1-amount),'REPLACE')
            obj.vertex_groups[target].add([v.index],old.get(target,0)*(1-amount)+amount,'REPLACE')
        for v in obj.data.vertices:
            values=sorted([(g.group,g.weight) for g in v.groups if g.weight>1e-8],key=lambda a:a[1],reverse=True)
            keep=values[:4]
            total=sum(w for _,w in keep)
            for idx,_ in values[4:]:
                obj.vertex_groups[idx].remove([v.index])
            for idx,w in keep:
                obj.vertex_groups[idx].add([v.index],w/total,'REPLACE')
    # Direct LBS sampling gives useful floor diagnostics without per-bone bpy
    # updates and catches geometry issues that ankle-only tests cannot see.
    vertices=np.array([list(v.co)+[1] for v in meshes[0].data.vertices])
    ids=np.zeros((len(vertices),4),np.int32)
    skin_w=np.zeros((len(vertices),4),np.float32)
    for v in meshes[0].data.vertices:
        values=sorted([(skel.index[meshes[0].vertex_groups[g.group].name],g.weight) for g in v.groups if g.weight>1e-7],
                      key=lambda a:a[1],reverse=True)[:4]
        total=sum(w for _,w in values)
        for j,(idx,w) in enumerate(values):
            ids[v.index,j]=idx
            skin_w[v.index,j]=w/total
    rest_inv=np.array([np.array(m) for m in skel.inverse])
    def skin_vertices(g):
        skin_m=np.array([np.array(m) for m in g])@rest_inv
        mixed=np.einsum('nk,nkab->nab',skin_w,skin_m[ids],optimize=True)
        return np.einsum('nab,nb->na',mixed,vertices,optimize=True)[:,:3]
    head_group=meshes[0].vertex_groups['Head'].index
    head_ids=np.array([v.index for v in meshes[0].data.vertices if any(
        g.group==head_group and g.weight>.5 for g in v.groups)],np.int32)
    deer=sample_sources('deer',args.deer,skel)
    horse=sample_sources('horse',args.root/'sources/horse-animations.glb',skel)
    for g in horse['Sleep']['globals']:
        skel.fold_rest(g)
        skel.translate_all(g,(0,0,.003-float(skin_vertices(g)[:,2].min())))
    definitions={
        'Idle':(deer['Idle'],True,0),'Walk':(deer['Walk'],True,1.05),
        'Trot':(horse['Trot'],True,2.5),'Run':(deer['Gallop'],True,4.6),
        'Graze':(deer['Eating'],True,0),'Drink':(horse['Eating'],True,0),
        'Alert':(deer['Idle_2'],True,0),'Rest':(horse['Sleep'],True,0),
        'Hit Left':(deer['Idle_HitReact_Left'],False,0),'Hit Right':(deer['Idle_HitReact_Right'],False,0),
        'Death':(deer['Death'],False,0),'Jump':(deer['Gallop_Jump'],False,2.8),
        'Turn Left':(horse['Turn_Left'],False,0),'Turn Right':(horse['Turn_Right'],False,0)}
    # Adapt the authored rise to enter from the authored sternal rest, avoiding
    # the original horse clip's unrelated on-side starting pose.
    stand=deer['Idle']['globals'][0]
    sleep=horse['Sleep']['globals'][0]
    rise=horse['lay_to_idle']['globals']
    segment=rise[round(len(rise)*.30):]
    entry=[]
    a,b=skel.basis(sleep),skel.basis(segment[0])
    for u in np.linspace(0,1,15,endpoint=False):
        entry.append(skel.fk(blend_pose(a,b,smooth(u))))
    get_up=entry+segment
    end_a,end_b=skel.basis(get_up[-1]),skel.basis(stand)
    get_up += [skel.fk(blend_pose(end_a,end_b,smooth(u))) for u in np.linspace(0,1,9)[1:]]
    definitions['Get Up']=(dict(globals=get_up,source_height=np.zeros((len(get_up),4)),
        source='horse:lay_to_idle, sternal-rest entry and standing exit adaptation'),False,0)
    down=resample_pose_frames(list(reversed(get_up)),round(len(get_up)*1.15),skel)
    definitions['Lie Down']=(dict(globals=down,source_height=np.zeros((len(down),4)),
        source='Derived reversed/timed adapted Get Up; prototype descent'),False,0)
    arrays={}
    meta.update(clips={},neutral_head=neutral,fps=24,version=1,
        skeleton_origin='Existing SkinTokens rig, forward head bind correction + non-deforming MotionRoot',
        animation_origin='Quaternius CC0 deer / Mesh2Motion CC0 horse, retarget/adaptation',
        forward=list(F),lods=[o.name for o in meshes])
    mesh=meshes[0]
    foot_history={}
    for name,(record,loop,speed) in definitions.items():
        frames=[[m.copy() for m in g] for g in record['globals']]
        source_height=record['source_height']
        # Native turning becomes an explicit root-motion track, not a twisted
        # skeleton that snaps back when the controller returns to Idle.
        yaws=np.zeros(len(frames))
        shifts=np.zeros((len(frames),3))
        if name.startswith('Turn '):
            pelvis=skel.index['Pelvis']
            for f,g in enumerate(frames):
                dq=g[pelvis].to_quaternion()@skel.rest[pelvis].to_quaternion().inverted()
                direction=dq@F
                yaws[f]=wrap_angle(math.atan2(direction.y,direction.x)-math.atan2(F.y,F.x))
            yaws=np.unwrap(yaws)
            yaws-=yaws[0]
            in_place=float(np.ptp(yaws))<math.radians(15)
            if in_place:
                sign=-1 if name=='Turn Left' else 1
                yaws=np.array([sign*math.pi/2*smooth(u) for u in np.linspace(0,1,len(frames))])
            for f,g in enumerate(frames):
                if in_place:
                    continue
                q=Quaternion(Z,float(yaws[f]))
                p=g[pelvis].translation
                rest_p=skel.rest[pelvis].translation
                shifts[f,:2]=np.array(p-q@rest_p)[:2]
                root_m=matrix(q,Vector(shifts[f]))
                inverse=root_m.inverted()
                for i in range(1,len(g)):
                    g[i]=inverse@g[i]
        heights=source_height-source_height.min(axis=0,keepdims=True)
        contacts=np.clip((.065-heights)/.04,0,1)
        if name in ('Idle','Graze','Drink','Alert'):
            contacts[:]=1
        if name in ('Rest','Lie Down','Get Up','Death'):
            contacts[:]=0
        local_frames=[]
        foot_positions=[]
        locks=[None]*4
        floor_lifts=[]
        for f,g in enumerate(frames):
            if name in ('Graze','Drink'):
                skel.reach_ground(g,.07 if name=='Graze' else .10)
            if name=='Alert':
                skel.gaze(g,0,math.radians(3),weight=1)
            if name in ('Get Up','Lie Down','Rest','Alert','Idle','Walk','Trot','Run'):
                head=skel.index['Head']
                aim=g[head].to_quaternion()@(skel.rest[head].to_quaternion().inverted()@F)
                pitch=math.asin(max(-1,min(1,aim.z)))
                limit=max(math.radians(-65),min(math.radians(25),pitch))
                if abs(pitch-limit)>.001:
                    horizontal=Vector((aim.x,aim.y,0)).normalized()
                    desired=horizontal*math.cos(limit)+Z*math.sin(limit)
                    skel.rotate_branch(g,head,aim.rotation_difference(desired))
            actor=matrix(Quaternion(Z,float(yaws[f])),Vector(shifts[f]))
            if name.startswith('Turn '):
                for k,foot in enumerate(skel.feet):
                    if contacts[f,k]>.5 and locks[k] is None:
                        locks[k]=actor@skel.sole(g,foot)
                    elif contacts[f,k]<.2:
                        locks[k]=None
            diag=skel.ground_feet(g,contacts[f],actor=actor,locks=locks)
            for _ in range(2):
                gaps=[gap for k,gap in diag if contacts[f,k]>.8]
                high=max(gaps,default=0)
                if high<.012:
                    break
                skel.translate_all(g,(0,0,-min(.15,high)))
                diag=skel.ground_feet(g,contacts[f],actor=actor,locks=locks)
            # Keep feeding muzzles above the ground using actual skinned
            # vertices, rather than assuming the AI head joint is its nose.
            if name in ('Graze','Drink'):
                low=float(skin_vertices(g)[head_ids,2].min())
                for _ in range(14):
                    if low>=.012:
                        break
                    idx=skel.index['Neck3']
                    tip=g[skel.index['Head']]@skel.nose
                    axis=(tip-g[idx].translation).cross(Z)
                    if axis.length<.001:
                        axis=-R
                    skel.rotate_branch(g,idx,Quaternion(axis.normalized(),math.radians(2)))
                    low=float(skin_vertices(g)[head_ids,2].min())
            low=float(skin_vertices(g)[:,2].min())
            lift=max(0,.003-low)
            # A fallen/resting body has no standing hoof constraints; its
            # lowest actual surface defines the floor contact.
            if lift>0:
                skel.translate_all(g,(0,0,lift))
            floor_lifts.append(lift)
            local_frames.append(skel.basis(g))
            foot_positions.append([list(skel.sole(g,foot)) for foot in skel.feet])
        loc=np.array([p[0] for p in local_frames])
        rot=np.array([p[1] for p in local_frames])
        # Quaternion signs must be continuous for exports and interpolation.
        for f in range(1,len(rot)):
            for b in range(len(skel.names)):
                if rot[f,b]@rot[f-1,b]<0:
                    rot[f,b]*=-1
        if loop:
            positions=np.array(foot_positions)
            similar=float(np.max(np.linalg.norm(positions[-1]-positions[0],axis=1)))<.035
            if similar:
                loc[-1]=loc[0];rot[-1]=rot[0];contacts[-1]=contacts[0]
            else:
                loc=np.concatenate([loc,loc[:1]])
                rot=np.concatenate([rot,rot[:1]])
                contacts=np.concatenate([contacts,contacts[:1]])
                yaws=np.concatenate([yaws,yaws[:1]])
                shifts=np.concatenate([shifts,shifts[:1]])
        count=len(loc)
        arrays[name+'_loc']=loc.astype(np.float32)
        arrays[name+'_rot']=rot.astype(np.float32)
        arrays[name+'_contact']=contacts.astype(np.float32)
        arrays[name+'_yaw']=yaws.astype(np.float32)
        arrays[name+'_shift']=shifts.astype(np.float32)
        phase_offset=0
        if loop and speed>0:
            for k in range(count-1):
                if contacts[k,0]<.5 and contacts[k+1,0]>=.5:
                    phase_offset=(k+1)/24
                    break
        meta['clips'][name]=dict(frames=count,fps=24,duration=(count-1)/24,loop=loop,speed=speed,
            source=record['source'],root_yaw_end=float(yaws[-1]),root_shift_end=shifts[-1].tolist(),
            phase_offset=phase_offset,
            maximum_floor_lift=max(floor_lifts),
            procedural_gaze=name not in ('Graze','Drink','Lie Down','Rest','Get Up','Death'))
        print('PREPARED',name,count,flush=True)
    np.savez_compressed(args.root/'deer-motion-library.npz',**arrays)
    (args.root/'deer-motion-library.json').write_text(json.dumps(meta,indent=2))
    # Bake portable native actions. Root motion is separate from the pose data
    # used by the viewer; this gives engines an actual root extraction node.
    for name,info in meta['clips'].items():
        action=bpy.data.actions.new(name)
        action.use_fake_user=True
        rig.animation_data_create().action=action
        for f in range(info['frames']):
            scene.frame_set(f)
            for i,n in enumerate(skel.names):
                pb=rig.pose.bones[n]
                pb.rotation_mode='QUATERNION'
                pb.location=arrays[name+'_loc'][f,i]
                pb.rotation_quaternion=arrays[name+'_rot'][f,i]
                if n=='MotionRoot':
                    pb.location=arrays[name+'_shift'][f]
                    pb.rotation_quaternion=Quaternion(Z,float(arrays[name+'_yaw'][f]))
                pb.scale=(1,1,1)
                pb.keyframe_insert('location',frame=f)
                pb.keyframe_insert('rotation_quaternion',frame=f)
            # Native exports use linear keys; avoid spline overshoot below soil.
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points:
                            key.interpolation='LINEAR'
    rig.animation_data.action=bpy.data.actions['Idle']
    if rig.animation_data.action.slots:
        rig.animation_data.action_slot=rig.animation_data.action.slots[0]
    scene.frame_start=0
    scene.frame_end=24*90-1
    scene.frame_set(0)
    actor=bpy.data.objects.new('Deer Actor',None)
    scene.collection.objects.link(actor)
    world=rig.matrix_world.copy()
    rig.parent=actor
    rig.matrix_world=world
    floor=bpy.data.objects.get('Studio floor (not part of animal)')
    if floor:
        floor.scale*=8
    scene['deer_library_root']=str(args.root)
    scene['deer_behaviour_viewer']=True
    scene['review_clips']='|'.join(meta['clips'])
    scene['review_approved_motion']=True
    scene['review_status']='Behaviour/IK prototype; native clips and export are separate from Blender controller'
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.show_region_ui=True
                area.spaces.active.overlay.show_overlays=False
                area.spaces.active.region_3d.view_distance=4.3
                area.spaces.active.region_3d.view_location=Vector((0,0,.8))
    for obj in scene.objects:
        obj.select_set(False)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active=mesh
    for i,obj in enumerate(meshes):
        obj.hide_set(i!=0)
        obj.hide_render=i!=0
    bpy.ops.wm.save_as_mainfile(filepath=str(output))
    print('SAVED',output,flush=True)


if __name__=='__main__':
    main()
