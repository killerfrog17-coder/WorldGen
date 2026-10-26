"""Visibility-aware projection of one calibrated generated paint guide."""
import json
import numpy as np
from PIL import Image
from scipy.interpolate import RBFInterpolator
from scipy.ndimage import map_coordinates

def sample_view(root,config,vertices,faces,points,normals):
    guide=json.loads((root/config.get('camera_directory','eye-audit')/f"{config['camera']}.json").read_text());M=np.array(guide['camera_matrix']);R=M[:3,:3];position=M[:3,3];scale=guide['scale'];W,H=guide['size']
    path=root/config['image'];paint=np.asarray(Image.open(path).convert('RGB'),np.float32)/255
    if config.get('flip_x'):paint=paint[:,::-1].copy()
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
    at=np.clip(np.rint(screen[:,:2]).astype(int),[0,0],[W-1,H-1]);visible=np.abs(screen[:,2]-depth[at[:,1],at[:,0]])<.022
    alignment=normals@M[:3,2]
    if config.get('two_sided'):alignment=np.abs(alignment)
    t=np.clip((alignment-.15)/.65,0,1);weight=t*t*(3-2*t)*visible
    weight*=((screen[:,0]>0)&(screen[:,0]<W-1)&(screen[:,1]>0)&(screen[:,1]<H-1))
    border=config.get('fade_border',.08);edge=np.minimum.reduce([screen[:,0]/W,1-screen[:,0]/W,screen[:,1]/H,1-screen[:,1]/H]);fade=np.clip(edge/max(border,1e-6),0,1);weight*=fade*fade*(3-2*fade)*config.get('weight',1.)
    return sampled,weight
