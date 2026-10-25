"""Explicit aquatic CC0 fin-stroke adaptation; never relabel fox running as swim."""
import hashlib,json,shutil
from pathlib import Path
import numpy as np
repo=Path(__file__).resolve().parent.parent;bank=repo/'artifacts/fauna-motion-sources'
src=bank/'fish';dst=bank/'sea-otter-swim-trial';dst.mkdir(exist_ok=True)
assert not (dst/'source-rig.json').exists(),'Keep an existing source trial intact'
s=json.loads((src/'source-rig.json').read_text());arrays=np.load(src/'source-motions.npz')
clips=[dict(c) for c in s['clips'] if c['label'] in ('Idle','Swim Horizontal')];assert len(clips)==2
for clip in clips:
    if clip['label']=='Swim Horizontal':clip['label']='Swim'
paths={key:list(s['paths'][key]) for key in ('spine','neck','tail')}
for side in ('L','R'):
    paths['fore_'+side]=s['paths']['fin_front_'+side];paths['hind_'+side]=s['paths']['fin_back_'+side]
s.update(family='crocodile',paths=paths,clips=clips,motion_profile='experimental_aquatic_quadruped',aquatic_only=True,
         adaptation='EXPERIMENTAL aquatic paddle transfer from unchanged CC0 fish fin-stroke clips onto four otter limbs; not authored sea-otter capture. No fox/dog Walk or Run, land gait, grooming or back-float clip supplied.')
np.savez_compressed(dst/'source-motions.npz',**{c['key']:arrays[c['key']] for c in clips})
(dst/'source-rig.json').write_text(json.dumps(s,indent=2)+'\n')
for name in ('LICENSE-CC0.md','provenance.json'):
    if (src/name).exists():shutil.copy2(src/name,dst/name)
(dst/'trial-provenance.json').write_text(json.dumps({'source_bank':'fish','arrays_unchanged':True,'source_rig_sha256':hashlib.sha256((src/'source-rig.json').read_bytes()).hexdigest(),'source_motion_sha256':hashlib.sha256((src/'source-motions.npz').read_bytes()).hexdigest(),'donor_mesh_exported':False,'art_approved':False,'research':'Gobkit creator lists idle/attack/dead/walk, not a dedicated swim; sampled shared skeletons were previously unsuitable. Public SeaOtter GLB returned403. No paid download or auth used.'},indent=2)+'\n')
p=repo/'docs/fauna/MOTION_REPAIR_OVERRIDES.json';d=json.loads(p.read_text());d['sea-otter']={'motion_bank':str(dst.relative_to(repo)),'anatomical_quadruped':True,'source_flags':{'hold_core_bind':True,'reference_pose':'source_bind','minimal_twist_limb_swing':True},'reason':'Owner explicitly rejects dog running for aquatic sea otter; inspected aquatic pose and bounded free fin-stroke transfer instead.'};p.write_text(json.dumps(d,indent=2)+'\n')
print('Prepared aquatic-only sea-otter stroke trial; target inspection still required')
