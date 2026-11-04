"""Pin an inspected world-visual checkpoint to its real source and evidence.

The review specification supplies inspected images and candid findings. This
recorder never infers visual acceptance from an exit code or calls a measured
average a universal frame-rate guarantee. It does not change the shortcut.
"""
import argparse
import csv
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    data = path.read_bytes()
    return data.decode('utf-16' if data[:2] in (b'\xff\xfe', b'\xfe\xff') else 'utf-8-sig')


def load(path):
    return json.loads(read(path))


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def require(ok, message):
    if not ok:
        raise ValueError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--slot', choices=['slot-a', 'slot-b'], required=True)
    parser.add_argument('--review', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    review = load(args.review)
    player = ROOT / 'builds' / args.slot
    require(not (player / 'build-incomplete.txt').exists(), 'Build is incomplete')
    stamp = load(player / 'build-inputs.json')
    require(not stamp['Development'], 'Unexpected development watermark')
    require(not stamp['SourceChanges'], 'Build contains uncommitted source')
    source = stamp['SourceCommit']
    subprocess.run(['git', 'diff', '--exit-code', source, '--', 'src',
                    'unity/WorldGen.Unity/Assets/WorldGen/Runtime',
                    'unity/WorldGen.Unity/Assets/WorldGen/Resources'], cwd=ROOT, check=True)
    package = {
        'AssemblySha256': sha(player / 'WorldGen_Data/Managed/Assembly-CSharp.dll'),
        'CoreSha256': sha(player / 'WorldGen_Data/Managed/WorldGen.Core.dll'),
        'WorkerSha256': sha(player / 'TerrainWorker/WorldGen.Cli.exe'),
    }
    require(package['WorkerSha256'] == stamp['TerrainWorkerSha256'], 'Worker stamp mismatch')
    for item in stamp['Inputs']:
        require(sha(player / 'WorldGen_Data/Managed' / (item['Assembly'] + '.dll')) == item['Sha256'], 'Imported assembly mismatch')

    cases = []
    for item in review['native_cases']:
        folder = ROOT / item['folder']
        require(int(read(folder / 'exit-code.txt')) == 0 and not (folder / 'failure.txt').exists(), f'Failed native case: {folder}')
        receipt = load(folder / 'package.json')
        current = all(receipt[k] == v for k, v in package.items())
        require(current or item.get('historical_reference_only', False), f'Stale native package: {folder}')
        checks = read(folder / 'checks.txt').splitlines()
        require(checks and all(c.startswith('PASS ') for c in checks), f'Incomplete native checks: {folder}')
        require(item['findings'].strip(), f'Missing actual inspection findings: {folder}')
        images = {}
        for name, expected in item.get('images', {}).items():
            actual = sha(folder / name)
            require(actual == expected, f'Inspected image changed: {folder / name}')
            images[name] = actual
        cases.append({**item, 'package': receipt, 'current_package': current,
                      'checks': len(checks), 'checks_sha256': sha(folder / 'checks.txt'), 'images': images})
    current_cases = [c for c in cases if c['current_package']]
    for suffix in ['-wet', '-cold']:
        require(any(c['folder'].endswith(suffix) for c in current_cases), f'Missing current NPC regression: {suffix}')
    require(any(c.get('images') for c in current_cases), 'No inspected current-package landscape')

    performance = []
    misses = []
    for item in review['performance']:
        folder = ROOT / item['folder']
        require((folder / 'complete.txt').exists() and (folder / 'input-sha256.txt').exists(), f'Incomplete benchmark: {folder}')
        receipt = load(folder / 'package.json') if (folder / 'package.json').exists() else None
        current = receipt is not None and all(receipt[k] == v for k, v in package.items())
        require(current or item.get('historical_reference_only', False), f'Unpinned or stale benchmark: {folder}')
        rows = list(csv.DictReader(read(folder / 'summary.csv').splitlines()))
        require(rows, f'Empty benchmark: {folder}')
        for row in rows:
            require(all(math.isfinite(float(row[k])) for k in ['fps', 'p95_ms', 'max_ms']), f'Invalid timing: {folder}')
            if current and row['case'] in ('turning', 'travel') and float(row['fps']) < 100:
                misses.append({'site': row['site'], 'case': row['case'], 'fps': float(row['fps'])})
        performance.append({**item, 'package': receipt, 'current_package': current,
                            'summary_sha256': sha(folder / 'summary.csv'), 'rows': rows})
    require(any(p['current_package'] for p in performance), 'No current benchmark')
    report = {
        'recorded_utc': datetime.now(timezone.utc).isoformat(), 'source': source,
        'slot': args.slot, 'build': stamp, 'package': package,
        'status': 'Playable review checkpoint; owner visual approval pending',
        'scope': review['scope'], 'limitations': review['limitations'],
        'native_cases': cases, 'performance': performance,
        'measured_100fps_misses': misses,
        'universal_100fps_claim': False,
        'feature_rollbacks': 'assets/verification/living-world-feature-index-20261104.json',
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + '\n')
    print(f'Recorded {len(cases)} native cases, {len(performance)} benchmarks; {len(misses)} measured target misses. Shortcut unchanged.')


if __name__ == '__main__':
    main()
