"""Exact final eye-pass inventory; technical checks do not grant art approval."""
import hashlib,json
from pathlib import Path
from fauna_eye_profiles import pipeline_signature
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024';jobs=json.loads((root/'paint-queue.json').read_text())['jobs']
signature=pipeline_signature(repo);errors={};verified=[];samples=0
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
for job in jobs:
 slug=job['slug'];folder=root/slug
 try:
  digest=sha(folder/'Animal - Painted Species Review.blend');revision=json.loads((folder/'eye-audit/revision.json').read_text());native=json.loads((folder/'paint-native-checks.json').read_text());motion=json.loads((folder/'paint-motion-checks.json').read_text());final=json.loads((folder/'final-qa.json').read_text())
  assert revision['pipeline_sha256']==signature,'changed implementation'
  assert revision['review_sha256']==sha(folder/'eye-review.json'),'changed inspected picks'
  assert revision['blend_sha256']==native['checked_blend_sha256']==motion['read_blend_sha256']==final['checked_blend_sha256']==digest,'stale native evidence'
  assert native['source_bind_geometry_skin_rig_exact'] and native['source_actions_exact'],'source changed'
  assert not revision['actual_after_vision_pending'] and final['actually_visually_inspected'],'pending actual eye inspection'
  assert motion['all_sampled_surfaces_finite'] and not motion['warnings'],'attachment warning'
  for v in revision['visual_inspection']['views']:
   assert v['source_sha256']==digest and v['sha256']==sha(folder/f"eye-audit/{v['view']}-after.png"),'changed inspected view'
  for view in ['side','front','rear']:
   path=folder/f'previews/1-Bind-{view}-painted.png';meta=json.loads(path.with_suffix('.json').read_text());assert meta['source_sha256']==digest and meta['render_sha256']==sha(path),'stale body view'
  assert final['lineage_render_sha256']==sha(repo/final['lineage_render']),'stale alternate coat'
  samples+=len(motion['samples']);verified.append(slug)
 except Exception as exc:errors[slug]=str(exc)
deer=repo/'artifacts/fauna-assets/deer/Deer - Prototype.blend';protected=sha(deer)=='0e2ff46b0b0860750084bed3af94d84f636144230550e9e80f753b3e415418a1'
report={'total':len(jobs),'verified':len(verified),'animals':verified,'errors':errors,'sampled_clip_phases':samples,'accepted_deer_unchanged':protected,'appearance_approved':False,'limits':'Agent eye-focused review and source-preservation/attachment checks. Existing selected-body sculpt, gait and collision limitations remain. Owner appearance acceptance is independent.'}
(root/'inspection/eye-pass-completion.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({k:v for k,v in report.items() if k!='animals'}));assert not errors and protected
