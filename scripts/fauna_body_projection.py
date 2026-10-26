"""Registered full-body diffuse guide projection, using existing mesh UVs."""
import json,numpy as np
from scipy.spatial import cKDTree
from fauna_face_projection import sample_view

def prepare(root,vertices,faces,p,n):
    path=root/'body-paint-views.json'
    if not path.exists():return None
    total=np.zeros(len(p));combined=np.zeros_like(p);low=np.zeros_like(p)
    for config in json.loads(path.read_text()):
        colour,weight,control=sample_view(root,config,vertices,faces,p,n,control_sigma=12.);combined+=colour*weight[:,None];low+=control*weight[:,None];total+=weight
    t=np.clip(total/.10,0,1);alpha=t*t*(3-2*t);colour=combined/np.maximum(total[:,None],1e-8);control=low/np.maximum(total[:,None],1e-8)
    # Side cameras cannot directly observe the narrow dorsal/ventral seam.
    # Extend nearby observed pigment in actual surface space, bounded to the
    # same small local neighborhood, rather than exposing the old flat coat.
    good=np.where(alpha>.92)[0];missing=np.where(alpha<.92)[0]
    if len(good) and len(missing):
        distances,near=cKDTree(p[good]).query(p[missing],k=3);limit=float(np.clip(np.ptp(p[:,2])*.16,.10,.24));use=distances[:,0]<limit
        (root/'body-projection-coverage.json').write_text(json.dumps({'observed_texels':len(good),'low_confidence_texels':len(missing),'surface_extension_limit':limit,'extended_texels':int(use.sum()),'unfilled_texels':int((~use).sum()),'nearest_observed_distance_quantiles':np.quantile(distances[:,0],[.5,.9,.99,1.]).tolist(),'limits':'Nearby surface colour extension, not additional observed views. Pattern seams and all coats require actual visual review.'},indent=2)+'\n')
        ids=missing[use];distances=distances[use];near=good[near[use]];w=1/np.maximum(distances,.002)**3;w/=w.sum(1,keepdims=True)
        fill=(colour[near]*w[:,:,None]).sum(1);fill_low=(control[near]*w[:,:,None]).sum(1);fraction=alpha[ids,None];colour[ids]=colour[ids]*fraction+fill*(1-fraction);control[ids]=control[ids]*fraction+fill_low*(1-fraction);alpha[ids]=1.
    return colour,control,alpha

def apply(image,body,coat,palettes):
    if body is None:return image
    colour,control,alpha=body;colour=colour.copy()
    if coat:
        # Generated albedo is not numerically equal to palette[0]. Dividing
        # each RGB channel by that palette turns brown shadow into cyan on
        # grey coats. Retain luminance detail within the requested pigment.
        base=np.array(palettes[coat][1]);pale=np.array(palettes[coat][2]);lum=colour@np.array([.2126,.7152,.0722]);low=control@np.array([.2126,.7152,.0722]);chroma=np.ptp(control,axis=1)/np.maximum(control.mean(1),.035)
        use=(low>.18)&(low<.75)&(chroma>.12);reference=float(np.median(low[use])) if use.any() else .35
        detail=np.clip(lum/max(reference,.10)-1.,-.82,1.20);tinted=base[None,:]+detail[:,None]*np.minimum(base*.68,(1-base)*.68)[None,:]
        white=(1-np.clip((chroma-.12)/.20,0,1))*np.clip((low-.50)/.20,0,1);pale_detail=np.clip(lum/.65-1.,-.65,.55);pale_colour=pale[None,:]+pale_detail[:,None]*np.minimum(pale*.50,(1-pale)*.50)[None,:]
        tinted=tinted*(1-white[:,None])+pale_colour*white[:,None]
        keep=1-np.clip((low-.06)/.10,0,1);colour=colour*keep[:,None]+tinted*(1-keep[:,None])
    return image*(1-alpha[:,None])+colour*alpha[:,None]
