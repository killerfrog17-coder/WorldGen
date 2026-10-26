"""Visibility-aware projection of one calibrated generated paint guide."""
import json
import numpy as np
from PIL import Image
from scipy.interpolate import RBFInterpolator
from scipy.ndimage import map_coordinates,binary_propagation,distance_transform_edt,gaussian_filter

def neutral_exterior_coverage(paint):
    """Fit the actual controlled grey backdrop; do not assume one grey value."""
    H,W=paint.shape[:2];yy,xx=np.mgrid[0:H,0:W];x=xx/max(W-1,1);y=yy/max(H-1,1);basis=np.stack([np.ones_like(x),x,y,x*y,x*x,y*y],axis=2)
    border=(xx<8)|(xx>=W-8)|(yy<8)|(yy>=H-8);ids=np.where(border.ravel())[0][::8];rgb=paint.reshape(-1,3);median=np.median(rgb[ids],axis=0);use=np.linalg.norm(rgb[ids]-median,axis=1)<.10;A=basis.reshape(-1,6)
    if use.sum()<30:return np.ones((H,W))
    for _ in range(4):
        fit=np.linalg.lstsq(A[ids[use]],rgb[ids[use]],rcond=None)[0];error=np.linalg.norm(A[ids]@fit-rgb[ids],axis=1);use=error<.035
        if use.sum()<30:return np.ones((H,W))
    predicted=(A@fit).reshape(H,W,3);error=np.linalg.norm(paint-predicted,axis=2);candidate=(error<.045)&(np.ptp(paint,axis=2)<.17);seed=border&candidate;exterior=binary_propagation(seed,mask=candidate)
    return np.clip(distance_transform_edt(~exterior)/4.,0,1)

def sample_view(root,config,vertices,faces,points,normals,control_sigma=0.,ignore_occlusion=False):
    guide=json.loads((root/config.get('camera_directory','eye-audit')/f"{config['camera']}.json").read_text());M=np.array(guide['camera_matrix']);R=M[:3,:3];position=M[:3,3];scale=guide['scale'];W,H=guide['size']
    path=root/config['image'];rgba=np.asarray(Image.open(path).convert('RGBA'),np.float32)/255;paint=rgba[:,:,:3];coverage=rgba[:,:,3]
    if np.min(coverage)>.99 and config.get('reject_neutral_exterior',False):
        # Registered AI paint guides can shift an outer silhouette despite
        # accurate eye/nose registration. Never project their neutral exterior
        # onto the model: it created pale angular withers patches on variants.
        coverage=neutral_exterior_coverage(paint)
    if config.get('flip_x'):paint=paint[:,::-1].copy();coverage=coverage[:,::-1].copy()
    def project(p):
        camera=(p-position)@R
        return np.stack([(.5+camera[:,0]/scale)*W,(.5-camera[:,1]/scale)*H,-camera[:,2]],axis=1)
    screen=project(points);vscreen=project(vertices);depth=np.full((H,W),np.inf,np.float32)
    for face in faces:
        tri=vscreen[face];lo=np.maximum(np.floor(tri[:,:2].min(0)).astype(int),0);hi=np.minimum(np.ceil(tri[:,:2].max(0)).astype(int),[W-1,H-1])
        if np.any(lo>hi):continue
        yy,xx=np.mgrid[lo[1]:hi[1]+1,lo[0]:hi[0]+1];A,B,C=tri;den=(B[1]-C[1])*(A[0]-C[0])+(C[0]-B[0])*(A[1]-C[1])
        if abs(den)<1e-9:continue
        w0=((B[1]-C[1])*(xx-C[0])+(C[0]-B[0])*(yy-C[1]))/den;w1=((C[1]-A[1])*(xx-C[0])+(A[0]-C[0])*(yy-C[1]))/den;w2=1-w0-w1
        inside=(w0>=0)&(w1>=0)&(w2>=0);z=w0*A[2]+w1*B[2]+w2*C[2];depth[yy[inside],xx[inside]]=np.minimum(depth[yy[inside],xx[inside]],z[inside])
    target=np.array(config['target_pixels'],float)/[W,H];source=np.array(config['guide_pixels'],float)/[paint.shape[1],paint.shape[0]]
    warp=RBFInterpolator(target,source,kernel='thin_plate_spline',smoothing=0);xy=warp(screen[:,:2]/[W,H])*[paint.shape[1]-1,paint.shape[0]-1]
    sampled=np.stack([map_coordinates(paint[:,:,c],[xy[:,1],xy[:,0]],order=1,mode='nearest') for c in range(3)],axis=1)
    paint_coverage=map_coordinates(coverage,[xy[:,1],xy[:,0]],order=1,mode='constant',cval=0.)
    at=np.clip(np.rint(screen[:,:2]).astype(int),[0,0],[W-1,H-1]);visible=np.abs(screen[:,2]-depth[at[:,1],at[:,0]])<.022
    alignment=normals@M[:3,2]
    if config.get('two_sided'):alignment=np.abs(alignment)
    t=np.clip((alignment-.15)/.65,0,1);weight=t*t*(3-2*t)*visible
    if ignore_occlusion:weight=t*t*(3-2*t)
    weight*=((screen[:,0]>0)&(screen[:,0]<W-1)&(screen[:,1]>0)&(screen[:,1]<H-1))
    border=config.get('fade_border',.22);edge=np.minimum.reduce([screen[:,0]/W,1-screen[:,0]/W,screen[:,1]/H,1-screen[:,1]/H]);fade=np.clip(edge/max(border,1e-6),0,1);weight*=fade*fade*(3-2*fade)*config.get('weight',1.)*paint_coverage
    if control_sigma:
        control=gaussian_filter(paint,(control_sigma,control_sigma,0),mode='nearest')
        control_sample=np.stack([map_coordinates(control[:,:,c],[xy[:,1],xy[:,0]],order=1,mode='nearest') for c in range(3)],axis=1)
    else:control_sample=sampled
    return sampled,weight,control_sample
