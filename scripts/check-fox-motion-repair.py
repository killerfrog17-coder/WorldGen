"""Check the explicit Fox3 edit and densely sample actual skinned walk/run."""
import hashlib,json
from pathlib import Path
import bpy,numpy as np
repo=Path(__file__).resolve().parent.parent;folder=repo/'artifacts/fauna-repairs/red-fox';record=json.loads((folder/'run-repair.json').read_text())
def snapshot(path):
 bpy.ops.wm.open_mainfile(filepath=str(path));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0];body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']];h=hashlib.sha256()
 for name in [e['body'],*e['facial_objects']]:
  ob=bpy.data.objects[name];h.update(np.array([v.co[:] for v in ob.data.vertices],'<f4').tobytes());h.update(json.dumps([p.vertices[:] for p in ob.data.polygons]).encode())
  for uv in ob.data.uv_layers:h.update(np.array([v.uv[:] for v in uv.data],'<f4').tobytes())
  if ob.data.shape_keys:
   for key in ob.data.shape_keys.key_blocks:h.update(key.name.encode());h.update(np.array([v.co[:] for v in key.data],'<f4').tobytes())
  if name!=e['body']:h.update(json.dumps([[(ob.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in ob.data.vertices]).encode())
 rest=[(b.name,b.parent.name if b.parent else None,[list(row) for row in b.matrix_local]) for b in rig.data.bones];images={im.name:hashlib.sha256(im.packed_file.data).hexdigest() for im in bpy.data.images if im.packed_file};weights=[[(body.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in body.data.vertices]
 return s,e,body,rig,h.hexdigest(),rest,images,weights
source=repo/record['source'];out=repo/record['output'];old=snapshot(source);expected=old[4:];s,e,body,rig,geom,rest,images,weights=snapshot(out);assert geom==expected[0] and rest==expected[1] and images==expected[2];changed=[i for i,(x,y) in enumerate(zip(expected[3],weights)) if x!=y];assert set(changed)==set(record['changed_skin_vertices']);assert all(e['groups'][i]=='tail' for i in range(26,34)) and all(e['groups'][i]=='hind' for i in range(5,9));rig.animation_data_clear();rig.hide_set(False);rig.data.pose_position='POSE';bind=np.array([v.co[:] for v in body.data.vertices]);edges=np.array([edge.vertices[:] for edge in body.data.edges]);paws={}
for bone,ids in record['rigid_paws'].items():
 assert len(ids)>3;mask=np.isin(edges,ids).all(1);part=edges[mask];length=np.linalg.norm(bind[part[:,0]]-bind[part[:,1]],axis=1);mask=length>1e-6;paws[bone]=(part[mask],length[mask]);assert all(dict(weights[i]).get(e['joint_names'][int(bone)],0)>.9999 for i in ids)
clips={};surface_samples=0;worst_paw_error=0
for label in ['Walk','Run']:
 action=bpy.data.actions[e['actions'][label]];rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0];first,last=action.frame_range;feet=[];floors=[];ends=[]
 for phase in np.linspace(0,1,97):
  frame=first+(last-first)*phase;s.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update();feet.append(list(rig.pose.bones[e['joint_names'][8]].head));ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();p=np.array([v.co[:] for v in mesh.vertices]);ev.to_mesh_clear();assert np.isfinite(p).all();floors.append(float(p[:,2].min()))
  for part,length in paws.values():worst_paw_error=max(worst_paw_error,float(np.max(abs(np.linalg.norm(p[part[:,0]]-p[part[:,1]],axis=1)/length-1))))
  for name in e['facial_objects']:
   ev=bpy.data.objects[name].evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();assert np.isfinite(np.array([v.co[:] for v in mesh.vertices])).all();ev.to_mesh_clear()
  if phase in [0,1]:ends.append(p)
  surface_samples+=1
 feet=np.array(feet);excursion=np.ptp(feet,axis=0);assert np.max(excursion)>.04;loop=float(np.max(abs(ends[0]-ends[1])));assert loop<1e-5 and min(floors)>-.002;clips[label]={'right_hind_foot_excursion':excursion.tolist(),'minimum_surface_z':min(floors),'loop_max_vertex_delta':loop}
assert worst_paw_error<.0002
report={'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'checked_blend_sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'bind_body_face_uv_shapes_rest_and_packed_textures_exact':True,'changed_skin_vertices':len(changed),'changed_skin_confined_to_declared_ids':True,'actual_right_hind_and_tail_roles':True,'right_hind_moves_in_both_clips':True,'whole_paw_max_relative_edge_error':worst_paw_error,'surface_samples':surface_samples,'clips':clips,'all_checks_passed':True,'actually_visually_inspected':False,'limits':'Prototype physical retarget; finite/rigid-paw checks do not prove natural gait or collision-free contact.'};assert report['source_sha256']==record['source_sha256'] and report['checked_blend_sha256']==record['output_sha256'];(folder/'native-check.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report))
