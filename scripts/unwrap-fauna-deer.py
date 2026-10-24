"""UV atlas only, before rigging; carries anatomical region tags through splits."""
import argparse
from pathlib import Path
import numpy as np
import xatlas
ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True);a=ap.parse_args();root=a.root.resolve()
d=np.load(root/'body-tagged.npz');atlas=xatlas.Atlas();atlas.add_mesh(d['vertices'],d['faces'].astype(np.uint32))
pack=xatlas.PackOptions();pack.resolution=2048;pack.padding=12;atlas.generate(pack_options=pack)
vmap,faces,uv=atlas[0]
vv=d['vertices'];ff=d['faces'];normal=np.zeros_like(vv)
fn=np.cross(vv[ff[:,1]]-vv[ff[:,0]],vv[ff[:,2]]-vv[ff[:,0]])
for i in range(3):np.add.at(normal,ff[:,i],fn)
normal/=np.maximum(np.linalg.norm(normal,axis=1,keepdims=True),1e-8)
np.savez_compressed(root/'anatomy.npz',vertices=d['vertices'][vmap],faces=faces,uv=uv[faces],
    vertex_uv=uv,normals=normal[vmap],region_weights=d['region_weights'][vmap])
print('Atlas prepared',len(faces),'triangles.',flush=True)
