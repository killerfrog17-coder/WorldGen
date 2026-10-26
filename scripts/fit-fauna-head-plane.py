"""Geometry-based bilateral head plane for the queued eye review."""
import argparse,hashlib,json
from pathlib import Path
import numpy as np
from scipy.spatial import cKDTree
from scipy.optimize import differential_evolution
repo=Path(__file__).resolve().parent.parent;ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);a=ap.parse_args();root=repo/'artifacts/fauna-review-revisions'/a.slug;land=json.loads((root/'source-landmarks.json').read_text());p=np.load(root/'paint-anatomy.npz')['vertices'];nose=np.array(land['nose']['point']);mid=(np.array(land['right_eye']['point'])+np.array(land['left_eye']['point']))*.5;gap=np.linalg.norm(np.array(land['right_eye']['point'])-np.array(land['left_eye']['point']));old=np.array(land['front_axis']);old[2]=0;old/=np.linalg.norm(old);extent=float(np.clip(max(gap*1.8,np.linalg.norm(nose-mid)*1.4),.15,.50));z=mid[2];depth=(p-nose)@old;mask=(p[:,2]>z-extent*.36)&(p[:,2]<z+extent*.40)&(depth>-extent*.86)&(depth<extent*.16)&(np.linalg.norm((p-nose)[:,:2],axis=1)<extent);head=p[mask]
if len(head)<20:
 mask=(np.linalg.norm(p-mid,axis=1)<extent);head=p[mask]
assert len(head)>8,(a.slug,'insufficient skull support');tree=cKDTree(head)
manual_file=repo/'docs/fauna/EYE_REVIEW_ADJUSTMENTS.json';manual=json.loads(manual_file.read_text()).get('animals',{}).get(a.slug) if manual_file.exists() else None
if manual and 'forward' in manual:
 state=json.loads((root/'paint-state.json').read_text());assert manual['geometry_sha256']==state['geometry_sha256'];forward=np.array(manual.get('forward',old),dtype=float);forward[2]=0;forward/=np.linalg.norm(forward);side=np.array([forward[1],-forward[0],0.]);report={'forward':forward.tolist(),'side':side.tolist(),'lateral_center':float(mid@side),'plane_offset':float(mid@side),'z':float(z+manual.get('height_delta',0)+.006),'eye_depth':float(mid@forward+manual.get('depth_delta',0)),'sample_count':len(head),'manual_visual_adjustment':manual,'agent_visually_checked':False};(root/'head-plane-fit.json').write_text(json.dumps(report,indent=2)+'\n');print('REVIEWED HEAD PLANE',a.slug);raise SystemExit(0)
def error(v):
 theta,offset=v;side=np.array([np.cos(theta),-np.sin(theta),0]);d=nose@side+offset;ref=head-2*(head@side-d)[:,None]*side;distance=tree.query(ref)[0];return np.mean(np.minimum(distance,extent*.12)**2)+offset**2*.30
t0=np.arctan2(old[0],old[1]);result=differential_evolution(error,[(t0-.70,t0+.70),(-extent*.11,extent*.11)],seed=17,popsize=10,maxiter=80,tol=1e-7);theta,offset=result.x;side=np.array([np.cos(theta),-np.sin(theta),0]);forward=np.array([np.sin(theta),np.cos(theta),0]);report={'forward':forward.tolist(),'side':side.tolist(),'plane_offset':float(nose@side+offset),'z':float(z),'eye_depth':float(mid@forward),'fit_rms':float(np.sqrt(result.fun)),'sample_count':len(head),'extent':extent,'agent_visually_checked':False}
if manual:report.update(manual_visual_adjustment=manual,lateral_center=float(mid@side),z=float(z+manual.get('height_delta',0)+.006),eye_depth=float(mid@forward+manual.get('depth_delta',0)))
(root/'head-plane-fit.json').write_text(json.dumps(report,indent=2)+'\n');print('SKULL PLANE',a.slug,len(head),report['fit_rms'])
