"""Record a technical source decision, never owner art approval.

Default requires the serial worker to have exited. A completed, unrelated
animal can defer worker-state synchronization while the producer keeps running.
"""
import argparse,datetime,hashlib,json,subprocess,sys
from pathlib import Path

repo=Path(__file__).resolve().parent.parent;batch=repo/'artifacts/fauna-overnight-20261024'
ap=argparse.ArgumentParser();ap.add_argument('--animal-root',type=Path,required=True)
ap.add_argument('--decision',choices=['usable_for_raw_audition','rejected'],required=True)
ap.add_argument('--reason',required=True);ap.add_argument('--defer-worker-state',action='store_true');a=ap.parse_args();root=a.animal_root.resolve()
live=(batch/'worker.lock').exists();assert root.parent==batch
assert not live or a.defer_worker_state,'Wait for the verified worker exit or defer unrelated-target state synchronization'
state_file=batch/'queue-state.json';state=json.loads(state_file.read_text());entry=state['animals'][root.name]
if live:assert state.get('current')!=root.name,'Never finalize the animal the producer is currently changing'
marker=root/'source-trial-pending.json';trial=json.loads(marker.read_text())
record=root/'source-trial-reviewed.json';request_archive=root/'source-trial-original-request.json'
assert not record.exists() and not request_archive.exists(),'Keep an existing source decision; do not overwrite it'
previous_pending=root/'raw-motion-pending.json';previous_archive=root/'raw-motion-pending-before-source-review.json'
if a.decision=='usable_for_raw_audition' and previous_pending.exists():assert not previous_archive.exists()
meta=json.loads((root/'audition.json').read_text());checks=json.loads((root/'native-checks.json').read_text())
assert not meta.get('art_approved') and len(meta['candidates'])==4 and len(checks['candidates'])==4 and checks['controls']
motion_source=Path(trial['motion_source']).resolve();assert motion_source.is_relative_to(repo/'artifacts/fauna-motion-sources')
assert str(motion_source)==meta['motion_bank']
assert hashlib.sha256((motion_source/'source-rig.json').read_bytes()).hexdigest()==meta['motion_rig_sha256']
assert hashlib.sha256((motion_source/'source-motions.npz').read_bytes()).hexdigest()==meta['motion_array_sha256']
for i in range(1,5):
    assert all((root/f'candidate-{i}'/name).exists() for name in ('model.glb','generated-rig.npz'))
trial.update(decision=a.decision,reason=a.reason,technical_source_review_pending=False,
    owner_art_approval_pending=True,art_approved=False,reviewed=datetime.datetime.now().astimezone().isoformat(),
    motion_rig_sha256=meta['motion_rig_sha256'],motion_array_sha256=meta['motion_array_sha256'])
if a.decision=='rejected':
    dest=root/('rejected-motion-'+Path(trial['motion_source']).name);assert not dest.exists();dest.mkdir()
    for name in ('Animal - Raw Audition.blend','audition.json','native-checks.json'):
        source=(root/name).resolve();target=(dest/name).resolve()
        assert source.parent==root and target.is_relative_to(root)
        source.rename(target)
    trial['retained_failed_review']=str(dest/'Animal - Raw Audition.blend')
    (root/'raw-motion-pending.json').write_text(json.dumps({'animal':trial['animal'],'status':'models_rigged_motion_pending',
        'animated_review_ready':False,'painted':False,'raw_candidates':4,'missing':'Suitable CC0 motion source',
        'rejected_source':trial['motion_source'],'reason':a.reason},indent=2))
    entry.update(status='models_rigged_motion_pending',source=None);entry.pop('review',None)
else:
    entry.update(status='ready',review=str(root/'Animal - Raw Audition.blend'))
    if previous_pending.exists():previous_pending.rename(previous_archive)
trial['worker_state_sync_deferred']=live
temporary=record.with_suffix('.json.tmp');assert not temporary.exists();temporary.write_text(json.dumps(trial,indent=2));temporary.replace(record)
# Removing the marker is last: the running producer skips the target until all
# its artifacts and immutable decision are in place. Never race its state file.
marker.rename(request_archive)
if not live:state_file.write_text(json.dumps(state,indent=2))
subprocess.run([sys.executable,str(repo/'scripts/index-fauna-auditions.py'),'--root',str(batch)],check=True,cwd=repo)
print('Technical source decision recorded; owner art approval remains pending.',a.decision,root.name)
