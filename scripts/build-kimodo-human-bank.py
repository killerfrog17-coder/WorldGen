"""Bake generated SOMA motion onto the original MPFB skeletons, animation only.

Never writes character geometry, weights, textures or legacy animation assets.
Charge/release share a boundary pose. Raw generations remain unmodified.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

REPO = Path(__file__).resolve().parent.parent
ROOT = REPO / 'artifacts/kimodo-human-bank'
DEMO = REPO / 'artifacts/kimodo-first-person-demo'
SOURCE = REPO / 'artifacts/human-animation-preview/Human - Animation Comparison.blend'
OUTPUT = REPO / 'assets/human-motion-kimodo'
sys.path.insert(0, str(REPO / 'scripts'))
from kimodo_demo_motion import SomaMotion, SOMA_MAP, retarget_fix

LOCO = {'Idle', 'Walk', 'Run', 'Sprint', 'Crouch', 'CrouchIdle', 'Jump', 'Swim','WalkBack','StrafeLeft','StrafeRight','RunBack','RunLeft','RunRight'}
MIRROR = {'ToolReady','ToolWalk','ToolCutA','ToolCutB','ToolBlunt','ToolChop','ToolGuard',
          'SpearReady1','SpearThrust1','SpearReady2','SpearThrust2','ThrowCharge','ThrowRelease',
          'SpearThrowCharge','SpearThrowRelease','SlingReady','SlingWhirl','SlingRelease','Punch','Gather'}
ROLE_SOURCE = {
    'Idle': ('idle', True), 'Walk': ('demo_walk', True), 'Run': ('jog', True),
    'Sprint': ('sprint', True), 'Crouch': ('crouch_walk', True), 'CrouchIdle': ('crouch_idle', True),
    'Jump': ('jump', False), 'Swim': ('swim', True),
    'WalkBack': ('walk_back', True), 'StrafeLeft': ('strafe_left', True), 'StrafeRight': ('strafe_right', True),
    'RunBack': ('run_back', True), 'RunLeft': ('run_left', True), 'RunRight': ('run_right', True),
    'Empty': ('idle', True), 'FistsReady': ('fists_ready', True), 'FistsGuard': ('fists_guard', True),
    'Punch': ('punch', False), 'ToolReady': ('tool_ready', True), 'ToolWalk': ('tool_walk', True),
    'ToolCutA': ('tool_cut', False), 'ToolCutB': ('tool_stab', False),
    'ToolBlunt': ('tool_blunt', False), 'ToolChop': ('tool_chop', False), 'ToolGuard': ('tool_guard', True),
    'SpearReady1': ('spear_ready_one', True), 'SpearThrust1': ('spear_thrust_one', False),
    'SpearReady2': ('spear_ready_two', True), 'SpearThrust2': ('spear_thrust_two', False),
    'ThrowCharge': ('throw', False), 'ThrowRelease': ('throw', False),
    'SpearThrowCharge': ('demo_spear_throw', False), 'SpearThrowRelease': ('demo_spear_throw', False),
    'SlingReady': ('sling_ready', True), 'SlingWhirl': ('sling_whirl', True), 'SlingRelease': ('sling_release', False),
    'BowReady': ('bow_ready', True), 'BowDraw': ('bow_shoot', False), 'BowRelease': ('bow_shoot', False),
    'Gather': ('gather', False), 'Drink': ('drink', False), 'Eat': ('eat', False),
    'WorkKnapping': ('work_knapping', True), 'WorkCut': ('work_cut', True),
    'WorkTwist': ('work_twist', True), 'WorkDig': ('work_dig', True), 'CarryLarge': ('carry_large', True),
}


def selected_take(name):
    if name == 'demo_walk': return DEMO / 'motions/empty_walk/seed-240'
    if name == 'demo_spear_throw': return DEMO / 'motions/spear_throw/seed-252'
    folder = ROOT / 'motions' / name
    if not folder.exists():folder=REPO/'artifacts/kimodo-human-bank/motions'/name
    marker = folder / 'selected.json'
    if marker.exists(): return folder / json.loads(marker.read_text())['take']
    return next(iter(sorted(folder.glob('seed-*/generation.json'))), Path('missing')).parent


def swap(name):
    return name.replace('Left','@').replace('Right','Left').replace('@','Right')


class FittedMotion:
    license = SomaMotion.license
    def __init__(self, source, samples, mirrored=False):
        self.path = source.path
        reflect = Matrix.Diagonal(Vector((-1, 1, 1, 1)))
        self.rest = {n: reflect @ source.rest[swap(n)] @ reflect for n in source.names} if mirrored else source.rest
        self.samples = [{n: reflect @ row[swap(n)] @ reflect for n in source.names} for row in samples] if mirrored else samples


def prepare(source, name, role, loop, overrides):
    rows = [source.sample(None, i / 30) for i in range(source.info['frames'])]
    count = len(rows)
    points = np.array([tuple(r['RightHand'].translation) for r in rows])
    forward_velocity = -np.diff(points[:, 1]) * 30
    score = forward_velocity * np.clip((points[1:, 2] - .85) / .4, 0, 1)
    release = int(np.argmax(score)) + 1
    release = max(15, min(count - 15, release))
    begin, end = 0, count - 1
    attack_frame=None
    jump_air=[]
    if role=='Jump':
        floor=min(min(r['LeftToeBase'].translation.z,r['RightToeBase'].translation.z) for r in rows)
        jump_air=[i for i,r in enumerate(rows) if min(r['LeftToeBase'].translation.z,r['RightToeBase'].translation.z)>floor+.065]
        if jump_air:begin,end=max(0,jump_air[0]-3),min(count-1,jump_air[-1]+4)
    elif role.endswith('Charge'):
        end = max(8, release - 7)
    elif role in {'ThrowRelease', 'SpearThrowRelease', 'SlingRelease'}:
        begin = max(0, release - 7)
        end = min(count - 1, release + 30)
    elif role in {'BowDraw', 'BowRelease'}:
        distance = np.array([(r['RightHand'].translation-r['LeftHand'].translation).length for r in rows])
        draw = int(np.argmax(distance[15:-15])) + 15
        if role == 'BowDraw': end = draw
        else: begin,end = draw,min(count-1,draw+30)
    elif role in {'Gather','Drink','Eat'}:
        if role=='Gather':
            relative=np.array([tuple(r['RightHand'].translation-r['Hips'].translation) for r in rows])
            active=[i for i,r in enumerate(rows) if np.linalg.norm(relative[i]-relative[0])>.1 or r['Hips'].translation.z<source.rest['Hips'].translation.z-.1]
        else:
            hands=['RightHand','LeftHand'] if role=='Drink' else ['RightHand']
            active=[i for i,r in enumerate(rows) if min((r[h].translation-r['Head'].translation).length for h in hands)<.4]
        if active:begin,end=max(0,active[0]-6),min(count-1,active[-1]+12)
    elif role in {'Punch','ToolCutA','ToolCutB','ToolBlunt','ToolChop','SpearThrust1','SpearThrust2'}:
        reach = points[:, 1] - np.array([r['Hips'].translation.y for r in rows])
        if role in {'ToolBlunt','ToolChop'}:
            height=points[:,2]-np.array([r['Hips'].translation.z for r in rows])
            contact=int(np.argmin(np.diff(height)))+1
        else:contact = int(np.argmin(reach))
        attack_frame=contact
        begin = max(0, contact - 18)
        end = min(count - 1, max(begin + 20, contact + 23))
    elif loop and role in {'Walk','Run','Sprint','Crouch','Swim','SwimCarry','SwimCarryTwo','WalkBack','StrafeLeft','StrafeRight','RunBack','RunLeft','RunRight','ToolWalk','SlingWhirl','WorkKnapping','WorkCut','WorkTwist','WorkDig'}:
        names = ['LeftFoot','RightFoot','LeftLeg','RightLeg','LeftHand','RightHand']
        if role in {'Swim','SwimCarry','SwimCarryTwo','SlingWhirl','WorkKnapping','WorkCut','WorkTwist','WorkDig'}:
            names=['LeftHand','RightHand','LeftForeArm','RightForeArm']
        features = np.array([[tuple(r[n].translation-r['Hips'].translation) for n in names] for r in rows])
        low, high = (17, 44) if role in {'Run','Sprint','RunBack','RunLeft','RunRight'} else (24, 76)
        best = None
        for a in range(8, count - low - 5):
            for length in range(low, min(high, count - a - 1) + 1):
                b = a + length
                error = float(np.square(features[a] - features[b]).sum())
                excursion=float(np.square(np.ptp(features[a:b+1],axis=0)).sum())
                error=(error+.003)/(excursion+.005)
                # Prefer a useful cycle rather than an almost stationary fragment.
                if best is None or error < best[0]: best = (error,a,b)
        if best: _, begin, end = best
    elif loop:
        begin, end = min(15,count//5), count-12
    override = overrides.get(role, {})
    begin = int(override.get('begin', begin));end = int(override.get('end', end))
    assert 0 <= begin < end < count, (role,begin,end,count)
    release = int(override.get('release', release))
    selected = [{n:m.copy() for n,m in r.items()} for r in rows[begin:end+1]]
    swim_lift = source.rest['Head'].translation.z-float(np.median([r['Head'].translation.z for r in selected])) if role.startswith('Swim') else 0
    # Controller owns horizontal travel. Preserve generated vertical body motion.
    for row in selected:
        offset = row['Hips'].translation.copy();offset.z = 0
        for matrix in row.values(): matrix.translation -= offset;matrix.translation.z+=swim_lift
    if role=='Jump' and jump_air:
        start,finish=jump_air[0]-2,jump_air[-1]+2
        height=max(0,max(r['Hips'].translation.z for r in rows)-source.rest['Hips'].translation.z)
        for i,row in enumerate(selected,begin):
            t=max(0,min(1,(i-start)/max(1,finish-start)))
            arc=4*height*t*(1-t)
            for matrix in row.values():matrix.translation.z-=arc
    if loop:
        # Spread the small endpoint correction over the cycle instead of
        # freezing its last quarter by blending every closing pose to frame one.
        corrections={n:(selected[0][n].translation-selected[-1][n].translation,
                         selected[0][n].to_quaternion() @ selected[-1][n].to_quaternion().inverted()) for n in source.names}
        for i,row in enumerate(selected):
            amount=i/(len(selected)-1)
            for n in source.names:
                delta,rotation=corrections[n];a=row[n]
                row[n]=Matrix.LocRotScale(a.translation+delta*amount,Quaternion().slerp(rotation,amount) @ a.to_quaternion(),Vector((1,1,1)))
    if role in {'Walk','Run','Sprint','Crouch','WalkBack','StrafeLeft','StrafeRight','RunBack','RunLeft','RunRight','ToolWalk'} and loop:
        core=selected[:-1]
        low=min(r['LeftFoot'].translation.z for r in core)
        candidates=[i for i,r in enumerate(core) if r['LeftFoot'].translation.z<=low+.035]
        phase=min(candidates,key=lambda i:core[i]['LeftFoot'].translation.y-core[i]['Hips'].translation.y)
        core=core[phase:]+core[:phase]
        selected=core+[{n:m.copy() for n,m in core[0].items()}]
    duration = (len(selected)-1)/30
    root_travel = (rows[end]['Hips'].translation-rows[begin]['Hips'].translation)
    root_travel.z = 0
    contact = -1
    if role in {'Punch','ToolCutA','ToolCutB','ToolBlunt','ToolChop','SpearThrust1','SpearThrust2'}:
        contact = max(.1,min(.9,(int(override.get('contact_frame',attack_frame))-begin)/(end-begin)))
    release_seconds = max(0,min(duration,(release-begin)/30)) if role.endswith('Release') else -1
    if role == 'BowRelease': release_seconds = 0
    return selected, {'role':role,'source':str(source.folder.relative_to(REPO)),'begin':begin,'end':end,
                      'loop':loop,'contact':contact,'release':release_seconds,'travel_m':root_travel.length,
                      'duration':duration,'raw_name':name}


def main():
    global ROOT,OUTPUT
    parser=argparse.ArgumentParser();parser.add_argument('--variants',default='ABCDEF');parser.add_argument('--partial',action='store_true');parser.add_argument('--save-review',action='store_true')
    parser.add_argument('--take-root');parser.add_argument('--output');parser.add_argument('--natural',action='store_true');parser.add_argument('--review-variants',default='B')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    if args.take_root:ROOT=REPO/args.take_root
    if args.output:OUTPUT=REPO/args.output
    if args.natural:
        LOCO.update({'SwimCarry','SwimCarryLeft','SwimCarryTwo'})
        ROLE_SOURCE.update({'SwimCarry':('swim',True),'SwimCarryTwo':('swim',True),'ToolBlunt':('tool_chop',False)})
    OUTPUT.mkdir(parents=True,exist_ok=True)
    overrides=json.loads((ROOT/'authoring.json').read_text()) if (ROOT/'authoring.json').exists() else {}
    sources={};prepared={}
    for role,(name,loop) in ROLE_SOURCE.items():
        if name not in sources:
            take=selected_take(name)
            if not (take/'generation.json').exists():
                if args.partial: continue
                raise RuntimeError('Motion missing: '+name)
            sources[name]=SomaMotion(take)
        samples,record=prepare(sources[name],name,role,loop,overrides)
        prepared[role]=(FittedMotion(sources[name],samples),record)
        if role in MIRROR:
            prepared[role+'_Left']=(FittedMotion(sources[name],samples,True),{**record,'role':role+'_Left','mirrored':True})
        if role=='SwimCarry':prepared['SwimCarryLeft']=(FittedMotion(sources[name],samples,True),{**record,'role':'SwimCarryLeft','mirrored':True})
    (ROOT/'authoring-decisions.json').write_text(json.dumps([v[1] for v in prepared.values()],indent=2))
    spec=importlib.util.spec_from_file_location('kimodo_retarget',REPO/'scripts/build-human-animation-preview.py');retarget=importlib.util.module_from_spec(spec);spec.loader.exec_module(retarget)
    retarget.source_map=lambda source:SOMA_MAP
    retarget.rotation_fix=retarget_fix
    source_sha=hashlib.sha256(SOURCE.read_bytes()).hexdigest();people=[]
    for letter in args.variants:
        bpy.ops.wm.open_mainfile(filepath=str(SOURCE));bpy.context.preferences.filepaths.save_version=0
        col=bpy.data.collections['MPFB_'+letter];rig=next(o for o in col.objects if o.type=='ARMATURE')
        col.hide_viewport=False;col.hide_render=False;rig.hide_viewport=False
        # Keep the review meshes in memory only when requested. FBX is skeleton-only.
        review=args.save_review and letter in args.review_variants
        keep={rig}|(set(col.objects) if review else set())
        for obj in list(bpy.data.objects):
            if obj not in keep:bpy.data.objects.remove(obj,do_unlink=True)
        rig.hide_set(False);rig.hide_render=False;rig.location=(0,0,0);rig['variant']=letter
        if review:
            for obj in col.objects:
                for modifier in obj.modifiers:
                    if modifier.type=='SUBSURF':modifier.show_viewport=False;modifier.show_render=False
        relaxed_action=bpy.data.actions['MPFB '+letter+' | Idle'];grip_action=bpy.data.actions['MPFB '+letter+' | Sword attack']
        fingers={}
        for kind,action,frame in [('relaxed',relaxed_action,1),('grip',grip_action,12)]:
            rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0];bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
            fingers[kind]={b.name:b.rotation_quaternion.copy() for b in rig.pose.bones if b.name.startswith(('index_','middle_','ring_','pinky_','thumb_'))}
        records=[];natural_idle=None
        for role,(motion,metadata) in prepared.items():
            record=retarget.animate(rig,'MPFB',(role,motion,role),motion.samples);action=bpy.data.actions[record['action']]
            if args.natural:
                from natural_human_bake import pin_ordinary_legs,restrain_ordinary_torso,smooth_ordinary_arms,stable_bow_wrists,ground_action,is_upper
                if natural_idle is not None and is_upper(role,LOCO):
                    pin_ordinary_legs(rig,action,natural_idle,len(motion.samples))
                    restrain_ordinary_torso(action,natural_idle,role)
                    smooth_ordinary_arms(action,metadata['loop'])
                    if role.startswith('Bow'):stable_bow_wrists(action,natural_idle)
                if role in LOCO and role!='Jump' and not role.startswith('Swim'):
                    metadata={**metadata,'ground_offset_m':ground_action(rig,action,len(motion.samples))}
                if role=='Idle':natural_idle=action
            # Calm hands, or a power grip. Fingers are fitted, not generated SOMA30 data.
            bag=action.layers[0].strips[0].channelbag(action.slots[0]);left=role.endswith('_Left')
            for curve in bag.fcurves:
                bone=curve.data_path.split('"')[1]
                if bone not in fingers['relaxed']:continue
                both=role.startswith(('SpearReady2','SpearThrust2','Bow','Work','Drink','Carry','Fists','Punch'))
                gripping=role not in LOCO|{'Empty','Gather','Gather_Left','SlingRelease','SlingRelease_Left'} and (both or bone.endswith('_l' if left else '_r'))
                if args.natural and role in {'SwimCarry','SwimCarryLeft','SwimCarryTwo'}:
                    gripping=role=='SwimCarryTwo' or bone.endswith('_l' if role=='SwimCarryLeft' else '_r')
                desired=fingers['grip' if gripping else 'relaxed'][bone]
                # The source attack's opposite hand is relaxed; mirror the right curl
                # in the native local finger frame for left-dominant grip auditions.
                if gripping and bone.endswith('_l'):
                    other=fingers['grip'][bone[:-1]+'r'];desired=Quaternion((other.w,other.x,-other.y,-other.z))
                if role in {'Drink','WorkTwist'}:desired=fingers['relaxed'][bone].slerp(desired,.45)
                if args.natural and role in {'CarryLarge','SwimCarryTwo'}:desired=fingers['relaxed'][bone].slerp(desired,.45)
                if args.natural and role.startswith('Bow') and bone.endswith('_r'):
                    amount=0 if role=='BowReady' else .8
                    desired=fingers['relaxed'][bone].slerp(desired,amount)
                if role=='Eat' and not bone.startswith(('thumb_','index_')):desired=fingers['relaxed'][bone].slerp(desired,.35)
                for key in curve.keyframe_points:
                    q=desired
                    if role.endswith('Release') or role.endswith('Release_Left'):
                        seconds=(key.co.x-1)/30;amount=max(0,min(1,(seconds-metadata['release'])*30/4))
                        q=desired.slerp(fingers['relaxed'][bone],amount)
                    key.co.y=q[curve.array_index]
            action.name='Kimodo '+letter+' | '+role;action['raw_motion']=metadata['source'];action['finger_fitting']='Native relaxed/power-grip poses; SOMA30 has no finger tracks'
            records.append({**metadata,'action':action.name,'frames':len(motion.samples),'travel_m':metadata['travel_m']*record['leg_ratio']})
        wanted={r['action'] for r in records}
        for action in list(bpy.data.actions):
            if action.name not in wanted:bpy.data.actions.remove(action)
        bpy.context.scene.render.fps=30;bpy.context.scene.render.fps_base=1
        rig.animation_data_clear();rig.animation_data_create();first=bpy.data.actions[records[0]['action']];rig.animation_data.action=first;rig.animation_data.action_slot=first.slots[0]
        bpy.context.scene.frame_set(1)
        # A sibling makes the FBX retain the same armature prefix as the full skin FBX.
        sibling=bpy.data.objects.new('Animation bank binding sibling',None);bpy.context.scene.collection.objects.link(sibling)
        for obj in bpy.context.selected_objects:obj.select_set(False)
        rig.select_set(True);sibling.select_set(True);bpy.context.view_layer.objects.active=rig
        bpy.ops.export_scene.fbx(filepath=str(OUTPUT/(letter+'.fbx')),use_selection=True,object_types={'ARMATURE','EMPTY'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=.05 if args.natural else 0,bake_anim_step=1,use_custom_props=False,path_mode='STRIP')
        if review:
            for obj in col.objects:obj.hide_set(False);obj.hide_render=False
            rig['kimodo_bank_clips']=json.dumps(records)
            bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/('Kimodo bank '+letter+'.blend')),compress=True)
        people.append({'variant':letter,'eye_forward':-float(col['head_center'][1])+.025,'clips':records,'file':letter+'.fbx','fbx_sha256':hashlib.sha256((OUTPUT/(letter+'.fbx')).read_bytes()).hexdigest()})
        print('KIMODO BANK EXPORTED',letter,len(records),flush=True)
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==source_sha
    (OUTPUT/'bank.json').write_text(json.dumps({'fps':30,'source_sha256':source_sha,'model':'Kimodo-SOMA-RP-v1.1','people':people},indent=2))


if __name__=='__main__':main()
