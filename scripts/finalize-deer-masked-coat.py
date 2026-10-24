"""Save packed texture-only comparison and verify the preserved asset state."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import bpy
import numpy as np
ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
ap.add_argument('--source',type=Path,required=True);ap.add_argument('--check',action='store_true')
ap.add_argument('--replace-unopened',action='store_true')
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
sys.path.insert(0,str(Path(__file__).parent))
spec=importlib.util.spec_from_file_location('retarget',Path(__file__).with_name('retarget-mesh2motion-own-deer.py'))
tools=importlib.util.module_from_spec(spec);spec.loader.exec_module(tools)
def snapshot():
    rig=bpy.data.objects['AI Deer Rig'];mesh=bpy.data.objects['Locally generated deer']
    actions={}
    for action in bpy.data.actions:
        h=hashlib.sha256()
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        h.update((curve.data_path+str(curve.array_index)).encode())
                        h.update(np.array([k.co for k in curve.keyframe_points],dtype='<f4').tobytes())
                        h.update(np.array([(*k.handle_left,*k.handle_right) for k in curve.keyframe_points],dtype='<f4').tobytes())
                        h.update(str([(k.interpolation,k.handle_left_type,k.handle_right_type) for k in curve.keyframe_points]).encode())
        actions[action.name]=h.hexdigest()
    return dict(geometry_skin_uv_rig=tools.digest(mesh,rig),actions=actions)
source_hash=hashlib.sha256(a.source.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(a.source.resolve()));before=snapshot()
out=root/'Deer - Masked Coat Review.blend'
if not a.check:
    if out.exists() and not a.replace_unopened:raise FileExistsError(out)
    bpy.ops.wm.open_mainfile(filepath=str(root/'Deer - Masked Coat Working Review.blend'))
    s=bpy.context.scene;info=json.loads(s['masked_coat_review']);mat=bpy.data.materials[info['trial']]
    bpy.data.materials[info['previous']].use_fake_user=True
    for role,filename,space in [('colour','coat-colour.png','sRGB'),('roughness','coat-roughness.png','Non-Color'),('normal','coat-normal.png','Non-Color')]:
        node=mat.node_tree.nodes['Coat '+role];node.image=bpy.data.images.load(str(root/filename),check_existing=False)
        node.image.colorspace_settings.name=space;node.image.pack()
        bsdf=mat.node_tree.nodes.get('Principled BSDF')
        dst=mat.node_tree.nodes['Coat normal mapping'].inputs['Color'] if role=='normal' else bsdf.inputs['Base Color' if role=='colour' else 'Roughness']
        mat.node_tree.links.new(node.outputs['Color'],dst)
    mat.node_tree.nodes['Coat normal mapping'].inputs['Strength'].default_value=1.0
    for o in s.objects:
        if o.type=='MESH' and any(m and m.name==info['previous'] for m in o.data.materials):
            if any(mod.type=='ARMATURE' and mod.object and mod.object.name==info['rig'] for mod in o.modifiers):o.data.materials[0]=mat
    assert snapshot()==before,'Unexpected geometry, rig, skin, UV or animation change'
    bpy.ops.wm.save_as_mainfile(filepath=str(out))
bpy.ops.wm.open_mainfile(filepath=str(out));assert snapshot()==before
s=bpy.context.scene;info=json.loads(s['masked_coat_review']);mat=bpy.data.materials[info['trial']]
images=[n.image for n in mat.node_tree.nodes if n.type=='TEX_IMAGE']
assert len(images)==3 and all(im and im.packed_file and list(im.size)==[2048,2048] for im in images)
for image in images:assert np.isfinite(np.asarray(image.pixels[:])).all()
viewer=Path(__file__).with_name('deer-masked-coat-viewer.py');exec(compile(viewer.read_text(),str(viewer),'exec'),{'__file__':str(viewer)})
assert bpy.ops.deer_coat.compare(previous=True)=={'FINISHED'}
assert bpy.data.objects[info['mesh']].data.materials[0].name==info['previous']
assert bpy.ops.deer_coat.compare(previous=False)=={'FINISHED'}
for clip in ('Bind pose','Idle','Walk','Run','Eating'):
    assert bpy.ops.deer_own.clip(clip=clip)=={'FINISHED'}
    obj=bpy.data.objects[info['mesh']].evaluated_get(bpy.context.evaluated_depsgraph_get())
    points=np.empty(len(obj.data.vertices)*3,np.float32);obj.data.vertices.foreach_get('co',points);assert np.isfinite(points).all()
assert hashlib.sha256(a.source.read_bytes()).hexdigest()==source_hash
report=dict(source_sha256=source_hash,geometry_skin_uv_rig_exact=True,all_source_actions_exact=True,
    action_count=len(before['actions']),packed_maps=True,comparison_and_motion_controls=True,
    selected_deformed_poses_finite=True,game_launch=False,owner_art_approved=False)
(root/'native-validation.json').write_text(json.dumps(report,indent=2))
(root/'progress.json').write_text(json.dumps(dict(stage='Ready - masked coat review',ready=True)))
print('Packed masked-coat review validated.',flush=True)
