"""Remove only tiny disconnected head flecks, retaining vertices/UVs/skin/clips."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,bmesh,numpy as np
repo=Path(__file__).resolve().parent.parent;sys.path.insert(0,str(repo/'scripts'))
from fauna_surface_components import components
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--apply',action='store_true');a=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
root=repo/'artifacts/fauna-visual-corrections'/a.slug;path=root/'Animal - Painted Species Review.blend';land=json.loads((root/'face-landmarks.json').read_text())
if a.apply and (root/'head-fleck-cleanup.json').exists():
    print('Head fleck cleanup already applied; preserving original face-index proof',a.slug);raise SystemExit
bpy.ops.wm.open_mainfile(filepath=str(path));entry=json.loads(bpy.context.scene['deer_four_rigs'])[0];body=bpy.data.objects[entry['body']]
points=np.array([v.co[:] for v in body.data.vertices]);body.data.calc_loop_triangles();faces=np.array([f.vertices[:] for f in body.data.loop_triangles]);parts=components(points,faces)
mid=(np.array(land['right_eye']['point'])+land['left_eye']['point'])/2
tiny=set();details=[]
for part in parts:
    ids=np.array(list(part));q=points[ids]
    if len(part)<=10 and np.linalg.norm(q.mean(0)-mid)<.55 and np.ptp(q,axis=0).max()<.14:
        tiny.update(part);details.append({'vertices':ids.tolist(),'span':np.ptp(q,axis=0).tolist()})
remove=[f.index for f in body.data.polygons if all(int(v) in tiny for v in f.vertices)]
print('TINY HEAD FLECKS',a.slug,len(details),'parts',len(remove),'faces',flush=True)
if not a.apply:raise SystemExit
before=hashlib.sha256(path.read_bytes()).hexdigest();bm=bmesh.new();bm.from_mesh(body.data);bm.faces.ensure_lookup_table();bmesh.ops.delete(bm,geom=[bm.faces[i] for i in remove],context='FACES_ONLY');bm.to_mesh(body.data);bm.free();body.data.update()
assert np.array_equal(points,np.array([v.co[:] for v in body.data.vertices])),'Vertex order changed'
state=json.loads((root/'paint-state.json').read_text());polys=[f.vertices[:] for f in body.data.polygons]
polybytes=json.dumps(polys).encode() if state.get('polygon_hash_encoding')=='json' else np.array(polys,np.int32).tobytes()
state['geometry_sha256']=hashlib.sha256(points.astype(np.float32).tobytes()+polybytes).hexdigest()
report={'slug':a.slug,'before_sha256':before,'removed_polygon_indices':remove,'removed_disconnected_components':details,'scope':'Only tiny detached head fleck faces; vertices and their skin remain, all retained face UVs unchanged.'}
(root/'head-fleck-cleanup.json').write_text(json.dumps(report,indent=2)+'\n');state['head_fleck_cleanup']=report
(root/'paint-state.json').write_text(json.dumps(state,indent=2)+'\n');bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(path))
