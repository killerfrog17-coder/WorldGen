"""Scope review cameras to verified native head/forward landmarks."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy
repo=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser();p.add_argument('--slug',choices=['whale','tern'],required=True);a=p.parse_args(sys.argv[sys.argv.index('--')+1:])
root=repo/'artifacts/fauna-overnight-20261024'/a.slug;out=root/'Animal - Owner Corrected.blend';proof=json.loads((root/'owner-fix.json').read_text())
choices=json.loads((repo/'docs/fauna/OWNER_SELECTIONS.json').read_text());assert not choices['animals'][a.slug].get('manual_fix_blend')
bpy.ops.wm.open_mainfile(filepath=str(out));entries=json.loads(bpy.context.scene['deer_four_rigs']);index=proof.get('review_model_number',proof['candidate'])-1;e=entries[index]
e['review_forward_direction']=[0,-1,0]
if a.slug=='tern':e['review_head_bone']=e['joint_names'][6]
bpy.context.scene['deer_four_rigs']=json.dumps(entries);bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(out))
proof.update(output_sha256=hashlib.sha256(out.read_bytes()).hexdigest(),native_checks_pending=True,review_camera='Verified negative-Y anatomical forward; correct face landmark, no change to geometry, rig or action curves')
(root/'owner-fix.json').write_text(json.dumps(proof,indent=2)+'\n');print('Corrected review direction',a.slug)
