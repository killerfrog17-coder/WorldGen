"""Copy a one-candidate manual native build into its checked correction slot."""
import argparse,hashlib,json,shutil
from pathlib import Path
repo=Path(__file__).resolve().parent.parent;p=argparse.ArgumentParser();p.add_argument('--slug',required=True,choices=['iguana','monitor-lizard','whale']);a=p.parse_args()
root=repo/'artifacts/fauna-overnight-20261024'/a.slug;work=repo/'artifacts/fauna-owner-fixes-20261025'/a.slug
choice=json.loads((repo/'docs/fauna/OWNER_SELECTIONS.json').read_text())['animals'][a.slug];number,=choice['selected_models']
out=root/'Animal - Owner Corrected.blend';assert not out.exists()
shutil.copy2(work/'Animal - Raw Audition.blend',out)
proof={'slug':a.slug,'candidate':number,'review_model_number':1,'source_sha256':hashlib.sha256((root/'Animal - Raw Audition.blend').read_bytes()).hexdigest(),'output':str(out.relative_to(repo)),'output_sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'geometry_unchanged':True,'manual_skeleton':a.slug!='whale','owner_requests_no_repeat_review':a.slug=='whale','natural_motion_approved':False,'native_checks_pending':True,'manual_build_root':str(work.relative_to(repo)),'notes':json.loads((work/'candidate-1/generated-rig.json').read_text())}
(root/'owner-fix.json').write_text(json.dumps(proof,indent=2)+'\n');print('Staged manual correction',a.slug,number,'as single selected review')
