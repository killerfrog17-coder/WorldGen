"""Diagnostic: CC0 Quaternius deer motion on our accepted AI mesh/AI skin.

Does not substitute the donor mesh. Uses world-space deformation rotations,
including bind-axis corrections; not direct copying of local Euler channels.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Matrix, Vector


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--review',type=Path,required=True)
    ap.add_argument('--donor',type=Path,required=True)
    ap.add_argument('--output',type=Path,required=True)
    args=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
    args.review,args.donor,args.output=[p.resolve() for p in (args.review,args.donor,args.output)]
    if args.output.exists():
        raise FileExistsError(args.output)
    bpy.ops.wm.open_mainfile(filepath=str(args.review))
    scene=bpy.context.scene
    scene.render.fps=24
    target=bpy.data.objects['AI Deer Rig']
    mesh=bpy.data.objects['Locally generated deer']
    target.animation_data_clear()
    for pb in target.pose.bones:
        pb.matrix_basis.identity()
    before=set(scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(args.donor))
    imported=[o for o in scene.objects if o not in before]
    donor=next(o for o in imported if o.type=='ARMATURE')
    donor.animation_data_clear()
    # Donor faces world -Y; generated doe faces diagonally -X/-Y.
    forward=Vector((-.91514678,-.40312079,0))
    right=Vector((-forward.y,forward.x,0))
    rotate=Matrix((right,-forward,Vector((0,0,1)))).transposed().to_quaternion()
    mapping={'Pelvis':'Back','Spine1':'Torso','Spine2':'Torso2','Chest':'Torso3',
        'Neck1':'Neck1','Neck2':'Neck2','Neck3':'Neck3','Head':'Head',
        'Ear_L':'Ear1.L','Ear_R':'Ear1.R','Jaw':'Head','Muzzle_R':'Head','Muzzle_L':'Head',
        'Tail1':'Tail1','Tail2':'Tail3'}
    for side in ('L','R'):
        mapping.update({f'Shoulder_{side}':f'FrontShoulder.{side}',
            f'UpperFore_{side}':f'FrontUpperLeg.{side}',f'Elbow_{side}':f'FrontLowerLeg.{side}',
            f'Wrist_{side}':f'FrontLowerLeg.{side}',f'ForeHoof_{side}':f'IKFrontLeg.{side}',
            f'Thigh_{side}':f'BackLeg.{side}',f'Knee_{side}':f'BackUpperLeg.{side}',
            f'Hock_{side}':f'BackLowerLeg.{side}',f'HindHoof_{side}':f'IKBackLeg.{side}'})
    bones=list(target.data.bones)
    rests={b.name:b.matrix_local.copy() for b in bones}
    donor_rest={n:(donor.matrix_world@donor.data.bones[n].matrix_local).to_quaternion()
                for n in set(mapping.values())}
    hip_height=(donor.matrix_world@donor.data.bones['Back'].head_local).z
    scale=rests['Pelvis'].translation.z/hip_height
    samples={}
    for source_name,out_name in [('Idle','Idle'),('Walk','Walk'),('Gallop','Run')]:
        action=bpy.data.actions[source_name]
        donor.animation_data_create().action=action
        if action.slots:
            donor.animation_data.action_slot=action.slots[0]
        frames=[]
        for f in range(int(action.frame_range[0]),int(action.frame_range[1])+1):
            scene.frame_set(f)
            bpy.context.view_layer.update()
            rotations={}
            for n in set(mapping.values()):
                q=(donor.matrix_world@donor.pose.bones[n].matrix).to_quaternion()
                rotations[n]=rotate@(q@donor_rest[n].inverted())@rotate.inverted()
            z=(donor.matrix_world@donor.pose.bones['Back'].head).z
            frames.append((rotations,(z-hip_height)*scale))
        samples[out_name]=frames
    # The donor was solely an animation source. No donor geometry in output.
    for obj in imported:
        bpy.data.objects.remove(obj,do_unlink=True)
    for action in list(bpy.data.actions):
        if action.users==0 or action.name.startswith('AI motion'):
            bpy.data.actions.remove(action)
    checks=[]
    for name,frames in samples.items():
        action=bpy.data.actions.new(name)
        action.use_fake_user=True
        target.animation_data_create().action=action
        for pb in target.pose.bones:
            pb.rotation_mode='QUATERNION'
            pb.matrix_basis.identity()
        for f,(rotations,lift) in enumerate(frames):
            scene.frame_set(f)
            globals_m={}
            for bone in bones:
                n=bone.name
                q=rotations[mapping[n]]@rests[n].to_quaternion()
                if n in ('Ear_L','Ear_R','Jaw','Muzzle_R','Muzzle_L','Tail2'):
                    q=globals_m[bone.parent.name].to_quaternion()@(
                        rests[bone.parent.name].to_quaternion().inverted()@rests[n].to_quaternion())
                m=q.to_matrix().to_4x4()
                if bone.parent:
                    m.translation=globals_m[bone.parent.name]@rests[bone.parent.name].inverted()@rests[n].translation
                else:
                    m.translation=rests[n].translation+Vector((0,0,lift))
                globals_m[n]=m
                pb=target.pose.bones[n]
                pb.matrix=m
                bpy.context.view_layer.update()
                pb.keyframe_insert('location',frame=f)
                pb.keyframe_insert('rotation_quaternion',frame=f)
                pb.keyframe_insert('scale',frame=f)
        # A minimal non-penetration correction for this diagnostic. This is not
        # foot locking, IK, or proof of believable locomotion/contact.
        lows=[]
        for f in range(len(frames)):
            scene.frame_set(f)
            ev=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
            lows.append(min(v.co.z for v in ev.data.vertices))
        lifts=np.maximum(0,-np.array(lows))
        smoothed=np.convolve(np.pad(lifts,(1,1),mode='wrap'),[.25,.5,.25],mode='valid')
        lifts=np.maximum(lifts,smoothed)
        for f,lift in enumerate(lifts):
            scene.frame_set(f)
            pb=target.pose.bones['Pelvis']
            m=pb.matrix.copy()
            m.translation.z+=float(lift)
            pb.matrix=m
            pb.keyframe_insert('location',frame=f)
        bounds=[]
        for f in range(len(frames)):
            scene.frame_set(f)
            ev=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
            xyz=np.array([v.co[:] for v in ev.data.vertices])
            if not np.isfinite(xyz).all():
                raise ValueError('Non-finite mesh')
            bounds.append([float(xyz[:,2].min()),float(xyz[:,2].max())])
        checks.append(dict(action=name,frames=len(frames),lowest_before_correction=min(lows),
            max_root_lift=float(lifts.max()),lowest=min(p[0] for p in bounds),
            highest=max(p[1] for p in bounds)))
    scene.render.fps=24
    scene['review_clips']='Idle|Walk|Run'
    scene['review_clip']='Walk'
    scene['review_loop_closed']=True
    # Source Idle/Walk duplicate their first pose at the end; Gallop has a
    # distinct final pose and needs every source frame when cycling.
    scene['review_closed_clips']='Idle|Walk'
    scene['review_autoplay']=True
    scene['motion_origin']='Quaternius authored CC0 deer clips; bind-corrected retarget, not AI motion'
    scene['rig_origin']='Same SkinTokens rig and skin as rejected AnyTop review; diagnostic control'
    scene['review_status']='Motion-source comparison; rig/skin NOT accepted'
    target.animation_data.action=bpy.data.actions['Walk']
    if target.animation_data.action.slots:
        target.animation_data.action_slot=target.animation_data.action.slots[0]
    scene.frame_start=0
    scene.frame_end=len(samples['Walk'])-2
    scene.frame_set(4)
    bpy.ops.object.select_all(action='DESELECT')
    mesh.select_set(True)
    bpy.context.view_layer.objects.active=mesh
    args.output.with_suffix('.json').write_text(json.dumps(dict(
        donor=str(args.donor),donor_sha256=hashlib.sha256(args.donor.read_bytes()).hexdigest(),
        credit='Quaternius, Ultimate Animated Animals, CC0',
        model='Our accepted TripoSG mesh and Hunyuan UV texture; no donor mesh',
        skin='Unchanged SkinTokens weights',checks=checks),indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(args.output))
    # A contact sheet is used for our own offline visual diagnosis.
    scene.cycles.samples=12
    scene.render.resolution_percentage=60
    for name,f in [('Walk',4),('Walk',11),('Run',3),('Run',8)]:
        target.animation_data.action=bpy.data.actions[name]
        if target.animation_data.action.slots:
            target.animation_data.action_slot=target.animation_data.action.slots[0]
        scene.frame_set(f)
        scene.render.filepath=str(args.output.parent/f'authored-{name.lower()}-{f}.png')
        bpy.ops.render.render(write_still=True)


if __name__=='__main__':
    main()
