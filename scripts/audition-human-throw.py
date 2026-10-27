"""Isolated CPU audition of an authored CC0 throw on the existing MPFB rig.

Never writes the source audition, runtime exports or Unity assets.
"""
import hashlib
import importlib.util
import json
from pathlib import Path
import sys

import bpy

REPO = Path(__file__).resolve().parent.parent
SOURCE = REPO / 'artifacts/human-animation-preview/Human - Animation Comparison.blend'
OUTPUT = REPO / 'artifacts/human-throw-audition'
sys.path.insert(0, str(REPO / 'scripts'))
from human_motion_common import Motion
import human_animation_stage as stage

OUTPUT.mkdir(parents=True, exist_ok=True)
source_hash = hashlib.sha256(SOURCE.read_bytes()).hexdigest()
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
spec = importlib.util.spec_from_file_location('human_retarget', REPO / 'scripts/build-human-animation-preview.py')
retarget = importlib.util.module_from_spec(spec)
spec.loader.exec_module(retarget)
motion = Motion(next((REPO / 'artifacts/human-animation-sources/quaternius-ual2').rglob('UAL2_Standard.glb')))
clip_name = 'OverhandThrow'
duration = motion.duration(clip_name)
samples = [motion.sample(clip_name, min(i / 30, duration)) for i in range(round(duration * 30) + 1)]
collection = bpy.data.collections['MPFB_A']
rig = next(o for o in collection.objects if o.type == 'ARMATURE')
record = retarget.animate(rig, 'MPFB', ('Throw', motion, clip_name), samples)
rig['preview_clips'] = json.dumps(json.loads(rig['preview_clips']) + [record])
rig.animation_data.action = bpy.data.actions[record['action']]
rig.animation_data.action_slot = rig.animation_data.action.slots[0]
rig.data.pose_position = 'POSE'
for bone in rig.pose.bones:
    bone.location = (0, 0, 0)
    bone.scale = (1, 1, 1)
for obj in collection.objects:
    if obj.type != 'MESH':
        continue
    if 'suit' in obj.name.lower():
        obj.hide_render = True
    for modifier in list(obj.modifiers):
        if modifier.type == 'SUBSURF' or modifier.type == 'MASK' and modifier.name.startswith('Delete.'):
            obj.modifiers.remove(modifier)
scene = bpy.context.scene
scene['demo_builder'] = 'MPFB'
scene['demo_variant'] = 'A'
scene['demo_compare'] = False
scene['demo_lineup'] = False
scene['demo_view'] = 'side'
scene['preview_clip'] = 'Throw'
stage.refresh()
scene.frame_start, scene.frame_end = 1, len(samples)
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 8
scene.render.threads_mode = 'FIXED'
scene.render.threads = 2
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
targets = retarget.mapping(rig, 'MPFB')
hand, shoulder = rig.pose.bones[targets['hand_r']], rig.pose.bones[targets['upperarm_r']]
positions = []
for frame in range(1, len(samples) + 1):
    scene.frame_set(frame)
    point = rig.matrix_world @ hand.matrix.translation
    upper = rig.matrix_world @ shoulder.matrix.translation
    positions.append({'frame': frame, 'seconds': (frame - 1) / 30, 'hand': list(point), 'shoulder': list(upper)})
candidates = [(i, -(positions[i + 1]['hand'][1] - p['hand'][1]) * 30) for i, p in enumerate(positions[:-1]) if p['hand'][2] > p['shoulder'][2] - .25]
release, speed = max(candidates, key=lambda pair: pair[1])
record.update(source_native_sha256=source_hash, duration_seconds=duration,
              estimated_release_seconds=positions[release]['seconds'], forward_speed_mps=speed,
              note='Velocity marker proposal only; actual poses require inspection.', positions=positions)
for phase in (0, .25, .5, .75, .95):
    scene.frame_set(1 + round((len(samples) - 1) * phase))
    scene.render.filepath = str(OUTPUT / f'throw-side-{phase:.2f}.png')
    bpy.ops.render.render(write_still=True)
    print('THROW_POSE', phase, flush=True)
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT / 'Human - Throw Audition.blend'))
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == source_hash
(OUTPUT / 'motion.json').write_text(json.dumps(record, indent=2), encoding='utf-8')
print('THROW_AUDITION_DONE', duration, record['estimated_release_seconds'], flush=True)
