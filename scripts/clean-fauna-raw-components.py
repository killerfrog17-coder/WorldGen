"""Remove tiny disconnected inference debris before a strict triangle budget.

No smoothing, sculpting, remeshing or new vertices. Otherwise thousands of
irreducible tiny components can consume the budget and collapse the main body.
"""
import argparse,hashlib,json
from pathlib import Path
import numpy as np
import trimesh
ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True);ap.add_argument('--triangles',type=int,required=True)
a=ap.parse_args();root=a.root.resolve();source=root/'raw.glb';out=root/'raw-clean.glb';report=root/'mesh-component-cleanup.json'
assert source.exists() and not out.exists() and not report.exists()
mesh=trimesh.load(source,force='mesh',process=False)
groups=trimesh.graph.connected_components(mesh.face_adjacency,min_len=1,nodes=np.arange(len(mesh.faces)))
groups=sorted(groups,key=len,reverse=True);threshold=max(16,round(len(groups[0])*.00025));kept=groups
if len(groups)>32:
    kept=[g for g in groups if len(g)>=threshold][:max(8,a.triangles//16)]
indices=np.sort(np.concatenate(kept));removed=len(mesh.faces)-len(indices)
prepared=source
if removed:
    result=mesh.submesh([indices],append=True,repair=False);result.export(out);prepared=out
    area_removed=1-float(result.area)/max(float(mesh.area),1e-12)
else:area_removed=0
data={'method':'Discard tiny disconnected inference components before decimation; retained vertices unchanged',
 'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'prepared_source':str(prepared),
 'original_components':len(groups),'kept_components':len(kept),'minimum_component_faces':threshold,
 'original_faces':len(mesh.faces),'kept_faces':len(indices),'removed_faces':removed,'removed_surface_area_fraction':area_removed,
 'new_vertices_created':0,'smoothing':False,'manual_repair':False,'triangle_budget':a.triangles}
report.write_text(json.dumps(data,indent=2));print(json.dumps(data),flush=True)
