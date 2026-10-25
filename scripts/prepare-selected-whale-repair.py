"""Correct selected whale head/tail correspondence and confine tail skin."""
import hashlib,json,shutil
from pathlib import Path
import numpy as np
repo=Path(__file__).resolve().parent.parent;slug='whale';source=repo/'artifacts/fauna-overnight-20261024'/slug
choice=json.loads((repo/'docs/fauna/OWNER_SELECTIONS.json').read_text())['animals'][slug];number,=choice['selected_models'];assert number==4
root=repo/'artifacts/fauna-owner-fixes-20261025'/slug;folder=root/'candidate-1';folder.mkdir(parents=True,exist_ok=True)
assert not (folder/'generated-rig.npz').exists()
raw=np.load(source/f'candidate-{number}'/'generated-rig.npz');data={key:raw[key].copy() for key in raw.files}
v=data['source_vertices'];points=data['sample_points'];j=data['joints'];w=data['sample_skin'];tail=list(range(11,26))
starts=j[tail[:-1]];delta=np.diff(j[tail],axis=0);t=np.clip(np.sum((points[:,None]-starts)*delta,axis=2)/np.maximum(np.sum(delta**2,axis=1),1e-9),0,1);dist=np.linalg.norm(points[:,None]-(starts+t[:,:,None]*delta),axis=2);near=dist.argmin(1)
count=0
for i in np.flatnonzero(points[:,1]>-.1):
    k=int(near[i]);u=t[i,k];row=np.zeros(w.shape[1]);row[tail[k]]=1-u;row[tail[k+1]]=u
    if points[i,1]>2.30:row[:]=0;row[25]=1
    blend=float(np.clip((points[i,1]+.1)/.3,0,1));blend=blend*blend*(3-2*blend);new=w[i]*(1-blend)+row*blend
    keep=np.argsort(new)[-4:];new[[k for k in range(len(new)) if k not in keep]]=0;w[i]=new/new.sum();count+=1
np.savez_compressed(folder/'generated-rig.npz',**data)
for name in ('model.glb','mesh-preparation.json'):shutil.copy2(source/f'candidate-{number}'/name,folder/name)
meta=json.loads((source/f'candidate-{number}'/'generated-rig.json').read_text());meta.update(method='Neural rest skeleton with owner-requested explicit head/tail mapping and tail skin repair',manual_rig=False,tail_samples_reweighted=count,geometry_unchanged=True,rest_joints_unchanged=True)
(folder/'generated-rig.json').write_text(json.dumps(meta,indent=2)+'\n')
policy={'head_node':4,'tail_node':25,'reason':'Native top view shows snout at -Y and flukes at +Y; previous +Y head heuristic assigned the fluke to Head. Explicit true head/tail paths and adjacent-tail weights.','source_flags':{'reference_pose':'source_bind'}}
(root/'owner-manual-motion-policy.json').write_text(json.dumps(policy,indent=2)+'\n');print('Whale4 explicit head/tail repair prepared',count,'tail skin samples')
