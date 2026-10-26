"""Bounded manual smoothing of explicitly inspected head-face pixel picks."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=repo/'artifacts/fauna-visual-corrections'/a.slug
path=root/'Animal - Painted Species Review.blend';state=json.loads((root/'paint-state.json').read_text());assert not state.get('manual_head_positions'),'Already applied'
picks=json.loads((root/'inspected-face-picks.json').read_text());ids=np.array(sorted({i for face in picks for i in face['vertices']}));assert len(ids)<=16
bpy.ops.wm.open_mainfile(filepath=str(path));entry=json.loads(bpy.context.scene['deer_four_rigs'])[0];body=bpy.data.objects[entry['body']];p=np.array([v.co[:] for v in body.data.vertices]);before=p.copy();neighbors=[set() for _ in p]
for face in body.data.polygons:
 for i in face.vertices:neighbors[i].update(j for j in face.vertices if j!=i)
for _ in range(8):
 q=p.copy()
 for i in ids:q[i]=p[i]*.25+np.mean(p[list(neighbors[i])],axis=0)*.75
 p=q
shift=p-before;length=np.linalg.norm(shift,axis=1);p=before+shift*np.minimum(1,.065/np.maximum(length,1e-8))[:,None]
for i in ids:body.data.vertices[int(i)].co=p[i]
body.data.update();state['manual_head_positions']={str(i):p[i].tolist() for i in ids};state['manual_head_geometry_scope']='Only vertices of explicitly ray-picked three cheek fins; bounded local smoothing, topology/skin/UV/rest/actions retained.'
faces=[face.vertices[:] for face in body.data.polygons];polybytes=json.dumps(faces).encode() if state.get('polygon_hash_encoding')=='json' else np.array(faces,np.int32).tobytes();state['geometry_sha256']=hashlib.sha256(p.astype(np.float32).tobytes()+polybytes).hexdigest()
(root/'paint-state.json').write_text(json.dumps(state,indent=2)+'\n');bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(path));print('PICKED HEAD SMOOTH',a.slug,len(ids),float(np.linalg.norm(p-before,axis=1).max()))
