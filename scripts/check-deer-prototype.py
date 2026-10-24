"""Small retained-file/control check; no motion-quality or game acceptance test."""
import argparse,hashlib,json,runpy,sys
from pathlib import Path
import bpy

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
manifest=json.loads((root/'asset.json').read_text());path=root/'Deer - Prototype.blend'
assert hashlib.sha256(path.read_bytes()).hexdigest()==manifest['blend_sha256']
bpy.ops.wm.open_mainfile(filepath=str(path));entries=json.loads(bpy.context.scene['deer_four_rigs']);assert len(entries)==1
entry=entries[0];assert len([o for o in bpy.data.objects if o.type=='ARMATURE'])==1
for name in entry['actions'].values():assert name in bpy.data.actions
for coat in entry['approved_art']['coats']:
    material=bpy.data.materials[coat['material']]
    for node in material.node_tree.nodes:
        if node.type=='TEX_IMAGE' and node.image:assert node.image.packed_file
runpy.run_path(str(Path(__file__).with_name('deer-four-rigs-viewer.py')))
assert bpy.ops.deer_four.model()=={'FINISHED'}
for label in ('Bind','Idle','Walk','Trot','Run','Eating','Sleep'):assert bpy.ops.deer_four.clip(clip=label)=={'FINISHED'}
for _ in range(2):assert bpy.ops.deer_four.skeleton()=={'FINISHED'}
for _ in range(3):assert bpy.ops.deer_four.view()=={'FINISHED'}
for _ in range(2):assert bpy.ops.deer_four.mode()=={'FINISHED'}
for _ in range(16):assert bpy.ops.deer_four.species()=={'FINISHED'}
print('Retained prototype: one model/rig, packed maps, source hash and review controls pass.',flush=True)
