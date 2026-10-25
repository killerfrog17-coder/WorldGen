"""Record actual agent screening of hash-matching new four-candidate sets."""
import argparse,datetime,hashlib,json
from pathlib import Path
repo=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser();p.add_argument('--slugs',nargs='+',required=True);p.add_argument('--inspection',required=True);a=p.parse_args()
for slug in a.slugs:
    root=repo/'artifacts/fauna-overnight-20261024'/slug;out=root/'Animal - Raw Audition.blend';digest=hashlib.sha256(out.read_bytes()).hexdigest()
    native=json.loads((root/'native-checks.json').read_text());render=json.loads((root/'owner-alpha-render-evidence.json').read_text());request=json.loads((root/'owner-reroll-request-current.json').read_text())
    assert digest==native['checked_blend_sha256']==render['checked_blend_sha256'] and native['controls'] and len(native['candidates'])==4 and not (root/'review-rebuild.json').exists()
    render.update(inspection_pending=False,inspection=a.inspection);(root/'owner-alpha-render-evidence.json').write_text(json.dumps(render,indent=2)+'\n')
    proof={'checked_blend_sha256':digest,'request_time':request['time'],'visual_inspected':True,'inspection':a.inspection,'prepared':datetime.datetime.now().astimezone().isoformat(),'owner_model_selection_pending':True,'natural_gait_approved':False,'universal_collision_free_claim':False}
    (root/'owner-redo-readiness.json').write_text(json.dumps(proof,indent=2)+'\n');print('New four-model review screened',slug)
