"""Publish only specifically inspected visual revisions into the phone queue.

This records agent reference comparison, never owner appearance approval. Old
selections/Done/notes remain. It does not perform or claim animation repairs.
"""
import argparse,hashlib,json
from pathlib import Path
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('slugs',nargs='+');ap.add_argument('--confirm-inspected',action='store_true');ap.add_argument('--root-base',type=Path);ap.add_argument('--critic-report',type=Path);a=ap.parse_args();assert a.confirm_inspected
base=(a.root_base or repo/'artifacts/fauna-visual-corrections').resolve();assert base.is_relative_to(repo)
critic_path=a.critic_report.resolve() if a.critic_report else None
if base!=repo/'artifacts/fauna-visual-corrections':assert critic_path and critic_path.is_relative_to(repo),'New anatomy publication requires a source-bound independent critic gate'
critic=json.loads(critic_path.read_text()) if critic_path else None
feedback_path=repo/'docs/fauna/PHONE_REVIEW_FEEDBACK.json';feedback=json.loads(feedback_path.read_text());index_path=repo/'docs/fauna/VISUAL_CORRECTIONS.json';index=json.loads(index_path.read_text())
references={
 'red-fox':'https://www.nationalgeographic.com.es/mundo-animal/la-pupila-un-instrumento-optico-de-precision_19363',
 'wolf':'https://www.wildlifesciencecenter.org/gray-wolf',
 'raccoon':'https://rachelmankowitz.com/2014/04/19/the-dancing-raccoon/',
}
for slug in a.slugs:
    root=base/slug;native=root/'Animal - Painted Species Review.blend';digest=hashlib.sha256(native.read_bytes()).hexdigest();gate=None
    if critic:
        gate=next(row for row in critic['animals'] if row['slug']==slug);assert gate['native_sha256']==digest and gate['verdict'] in {'no_major_visible_faults_at_low_poly_prototype_standard','pass_no_major_visible_faults_at_low_poly_prototype_standard'} and not gate['blockers'],'Independent appearance gate is stale or still has blockers'
        assert len(gate['reference_photos'])>=2
        for record in [*gate['evidence'],*gate['reference_photos']]:
            assert record['actually_viewed'] and hashlib.sha256((repo/record['path']).read_bytes()).hexdigest()==record['sha256'],'Changed/unviewed critic evidence'
        references[slug]=[r.get('url') or r['path'] for r in gate['reference_photos']]
    else:assert slug in references,'Add an actually inspected animal-specific reference first'
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
    visual_evidence=[{'file':r['path'],'sha256':r['sha256']} for r in gate['evidence']] if gate else [{'file':str(p.relative_to(repo)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in files]
    proof.update(actually_visually_inspected=True,appearance_approved=False,reference=references[slug],visual_evidence=visual_evidence,visual_scope='Front/profile heads, all coat examples and all distinct attachment sets compared to actual photographs and independently critiqued.' if gate else 'Front and both profile faces, all head coat variants and selected body views compared. Face improvement for owner review; original source-body limitations and bad animations retained.',limits='Low-poly prototype appearance for owner review; existing bad animation retained. No owner approval, real sex/age body implementation or full game-integration claim.')
    if gate:proof['independent_critic']={'file':str(critic_path.relative_to(repo)),'sha256':hashlib.sha256(critic_path.read_bytes()).hexdigest(),'native_sha256':digest,'verdict':gate['verdict'],'secondary_polish':gate['secondary_polish']}
    proof_path.write_text(json.dumps(proof,indent=2)+'\n')
    item=next((r for r in feedback['items'] if r['slug']==slug),None)
    if item is None:item={'slug':slug};feedback['items'].append(item)
    item.update(status='awaiting_owner_review',owner_approved=False,review_update='Appearance rebuilt against actual front/profile photos; all coats and attachment examples independently checked at prototype quality. Existing selected body and animations retained; awaiting your review.' if gate else 'Face revised against real animal references: fitted eyes/pupils, lids, ear fur, mask and nasal paint. Existing selected body and animations retained; appearance remains for review.',repair_review={'plan':str((root/'phone-review-plan.json').relative_to(repo)),'media_root':str((root.parent/'phone-media').relative_to(repo)),'check':str(proof_path.relative_to(repo)),'blend':str(native.relative_to(repo)),'blend_sha256':digest})
    row=next(r for r in index['animals'] if r['slug']==slug);row.update(status='independently_checked_static_appearance_ready_for_owner_review' if gate else 'improved_face_ready_for_review',reference_comparison_completed=True,appearance_approved=False,source_sha256=digest,reference=references[slug],body_and_pattern_review='All coat and distinct attachment examples reviewed at prototype standard; secondary limitations in exact-SHA critic gate.' if gate else 'Remaining general body/pattern review not certified by this face pass.')
    if gate:row['independent_critic']=proof['independent_critic']
feedback_path.write_text(json.dumps(feedback,indent=2)+'\n');index_path.write_text(json.dumps(index,indent=2)+'\n');print('Inspected face revisions recorded:',', '.join(a.slugs))
