"""Publish only specifically inspected visual revisions into the phone queue.

This records agent reference comparison, never owner appearance approval. Old
selections/Done/notes remain. It does not perform or claim animation repairs.
"""
import argparse,hashlib,json
from pathlib import Path
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('slugs',nargs='+');ap.add_argument('--confirm-inspected',action='store_true');a=ap.parse_args();assert a.confirm_inspected
feedback_path=repo/'docs/fauna/PHONE_REVIEW_FEEDBACK.json';feedback=json.loads(feedback_path.read_text());index_path=repo/'docs/fauna/VISUAL_CORRECTIONS.json';index=json.loads(index_path.read_text())
references={
 'red-fox':'https://www.nationalgeographic.com.es/mundo-animal/la-pupila-un-instrumento-optico-de-precision_19363',
 'wolf':'https://www.wildlifesciencecenter.org/gray-wolf',
 'raccoon':'https://rachelmankowitz.com/2014/04/19/the-dancing-raccoon/',
}
for slug in a.slugs:
    assert slug in references,'Add an actually inspected animal-specific reference first'
    root=repo/'artifacts/fauna-visual-corrections'/slug;native=root/'Animal - Painted Species Review.blend';digest=hashlib.sha256(native.read_bytes()).hexdigest()
    proof_path=root/'visual-native-check.json';proof=json.loads(proof_path.read_text());assert proof['all_checks_passed'] and proof['checked_blend_sha256']==digest
    media=json.loads((root.parent/'phone-media'/slug/'receipt.json').read_text());assert media['complete'] and media['source_sha256']==digest
    for record in [*media['stills'],*media['videos']]:assert hashlib.sha256((root.parent/'phone-media'/slug/record['file']).read_bytes()).hexdigest()==record['sha256']
    assert len(media['videos'])==2
    coat_report=json.loads((root/'eye-audit/coat-renders.json').read_text());assert coat_report['source_sha256']==digest
    after=json.loads((root/'eye-audit/after-renders.json').read_text());assert after['source_sha256']==digest
    for record in after['views']:
        assert hashlib.sha256((repo/record['file']).read_bytes()).hexdigest()==record['sha256']
        assert hashlib.sha256((root/'eye-audit'/f"{record['view']}.json").read_bytes()).hexdigest()==record['camera_guide_sha256']
    files=[root/'eye-audit'/f'{v}-after.png' for v in ['front','right','left']]
    files += [root/'eye-audit'/f'coat-{i:02d}-front.png' for i in range(len(coat_report['coats']))]
    proof.update(actually_visually_inspected=True,appearance_approved=False,reference=references[slug],visual_evidence=[{'file':str(p.relative_to(repo)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in files],visual_scope='Front and both profile faces, all head coat variants and selected body views compared. Face improvement for owner review; original source-body limitations and bad animations retained.',limits='Selected skull remains coarse. Single front paint projection can stretch fine fibres at oblique angles and contains residual baked shading. No full animal/game-ready approval or gait claim.')
    proof_path.write_text(json.dumps(proof,indent=2)+'\n')
    item=next((r for r in feedback['items'] if r['slug']==slug),None)
    if item is None:item={'slug':slug};feedback['items'].append(item)
    item.update(status='awaiting_owner_review',owner_approved=False,review_update='Face revised against real animal references: fitted eyes/pupils, lids, ear fur, mask and nasal paint. Existing selected body and animations retained; appearance remains for review.',repair_review={'plan':str((root/'phone-review-plan.json').relative_to(repo)),'media_root':str((root.parent/'phone-media').relative_to(repo)),'check':str(proof_path.relative_to(repo)),'blend':str(native.relative_to(repo)),'blend_sha256':digest})
    row=next(r for r in index['animals'] if r['slug']==slug);row.update(status='improved_face_ready_for_review',reference_comparison_completed=True,appearance_approved=False,source_sha256=digest,reference=references[slug],body_and_pattern_review='Remaining general body/pattern review not certified by this face pass.')
feedback_path.write_text(json.dumps(feedback,indent=2)+'\n');index_path.write_text(json.dumps(index,indent=2)+'\n');print('Inspected face revisions recorded:',', '.join(a.slugs))
