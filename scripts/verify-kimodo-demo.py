"""Audit the saved audition's actions, floor reference, and view consistency."""
import hashlib
import json
import math
from pathlib import Path
import sys
import bpy

REPO = Path(__file__).resolve().parent.parent
ROOT = REPO / 'artifacts/kimodo-first-person-demo'
sys.path.insert(0, str(REPO / 'scripts'))
import kimodo_demo_stage as stage

bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'Kimodo - First Person Demo.blend'))
scene = bpy.context.scene
results = []
for rig in stage.rigs():
    scene['kimodo_variant'] = rig['variant']
    assert len(rig.data.bones) == 53
    records = json.loads(rig['kimodo_clips'])
    assert {x['label'] for x in records} == {'empty_walk', 'spear_walk', 'spear_throw'}
    for record in records:
        stage.select_clip(record['label'])
        previous = {}
        max_angle = 0
        toe_min = float('inf')
        frames = [1, record['frames'] // 2, record['frames']]
        for frame in range(1, record['frames'] + 1):
            scene.frame_set(frame)
            for bone in rig.pose.bones:
                q = bone.rotation_quaternion.copy()
                assert all(math.isfinite(v) for v in q)
                if bone.name in previous:
                    angle = 2 * math.acos(min(1, abs(q.normalized().dot(previous[bone.name].normalized()))))
                    max_angle = max(max_angle, math.degrees(angle))
                previous[bone.name] = q
            toe_min = min(toe_min, *(rig.pose.bones[n].tail.z for n in ['ball_l', 'ball_r']))
        for frame in frames:
            positions = []
            for view in ['first', 'outside']:
                scene['kimodo_view'] = view
                stage.refresh()
                scene.frame_set(frame)
                positions.append([tuple(rig.pose.bones[n].head) for n in ['hand_r', 'foot_r', 'pelvis']])
            assert max(abs(a - b) for x, y in zip(*positions) for a, b in zip(x, y)) < 1e-5
        # Diagnostic ranges, not evidence of visual acceptance or solved contacts.
        assert -.25 < toe_min < .3, (rig.name, record['label'], toe_min)
        results.append({'variant': rig['variant'], 'clip': record['label'],
                        'frames': record['frames'], 'minimum_toe_tip_z': toe_min,
                        'maximum_local_bone_angle_per_frame_deg': max_angle,
                        'camera_switch_preserves_pose': True})
source = REPO / 'artifacts/human-animation-preview/Human - Animation Comparison.blend'
assert hashlib.sha256(source.read_bytes()).hexdigest() == scene['kimodo_source_sha']
assert len(results) == len(scene['kimodo_variants']) * 3
(ROOT / 'verification.json').write_text(json.dumps({'source_unchanged': True, 'results': results}, indent=2))
print('KIMODO DEMO AUDIT PASS', len(results), flush=True)
