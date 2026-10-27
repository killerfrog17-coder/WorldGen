"""Animate the accepted appearance samples using their own original skin weights."""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from human_motion_common import ROOT, REPO, C, SEMANTICS, primary_child, source_map, sources


def import_uma_skin(letter):
    collection = bpy.data.collections['UMA_'+letter]
    data = json.loads((ROOT/'uma-skin'/(letter+'.json')).read_text())
    skeleton = bpy.data.armatures.new('UMA_'+letter+' original skeleton')
    rig = bpy.data.objects.new('UMA_'+letter+'.Rig', skeleton)
    collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for bone in data['bones']:
        p = bone['position']; q = bone['rotation']
        matrix = C @ Matrix.LocRotScale(Vector([p[k] for k in 'xyz']),
                                       Quaternion([q[k] for k in 'wxyz']), Vector((1,1,1))) @ C.inverted()
        edit = skeleton.edit_bones.new(bone['name'])
        edit.matrix = matrix
        edit.length = .045
    for bone in data['bones']:
        if bone['parent']: skeleton.edit_bones[bone['name']].parent = skeleton.edit_bones[bone['parent']]
    bpy.ops.object.mode_set(mode='OBJECT')
    semantics = {bone['human']: bone['name'] for bone in data['bones'] if bone['human']}
    # Unity can omit the optional UpperChest mapping, while the UMA helper still exists.
    rig['semantic_bones'] = json.dumps(semantics)
    evidence = []
    for part in data['meshes']:
        obj = next(o for o in collection.objects if o.type=='MESH' and o.data.name.startswith(part['name']))
        points = np.array([(p['x'], -p['z'], p['y']) for p in part['vertices']])
        old = np.array([tuple(v.co) for v in obj.data.vertices])
        assert old.shape == points.shape, (letter, part['name'], old.shape, points.shape)
        error = float(np.abs(old-points).max())
        # The same native recipe must reproduce the static appearance, not another random mesh.
        assert error < .0001, (letter, part['name'], 'bind surface mismatch', error)
        assert len(part['counts']) == len(old)
        offset = 0
        for index,count in enumerate(part['counts']):
            total=0
            for slot in range(offset,offset+count):
                name=part['bones'][part['indices'][slot]];weight=part['weights'][slot]
                group=obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
                group.add([index],weight,'REPLACE');total+=weight
            assert abs(total-1)<.001, (index,total)
            offset += count
        assert offset == len(part['indices']) == len(part['weights'])
        obj.parent=rig;obj.matrix_parent_inverse=Matrix.Identity(4)
        obj.location=(0,0,0)
        modifier=obj.modifiers.new('Original UMA skin', 'ARMATURE');modifier.object=rig
        obj['source']='Native UMA snapshot with original per-vertex weights and original skeleton'
        evidence.append({'mesh':obj.name,'vertices':len(old),'max_bind_difference':error})
    rig.hide_render=True;rig.show_in_front=True;rig.hide_set(True)
    return rig,evidence


def mapping(rig,method):
    if method=='MPFB': return {key:key for key in SEMANTICS if key in rig.data.bones}
    human=json.loads(rig['semantic_bones'])
    result={key:human[value] for key,value in SEMANTICS.items() if value in human}
    # Native UMA maps two chest levels, with optional humanoid UpperChest absent in older avatars.
    if 'spine_03' not in result:
        for candidate in ['Chest','UpperChest']:
            if candidate in rig.data.bones and candidate not in result.values():result['spine_03']=candidate;break
    # UMA's anatomical-side labels use the opposite X convention to these GLB rigs.
    # Resolve side by actual bind coordinates; otherwise both arms fold across the torso.
    if rig.data.bones[result['upperarm_l']].head_local.x < rig.data.bones[result['upperarm_r']].head_local.x:
        for key in list(result):
            if key.endswith('_l') and key[:-2]+'_r' in result:
                other=key[:-2]+'_r';result[key],result[other]=result[other],result[key]
    return result


def rotation_fix(semantic,targets,srcmap,rest,source):
    """Calibrate differing bind-pose limb directions without modifying rest meshes/weights."""
    bone=targets[semantic];child=primary_child(semantic)
    fix=Quaternion()
    if semantic.startswith(('upperarm','lowerarm','thigh','calf','foot','hand')) and child in targets and child in srcmap:
        target_vector=rest[targets[child]].translation-rest[bone].translation
        source_vector=source.rest[srcmap[child]].translation-source.rest[srcmap[semantic]].translation
        if target_vector.length>.001 and source_vector.length>.001:
            fix=target_vector.rotation_difference(source_vector)
    elif semantic.startswith('hand_') and child in targets and 'wrist.'+semantic[-1] in source.rest:
        target_vector=rest[targets[child]].translation-rest[bone].translation
        source_vector=source.rest['hand.'+semantic[-1]].translation-source.rest['wrist.'+semantic[-1]].translation
        fix=target_vector.rotation_difference(source_vector)
    return fix


def animate(rig,method,clip,sampled):
    label,source,name=clip
    targets=mapping(rig,method);srcmap=source_map(source)
    rest={b.name:b.matrix_local.copy() for b in rig.data.bones}
    bones=list(rig.pose.bones)
    for b in bones:b.rotation_mode='QUATERNION';b.matrix_basis=Matrix.Identity(4)
    rig.animation_data_create();action=bpy.data.actions.new(f'{method} {rig["variant"]} | {label}')
    action.use_fake_user=True;rig.animation_data.action=action
    fixes={key:rotation_fix(key,targets,srcmap,rest,source) for key in targets if key in srcmap}
    for key in fixes:
        if key.startswith(('thumb_','index_','middle_','ring_','pinky_')):
            fixes[key]=fixes.get('hand_'+key[-1],Quaternion())
    reverse={value:key for key,value in targets.items() if key in srcmap}
    thigh=rest[targets['thigh_l']].translation
    ankle=rest[targets['foot_l']].translation
    source_thigh=source.rest[srcmap['thigh_l']].translation
    source_ankle=source.rest[srcmap['foot_l']].translation
    ratio=(thigh-ankle).length/(source_thigh-source_ankle).length
    restlocal={b.name:(rest[b.parent.name].inverted()@rest[b.name] if b.parent else rest[b.name]) for b in bones}
    curves={b.name:[] for b in bones};locations=[];previous={}
    for frame,world in enumerate(sampled,1):
        posed={}
        for b in bones:
            parent=posed[b.parent.name] if b.parent else Matrix.Identity(4)
            inherited=parent@restlocal[b.name]
            key=reverse.get(b.name)
            if key:
                s=srcmap[key]
                desired=(world[s].to_quaternion() @ source.rest[s].to_quaternion().inverted()
                         @ fixes[key] @ rest[b.name].to_quaternion())
                position=inherited.translation
                if key=='pelvis':
                    delta=(world[s].translation-source.rest[s].translation)*ratio
                    position=rest[b.name].translation+delta
                matrix=Matrix.LocRotScale(position,desired,Vector((1,1,1)))
                basis=restlocal[b.name].inverted() @ parent.inverted() @ matrix
            else:matrix=inherited;basis=Matrix.Identity(4)
            q=basis.to_quaternion()
            if b.name in previous and previous[b.name].dot(q)<0:q.negate()
            previous[b.name]=q.copy();curves[b.name].append(tuple(q))
            posed[b.name]=matrix
            if key=='pelvis':locations.append(tuple(basis.translation))
    # Native Blender actions, 30 Hz with continuous quaternion tracks, no live constraints.
    for b in bones:
        b.rotation_quaternion=curves[b.name][0]
        b.keyframe_insert(data_path='rotation_quaternion',frame=1,group=b.name)
    pelvis=rig.pose.bones[targets['pelvis']]
    pelvis.location=locations[0];pelvis.keyframe_insert(data_path='location',frame=1,group=pelvis.name)
    slot=rig.animation_data.action_slot
    channelbag=action.layers[0].strips[0].channelbag(slot)
    for fc in channelbag.fcurves:
        bone=fc.data_path.split('"')[1]
        array=locations if fc.data_path.endswith('.location') else curves[bone]
        fc.keyframe_points.clear();fc.keyframe_points.add(len(array))
        fc.keyframe_points.foreach_set('co',[v for index,value in enumerate(array,1) for v in (index,value[fc.array_index])])
        for point in fc.keyframe_points:point.interpolation='LINEAR'
    action['source_file']=str(source.path.relative_to(REPO));action['source_clip']=name
    if getattr(source,'use_idle_fingers',False):
        idle=next(c for c in json.loads(rig['preview_clips']) if c['label']=='Idle')
        idle_action=bpy.data.actions[idle['action']]
        idle_bag=idle_action.layers[0].strips[0].channelbag(idle_action.slots[0])
        reference={(fc.data_path,fc.array_index):fc for fc in idle_bag.fcurves}
        for fc in channelbag.fcurves:
            bone=fc.data_path.split('"')[1]
            if bone.startswith(('thumb_','index_','middle_','ring_','pinky_')) and (fc.data_path,fc.array_index) in reference:
                value=reference[fc.data_path,fc.array_index].evaluate(1)
                for point in fc.keyframe_points:point.co.y=value
    action['license']=getattr(source,'license','CC0');action['sample_rate']=30;action['source_sha256']=hashlib.sha256(source.path.read_bytes()).hexdigest()
    action['label']=label
    return {'label':label,'action':action.name,'frames':len(sampled),'source':str(source.path.relative_to(REPO)),
            'source_clip':name,'mapped_bones':len(reverse),'leg_ratio':ratio}


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--mpfb-probe',action='store_true');args=ap.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    ROOT.mkdir(exist_ok=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.open_mainfile(filepath=str(REPO/'artifacts/human-builder-demo/Human - Character Builder Comparison.blend'))
    scene=bpy.context.scene;scene.render.fps=30
    for c in bpy.data.collections:
        c.hide_viewport=False;c.hide_render=False
        for o in c.objects:
            if o.parent is None and c.name.startswith(('MPFB_','UMA_')):o.location=(0,0,0)
    clips=sources()
    samples={label:[source.sample(name,min(index/30,source.duration(name))) for index in range(round(source.duration(name)*30)+1)]
             for label,source,name in clips}
    evidence=[];rigs=[]
    for letter in ('A' if args.mpfb_probe else 'ABCDEF'):
        methods=['MPFB'] if args.mpfb_probe else ['MPFB','UMA']
        for method in methods:
            if method=='UMA':rig,skin=import_uma_skin(letter)
            else:
                rig=next(o for o in bpy.data.collections[method+'_'+letter].objects if o.type=='ARMATURE');skin=[]
            rig['variant']=letter;rig['builder']=method
            rig.animation_data_clear()
            records=[animate(rig,method,clip,samples[clip[0]]) for clip in clips]
            rig['preview_clips']=json.dumps(records);rigs.append(rig)
            evidence.append({'builder':method,'variant':letter,'bones':len(rig.data.bones),'skin':skin,'clips':records})
            print('HUMAN_ANIMATED',method,letter,len(records),flush=True)
    scene['animation_preview_evidence']=json.dumps(evidence)
    scene['animation_labels']=json.dumps([clip[0] for clip in clips])
    scene['preview_clip']='Walk';scene['demo_view']='body';scene['demo_compare']=not args.mpfb_probe
    scene['demo_builder']='MPFB';scene['demo_variant']='A';scene['demo_lineup']=False
    import human_animation_stage as stage
    stage.select_clip('Walk');stage.refresh()
    for obj in bpy.context.selected_objects:obj.select_set(False)
    scene.frame_set(1)
    output=ROOT/('MPFB - Motion Probe.blend' if args.mpfb_probe else 'Human - Animation Comparison.blend')
    bpy.ops.wm.save_as_mainfile(filepath=str(output),compress=True)
    (ROOT/('probe-evidence.json' if args.mpfb_probe else 'build-evidence.json')).write_text(json.dumps(evidence,indent=2))
    print('HUMAN_ANIMATION_READY',output,flush=True)


if __name__=='__main__':main()
