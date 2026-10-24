"""Select authored CC0 rabbit hind motion for a masked transfer experiment.

No new gait curves: incompatible upper-body channels are held at target bind.
"""
import hashlib,json,shutil
from pathlib import Path
import numpy as np
r=Path(__file__).resolve().parent.parent/'artifacts/fauna-motion-sources';src=r/'rabbit';dst=r/'hind-hopper-trial';dst.mkdir(exist_ok=True)
assert not (dst/'source-rig.json').exists(),'Do not overwrite an inspected bank'
s=json.loads((src/'source-rig.json').read_text());arrays=np.load(src/'source-motions.npz')
clips=[c for c in s['clips'] if c['label'] in {'Idle','Run'}];assert len(clips)==2
np.savez_compressed(dst/'source-motions.npz',**{c['key']:arrays[c['key']] for c in clips})
s.update(motion_profile='experimental_hind_hopper',clips=clips,normalize_root_orientation=True,rotation_transfer='foot_track_ik',reference_pose='source_bind',zero_root_translation_per_clip=True,
    adaptation='EXPERIMENTAL authored rabbit Idle/Run foot paths retargeted with fixed-length planar IK. Upper body, arms, head and tail held at target bind; donor root orientation extracted and per-clip root offset reset. Not authored macropod motion; no slow tail-supported gait, full-body balance, feeding, rest or death supplied.')
(dst/'source-rig.json').write_text(json.dumps(s,indent=2))
for name in ('download-provenance.json',):
    if (src/name).exists():shutil.copy2(src/name,dst/name)
(dst/'LICENSE-CC0.txt').write_text('Rabbit by CDmir/TinyWorlds. CC0 dedication verified at creator page.\nhttps://opengameart.org/content/rabbit-0\nhttps://creativecommons.org/publicdomain/zero/1.0/\nMotion/bones only, no donor mesh.\n')
(dst/'trial-provenance.json').write_text(json.dumps({'source_bank':'rabbit','arrays_unchanged':True,'source_rig_sha256':hashlib.sha256((src/'source-rig.json').read_bytes()).hexdigest(),'source_motion_sha256':hashlib.sha256((src/'source-motions.npz').read_bytes()).hexdigest(),'donor_mesh_exported':False,'art_approved':False},indent=2))
print('Prepared masked hind-leg transfer experiment; target inspection required.')
