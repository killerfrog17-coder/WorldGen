"""Record the measured generation/adoption/cell-map update after actual player checks."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess

root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source',required=True)
parser.add_argument('--slot',choices=['slot-a','slot-b'],default='slot-b')
parser.add_argument('--evidence',default='artifacts/generation-adoption-20261103')
args=parser.parse_args();evidence=root/args.evidence
def read(path):
    data=path.read_bytes();return data.decode('utf-16' if data[:2] in (b'\xff\xfe',b'\xfe\xff') else 'utf-8-sig')
def load(path):return json.loads(read(path))
def sha(path):
    with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def passed(folder):assert int(read(evidence/folder/'exit-code.txt'))==0,folder

for folder in ('accepted-generation','accepted-climate','accepted-playback'):passed(folder)
baseline=load(evidence/'baseline/result.json');generation=load(evidence/'accepted-generation/result.json')
recipe=load(evidence/'baseline/recipe.json')
assert recipe==load(evidence/'accepted-generation/recipe.json')
assert generation['Cells']==baseline['Cells']==200000
assert generation['WorldHash']==baseline['WorldHash']=='e03f62240e5402c7a77883c87f30d81ce2c7f3217408229ac9109f723473fc1b'
assert 'PASS all 15 stage hashes' in read(evidence/'stage-parity.log')
assert 'PASS 12' in read(evidence/'accepted-climate/checks.txt')
climate=read(evidence/'accepted-climate/gpu-reference.txt').splitlines()
assert len(climate)==10 and all('mismatched=0 ' in row for row in climate)
player=load(evidence/'accepted-playback/result.json')
assert player['RuntimeErrors']==0 and player['Cells']==200000
assert player['FastYearsPerSecond']>=10000
assert player['MaximumPublicationPauseMs']<250 and player['MaximumLayerSwitchMs']<200
assert player['FineDays']/player['FineSeconds']>=.8 and player['LateFineDays']/player['LateFineSeconds']>=.8
batch=next(json.loads(row) for row in read(evidence/'traits-final.log').splitlines() if row.startswith('{"checks"'))
assert batch['cells']==200000 and batch['years']==100000 and batch['checks']>=23
semantic=load(evidence/'core-final/result.json')
assert semantic['checks']>=200 and .25<semantic['knowledgeFraction']<.75
stamp=load(root/'builds'/args.slot/'build-inputs.json')
assert stamp['Backend']=='Mono' and not stamp['Development']
for assembly in stamp['Inputs']:
    assert sha(root/'builds'/args.slot/'WorldGen_Data/Managed'/(assembly['Assembly']+'.dll'))==assembly['Sha256']
    if assembly['Assembly'].startswith('WorldGen.'):
        assert sha(root/'builds'/args.slot/'TerrainWorker'/(assembly['Assembly']+'.dll'))==assembly['Sha256']
assert sha(root/'builds'/args.slot/'TerrainWorker/WorldGen.Cli.exe')==stamp['TerrainWorkerSha256']
unchanged=subprocess.check_output(['git','diff','--name-only','56eb41d007a263d4fd3bb67b0bc20b8c0895adf8',args.source,'--','assets-source','sounds','music'],cwd=root,text=True).strip()
assert not unchanged,unchanged
manifest=dict(recorded_utc=datetime.now(timezone.utc).isoformat(),source=args.source,
    rules='foragers-regional-4',selected_slot='builds/'+args.slot,build=stamp,
    runtime_assembly_sha256=sha(root/'builds'/args.slot/'WorldGen_Data/Managed/Assembly-CSharp.dll'),
    worker_assemblies={p.name:sha(p) for p in (root/'builds'/args.slot/'TerrainWorker').glob('WorldGen.*.dll')},
    resolved_recipe=recipe,generation_baseline=baseline,generation_selected=generation,
    generation_ready_speedup=baseline['ReadyMs']/generation['ReadyMs'],
    timing_context='Individual runs on this machine. Owner player was open during baseline, closed during selected measurement; not a controlled whole-game FPS benchmark.',
    exact_stage_parity=read(evidence/'stage-parity.log').strip(),
    semantics=semantic,current_200k_100k=batch,actual_player=player,gpu_reference=climate,
    adoption=dict(unit='Approximate fraction of tribes',dimensions=['knowledge','practical access','routine adoption'],
        evaluation='Finite dated curves, queried on demand. Completed curves/projections use terminal cached values.',
        opportunity_review_years=[600,1800],local_detail='Stable seeded tribal coverage, prerequisites and individual craft specialists'),
    maps=dict(mesh='One cell mesh for all layers; smoothed natural surface disabled',
        dated_background='Cached per-cell GPU climate/satellite/seasonal temperature/rain palettes; temporal interpolation between250-year classification endpoints',
        civilizations='Default; other human species light grey, sapiens medium grey; later-era colours reserved',
        ice_indicator='Actual glacial forcing, beside year'),
    original_art_audio='No tracked assets-source, sounds or music changes versus previous selected source',
    inspected_images={p.name:sha(p) for p in (evidence/'accepted-climate').glob('*.png')},
    excluded=['First climate attempt caught a remaining Cultures load default; corrected.',
        'Hidden-window screen captures were black and are excluded; actual camera render requests produce the inspected map images.',
        'An intermediate curve-sharing optimization failed future replay; structural equality corrected it before acceptance.',
        'A first compact-wire run had221ms gaps but9909years/sec; excluded. Contact lookup no longer projects unused neighbour technology. The200k10k history hash is unchanged.',
        'Pre-compaction player reached10053years/sec but exceeded the250ms publication gate (254.48ms); excluded. Curve packets now use lossless flags and inline scalar reads.',
        read(evidence/'wire-bits.log').strip()],
    limits=['Spread/review durations are approximate game priors, not exact archaeological dates.',
        'Other ecological diagnostic backgrounds still use regional summaries.',
        'No physical moving shorelines/ice or live vegetation reconstruction was added.',
        'Old simulation saves are unsupported; generate a fresh world.',
        'Offscreen camera renders verify maps; final UI appearance remains subject to owner review.'])
output=root/'assets/verification/generation-adoption-20261103.json'
output.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8');print(output)
