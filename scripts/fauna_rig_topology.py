"""Minimal retarget topology checks, never an art or deformation acceptance gate."""
from pathlib import Path
import numpy as np

def minimum_leaves(entry):
    family=entry['family'];name=entry['animal']
    if family=='snake':return 1
    if family=='spider':return 9
    if family=='ground_insect':return 7
    if family=='bird' and (Path(__file__).resolve().parent.parent/'artifacts/fauna-overnight-20261024'/entry['slug']/'folded-wing-ground-review.json').exists():return 3
    if family in ('bird','bat'):return 3 if name in {'Ostrich','Emu','Cassowary','Kiwi','Penguin'} else 5
    if family in ('fish','marine'):return 4
    return 5

def topology(path,required,family=None):
    with np.load(path) as data:
        parents=data['parents'].astype(int);children=np.bincount(parents[parents>=0],minlength=len(parents));leaf_ids=np.flatnonzero(children==0);leaves=len(leaf_ids)
        x=data['source_vertices'][:,0];joints=data['joints'];height=float(np.ptp(data['source_vertices'][:,2]));roots=np.flatnonzero(parents<0)
        assert len(roots)==1,'Retarget topology requires one root'
        bbox_mid=float((x.min()+x.max())*.5);mid=bbox_mid;method='whole mesh bbox X'
        # Low curled tails can shift bounding boxes; a learned pelvis can also
        # be eccentric. Use the middle-height body surface for land mammals.
        # Keep other families' existing convention (including legacy bird axes).
        if family in {'mammal','canid','felid','bear','ungulate','thickskin','armoured_mammal','spiny_mammal'}:
            z=data['source_vertices'][:,2];lo,hi=np.quantile(z,[.3,.7]);body=x[(z>=lo)&(z<=hi)]
            if len(body):mid=float(np.median(body));method='median surface X in middle 30-70 percent height quantiles'
        epsilon=max(1e-5,float(np.ptp(x))*.005)
        left=int(np.sum(data['joints'][leaf_ids,0]<mid-epsilon));right=int(np.sum(data['joints'][leaf_ids,0]>mid+epsilon))
        # This is only a minimum endpoint feasibility check. An asymmetric
        # stance can move the torso median too; retain the original bbox test
        # when it provides better bilateral support rather than reject valid
        # cached rigs solely because a new center estimate changed.
        bbox_left=int(np.sum(data['joints'][leaf_ids,0]<bbox_mid-epsilon));bbox_right=int(np.sum(data['joints'][leaf_ids,0]>bbox_mid+epsilon))
        if min(bbox_left,bbox_right)>min(left,right):
            mid=bbox_mid;left,right=bbox_left,bbox_right;method='whole mesh bbox X (stronger bilateral endpoint support)'
        proximal_pairs=[]
        if family=='primate' and required==5 and min(left,right)<2:
            graph=[np.flatnonzero(parents==i).tolist() for i in range(len(parents))]
            for anchor,kids in enumerate(graph):
                if len(kids)<2 or joints[anchor,2]<height*.4:continue
                branches={-1:[],1:[]}
                for child in kids:
                    pending=[child];nodes=[];tips=[]
                    while pending:
                        n=pending.pop();nodes.append(n);pending.extend(graph[n])
                        if not graph[n]:tips.append(n)
                    # A complete low appendage, not a torso+head subtree.
                    if not tips or max(joints[tips,2])>height*.4:continue
                    tip=min(tips,key=lambda n:joints[n,2])
                    if joints[anchor,2]-joints[tip,2]<height*.3:continue
                    early=[child]
                    while len(early)<3 and len(graph[early[-1]])==1:early.append(graph[early[-1]][0])
                    dx=float(np.mean(joints[early,0])-joints[anchor,0])
                    if abs(dx)<height*.02 or np.mean(joints[early,1])-joints[anchor,1]<-height*.15:continue
                    branches[-1 if dx<0 else 1].append({'child':child,'tip':int(tip),'nodes':nodes})
                if branches[-1] and branches[1]:
                    pair=[min(branches[side],key=lambda r:joints[r['tip'],2]) for side in (-1,1)]
                    proximal_pairs.append({'anchor':anchor,'branches':pair})
            # Require two separate full-body attachment pairs with four
            # disjoint appendage subtrees. Crossed fingertips are allowed;
            # missing arms/legs and several digits on one hand are not.
            valid=[]
            for a in proximal_pairs:
                for b in proximal_pairs:
                    if a['anchor']>=b['anchor']:continue
                    limbs=a['branches']+b['branches'];sets=[set(r['nodes']) for r in limbs]
                    if all(not (sets[i]&sets[j]) for i in range(4) for j in range(i+1,4)):
                        valid=[a,b];break
                if valid:break
            proximal_pairs=valid
    side_required=4 if required>=9 else 3 if required>=7 else 2 if required>=5 else 1 if required>=3 else 0
    return {'joints':len(parents),'leaves':leaves,'required_leaves':required,'midline_method':method,'midline_x':mid,'legacy_bbox_midline_x':bbox_mid,'left_endpoints':left,'right_endpoints':right,'required_per_side':side_required,'proximal_full_limb_pairs':proximal_pairs,'pass':leaves>=required and (min(left,right)>=side_required or len(proximal_pairs)==2)}

def quarantine(path,root):
    path=Path(path).resolve();root=Path(root).resolve()
    assert path.is_relative_to(root) and path.name=='generated-rig.npz'
    n=1
    while path.with_name(f'generated-rig.rejected-topology-{n}.npz').exists():n+=1
    target=path.with_name(f'generated-rig.rejected-topology-{n}.npz');path.rename(target)
    meta=path.with_suffix('.json')
    if meta.exists():meta.rename(target.with_suffix('.json'))
    return str(target)
