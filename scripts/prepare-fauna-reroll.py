"""Archive one explicitly rejected four-model audition before its replacement."""
import argparse,datetime,hashlib,json,shutil
from pathlib import Path

repo=Path(__file__).resolve().parent.parent;batch=repo/'artifacts/fauna-overnight-20261024'
p=argparse.ArgumentParser();p.add_argument('--slug',required=True);p.add_argument('--reason',required=True);p.add_argument('--reference',type=Path)
a=p.parse_args();root=(batch/a.slug).resolve();assert root.parent==batch.resolve()
assert not (batch/'worker.lock').exists(),'Finish the sole producer before moving its inputs'
choices=json.loads((repo/'docs/fauna/OWNER_SELECTIONS.json').read_text());assert a.slug not in choices['animals'],'Do not reroll selected models'
old=root/'previous-owner-rejected';assert not old.exists() and not (root/'review-rebuild.json').exists()
assert not (root/'previous-rejection-provenance.json').exists(),'Archive previous compact provenance first'
meta=json.loads((root/'audition.json').read_text());assert not meta.get('art_approved')
if a.reference:assert a.reference.is_file()
old.mkdir();manifest=[]
for i in range(1,5):
    folder=root/f'candidate-{i}';assert folder.resolve().parent==root and not folder.is_symlink()
    files=[{'path':str(f.relative_to(folder)),'bytes':f.stat().st_size,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()} for f in folder.rglob('*') if f.is_file()]
    assert files and all((folder/n).is_file() for n in ('model.glb','generated-rig.npz'))
    prev=folder/'seed-overrides.json';seeds=json.loads(prev.read_text()) if prev.exists() else {}
    preparation=json.loads((folder/'shape-generation.json').read_text()) if (folder/'shape-generation.json').exists() else {}
    # Original generation metadata is retained; avoid identical seed reruns.
    plan=next(x for x in json.loads((repo/'docs/fauna/asset-plan.json').read_text())['rows'] if x['slug']==a.slug)
    seed=int(seeds.get('shape_seed',735000+plan['priority_rank']*100+i))+2000003
    folder.rename(old/folder.name);manifest.append({'candidate':i,'files':files});folder.mkdir()
    (folder/'seed-overrides.json').write_text(json.dumps({'shape_seed':seed,'rig_seed':seed+100000,'reason':a.reason},indent=2))
(old/'candidate-manifest.json').write_text(json.dumps(manifest,indent=2))
for name in ('audition.json','native-checks.json'):shutil.copy2(root/name,old/name)
reference=batch/'references'/f'{a.slug}.png';shutil.copy2(reference,old/'reference.png')
if a.reference:shutil.copy2(a.reference,reference)
(root/'native-checks.json').unlink()
request={'reason':a.reason,'time':datetime.datetime.now().astimezone().isoformat(),'art_approved':False,'reference_sha256':hashlib.sha256(reference.read_bytes()).hexdigest(),'replacement_models':4}
(root/'review-rebuild.json').write_text(json.dumps(request,indent=2));(root/'owner-reroll-request-current.json').write_text(json.dumps(request,indent=2))
print('Prepared rejected audition replacement',a.slug)
