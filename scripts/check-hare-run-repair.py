"""Check the hare's scoped native edit and densely sample the repaired cycle."""
import argparse,hashlib,json,math,sys
from pathlib import Path
import bpy,numpy as np
ap=argparse.ArgumentParser();ap.add_argument('--slug',choices=['hare','rabbit'],default='hare');a=ap.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
repo=Path(__file__).resolve().parent.parent;folder=repo/'artifacts/fauna-repairs'/a.slug;record=json.loads((folder/'run-repair.json').read_text());source=repo/record['source'];repaired=repo/record['output'];torso_end=4 if a.slug=='hare' else 2
def action_signature(action):
 h=hashlib.sha256()
 for layer in action.layers:
  for strip in layer.strips:
   for bag in strip.channelbags:
    for curve in sorted(bag.fcurves,key=lambda c:(c.data_path,c.array_index)):
     h.update((curve.data_path+str(curve.array_index)).encode());h.update(np.array([[*k.co,*k.handle_left,*k.handle_right] for k in curve.keyframe_points],'<f8').tobytes());h.update('|'.join(k.interpolation for k in curve.keyframe_points).encode())
 return h.hexdigest()
def load(path):
 bpy.ops.wm.open_mainfile(filepath=str(path));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0];body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']];names=e['joint_names'];rig.animation_data_clear()
 geom=hashlib.sha256()
 for ob in [body,*[bpy.data.objects[n] for n in e['facial_objects']]]:
  geom.update(np.array([v.co[:] for v in ob.data.vertices],'<f4').tobytes());geom.update(json.dumps([p.vertices[:] for p in ob.data.polygons]).encode())
  for uv in ob.data.uv_layers:geom.update(np.array([v.uv[:] for v in uv.data],'<f4').tobytes())
  if ob.data.shape_keys:
   for key in ob.data.shape_keys.key_blocks:geom.update(key.name.encode());geom.update(np.array([v.co[:] for v in key.data],'<f4').tobytes())
 v=np.array([v.co[:] for v in body.data.vertices]);w=[[(body.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in body.data.vertices]
 rests=[(b.name,b.parent.name if b.parent else None,[list(row) for row in b.matrix_local]) for b in rig.data.bones]
 images={im.name:hashlib.sha256(im.packed_file.data).hexdigest() for im in bpy.data.images if im.packed_file}
 actions={k:action_signature(bpy.data.actions[n]) for k,n in e['actions'].items()}
 core=(v[:,1]<.05)&(v[:,1]>-.65)&(v[:,2]>.68)&(abs(v[:,0])<.32);edges=np.array([edge.vertices[:] for edge in body.data.edges]);edges=edges[core[edges].all(1)];lengths=np.linalg.norm(v[edges[:,0]]-v[edges[:,1]],axis=1);good=lengths>1e-5;edges=edges[good];lengths=lengths[good]
 a=bpy.data.actions[e['actions']['Run']];rig.data.pose_position='POSE';rig.animation_data_create().action=a;rig.animation_data.action_slot=a.slots[0];first,last=a.frame_range;pitch=[];distortions=[];floor=[];ends=[];gaze=[]
 for t in np.linspace(first,last,161):
  s.frame_set(int(t),subframe=t-int(t));bpy.context.view_layer.update();ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();p=np.array([ev.matrix_world@x.co for x in mesh.vertices]);ev.to_mesh_clear();assert np.isfinite(p).all();floor.append(float(p[:,2].min()));axis=rig.pose.bones[names[torso_end]].head-rig.pose.bones[names[0]].head;pitch.append(math.degrees(math.atan2(axis.z,axis.y)));gaze.append(list(rig.pose.bones['Head'].matrix.to_quaternion()));distortions.append(float(np.mean(abs(np.log(np.maximum(np.linalg.norm(p[edges[:,0]]-p[edges[:,1]],axis=1)/lengths,1e-8))))))
  if t in [first,last]:ends.append(p)
 return dict(geom=geom.hexdigest(),vertices=v,weights=w,rests=rests,images=images,actions=actions,pitch=pitch,distortions=distortions,floor=floor,loop=float(np.max(abs(ends[0]-ends[1]))),gaze=np.array(gaze))
before=load(source);after=load(repaired)
assert before['geom']==after['geom'] and before['rests']==after['rests'] and before['images']==after['images']
assert all(before['actions'][k]==after['actions'][k] for k in before['actions'] if k!='Run')
changed=[i for i,(a,b) in enumerate(zip(before['weights'],after['weights'])) if a!=b];assert set(changed)<=set(record['upper_rear_torso_skin_vertices'])|set(record.get('paw_skin_vertices',[]))
assert all(before['vertices'][i,2]>.58 for i in record['upper_rear_torso_skin_vertices']);assert all(before['vertices'][i,2]<=.27 for i in record.get('paw_skin_vertices',[]));assert min(after['floor'])>-.002;assert after['loop']<1e-5;assert max(abs(np.array(after['pitch'])))<(9 if a.slug=='hare' else 18)
gaze_shift=np.degrees(2*np.arccos(np.clip(abs(np.sum(before['gaze']*after['gaze'],axis=1)),0,1)))
report={'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'checked_blend_sha256':hashlib.sha256(repaired.read_bytes()).hexdigest(),'bind_geometry_uv_shape_keys_face_and_packed_textures_exact':True,'rest_skeleton_exact':True,'non_run_individual_actions_exact':True,'skin_outside_declared_rear_core_and_paws_exact':True,'changed_skin_vertices':len(changed),'run_dense_samples':161,'pitch_before_range':list(map(float,[min(before['pitch']),max(before['pitch'])])),'pitch_after_range':list(map(float,[min(after['pitch']),max(after['pitch'])])),'rear_core_mean_log_edge_distortion_before':float(np.mean(before['distortions'])),'rear_core_mean_log_edge_distortion_after':float(np.mean(after['distortions'])),'maximum_head_world_gaze_shift_degrees':float(gaze_shift.max()),'minimum_floor_z':min(after['floor']),'loop_max_vertex_delta':after['loop'],'all_checks_passed':True,'actually_visually_inspected':False,'limits':'Prototype correction. Dense finite/loop checks do not establish natural gait, collision-free movement or owner acceptance.'}
(folder/'native-check.json').write_text(json.dumps(report,indent=2)+'\n');assert report['source_sha256']==record['source_sha256'];assert report['checked_blend_sha256']==record['output_sha256'];print(json.dumps(report),flush=True)
