"""Publish an actually inspected revision for explicit owner re-review.

Keep the prior raw review intact. Exact file identity must agree across native
controls, multi-frame renders and deformation diagnostics where available.
"""
import argparse,datetime,hashlib,json
from pathlib import Path
repo=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser();p.add_argument('--slugs',nargs='+',required=True);p.add_argument('--inspection',required=True)
a=p.parse_args();choices_path=repo/'docs/fauna/OWNER_SELECTIONS.json';choices=json.loads(choices_path.read_text())
for slug in a.slugs:
    root=repo/'artifacts/fauna-overnight-20261024'/slug;path=root/'Animal - Motion Revision.blend';digest=hashlib.sha256(path.read_bytes()).hexdigest()
    native=json.loads((root/'motion-native-checks.json').read_text());render=json.loads((root/'owner-motion-render-evidence.json').read_text())
    assert digest==native['checked_blend_sha256']==render['checked_blend_sha256'] and native['controls'] and len(native['candidates'])==4
    for filename in ('rodent-deformation-checks.json','snake-deformation-checks.json'):
        proof=root/filename
        if proof.exists():assert json.loads(proof.read_text())['checked_blend_sha256']==digest
    for row in render['images']:
        assert hashlib.sha256((root/row['image']).read_bytes()).hexdigest()==row['sha256']
    ready={'output':path.name,'checked_blend_sha256':digest,'native_evidence':'motion-native-checks.json','candidates':len(native['candidates']),'visual_inspection_completed':True,'inspection':a.inspection,'time':datetime.datetime.now().astimezone().isoformat(),'owner_approval_pending':True,'natural_gait_approved':False,'collision_free_claim':False}
    (root/'owner-motion-review-ready.json').write_text(json.dumps(ready,indent=2)+'\n')
    render.update(inspection_pending=False,inspection=a.inspection);(root/'owner-motion-render-evidence.json').write_text(json.dumps(render,indent=2)+'\n')
    choices['pending_revisions'][slug].update(status='motion_revision_ready_owner_review_pending',explicit_repeat_review=True,prepared=ready['time'],art_approved=False)
    print('Inspected motion review ready',slug)
choices_path.write_text(json.dumps(choices,indent=2)+'\n')
