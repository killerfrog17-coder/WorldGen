"""Shorten the fly proxy's abdomen; retain all rig, UV and motion data.

The tiny guild's original procedural topology is explicitly checked. This
appearance-only copy is rebuilt from the retained painted source each time.
"""
import hashlib,json,sys
from pathlib import Path
import bpy,numpy as np

repo=Path(__file__).resolve().parent.parent
slug='native-detritivorous-flies-guild'
root=repo/'artifacts/fauna-anatomy-revision'/slug;root.mkdir(exist_ok=True)
source=repo/'artifacts/fauna-painted-20261024'/slug/'Animal - Painted Species Review.blend'
source_sha=hashlib.sha256(source.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene
entries=json.loads(scene['deer_four_rigs'])
def retained(entry):
 body=bpy.data.objects[entry['body']];mesh=body.data;rig=bpy.data.objects[entry['rig']]
 value={'topology':[list(p.vertices) for p in mesh.polygons],
        'uvs':{uv.name:[list(v.uv) for v in uv.data] for uv in mesh.uv_layers},
        'skin':[[(body.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in mesh.vertices],
        'bones':[(b.name,b.parent.name if b.parent else None,list(b.head_local),list(b.tail_local),[list(r) for r in b.matrix_local]) for b in rig.data.bones],
        'coats':entry['painting_coats'],'actions':{}}
 for name in [*entry['actions'].values(),*entry['demos'].values()]:
  action=bpy.data.actions[name];rows=[]
  for layer in action.layers:
   for strip in layer.strips:
    for bag in strip.channelbags:
     for curve in bag.fcurves:
      rows.append([curve.data_path,curve.array_index,[(list(k.co),list(k.handle_left),list(k.handle_right),k.interpolation) for k in curve.keyframe_points]])
  value['actions'][name]=rows
 return hashlib.sha256(json.dumps(value,sort_keys=True).encode()).hexdigest()
before=[retained(e) for e in entries];evidence=[]
for entry in entries:
 body=bpy.data.objects[entry['body']];mesh=body.data;original=np.array([v.co[:] for v in mesh.vertices]);p=original.copy()
 assert len(p)==294,'Unexpected procedural fly topology'
 assert np.allclose(p[80:120].mean(0),[0,-.31,.82],atol=1e-6)
 # Full, blunt abdomen overlaps the rear thorax instead of a narrow tail and
 # wasp-like pinch. Keep the established low-poly ellipsoid topology.
 p[80:120]=(p[80:120]-[0,-.31,.82])*[1.85,.645,1.30]+[0,-.14,.82]
 # Original two antenna tubes only: short paired antennae, not long feelers.
 for start in [144,224]:
  assert np.allclose(original[start:start+8,1].mean(),.57,atol=.003)
  p[start:start+8,1]=.50+(p[start:start+8,1]-.50)*.43
  p[start:start+8,0]*=.70
 # Larger lateral compound-eye masses, with no added topology/materials.
 for start,sign in [(120,-1),(200,1)]:
  centre=np.array([sign*.075,.47,.87]);p[start:start+24]=(p[start:start+24]-centre)*[1.30,1.22,1.18]+centre+[sign*.008,0,0]
 # Overlap the existing head with the thorax instead of leaving the original
 # small daylight gap. Carry its eyes and antennae through the same offset.
 for lo,hi in [(40,80),(120,152),(200,232)]:p[lo:hi,1]-=.040
 delta=p-original
 for vertex,co in zip(mesh.vertices,p):vertex.co=co
 if mesh.shape_keys:
  for key in mesh.shape_keys.key_blocks:
   for vertex,d in zip(key.data,delta):vertex.co=np.array(vertex.co)+d
 mesh.update();assert np.isfinite(p).all()
 evidence.append({'body':body.name,'changed_vertices':np.where(np.linalg.norm(delta,axis=1)>1e-7)[0].tolist(),'max_delta':float(np.linalg.norm(delta,axis=1).max()),'triangles':sum(len(f.vertices)-2 for f in mesh.polygons)})
assert before==[retained(e) for e in entries]
bpy.context.preferences.filepaths.save_version=0
output=root/'Animal - Painted Species Review.blend';bpy.ops.wm.save_as_mainfile(filepath=str(output))
assert hashlib.sha256(source.read_bytes()).hexdigest()==source_sha
proof={'slug':slug,'source':str(source.relative_to(repo)),'source_sha256':source_sha,'checked_blend_sha256':hashlib.sha256(output.read_bytes()).hexdigest(),'all_checks_passed':True,'retained_topology_uv_skin_rest_actions_coats_sha256':before,'changed_body_regions':evidence,'method':'Reference-directed compact blunt fly abdomen, short antenna tubes and larger lateral eyes; fixed explicit procedural vertex groups. No extra triangles.','motion_modified':False,'actually_visually_inspected':False,'appearance_approved':False}
(root/'visual-native-check.json').write_text(json.dumps(proof,indent=2)+'\n')
print('FLY APPEARANCE COPY',len(entries),proof['checked_blend_sha256'])
