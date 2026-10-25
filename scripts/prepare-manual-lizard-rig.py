"""Owner-requested four-leg anatomical rigs on the selected unchanged meshes.

Coordinates were chosen against front/side/top body views. Four explicit limb
chains replace digit-as-leg guesses; each toe fan follows one rigid palm bone.
"""
import argparse,hashlib,json,shutil
from pathlib import Path
import numpy as np
repo=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser();p.add_argument('--slug',choices=['iguana','monitor-lizard'],required=True);a=p.parse_args()
source=repo/'artifacts/fauna-overnight-20261024'/a.slug
choice=json.loads((repo/'docs/fauna/OWNER_SELECTIONS.json').read_text())['animals'][a.slug];number,=choice['selected_models']
root=repo/'artifacts/fauna-owner-fixes-20261025'/a.slug;folder=root/'candidate-1';folder.mkdir(exist_ok=True,parents=True)
assert not (folder/'generated-rig.npz').exists(),'Inspect an existing manual rig before replacement'
raw=np.load(root/'repair-baseline.npz');v=raw['vertices'];faces=raw['faces'];old=raw['joints']
if a.slug=='iguana':
    limbs=[np.array([old[7],old[9],old[10],[-.17,1.35,.13]]),np.array([old[23],old[25],old[26],[.93,1.03,.13]]),np.array([old[39],old[40],old[41],old[42]]),np.array([old[54],old[55],old[56],old[57]])]
    tail=old[70:86]
else:
    limbs=[np.array([old[8],old[10],old[11],[-.48,1.72,.13]]),np.array([old[16],old[18],old[19],[.85,1.50,.13]]),np.array([old[24],old[25],old[26],[-.86,-.15,.10]]),np.array([[.16,-.35,.83],[.59,-.13,.60],[.64,-.47,.21],[.76,-.35,.10]])]
    tail=old[32:40]
joints=np.vstack([old[:7],*limbs,tail]);parents=[-1,0,1,2,3,4,5]
for attach,start in [(3,7),(3,11),(0,15),(0,19)]:parents.extend([attach,start,start+1,start+2])
parents.extend([0,*range(23,23+len(tail)-1)]);parents=np.asarray(parents);assert len(parents)==len(joints)
core=[1,2,3,4,5,6,*range(23,len(joints))]
starts=joints[parents[core]];delta=joints[core]-starts
t=np.clip(np.sum((v[:,None]-starts)*delta,axis=2)/np.maximum(np.sum(delta**2,axis=1),1e-8),0,1)
dist=np.linalg.norm(v[:,None]-(starts+t[:,:,None]*delta),axis=2);near=np.argmin(dist,axis=1);body_distance=dist[np.arange(len(v)),near]
skin=np.zeros((len(v),len(joints)))
for i,k in enumerate(near):skin[i,parents[core[k]]]=1-t[i,k];skin[i,core[k]]+=t[i,k]
limb_dist=[];limb_t=[]
for points in limbs:
    start=points[:-1];vector=np.diff(points,axis=0);u=np.clip(np.sum((v[:,None]-start)*vector,axis=2)/np.sum(vector**2,axis=1),0,1)
    dd=np.linalg.norm(v[:,None]-(start+u[:,:,None]*vector),axis=2);limb_dist.append(dd);limb_t.append(u)
closest=np.argmin(np.array(limb_dist).min(2),axis=0);rigid_counts=[]
for k,start in enumerate([7,11,15,19]):
    dd=limb_dist[k];seg=np.argmin(dd,axis=1);distance=dd[np.arange(len(v)),seg];rigid=0
    for i in np.flatnonzero(closest==k):
        blend=float(np.clip((body_distance[i]-distance[i]-.025)/.19,0,1));blend=blend*blend*(3-2*blend)
        limb=np.zeros(len(joints));n=int(seg[i]);u=limb_t[k][i,n];limb[start+n]=1-u;limb[start+n+1]=u
        skin[i]*=1-blend;skin[i]+=limb*blend
        palm=limbs[k][-1];ankle=limbs[k][-2]
        if np.linalg.norm(v[i,:2]-palm[:2])<.31 and v[i,2]<ankle[2]+.055 and distance[i]<.29:
            skin[i]=0;skin[i,start+3]=1;rigid+=1
    assert rigid>8,(a.slug,k,rigid);rigid_counts.append(rigid)
assert np.allclose(skin.sum(1),1) and (skin>1e-8).sum(1).max()<=4
np.savez_compressed(folder/'generated-rig.npz',source_vertices=v,source_faces=faces,joints=joints,parents=parents,sample_points=v,sample_skin=skin)
mesh=source/f'candidate-{number}'/'model.glb';shutil.copy2(mesh,folder/'model.glb');shutil.copy2(source/f'candidate-{number}'/'mesh-preparation.json',folder/'mesh-preparation.json')
meta={'method':'Owner-authorized four-leg anatomical rig','manual_rig':True,'original_candidate':number,'source_mesh_sha256':hashlib.sha256(mesh.read_bytes()).hexdigest(),'joints':len(joints),'placement':'Explicit shoulder/elbow/ankle/palm chains, manually replaced missing monitor hind-right leg; existing sculpt centreline retained for core/tail','rigid_toe_fan_vertices':rigid_counts,'weights':'Geometric own-limb confinement, smooth body attachment, at most four normalized influences; each toe fan one rigid palm bone','model_geometry_unchanged':True}
(folder/'generated-rig.json').write_text(json.dumps(meta,indent=2)+'\n')
policy={'anatomical_quadruped':True,'chains':{'1':{'fore':[[3,7,8,9,10],[3,11,12,13,14]],'hind':[[0,15,16,17,18],[0,19,20,21,22]]}},'source_flags':{'hold_core_bind':True,'reference_pose':'source_bind','minimal_twist_limb_swing':True},'reason':'Owner manual all-four-leg rig request; no digit treated as another leg.'}
(root/'owner-manual-motion-policy.json').write_text(json.dumps(policy,indent=2)+'\n');print('MANUAL ANATOMICAL RIG',a.slug,len(joints),'bones',rigid_counts,flush=True)
