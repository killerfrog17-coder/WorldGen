"""Freeze the new package inputs and prove original art/legacy assets unchanged."""
import concurrent.futures
import argparse
import hashlib
import json
from pathlib import Path
import sys

ROOT=Path(__file__).resolve().parent.parent
parser=argparse.ArgumentParser()
parser.add_argument('--player-folder',default='player-human-kimodo')
parser.add_argument('--proof-folder',default='kimodo-human-bank/package-proof')
parser.add_argument('--freeze',action='store_true')
parser.add_argument('--baseline-freeze',default='artifacts/terrain-boundaries-20261031/package-proof/source-freeze.json')
args=parser.parse_args()
TASK=ROOT/'artifacts'/args.proof_folder
PLAYER=ROOT/'artifacts'/args.player_folder
TASK.mkdir(parents=True,exist_ok=True)
def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(4*1024*1024),b''):h.update(block)
    return h.hexdigest()
old=json.loads((ROOT/args.baseline_freeze).read_text())
if args.freeze:
    paths=set(old)
    for directory in ['unity/WorldGen.Unity/Assets/WorldGen/Runtime','unity/WorldGen.Unity/Assets/WorldGen/Editor','unity/WorldGen.Unity/Assets/WorldGen/Resources']:
        paths.update(p.relative_to(ROOT).as_posix() for p in (ROOT/directory).rglob('*') if p.is_file() and p.suffix in {'.cs','.shader','.hlsl'})
    for directory in ['assets/human-motion-kimodo','unity/WorldGen.Unity/Assets/WorldGen/GeneratedHumanKimodo','unity/WorldGen.Unity/Assets/WorldGen/Resources/HumanKimodo']:
        paths.update(p.relative_to(ROOT).as_posix() for p in (ROOT/directory).rglob('*') if p.is_file())
    paths.update(p.relative_to(ROOT).as_posix() for p in (ROOT/'scripts').glob('*kimodo*.py'))
    paths.update(p.relative_to(ROOT).as_posix() for p in (ROOT/'scripts').glob('*natural*human*.py'))
    paths.update(p.relative_to(ROOT).as_posix() for p in (ROOT/'scripts').glob('natural_human*.py'))
    for person in json.loads((ROOT/'assets/human-motion-kimodo/bank.json').read_text())['people']:
        for clip in person['clips']:
            folder=Path(clip['source'].replace('\\','/'))
            paths.update((folder/name).as_posix() for name in ['generation.json','root_positions.f32','local_rotations_xyzw.f32'])
            if (ROOT/folder.parent/'selected.json').exists():paths.add((folder.parent/'selected.json').as_posix())
    (TASK/'source-freeze.json').write_text(json.dumps({p:sha(ROOT/p) for p in sorted(paths)},indent=2))
    print('FROZEN',len(paths));sys.exit(0)
frozen=json.loads((TASK/'source-freeze.json').read_text())
assert all(sha(ROOT/p)==expected for p,expected in frozen.items()),'Frozen build inputs changed'
legacy={p:h for p,h in old.items() if p.startswith(('assets/first-person/','unity/WorldGen.Unity/Assets/WorldGen/Resources/FirstPerson/'))}
assert all(sha(ROOT/p)==expected for p,expected in legacy.items()),'Original legacy arm assets changed'
lock=json.loads((ROOT/'assets/game-assets.lock.json').read_text())['files']
preserved={p:r for p,r in lock.items() if p.startswith(('artifacts/human-','unity/WorldGen.Unity/Assets/WorldGen/GeneratedHumans/','unity/WorldGen.Unity/Assets/StreamingAssets/fauna/','unity/WorldGen.Unity/Assets/StreamingAssets/fauna-collision/'))}
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
    checks=list(pool.map(lambda pair:sha(ROOT/pair[0])==pair[1]['sha256'],preserved.items()))
assert all(checks),[p for (p,_),ok in zip(preserved.items(),checks) if not ok]
inputs=json.loads((PLAYER/'build-inputs.json').read_text(encoding='utf-8-sig'))
assert inputs['Backend']=='IL2CPP' and (PLAYER/'GameAssembly.dll').exists() and not (PLAYER/'MonoBleedingEdge').exists()
assert all(sha(Path(r['Path']))==r['Sha256'] for r in inputs['Inputs'])
index=ROOT/'unity/WorldGen.Unity/Assets/StreamingAssets/fauna/assets.json'
assert sha(index)==sha(PLAYER/'WorldGen_Data/StreamingAssets/fauna/assets.json')
for family in ['fauna','fauna-collision','fauna-motion']:
    for path in (ROOT/'unity/WorldGen.Unity/Assets/StreamingAssets'/family).glob('*'):
        if path.is_file() and path.suffix!='.meta':assert sha(path)==sha(PLAYER/'WorldGen_Data/StreamingAssets'/family/path.name),(family,path.name)
for directory,count in [('assets/item-audio/provenance.json',18),('assets/player-audio/provenance.json',24)]:
    clips=json.loads((ROOT/directory).read_text())['clips'];assert len(clips)==count
    assert all(sha(ROOT/c['path'])==c['sha256'] for c in clips)
bank=json.loads((ROOT/'assets/human-motion-kimodo/bank.json').read_text())
assert len(bank['people'])==6
for person in bank['people']:
    assert len(person['clips'])>=67
    for base in [ROOT/'assets/human-motion-kimodo',ROOT/'unity/WorldGen.Unity/Assets/WorldGen/GeneratedHumanKimodo']:
        assert sha(base/person['file'])==person['fbx_sha256']
report={'backend':'IL2CPP','BuiltUtc':inputs['BuiltUtc'],'frozen_inputs':len(frozen),'legacy_files_unchanged':len(legacy),'protected_original_assets':len(preserved),'native_character_banks':6,'clips_per_character':[len(p['clips']) for p in bank['people']],'fauna_package_exact':True,'original_audio_exact':True}
(TASK/'package-proof.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
