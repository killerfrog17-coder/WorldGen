"""Manual longitudinal snake chains with adjacent-only skin, no neural rig.

Uses the ordered-chain/curve principle in Blender's Spline IK manual. The
review clips are baked FK transfers; this script does not claim live Spline IK.
"""
import argparse,hashlib,json
from pathlib import Path
import numpy as np,trimesh
repo=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);a=p.parse_args();root=a.root.resolve()
assert root.parent==repo/'artifacts/fauna-overnight-20261024' and root.name in ('python','venomous-snake')
assert (root/'manual-straight-snake-request.json').exists()
for number in range(1,5):
    folder=root/f'candidate-{number}';out=folder/'generated-rig.npz'
    if out.exists():
        assert json.loads(out.with_suffix('.json').read_text()).get('manual_rig');continue
    mesh=trimesh.load(folder/'model.glb',force='mesh',process=False);v=mesh.vertices[:,[0,2,1]].copy();v[:,1]*=-1
    v*=1.65/np.ptp(v,axis=0).max();lo,hi=v[:,1].min(),v[:,1].max();length=hi-lo
    end_width=[]
    for end in [lo,hi]:
        region=v[np.abs(v[:,1]-end)<length*.12];end_width.append(float(np.ptp(region[:,0])+np.ptp(region[:,2])))
    if end_width[0]>end_width[1]:v[:,:2]*=-1;lo,hi=-hi,-lo
    # Explicit head-to-tail ordering. Slice centres follow only small residual
    # sculpt variation; there is no topology guess from toes or nearby coils.
    ys=np.linspace(hi-length*.025,lo+length*.01,40);centres=[]
    for y in ys:
        q=v[np.argsort(abs(v[:,1]-y))[:max(12,len(v)//40)]]
        centres.append([float(np.median(q[:,0])),float(y),float((np.percentile(q[:,2],15)+np.percentile(q[:,2],85))*.5)])
    joints=np.asarray(centres);parents=np.arange(len(joints))-1
    starts=joints[:-1];delta=np.diff(joints,axis=0);t=np.clip(np.sum((v[:,None]-starts)*delta,axis=2)/np.maximum(np.sum(delta**2,axis=1),1e-9),0,1)
    distance=np.linalg.norm(v[:,None]-(starts+t[:,:,None]*delta),axis=2);segment=distance.argmin(1);u=t[np.arange(len(v)),segment]
    skin=np.zeros((len(v),len(joints)),np.float32);skin[np.arange(len(v)),segment]=1-u;skin[np.arange(len(v)),segment+1]=u
    assert np.allclose(skin.sum(1),1) and (skin>0).sum(1).max()<=2
    np.savez_compressed(out,source_vertices=v,source_faces=mesh.faces,joints=joints,parents=parents,sample_points=v,sample_skin=skin)
    meta={'method':'Owner-authorized straight longitudinal snake rig','manual_rig':True,'joints':len(joints),'source_mesh_sha256':hashlib.sha256((folder/'model.glb').read_bytes()).hexdigest(),'body_order':'Head to tail, 40 manually specified longitudinal stations; no neural graph','weights':'Only two adjacent longitudinal bones; no cross-body/cross-coil weights','review_length_m':1.65,'tutorial':'https://docs.blender.org/manual/en/latest/animation/constraints/tracking/spline_ik.html','motion':'Existing CC0 snake clips transferred into baked fixed-length FK; live Spline IK is not active'}
    out.with_suffix('.json').write_text(json.dumps(meta,indent=2)+'\n');print('MANUAL STRAIGHT SNAKE RIG',root.name,number,len(joints),flush=True)
