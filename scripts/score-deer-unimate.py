"""Rank learned deer takes using action-aware kinematic diagnostics.

REVIEW is not visual acceptance. The thresholds are deer prototype heuristics,
not a universal classifier. Source features and rejected takes are retained.
"""
import argparse
import json
from pathlib import Path
import sys
import numpy as np


def evaluate(positions,rotations,rest,names,action,fps=30):
    failures=[]
    if not np.isfinite(positions).all() or not np.isfinite(rotations).all():
        return {'reasons':['nonfinite'],'score':1e12,'status':'REJECT'}
    feet=[i for i,n in enumerate(names) if 'Hoof' in n]
    head=names.index('Head')
    length=max(.1,float(rest[0,1]-min(rest[feet,1])))
    p=positions/length
    root=p[:,0]
    local=p-root[:,None]
    height=(positions[:,feet,1]-rest[feet,1][None])/length
    drift=np.linalg.norm(np.diff(p[:,feet][:,:,[0,2]],axis=0),axis=-1)
    near=height[1:]<.05
    skate=float(np.mean(drift[near]>.025)) if near.any() else 1
    slide=float(np.mean(drift[near])) if near.any() else 0
    penetration=float(np.maximum(0,-height).mean())
    contact_fraction=float(np.mean(np.min(np.abs(height),axis=1)<.10))
    q=rotations/np.maximum(np.linalg.norm(rotations,axis=-1,keepdims=True),1e-8)
    angular=2*np.arccos(np.clip(np.abs(np.sum(q[1:]*q[:-1],axis=-1)),0,1))
    jumps=float(np.degrees(angular).max())
    bind_angles=np.degrees(2*np.arccos(np.clip(np.abs(q[...,0]),0,1)))
    torso=[i for i,n in enumerate(names) if n in ('Spine1','Spine2','Chest')]
    torso_bend=float(bind_angles[:,torso].max())
    if torso_bend>70:
        failures.append('torso joint folds excessively away from standing bind')
    # Magnitude of acceleration is an imperfect ranking cue, not a demand
    # that running feet have the same acceleration as an idle head.
    acceleration=np.linalg.norm(np.diff(local,n=2,axis=0),axis=-1)*fps*fps
    jitter=float(np.percentile(acceleration,95))
    activity=float(np.mean(np.std(local[:,feet],axis=0)))
    travel=float(np.linalg.norm(root[-1,[0,2]]-root[0,[0,2]]))
    head_low=float(np.min(local[:,head,1]))
    extents=np.ptp(p,axis=1)
    size_ratio=float(np.max(extents)/max(.1,np.ptp(rest/length,axis=0).max()))
    if jumps>100:
        failures.append('large per-frame joint rotation')
    if size_ratio>3:
        failures.append('explosive displacement')
    if penetration>.20:
        failures.append('persistent severe foot penetration')
    if action in ('walk','run'):
        if activity<.012:
            failures.append('locomotion has almost no relative foot movement')
        if contact_fraction<.20:
            failures.append('locomotion mostly airborne/hovering')
        # These prompts explicitly request FORWARD travel. A quiet standing
        # take must not win just because it has fewer acceleration artifacts.
        minimum_travel=.50 if action=='walk' else 1.0
        if travel<minimum_travel:
            failures.append('forward locomotion prompt has insufficient forward travel')
    if action=='feed' and head_low>-.20:
        failures.append('feeding head never lowers sufficiently below pelvis')
    loop=None
    if action in ('walk','run'):
        best=1e9
        for a in range(len(p)-16):
            for b in range(a+15,len(p)-1):
                pose=np.sqrt(np.mean((local[a]-local[b])**2))
                velocity=np.sqrt(np.mean(((local[a+1]-local[a])-(local[b+1]-local[b]))**2))*3
                cost=float(pose+velocity)
                if cost<best:
                    best=cost
                    loop={'first':a,'last':b,'error':cost}
    score=skate*2+penetration*8+jitter*.025+max(0,jumps-25)*.01
    if action in ('walk','run'):
        score+=(1-contact_fraction)*2
        if loop:
            score+=loop['error']*3
    if action=='feed':
        score+=max(0,head_low+.35)*3
    return dict(status='REJECT' if failures else 'REVIEW',reasons=failures,score=score,
        skating_ratio=skate,mean_slide_per_frame=slide,mean_penetration=penetration,
        ground_contact_fraction=contact_fraction,rotation_step_max_deg=jumps,
        torso_bend_max_deg=torso_bend,
        normalized_acceleration_p95=jitter,foot_activity=activity,root_travel=travel,
        head_min_relative_height=head_low,loop_candidate=loop,scale_root_height=length)


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--root',type=Path,required=True)
    ap.add_argument('--repo',type=Path,required=True)
    ap.add_argument('--samples',type=Path,required=True)
    args=ap.parse_args()
    root,repo,samples=[p.resolve() for p in (args.root,args.repo,args.samples)]
    sys.path.insert(0,str(repo))
    from unimate.utils.motion_utils import recover_unimate_anim_from_rot
    from Animation import positions_global
    cond=np.load(root/'unimate-rig/cond.npy',allow_pickle=True).item()['WorldGenDeer']
    rest=cond['tpos_first_frame']
    records=[]
    decoded=samples/'decoded'
    decoded.mkdir(exist_ok=True)
    for path in sorted((samples/'motions').glob('*.npy')):
        features=np.load(path)
        action=path.name.split('-')[1]
        anim=recover_unimate_anim_from_rot(features,cond['parents'],cond['tpos_offsets'])
        positions=positions_global(anim)
        metrics=evaluate(positions,anim.rotations.qs,rest,list(cond['joint_names']),action)
        record={'file':path.name,'action':action,**metrics}
        records.append(record)
        np.savez_compressed(decoded/(path.stem+'.npz'),positions=positions,rotations=anim.rotations.qs,
            local_positions=anim.positions,offsets=anim.offsets,parents=anim.parents,
            names=cond['joint_names'])
    records.sort(key=lambda r:(r['action'],r['status']=='REJECT',r['score']))
    report={'disclaimer':'Heuristic candidate ranking, NOT naturalness acceptance',
        'ground_rules':'Deer standing/locomotion/feed only. No jumping, flying, swimming or lying takes.',
        'takes':records,'count':len(records),'review_candidates':sum(r['status']=='REVIEW' for r in records)}
    (samples/'candidate-scores.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(report,indent=2),flush=True)


if __name__=='__main__':
    main()
