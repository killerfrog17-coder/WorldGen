"""Publish an unrelated target's checked manual recovery without editing live worker state."""
import argparse,hashlib,json
from pathlib import Path
repo=Path(__file__).resolve().parent.parent;batch=repo/'artifacts/fauna-overnight-20261024'
p=argparse.ArgumentParser();p.add_argument('--slug',required=True);p.add_argument('--reason',required=True);a=p.parse_args();root=(batch/a.slug).resolve();assert root.parent==batch.resolve()
state=json.loads((batch/'queue-state.json').read_text());assert state.get('current')!=a.slug
checks=json.loads((root/'native-checks.json').read_text());meta=json.loads((root/'audition.json').read_text());assert checks['controls'] and len(checks['candidates'])==len(meta['candidates'])==4 and not meta.get('art_approved')
retained={p.relative_to(root).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for n in range(1,5) for p in [root/f'candidate-{n}'/'model.glb',root/f'candidate-{n}'/'generated-rig.npz']}
request={'reason':a.reason,'prepared_models':[retained[f'candidate-{n}/model.glb'] for n in range(1,5)]}
if state['animals'].get(a.slug,{}).get('status')=='failed':(root/'retry-failed.json').write_text(json.dumps(request,indent=2))
marker=root/'review-rebuild.json'
if marker.exists():
    dest=root/'manual-rebuild-completed.json'
    if dest.exists():dest=root/'manual-rebuild-completed-current.json'
    assert not dest.exists();marker.rename(dest)
removed=[]
for n in range(1,5):
    for name in ('raw.glb','raw-clean.glb','raw.latents.pt','reference-prepared.png'):
        p=(root/f'candidate-{n}'/name).resolve();assert p.parent==root/f'candidate-{n}'
        if p.exists():removed.append({'path':str(p.relative_to(root)),'bytes':p.stat().st_size});p.unlink()
assert all(hashlib.sha256((root/p).read_bytes()).hexdigest()==h for p,h in retained.items())
(root/'manual-recovery-ready.json').write_text(json.dumps({'reason':a.reason,'native_checks_pass':True,'art_approved':False,'worker_state_sync_pending':state['animals'].get(a.slug,{}).get('status')=='failed','retained_sha256':retained,'removed_exact_intermediates':removed},indent=2))
print('Checked manual recovery published',a.slug,'removed MiB',round(sum(x['bytes'] for x in removed)/2**20,2))
