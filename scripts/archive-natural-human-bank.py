"""Record reproducible selected takes and constraints; no model/texture edits."""
import hashlib,json
from pathlib import Path

REPO=Path(__file__).resolve().parent.parent
ROOT=REPO/'artifacts/human-natural-20261031';BANK=REPO/'assets/human-motion-kimodo'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
bank=json.loads((BANK/'bank.json').read_text());old=json.loads((BANK/'provenance.json').read_text())
paths=sorted({clip['source'].replace('\\','/') for person in bank['people'] for clip in person['clips']})
sources=[];tracked=[]
for source in paths:
 folder=REPO/source;files={name:sha(folder/name) for name in ['generation.json','root_positions.f32','local_rotations_xyzw.f32']}
 sources.append({'path':source,'generation':json.loads((folder/'generation.json').read_text()),'files':files})
 tracked.extend((folder/name).relative_to(REPO).as_posix() for name in files)
 marker=folder.parent/'selected.json'
 if marker.exists():tracked.append(marker.relative_to(REPO).as_posix())
old.update({'baked_roles_per_character':[len(person['clips']) for person in bank['people']],
 'selected_raw_sources':sources,'source_character_sha256':bank['source_sha256'],
 'natural_revision':{'task':'human-natural-20261031','shared_first_third_person':True,'runtime_ai':False,
 'authoring':['Retain approved locomotion where suitable; restrained replacement action auditions.',
 'Ordinary upper-body actions preserve grounded locomotion legs; no imported action squat.',
 'Restrained local torso/clavicle rotations relative to relaxed native idle; original skin weights untouched.'],
 'runtime_fitting':['Relaxed body-relative carry independent of view pitch; explicit actions aim.',
 'Real prop dimensions, measured character surface bounds, anatomical arm reach and two-grip contacts.',
 'Bow whole-body side-on stance, cheek anchor, draw/hold/cancel/release/lower sequence.',
 'Separate one/two-hand armed swimming poses; free limb remains available for propulsion.'],
 'review_limits':'Automated screening and mechanical audits do not constitute owner visual acceptance.'}})
(BANK/'provenance.json').write_text(json.dumps(old,indent=2))
tracked.extend([f'artifacts/human-natural-20261031/{name}' for name in ['skeleton.json','model-provenance.json','authoring-decisions.json','session.json']])
(ROOT/'selected-source-files.txt').write_text('\n'.join(sorted(set(tracked)))+'\n')
print('ARCHIVED',len(sources),'selected raw sources;',len(tracked),'reproducibility inputs')
