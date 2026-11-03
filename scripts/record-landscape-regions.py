"""Record the actual source-matched landscape acceptance package.

This records completed checks, not a claim that their screenshots look good.
Visual findings must be supplied separately after inspecting the captures.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import statistics
import subprocess

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source', required=True)
parser.add_argument('--prefix', default='regions-canopy')
parser.add_argument('--slot', choices=['slot-a', 'slot-b'], default='slot-b')
parser.add_argument('--visual-review', type=Path, required=True)
parser.add_argument('--npc-prefix', default='landscape-canopy')
parser.add_argument('--performance-prefix', default='landscape-perf')
args = parser.parse_args()
evidence = ROOT / 'artifacts/landscape-20261103'
player = ROOT / 'builds' / args.slot


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    raw = path.read_bytes()
    return raw.decode('utf-16' if raw[:2] in (b'\xff\xfe', b'\xfe\xff') else 'utf-8-sig')


def load(path):
    return json.loads(read(path))


stamp = load(player / 'build-inputs.json')
source = subprocess.check_output(['git', 'rev-parse', args.source], cwd=ROOT, text=True).strip()
subprocess.run(['git', 'diff', '--exit-code', source, '--', 'src',
                'unity/WorldGen.Unity/Assets/WorldGen/Runtime',
                'unity/WorldGen.Unity/Assets/WorldGen/Editor',
                'unity/WorldGen.Unity/Assets/WorldGen/Resources'], cwd=ROOT, check=True)
assert stamp['Backend'] == 'Mono' and not stamp['Development']
package = {
    'AssemblySha256': sha(player / 'WorldGen_Data/Managed/Assembly-CSharp.dll'),
    'CoreSha256': sha(player / 'WorldGen_Data/Managed/WorldGen.Core.dll'),
    'WorkerSha256': sha(player / 'TerrainWorker/WorldGen.Cli.exe'),
}
assert package['WorkerSha256'] == stamp['TerrainWorkerSha256']
for assembly in stamp['Inputs']:
    assert sha(player / 'WorldGen_Data/Managed' / (assembly['Assembly'] + '.dll')) == assembly['Sha256']

kinds = ['pond', 'SandSea', 'GravelDesert', 'ClayPan', 'SaltFlat', 'FreshwaterMarsh',
         'WoodedSwamp', 'Peatland', 'LimestoneUpland', 'GlacialLowland']
cases = []
review = load(args.visual_review)
for kind in kinds:
    name = args.prefix + '-' + kind.lower()
    folder = evidence / name
    assert int(read(folder / 'exit-code.txt')) == 0, name
    assert not (folder / 'failure.txt').exists(), name
    receipt = load(folder / 'package.json')
    assert all(receipt[k] == v for k, v in package.items()), (name, 'Stale package')
    scene = load(folder / 'scene.json')
    assert scene['Requested'] == kind
    if kind != 'pond':
        assert scene['Actual'] == kind, name
    else:
        assert scene['Depth'] > .9 and scene['WaterRenderers'] > 0
    checks = read(folder / 'checks.txt').splitlines()
    assert checks and all(line.startswith('PASS ') for line in checks), name
    for phrase in ['loaded surrounding terrain footprint', 'summer illumination', 'do not mutate regional history']:
        assert any(phrase in line for line in checks), (name, phrase)
    images = {n: sha(folder / (n + '.png')) for n in ['wide', 'ground']}
    assert review[kind]['images'] == images, (name, 'Review must identify the actual inspected images')
    assert review[kind]['accepted'], (name, 'Visual review unresolved')
    cases.append({'name': name, 'checks': len(checks), 'scene': scene, 'images': images,
                  'checks_sha256': sha(folder / 'checks.txt'), 'visual_review': review[kind]})

assert 'PASS actual terrain contains enclosed local ponds and shallow wetland pools' in read(ROOT / 'artifacts/landscape-canopy-world.log')
assert 'PASS 27175' in read(ROOT / 'artifacts/landscape-canopy-rivers.log')
npc_cases = []
for kind in ['wet', 'cold']:
    folder = ROOT / 'artifacts/npc-20261103' / (args.npc_prefix + '-' + kind)
    assert int(read(folder / 'exit-code.txt')) == 0, folder
    assert not (folder / 'failure.txt').exists(), folder
    receipt = load(folder / 'package.json')
    assert all(receipt[k] == v for k, v in package.items()), (folder, 'Stale NPC regression package')
    checks = read(folder / 'checks.txt').splitlines()
    assert checks and all(line.startswith('PASS ') for line in checks), folder
    for phrase in ['can enter', 'can leave', 'Actual named binary save', 'does not duplicate']:
        assert any(phrase in line for line in checks), (folder, phrase)
    npc_cases.append({'name': folder.name, 'checks': len(checks), 'checks_sha256': sha(folder / 'checks.txt')})
performance = {}
for name in ['baseline', 'candidate']:
    folder = ROOT / 'artifacts/npc-20261103' / (args.performance_prefix + '-' + name)
    assert int(read(folder / 'exit-code.txt')) == 0 and not (folder / 'failure.txt').exists(), folder
    receipt = load(folder / 'package.json')
    if name == 'candidate':
        assert all(receipt[k] == v for k, v in package.items()), (folder, 'Stale measured candidate')
    rows = load(folder / 'performance.json')
    assert len(rows) == 4 and rows[0]['Residents'] >= 25, folder
    performance[name] = {'package': receipt, 'scene': load(folder / 'scene-info.json'), 'rows': rows}
assert performance['baseline']['scene'] == performance['candidate']['scene'], 'Comparison must use the same simulated community and context'
cost_log = ROOT / 'artifacts/landscape-uncontended-cost.log'
costs = [json.loads(line) for line in read(cost_log).splitlines() if line.startswith('{"Directory":')]
assert len(costs) == 2 and all(len(row['LandSample12675Ms']) == 3 for row in costs)
warm_ratio = statistics.median(costs[1]['LandSample12675Ms'][1:]) / statistics.median(costs[0]['LandSample12675Ms'][1:])
subprocess.run(['python', 'scripts/verify-landscape-texture-budget.py'], cwd=ROOT, check=True)
lock = load(ROOT / 'assets/game-assets.lock.json')
prior_lock = json.loads(subprocess.check_output(
    ['git', 'show', 'living-npcs-foundation-20261103:assets/game-assets.lock.json'], cwd=ROOT, text=True))
for path, prior in prior_lock['files'].items():
    assert lock['files'][path]['sha256'] == prior['sha256'], path

manifest = {
    'recorded_utc': datetime.now(timezone.utc).isoformat(),
    'source': source,
    'baseline': 'living-npcs-foundation-20261103',
    'slot': 'builds/' + args.slot,
    'build': stamp,
    'package': package,
    'cases': cases,
    'npc_regression': npc_cases,
    'npc_performance': performance,
    'performance_conditions': 'Baseline and candidate players run sequentially after the owner closed WorldGen. Earlier contended wet-camp timing is excluded from this comparison. Terrain readiness is retained in every measurement row.',
    'land_sampling_cost': {'runs': costs, 'warm_ratio_to_npc_checkpoint': warm_ratio,
                           'log_sha256': sha(cost_log), 'conditions': 'Sequential comparison after native jobs complete; same three physical land contexts, baseline first. This is not whole-game FPS.'},
    'research': 'assets/verification/landscape-research-20261103.json',
    'art_sources': 'assets/verification/landscape-surface-sources-20261103.json',
    'texture_budget': load(ROOT / 'assets/verification/landscape-surface-budget-20261103.json'),
    'original_locked_inputs_unchanged': len(prior_lock['files']),
    'architecture': {
        'terrain': 'Continuous substrate, moisture, cover and relief controls on shared terrain vertices, before river fitting.',
        'ponds': 'Sparse deeper ponds plus finer shallow wetland pools, complete rim surveys, disjoint basin collars and bounded caches; no global pond tick ledger.',
        'art': 'Four slots replaced in the same fourteen-layer BC7 arrays; original other slices byte-exact.',
    },
    'limits': [
        'This is a local landscape province pass, not a new tectonic or river-network generator.',
        'Small ponds are static derived enclosed basins; seasonal drying, thaw and water budgets are not simulated.',
        'No volcano, waterfall or cave landmark pass; no stage-three tree-art overhaul in this checkpoint.',
        'Numerical wavelengths and encounter density are tunable game priors, not measured global distributions.',
        'Visual findings are internal review; owner acceptance is not inferred.',
    ],
}
output = ROOT / 'assets/verification/landscape-regions-20261103.json'
output.write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print(output)
