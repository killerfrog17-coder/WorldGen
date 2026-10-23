"""Alternative offline decode of MoCap neural joint positions, not new motion.

Fits joint swing to predicted child directions, retaining exact bind lengths.
No leaf twist is inferred from an unobservable point. Raw neural rotations are
retained independently. This is a diagnostic alternative, not validated mocap.
"""
import argparse
import json
from pathlib import Path
import numpy as np
from scipy.ndimage import gaussian_filter1d
from scipy.spatial.transform import Rotation


def swing(a,b):
    a=a/np.linalg.norm(a);b=b/np.linalg.norm(b)
    axis=np.cross(a,b);cos=np.clip(a@b,-1,1);s=np.linalg.norm(axis)
    if s<1e-8:
        if cos>0:return np.eye(3)
        axis=np.cross(a,np.eye(3)[np.argmin(np.abs(a))])
        axis/=np.linalg.norm(axis)
        return Rotation.from_rotvec(axis*np.pi).as_matrix()
    return Rotation.from_rotvec(axis/s*np.arctan2(s,cos)).as_matrix()


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
    a=ap.parse_args();root=a.root.resolve();out=root/'mocap-results'
    with np.load(out/'mocap-raw.npz') as n:d={k:n[k].copy() for k in n.files}
    p=gaussian_filter1d(d['predicted_positions'],sigma=1,axis=0,mode='nearest')
    parents=d['parents'];offsets=d['offsets'];count,joints=p.shape[:2]
    children=[[j for j,pa in enumerate(parents) if pa==i] for i in range(joints)]
    mats=np.tile(np.eye(3),(count,joints,1,1));previous=np.tile(np.eye(3),(joints,1,1))
    for f in range(count):
        for j in range(joints):
            child=children[j];parent=int(parents[j])
            inherited=mats[f,parent] if parent>=0 else np.eye(3)
            if len(child)>1:
                x=offsets[child];y=p[f,child]-p[f,j]
                weights=np.linalg.norm(x,axis=1);x=x/np.linalg.norm(x,axis=1,keepdims=True)
                y=y/np.maximum(np.linalg.norm(y,axis=1,keepdims=True),1e-8)
                u,s,vt=np.linalg.svd((x*weights[:,None]).T@y)
                fix=np.ones(3);fix[-1]=np.linalg.det(vt.T@u.T)
                mats[f,j]=vt.T@np.diag(fix)@u.T
            elif child:
                k=child[0]
                # Transport previous orientation with the changing direction,
                # avoiding arbitrary frame-to-frame roll around long limbs.
                base=previous[j] if f else inherited
                mats[f,j]=swing(base@offsets[k],p[f,k]-p[f,j])@base
            else:mats[f,j]=inherited
        previous=mats[f].copy()
    local=mats.copy()
    for j,pa in enumerate(parents):
        if pa>=0:local[:,j]=np.swapaxes(mats[:,pa],-1,-2)@mats[:,j]
    q=Rotation.from_matrix(local.reshape(-1,3,3)).as_quat(scalar_first=True).reshape(count,joints,4)
    lp=np.tile(offsets,(count,1,1));pos=lp.copy()
    for j,pa in enumerate(parents):
        if pa>=0:pos[:,j]=pos[:,pa]+np.einsum('fab,b->fa',mats[:,pa],offsets[j])
    np.savez_compressed(out/'mocap-pose-raw.npz',rotations=q,positions=pos,local_positions=lp,
        offsets=offsets,parents=parents,names=d['names'],rest_positions=d['rest_positions'],fps=30)
    (out/'pose-reconstruction.json').write_text(json.dumps({'method':'neural positions, constrained bind-length swing',
        'frames':count,'position_temporal_sigma_frames':1,'root_translation':'none; in-place evaluation',
        'leaf_twist':'inherits parent; unobservable from a joint point','new_inference':False},indent=2))


if __name__=='__main__':main()
