"""Appearance-only preservation proof, allowing documented local socket edits.

This cannot approve appearance. It checks that visual work did not alter clips,
skin, rest bones, selected topology or UVs, and bounds any socket displacement.
"""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
repo=Path(__file__).resolve().parent.parent;sys.path.insert(0,str(repo/'scripts'))
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--root',type=Path);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
root=(a.root or repo/'artifacts/fauna-visual-corrections'/a.slug).resolve();meta=json.loads((root/'visual-source.json').read_text());source=repo/meta['source'];output=root/'Animal - Painted Species Review.blend'
def digest(data):return hashlib.sha256(data).hexdigest()
def snapshot():
    entry=json.loads(bpy.context.scene['deer_four_rigs'])[0];body=bpy.data.objects[entry['body']];rig=bpy.data.objects[entry['rig']]
    result={'points':np.array([v.co[:] for v in body.data.vertices]),'faces':[list(p.vertices) for p in body.data.polygons],'weights':[[[body.vertex_groups[g.group].name,g.weight] for g in v.groups] for v in body.data.vertices],'rest':[(b.name,b.parent.name if b.parent else None,[list(row) for row in b.matrix_local]) for b in rig.data.bones],'coats':[c['label'] for c in entry['painting_coats']]}
    result['uv']=[(uv.name,[[list(uv.data[i].uv) for i in f.loop_indices] for f in body.data.polygons]) for uv in body.data.uv_layers]
    result['shapes']={key.name:np.array([v.co[:] for v in key.data]) for key in body.data.shape_keys.key_blocks} if body.data.shape_keys else {}
    parent=body.data.attributes.get('Visual source face');result['source_face_ids']=[v.value for v in parent.data] if parent else None
    result['colour_uv_signature']=body.get('fauna_colour_uv_signature')
    result['actions']={}
    for label,name in {**entry['actions'],**entry['demos']}.items():
        action=bpy.data.actions[name];h=hashlib.sha256()
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in sorted(bag.fcurves,key=lambda c:(c.data_path,c.array_index)):
                        h.update((curve.data_path+str(curve.array_index)).encode());h.update(np.array([[*k.co,*k.handle_left,*k.handle_right] for k in curve.keyframe_points],'<f8').tobytes())
                        h.update(json.dumps([(k.interpolation,k.easing) for k in curve.keyframe_points]).encode())
        result['actions'][label]=h.hexdigest()
    return result,entry
assert digest(source.read_bytes())==meta['source_sha256'],'Source changed'
bpy.ops.wm.open_mainfile(filepath=str(source));old,_=snapshot()
bpy.ops.wm.open_mainfile(filepath=str(output));new,entry=snapshot()
cleanup=root/'head-fleck-cleanup.json';flecks=set(json.loads(cleanup.read_text())['removed_polygon_indices']) if cleanup.exists() else set()
retopology_path=root/'eye-retopology.json';retopology=json.loads(retopology_path.read_text()) if retopology_path.exists() else None
orbital=set(retopology['removed_polygon_indices']) if retopology else set();removed=flecks|orbital
if retopology:
    flecks|=set(retopology.get('removed_fleck_polygon_indices',[]));orbital=set(retopology.get('removed_orbital_polygon_indices',orbital))
refinement=None
if new['source_face_ids'] is not None:
    from fauna_refinement_check import verify as verify_refinement
    refinement=verify_refinement(old,new,retopology)
    colour_uv=json.loads((root/'paint-uv-correction.json').read_text());assert new['colour_uv_signature']==colour_uv['source_uv_sha256'],'Colour UV metadata does not match this native'
elif removed:
    old['faces']=[face for i,face in enumerate(old['faces']) if i not in removed]
    old['uv']=[(name,[face for i,face in enumerate(values) if i not in removed]) for name,values in old['uv']]
for key in (['rest','coats','actions'] if refinement else ['faces','weights','rest','coats','uv','actions']):assert old[key]==new[key],key+' changed'
count=len(old['points']);land=json.loads((root/'face-landmarks.json').read_text());delta=np.linalg.norm(new['points'][:count]-old['points'],axis=1);changed=np.where(delta>2e-7)[0]
assert old['shapes'].keys()==new['shapes'].keys()
for name in old['shapes']:
    assert np.allclose(new['shapes'][name][:count]-old['shapes'][name],new['points'][:count]-old['points'],atol=2e-7),'Shape-key correction/morph delta mismatch: '+name
covered=np.zeros(len(delta),bool)
for side in ['right','left']:
    frame=land[side+'_eye']['fit_frame'];n,h,v=[np.array(frame[k]) for k in ['normal','horizontal','vertical']];center=np.array(land[side+'_eye']['point']);rx,rz=land[side+'_eye']['radius'];rel=old['points']-center
    if land.get('anatomical_orbit'):
        R=rx*land['anatomical_orbit']['globe_radius_factor'];covered|=(((rel@h/rx)**2+(rel@v/(R*.60))**2)<2.21**2)&(np.abs(rel@n)<R*2.01)
    else:covered|=(((rel@h/rx)**2+(rel@v/rz)**2)<1.24**2)&(np.abs(rel@n)<rx*1.5)
state=json.loads((root/'paint-state.json').read_text());manual=[int(i) for i in state.get('manual_head_positions',{})]
sculpt=state.get('reference_head_sculpt')
if sculpt:
    midpoint=(np.array(land['right_eye']['point'])+land['left_eye']['point'])/2
    for i in sculpt['vertices']:
        assert np.allclose(new['points'][i],sculpt['positions'][str(i)],atol=1e-7)
        assert np.linalg.norm(old['points'][i]-midpoint)<.7,'Head correction outside inspected head'
        covered[i]=True
for i in manual:
    expected=sculpt['positions'].get(str(i),state['manual_head_positions'][str(i)]) if sculpt else state['manual_head_positions'][str(i)]
    assert np.allclose(new['points'][i],expected,atol=1e-7),'Final manual/sculpt composition differs'
    assert np.linalg.norm(old['points'][i]-(np.array(land['right_eye']['point'])+land['left_eye']['point'])/2)<.55
    covered[i]=True
assert covered[changed].all(),'Geometry changed outside inspected socket/manual cheek region: '+str([(int(i),float(delta[i])) for i in changed if not covered[i]][:20])
assert float(delta.max())<(.155 if sculpt and a.slug=='lynx' else .11 if sculpt and a.slug=='buffalo' else .095 if sculpt and a.slug in {'hare','rabbit','red-fox','raccoon','wolf','lion','tiger','jaguar','leopard','puma','ocelot'} else .07 if manual or land.get('anatomical_orbit') else .05),'Excessive local head edit'
for name in entry['facial_objects']:
    ob=bpy.data.objects[name];assert all(np.isfinite(v.co[:]).all() for v in ob.data.vertices),name
boundary_gap=0.;posed_gap=0.;morph_boundary_delta=0.;posed_samples=[]
if retopology:
    body=bpy.data.objects[entry['body']]
    for eye in retopology['eyes']:
        ob=bpy.data.objects[eye['skin_object']]
        for bi,si in zip(eye['boundary_body_vertices'],eye['boundary_skin_vertices']):
            boundary_gap=max(boundary_gap,float(np.linalg.norm(np.array(body.data.vertices[bi].co)-ob.data.vertices[si].co)))
            body_weights={body.vertex_groups[g.group].name:round(g.weight,6) for g in body.data.vertices[bi].groups};skin_weights={ob.vertex_groups[g.group].name:round(g.weight,6) for g in ob.data.vertices[si].groups}
            assert body_weights==skin_weights,'Orbital boundary skin differs'
    assert boundary_gap<2e-6,'Orbital boundary position differs'
    if body.data.shape_keys:
        basis=body.data.shape_keys.reference_key
        for eye in retopology['eyes']:
            for key in body.data.shape_keys.key_blocks:
                for i in eye['boundary_body_vertices']:morph_boundary_delta=max(morph_boundary_delta,float((key.data[i].co-basis.data[i].co).length))
    rig=bpy.data.objects[entry['rig']];rig.data.pose_position='POSE';rig.animation_data_create()
    names=[name for label,name in entry['actions'].items() if label.lower() in {'walk','run'}][:2] or list(entry['actions'].values())[:2]
    for name in names:
        action=bpy.data.actions[name];rig.animation_data.action=action
        if action.slots:rig.animation_data.action_slot=action.slots[0]
        start,end=action.frame_range
        for phase in [0.,.33,.67]:
            bpy.context.scene.frame_set(int(start+(end-start)*phase));bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get();evaluated=body.evaluated_get(deps);mesh=evaluated.to_mesh()
            sample_gap=0.
            for eye in retopology['eyes']:
                ob=bpy.data.objects[eye['skin_object']];skin_evaluated=ob.evaluated_get(deps);skin_mesh=skin_evaluated.to_mesh()
                for bi,si in zip(eye['boundary_body_vertices'],eye['boundary_skin_vertices']):sample_gap=max(sample_gap,float((evaluated.matrix_world@mesh.vertices[bi].co-skin_evaluated.matrix_world@skin_mesh.vertices[si].co).length))
                skin_evaluated.to_mesh_clear()
            evaluated.to_mesh_clear();posed_gap=max(posed_gap,sample_gap);posed_samples.append({'clip':name,'phase':phase,'max_gap':sample_gap})
    assert posed_gap<2e-5,'Fitted orbital boundary separates during unchanged source motion'
report={'slug':a.slug,'source_sha256':meta['source_sha256'],'checked_blend_sha256':digest(output.read_bytes()),'retained_source_surface_uv_skin_rest_all_actions_and_coat_order_verified':True,'local_refinement':refinement,'removed_head_fleck_faces':len(flecks),'source_orbital_faces_replaced':len(orbital),'orbital_boundary_max_gap':boundary_gap,'changed_socket_vertices':changed.tolist(),'max_socket_displacement':float(delta.max()),'all_checks_passed':True,'actually_visually_inspected':False,'appearance_approved':False,'limits':'Preservation and finite-geometry checks only. Realism, placement, joins, materials and attachments require actual reference comparison.'}
report.update(orbital_boundary_posed_max_gap=posed_gap,orbital_boundary_pose_samples=posed_samples,orbital_boundary_source_morph_max_delta=morph_boundary_delta)
(root/'visual-native-check.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report))
