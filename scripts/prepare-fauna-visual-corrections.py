"""Isolated appearance revisions from current live sources; never choose bodies.

Only prepare the requested animals. Old phone assets and Done/notes are untouched.
"""
import argparse, hashlib, json, shutil
from pathlib import Path

repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('slugs',nargs='+');a=ap.parse_args()
feedback=json.loads((repo/'docs/fauna/PHONE_REVIEW_FEEDBACK.json').read_text())
overrides={i['slug']:i['repair_review']['blend'] for i in feedback['items'] if i.get('repair_review')}
base=repo/'artifacts/fauna-visual-corrections'
for slug in a.slugs:
    src=repo/'artifacts/fauna-review-revisions'/slug
    dest=base/slug
    assert src.is_dir(),slug
    dest.mkdir(parents=True,exist_ok=True)
    assert not (dest/'visual-source.json').exists(),'Already prepared: '+slug
    for path in src.iterdir():
        if path.is_file() and path.suffix in {'.json','.npz','.png'}:
            shutil.copy2(path,dest/path.name)
    shutil.copytree(src/'eye-audit',dest/'eye-audit',dirs_exist_ok=True)
    native=repo/overrides.get(slug,str((src/'Animal - Painted Species Review.blend').relative_to(repo)))
    shutil.copy2(native,dest/'Animal - Painted Species Review.blend')
    source={'slug':slug,'source':str(native.relative_to(repo)),'source_sha256':hashlib.sha256(native.read_bytes()).hexdigest(),'scope':'Appearance only; all existing actions, rest bones, skin and chosen body retained. Prior appearance judgments withdrawn.','appearance_approved':False}
    (dest/'visual-source.json').write_text(json.dumps(source,indent=2)+'\n')
    (dest/'revision-source.json').write_text(json.dumps(source,indent=2)+'\n')
    print('PREPARED',slug)
