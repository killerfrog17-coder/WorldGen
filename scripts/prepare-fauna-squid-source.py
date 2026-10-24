"""Project-authored, deliberately modest squid swim envelopes; no donor motion.

Canonical slots store local additive rotations for learned appendage branches.
This is not a capture of real squid dynamics, feeding or individual sucker grip.
"""
import json,math
from pathlib import Path
import numpy as np

repo=Path(__file__).resolve().parent.parent
dest=repo/'artifacts/fauna-motion-sources/procedural-squid';dest.mkdir(exist_ok=True)
names=['Pelvis','Head','Fin_L','Fin_R',*[f'Arm_{n}' for n in range(10)]]
positions=[(0,0,.8),(0,.35,.8),(-.35,-.5,.8),(.35,-.5,.8)]
positions += [(.17*math.cos(n*math.tau/10),.8,.8+.17*math.sin(n*math.tau/10)) for n in range(10)]
bones=[]
for n,p in zip(names,positions):
 m=np.eye(4);m[:3,3]=p
 bones.append({'name':n,'parent':None if n=='Pelvis' else 'Pelvis','head':list(p),'tail':[p[0],p[1]+.08,p[2]],'rest':m.tolist()})
arrays={};clips=[]
for label,cycles,arm_degrees,fin_degrees,bob in [('Idle',1,1.4,4,.004),('Swim',2,3.0,8,.008),('Escape pulse',4,5.0,11,.016)]:
 key=label.lower().replace(' ','_');frames=[]
 for frame in range(91):
  phase=math.tau*cycles*frame/90;mats=np.asarray([b['rest'] for b in bones],dtype=np.float32)
  mats[0,2,3]+=bob*math.sin(phase)
  for i in range(2,len(names)):
   offset=(i-4)*math.tau/10 if i>=4 else (0 if i==2 else math.pi)
   amp=math.radians(arm_degrees if i>=4 else fin_degrees)
   x=amp*math.sin(phase-offset);z=amp*.45*math.sin(phase-offset+.7)
   rx=np.array([[1,0,0],[0,math.cos(x),-math.sin(x)],[0,math.sin(x),math.cos(x)]])
   rz=np.array([[math.cos(z),-math.sin(z),0],[math.sin(z),math.cos(z),0],[0,0,1]])
   mats[i,:3,:3]=rx@rz
  frames.append(mats)
 arrays[key]=np.asarray(frames);clips.append({'label':label,'key':key,'fps':30,'frames':91,'loop':True})
np.savez_compressed(dest/'source-motions.npz',**arrays)
source={'family':'squid','motion_profile':'procedural_squid','source':'Project-authored analytic envelopes; no external animal mesh or animation',
 'license':'Project-authored procedural motion; no third-party motion','root':'Pelvis','head':'Head','reference_pose':'source_bind','bones':bones,
 'paths':{'spine':['Pelvis','Head']},'clips':clips,
 'adaptation':'Simple procedural additive arm/fin envelopes on generated skin. Body held near bind; no feeding, grasping, sucker motion, true fluid dynamics or land action. Species-scale and world movement pending.'}
(dest/'source-rig.json').write_text(json.dumps(source,indent=2))
(dest/'provenance.json').write_text(json.dumps({'author':'WorldGen project procedural implementation','third_party_motion':False,'owner_authorized':'2026-10-24 underwater squid procedural tentacle placeholder',
 'parameters':{'fps':30,'seconds':3,'arm_peak_degrees':[1.4,3,5],'fin_peak_degrees':[4,8,11]},'script':str(Path(__file__).resolve())},indent=2))
for values in arrays.values():
 assert np.isfinite(values).all() and np.max(np.abs(values[0]-values[-1]))<1e-5
 assert np.max(np.ptp(values[:,:,:3,:3],axis=0))>.01
print('Prepared three own looping squid envelopes; no external motion assets.')
