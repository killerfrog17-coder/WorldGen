"""Render actual skinned poses, not just stick skeletons, for motion review."""
import argparse
import json
import sys
from pathlib import Path
import bpy

sys.path.insert(0,str(Path(__file__).resolve().parent))
from human_motion_common import ROOT
import human_animation_stage as stage

ap=argparse.ArgumentParser();ap.add_argument('--probe',action='store_true');ap.add_argument('--all-people',action='store_true');ap.add_argument('--clip');args=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
bpy.ops.wm.open_mainfile(filepath=str(ROOT/('MPFB - Motion Probe.blend' if args.probe else 'Human - Animation Comparison.blend')))
scene=bpy.context.scene;scene.eevee.taa_render_samples=24
scene.render.resolution_x=720;scene.render.resolution_y=640;scene.render.resolution_percentage=100
out=ROOT/('probe-renders' if args.probe else 'check-renders');out.mkdir(exist_ok=True)
scene['demo_compare']=not args.probe;scene['demo_variant']='A';scene['demo_view']='body';stage.refresh()
schedule=[('Walk',p) for p in [0,.25,.5,.75]]+[('Run',p) for p in [0,.25,.5,.75]]
schedule += [('Jump',.5),('Sitting',.4),('Sword attack',.4),('Dig',.35),('Crouch',.5),('Swim',.5),('Chop wood',.4)]
schedule += [('KayKit walk',.25),('KayKit run',.5)]
if args.all_people:
    schedule=[('Walk',.25),('Run',.65),('Jump',.5),('Sword attack',.4),('Sitting',.4)]
if args.clip:schedule=[(args.clip,.5)]
for letter in ('ABCDEF' if args.all_people else 'A'):
    scene['demo_variant']=letter;stage.refresh()
    for label,phase in schedule:
        stage.select_clip(label);frame=1+round((scene.frame_end-1)*phase);scene.frame_set(frame)
        scene.render.filepath=str(out/f'{letter}-{label.replace(" ","_")}-{phase:.2f}.png')
        bpy.ops.render.render(write_still=True)
        print('POSE_RENDERED',letter,label,phase,flush=True)
print('HUMAN_MOTION_RENDERS_DONE',flush=True)
