"""Archive one explicitly rejected four-model audition before its replacement."""
import argparse,datetime,hashlib,json,shutil
from pathlib import Path

repo=Path(__file__).resolve().parent.parent;batch=repo/'artifacts/fauna-overnight-20261024'
p=argparse.ArgumentParser();p.add_argument('--slug',required=True);p.add_argument('--reason',required=True);p.add_argument('--reference',type=Path)
p.add_argument('--repeat',action='store_true',help='Explicit subsequent owner rejection; retain earlier compact rejection evidence')
p.add_argument('--allow-unrelated-worker',action='store_true',help='Prepare a different animal while the sole producer owns another root')
a=p.parse_args();root=(batch/a.slug).resolve();assert root.parent==batch.resolve()
if (batch/'worker.lock').exists():
    assert a.allow_unrelated_worker,'Finish the sole producer before moving its inputs'
    state=json.loads((batch/'queue-state.json').read_text())
    assert state.get('current') and state['current']!=a.slug,'Never move inputs owned by the live producer'
choices=json.loads((repo/'docs/fauna/OWNER_SELECTIONS.json').read_text());assert a.slug not in choices['animals'],'Do not reroll selected models'
old=root/'previous-owner-rejected'
if a.repeat:
    version=2
    while old.exists():old=root/f'previous-owner-rejected-{version}';version+=1
else:
    assert not old.exists() and not (root/'review-rebuild.json').exists()
    assert not (root/'previous-rejection-provenance.json').exists(),'Archive previous compact provenance first'
meta=json.loads((root/'audition.json').read_text());assert not meta.get('art_approved')
if a.reference:assert a.reference.is_file()
old.mkdir();manifest=[]
for i in range(1,5):
    folder=root/f'candidate-{i}';assert folder.resolve().parent==root and not folder.is_symlink()
    files=[{'path':str(f.relative_to(folder)),'bytes':f.stat().st_size,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()} for f in folder.rglob('*') if f.is_file()]
    assert files and (a.repeat or all((folder/n).is_file() for n in ('model.glb','generated-rig.npz')))
    prev=folder/'seed-overrides.json';seeds=json.loads(prev.read_text()) if prev.exists() else {}
    preparation=json.loads((folder/'shape-generation.json').read_text()) if (folder/'shape-generation.json').exists() else {}
    # Original generation metadata is retained; avoid identical seed reruns.
    plan=next(x for x in json.loads((repo/'docs/fauna/asset-plan.json').read_text())['rows'] if x['slug']==a.slug)
    seed=int(seeds.get('shape_seed',735000+plan['priority_rank']*100+i))+2000003
    folder.rename(old/folder.name);manifest.append({'candidate':i,'files':files});folder.mkdir()
    (folder/'seed-overrides.json').write_text(json.dumps({'shape_seed':seed,'rig_seed':seed+100000,'reason':a.reason},indent=2))
(old/'candidate-manifest.json').write_text(json.dumps(manifest,indent=2))
for name in ('audition.json','native-checks.json'):
    if (root/name).exists():shutil.copy2(root/name,old/name)
# Per-joint/axis corrections belong to the rejected meshes. Keep only generic
# correspondence/source policies for new graphs; never reuse old joint IDs or
# silently defeat their hash guard on a freshly generated model.
repair_path=repo/'docs/fauna/MOTION_REPAIR_OVERRIDES.json'
if repair_path.exists():
    repairs=json.loads(repair_path.read_text())
    if a.slug in repairs:
        previous=repairs[a.slug];(old/'motion-repair-override.json').write_text(json.dumps(previous,indent=2)+'\n')
        generic={k:previous[k] for k in ('anatomical_quadruped','source_flags','motion_bank','retain_flight') if k in previous}
        if generic:
            generic['reason']='Owner-requested reference reroll: generic matching on new graphs; old model-specific corrections archived with rejected inputs.'
            repairs[a.slug]=generic
        else:del repairs[a.slug]
        repair_path.write_text(json.dumps(repairs,indent=2)+'\n')
reference=batch/'references'/f'{a.slug}.png';shutil.copy2(reference,old/'reference.png')
if a.reference:shutil.copy2(a.reference,reference)
(root/'native-checks.json').unlink(missing_ok=True)
request={'reason':a.reason,'time':datetime.datetime.now().astimezone().isoformat(),'art_approved':False,'reference_sha256':hashlib.sha256(reference.read_bytes()).hexdigest(),'replacement_models':4}
(root/'review-rebuild.json').write_text(json.dumps(request,indent=2));(root/'owner-reroll-request-current.json').write_text(json.dumps(request,indent=2))
print('Prepared rejected audition replacement',a.slug)
