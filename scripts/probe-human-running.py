"""Render prospective captured running cycles on the existing MPFB A skin, CPU only."""
import argparse,hashlib,importlib.util,json,math,sys
from pathlib import Path
import bpy
from mathutils import Vector
REPO=Path(__file__).resolve().parent.parent
sys.path.insert(0,str(REPO/'scripts'))
from human_cmu_motion import CmuMotion
from human_motion_common import Motion
import human_animation_stage as stage
ap=argparse.ArgumentParser();ap.add_argument('--candidates',default='16_35,16_36,16_55,35_17,127_03');ap.add_argument('--output',default='artifacts/human-running-review')
args=ap.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
OUT=REPO/args.output;OUT.mkdir(parents=True,exist_ok=True)
SOURCE=REPO/'artifacts/human-animation-preview/Human - Animation Comparison.blend'
digest=hashlib.sha256(SOURCE.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
spec=importlib.util.spec_from_file_location('retarget',REPO/'scripts/build-human-animation-preview.py')
retarget=importlib.util.module_from_spec(spec);spec.loader.exec_module(retarget)
rig=next(o for o in bpy.data.collections['MPFB_A'].objects if o.type=='ARMATURE')
scene=bpy.context.scene;scene['demo_builder']='MPFB';scene['demo_variant']='A';scene['demo_compare']=False;scene['demo_lineup']=False;scene['demo_view']='side'
for obj in bpy.data.collections['MPFB_A'].objects:
    if obj.type!='MESH':continue
    if 'suit' in obj.name.lower():obj.hide_render=True
    for modifier in list(obj.modifiers):
        if modifier.type=='SUBSURF' or modifier.type=='MASK' and modifier.name.startswith('Delete.'):obj.modifiers.remove(modifier)
records=[];clips=json.loads(rig['preview_clips'])
reference=Motion(next((REPO/'artifacts/human-animation-sources/quaternius-ual1').rglob('UAL1_Standard.glb')))
for filename in args.candidates.split(','):
    motion=CmuMotion(REPO/'artifacts/human-running-sources/cmu'/f'{filename}.bvh')
    motion.align_to(reference)
    samples=[motion.sample(filename,min(i/30,motion.seconds)) for i in range(round(motion.seconds*30)+1)]
    record=retarget.animate(rig,'MPFB',('CMU '+filename,motion,filename),samples)
    record.update(motion.provenance);clips.append(record)
rig['preview_clips']=json.dumps(clips);scene.render.fps=30
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=8
scene.render.threads_mode='FIXED';scene.render.threads=2
scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100
for record in [c for c in clips if c['label'] in ['Run','Sprint'] or c['label'].startswith('CMU ' )]:
    scene['preview_clip']=record['label'];stage.refresh();rig.animation_data.action=bpy.data.actions[record['action']];rig.animation_data.action_slot=rig.animation_data.action.slots[0]
    for bone in rig.pose.bones:bone.location=(0,0,0);bone.scale=(1,1,1)
    lean=[]
    for phase in [i/20 for i in range(20)]:
        scene.frame_set(1+round((record['frames']-1)*phase))
        delta=rig.pose.bones['neck_01'].matrix.translation-rig.pose.bones['pelvis'].matrix.translation
        lean.append(math.degrees(math.atan2(-delta.y,delta.z)))
    record['mean_forward_lean_degrees']=sum(lean)/len(lean);record['max_forward_lean_degrees']=max(lean)
    for phase in [.15,.4]:
        scene.frame_set(1+round((record['frames']-1)*phase));scene.render.filepath=str(OUT/(record['label'].replace(' ','-')+f'-{phase:.2f}.png'))
        bpy.ops.render.render(write_still=True)
    records.append(record);print('RUN_REVIEW',record['label'],record['mean_forward_lean_degrees'],flush=True)
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Human - Running Review.blend'))
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==digest
(OUT/'review.json').write_text(json.dumps({'source_sha256':digest,'motions':records},indent=2),encoding='utf-8')
