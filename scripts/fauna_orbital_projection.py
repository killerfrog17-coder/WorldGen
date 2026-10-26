"""CPU projection for new orbital skin; Blender does not need image libraries."""
import numpy as np
from fauna_face_projection import sample_view

def bake(root,views):
    path=root/'orbital-projection-input.npz'
    if not path.exists():return
    inputs=np.load(path);d=np.load(root/'projection-anatomy.npz');query=inputs['points'];normals=inputs['normals'];combined=np.zeros_like(query);total=np.zeros(len(query))
    for config in views:
        colour,weight,_=sample_view(root,config,d['vertices'],d['faces'],query,normals,ignore_occlusion=True);combined+=colour*weight[:,None];total+=weight
    srgb=combined/np.maximum(total[:,None],1e-8);linear=np.where(srgb<=.04045,srgb/12.92,((srgb+.055)/1.055)**2.4);linear[total<.01]=np.nan
    np.savez_compressed(root/'orbital-projection-colour.npz',colour=linear,signature=inputs['signature'])
