"""Record an explicit human/agent visual review, not an automatic approval.

Run only after actually inspecting the current front/both-side rendered heads
and the changed body-pattern previews. Hash gates prevent stale render reuse.
"""
import argparse,hashlib,json
from pathlib import Path
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-review-revisions'
ap=argparse.ArgumentParser();ap.add_argument('--confirm-inspected',action='store_true',required=True);a=ap.parse_args()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
media=json.loads((root/'feedback-media-check.json').read_text());assert not media['pending'] and not media['errors']
items=[]
for slug in media['checked']:
 folder=root/slug;plan=folder/'phone-review-plan.json';item=json.loads(plan.read_text())['animals'][0];native=repo/item['source'];digest=sha(native)
 assert digest==item['source_sha256']
 proof_path=folder/'revision-native-check.json';proof=json.loads(proof_path.read_text());assert proof['all_checks_passed'] and proof['checked_blend_sha256']==digest
 assert json.loads((folder/'eye-audit/after-renders.json').read_text())['source_sha256']==digest
 evidence=[]
 for view in ['front','right','left']:
  file=folder/'eye-audit'/(view+'-after.png');evidence.append({'file':str(file.relative_to(repo)),'sha256':sha(file)})
 proof.update(actually_visually_inspected=True,visual_evidence=evidence,owner_approved=False)
 proof['visual_review_limits']='Front and both sides reviewed against native sculpt renders. Coarse/asymmetric source skulls and facial fragments remain prototype art; this does not approve gait or certify every real species anatomy. Horse retains independently fitted asymmetric source sockets; no rejected skull edits are published.'
 proof_path.write_text(json.dumps(proof,indent=2)+'\n')
 items.append({'slug':slug,'status':'awaiting_owner_review','owner_approved':False,'review_update':'Eye placement, eyelid joins and highlights reviewed; coat patterns revised. Original selected body and motion retained.' if slug!='horse' else 'Eyes refitted to the original sockets; source skull asymmetry remains for review.','repair_review':{'plan':str(plan.relative_to(repo)),'media_root':str((root/'phone-media').relative_to(repo)),'check':str(proof_path.relative_to(repo)),'blend':item['source'],'blend_sha256':digest}})
index=root/'checked-review-revisions.json';index.write_text(json.dumps({'items':items,'owner_approved':False,'visual_bodies_audited':177,'added_eye_bodies':168,'batch_published_bodies':len(items),'retained':{'accepted_deer':1,'procedural_tiny_guilds':5,'surface_only_generated_arthropods':3},'explicit_motion_repairs':'Rabbit/Fox override this appearance batch; Hare batch starts from its Run repair.'},indent=2)+'\n')
feedback_path=repo/'docs/fauna/PHONE_REVIEW_FEEDBACK.json';feedback=json.loads(feedback_path.read_text())
for row in feedback['items']:
 if row['slug']=='hare':
  revised=next(item for item in items if item['slug']=='hare');row['repair_review']=revised['repair_review'];row['review_update']='Run repair retained; paired eyes, eyelid joins and coat patterns updated.'
 if row['slug'] in ['__all_eyes__','__all_stripes__','wolf']:
  row.update(status='awaiting_owner_review',owner_approved=False)
 if row['slug']=='__all_eyes__':row.update(checked_revisions_index=str(index.relative_to(repo)),correction='Paired skull-plane/surface fits, source-specific manual adjustments, seam-free fur surround sampling and softer corneal highlights. All 177 visual entries audited; 168 added-eye bodies revised. Nine tiny/protected/surface-only entries retain appropriate original treatment.',remaining_limits=['Horse original asymmetric sockets retained; rejected skull edits excluded.','Several coarse source heads retain angular surrounds; owner art review remains pending.'])
 if row['slug']=='__all_stripes__':row['correction']='Static atlases now use curved, tapered, irregular, branching and broken marks. Tiger/Zebra anatomical body/limb/head masks and Tiger tail rings were separately inspected; discrete palettes retained.'
 if row['slug']=='__all_colour_variation__':row.update(status='design_recorded_pending_runtime_profile',correction='Shared-material stable per-instance tint design recorded; runtime disabled until measured. No Unity/game changes or individual texture duplication.')
feedback_path.write_text(json.dumps(feedback,indent=2)+'\n');print('VISUAL REVIEW INDEX',len(items))
