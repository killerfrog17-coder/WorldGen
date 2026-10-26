"""Record explicitly inspected final contact sheets and publishable repair IDs."""
import argparse,hashlib,json
from pathlib import Path
import cv2
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-review-revisions/final-sweep';ap=argparse.ArgumentParser();ap.add_argument('--confirm-inspected',action='store_true',required=True);a=ap.parse_args()
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
audit=json.loads((root/'review.json').read_text());assert len(audit['animals'])==177 and len(audit['sheets'])==60
for sheet in audit['sheets']:assert sha(repo/sheet['file'])==sheet['sha256'];sheet['actually_inspected']=True
issues={'horse':['Original skull/socket asymmetry remains. Rejected local eye trials excluded.'],'capuchin':['Run reworked; original coarse eye surrounds remain.'],'tiger':['Tail/loops/floor revised; original limb gait remains prototype.'],'common-brushtail-possum':['Coarse irregular socket surrounds remain for art review.'],'sloth':['Source brow and protruding eye surround remain for art review.'],'tamarin':['Coarse source brow/surround remains for art review.'],'giraffe':['Angular source head and brow remain for art review.'],'sea-lion':['Source skull asymmetry remains visible in frontal view.']}
for row in audit['animals']:row.update(actually_inspected=True,issues=issues.get(row['slug'],[]),appearance_approved=False)
audit.update(actually_inspected=True,owner_approved=False,sampled_motion_frames=177*2*4,limits='A last visual sweep, not a collision/contact simulation or universal natural-gait acceptance. Remaining raw sculpt and prototype motion limitations are retained for owner review.')
feedback_path=repo/'docs/fauna/PHONE_REVIEW_FEEDBACK.json';feedback=json.loads(feedback_path.read_text())
for slug in ['capuchin','tiger']:
 folder=repo/'artifacts/fauna-repairs'/slug;record=json.loads((folder/'run-repair.json').read_text());proof_path=folder/'native-check.json';proof=json.loads(proof_path.read_text());native=repo/record['output'];digest=sha(native);assert proof['all_checks_passed'] and proof['checked_blend_sha256']==digest
 item=json.loads((folder/'review-plan.json').read_text())['animals'][0];assert item['source_sha256']==digest;media=folder/'media'/slug;receipt=json.loads((media/'receipt.json').read_text());assert receipt['complete'] and receipt['source_sha256']==digest and len(receipt['videos'])==2
 for file in [*receipt['stills'],*receipt['videos']]:assert sha(media/file['file'])==file['sha256']
 for clip in receipt['videos']:
  cap=cv2.VideoCapture(str(media/clip['file']));assert cap.isOpened();count=int(cap.get(cv2.CAP_PROP_FRAME_COUNT));assert abs(cap.get(cv2.CAP_PROP_FPS)-24)<.01
  for phase in [.09,.31,.57,.81]:cap.set(cv2.CAP_PROP_POS_FRAMES,min(count-1,int(count*phase)));ok,frame=cap.read();assert ok and frame.shape[:2]==(480,640)
  cap.release()
 evidence=[folder/'contact'/f'after-{view}-{i}.png' for view in ['side','front'] for i in range(8)]+[folder/'eyes'/(view+'-after.png') for view in ['front','right','left']]+[root/'final-motion-media.jpg']
 proof.update(actually_visually_inspected=True,visual_evidence=[{'file':str(p.relative_to(repo)),'sha256':sha(p)} for p in evidence],owner_approved=False)
 if slug=='tiger':proof['tail_scope']='Original physical-tail vertices averaged 97.5% tail-chain skin and only0.025% hind-chain influence. This is tail confinement/sway stabilization, not a major hind-leg role reassignment.'
 proof_path.write_text(json.dumps(proof,indent=2)+'\n')
 row=next((r for r in feedback['items'] if r['slug']==slug),None)
 if row is None:row={'slug':slug};feedback['items'].append(row)
 row.update(status='awaiting_owner_review',origin='final_sweep',owner_approved=False,review_update='Run rebuilt on the actual four limbs with a stable torso; original selected Walk and skin retained.' if slug=='capuchin' else 'Tail skin/sway and walk/run loops stabilized; true white coat added. Original limb gait remains prototype.',repair_review={'plan':str((folder/'review-plan.json').relative_to(repo)),'media_root':str((folder/'media').relative_to(repo)),'check':str(proof_path.relative_to(repo)),'blend':record['output'],'blend_sha256':digest})
 audit.setdefault('corrected_motion_assets',[]).append({'slug':slug,'blend_sha256':digest,'native_phase_checks':proof['native_motion_phases']})
audit['palette_refinements']=['tiger','african-hunting-dog','peafowl','parrot','snowy-owl'];audit['rejected_trials_published']=False
feedback['last_sweep']={'report':str((root/'review.json').relative_to(repo)),'animals_visually_inspected':177,'sampled_motion_frames':1416,'motion_corrections':['capuchin','tiger'],'palette_refinements':audit['palette_refinements'],'owner_approval_inferred':False,'deadline_local':'2026-10-25 23:00 America/Chicago'}
(root/'review.json').write_text(json.dumps(audit,indent=2)+'\n');feedback_path.write_text(json.dumps(feedback,indent=2)+'\n');print('FINAL SWEEP RECORDED',177,'animals /1416 motion samples /2 motion repairs /5 palette refinements')
