"""Reprepare exact unapproved night candidates after a verified reduction bug."""
import argparse,json
from pathlib import Path
repo=Path(__file__).resolve().parent.parent;base=repo/'artifacts/fauna-overnight-20261024'
ap=argparse.ArgumentParser();ap.add_argument('--animal',required=True);ap.add_argument('--candidates',nargs='+',type=int,required=True);ap.add_argument('--reason',required=True)
a=ap.parse_args();folder=(base/a.animal).resolve();assert folder.parent==base.resolve() and not (base/'worker.lock').exists()
meta=folder/'audition.json'
assert not meta.exists() or not json.loads(meta.read_text()).get('art_approved')
for n in a.candidates:
    assert 1<=n<=4;candidate=folder/f'candidate-{n}';backup=candidate/'preparation-v1';assert not backup.exists();backup.mkdir()
    if not (candidate/'raw.glb').exists():
        original=json.loads((candidate/'raw.json').read_text());seed=original['seed']
        override=candidate/'seed-overrides.json'
        if not override.exists():override.write_text(json.dumps({'shape_seed':seed,'rig_seed':seed+100000,'reason':'Regenerate original unpainted raw seed for fragment-safe preparation after saved intermediate cleanup'},indent=2))
    for name in ('model.glb','generated-rig.npz','generated-rig.json','mesh-preparation.json','mesh-component-cleanup.json'):
        source=candidate/name;target=backup/name
        assert source.resolve().parent==candidate.resolve() and target.resolve().parent==backup.resolve()
        if source.exists():source.rename(target)
(folder/'review-rebuild.json').write_text(json.dumps({'reason':a.reason,'candidates':a.candidates,'preparation_version':2,'previous_unapproved_review_retained':True},indent=2))
p=base/'queue-state.json';state=json.loads(p.read_text());state['animals'][a.animal].update(status='rebuild_pending',reason=a.reason);p.write_text(json.dumps(state,indent=2))
print('Prepared exact candidate refresh',a.animal,a.candidates,flush=True)
