"""Offline contact cleanup and candidate selection; original samples immutable.

Conservative stance anchors, damped least-squares three-joint IK, eased contact
boundaries. No live IK, root heaving, bone stretching or whole-gait smoothing.
Metrics are ranking aids, not a naturalness guarantee.
"""
import argparse
import importlib.util
import json
from pathlib import Path
import numpy as np
from scipy.ndimage import median_filter
from scipy.spatial.transform import Rotation


def fk(q,local,parents):
    r=Rotation.from_quat(q.reshape(-1,4),scalar_first=True).as_matrix().reshape(*q.shape[:-1],3,3)
    g=r.copy();p=local.copy()
    for j,parent in enumerate(parents):
        if parent>=0:
            p[:,j]=p[:,parent]+np.einsum('fab,fb->fa',g[:,parent],local[:,j])
            g[:,j]=g[:,parent]@r[:,j]
    return p,g


def cleanup(q,local,parents,names,rest,fps=30):
    original=q.copy();q=q.copy();raw,_=fk(q,local,parents)
    feet=[i for i,n in enumerate(names) if 'Hoof' in n]
    length=float(rest[0,1]-rest[feet,1].min());contacts=[]
    for foot in feet:
        speed=np.linalg.norm(np.gradient(raw[:,foot],axis=0),axis=-1)*fps/length
        height=(raw[:,foot,1]-rest[foot,1])/length
        contact=median_filter((speed<.55)&(height<.10)&(height>-.15),size=5,mode='nearest')
        boundaries=np.flatnonzero(np.diff(np.r_[False,contact,False]))
        chain=[];j=int(parents[foot])
        while j>0 and len(chain)<3:
            chain.append(j);j=int(parents[j])
        for start,end in zip(boundaries[::2],boundaries[1::2]):
            if end-start<4:continue
            anchor=raw[start:end,foot].mean(0);anchor[1]=rest[foot,1]
            contacts.append(dict(foot=names[foot],first=int(start),last=int(end-1)))
            for frame in range(start,end):
                blend=min(1.,(frame-start+1)/5,(end-frame)/5)
                blend=blend*blend*(3-2*blend)
                target=raw[frame,foot]*(1-blend)+anchor*blend
                for _ in range(5):
                    p,g=fk(q[frame:frame+1],local[frame:frame+1],parents);p=p[0];g=g[0]
                    error=(target-p[foot])/length
                    if np.linalg.norm(error)<.001:break
                    jac=np.column_stack([np.cross(g[j,:,axis],p[foot]-p[j])/length
                        for j in chain for axis in range(3)])
                    step=jac.T@np.linalg.solve(jac@jac.T+np.eye(3)*.04**2,error)
                    for k,j in enumerate(chain):
                        delta=step[k*3:k*3+3]
                        delta*=min(1,np.radians(6)/max(np.linalg.norm(delta),1e-8))
                        r=Rotation.from_quat(q[frame,j],scalar_first=True)*Rotation.from_rotvec(delta)
                        base=Rotation.from_quat(original[frame,j],scalar_first=True)
                        correction=(base.inv()*r).as_rotvec()
                        correction*=min(1,np.radians(22)/max(np.linalg.norm(correction),1e-8))
                        q[frame,j]=(base*Rotation.from_rotvec(correction)).as_quat(scalar_first=True)
    # Smooth only correction rotations, never the original gait or root track.
    corrections=(Rotation.from_quat(original.reshape(-1,4),scalar_first=True).inv()*
        Rotation.from_quat(q.reshape(-1,4),scalar_first=True)).as_rotvec().reshape(*q.shape[:2],3)
    padded=np.pad(corrections,((1,1),(0,0),(0,0)),mode='edge')
    corrections=(padded[:-2]+padded[1:-1]*2+padded[2:])/4
    q=(Rotation.from_quat(original.reshape(-1,4),scalar_first=True)*
        Rotation.from_rotvec(corrections.reshape(-1,3))).as_quat(scalar_first=True).reshape(q.shape)
    return q,contacts


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
    a=ap.parse_args();root=a.root.resolve()
    spec=importlib.util.spec_from_file_location('score',Path(__file__).with_name('score-deer-unimate.py'))
    score=importlib.util.module_from_spec(spec);spec.loader.exec_module(score)
    pilot=root/'unimate-samples/pilot';ranking=json.loads((pilot/'candidate-scores.json').read_text())
    cond=np.load(root/'unimate-rig/cond.npy',allow_pickle=True).item()['WorldGenDeer']
    chosen=[]
    for action in ('walk','run','feed','alert'):
        r=next(r for r in ranking['takes'] if r['action']==action)
        chosen.append((f'UniMate {action}',pilot/'decoded'/Path(r['file']).with_suffix('.npz'),
            cond['tpos_first_frame'],action,r))
    mocap=root/'mocap-results/mocap-raw.npz'
    if mocap.exists():
        with np.load(mocap) as m:rest=m['rest_positions'].copy()
        chosen.append(('MoCap browse',mocap,rest,'feed',{}))
    pose=root/'mocap-results/mocap-pose-raw.npz'
    if pose.exists():
        with np.load(pose) as m:rest=m['rest_positions'].copy()
        chosen.append(('MoCap pose IK',pose,rest,'feed',{}))
    out=root/'comparison-motions';out.mkdir(exist_ok=True);report=[]
    for label,path,rest,action,rank in chosen:
        with np.load(path) as n:data={k:n[k].copy() for k in n.files}
        names=list(data['names']);q=data['rotations'];local=data['local_positions'];parents=data['parents']
        raw,_=fk(q,local,parents)
        cleaned,contacts=cleanup(q,local,parents,names,rest)
        cp,_=fk(cleaned,local,parents)
        before=score.evaluate(raw,q,rest,names,action)
        after=score.evaluate(cp,cleaned,rest,names,action)
        admitted=after['status']=='REVIEW' and after['score']<=before['score']*1.02 and \
            after['normalized_acceleration_p95']<=before['normalized_acceleration_p95']*1.15 and \
            after['rotation_step_max_deg']<=max(15,before['rotation_step_max_deg']*1.2) and \
            after['mean_penetration']<=before['mean_penetration']+.005
        # Keep the rejected cleanup too for diagnostic inspection, labelled.
        for suffix,rot,pos,metrics in [('raw',q,raw,before),('clean',cleaned,cp,after)]:
            name=f'{label} {suffix}';target=out/(name+'.npz')
            gp,gr=fk(rot,local,parents)
            np.savez_compressed(target,rotations=rot,positions=gp,global_rotations=gr,
                local_positions=local,parents=parents,names=np.array(names),rest_positions=rest,fps=30)
        report.append(dict(label=label,source=str(path),raw=before,clean=after,
            cleanup_admitted=bool(admitted),contacts=contacts,selected_rank=rank,
            note='Non-looping take; end/start seam is not a locomotion transition'))
    (root/'comparison-report.json').write_text(json.dumps(report,indent=2))
    print(json.dumps([{k:r[k] for k in ('label','cleanup_admitted','contacts')} for r in report],indent=2))


if __name__=='__main__':main()
