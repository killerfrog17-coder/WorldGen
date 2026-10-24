"""Explicit experimental wing-to-flipper motion bank; no new motion or mesh.

Glide is the moving reference/Idle, not a perched bird pose. Only genuine Flap
and Glide arrays are reused. Anatomical suitability must be checked on targets.
"""
import hashlib,json,shutil
from pathlib import Path
import numpy as np
r=Path(__file__).resolve().parent.parent/'artifacts/fauna-motion-sources'
src=r/'bird';dst=r/'flipper-trial';dst.mkdir(exist_ok=True)
s=json.loads((src/'source-rig.json').read_text());a=np.load(src/'source-motions.npz')
glide=next(c for c in s['clips'] if c['label']=='Glide');flap=next(c for c in s['clips'] if c['label']=='Flap')
np.savez_compressed(dst/'source-motions.npz',glide=a[glide['key']],flap=a[flap['key']])
paths=s['paths'];converted={'spine':paths['spine']+paths['neck'][1:],'neck':paths['neck'],'tail':paths['tail']}
for side in ('L','R'):
    converted['fin_front_'+side]=paths['fore_'+side]
    converted['fin_back_'+side]=paths['hind_'+side]
s.update(family='fish',motion_profile='experimental_flipper',paths=converted,
    clips=[dict(glide,label='Idle',key='glide'),dict(glide,label='Glide transfer',key='glide'),dict(flap,label='Flipper stroke transfer',key='flap')],
    adaptation='EXPERIMENTAL unchanged bird Glide/Flap arrays transferred to flippers, not authored turtle swimming. Idle is a moving glide. No land locomotion, feeding, nesting, resting or death supplied.')
(dst/'source-rig.json').write_text(json.dumps(s,indent=2))
for name in ('LICENSE-CC0.md','provenance.json'):shutil.copy2(src/name,dst/name)
(dst/'trial-provenance.json').write_text(json.dumps({'source_bank':'bird','arrays_unchanged':True,'source_rig_sha256':hashlib.sha256((src/'source-rig.json').read_bytes()).hexdigest(),'source_motion_sha256':hashlib.sha256((src/'source-motions.npz').read_bytes()).hexdigest(),'donor_mesh_exported':False,'art_approved':False},indent=2))
print('Prepared experimental Glide/Flap-to-flipper source, target review pending.')
