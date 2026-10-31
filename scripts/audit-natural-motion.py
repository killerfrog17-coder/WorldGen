"""Rank raw motion auditions using whole-clip measurements, not visual approval."""
import argparse
import json
from pathlib import Path
import numpy as np

REPO=Path(__file__).resolve().parent.parent
ROOT=REPO/'artifacts/human-natural-20261031'

def multiply(a,b):
 x,y,z,w=np.moveaxis(a,-1,0);u,v,t,s=np.moveaxis(b,-1,0)
 return np.stack((w*u+x*s+y*t-z*v,w*v-x*t+y*s+z*u,w*t+x*v-y*u+z*s,w*s-x*u-y*v-z*t),axis=-1)

def rotate(q,v):
 u=q[...,:3];t=2*np.cross(u,v)
 return v+q[...,3,None]*t+np.cross(u,t)

def positions(folder):
 info=json.loads((folder/'generation.json').read_text());skel=json.loads((ROOT/'skeleton.json').read_text())
 n=info['frames'];raw=np.fromfile(folder/'local_rotations_xyzw.f32',dtype='<f4').reshape(n,30,4).astype(float)
 raw/=np.linalg.norm(raw,axis=-1,keepdims=True)
 roots=np.fromfile(folder/'root_positions.f32',dtype='<f4').reshape(n,3)
 neutral=np.array(skel['neutral_joints']);world=np.zeros((n,30,3));rot=np.zeros_like(raw)
 for i,parent in enumerate(skel['parents']):
  if parent<0:world[:,i]=roots;rot[:,i]=raw[:,i]
  else:
   rot[:,i]=multiply(rot[:,parent],raw[:,i]);world[:,i]=world[:,parent]+rotate(rot[:,parent],neutral[i]-neutral[parent])
 return info,{name:world[:,i] for i,name in enumerate(skel['names'])}

def measure(folder):
 info,p=positions(folder);hip=p['Hips'];n=info['frames'];case=info['id'];middle=slice(5,n-5)
 relative={name:value-hip for name,value in p.items()}
 def excursion(name):return float(np.linalg.norm(np.ptp(relative[name][middle],axis=0)))
 def flex(side):
  a=p[side+'Leg']-p[side+'Shin'];b=p[side+'Foot']-p[side+'Shin'];cos=np.sum(a*b,axis=1)/(np.linalg.norm(a,axis=1)*np.linalg.norm(b,axis=1))
  return 180-np.degrees(np.arccos(np.clip(cos,-1,1)))
 spine=p['Chest']-hip;lean=np.degrees(np.arccos(np.clip(spine[:,1]/np.linalg.norm(spine,axis=1),-1,1)))
 m={'case':case,'take':folder.name,'frames':n,'hip_dip':float(np.percentile(hip[:,1],95)-np.percentile(hip[:,1],5)),
    'hip_range':float(np.ptp(hip[:,1])),'torso_lean_p95':float(np.percentile(lean,95)),
    'knee_flex_p95':float(np.percentile(np.concatenate((flex('Left'),flex('Right'))),95)),
    'right_hand_excursion':excursion('RightHand'),'left_hand_excursion':excursion('LeftHand'),
    'right_hand_above_hip_p95':float(np.percentile(relative['RightHand'][:,1],95)),
    'right_hand_vertical_range':float(np.ptp(relative['RightHand'][:,1])),
    'left_hand_above_hip_p95':float(np.percentile(relative['LeftHand'][:,1],95)),
    'root_horizontal_travel':float(np.linalg.norm((hip[-1]-hip[0])[[0,2]])),
    'right_hand_acceleration_p99':float(np.percentile(np.linalg.norm(np.diff(relative['RightHand'],n=2,axis=0)*900,axis=1),99))}
 score=0;flags=[]
 relaxed=case in {'idle','tool_ready','bow_ready','spear_ready_one','spear_ready_two','sling_ready','fists_ready','fists_guard','tool_guard','carry_large'}
 upper=case in {'tool_stab','tool_cut','tool_blunt','spear_thrust_one','spear_thrust_two','punch','bow_shoot','drink','eat','sling_whirl','sling_release'}
 if relaxed or upper or case.startswith('work_') and case!='work_dig':
  dip_limit=.055 if relaxed else .1
  if m['hip_dip']>dip_limit:flags.append('unwanted pelvis dip');score+=(m['hip_dip']-dip_limit)*80
  knee_limit=30 if relaxed else 45
  if m['knee_flex_p95']>knee_limit:flags.append('excess knee bend');score+=(m['knee_flex_p95']-knee_limit)*.06
  if m['torso_lean_p95']>25:flags.append('excess torso lean');score+=(m['torso_lean_p95']-25)*.07
 if relaxed:
  score+=m['root_horizontal_travel']*5
  score+=max(0,m['right_hand_excursion']-.35)*4+max(0,m['left_hand_excursion']-.35)*4
 if upper and case not in {'drink','eat','bow_shoot','sling_whirl'}:
  if m['right_hand_excursion']<.25:flags.append('weak arm action');score+=(.25-m['right_hand_excursion'])*20
 if case in {'tool_blunt','tool_chop'} and m['right_hand_vertical_range']<.25:
  flags.append('insufficient downward strike');score+=(.25-m['right_hand_vertical_range'])*40
 if case=='tool_stab':
  peak=int(np.argmax(relative['RightHand'][:,2]));height=relative['RightHand'][peak,1]
  if height<.22:flags.append('thrust excessively low for level aiming');score+=(.22-height)*15
 if case.startswith('work_') and case!='work_dig':
  if m['right_hand_above_hip_p95']>.42:flags.append('work hands excessively high');score+=(m['right_hand_above_hip_p95']-.42)*20
  if m['right_hand_excursion']<.045:flags.append('almost static hand task');score+=(.045-m['right_hand_excursion'])*30
 if case.startswith('swim'):
  if m['torso_lean_p95']<45:flags.append('not horizontal swimming');score+=(45-m['torso_lean_p95'])*.2
  if case!='swim_carry_two' and m['left_hand_excursion']<.3:flags.append('insufficient free-arm stroke');score+=(.3-m['left_hand_excursion'])*20
 if case=='bow_shoot':
  separation=np.linalg.norm(p['RightHand']-p['LeftHand'],axis=1)
  m['bow_hand_separation_range']=float(np.ptp(separation));m['right_hand_to_face_min']=float(np.min(np.linalg.norm(p['RightHand']-p['Head'],axis=1)))
  if m['bow_hand_separation_range']<.25:flags.append('little bow draw');score+=(.25-m['bow_hand_separation_range'])*30
  if m['right_hand_to_face_min']>.35:flags.append('no clear face anchor');score+=(m['right_hand_to_face_min']-.35)*20
 m['screening_score']=round(score,4);m['flags']=flags;m['path']=folder.relative_to(REPO).as_posix();return m

def main():
 a=argparse.ArgumentParser();a.add_argument('--select',action='store_true');args=a.parse_args()
 rows=[]
 for path in sorted((ROOT/'motions').glob('*/seed-*/generation.json')):
  rows.append(measure(path.parent))
 groups={}
 for r in rows:groups.setdefault(r['case'],[]).append(r)
 for case,records in groups.items():
  records.sort(key=lambda r:r['screening_score']);best=records[0]
  if args.select:(ROOT/'motions'/case/'selected.json').write_text(json.dumps({'take':best['take'],'screening_score':best['screening_score'],'visual_approval':False},indent=2))
 (ROOT/'raw-screening.json').write_text(json.dumps({'takes':rows,'ranked':groups,'note':'Ranking diagnostics only. Not animation/prop visual approval.'},indent=2))
 for case,records in groups.items():print(case,records[0]['take'],records[0]['screening_score'],', '.join(records[0]['flags']))
 print('RAW AUDITIONS SCREENED',len(rows),'cases',len(groups))

if __name__=='__main__':main()
