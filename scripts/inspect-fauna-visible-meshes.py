"""Read-only inventory to distinguish body paint from leftover feature objects."""
import argparse,json,sys
from pathlib import Path
import bpy,numpy as np
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--root',type=Path);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=(a.root or repo/'artifacts/fauna-anatomy-revision'/a.slug).resolve()
bpy.ops.wm.open_mainfile(filepath=str(root/'Animal - Painted Species Review.blend'));entry=json.loads(bpy.context.scene['deer_four_rigs'])[0];result=[]
for ob in bpy.context.scene.objects:
 if ob.type!='MESH' or ob.hide_render:continue
 p=np.array([ob.matrix_world@v.co for v in ob.data.vertices]);role=next((key for key in ['facial_objects','coat_objects','appendage_objects'] if ob.name in entry.get(key,[])),'body' if ob.name==entry['body'] else 'untracked')
 basis_delta=float(np.max(np.linalg.norm(np.array([v.co[:] for v in ob.data.shape_keys.reference_key.data])-np.array([v.co[:] for v in ob.data.vertices]),axis=1))) if ob.data.shape_keys else 0.
 result.append({'name':ob.name,'role':role,'vertices':len(p),'minimum':p.min(0).tolist(),'maximum':p.max(0).tolist(),'mesh_basis_max_delta':basis_delta,'active_shapes':{k.name:k.value for k in ob.data.shape_keys.key_blocks if k.value} if ob.data.shape_keys else {},'material_indices':sorted({f.material_index for f in ob.data.polygons}),'materials':[m.name if m else None for m in ob.data.materials],'uv_layers':[{'name':uv.name,'active':uv.active,'active_render':uv.active_render} for uv in ob.data.uv_layers],'normal_maps':[{'material':m.name,'uv':n.uv_map,'space':n.space} for m in ob.data.materials if m and m.use_nodes for n in m.node_tree.nodes if n.type=='NORMAL_MAP']})
(root/'visible-mesh-inventory.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
