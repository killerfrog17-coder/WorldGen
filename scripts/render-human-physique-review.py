"""Small front, profile and moving-frame contact sheets for actual visual review."""
import importlib.util,sys
from pathlib import Path
import bpy
REPO=Path(__file__).resolve().parent.parent
ROOT=REPO/'assets-source/humans/appearance-review'
spec=importlib.util.spec_from_file_location('physique',REPO/'scripts/human-physique-viewer.py');viewer=importlib.util.module_from_spec(spec);spec.loader.exec_module(viewer)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Human physique comparison.blend'))
s=bpy.context.scene;s.render.resolution_x=700;s.render.resolution_y=650;s.render.resolution_percentage=100
s.eevee.taa_render_samples=24
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
for person in (args[0] if args else 'ABCDEF'):
    for view,clip,frame in [('Body','Neutral',1),('Torso','Neutral',1),('Face','Neutral',1),('Side','Walk',9),('Body','Run',5)]:
        s['person']=person;s['mode']='Pair';s['view']=view;s['clip']=clip
        viewer.refresh();s.frame_set(frame);s.render.filepath=str(ROOT/f'{person}-{view}-{clip}.png');bpy.ops.render.render(write_still=True)
print('PHYSIQUE_IMAGES_READY',flush=True)
