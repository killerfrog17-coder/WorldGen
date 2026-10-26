"""Native source preservation and paired-eye placement checks, no art approval."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
repo=Path(__file__).resolve().parent.parent;sys.path.insert(0,str(repo/'scripts'));from fauna_phone_clips import review_clip_labels
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);folder=repo/'artifacts/fauna-review-revisions'/a.slug;meta=json.loads((folder/'revision-source.json').read_text());source=repo/meta['source'];output=folder/'Animal - Painted Species Review.blend'
def signature(entry):
 body=bpy.data.objects[entry['body']];rig=bpy.data.objects[entry['rig']];h=hashlib.sha256();h.update(np.array([v.co[:] for v in body.data.vertices],'<f4').tobytes());h.update(json.dumps([p.vertices[:] for p in body.data.polygons]).encode());h.update(json.dumps([[(body.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in body.data.vertices]).encode())
 for uv in body.data.uv_layers:h.update(np.array([x.uv[:] for x in uv.data],'<f4').tobytes())
 if body.data.shape_keys:
  for key in body.data.shape_keys.key_blocks:h.update(key.name.encode());h.update(np.array([v.co[:] for v in key.data],'<f4').tobytes())
 h.update(json.dumps([(b.name,b.parent.name if b.parent else None,[list(row) for row in b.matrix_local]) for b in rig.data.bones]).encode())
 for label,name in sorted({**entry['actions'],**entry['demos']}.items()):
  action=bpy.data.actions[name];h.update(label.encode())
  for layer in action.layers:
   for strip in layer.strips:
    for bag in strip.channelbags:
     for curve in sorted(bag.fcurves,key=lambda c:(c.data_path,c.array_index)):h.update((curve.data_path+str(curve.array_index)).encode());h.update(np.array([[*k.co,*k.handle_left,*k.handle_right] for k in curve.keyframe_points],'<f8').tobytes())
 return h.hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(source));old=json.loads(bpy.context.scene['deer_four_rigs'])[0];expected=signature(old);bpy.ops.wm.open_mainfile(filepath=str(output));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0];assert signature(e)==expected,'Body/skin/rest/actions changed during appearance work';rig=bpy.data.objects[e['rig']];rig.hide_set(False);rig.data.pose_position='POSE';rig.animation_data_clear();samples=0
for label in review_clip_labels(e['actions']):
 action=bpy.data.actions[e['actions'][label]];rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0];first,last=action.frame_range
 for phase in np.linspace(0,1,17):
  t=first+(last-first)*phase;s.frame_set(int(t),subframe=t-int(t));bpy.context.view_layer.update()
  for name in [e['body'],*e['facial_objects']]:
   ev=bpy.data.objects[name].evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();p=np.array([v.co[:] for v in mesh.vertices]);assert np.isfinite(p).all(),(label,phase,name);ev.to_mesh_clear()
  samples+=1
land=json.loads((folder/'face-landmarks.json').read_text());F=np.array(land['front_axis']);S=np.array(land['side_axis']);R=np.array(land['right_eye']['point']);L=np.array(land['left_eye']['point']);assert (abs((R-L)@F)<1e-5 or land['paired_eye_revision']['head_plane'].get('manual_visual_adjustment',{}).get('independent_socket_centers')) and abs(R[2]-L[2])<1e-5;assert np.allclose(land['right_eye']['radius'],land['left_eye']['radius']);eyes=[n for n in e['facial_objects'] if n.endswith(' eye')];assert len(eyes)==2
report={'slug':a.slug,'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'checked_blend_sha256':hashlib.sha256(output.read_bytes()).hexdigest(),'body_geometry_skin_uv_shapes_rest_and_actions_exact':True,'paired_eye_height_match':True,'paired_eye_depth_matches':bool(abs((R-L)@F)<1e-5),'independently_reviewed_socket_depths':bool(land['paired_eye_revision']['head_plane'].get('manual_visual_adjustment',{}).get('independent_socket_centers')),'paired_eye_apertures_match':True,'clip_phases':samples,'all_sampled_surfaces_finite':True,'coats':len(e['painting_coats']),'all_checks_passed':True,'actually_visually_inspected':False,'limits':'Technical preservation/placement/finite checks; natural anatomy, reflections, gait and art remain visually reviewed and owner pending.'};assert report['source_sha256']==meta['source_sha256'];(folder/'revision-native-check.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report))
