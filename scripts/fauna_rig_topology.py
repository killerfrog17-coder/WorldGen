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
        parents=data['parents'].astype(int);children=np.bincount(parents[parents>=0],minlength=len(parents));leaves=int(np.sum(children==0))
    return {'joints':len(parents),'leaves':leaves,'required_leaves':required,'pass':leaves>=required}

def quarantine(path,root):
    path=Path(path).resolve();root=Path(root).resolve()
    assert path.is_relative_to(root) and path.name=='generated-rig.npz'
    n=1
    while path.with_name(f'generated-rig.rejected-topology-{n}.npz').exists():n+=1
    target=path.with_name(f'generated-rig.rejected-topology-{n}.npz');path.rename(target)
    meta=path.with_suffix('.json')
    if meta.exists():meta.rename(target.with_suffix('.json'))
    return str(target)
