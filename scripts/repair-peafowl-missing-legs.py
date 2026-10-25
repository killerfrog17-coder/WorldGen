"""Add two manually placed leg chains where peafowl1's neural rig missed both."""
import hashlib,json,shutil
from pathlib import Path
import numpy as np
repo=Path(__file__).resolve().parent.parent;folder=repo/'artifacts/fauna-overnight-20261024/peafowl/candidate-1'
original=folder/'generated-rig.before-manual-legs.npz';meta_backup=original.with_suffix('.json')
if not original.exists():shutil.copy2(folder/'generated-rig.npz',original);shutil.copy2(folder/'generated-rig.json',meta_backup)
d=np.load(original);data={k:d[k].copy() for k in d.files};v=data['source_vertices'];upper=v[v[:,2]>.78*1.65];torso=v[(v[:,2]>.25*1.65)&(v[:,2]<.65*1.65)];sign=1 if upper[:,0].mean()>=torso[:,0].mean() else -1
C=np.array([[0,-sign,0],[sign,0,0],[0,0,1]]);canonical=v@C.T;heads=data['joints']@C.T;parents=data['parents'].astype(int).tolist();new=[];chains=[]
for side in (-1,1):
    # Exclude the trailing train. Pick the actual foot surface below the body.
    foot_points=canonical[(canonical[:,0]*side>.035)&(canonical[:,1]>.2)&(canonical[:,1]<1.1)&(canonical[:,2]<.15)]
    assert len(foot_points)>6,'Manual foot landmark region lacks geometry'
    foot=np.median(foot_points,axis=0);x=float(foot[0]);y=float(foot[1]);z=float(foot_points[:,2].min())+.045
    chain=[6];pose=[[x*.8,y-.07,.64],[x*.9,y-.15,.40],[x,y-.05,.14],[x,y,z],[x,y+.09,z-.015]]
    for point in pose:
        index=len(heads)+len(new);parents.append(chain[-1]);chain.append(index);new.append(point)
    chains.append(chain)
data['joints']=np.vstack([heads,np.array(new)])@C;data['parents']=np.array(parents);data['sample_skin']=np.pad(data['sample_skin'],((0,0),(0,len(new))))
np.savez_compressed(folder/'generated-rig.npz',**data)
meta=json.loads(meta_backup.read_text());meta.update(method='Neural rig with owner-requested manual leg additions',joints=len(parents),manual_leg_chains=chains,original_neural_sha256=hashlib.sha256(original.read_bytes()).hexdigest(),source_mesh_sha256=hashlib.sha256((folder/'model.glb').read_bytes()).hexdigest(),reason='The network classified folded wings but omitted both actual legs; own body/skin retained, real lower-leg geometry gets local manual skin during assembly.')
(folder/'generated-rig.json').write_text(json.dumps(meta,indent=2));print('Manual peafowl1 legs',chains)
