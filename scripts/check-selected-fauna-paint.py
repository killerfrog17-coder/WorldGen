"""Native preservation/control checks for a selected model's colour copy.

No art, anatomy, natural-motion or game-performance approval is inferred.
"""
import argparse,hashlib,json,runpy,sys
from pathlib import Path
import bpy,numpy as np

repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=repo/'artifacts/fauna-painted-20261024'/a.slug
state=json.loads((root/'paint-state.json').read_text());source=repo/'artifacts/fauna-overnight-20261024'/a.slug/'Animal - Raw Audition.blend'
if state.get('source_blend_path'):source=repo/state['source_blend_path']

def snapshot(entry):
    body=bpy.data.objects[entry['body']];rig=bpy.data.objects[entry['rig']];digest=hashlib.sha256()
    digest.update(np.array([v.co[:] for v in body.data.vertices],'<f4').tobytes())
    polygons=[p.vertices[:] for p in body.data.polygons]
    digest.update(np.array(polygons,'<i4').tobytes() if all(len(p)==3 for p in polygons) else json.dumps(polygons).encode())
    group_names={g.index:g.name for g in body.vertex_groups}
    digest.update(json.dumps([sorted((group_names[g.group],g.weight) for g in v.groups) for v in body.data.vertices]).encode())
    digest.update(json.dumps([(b.name,b.parent.name if b.parent else None,[list(row) for row in b.matrix_local],b.head_local[:],b.tail_local[:]) for b in rig.data.bones]).encode())
    curves={}
    for name in [*entry['actions'].values(),*entry['demos'].values()]:
        action=bpy.data.actions[name];h=hashlib.sha256()
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        h.update((curve.data_path+str(curve.array_index)).encode())
                        h.update(np.array([(*k.co,*k.handle_left,*k.handle_right) for k in curve.keyframe_points],'<f4').tobytes())
                        h.update(str([(k.interpolation,k.handle_left_type,k.handle_right_type) for k in curve.keyframe_points]).encode())
        curves[name]=h.hexdigest()
    return {'bind_geometry_skin_rig':digest.hexdigest(),'actions':curves}

bpy.ops.wm.open_mainfile(filepath=str(source));source_entries=json.loads(bpy.context.scene['deer_four_rigs'])
before=snapshot(source_entries[state.get('source_review_candidate',state['source_candidate'])-1])
bpy.ops.wm.open_mainfile(filepath=str(root/'Animal - Painted Species Review.blend'))
entries=json.loads(bpy.context.scene['deer_four_rigs']);assert len(entries)==1
entry=entries[0];assert snapshot(entry)==before,'Selected bind geometry, skin, rig or source motions changed'
body=bpy.data.objects[entry['body']];uv=np.array([x.uv[:] for x in body.data.uv_layers.active.data]);assert np.isfinite(uv).all() and uv.min()>=-1e-5 and uv.max()<=1.00001
for coat in entry['painting_coats']:
    attr=body.data.color_attributes[coat['working_vertex_colour']]
    assert np.isfinite(np.array([x.color[:] for x in attr.data])).all()
    material=bpy.data.materials[coat['material']]
    assert all(n.image.packed_file for n in material.node_tree.nodes if n.type=='TEX_IMAGE')
    if state.get('uv_atlases_installed'):
        assert all(coat.get(key) for key in ['colour_atlas','roughness_atlas','normal_atlas'])
        assert len([n for n in material.node_tree.nodes if n.type=='TEX_IMAGE'])==3
for name in [*entry.get('facial_objects',[]),*entry.get('appendage_objects',[])]:
    feature=bpy.data.objects[name]
    assert any(mod.type=='ARMATURE' and mod.object.name==entry['rig'] for mod in feature.modifiers)
    for v in feature.data.vertices:
        assert 1<=len(v.groups)<=4 and abs(sum(g.weight for g in v.groups)-1)<1e-5
        assert all(np.isfinite(v.co))
    if feature.data.uv_layers:
        assert np.isfinite(np.array([x.uv[:] for x in feature.data.uv_layers.active.data])).all()
    for material in feature.data.materials:
        if material.use_nodes:assert all(n.image.packed_file for n in material.node_tree.nodes if n.type=='TEX_IMAGE')
viewer=runpy.run_path(str(repo/'scripts/deer-four-rigs-viewer.py'))
assert bpy.ops.deer_four.model()=={'FINISHED'}
seen=set();extra_seen={}
for _ in range(80):
    bpy.ops.deer_four.species();seen.add(body.data.materials[0].name)
    for name in entry.get('coat_objects',[]):assert bpy.data.objects[name].data.materials[0]==body.data.materials[0]
    visible={name for name in entry.get('appendage_objects',[]) if not bpy.data.objects[name].hide_get()}
    profile=next(c for c in entry['painting_coats'] if c['material']==body.data.materials[0].name)
    options=profile.get('appendage_variants')
    expected=options[viewer['state']['extra_variant']]['objects'] if options else profile.get('visible_extras',[])
    assert visible==set(expected),'Wrong age/sex appendages visible'
    extra_seen.setdefault(profile['material'],set()).add(tuple(sorted(visible)))
assert seen=={c['material'] for c in entry['painting_coats']},'Not all anatomical coat materials reachable'
for coat in entry['painting_coats']:
    if coat.get('appendage_variants'):
        assert extra_seen[coat['material']]=={tuple(sorted(option['objects'])) for option in coat['appendage_variants']},'Not all appendage options reachable'
bpy.ops.deer_four.skeleton();assert not bpy.data.objects[entry['rig']].hide_get()
bpy.ops.deer_four.skeleton();assert bpy.data.objects[entry['rig']].hide_get()
for _ in range(3):assert bpy.ops.deer_four.view()=={'FINISHED'}
assert bpy.ops.deer_four.mode()=={'FINISHED'}
for clip in ['Bind',*entry['actions']]:
    assert bpy.ops.deer_four.clip(clip=clip)=={'FINISHED'}
    bpy.context.view_layer.update();evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get())
    assert np.isfinite(np.array([v.co[:] for v in evaluated.data.vertices])).all()
    for name in [*entry.get('facial_objects',[]),*entry.get('appendage_objects',[])]:
        evaluated=bpy.data.objects[name].evaluated_get(bpy.context.evaluated_depsgraph_get())
        assert np.isfinite(np.array([v.co[:] for v in evaluated.data.vertices])).all()
assert hashlib.sha256(source.read_bytes()).hexdigest()==state['source_blend_sha256']
report={'source_bind_geometry_skin_rig_exact':True,'source_actions_exact':True,'action_count':len(before['actions']),'uv_added_valid':True,'packed_fur_source':True,'all_coats_reachable':len(seen),'numbered_controls_and_skeleton':True,'all_source_clip_start_poses_finite':True,'facial_objects_skinned':len(entry.get('facial_objects',[])),'uv_atlases_installed':state.get('uv_atlases_installed',False),'appearance_approved':False,'motion_approved':False,'stage':state['status']}
report['checked_blend_sha256']=hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest()
report['source_blend_sha256']=state['source_blend_sha256']
report['appendage_objects_skinned']=len(entry.get('appendage_objects',[]))
report['age_sex_appendage_visibility_checked']=bool(entry.get('appendage_objects'))
if entry.get('appendage_objects'):report['appendage_options_reached']={coat['label']:len(extra_seen[coat['material']]) for coat in entry['painting_coats']}
(root/'paint-native-checks.json').write_text(json.dumps(report,indent=2)+'\n')
print('PAINT COPY CHECKS PASS',a.slug,len(seen),'coats; selected geometry/skin/rig/actions exact; art remains pending',flush=True)
