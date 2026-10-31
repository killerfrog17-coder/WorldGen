"""Check the hair replacement at useful viewing distances before presentation."""
import importlib.util,sys
from pathlib import Path
import bpy
from mathutils import Vector
REPO=Path(__file__).resolve().parent.parent;ROOT=REPO/'assets-source/humans/appearance-review'
spec=importlib.util.spec_from_file_location('hair_viewer',REPO/'scripts/human-physique-viewer.py');view=importlib.util.module_from_spec(spec);spec.loader.exec_module(view)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Human physique comparison.blend'))
s=bpy.context.scene;s.render.resolution_x=1000;s.render.resolution_y=1000;s.render.resolution_percentage=100;s.eevee.taa_render_samples=64
people=sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else 'B'
for person in people:
    for label,v,clip,frame in [('legs','Legs','Neutral',1),('body','Body','Neutral',1),('torso','Torso','Neutral',1),('walk','Body','Walk',9),('run','Body','Run',12)]:
        s['person']=person;s['mode']='After';s['view']=v;s['clip']=clip;view.refresh();s.frame_set(frame)
        s.render.filepath=str(ROOT/f'hair-{person}-{label}.png');bpy.ops.render.render(write_still=True)
    s['view']='Legs';s['clip']='Neutral';view.refresh();s.frame_set(1)
    s.camera.location=(.8,-1.3,.48);s.camera.rotation_euler=(Vector((0,0,.48))-s.camera.location).to_track_quat('-Z','Y').to_euler()
    s.render.filepath=str(ROOT/f'hair-{person}-legs-oblique.png');bpy.ops.render.render(write_still=True)
print('HAIR_DETAIL_IMAGES_READY',flush=True)
