"""Read raw Kimodo SOMA motion for offline fitting to the existing MPFB rigs."""
import json
import math
from pathlib import Path

import numpy as np
from mathutils import Matrix, Quaternion, Vector

REPO = Path(__file__).resolve().parent.parent
ROOT = REPO / 'artifacts/kimodo-first-person-demo'
C = Matrix.Rotation(math.pi / 2, 4, 'X')
SOMA_MAP = {'pelvis': 'Hips', 'spine_01': 'Spine1', 'spine_02': 'Spine2',
            'spine_03': 'Chest', 'neck_01': 'Neck1', 'head': 'Head'}
for side, label in [('l', 'Left'), ('r', 'Right')]:
    for target, source in [('clavicle', 'Shoulder'), ('upperarm', 'Arm'),
                           ('lowerarm', 'ForeArm'), ('hand', 'Hand'),
                           ('thigh', 'Leg'), ('calf', 'Shin'),
                           ('foot', 'Foot'), ('ball', 'ToeBase')]:
        SOMA_MAP[target + '_' + side] = label + source


class SomaMotion:
    license = 'NVIDIA Open Model License; generated with Kimodo SOMA RP v1.1'

    def __init__(self, folder):
        self.folder = Path(folder)
        self.path = self.folder / 'generation.json'
        self.info = json.loads(self.path.read_text())
        skel = json.loads((ROOT / 'skeleton.json').read_text())
        self.names = skel['names']
        self.parents = skel['parents']
        self.neutral = [Vector(p) for p in skel['neutral_joints']]
        # NVIDIA's neutral tensor is hip-centred, while generated roots include
        # absolute standing height. Calibrate a ground-referenced bind pose so
        # retargeting does not add the standing height a second time.
        self.bind_height = -sum(self.neutral[self.names.index(n)].y for n in ['LeftToeBase', 'RightToeBase']) / 2
        count = self.info['frames']
        self.roots = np.fromfile(self.folder / 'root_positions.f32', dtype='<f4').reshape(count, 3)
        raw = np.fromfile(self.folder / 'local_rotations_xyzw.f32', dtype='<f4').reshape(count, 30, 4)
        self.rotations = [[Quaternion((float(q[3]), *map(float, q[:3]))).normalized() for q in frame] for frame in raw]
        self.rest = {name: C @ Matrix.Translation(position + Vector((0, self.bind_height, 0))) @ C.inverted()
                     for name, position in zip(self.names, self.neutral)}

    def duration(self, name=None):
        return (self.info['frames'] - 1) / 30

    def sample(self, name, seconds):
        x = max(0, min(len(self.roots) - 1, seconds * 30))
        lo = int(x)
        hi = min(lo + 1, len(self.roots) - 1)
        alpha = x - lo
        root = Vector(self.roots[lo] * (1 - alpha) + self.roots[hi] * alpha)
        posed = []
        for i, parent in enumerate(self.parents):
            q = self.rotations[lo][i].slerp(self.rotations[hi][i], alpha)
            offset = self.neutral[i] - self.neutral[parent] if parent >= 0 else root
            local = Matrix.LocRotScale(offset, q, Vector((1, 1, 1)))
            posed.append(posed[parent] @ local if parent >= 0 else local)
        return {name: C @ matrix @ C.inverted() for name, matrix in zip(self.names, posed)}


def palm_frame(wrist, middle, thumb):
    x = (middle - wrist).normalized()
    y = thumb - wrist
    y = (y - x * x.dot(y)).normalized()
    return Matrix((x, y, x.cross(y))).transposed().to_quaternion()


def retarget_fix(semantic, targets, srcmap, rest, source):
    """Match bind directions; use palm orientation to resolve the wrist roll."""
    if semantic.startswith('hand_'):
        side = semantic[-1]
        label = 'Left' if side == 'l' else 'Right'
        target_frame = palm_frame(rest[targets[semantic]].translation,
                                  rest['middle_01_' + side].translation,
                                  rest['thumb_01_' + side].translation)
        source_frame = palm_frame(source.rest[label + 'Hand'].translation,
                                  source.rest[label + 'HandMiddleEnd'].translation,
                                  source.rest[label + 'HandThumbEnd'].translation)
        return source_frame @ target_frame.inverted()
    from human_motion_common import primary_child
    child = primary_child(semantic)
    if semantic.startswith(('upperarm', 'lowerarm', 'thigh', 'calf', 'foot')) and child in targets and child in srcmap:
        a = rest[targets[child]].translation - rest[targets[semantic]].translation
        b = source.rest[srcmap[child]].translation - source.rest[srcmap[semantic]].translation
        if a.length > .001 and b.length > .001:
            return a.rotation_difference(b)
    return Quaternion()


def movement_report(motion):
    """Expose useful whole-sequence measurements without claiming visual quality."""
    frames = [motion.sample(None, i / 30) for i in range(motion.info['frames'])]
    points = {name: np.array([tuple(f[name].translation) for f in frames]) for name in motion.names}
    return {'id': motion.info['id'], 'frames': len(frames),
            'root_travel': float(np.linalg.norm(motion.roots[-1] - motion.roots[0])),
            'right_hand_max_height': float(points['RightHand'][:, 2].max()),
            'left_foot_min_height': float(points['LeftFoot'][:, 2].min()),
            'right_foot_min_height': float(points['RightFoot'][:, 2].min()),
            'right_hand_max_speed': float(np.linalg.norm(np.diff(points['RightHand'], axis=0), axis=1).max() * 30),
            'right_hand': points['RightHand'].tolist()}
