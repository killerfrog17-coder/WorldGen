"""Render actual fitted Kimodo poses from the review's first/outside cameras."""
import argparse
import json
from pathlib import Path
import sys
import bpy

REPO = Path(__file__).resolve().parent.parent
ROOT = REPO / 'artifacts/kimodo-first-person-demo'
sys.path.insert(0, str(REPO / 'scripts'))
import kimodo_demo_stage as stage

ap = argparse.ArgumentParser()
ap.add_argument('--variant', default='B')
ap.add_argument('--clip')
ap.add_argument('--all-frames', action='store_true')
args = ap.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'Kimodo - First Person Demo.blend'))
scene = bpy.context.scene
scene.render.resolution_x = 960
scene.render.resolution_y = 540
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene['kimodo_variant'] = args.variant
folder = ROOT / 'renders'
folder.mkdir(exist_ok=True)
clips = json.loads(stage.selected()['kimodo_clips'])
records = []
for record in clips:
    clip = record['label']
    if args.clip and args.clip != clip:
        continue
    stage.select_clip(clip)
    count = record['frames']
    frames = list(range(1, count + 1, 10)) if args.all_frames else [round(count * p) for p in [.18, .48, .76]]
    if clip == 'spear_throw' and not args.all_frames:
        release = record['release_frame']
        frames = [max(1, release - 12), release, min(count, release + 12)]
    for view in ['outside', 'first', 'down']:
        scene['kimodo_view'] = 'first' if view == 'down' else view
        scene['kimodo_pitch'] = -65.0 if view == 'down' else -15.0
        stage.refresh()
        for frame in frames:
            scene.frame_set(frame)
            stage.update_camera()
            name = f'{args.variant}-{clip}-{view}-{frame:03d}.png'
            scene.render.filepath = str(folder / name)
            bpy.ops.render.render(write_still=True)
            records.append({'variant': args.variant, 'clip': clip, 'view': view, 'frame': frame, 'file': name})
            print('REVIEW_RENDER', name, flush=True)
(folder / ('index-' + args.variant + '.json')).write_text(json.dumps(records, indent=2))
