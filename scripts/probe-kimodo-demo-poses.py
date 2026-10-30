"""Check the fitted rig/skin really agree across the first/outside cameras."""
import sys
from pathlib import Path
import json
import bpy
from mathutils import Vector

REPO = Path(__file__).resolve().parent.parent
ROOT = REPO / 'artifacts/kimodo-first-person-demo'
sys.path.insert(0, str(REPO / 'scripts'))
import kimodo_demo_stage as stage
from kimodo_demo_motion import SomaMotion
bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'Kimodo - First Person Demo.blend'))
scene = bpy.context.scene
rig = stage.selected()
stage.select_clip('spear_throw')
take = json.loads((ROOT / 'motions/spear_throw/selected.json').read_text())['take']
source = SomaMotion(ROOT / 'motions/spear_throw' / take)
records = []
for view in ['outside', 'first', 'outside']:
    scene['kimodo_view'] = view
    stage.refresh()
    for frame in [48, 60, 72, 48]:
        scene.frame_set(frame)
        stage.update_camera()
        deps = bpy.context.evaluated_depsgraph_get()
        erig = rig.evaluated_get(deps)
        row = {'view': view, 'frame': frame,
               'hand': list(rig.pose.bones['hand_r'].head),
               'evaluated_hand': list(erig.pose.bones['hand_r'].head),
               'source_hand': list(source.sample(None, (frame - 1) / 30)['RightHand'].translation),
               'hip': list(rig.pose.bones['pelvis'].head),
               'foot': list(rig.pose.bones['foot_r'].head),
               'camera': list(scene.camera.location)}
        for name in [f'MPFB_{rig["variant"]}.body', f'MPFB_{rig["variant"]}.body — local head concealed']:
            ob = bpy.data.objects[name]
            row[name] = {'armature': [(m.object.name if m.object else None) for m in ob.modifiers if m.type == 'ARMATURE']}
        records.append(row)
(ROOT / 'pose-probe.json').write_text(json.dumps(records, indent=2))
print(json.dumps(records, indent=2), flush=True)
