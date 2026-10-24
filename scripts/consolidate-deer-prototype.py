"""Retain one owner-accepted prototype from the four-rig audition, without edits."""
import argparse,json,sys,hashlib,shutil
from pathlib import Path
import bpy

ap=argparse.ArgumentParser();ap.add_argument('--source',type=Path,required=True);ap.add_argument('--output',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);a.output.mkdir(parents=True,exist_ok=True)
old=a.source/'Deer - Four Generated Rigs.blend';out=a.output/'Deer - Prototype.blend'
assert not out.exists();bpy.ops.wm.open_mainfile(filepath=str(old))
s=bpy.context.scene;entries=json.loads(s['deer_four_rigs']);keep=entries[0]
# Owner accepted the overall result without naming a preferred rig. Keep the
# initially displayed candidate, not a claim it won an automatic quality test.
for entry in entries[1:]:
    for name in [*entry['objects'],entry['rig']]:bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
    for name in [*entry['actions'].values(),*entry['demos'].values()]:
        action=bpy.data.actions.get(name)
        if action:bpy.data.actions.remove(action)
rig=bpy.data.objects[keep['rig']];rig.animation_data_create().action=bpy.data.actions[keep['actions']['Walk']]
rig.animation_data.action_slot=rig.animation_data.action.slots[0];rig.data.pose_position='POSE'
s.frame_start=0;s.frame_end=int(rig.animation_data.action.frame_range[1]);s.frame_set(0)
old_rig=Path(keep['rig_file']);shutil.copy2(old_rig,a.output/'generated-rig.npz')
shutil.copy2(old_rig.with_suffix('.json'),a.output/'generated-rig.json')
keep['rig_file']=str(a.output/'generated-rig.npz');keep.pop('mesh_file',None)
keep['approved_art']['root']=str(a.output);s['deer_four_rigs']=json.dumps([keep]);s['fauna_stage1']=json.dumps(keep['approved_art'])
for name in keep['objects']:
    obj=bpy.data.objects[name];hidden=name in [rack['object'] for rack in keep['approved_art']['antlers']]
    obj.hide_set(hidden);obj.hide_render=hidden
rig.hide_set(True)
for text in list(bpy.data.texts):bpy.data.texts.remove(text)
bpy.data.orphans_purge(do_recursive=True);bpy.ops.wm.save_as_mainfile(filepath=str(out))
bpy.ops.wm.open_mainfile(filepath=str(out));assert len(json.loads(bpy.context.scene['deer_four_rigs']))==1
assert len([o for o in bpy.data.objects if o.type=='ARMATURE'])==1
for coat in keep['approved_art']['coats']:assert coat['material'] in bpy.data.materials
for name in keep['actions'].values():assert name in bpy.data.actions
manifest={'asset':'Owner-accepted deer prototype','review_status':'Owner accepts prototype quality; motion still has known imperfections',
 'selected_candidate':1,'selection_reason':'Original displayed candidate; no owner preference among four',
 'retained':'Same finished geometry, UVs, packed textures, ear/tail traits, eyes, hairs, four optional racks, fresh rig and existing authored motions',
 'source_review_sha256':hashlib.sha256(old.read_bytes()).hexdigest(),'blend_sha256':hashlib.sha256(out.read_bytes()).hexdigest(),
 'rig':keep,'checks':{'one_armature':True,'all_coats':True,'all_actions':True,'native_reload':True},
 'review_command':f'python scripts/open-deer-mesh2motion.py --root "{a.output}" --four-rigs --blend-name "Deer - Prototype.blend" --rightmost'}
(a.output/'asset.json').write_text(json.dumps(manifest,indent=2))
print('Single self-contained deer prototype saved and reloaded.',flush=True)
