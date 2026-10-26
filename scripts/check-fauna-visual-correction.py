"""Appearance-only preservation proof, allowing documented local socket edits.

This cannot approve appearance. It checks that visual work did not alter clips,
skin, rest bones, selected topology or UVs, and bounds any socket displacement.
"""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
root=repo/'artifacts/fauna-visual-corrections'/a.slug;meta=json.loads((root/'visual-source.json').read_text());source=repo/meta['source'];output=root/'Animal - Painted Species Review.blend'
def digest(data):return hashlib.sha256(data).hexdigest()
def snapshot():
    entry=json.loads(bpy.context.scene['deer_four_rigs'])[0];body=bpy.data.objects[entry['body']];rig=bpy.data.objects[entry['rig']]
    result={'points':np.array([v.co[:] for v in body.data.vertices]),'faces':[list(p.vertices) for p in body.data.polygons],'weights':[[[body.vertex_groups[g.group].name,g.weight] for g in v.groups] for v in body.data.vertices],'rest':[(b.name,b.parent.name if b.parent else None,[list(row) for row in b.matrix_local]) for b in rig.data.bones],'coats':[c['label'] for c in entry['painting_coats']]}
    result['uv']=[(uv.name,[[list(uv.data[i].uv) for i in f.loop_indices] for f in body.data.polygons]) for uv in body.data.uv_layers]
    result['shapes']={key.name:np.array([v.co[:] for v in key.data]) for key in body.data.shape_keys.key_blocks} if body.data.shape_keys else {}
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
cleanup=root/'head-fleck-cleanup.json';removed=set(json.loads(cleanup.read_text())['removed_polygon_indices']) if cleanup.exists() else set()
if removed:
    old['faces']=[face for i,face in enumerate(old['faces']) if i not in removed]
    old['uv']=[(name,[face for i,face in enumerate(values) if i not in removed]) for name,values in old['uv']]
for key in ['faces','weights','rest','coats','uv','actions']:assert old[key]==new[key],key+' changed'
land=json.loads((root/'face-landmarks.json').read_text());delta=np.linalg.norm(new['points']-old['points'],axis=1);changed=np.where(delta>1e-7)[0]
assert old['shapes'].keys()==new['shapes'].keys()
for name in old['shapes']:
    assert np.allclose(new['shapes'][name]-old['shapes'][name],new['points']-old['points'],atol=2e-7),'Shape-key correction/morph delta mismatch: '+name
covered=np.zeros(len(delta),bool)
for side in ['right','left']:
    frame=land[side+'_eye']['fit_frame'];n,h,v=[np.array(frame[k]) for k in ['normal','horizontal','vertical']];center=np.array(land[side+'_eye']['point']);rx,rz=land[side+'_eye']['radius'];rel=old['points']-center
    covered|=(((rel@h/rx)**2+(rel@v/rz)**2)<1.24**2)&(np.abs(rel@n)<rx*1.5)
state=json.loads((root/'paint-state.json').read_text());manual=[int(i) for i in state.get('manual_head_positions',{})]
for i in manual:
    assert np.allclose(new['points'][i],state['manual_head_positions'][str(i)],atol=1e-7)
    assert np.linalg.norm(old['points'][i]-(np.array(land['right_eye']['point'])+land['left_eye']['point'])/2)<.55
    covered[i]=True
assert covered[changed].all(),'Geometry changed outside inspected socket/manual cheek region'
assert float(delta.max())<(.07 if manual else .05),'Excessive local head edit'
for name in entry['facial_objects']:
    ob=bpy.data.objects[name];assert all(np.isfinite(v.co[:]).all() for v in ob.data.vertices),name
report={'slug':a.slug,'source_sha256':meta['source_sha256'],'checked_blend_sha256':digest(output.read_bytes()),'retained_topology_uv_skin_rest_all_actions_and_coat_order_exact':True,'removed_head_fleck_faces':len(removed),'changed_socket_vertices':changed.tolist(),'max_socket_displacement':float(delta.max()),'all_checks_passed':True,'actually_visually_inspected':False,'appearance_approved':False,'limits':'Preservation and finite-geometry checks only. Realism, placement, joins, materials and attachments require actual reference comparison.'}
(root/'visual-native-check.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report))
