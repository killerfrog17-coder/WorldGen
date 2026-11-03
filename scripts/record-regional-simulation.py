"""Record current regional history and actual interactive acceptance; no legacy replay."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source', required=True)
parser.add_argument('--player', default='artifacts/regional-sim-20261103/stream-budget-player')
parser.add_argument('--batch', default='artifacts/regional-sim-20261103/events-scale')
parser.add_argument('--slot', choices=['slot-a', 'slot-b'], default='slot-a')
args = parser.parse_args()

def read(path):
    raw = path.read_bytes()
    return raw.decode('utf-16' if raw[:2] in (b'\xff\xfe', b'\xfe\xff') else 'utf-8-sig')

def load(path):
    return json.loads(read(path))

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

full = load(root / args.batch / 'result.json')
assert full['cells'] == 200000 and full['years'] == 190000 and full['checks'] >= 20
assert full['aggregates'] <= full['regions'] and full['families'] <= full['regions'] * 2
assert full['lateFineClock1000Ms'] < 1000
folder = root / args.player
player = load(folder / 'result.json')
assert int(read(folder / 'exit-code.txt')) == 0
assert player['Cells'] == 200000 and player['Checks'] >= 8 and player['RuntimeErrors'] == 0
assert player['FineDays'] / player['FineSeconds'] >= .8
assert player['LateFineDays'] / player['LateFineSeconds'] >= .8
assert player['FastYearsPerSecond'] >= 10000
assert player['MaximumLayerSwitchMs'] < 200 and player['MaximumPublicationPauseMs'] < 250
stamp = load(root / 'builds' / args.slot / 'build-inputs.json')
assert not stamp['Development'] and stamp['Backend'] == 'Mono'
for assembly in stamp['Inputs']:
    packaged = root / 'builds' / args.slot / 'WorldGen_Data/Managed' / (assembly['Assembly'] + '.dll')
    assert sha(packaged) == assembly['Sha256'], assembly['Assembly']
worker = root / 'builds' / args.slot / 'TerrainWorker/WorldGen.Cli.exe'
assert sha(worker) == stamp['TerrainWorkerSha256']
manifest = dict(
    recorded_utc=datetime.now(timezone.utc).isoformat(), source=args.source,
    status='Current-version interactive performance review; owner playtesting pending',
    rules='foragers-regional-3', selected_slot='builds/' + args.slot, build=stamp,
    architecture=dict(
        population='One aggregate per connected ecoregion; no global tribe ledger',
        event_windows_years=dict(population=[200,400], technology=[150,350], migration=[200,600], language=[300,900], tradition=[200,800], ecology=[700,1700], culture=[300,900]),
        events='Independent seeded random dates, direct regional lookup, local neighbours, persistent O(log E) queue',
        affiliation_precision='1% regional shares; finer local differences are procedural, not traces of every historical migrant',
        publication='Copy-on-write records; lazy full hash only when requested/saving',
        executor='Same Core simulation in shipped CoreCLR worker; committed regional binary deltas returned to managed player',
        streaming='Bounded calculation batches; completed update decoding overlaps the next calculation; pause stops at a committed boundary',
        display='60Hz map redraw budget during fast simulation; gameplay display preferences restored afterward; Paleolithic Auto target20000 years/second',
        global_work='World generation/save/load, visible regional map summaries and rare bounded family-registry compaction scale with data, not ordinary entity handlers',
        maps='Cell-to-region GPU lookup built once; small regional palettes per layer',
        ecology='Broad climate/resource/forest state; local species derived from native realm, dated climate and resource stocks',
        local_detail='Deterministic camps, residents and itineraries generated on demand'),
    current_200k_190k=full, actual_player=player,
    images={p.name:sha(p) for p in folder.glob('*.png')},
    acceptance=dict(minimum_actual_years_per_second=10000, maximum_layer_switch_ms=200, maximum_publication_gap_ms=250),
    excluded_failures=[
        'Regional-1 synchronized reviews and416 years/second were rejected.',
        'Initial regional-2 player hit its playback-rate cap before producing a valid speed result.',
        'Local Mono executor remained roughly1000 years/second despite fast maps.',
        'First CoreCLR transport used tiny unbuffered reads/writes; failed speed and publication-latency gates.',
        'Sequential and uncapped-rendering attempts up to9652 years/second failed the10000 speed gate; only the final streamed/render-budget player is accepted.'
    ],
    limits=[
        'Measurements are this machine and fixture, not a universal throughput guarantee.',
        'Regional rates remain approximate game priors; a performance repair is not complete historical calibration.',
        'Local species suitability is derived, not an individually tracked global animal migration history.',
        'Physical NPC camp integration remains separate; local candidates require terrain validation.',
        'Old simulation saves are unsupported. Generate a new world.'
    ])
output = root / 'assets/verification/regional-simulation-20261103.json'
output.write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print(output)
