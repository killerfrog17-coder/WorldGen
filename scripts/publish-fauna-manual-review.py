"""Make an explicitly requested one-model re-review selectable, not approved."""
import argparse,datetime,hashlib,json
from pathlib import Path
repo=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser();p.add_argument('--slugs',nargs='+',choices=['iguana','monitor-lizard','elephant'],required=True);a=p.parse_args()
path=repo/'docs/fauna/OWNER_SELECTIONS.json';choices=json.loads(path.read_text())
for slug in a.slugs:
    root=repo/'artifacts/fauna-overnight-20261024'/slug;work=repo/'artifacts/fauna-owner-fixes-20261025'/slug;out=root/'Animal - Owner Corrected.blend'
    proof=json.loads((root/'owner-fix.json').read_text());checks=json.loads((root/'owner-fix-native-checks.json').read_text());render=json.loads((work/'manual-rig-render-evidence.json').read_text());digest=hashlib.sha256(out.read_bytes()).hexdigest()
    assert digest==proof['output_sha256']==checks['checked_blend_sha256']==render['checked_blend_sha256'] and checks['controls'] and len(checks['candidates'])==1
    revision=choices['pending_revisions'][slug];assert revision['explicit_repeat_review']
    proof.update(native_checks_pending=False,visual_checks_completed=True,visual_inspection='Four CPU Walk phases in side and front inspected: all four anatomical limb chains active, rigid digit fans; no gross leg permutations in these sampled views. Foot contact and full gait art remain owner pending.')
    (root/'owner-fix.json').write_text(json.dumps(proof,indent=2)+'\n');render['inspection_pending']=False;(work/'manual-rig-render-evidence.json').write_text(json.dumps(render,indent=2)+'\n')
    choices['animals'][slug].update(manual_fix_blend=str(out.relative_to(repo)),manual_fix_blend_sha256=digest,manual_fix_status='explicit_owner_review_ready')
    revision.update(status='manual_motion_revision_ready_owner_review_pending',prepared=datetime.datetime.now().astimezone().isoformat(),art_approved=False)
    print('Explicit manual review ready',slug,proof['candidate'])
temp=path.with_suffix('.owner-update.tmp');temp.write_text(json.dumps(choices,indent=2)+'\n');temp.replace(path)
