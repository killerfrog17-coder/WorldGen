"""Private Blender review: our mesh + Hunyuan UV paint + SkinTokens + AnyTop.

AnyTop checkpoint terms are RESEARCH ONLY. No output enters the game. Uses
hierarchical rotation retargeting: fixed target bone lengths, not direct moving
joint positions or hand-invented foot curves. Review all takes before acceptance.
"""
import argparse
import json
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Vector,Matrix,Quaternion
from mathutils.kdtree import KDTree


def frame(direction,hint):
    y=Vector(direction).normalized()
    z=Vector(hint)-y*Vector(hint).dot(y)
    if z.length<.001:
        z=Vector((0,1,0))-y*y.y
    z.normalize()
    x=y.cross(z).normalized()
    z=x.cross(y).normalized()
    return Matrix((x,y,z)).transposed().to_quaternion()


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--studio',type=Path,required=True)
    ap.add_argument('--rig',type=Path,required=True)
    ap.add_argument('--motions',type=Path,required=True)
    ap.add_argument('--output',type=Path,required=True)
    ap.add_argument('--render',action='store_true')
    args=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
    args.studio,args.rig,args.motions,args.output=[p.resolve() for p in [args.studio,args.rig,args.motions,args.output]]
    if args.output.exists():
        raise FileExistsError(args.output)
    bpy.ops.wm.open_mainfile(filepath=str(args.studio))
    mesh=bpy.data.objects['Locally generated deer']
    data=np.load(args.rig)
    raw=data['source_vertices']
    low,high=raw.min(0),raw.max(0)
    offset=np.array([(low[0]+high[0])/2,(low[1]+high[1])/2,low[2]])
    scale=1.65/(high[2]-low[2])
    joints=(data['joints']-offset)*scale
    points=(data['sample_points']-offset)*scale
    parents=data['parents']
    weights=data['sample_skin']
    children=[list(np.flatnonzero(parents==i)) for i in range(len(parents))]
    # Semantic correspondences inspected on this generated 33-joint skeleton.
    mapping={0:0,1:14,2:15,3:17,4:18,5:19,6:21,7:23,8:26,9:24,10:25,11:27,12:28,
        13:29,14:30,15:31,16:32,17:33,18:35,19:36,20:37,21:38,22:39,
        23:4,24:5,25:6,26:7,27:2,28:3,29:9,30:10,31:11,32:12}
    if len(parents)!=33:
        raise ValueError('Landmark mapping is for the inspected Z-up 33-joint rig')
    names=['Pelvis','Spine1','Spine2','Chest','Neck1','Neck2','Neck3','Head','Ear_L','Ear_R','Jaw','Muzzle_R','Muzzle_L',
        'Shoulder_R','UpperFore_R','Elbow_R','Wrist_R','ForeHoof_R','Shoulder_L','UpperFore_L','Elbow_L','Wrist_L','ForeHoof_L',
        'Thigh_R','Knee_R','Hock_R','HindHoof_R','Tail1','Tail2','Thigh_L','Knee_L','Hock_L','HindHoof_L']
    body_forward=Vector((-.91514678,-.40312079,0))
    body_right=Vector((.40312079,-.91514678,0))
    up=Vector((0,0,1))
    source_to_world=Matrix((body_right,up,body_forward)).transposed()
    source_to_world_np=np.array(source_to_world)
    bpy.ops.object.select_all(action='DESELECT')
    arm=bpy.data.armatures.new('SkinTokens generated skeleton')
    rig=bpy.data.objects.new('AI Deer Rig',arm)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active=rig
    rig.select_set(True)
    rig.show_in_front=True
    bpy.ops.object.mode_set(mode='EDIT')
    target_directions=[]
    for i,parent in enumerate(parents):
        bone=arm.edit_bones.new(names[i])
        bone.head=joints[i]
        if len(children[i])==1:
            direction=Vector(joints[children[i][0]]-joints[i])
        elif i==0:
            direction=Vector(joints[1]-joints[0])
        elif parent>=0:
            direction=Vector(joints[i]-joints[parent])*.3
        else:
            direction=Vector((0,0,.06))
        if direction.length<.01:
            direction=Vector((0,0,.04))
        bone.tail=bone.head+direction
        hint=body_forward if i in list(range(13,27))+list(range(29,33)) else up
        bone.align_roll(hint)
        if parent>=0:
            bone.parent=arm.edit_bones[names[parent]]
        target_directions.append(direction)
    bpy.ops.object.mode_set(mode='OBJECT')
    tree=KDTree(len(points))
    for i,p in enumerate(points):
        tree.insert(p,i)
    tree.balance()
    groups=[mesh.vertex_groups.new(name=n) for n in names]
    vertex_positions=np.array([v.co[:] for v in mesh.data.vertices])
    bone_distances=[]
    for name in names:
        bone=arm.bones[name]
        a,b=np.array(bone.head_local),np.array(bone.tail_local)
        direction=b-a
        along=np.clip((vertex_positions-a)@direction/max(direction@direction,1e-8),0,1)
        bone_distances.append(np.linalg.norm(vertex_positions-(a+along[:,None]*direction),axis=1))
    bone_distances=np.stack(bone_distances,axis=1)
    nearest_distance=bone_distances.min(axis=1,keepdims=True)
    sigma=.065+.18*np.clip((vertex_positions[:,2]-.6)/.35,0,1)
    anatomical_gate=np.exp(-.5*((bone_distances-nearest_distance)/sigma[:,None])**2)
    for vertex in mesh.data.vertices:
        nearest=tree.find_n(vertex.co,4)
        indices=np.array([n[1] for n in nearest])
        distances=np.array([n[2] for n in nearest])
        blend=1/np.maximum(distances,.0001)**2
        predicted=(weights[indices]*blend[:,None]).sum(0)/blend.sum()
        # The model predicts independent skin occupancies; occasionally two
        # distant limbs both receive near-1 values. Match the purpose of its
        # optional geometric postprocess by suppressing remote influences.
        predicted*=anatomical_gate[vertex.index]
        best=np.argsort(predicted)[-4:]
        normalized=predicted[best]/max(predicted[best].sum(),1e-8)
        for j,w in zip(best,normalized):
            groups[j].add([vertex.index],float(w),'REPLACE')
    modifier=mesh.modifiers.new('AI generated skin weights','ARMATURE')
    modifier.object=rig
    modifier.use_deform_preserve_volume=True
    mesh.parent=rig
    rest_target=[arm.bones[n].matrix_local.copy() for n in names]
    rest_target_q=[m.to_quaternion() for m in rest_target]
    rest_target_local=[]
    for i,parent in enumerate(parents):
        rest_target_local.append(rest_target_q[parent].inverted()@rest_target_q[i] if parent>=0 else rest_target_q[i])
    scene=bpy.context.scene
    scene.render.fps=20
    reports=[]
    toe_endpoints={17:34,22:40,26:8,32:13}
    # Foot reference points come from the actual mesh/AI skin rather than the
    # predicted ankle joint, which can sit well above the hoof sole.
    toe_rest={}
    for bone_index in toe_endpoints:
        candidates=[v.co.copy() for v in mesh.data.vertices if
            any(g.group==groups[bone_index].index and g.weight>.3 for g in v.groups)]
        toe_rest[bone_index]=min(candidates,key=lambda p:p.z) if candidates else Vector(joints[bone_index])
    for take,path in enumerate(sorted(args.motions.glob('take-*.npz'))):
        source=np.load(path)
        position=source['positions']
        rest=source['rest']
        def source_frames(p):
            transformed=p@source_to_world_np.T
            frames=[]
            for i in range(len(parents)):
                if i==0:
                    rear=(transformed[4]+transformed[9])/2
                    front=(transformed[29]+transformed[35])/2
                    direction=front-rear
                elif len(children[i])==1:
                    direction=transformed[mapping[children[i][0]]]-transformed[mapping[i]]
                elif i in toe_endpoints:
                    direction=transformed[toe_endpoints[i]]-transformed[mapping[i]]
                else:
                    direction=transformed[mapping[i]]-transformed[mapping[parents[i]]]
                hint=body_forward if i in list(range(13,27))+list(range(29,33)) else up
                frames.append(frame(direction,hint))
            return frames
        reference=source_frames(rest)
        source_local_rest=[reference[p].inverted()@reference[i] if p>=0 else reference[i] for i,p in enumerate(parents)]
        action=bpy.data.actions.new(f'AI motion {take+1} - research only')
        rig.animation_data_create().action=action
        action.use_fake_user=True
        for pb in rig.pose.bones:
            pb.rotation_mode='QUATERNION'
            pb.matrix_basis.identity()
        for index in range(len(position)):
            scene.frame_set(index)
            current=source_frames(position[index])
            globals_q=[]
            globals_m=[]
            for i,parent in enumerate(parents):
                if parent<0:
                    q=(current[i]@reference[i].inverted())@rest_target_q[i]
                    translation=Vector(joints[i])
                    # Preserve a restrained vertical body response; horizontal
                    # root travel is removed for an in-place inspection loop.
                    translation.z+=float(position[index,0,1]-rest[0,1])*.85
                else:
                    src_local=current[parent].inverted()@current[i]
                    delta=source_local_rest[i].inverted()@src_local
                    q=globals_q[parent]@rest_target_local[i]@delta
                    translation=globals_m[parent]@rest_target[parent].inverted()@Vector(joints[i])
                # Tiny facial/ear motion is not inferred robustly by this body
                # model. Follow the parent rather than deforming the muzzle.
                if i in (8,9,10,11,12,28):
                    q=globals_q[parent]@rest_target_local[i]
                matrix=q.to_matrix().to_4x4()
                matrix.translation=translation
                globals_q.append(q)
                globals_m.append(matrix)
            # Ground the currently contacting feet after differing source/target
            # limb lengths have been retargeted. Keep airborne phases airborne.
            source_foot_height={i:position[index,end,1] for i,end in toe_endpoints.items()}
            contact=[i for i,height in source_foot_height.items() if height<.09]
            if contact:
                gap=min((globals_m[i]@rest_target[i].inverted()@toe_rest[i]).z for i in contact)
                shift=max(-.18,min(.18,-gap))
                for matrix in globals_m:
                    matrix.translation.z+=shift
            for i,matrix in enumerate(globals_m):
                pb=rig.pose.bones[names[i]]
                pb.matrix=matrix
                bpy.context.view_layer.update()
                pb.keyframe_insert('location',frame=index)
                pb.keyframe_insert('rotation_quaternion',frame=index)
                pb.keyframe_insert('scale',frame=index)
        reports.append({'action':action.name,'frames':len(position),'source':str(path)})
        # Retargeting can put the mesh's actual hoof sole below a target joint
        # reference. Lift the root only as needed, keeping all neural joint
        # rotations and bone lengths; do not invent replacement limb motion.
        penetrations=[]
        for keyframe in range(len(position)):
            scene.frame_set(keyframe)
            evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
            lowest=min(v.co.z for v in evaluated.data.vertices)
            penetrations.append(max(0,-lowest))
        smoothed=np.convolve(np.pad(penetrations,(2,2),mode='edge'),
            np.array([.054489,.244201,.402620,.244201,.054489]),mode='valid')
        correction=np.maximum(penetrations,smoothed)
        for keyframe,lift in enumerate(correction):
            if lift<=1e-6:
                continue
            scene.frame_set(keyframe)
            root=rig.pose.bones[names[0]]
            matrix=root.matrix.copy()
            matrix.translation.z+=float(lift)
            root.matrix=matrix
            root.keyframe_insert('location',frame=keyframe)
        print('Retargeted',action.name,flush=True)
    scene['review_clips']='|'.join(r['action'] for r in reports)
    scene['review_loop_closed']=False
    scene['review_autoplay']=True
    scene['review_clip']=reports[0]['action']
    scene['motion_origin']='AnyTop neural motion diffusion - NON-COMMERCIAL RESEARCH ONLY'
    scene['rig_origin']='Local SkinTokens neural skeleton and skin weights'
    scene['review_status']='Private pipeline evaluation; not a redistributable game asset'
    rig.animation_data.action=bpy.data.actions[reports[0]['action']]
    scene.frame_start,scene.frame_end=0,reports[0]['frames']-1
    scene.frame_set(0)
    rig.select_set(False)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active=mesh
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.overlay.show_overlays=False
                area.spaces.active.show_region_ui=False
    args.output.with_suffix('.json').write_text(json.dumps({'bones':len(parents),'rig':'SkinTokens Z-up prediction',
        'motion':'AnyTop learned takes, retargeted rotations, fixed target bone lengths',
        'clips':reports,'terms':'PRIVATE NON-COMMERCIAL RESEARCH ONLY'},indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(args.output))
    if args.render:
        scene.cycles.samples=12
        scene.render.resolution_percentage=70
        for take in range(len(reports)):
            rig.animation_data.action=bpy.data.actions[reports[take]['action']]
            scene.frame_set(30)
            scene.render.filepath=str(args.output.parent/f'ai-motion-{take+1}.png')
            bpy.ops.render.render(write_still=True)


if __name__=='__main__':
    main()
