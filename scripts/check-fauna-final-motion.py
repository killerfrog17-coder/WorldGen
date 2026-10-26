"""Independent exact-source and dense surface checks for final motion repairs."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
repo=Path(__file__).resolve().parent.parent;ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True,choices=['tiger','capuchin']);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);slug=a.slug;folder=repo/'artifacts/fauna-repairs'/slug;record=json.loads((folder/'run-repair.json').read_text());source=repo/record['source'];output=repo/record['output'];changed=set(record['changes']);assert hashlib.sha256(source.read_bytes()).hexdigest()==record['source_sha256']
def read():
 s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0];body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']];h=hashlib.sha256()
 for name in [e['body'],*e['facial_objects'],*e.get('appendage_objects',[])]:
  ob=bpy.data.objects[name];h.update(name.encode());h.update(np.array([v.co[:] for v in ob.data.vertices],'<f4').tobytes());h.update(json.dumps([list(p.vertices) for p in ob.data.polygons]).encode())
  for uv in ob.data.uv_layers:h.update(np.array([v.uv[:] for v in uv.data],'<f4').tobytes())
  if ob.data.shape_keys:
   for key in ob.data.shape_keys.key_blocks:h.update(key.name.encode());h.update(np.array([v.co[:] for v in key.data],'<f4').tobytes())
 h.update(json.dumps([(b.name,b.parent.name if b.parent else None,[list(row) for row in b.matrix_local]) for b in rig.data.bones]).encode())
 for image in sorted(bpy.data.images,key=lambda x:x.name):
  if image.packed_file:h.update(image.name.encode());h.update(bytes(image.packed_file.data))
 actions={}
 for label,name in e['actions'].items():
  if label in changed:continue
  ah=hashlib.sha256();action=bpy.data.actions[name]
  for layer in action.layers:
   for strip in layer.strips:
    for bag in strip.channelbags:
     for c in sorted(bag.fcurves,key=lambda c:(c.data_path,c.array_index)):ah.update((c.data_path+str(c.array_index)).encode());ah.update(np.array([[*k.co,*k.handle_left,*k.handle_right] for k in c.keyframe_points],'<f8').tobytes())
  actions[label]=ah.hexdigest()
 skin=[sorted((body.vertex_groups[g.group].name,round(g.weight,7)) for g in v.groups) for v in body.data.vertices]
 return s,e,body,rig,h.hexdigest(),actions,skin
bpy.ops.wm.open_mainfile(filepath=str(source));_,old,_,_,expected,actions,oldskin=read();bpy.ops.wm.open_mainfile(filepath=str(output));s,e,body,rig,actual,new_actions,newskin=read();assert actual==expected,'Bind geometry/UV/face/textures/rest skeleton changed';assert actions==new_actions,'An untouched action changed';edited=set(record['skin_changed_vertices']);assert all(oldskin[i]==newskin[i] for i in range(len(oldskin)) if i not in edited),'Skin changed outside declared correction';rig.hide_set(False);rig.data.pose_position='POSE';rig.animation_data_clear();samples=0;loops={};floors={};max_scale=0.;edge_ratios=[]
for label in ['Walk','Run']:
 action=bpy.data.actions[e['actions'][label]];rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0];first,last=action.frame_range;surfaces=[];floor=float('inf')
 for phase in np.linspace(0,1,97):
  t=first+(last-first)*phase;s.frame_set(int(t),subframe=t-int(t));bpy.context.view_layer.update();ob=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ob.to_mesh();p=np.array([v.co[:] for v in mesh.vertices]);assert np.isfinite(p).all();floor=min(floor,float(p[:,2].min()));ob.to_mesh_clear();surfaces.append(p);samples+=1
  for bone in rig.pose.bones:assert np.isfinite(np.array(bone.matrix)).all();max_scale=max(max_scale,max(abs(x-1) for x in bone.matrix.to_scale()))
  if slug=='capuchin' and label=='Run':
   for i in [0,1,2,3,4,5,6,7,46,47,48,49,50,51,52,53]:
    bone=rig.pose.bones[e['joint_names'][i]];assert np.max(np.abs(np.array(bone.matrix.to_3x3())-np.array(bone.bone.matrix_local.to_3x3())))<1e-5,'Core acquired donor limb rotation'
 loops[label]=float(np.max(np.linalg.norm(surfaces[0]-surfaces[-1],axis=1)));floors[label]=floor
 if label in changed:assert loops[label]<1e-4 and floor>=.0035,(label,loops[label],floor)
assert max_scale<1e-4
report={'slug':slug,'checked_blend_sha256':hashlib.sha256(output.read_bytes()).hexdigest(),'source_sha256':record['source_sha256'],'bind_geometry_uv_shapes_face_textures_and_rest_exact':True,'untouched_actions_exact':True,'modified_actions':list(changed),'native_motion_phases':samples,'all_sampled_surfaces_finite':True,'unit_bone_scales':True,'loop_max_vertex_drift':loops,'minimum_floor_z':floors,'skin_changed_vertices':len(record['skin_changed_vertices']),'all_checks_passed':True,'actually_visually_inspected':False,'owner_approved':False,'limits':'Prototype physical-chain repair; does not certify species gait, no-slip contact or zero self-intersection.'};(folder/'native-check.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report))
