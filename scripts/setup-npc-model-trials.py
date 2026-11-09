r"""Download pinned NPC comparison weights without changing the live AI profile.

Example: python scripts/setup-npc-model-trials.py --directory D:\WorldGenModels\NpcAI
Interrupted downloads resume; only complete SHA-256-verified files are published.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import time
import urllib.request


def digest(path):
    sha = hashlib.sha256()
    with path.open('rb') as source:
        for block in iter(lambda: source.read(4 * 1024 * 1024), b''):
            sha.update(block)
    return sha


def install(record, directory):
    target = directory / record['file']
    if target.exists():
        if target.stat().st_size != record['bytes'] or digest(target).hexdigest() != record['sha256']:
            raise RuntimeError('Existing model failed verification: ' + str(target))
        print('Verified existing ' + target.name, flush=True)
        return
    partial = target.with_suffix(target.suffix + '.partial')
    url = 'https://huggingface.co/{repository}/resolve/{revision}/{file}'.format(**record)
    for attempt in range(4):
        offset = partial.stat().st_size if partial.exists() else 0
        if offset > record['bytes']:
            raise RuntimeError('Oversized partial download: ' + str(partial))
        sha = digest(partial) if offset else hashlib.sha256()
        if offset == record['bytes']:
            if sha.hexdigest() != record['sha256']:
                raise RuntimeError('Download checksum mismatch: ' + target.name)
            partial.replace(target)
            print('Downloaded and verified ' + target.name, flush=True)
            return
        request = urllib.request.Request(url + '?download=true&attempt=' + str(time.time_ns()),
                                         headers={'User-Agent': 'WorldGen-local-NPC-trials',
                                                  'Range': 'bytes=' + str(offset) + '-'})
        try:
            with urllib.request.urlopen(request, timeout=90) as response:
                if response.status == 206:
                    content_range = response.headers.get('Content-Range', '')
                    if not content_range.startswith('bytes ' + str(offset) + '-') or not content_range.endswith('/' + str(record['bytes'])):
                        raise RuntimeError('Unexpected download range for ' + target.name)
                elif response.status == 200:
                    offset = 0
                    sha = hashlib.sha256()
                else:
                    raise RuntimeError('Unexpected HTTP status ' + str(response.status))
                last_report = time.monotonic()
                print('Downloading ' + target.name + ' from ' + str(offset // (1024 * 1024)) + ' MiB', flush=True)
                with partial.open('ab' if offset else 'wb') as output:
                    while True:
                        block = response.read(4 * 1024 * 1024)
                        if not block:
                            break
                        offset += len(block)
                        if offset > record['bytes']:
                            raise RuntimeError('Oversized response for ' + target.name)
                        output.write(block)
                        sha.update(block)
                        if time.monotonic() - last_report >= 20:
                            print(f'{target.name}: {offset / record["bytes"]:.1%}', flush=True)
                            last_report = time.monotonic()
            if offset != record['bytes']:
                raise OSError('Incomplete response')
            if sha.hexdigest() != record['sha256']:
                raise RuntimeError('Download checksum mismatch: ' + target.name)
            partial.replace(target)
            print('Downloaded and verified ' + target.name, flush=True)
            return
        except (OSError, TimeoutError) as error:
            if attempt == 3:
                raise
            print(f'Connection interrupted ({type(error).__name__}); resuming.', flush=True)
    raise RuntimeError('Download did not complete: ' + target.name)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    manifest = json.loads((root / 'assets/npc-ai/local-trials.json').read_text())
    directory = args.directory.resolve()
    directory.mkdir(parents=True, exist_ok=True)
    required = 0
    for item in manifest['models']:
        target = directory / item['file']
        partial = target.with_suffix(target.suffix + '.partial')
        existing = target if target.exists() else partial
        required += max(0, item['bytes'] - (existing.stat().st_size if existing.exists() else 0))
    if shutil.disk_usage(directory).free < required + 10 * 1024**3:
        raise RuntimeError('Insufficient disk space including 10 GiB reserve')
    print(f'Trial directory: {directory}; additional download {required / 1024**3:.2f} GiB', flush=True)
    for record in manifest['models']:
        install(record, directory)
    inventory = dict(manifest, directory=str(directory), status='Downloaded and SHA-256 verified; not benchmarked')
    (directory / 'installed-trials.json').write_text(json.dumps(inventory, indent=2) + '\n')
    print('All three candidates ready. Live AI settings were not changed.', flush=True)


if __name__ == '__main__':
    main()
