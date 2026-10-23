"""Author a study rig and walk/run loops on the NEW local TripoSG deer.

Landmarks below were measured on the generated study mesh, not a downloaded
skeleton. These are asset-specific study coordinates, not a general autorigger.
"""
import argparse
import json
import math
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Matrix, Vector


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--source', type=Path, required=True)
    ap.add_argument('--output', type=Path, required=True)
    ap.add_argument('--render', action='store_true')
    args = ap.parse_args(sys.argv[sys.argv.index('--')+1:])
    # Opening a .blend may change Blender's working directory on Windows.
    args.source = args.source.resolve()
    args.output = args.output.resolve()
    if args.output.exists():
        raise FileExistsError(args.output)
    bpy.ops.wm.open_mainfile(filepath=str(args.source.resolve()))
    obj = bpy.data.objects['Locally generated deer']
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    # The inferred animal is diagonally oriented. Canonicalize its torso axis;
    # preserve the original UV projection and untouched raw GLB alongside it.
    angle = -math.atan2(.40312079,.91514678)
    rotation = Matrix.Rotation(angle, 4, 'Z')
    for v in obj.data.vertices:
        v.co = rotation @ v.co
    for mod in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    arm = bpy.data.armatures.new('Deer study skeleton')
    rig = bpy.data.objects.new('Generated Deer Rig', arm)
    bpy.context.collection.objects.link(rig)
    obj.select_set(False)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    rig.show_in_front = True
    bpy.ops.object.mode_set(mode='EDIT')
    records = {}
    def bone(name, head, tail, parent=None, deform=True):
        b = arm.edit_bones.new(name)
        b.head, b.tail = head, tail
        b.use_deform = deform
        if parent:
            b.parent = arm.edit_bones[parent]
        records[name] = (np.array(head),np.array(tail),deform)
        return b
    bone('Root',(0,.16,.85),(0,.16,1.0),deform=False)
    bone('Body',(.65,.16,1.02),(-.24,.16,1.02),'Root')
    bone('Neck',(-.24,.16,1.02),(-.59,.08,1.38),'Body')
    bone('Head',(-.59,.08,1.38),(-.82,-.04,1.30),'Neck')
    bone('Tail',(.72,.16,1.13),(.82,.16,.99),'Body')
    # Coordinates follow the actual four legs, including the standing offset.
    chains = {
        'Fore_L':[(-.18,.055,.98),(-.138,.064,.60),(-.172,.041,.13),(-.23,.016,.025)],
        'Fore_R':[(-.29,.30,.98),(-.218,.319,.60),(-.307,.319,.13),(-.37,.326,.025)],
        'Hind_L':[(.65,.07,1.02),(.63,.075,.73),(.807,.033,.45),(.845,.001,.13),(.79,-.027,.025)],
        'Hind_R':[(.65,.29,1.02),(.59,.29,.73),(.644,.281,.45),(.51,.292,.13),(.43,.298,.025)],
    }
    for key, points in chains.items():
        parent = 'Body'
        for i,(h,t) in enumerate(zip(points[:-1],points[1:])):
            name = key+'_'+str(i)
            bone(name,h,t,parent)
            parent = name
        ankle,toe = Vector(points[-2]),Vector(points[-1])
        bone('CTRL_'+key, ankle,toe, deform=False)
        # Two-bone fore legs and three-bone hind legs bend in their own plane.
        pole = Vector(points[1])+Vector((1 if key.startswith('Fore') else -1,0,0))
        bone('POLE_'+key,pole,pole+Vector((0,0,.1)),deform=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    # Geometric segment weights, blended across joints and shoulder attachment.
    # A bounded four-influence skin is suitable for this preview, not claimed
    # production retopology or anatomically calibrated musculature.
    vertices = np.array([v.co[:] for v in obj.data.vertices])
    deform = [(n,h,t) for n,(h,t,d) in records.items() if d]
    distances = []
    for name,h,t in deform:
        direction = t-h
        u = np.clip((vertices-h)@direction/(direction@direction),0,1)
        closest = h+u[:,None]*direction
        distance = np.linalg.norm(vertices-closest,axis=1)
        # Do not let a long leg bone steal the underside of the torso, or torso
        # weights pin isolated lower limbs while they move.
        if name.startswith(('Fore','Hind')):
            distance += np.maximum(vertices[:,2]-.78,0)*1.6
        elif name in ('Body','Neck','Head','Tail'):
            distance += np.maximum(.70-vertices[:,2],0)*2
        distances.append(distance)
    distances = np.stack(distances,axis=1)
    weights = 1/(distances+.025)**5
    # The belly is a broad surface far below the spine centre. Pure nearest-
    # segment weights otherwise mistake it for a nearby upper leg and create
    # a swinging lump. Keep that central torso on the body, blending at ends.
    x,z = vertices[:,0],vertices[:,2]
    torso = np.clip((x+.14)/.12,0,1)*np.clip((.58-x)/.12,0,1)*np.clip((z-.60)/.10,0,1)
    for j,(name,h,t) in enumerate(deform):
        if name != 'Body':
            weights[:,j] *= 1-torso
    body_index = next(j for j,(name,h,t) in enumerate(deform) if name == 'Body')
    weights[:,body_index] += torso*weights.max(axis=1)*12
    keep = np.argsort(weights,axis=1)[:,-4:]
    retained = np.zeros_like(weights)
    rows = np.arange(len(vertices))[:,None]
    retained[rows,keep] = weights[rows,keep]
    retained /= retained.sum(axis=1)[:,None]
    for j,(name,h,t) in enumerate(deform):
        group = obj.vertex_groups.new(name=name)
        for index in np.flatnonzero(retained[:,j]>.001):
            group.add([int(index)],float(retained[index,j]),'REPLACE')
    modifier = obj.modifiers.new('Generated deer skin', 'ARMATURE')
    modifier.object = rig
    modifier.use_deform_preserve_volume = True
    obj.parent = rig
    for key, points in chains.items():
        count = len(points)-2
        end = rig.pose.bones[key+'_'+str(count-1)]
        ik = end.constraints.new('IK')
        ik.target = rig
        ik.subtarget = 'CTRL_'+key
        ik.chain_count = count
        ik.use_tail = True
        ik.iterations = 100
        # Default rest-plane IK avoids introducing a pole-angle discontinuity
        # in these slightly offset generated limbs.
        hoof = rig.pose.bones[key+'_'+str(count)]
        hold = hoof.constraints.new('COPY_ROTATION')
        hold.target = rig
        hold.subtarget = 'CTRL_'+key
        hold.target_space = 'WORLD'
        hold.owner_space = 'WORLD'
    scene = bpy.context.scene
    scene.render.fps = 24
    def delta_location(pb, delta):
        pb.location = pb.bone.matrix_local.to_3x3().inverted() @ Vector(delta)
    for clip,frames,stance,stride,lift in [('Idle',96,1,0,0),('Walk',48,.66,.42,.12),('Run',24,.34,.70,.25)]:
        rig.animation_data_create().action = bpy.data.actions.new(clip)
        action = rig.animation_data.action
        action.use_fake_user = True
        for frame in range(frames+1):
            phase = frame/frames
            root = rig.pose.bones['Root']
            bob = 0 if clip == 'Idle' else (-.045+.012*math.cos(phase*4*math.pi) if clip == 'Walk' else -.11+.055*math.sin(phase*2*math.pi))
            delta_location(root,(0,0,bob))
            root.keyframe_insert('location',frame=frame)
            neck = rig.pose.bones['Neck']
            neck.rotation_mode = 'XYZ'
            neck.rotation_euler[0] = (.008 if clip == 'Idle' else .012)*math.sin(phase*2*math.pi)
            neck.keyframe_insert('rotation_euler',frame=frame)
            offsets = {'Fore_L':0,'Hind_R':.25,'Fore_R':.5,'Hind_L':.75} if clip == 'Walk' else {'Hind_L':0,'Hind_R':.1,'Fore_L':.48,'Fore_R':.58}
            for key in chains:
                t = (phase+offsets[key])%1
                if clip == 'Idle':
                    dx,dz = 0,0
                elif t < stance:
                    dx,dz = -stride/2+stride*t/stance,0
                else:
                    swing = (t-stance)/(1-stance)
                    # Smoothstep return avoids a position/velocity discontinuity
                    # at the highest point; planted phase moves at steady speed.
                    dx = stride/2-stride*(swing*swing*(3-2*swing))
                    dz = lift*math.sin(math.pi*swing)**1.3
                control = rig.pose.bones['CTRL_'+key]
                delta_location(control,(dx,0,dz))
                control.keyframe_insert('location',frame=frame)
        print('Authored',clip,'frames',frames,flush=True)
    # Freeze constraints into ordinary FK keyframes per clip. This removes
    # preview solver dependencies and makes every clip self contained.
    source_actions = [bpy.data.actions[n] for n in ('Idle','Walk','Run')]
    baked = []
    for action in source_actions:
        rig.animation_data.action = None
        for pb in rig.pose.bones:
            pb.matrix_basis.identity()
        rig.animation_data.action = action
        scene.frame_start,scene.frame_end = map(int,action.frame_range)
        bpy.ops.object.mode_set(mode='POSE')
        bpy.ops.pose.select_all(action='SELECT')
        bpy.ops.nla.bake(frame_start=scene.frame_start,frame_end=scene.frame_end,
            only_selected=True,visual_keying=True,clear_constraints=False,
            use_current_action=False,bake_types={'POSE'})
        baked_action = rig.animation_data.action
        baked_action.name = 'Baked '+action.name
        baked_action.use_fake_user = True
        baked.append((action.name,baked_action))
        bpy.ops.object.mode_set(mode='OBJECT')
    for pb in rig.pose.bones:
        for constraint in list(pb.constraints):
            pb.constraints.remove(constraint)
    for name,action in baked:
        old = bpy.data.actions[name]
        old.name = 'Study IK source - '+name
        action.name = name
    rig.animation_data.action = bpy.data.actions['Idle']
    scene.frame_start,scene.frame_end = 0,95
    scene.frame_set(0)
    scene['review_clip'] = 'Idle'
    scene['review_status'] = 'Local generated mesh; authored study rig and baked walk/run loops'
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.shading.type = 'MATERIAL'
                area.spaces.active.shading.color_type = 'MATERIAL'
    obj.active_material.node_tree.nodes.active = next(n for n in obj.active_material.node_tree.nodes if n.type == 'TEX_IMAGE')
    # Keep review selection on the mesh, with rigs hidden via overlays by default.
    rig.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    report = {'vertices':len(obj.data.vertices),'triangles':len(obj.data.polygons),
        'bones':len(arm.bones),'clips':[{'name':n,'range':list(a.frame_range)} for n,a in baked],
        'motion_origin':'New authored procedural study loops, no downloaded animal animation',
        'limits':'Approximate skinning; not game locomotion/foot IK/animation-blend validation'}
    args.output.with_suffix('.json').write_text(json.dumps(report,indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(args.output.resolve()))
    if args.render:
        scene.render.resolution_percentage = 70
        scene.cycles.samples = 12
        for clip,frame in [('Idle',0),('Walk',10),('Run',7)]:
            rig.animation_data.action = bpy.data.actions[clip]
            scene.frame_set(frame)
            scene.render.filepath = str(args.output.parent/(clip.lower()+'-pose.png'))
            bpy.ops.render.render(write_still=True)
    print(json.dumps(report,indent=2))


if __name__ == '__main__':
    main()
