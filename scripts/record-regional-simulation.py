"""Record current regional simulation and real-player speed checks, no legacy replay."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source', required=True)
args = parser.parse_args()
evidence = root / 'artifacts/regional-sim-20261103'

def read(path):
    raw = path.read_bytes()
    return raw.decode('utf-16' if raw[:2] in (b'\xff\xfe', b'\xfe\xff') else 'utf-8-sig')

def load(path):
    return json.loads(read(path))

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

full = load(evidence / 'actual-scale/result.json')
short = load(evidence / 'actual-scale-10k.json')
earth = load(evidence / 'earth/result.json')
assert full['cells'] == short['cells'] == 200000
assert full['years'] == 190000 and short['years'] == earth['years'] == 10000
assert full['checks'] == 14 and short['checks'] == earth['checks'] == 15
assert full['aggregates'] <= full['regions'] and full['families'] <= full['regions'] * 2
assert full['historicalSeconds'] < 180 and full['lateFineClock1000Ms'] < 1000
player = load(evidence / 'player/result.json')
assert int(read(evidence / 'player/exit-code.txt')) == 0
assert player['Cells'] == 200000 and player['Checks'] == 8 and player['RuntimeErrors'] == 0
assert player['FineDays'] / player['FineSeconds'] >= .8 and player['FastYearsPerSecond'] >= 100
game = evidence / 'gameplay-earth'
assert int(read(game / 'exit-code.txt')) == 0
checks = read(game / 'checks.txt').splitlines()
assert len(checks) == 32 and all(x.startswith('PASS ') for x in checks)
assert 'Exception:' not in read(game / 'player.log')
stamp = load(root / 'builds/slot-b/build-inputs.json')
assert not stamp['Development'] and stamp['Backend'] == 'Mono'
for assembly in stamp['Inputs']:
    packaged = root / 'builds/slot-b/WorldGen_Data/Managed' / (assembly['Assembly'] + '.dll')
    assert sha(packaged) == assembly['Sha256'], assembly['Assembly']
worker = root / 'builds/slot-b/TerrainWorker/WorldGen.Cli.exe'
assert sha(worker) == stamp['TerrainWorkerSha256']
manifest = dict(
    recorded_utc=datetime.now(timezone.utc).isoformat(), source=args.source,
    status='Selected current-version review; owner playtesting pending',
    rules='foragers-regional-1', selected_slot='builds/slot-b', build=stamp,
    architecture=dict(population='One aggregate per connected ecoregion',
        population_review_years=100, culture_language_review_years=500, ecology_review_years=1000,
        local_detail='Deterministic camps, residents and itineraries generated from regional distributions',
        fine_clock='Shared frozen regional domain; small clock digest, no full-state copy or hash',
        history_bounds='At most one population/cultural profile per region, two family summaries per region, one discovery per technique/region'),
    current_200k_190k=full, current_200k_10k=short, current_earth_10k=earth,
    actual_player=player, actual_gameplay=dict(checks=checks, initial_terrain=read(game / 'initial-terrain.txt')),
    images={p.name:sha(p) for p in (evidence / 'player').glob('*.png')},
    excluded_failures=[
        'First small-world publication-partition check exposed arrival-date publication dependence; corrected before final tests.',
        '200k procedural-world nature fixture entered terrain but failed its fixed-location deer spawn; excluded. Known Earth-location interaction fixture passed all32 checks.'
    ],
    limits=[
        '62-second full history is a CoreCLR batch measurement; packaged interactive playback was416 years/second, not the same speed.',
        'Regional rates remain approximate game priors; this performance repair does not establish complete historical calibration.',
        'Physical NPC camp integration remains separate; local candidates still require terrain validation.',
        'Old detailed-ledger saves are unsupported. Generate a new world.'
    ])
output = root / 'assets/verification/regional-simulation-20261103.json'
output.write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print(output)
