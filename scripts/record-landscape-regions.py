"""Record the actual source-matched landscape acceptance package.

This records completed checks, not a claim that their screenshots look good.
Visual findings must be supplied separately after inspecting the captures.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source', required=True)
parser.add_argument('--prefix', default='regions-final')
parser.add_argument('--slot', choices=['slot-a', 'slot-b'], default='slot-b')
parser.add_argument('--visual-review', type=Path, required=True)
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

assert 'PASS actual terrain contains enclosed local ponds' in read(ROOT / 'artifacts/landscape-final-world.log')
assert 'PASS 27175' in read(ROOT / 'artifacts/landscape-final-rivers.log')
subprocess.run(['python', 'scripts/verify-landscape-texture-budget.py'], cwd=ROOT, check=True)
lock = load(ROOT / 'assets/game-assets.lock.json')
prior_lock = json.loads(subprocess.check_output(
    ['git', 'show', 'living-npcs-foundation-20261103:assets/game-assets.lock.json'], cwd=ROOT, text=True))
for path, prior in prior_lock['files'].items():
    assert lock['files'][path]['sha256'] == prior['sha256'], path

manifest = {
    'recorded_utc': datetime.now(timezone.utc).isoformat(),
    'source': args.source,
    'baseline': 'living-npcs-foundation-20261103',
    'slot': 'builds/' + args.slot,
    'build': stamp,
    'package': package,
    'cases': cases,
    'research': 'assets/verification/landscape-research-20261103.json',
    'art_sources': 'assets/verification/landscape-surface-sources-20261103.json',
    'texture_budget': load(ROOT / 'assets/verification/landscape-surface-budget-20261103.json'),
    'original_locked_inputs_unchanged': len(prior_lock['files']),
    'architecture': {
        'terrain': 'Continuous substrate, moisture, cover and relief controls on shared terrain vertices, before river fitting.',
        'ponds': 'Sparse seeded physical-space candidates, complete rim survey, disjoint basin collars and bounded caches; no global pond tick ledger.',
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
