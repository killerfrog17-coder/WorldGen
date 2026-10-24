"""Use the authored vertical-tail swim for cetacean raw auditions, not fish yaw."""
import json,shutil
from pathlib import Path
import numpy as np
r=Path(__file__).resolve().parent.parent/'artifacts/fauna-motion-sources'
src=r/'fish';dst=r/'cetacean';dst.mkdir(exist_ok=True)
s=json.loads((src/'source-rig.json').read_text());a=np.load(src/'source-motions.npz')
clip=next(c for c in s['clips'] if c['label']=='Swim Vertical');poses=a[clip['key']]
names=[b['name'] for b in s['bones']];delta=poses[:,names.index('tail_tip'),:3,3]-poses[:,names.index('pelvis'),:3,3]
variance=np.std(delta,axis=0);assert variance[2]>5*variance[0]
np.savez_compressed(dst/'source-motions.npz',vertical_swim=poses)
s.update(motion_profile='cetacean',clips=[dict(clip,label='Idle',key='vertical_swim'),dict(clip,key='vertical_swim')],
 adaptation='Both Idle and Swim Vertical use the same unchanged authored vertical-tail swim. No stationary rest, feeding or attack supplied.',
 measured_tail_relative_std_xyz=variance.tolist())
(dst/'source-rig.json').write_text(json.dumps(s,indent=2))
for name in ('LICENSE-CC0.md','provenance.json'):shutil.copy2(src/name,dst/name)
print('Prepared vertical-tail swim source; stationary rest and other motions remain missing.')
