"""Publish checked selected corrections without an unsolicited repeat review."""
import argparse,datetime,hashlib,json
from pathlib import Path
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slugs',nargs='+',required=True);ap.add_argument('--inspection',required=True)
a=ap.parse_args();p=repo/'docs/fauna/OWNER_SELECTIONS.json';choices=json.loads(p.read_text())
for slug in a.slugs:
    root=repo/'artifacts/fauna-overnight-20261024'/slug;out=root/'Animal - Owner Corrected.blend'
    evidence=json.loads((root/'owner-fix.json').read_text());checks=json.loads((root/'owner-fix-native-checks.json').read_text())
    sha=hashlib.sha256(out.read_bytes()).hexdigest();assert sha==evidence['output_sha256']==checks['checked_blend_sha256'] and checks['controls']
    source=root/'Animal - Raw Audition.blend';assert hashlib.sha256(source.read_bytes()).hexdigest()==evidence['source_sha256']
    assert choices['animals'][slug]['selected_models']==[evidence['candidate']]
    revision=choices['pending_revisions'][slug];assert not revision.get('explicit_repeat_review'),'Explicitly requested review must not be bypassed'
    evidence.update(native_checks_pending=False,visual_checks_completed=True,visual_inspection=a.inspection)
    (root/'owner-fix.json').write_text(json.dumps(evidence,indent=2)+'\n')
    choices['animals'][slug].update(manual_fix_blend=str(out.relative_to(repo)),manual_fix_blend_sha256=sha,manual_fix_status='completed_best_effort_no_repeat_review')
    revision.update(status='manual_revision_completed_no_repeat_review',completed=datetime.datetime.now().astimezone().isoformat(),art_approved=False)
    print('Published selected correction',slug,evidence['candidate'])
temporary=p.with_suffix('.owner-update.tmp');temporary.write_text(json.dumps(choices,indent=2)+'\n');temporary.replace(p)
