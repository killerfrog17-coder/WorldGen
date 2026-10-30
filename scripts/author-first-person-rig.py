"""Author and bake the private first-person rig, never the approved world avatar.

Run: blender -b -t 8 --python-exit-code 1 --python scripts/author-first-person-rig.py
The CC0 artist's hand topology and weights seed camera-authored grip poses.
New handling is authored in camera space and baked to FK before Unity export.
There are no runtime arm targets, stretch constraints or runtime finger solvers.
"""
from pathlib import Path
import bpy, math, json, hashlib
from mathutils import Matrix, Vector, Quaternion

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'assets/first-person'
EVIDENCE = ROOT / 'artifacts/first-person-rebuild-20261030'
GENERATOR_SHA=hashlib.sha256(Path(__file__).read_text().replace('\r\n','\n').encode('utf-8')).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(OUT / 'FirstPersonBase.blend'))
bpy.context.preferences.filepaths.save_version=0
scene = bpy.context.scene
rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
arms = next(o for o in bpy.data.objects if o.type == 'MESH')
native = {a.name.split('|')[-1]: a for a in bpy.data.actions}
R = rig.matrix_world.copy()
RI = R.inverted()
MIRROR = Matrix.Diagonal((-1, 1, 1, 1))
U = lambda v: Vector((-v[0], -v[2], v[1]))
FPS = 60
scene.render.fps = FPS
scene.camera.data.sensor_fit='VERTICAL';scene.camera.data.sensor_height=32
scene.camera.data.lens=32/(2*math.tan(math.radians(68)/2));scene.camera.data.clip_start=.1
AUTHOR_CAMERA_INV=scene.camera.matrix_world.inverted()


def sample_native(label, phase=0):
    action = native[label]
    # Upstream takes key only the channels they need. Newly authored takes also
    # move bone locations, so sampling an upstream take without resetting leaves
    # those unkeyed offsets behind (notably crossing/near-clipping the guard).
    rig.animation_data_clear()
    for bone in rig.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
    rig.animation_data_create()
    rig.animation_data.action = action
    if action.slots:
        rig.animation_data.action_slot = action.slots[0]
    first, last = action.frame_range
    scene.frame_set(int(first + (last-first)*phase), subframe=(first+(last-first)*phase) % 1)
    bpy.context.view_layer.update()
    return {b.name: b.matrix.copy() for b in rig.pose.bones}


rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
grasp = sample_native('Sword_Idle')
# Mirror deformation, rather than Euler angles: left and right bones need not
# share local axes. This preserves the artist's thumb opposition and knuckles.
for name in list(grasp):
    if name.endswith('.L'):
        other = name[:-2] + '.R'
        if other in grasp:
            grasp[name] = RI @ MIRROR @ (R @ grasp[other] @ rest[other].inverted() @ RI) @ MIRROR @ R @ rest[name]

source_grips, offsets = {}, {}
for side in ['R', 'L']:
    wrist = R @ grasp['Wrist.'+side]
    if side == 'R':
        center = wrist @ Vector((-.0066, .1093, -.0544))
        y = ((R @ grasp['Index2.R']).translation-(R @ grasp['Pinky2.R']).translation).normalized()
        z = Vector((0, -1, 0)); z = (z-y*z.dot(y)).normalized()
        x = y.cross(z).normalized()
        m = Matrix((x, y, z)).transposed().to_4x4(); m.translation = center
    else:
        m = MIRROR @ source_grips['R'] @ MIRROR
    source_grips[side] = m
    offsets[side] = wrist.inverted() @ m

# A long shaft rests diagonally across a power grip. Reusing the sword's
# perpendicular handle axis is what otherwise forces an extended spear wrist
# into an extreme bend. Keep a separate native socket for each grip style.
spear_grips, spear_offsets = {}, {}
for side in ['R','L']:
    old=source_grips[side]
    wrist=R @ grasp['Wrist.'+side]
    forward=((R @ grasp['Middle2.'+side]).translation-wrist.translation).normalized()
    angle=math.radians(35)
    y=(old.to_3x3().col[1]*math.cos(angle)+forward*math.sin(angle)).normalized()
    z=old.to_3x3().col[2];z=(z-y*z.dot(y)).normalized()
    m=Matrix((y.cross(z).normalized(),y,z)).transposed().to_4x4();m.translation=old.translation
    spear_grips[side]=m;spear_offsets[side]=wrist.inverted() @ m

# Actual socket bones are exported with the rig. Held view items follow these
# baked sockets; they never drag a real avatar's hands toward camera targets.
rig.animation_data_clear()
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for side in ['R','L']:
    for prefix in ['ForearmHalf','ForearmFull']:
        twist=rig.data.edit_bones.new(prefix+'.'+side)
        twist.length=rig.data.edit_bones['LowerArm.'+side].length
        twist.matrix=rest['LowerArm.'+side].copy()
        twist.parent=rig.data.edit_bones['LowerArm.'+side];twist.use_deform=True
for prefix,style_offsets in [('Grip',offsets),('SpearGrip',spear_offsets)]:
    for side in ['R', 'L']:
        bone = rig.data.edit_bones.new(prefix+'.'+side)
        bone.length = .08
        bone.matrix = rest['Wrist.'+side] @ style_offsets[side]
        bone.parent = rig.data.edit_bones['Wrist.'+side]
        bone.use_deform = False
bone=rig.data.edit_bones.new('Nock.R')
bone.length=.035
bone.matrix=rest['Wrist.R'] @ offsets['R'] @ Matrix.Translation((0,.035,0))
bone.parent=rig.data.edit_bones['Wrist.R'];bone.use_deform=False
bpy.ops.object.mode_set(mode='OBJECT')
rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
for side in ['R', 'L']:
    grasp['Grip.'+side] = grasp['Wrist.'+side] @ offsets[side]
    grasp['SpearGrip.'+side] = grasp['Wrist.'+side] @ spear_offsets[side]
    grasp['ForearmHalf.'+side]=grasp['LowerArm.'+side].copy()
    grasp['ForearmFull.'+side]=grasp['LowerArm.'+side].copy()
grasp['Nock.R']=grasp['Wrist.R'] @ offsets['R'] @ Matrix.Translation((0,.035,0))

# Three longitudinal rotation stations avoid concentrating pronation in a
# single wrist ring. The adjacent-station weights also avoid the 180-degree
# collapse of a two-weight linear-skinned forearm.
for side in ['R','L']:
    group=arms.vertex_groups['LowerArm.'+side]
    half=arms.vertex_groups.new(name='ForearmHalf.'+side)
    full=arms.vertex_groups.new(name='ForearmFull.'+side)
    head=(R @ rest['LowerArm.'+side]).translation
    end=(R @ rest['Wrist.'+side]).translation
    delta=end-head;length2=delta.length_squared
    for vertex in arms.data.vertices:
        weight=next((g.weight for g in vertex.groups if g.group==group.index),0)
        if weight<=0:continue
        t=((arms.matrix_world @ vertex.co)-head).dot(delta)/length2
        f=max(0,min(1,(t-.08)/.82))
        lower=max(0,1-f*2);middle=1-abs(f*2-1);distal=max(0,f*2-1)
        if lower>0:group.add([vertex.index],weight*lower,'REPLACE')
        else:group.remove([vertex.index])
        if middle>0:half.add([vertex.index],weight*middle,'REPLACE')
        if distal>0:full.add([vertex.index],weight*distal,'REPLACE')


def local_basis(global_pose):
    result = {}
    for b in rig.pose.bones:
        parent_rest = rest[b.parent.name] if b.parent else Matrix.Identity(4)
        parent_pose = global_pose[b.parent.name] if b.parent else Matrix.Identity(4)
        result[b.name] = (parent_rest.inverted() @ rest[b.name]).inverted() @ parent_pose.inverted() @ global_pose[b.name]
    return result


BASE_BASIS = local_basis(grasp)
FIST_BASIS = local_basis(sample_native('Fists_Idle', .3))
arm_stats = []
SPEAR_FINGERS = {}
POSE_ELBOWS = {}


def distribute_forearm_twist():
    """Bake axial wrist rotation into the private derivative's forearm stations."""
    for side in ['R','L']:
        lower=rig.pose.bones['LowerArm.'+side]
        hand=rig.pose.bones['Wrist.'+side]
        current=(R @ lower.matrix).to_quaternion()
        desired=(R @ hand.matrix).to_quaternion()
        reference=(R @ grasp['Wrist.'+side]).to_quaternion().inverted() @ (R @ grasp['LowerArm.'+side]).to_quaternion()
        candidate=desired @ reference
        axis=current @ Vector((0,1,0))
        candidate=(candidate @ Vector((0,1,0))).rotation_difference(axis) @ candidate
        difference=candidate @ current.inverted()
        angle=2*math.atan2(Vector((difference.x,difference.y,difference.z)).dot(axis),difference.w)
        angle=(angle+math.pi)%math.tau-math.pi
        for prefix,amount in [('ForearmHalf',.5),('ForearmFull',1)]:
            bone=rig.pose.bones[prefix+'.'+side];bone.rotation_mode='QUATERNION';bone.rotation_quaternion=Quaternion((0,1,0),angle*amount)
    bpy.context.view_layer.update()


def reset(shift=(0, -.12, 0)):
    rig.animation_data_clear()
    for b in rig.pose.bones:
        b.matrix_basis = BASE_BASIS[b.name]
    chest = rig.pose.bones['Chest']
    pose = grasp['Chest'].copy()
    pose.translation += RI.to_3x3() @ U(shift)
    chest.matrix = pose
    bpy.context.view_layer.update()
    return {b.name: b.matrix.copy() for b in rig.pose.bones}


def grip_pose(side, center, axis, roll, base, curl=1, pole_weight=.15, grip_style='tool', hand_rotation=None):
    """Offline two-link posing with authored roll and a stable elbow preference.

    The wrist's anatomical direction constrains the elbow plane. This avoids
    solving a reachable hand position at the cost of folding the wrist backward.
    Reach is bounded; any clamp is reported for visual correction before export.
    """
    axis = U(axis).normalized()
    source=(spear_grips if grip_style=='spear' else source_grips)[side]
    offset=(spear_offsets if grip_style=='spear' else offsets)[side]
    q = source.to_3x3().col[1].rotation_difference(axis)
    q = hand_rotation if hand_rotation is not None else Quaternion(axis, math.radians(roll)) @ q
    grip = q.to_matrix().to_4x4() @ source
    grip.translation = U(center)
    desired = grip @ offset.inverted()
    upper = rig.pose.bones['UpperArm.'+side]
    lower = rig.pose.bones['LowerArm.'+side]
    hand = rig.pose.bones['Wrist.'+side]
    shoulder = (R @ base[upper.name]).translation
    old_elbow = (R @ base[lower.name]).translation
    old_wrist = (R @ base[hand.name]).translation
    a, b = (old_elbow-shoulder).length, (old_wrist-old_elbow).length
    delta = desired.translation-shoulder
    distance = min(max(.01, delta.length), (a+b)*.985)
    direction = delta.normalized()
    target = shoulder + direction*distance
    hand_local = base[hand.name].inverted().to_3x3() @ (base['Middle2.'+side].translation-base[hand.name].translation)
    hand_direction = (desired.to_3x3() @ hand_local).normalized()
    hint = U((.45 if side == 'R' else -.45, -.5, 0))-shoulder
    pole = (hint-direction*hint.dot(direction)).normalized()
    anatomical = -hand_direction+direction*hand_direction.dot(direction)
    if anatomical.length < 1e-5:
        anatomical = pole.copy()
    bend = (anatomical.normalized()*(1-pole_weight)+pole*pole_weight).normalized()
    along = (a*a+distance*distance-b*b)/(2*distance)
    radius=math.sqrt(max(0,a*a-along*along))
    # Camera-authored elbows must remain below or outside the close view. A
    # mathematically reachable palm alone can otherwise put an upper arm across
    # the near plane. Search only during baking, never in the running game.
    preferred=bend.copy();side_axis=direction.cross(bend).normalized()
    def elbow_cost(theta):
        candidate_bend=preferred*math.cos(theta)+side_axis*math.sin(theta)
        candidate=shoulder+direction*along+candidate_bend*radius
        wrist_angle=(target-candidate).angle(hand_direction)
        exposure=0
        for t in [.2,.5,.8,1.0]:
            point=AUTHOR_CAMERA_INV @ shoulder.lerp(candidate,t);depth=-point.z
            if depth+.07<.1 or abs(point.x)-.07>max(.05,depth)*math.tan(math.radians(34))*2.5:continue
            exposure+=max(0,point.y+.07+depth*math.tan(math.radians(34)))**2
        for t in [0,.2,.4,.6,.8]:
            point=AUTHOR_CAMERA_INV @ candidate.lerp(target,t);depth=-point.z
            if depth+.05<.1 or depth>.23 or abs(point.x)-.05>max(.05,depth)*math.tan(math.radians(34))*2.5:continue
            exposure+=max(0,point.y+.05+depth*math.tan(math.radians(34)))**2*2
        continuity=(candidate-POSE_ELBOWS[side]).length_squared*.001 if side in POSE_ELBOWS else 0
        cost=exposure*.25+max(0,wrist_angle-math.radians(50))**2*.002+wrist_angle*wrist_angle*.00003+(candidate_bend-preferred).length_squared*.000005+continuity
        return cost,candidate,exposure
    best_angle=min((step*math.tau/24 for step in range(24)),key=lambda angle:elbow_cost(angle)[0])
    low,high=best_angle-math.tau/24,best_angle+math.tau/24
    # Continuous refinement avoids quantized fifteen-degree elbow jumps.
    for _ in range(15):
        a1=low+(high-low)*.381966;a2=low+(high-low)*.618034
        if elbow_cost(a1)[0]<elbow_cost(a2)[0]:high=a2
        else:low=a1
    best=elbow_cost((low+high)*.5)
    elbow=best[1]
    POSE_ELBOWS[side]=elbow.copy()
    for name, p, end, new_p, new_end in [
        (upper.name, shoulder, old_elbow, shoulder, elbow),
        (lower.name, old_elbow, old_wrist, elbow, target)]:
        rotation = (end-p).rotation_difference(new_end-new_p)
        pose = rotation.to_matrix().to_4x4() @ (R @ base[name])
        pose.translation = new_p
        rig.pose.bones[name].matrix = RI @ pose
        bpy.context.view_layer.update()
    desired.translation = target
    # A camera target must not force a visibly folded wrist. Stage the hand
    # within a conservative bend limit before baking; held props follow its
    # resulting native socket. Forearm pronation is distributed separately.
    forearm_direction=(target-elbow).normalized()
    wrist_bend=hand_direction.angle(forearm_direction)
    requested_wrist_bend=wrist_bend
    if wrist_bend>math.radians(50):
        correction=Quaternion().slerp(hand_direction.rotation_difference(forearm_direction),(wrist_bend-math.radians(50))/wrist_bend)
        position=desired.translation.copy()
        desired=correction.to_matrix().to_4x4() @ desired;desired.translation=position
        hand_direction=correction @ hand_direction
    hand.matrix = RI @ desired
    # Relaxation interpolates each native finger's own local quaternion. It does
    # not curl every digit around one guessed world axis.
    if curl != 1 or grip_style=='spear':
        for digit in ['Index', 'Middle', 'Ring', 'Pinky', 'Thumb']:
            for index in range(1, 5 if digit != 'Thumb' else 4):
                name = f'{digit}{index}.{side}'
                if name in rig.pose.bones:
                    bone = rig.pose.bones[name]
                    bone.rotation_mode = 'QUATERNION'
                    finger_rotation=SPEAR_FINGERS.get(name,BASE_BASIS[name].to_quaternion()) if grip_style=='spear' else BASE_BASIS[name].to_quaternion()
                    bone.rotation_quaternion = Quaternion().slerp(finger_rotation, curl)
    bpy.context.view_layer.update()
    arm_stats.append(dict(side=side, reach_error_m=max(0, delta.length-distance), wrist_bend_deg=math.degrees((target-elbow).angle(hand_direction)),requested_wrist_bend_deg=math.degrees(requested_wrist_bend),upperarm_camera_cost=best[2]))
    return grip


def palm_pose(side, center, base, curl=.45):
    """Forward-facing fingers and an upward palm, rather than a sideways fist.

    Use the anatomical metacarpal frame directly: turning a tool socket sideways
    to fake this pose bends the wrist instead of rotating the forearm.
    """
    wrist=(R @ grasp['Wrist.'+side]).translation
    forward=((R @ grasp['Middle2.'+side]).translation-wrist).normalized()
    across=((R @ grasp['Index2.'+side]).translation-(R @ grasp['Pinky2.'+side]).translation).normalized()
    normal=forward.cross(across).normalized()*(-1 if side=='R' else 1)
    def frame(y,z):
        x=y.cross(z).normalized();z=x.cross(y).normalized()
        return Matrix((x,y,z)).transposed()
    rotation=(frame(U((0,0,1)),U((0,1,0))) @ frame(forward,normal).inverted()).to_quaternion()
    return grip_pose(side,center,(0,1,0),0,base,curl,pole_weight=.5,hand_rotation=rotation)


def clench(side):
    for bone in rig.pose.bones:
        if bone.name.endswith('.'+side) and bone.name.startswith(('Index','Middle','Ring','Pinky','Thumb')):
            bone.rotation_mode='QUATERNION'
            bone.rotation_quaternion=FIST_BASIS[bone.name].to_quaternion()
    bpy.context.view_layer.update()


def smooth(t):
    return t*t*(3-2*t)


def keyed(keys, phase):
    """Camera-authored pose keys, smooth interpolation and explicit contact hold."""
    if phase <= keys[0][0]:
        return dict(keys[0][1])
    for (ta, a), (tb, b) in zip(keys, keys[1:]):
        if phase <= tb:
            t = smooth((phase-ta)/(tb-ta))
            result = {}
            for name in a:
                result[name] = tuple(x+(y-x)*t for x, y in zip(a[name], b[name])) if isinstance(a[name], tuple) else a[name]+(b[name]-a[name])*t
            return result
    return dict(keys[-1][1])


def spear(center=(.22, -.16, .28), axis=(-.43, .14, .89), support=.225, lean=0, two=True, curl=1, left_curl=1, rear_roll=240, front_roll=270):
    base = reset((0, -.12, lean))
    axis = Vector(axis).normalized()
    grip_pose('R', center, axis, rear_roll, base, curl,pole_weight=.35,grip_style='spear')
    front = Vector(center)+axis*support
    if two:
        grip_pose('L', front, axis, front_roll, base, left_curl,pole_weight=.35,grip_style='spear')
    else:
        # Empty hand stays relaxed below the image rather than a raised fist.
        grip_pose('L', (-.28, -.44, .22), (-.1, .8, .3), 0, base, .3)


finger_source=json.loads((OUT/'spear-grip.json').read_text())
if finger_source['source_sha256']!=hashlib.sha256((OUT/'source/Arms.fbx').read_bytes()).hexdigest():
    raise RuntimeError('Authored grip belongs to a different native hand skeleton')
for bone in rig.pose.bones:bone.matrix_basis=BASE_BASIS[bone.name]
for name,rotation in finger_source['rotations'].items():
    rig.pose.bones[name].rotation_mode='QUATERNION';rig.pose.bones[name].rotation_quaternion=Quaternion(rotation)
    SPEAR_FINGERS[name]=Quaternion(rotation)
bpy.context.view_layer.update()
fitted={b.name:b.matrix.copy() for b in rig.pose.bones}
for name in list(fitted):
    if name.endswith('.L'):
        other=name[:-2]+'.R'
        if other in fitted:fitted[name]=RI @ MIRROR @ (R @ fitted[other] @ rest[other].inverted() @ RI) @ MIRROR @ R @ rest[name]
mirrored_basis=local_basis(fitted)
for name in list(SPEAR_FINGERS):
    left=name[:-2]+'.L';SPEAR_FINGERS[left]=mirrored_basis[left].to_quaternion()
CLIPS = []


def bake(name, seconds, function, loop=False, contact=-1, release=-1):
    POSE_ELBOWS.clear()
    frames = max(2, round(seconds*FPS)+1)
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    previous = {}
    first_basis=None
    for frame in range(frames):
        scene.frame_set(frame+1)
        first_stat = len(arm_stats)
        function(frame/(frames-1))
        distribute_forearm_twist()
        for row in arm_stats[first_stat:]:
            row.update(clip=name, phase=frame/(frames-1))
        # Evaluate authoring, then write FK transforms into a native action.
        basis = {b.name: b.matrix_basis.copy() for b in rig.pose.bones}
        if first_basis is None:first_basis={n:m.copy() for n,m in basis.items()}
        if loop and frame==frames-1:basis=first_basis
        rig.animation_data_create(); rig.animation_data.action = action
        for bone in rig.pose.bones:
            bone.rotation_mode = 'QUATERNION'
            position, rotation, scale = basis[bone.name].decompose()
            if bone.name in previous and rotation.dot(previous[bone.name]) < 0:
                rotation.negate()
            previous[bone.name] = rotation.copy()
            bone.location = position; bone.rotation_quaternion = rotation; bone.scale = scale
            bone.keyframe_insert('location', frame=frame+1, group=bone.name)
            bone.keyframe_insert('rotation_quaternion', frame=frame+1, group=bone.name)
            bone.keyframe_insert('scale', frame=frame+1, group=bone.name)
    CLIPS.append(dict(name=name, duration=(frames-1)/FPS, loop=loop, contact=contact, release=release))
    print('AUTHORED', name, frames, flush=True)
    return action


def idle_spear(t):
    wave = math.sin(t*math.tau)
    spear(center=(.22, -.16+wave*.0015, .28), axis=(-.43+wave*.003, .14, .89))


def thrust(t, two):
    # The forward hand guides the shaft while the rear hand drives through it.
    # Its socket slides toward the rear grip during extension, then recovers.
    ready = dict(center=(.22,-.16,.28), axis=(-.43,.14,.89), support=.225, lean=0.,rear_roll=240.,front_roll=270.)
    windup = dict(center=(.25,-.17,.24), axis=(-.38,.16,.91), support=.225, lean=-.01,rear_roll=245.,front_roll=270.)
    contact = dict(center=(.13,-.13,.44 if two else .39), axis=(-.105,.105,.989), support=.15, lean=.09 if two else .05,rear_roll=190.,front_roll=250.)
    hold = dict(contact); hold['center']=(.13,-.14,.44 if two else .39)
    p = keyed([(0,ready),(.16,windup),(.36,contact),(.43,hold),(1,ready)], t)
    spear(**p, two=two)


bake('SpearReady2', 3, idle_spear, True)
bake('SpearThrust2', .72, lambda t: thrust(t,True), contact=.36)
bake('SpearReady1', 3, lambda t: spear(two=False), True)
bake('SpearThrust1', .83, lambda t: thrust(t,False), contact=.36)


def native_pose(label, phase, offset=(0,0,0)):
    pose = sample_native(label, phase)
    # New sockets are not present in the upstream clips, so restore their rest
    # basis rather than carrying a previous clip's evaluated socket transform.
    for side in ['R','L']:
        pose['Grip.'+side] = pose['Wrist.'+side] @ offsets[side]
        pose['SpearGrip.'+side] = pose['Wrist.'+side] @ spear_offsets[side]
        pose['ForearmHalf.'+side]=pose['LowerArm.'+side].copy()
        pose['ForearmFull.'+side]=pose['LowerArm.'+side].copy()
    pose['Nock.R']=pose['Wrist.R'] @ offsets['R'] @ Matrix.Translation((0,.035,0))
    for name in pose:
        pose[name].translation += RI.to_3x3() @ U(offset)
    rig.animation_data_clear()
    bases=local_basis(pose)
    for bone in rig.pose.bones:
        bone.matrix_basis=bases[bone.name]
    bpy.context.view_layer.update()


bake('ToolReady', 80/30, lambda t:native_pose('Sword_Idle',t), True)
bake('ToolWalk', 22/30, lambda t:native_pose('Sword_Walk',t), True)


def short_strike(t,kind):
    base=reset((0,-.08,0))
    ready=dict(center=(.236,-.184,.504),axis=(0,1,0),roll=0.)
    if kind=='cut':
        keys=[(0,ready),(.16,dict(center=(.30,-.14,.42),axis=(.35,.90,.25),roll=-15.)),(.4,dict(center=(.12,-.10,.58),axis=(-.52,.43,.74),roll=-15.)),(.57,dict(center=(-.05,-.17,.49),axis=(-.85,.20,.49),roll=-35.)),(1,ready)]
    elif kind=='thrust':
        keys=[(0,ready),(.15,dict(center=(.25,-.17,.42),axis=(0,.8,.6),roll=0.)),(.36,dict(center=(.03,-.07,.60),axis=(-.27,.63,.73),roll=-10.)),(.46,dict(center=(.03,-.07,.60),axis=(-.27,.63,.73),roll=-10.)),(1,ready)]
    else:
        contact=.4 if kind=='chop' else .42
        keys=[(0,ready),(.20,dict(center=(.36,.015,.36),axis=(.2,.94,.28),roll=0.)),(contact,dict(center=(.08,-.14,.52),axis=(-.18,.40,.90),roll=-12.)),(.60,dict(center=(-.03,-.24,.45),axis=(-.3,.1,.95),roll=-25.)),(1,ready)]
    grip_pose('R',base=base,**keyed(keys,t))
    grip_pose('L',(-.28,-.44,.25),(0,1,0),0,base,.3)


bake('ToolCutA',.64,lambda t:short_strike(t,'cut'),contact=.4)
bake('ToolCutB',.60,lambda t:short_strike(t,'thrust'),contact=.36)
bake('ToolBlunt',.75,lambda t:short_strike(t,'blunt'),contact=.42)
bake('ToolChop',.85,lambda t:short_strike(t,'chop'),contact=.4)
bake('ToolGuard', .15, lambda t:native_pose('Sword_Block',t))
bake('FistsReady', 80/30, lambda t:native_pose('Fists_Idle',t), True)
# The upstream block finishes with crossed, open hands close to the camera.
# A held guard needs its closed-fist middle pose; runtime blends into this hold.
bake('FistsGuard', .15, lambda t:native_pose('Fists_Block',.35))


def punch(t):
    base=reset((0,-.07,.04*math.sin(t*math.pi)))
    p=keyed([(0,dict(center=(.22,-.22,.36),axis=(0,1,0),roll=0.)),(.15,dict(center=(.25,-.21,.30),axis=(0,1,0),roll=0.)),(.38,dict(center=(.05,-.11,.60),axis=(0,1,.15),roll=-15.)),(1,dict(center=(.22,-.22,.36),axis=(0,1,0),roll=0.))],t)
    grip_pose('R',base=base,**p)
    grip_pose('L',(-.2,-.24,.34),(0,1,0),0,base)
    clench('R');clench('L')


bake('Punch',.55,punch,contact=.38)


def empty(t):
    base=reset((0,-.10,0))
    for side,x in [('R',.28),('L',-.28)]:
        grip_pose(side,(x,-.47,.25),(0,1,.1),0,base,.28)


bake('Empty',3,empty,True)


def throw_charge(t, long_spear=False):
    p=keyed([(0,dict(center=(.22,-.16,.28),axis=(-.43,.14,.89),roll=240. if long_spear else 300.)),(1,dict(center=(.43,-.025,.29),axis=(-.06,.04,.998),roll=245. if long_spear else 300.))],t)
    base=reset((0,-.12,0))
    grip_pose('R',base=base,grip_style='spear' if long_spear else 'tool',**p)
    grip_pose('L',(-.27,-.43,.23),(0,1,0),0,base,.25)


def throw_release(t, long_spear=False):
    base=reset((0,-.12,.10*math.sin(min(1,t/.55)*math.pi)))
    p=keyed([(0,dict(center=(.43,-.025,.29),axis=(-.06,.04,.998),roll=245. if long_spear else 300.,curl=1.)),(.35,dict(center=(.08,-.06,.44),axis=(-.12,.04,.992),roll=180. if long_spear else 150.,curl=.05)),(.6,dict(center=(.04,-.15,.43),axis=(-.05,-.1,.994),roll=150. if long_spear else 110.,curl=.1)),(1,dict(center=(.27,-.43,.26),axis=(0,-.25,.97) if long_spear else (0,1,0),roll=150. if long_spear else 0.,curl=.28))],t)
    grip_pose('R',base=base,grip_style='spear' if long_spear else 'tool',**p)
    grip_pose('L',(-.27,-.43,.23),(0,1,0),0,base,.25)


bake('ThrowCharge',.8,throw_charge)
bake('ThrowRelease',.62,throw_release,release=.24)
bake('SpearThrowCharge',.8,lambda t:throw_charge(t,True))
bake('SpearThrowRelease',.62,lambda t:throw_release(t,True),release=.24)


def sling_whirl(t):
    base=reset((0,-.10,0))
    grip_pose('R',(.27,-.075+.007*math.sin(t*math.tau),.29),(0,1,.1),15,base,.85)
    clench('R')
    grip_pose('L',(-.28,-.44,.25),(0,1,0),0,base,.25)


def sling_ready(t):
    base=reset((0,-.10,0))
    grip_pose('R',(.24,-.21+.0015*math.sin(t*math.tau),.40),(0,1,.1),15,base)
    clench('R')
    grip_pose('L',(-.28,-.44,.25),(0,1,0),0,base,.25)


bake('SlingReady',3,sling_ready,True)
bake('SlingWhirl',.8,sling_whirl,True)


def sling_release(t):
    base=reset((0,-.10,.035*math.sin(t*math.pi)))
    p=keyed([(0,dict(center=(.27,-.075,.29),axis=(0,1,.1),roll=15.)),(.3,dict(center=(.12,-.05,.42),axis=(-.1,.8,.6),roll=45.)),(.6,dict(center=(.14,-.15,.41),axis=(-.1,.9,.3),roll=30.)),(1,dict(center=(.24,-.21,.40),axis=(0,1,.1),roll=15.))],t)
    grip_pose('R',base=base,**p);clench('R')
    opening=keyed([(0,dict(v=0.)),(.18,dict(v=0.)),(.35,dict(v=1.)),(.7,dict(v=.6)),(1,dict(v=0.))],t)['v']
    for digit in ['Index','Thumb']:
        for i in range(1,4 if digit=='Thumb' else 5):
            name=f'{digit}{i}.R';bone=rig.pose.bones.get(name)
            if bone:bone.rotation_quaternion=FIST_BASIS[name].to_quaternion().slerp(Quaternion(),opening*.7)
    grip_pose('L',(-.28,-.44,.25),(0,1,0),0,base,.25)


bake('SlingRelease',.62,sling_release,release=.24)


BOW_ANCHOR = None


def bow(t, release=False):
    global BOW_ANCHOR
    base=reset((0,-.12,.03))
    # A view model's hidden shoulder can be staged forward while keeping its
    # visible arm lengths and skin intact. This permits a meaningful draw rather
    # than bringing the bow so close that the string hardly moves at all.
    shoulder=rig.pose.bones['Shoulder.L']
    shift=RI.to_3x3() @ U((0,-.045,.15))
    for name in base:
        bone=rig.pose.bones[name]
        if name=='Shoulder.L' or any(p.name=='Shoulder.L' for p in bone.parent_recursive):
            base[name].translation+=shift
    shoulder.matrix=base['Shoulder.L'];bpy.context.view_layer.update()
    draw=1-t if release else smooth(t)
    # A real drawing hand moves back toward the face. It does not pull the bow
    # forward through its own fingers; the nock and string use this same socket.
    # Leave clearance from the near plane: a thumb passing through that plane
    # appears as a detached oval even though its topology is continuous. The
    # drawing hand belongs near the right cheek, outside the central aim view.
    right_center=(.10,-.08,.42-.17*draw)
    if release:
        right_center=keyed([(0,dict(p=(.10,-.08,.25))),(.25,dict(p=(.20,-.07,.23))),(.6,dict(p=(.26,-.18,.29))),(1,dict(p=(.10,-.08,.42)))],t)['p']
    grip_pose('R',right_center,(0,1,0),60,base,.48 if not release else .2)
    for digit in ['Index','Middle','Ring','Pinky','Thumb']:
        for i in range(1,4 if digit=='Thumb' else 5):
            bone=rig.pose.bones.get(f'{digit}{i}.R')
            if bone is None:continue
            factor=.95 if digit in ['Thumb','Pinky'] else .4 if i<=2 else .95
            if release:factor*=.4+.6*t
            finger_basis=FIST_BASIS if digit in ['Thumb','Pinky'] or i>=3 else BASE_BASIS
            bone.rotation_mode='QUATERNION';bone.rotation_quaternion=Quaternion().slerp(finger_basis[bone.name].to_quaternion(),factor)
    bpy.context.view_layer.update()
    # The string sits at the distal finger creases, rather than an arbitrary
    # palm socket. Keep the bow rest on that forward aim line while drawing.
    hook=(rig.pose.bones['Index3.R'].tail+rig.pose.bones['Middle3.R'].tail)*.5
    nock=rig.pose.bones['Nock.R'].matrix.copy();nock.translation=hook
    rig.pose.bones['Nock.R'].matrix=nock;bpy.context.view_layer.update()
    hook_world=R@hook;hook_camera=Vector((-hook_world.x,hook_world.z,-hook_world.y))
    if not release:BOW_ANCHOR=(hook_camera.x,hook_camera.y-.075)
    aim=BOW_ANCHOR or (hook_camera.x,hook_camera.y-.075)
    grip_pose('L',(aim[0],aim[1],.65+.02*draw),(0,1,.05),0,base)


bake('BowReady',3,lambda t:bow(0),True)
bake('BowDraw',1.05,bow)
bake('BowRelease',.48,lambda t:bow(t,True),release=0)


def gather(t):
    base=reset((0,-.11,.045*math.sin(t*math.pi)))
    p=keyed([(0,dict(center=(.28,-.44,.25),axis=(0,1,0),roll=0.,curl=.25)),(.42,dict(center=(.08,-.13,.54),axis=(0,1,.1),roll=-12.,curl=.12)),(.58,dict(center=(.075,-.14,.54),axis=(0,1,.1),roll=-12.,curl=.55)),(1,dict(center=(.28,-.44,.25),axis=(0,1,0),roll=0.,curl=.25))],t)
    grip_pose('R',base=base,**p)
    grip_pose('L',(-.28,-.44,.25),(0,1,0),0,base,.25)


bake('Gather',1.05,gather,contact=.55)
def drinking(t):
    base=reset((0,-.13,0))
    p=keyed([(0,dict(x=.20,y=-.40,z=.36,curl=.35)),(.24,dict(x=.16,y=-.30,z=.47,curl=.6)),(.5,dict(x=.24,y=-.06,z=.30,curl=.6)),(.76,dict(x=.24,y=-.06,z=.30,curl=.6)),(1,dict(x=.20,y=-.40,z=.36,curl=.35))],t)
    palm_pose('R',(p['x'],p['y'],p['z']),base,p['curl'])
    grip_pose('L',(-.28,-.44,.25),(0,1,0),0,base,.3)


bake('Drink',1.5,drinking)


def hand_work(t, kind):
    """Close work stays below aim; the left hand supports the workpiece."""
    base=reset((0,-.13,0))
    if kind=='dig':
        stroke=math.sin(t*math.pi)**2
        spear(center=(.22,-.25-.08*stroke,.30+.035*stroke),axis=(-.22,-.65,.73),support=.19,lean=.02*stroke)
        return
    if kind=='knap':
        grip_pose('L',(-.09,-.16,.43),(.2,.1,.97),270,base,.85,pole_weight=.5,grip_style='spear')
        p=keyed([(0,dict(center=(.16,-.11,.38))),(.24,dict(center=(.17,-.035,.35))),(.45,dict(center=(.07,-.13,.45))),(.56,dict(center=(.07,-.13,.45))),(1,dict(center=(.16,-.11,.38)))],t)
        grip_pose('R',axis=(0,1,.1),roll=60,base=base,**p)
    elif kind=='cut':
        grip_pose('L',(-.12,-.16,.43),(.12,.08,.99),270,base,pole_weight=.5,grip_style='spear')
        stroke=math.sin(t*math.pi)**2
        grip_pose('R',(.18-.06*stroke,-.13-.025*stroke,.42+.025*stroke),(-.85,.1,.52),315,base)
    else:
        phase=math.sin(t*math.tau)
        grip_pose('L',(-.085,-.15,.43),(.1,.1,.99),270,base,.5,pole_weight=.5,grip_style='spear')
        grip_pose('R',(.085,-.15+.012*phase,.43+.008*phase),(-.1,.1,.99),240,base,.5,pole_weight=.5,grip_style='spear')


for label,kind,seconds in [('WorkKnapping','knap',1.25),('WorkCut','cut',1.2),('WorkTwist','twist',1.5),('WorkDig','dig',1.4)]:
    bake(label,seconds,lambda t,k=kind:hand_work(t,k),True)


def eating(t):
    base=reset((0,-.12,0))
    p=keyed([(0,dict(center=(.22,-.38,.30),axis=(0,1,0),roll=0.,curl=.35)),(.32,dict(center=(.20,-.035,.21),axis=(0,1,.1),roll=15.,curl=.45)),(.7,dict(center=(.20,-.035,.21),axis=(0,1,.1),roll=15.,curl=.45)),(1,dict(center=(.22,-.38,.30),axis=(0,1,0),roll=0.,curl=.35))],t)
    grip_pose('R',base=base,**p)
    grip_pose('L',(-.28,-.44,.25),(0,1,0),0,base,.25)


bake('Eat',1.2,eating)


def carrying(t):
    base=reset((0,-.13,0));breath=.002*math.sin(t*math.tau)
    palm_pose('R',(.13,-.28+breath,.42),base,.45)
    palm_pose('L',(-.13,-.28+breath,.42),base,.45)


bake('CarryLarge',3,carrying,True)

# Native mirrored takes support left-hand use and independent dual wielding.
# Keep the mesh and bind pose intact, reflecting deformation onto the opposite
# rest bone rather than reflecting the whole GameObject with a negative scale.
mirror_names=['ToolReady','ToolWalk','ToolCutA','ToolCutB','ToolBlunt','ToolChop','ToolGuard',
              'SpearReady1','SpearThrust1','SpearReady2','SpearThrust2',
              'ThrowCharge','ThrowRelease','SpearThrowCharge','SpearThrowRelease','SlingReady','SlingWhirl','SlingRelease','Punch','Gather']
for label in mirror_names:
    original=bpy.data.actions[label]
    original_info=next(c for c in CLIPS if c['name']==label)
    cached=[]
    for frame in range(round(original_info['duration']*FPS)+1):
        rig.animation_data_create();rig.animation_data.action=original
        if original.slots:rig.animation_data.action_slot=original.slots[0]
        scene.frame_set(frame+1);bpy.context.view_layer.update()
        pose={b.name:b.matrix.copy() for b in rig.pose.bones};reflected={}
        for bone in rig.pose.bones:
            if bone.name=='Nock.R':
                reflected[bone.name]=pose[bone.name].copy();continue
            other=bone.name[:-2]+('.R' if bone.name.endswith('.L') else '.L') if bone.name.endswith(('.L','.R')) else bone.name
            reflected[bone.name]=RI @ MIRROR @ (R @ pose[other] @ rest[other].inverted() @ RI) @ MIRROR @ R @ rest[bone.name]
        cached.append(local_basis(reflected))
    def mirrored(t,poses=cached):
        rig.animation_data_clear()
        for bone in rig.pose.bones:bone.matrix_basis=poses[round(t*(len(poses)-1))][bone.name]
        bpy.context.view_layer.update()
    bake(label+'_Left',original_info['duration'],mirrored,original_info['loop'],original_info['contact'],original_info['release'])

# Bake once more through Blender's native exporter. Runtime contains no controls
# or constraints, only native rig transforms and the artist's weighted mesh.
rig.animation_data_clear()
rig.data.pose_position='REST'
for bone in rig.pose.bones:
    bone.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.objects.active=arms
for obj in bpy.context.selected_objects:obj.select_set(False)
arms.select_set(True)
for modifier in list(arms.modifiers):
    if modifier.type=='SUBSURF':bpy.ops.object.modifier_apply(modifier=modifier.name)
# Match the player's four-influence GPU skinning exactly, including vertices
# created by subdivision, instead of reviewing a higher-influence Blender skin.
for vertex in arms.data.vertices:
    influences=sorted([(g.group,g.weight) for g in vertex.groups if g.weight>.00001],key=lambda x:x[1],reverse=True)
    total=sum(w for _,w in influences[:4])
    for group,weight in influences[4:]:arms.vertex_groups[group].remove([vertex.index])
    for group,weight in influences[:4]:arms.vertex_groups[group].add([vertex.index],weight/total,'REPLACE')
rig.data.pose_position='POSE'
for action in list(bpy.data.actions):
    if action.name not in {c['name'] for c in CLIPS}:
        bpy.data.actions.remove(action)
for material in list(bpy.data.materials):
    if material.users==0:bpy.data.materials.remove(material)
bpy.data.orphans_purge(do_recursive=True)
bpy.ops.file.pack_all()
rig.animation_data_create()
rig.animation_data.action=bpy.data.actions['SpearReady2']
if rig.animation_data.action.slots:
    rig.animation_data.action_slot=rig.animation_data.action.slots[0]
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'FirstPersonAuthored.blend'),compress=True)
rig.select_set(True);arms.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'Arms.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,path_mode='STRIP',embed_textures=False)
manifest=dict(version=2,fps=FPS,clips=CLIPS,bones=len(rig.data.bones),vertices=len(arms.data.vertices),triangles=sum(len(p.vertices)-2 for p in arms.data.polygons),sockets=['Grip.R','Grip.L','SpearGrip.R','SpearGrip.L','Nock.R'],authoring='Native CC0 first-person skin/rig; camera-authored FK clips. No runtime arm IK.',source_sha256=hashlib.sha256((OUT/'source/Arms.fbx').read_bytes()).hexdigest(),generator_sha256=GENERATOR_SHA,fbx_sha256=hashlib.sha256((OUT/'Arms.fbx').read_bytes()).hexdigest(),grip_sha256=hashlib.sha256((OUT/'spear-grip.json').read_text().replace('\r\n','\n').encode('utf-8')).hexdigest(),base_sha256=hashlib.sha256((OUT/'FirstPersonBase.blend').read_bytes()).hexdigest())
(OUT/'clips.json').write_text(json.dumps(manifest,indent=2)+'\n')
(EVIDENCE/'authored-pose-diagnostics.json').write_text(json.dumps(arm_stats,indent=2))
print('NATIVE FIRST-PERSON RIG EXPORTED',manifest,flush=True)
