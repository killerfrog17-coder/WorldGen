"""Two explicit CC0 transfer experiments, never invented pinniped clips.

Fore-flipper propulsion uses the unchanged bird-derived Glide/Flap bank. Hind
propulsion maps an unchanged fish tail stroke to both hind-flipper chains.
"""
import hashlib,json,shutil
from pathlib import Path
import numpy as np
root=Path(__file__).resolve().parent.parent/'artifacts/fauna-motion-sources'

def provenance(src,dst):
    for name in ('LICENSE-CC0.md','provenance.json'):shutil.copy2(src/name,dst/name)
    (dst/'trial-provenance.json').write_text(json.dumps({'source_bank':src.name,'arrays_unchanged':True,
        'source_rig_sha256':hashlib.sha256((src/'source-rig.json').read_bytes()).hexdigest(),
        'source_motion_sha256':hashlib.sha256((src/'source-motions.npz').read_bytes()).hexdigest(),
        'donor_mesh_exported':False,'art_approved':False},indent=2))

src=root/'flipper-trial';dst=root/'front-pinniped-trial';dst.mkdir(exist_ok=True)
assert not (dst/'source-rig.json').exists(),'Do not overwrite an inspected source'
s=json.loads((src/'source-rig.json').read_text())
s.update(motion_profile='experimental_front_pinniped',adaptation='EXPERIMENTAL unchanged bird Glide/Flap arrays mapped to fore-flipper propulsion, not authored sea-lion motion. Idle is a moving glide. No hauled-out land locomotion, feeding, resting or death supplied.')
shutil.copy2(src/'source-motions.npz',dst/'source-motions.npz');(dst/'source-rig.json').write_text(json.dumps(s,indent=2));provenance(src,dst)

src=root/'fish';dst=root/'hind-pinniped-trial';dst.mkdir(exist_ok=True)
assert not (dst/'source-rig.json').exists(),'Do not overwrite an inspected source'
s=json.loads((src/'source-rig.json').read_text());arrays=np.load(src/'source-motions.npz')
clips=[c for c in s['clips'] if c['label'] in {'Idle','Swim Horizontal'}];assert len(clips)==2
np.savez_compressed(dst/'source-motions.npz',**{c['key']:arrays[c['key']] for c in clips})
paths=s['paths'].copy()
for side in ('L','R'):paths['fin_back_'+side]=paths['tail'][:]
s.update(motion_profile='experimental_hind_pinniped',paths=paths,clips=clips,
    adaptation='EXPERIMENTAL unchanged fish Idle/Swim Horizontal arrays with tail stroke mapped to paired hind flippers, not authored seal/walrus motion. No hauled-out crawl/walk, feeding, resting or death supplied.')
(dst/'source-rig.json').write_text(json.dumps(s,indent=2));provenance(src,dst)
print('Prepared fore/hind flipper source trials; neither is admitted without target inspection.')
