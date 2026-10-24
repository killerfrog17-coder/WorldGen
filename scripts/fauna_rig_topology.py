"""Minimal retarget topology checks, never an art or deformation acceptance gate."""
from pathlib import Path
import numpy as np

def minimum_leaves(entry):
    family=entry['family'];name=entry['animal']
    if family=='snake':return 1
    if family=='spider':return 9
    if family=='ground_insect':return 7
    if family in ('bird','bat'):return 3 if name in {'Ostrich','Emu','Cassowary','Kiwi','Penguin'} else 5
    if family in ('fish','marine'):return 4
    return 5

def topology(path,required):
    with np.load(path) as data:
        parents=data['parents'].astype(int);children=np.bincount(parents[parents>=0],minlength=len(parents));leaf_ids=np.flatnonzero(children==0);leaves=len(leaf_ids)
        x=data['source_vertices'][:,0];mid=(x.min()+x.max())*.5;epsilon=max(1e-5,float(np.ptp(x))*.005)
        left=int(np.sum(data['joints'][leaf_ids,0]<mid-epsilon));right=int(np.sum(data['joints'][leaf_ids,0]>mid+epsilon))
    side_required=4 if required>=9 else 3 if required>=7 else 2 if required>=5 else 1 if required>=3 else 0
    return {'joints':len(parents),'leaves':leaves,'required_leaves':required,'left_endpoints':left,'right_endpoints':right,'required_per_side':side_required,'pass':leaves>=required and min(left,right)>=side_required}

def quarantine(path,root):
    path=Path(path).resolve();root=Path(root).resolve()
    assert path.is_relative_to(root) and path.name=='generated-rig.npz'
    n=1
    while path.with_name(f'generated-rig.rejected-topology-{n}.npz').exists():n+=1
    target=path.with_name(f'generated-rig.rejected-topology-{n}.npz');path.rename(target)
    meta=path.with_suffix('.json')
    if meta.exists():meta.rename(target.with_suffix('.json'))
    return str(target)
