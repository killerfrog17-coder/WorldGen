"""Read the downloaded CC0 motion sources and sample their native hierarchies."""
import json
import math
import struct
from pathlib import Path

import numpy as np
from mathutils import Matrix, Quaternion, Vector

REPO = Path(__file__).resolve().parent.parent
ROOT = REPO / 'artifacts/human-animation-preview'
C = Matrix.Rotation(math.pi / 2, 4, 'X')


class Motion:
    def __init__(self, path):
        self.path = path
        self.raw = path.read_bytes()
        length = struct.unpack_from('<I', self.raw, 12)[0]
        self.doc = json.loads(self.raw[20:20 + length])
        self.binary = 20 + length + 8
        self.nodes = self.doc['nodes']
        self.names = {n.get('name', str(i)): i for i, n in enumerate(self.nodes)}
        self.parents = {c: p for p, n in enumerate(self.nodes) for c in n.get('children', [])}
        self.clips = {a['name']: a for a in self.doc['animations']}
        self.rest = self.sample(None, 0)

    def array(self, index):
        a = self.doc['accessors'][index]
        view = self.doc['bufferViews'][a['bufferView']]
        dtype = np.dtype({5126: '<f4', 5125: '<u4', 5123: '<u2', 5121: 'u1'}[a['componentType']])
        width = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}[a['type']]
        return np.ndarray((a['count'], width), dtype=dtype, buffer=self.raw,
                          offset=self.binary + view.get('byteOffset', 0) + a.get('byteOffset', 0),
                          strides=(view.get('byteStride', dtype.itemsize * width), dtype.itemsize))

    def duration(self, name):
        return max(float(self.array(s['input'])[-1, 0]) for s in self.clips[name]['samplers'])

    def sample(self, name, time):
        values = {}
        if name:
            animation = self.clips[name]
            for channel in animation['channels']:
                sampler = animation['samplers'][channel['sampler']]
                times = self.array(sampler['input'])[:, 0]
                data = self.array(sampler['output'])
                lo = max(0, min(len(times) - 1, int(np.searchsorted(times, time, side='right')) - 1))
                hi = min(len(times) - 1, lo + 1)
                alpha = max(0, min(1, (time - times[lo]) / max(1e-8, times[hi] - times[lo])))
                kind = channel['target']['path']
                assert sampler.get('interpolation', 'LINEAR') in ['LINEAR', 'STEP']
                if sampler.get('interpolation') == 'STEP': alpha = 0
                if kind == 'rotation':
                    x, y = data[lo], data[hi]
                    value = Quaternion((float(x[3]), *map(float, x[:3]))).slerp(
                        Quaternion((float(y[3]), *map(float, y[:3]))), float(alpha))
                else: value = Vector(data[lo] * (1-alpha) + data[hi] * alpha)
                values[channel['target']['node'], kind] = value
        world = {}
        def visit(i):
            if i in world: return world[i]
            n = self.nodes[i]
            t = values.get((i, 'translation'), Vector(n.get('translation', [0, 0, 0])))
            xyzw = n.get('rotation', [0, 0, 0, 1])
            q = values.get((i, 'rotation'), Quaternion((xyzw[3], *xyzw[:3])))
            s = values.get((i, 'scale'), Vector(n.get('scale', [1, 1, 1])))
            local = Matrix.LocRotScale(t, q, s)
            world[i] = visit(self.parents[i]) @ local if i in self.parents else local
            return world[i]
        for i in range(len(self.nodes)): visit(i)
        return {self.nodes[i].get('name', str(i)): C @ m @ C.inverted() for i, m in world.items()}


def sources():
    base = REPO/'artifacts/human-animation-sources'
    q1 = Motion(next((base/'quaternius-ual1').rglob('UAL1_Standard.glb')))
    q2 = Motion(next((base/'quaternius-ual2').rglob('UAL2_Standard.glb')))
    kay = Motion(next((base/'kaykit').rglob('Rig_Medium_MovementBasic.glb')))
    tools = Motion(next((base/'kaykit').rglob('Rig_Medium_Tools.glb')))
    clips = [
        ('Idle', q1, 'Idle_Loop'), ('Walk', q1, 'Walk_Loop'),
        ('Run', q1, 'Jog_Fwd_Loop'), ('Sprint', q1, 'Sprint_Loop'),
        ('KayKit walk', kay, 'Walking_A'), ('KayKit run', kay, 'Running_A'),
        ('Crouch', q1, 'Crouch_Fwd_Loop'), ('Jump', kay, 'Jump_Full_Short'),
        ('Sit down', q1, 'Sitting_Enter'), ('Sitting', q1, 'Sitting_Idle_Loop'),
        ('Stand up', q1, 'Sitting_Exit'), ('Sword attack', q1, 'Sword_Attack'),
        ('Chop wood', q2, 'TreeChopping_Loop'), ('Dig', tools, 'Digging'),
        ('Swim', q1, 'Swim_Fwd_Loop'),
    ]
    return clips


SEMANTICS = {'pelvis': 'Hips', 'spine_01': 'Spine', 'spine_02': 'Chest',
             'spine_03': 'UpperChest', 'neck_01': 'Neck', 'head': 'Head'}
for side, label in [('l', 'Left'), ('r', 'Right')]:
    for base, semantic in [('clavicle', 'Shoulder'), ('upperarm', 'UpperArm'),
                           ('lowerarm', 'LowerArm'), ('hand', 'Hand'),
                           ('thigh', 'UpperLeg'), ('calf', 'LowerLeg'),
                           ('foot', 'Foot'), ('ball', 'Toes')]:
        SEMANTICS[base+'_'+side] = label+semantic
    for finger in ['thumb', 'index', 'middle', 'ring', 'pinky']:
        for number, joint in [(1, 'Proximal'), (2, 'Intermediate'), (3, 'Distal')]:
            SEMANTICS[f'{finger}_{number:02d}_{side}'] = label+('Little' if finger=='pinky' else finger.title())+joint


def source_map(motion):
    if 'pelvis' in motion.rest:
        return {name: ('Head' if name=='head' else name) for name in SEMANTICS if ('Head' if name=='head' else name) in motion.rest}
    result = {'pelvis': 'hips', 'spine_01': 'spine', 'spine_03': 'chest', 'head': 'head'}
    for side in ['l', 'r']:
        for target, source in [('upperarm', 'upperarm'), ('lowerarm', 'lowerarm'),
                                ('hand', 'wrist'), ('thigh', 'upperleg'), ('calf', 'lowerleg'),
                                ('foot', 'foot'), ('ball', 'toes')]:
            result[target+'_'+side] = source+'.'+side
    return result


def primary_child(name):
    base = {'pelvis': 'spine_01', 'spine_01': 'spine_02', 'spine_02': 'spine_03',
            'spine_03': 'neck_01', 'neck_01': 'head'}
    for side in ['l', 'r']:
        for a,b in [('clavicle','upperarm'),('upperarm','lowerarm'),('lowerarm','hand'),
                     ('hand','middle_01'),('thigh','calf'),('calf','foot'),('foot','ball')]:
            base[a+'_'+side] = b+'_'+side
        for finger in ['thumb','index','middle','ring','pinky']:
            for number in [1,2]: base[f'{finger}_{number:02d}_{side}'] = f'{finger}_{number+1:02d}_{side}'
    return base.get(name)
