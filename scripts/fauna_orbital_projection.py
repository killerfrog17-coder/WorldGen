"""CPU projection for new orbital skin; Blender does not need image libraries."""
import json,numpy as np
from fauna_face_projection import sample_view

def bake(root,views):
    path=root/'orbital-projection-input.npz'
    if not path.exists():return
    inputs=np.load(path);d=np.load(root/'projection-anatomy.npz');query=inputs['points'];normals=inputs['normals'];combined=np.zeros_like(query);total=np.zeros(len(query))
    for config in views:
        colour,weight,_=sample_view(root,config,d['vertices'],d['faces'],query,normals,ignore_occlusion=True);combined+=colour*weight[:,None];total+=weight
    srgb=combined/np.maximum(total[:,None],1e-8)
    # The fitted physical globe/lid already supplies the eye. Do not retain a
    # second painted iris or a broad photographic socket shadow on its fur.
    # Translate a nearby fur field rather than collapsing the entire inner
    # patch onto one sampled radius. Translation retains spatial fibre scale.
    ys,xs=inputs['ys'],inputs['xs'];land=json.loads((root/'face-landmarks.json').read_text());rho=np.full(len(query),10.);fur_query=query.copy()
    for index,side in enumerate(['right','left']):
        mask=(xs//128)==index;eye=land[side+'_eye'];delta=query[mask]-eye['point'];H,V=[np.array(eye['fit_frame'][k]) for k in ['horizontal','vertical']];R=eye['radius'][0];v=delta@V;rho[mask]=np.sqrt((delta@H/R)**2+(v/(R*.60))**2);fur_query[mask]+=V[None,:]*np.where(v>=0,R*1.55,-R*1.55)[:,None]
    combined_fur=np.zeros_like(query);fur_total=np.zeros(len(query))
    for config in views:
        colour,weight,_=sample_view(root,config,d['vertices'],d['faces'],fur_query,normals,ignore_occlusion=True);combined_fur+=colour*weight[:,None];fur_total+=weight
    fur=combined_fur/np.maximum(fur_total[:,None],1e-8);t=np.clip((1.8-rho)/.55,0,1);weight=t*t*(3-2*t);srgb=srgb*(1-weight[:,None])+fur*weight[:,None]
    # BYTE image buffers use the same encoded RGB convention as loaded coat
    # images. Converting this one field to linear again made a dark eye collar.
    srgb[total<.01]=np.nan
    np.savez_compressed(root/'orbital-projection-colour.npz',colour=srgb,signature=inputs['signature'])
