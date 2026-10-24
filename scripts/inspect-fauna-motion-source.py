"""Read native donor hierarchy/action metadata; never execute embedded scripts."""
import argparse,json,sys
from pathlib import Path
import bpy
ap=argparse.ArgumentParser();ap.add_argument('--source',type=Path,required=True);ap.add_argument('--output',type=Path)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
if a.source.suffix.lower()=='.fbx':
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(a.source.resolve()))
else:bpy.ops.wm.open_mainfile(filepath=str(a.source.resolve()))
def action_channels(action):
    rows=[]
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                rows.append({'slot_handle':bag.slot_handle,'channels':[(f.data_path,f.array_index,len(f.keyframe_points)) for f in bag.fcurves]})
    return {'name':action.name,'slots':[(s.identifier,s.handle) for s in action.slots],'bags':rows}
report={'source':str(a.source),'fps':bpy.context.scene.render.fps,'actions':[(x.name,list(x.frame_range)) for x in bpy.data.actions],
 'action_channels':[action_channels(x) for x in bpy.data.actions],
 'rigs':[{'name':o.name,'bones':[{'name':b.name,'parent':b.parent.name if b.parent else None,'deform':b.use_deform,'head':list(o.matrix_world@b.head_local),'tail':list(o.matrix_world@b.tail_local)} for b in o.data.bones]} for o in bpy.data.objects if o.type=='ARMATURE']}
if a.output:a.output.write_text(json.dumps(report,indent=2))
print('SOURCE',a.source,report['fps'],'fps','ACTIONS',report['actions'],flush=True)
for rig in report['rigs']:print('RIG',rig['name'],[(b['name'],b['parent']) for b in rig['bones']],flush=True)
