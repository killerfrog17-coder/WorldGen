"""Fetch the public SOMA Kimodo audition weights with pinned provenance and hashes.

No accounts, hosted inference, game edits, or source-art writes. Large weights
live on E: once; the audition keeps only receipts and its generated motions.
"""
import concurrent.futures
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import time
import threading
import urllib.request

REPO = Path(__file__).resolve().parent.parent
ROOT = REPO / 'artifacts/kimodo-first-person-demo'
MODELS = Path('E:/AI/WorldGen3D/models/Kimodo')
os.environ.setdefault('HF_HOME', 'E:/AI/WorldGen3D/kimodo-hf')
os.environ['HF_HUB_DISABLE_XET'] = '1'


def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(8 * 1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()


def range_download(repo_id, revision, name, path, expected):
    """Bounded parallel HTTP ranges; avoids a stalled single large-file stream."""
    size = expected['bytes']
    if path.exists() and path.stat().st_size == size and digest(path) == expected['sha256']:
        return path
    path.parent.mkdir(parents=True, exist_ok=True)
    marker = path.with_suffix(path.suffix + '.ranges.json')
    chunk = 32 * 1024 * 1024
    parts = [(start, min(size, start + chunk) - 1) for start in range(0, size, chunk)]
    completed = set(json.loads(marker.read_text()).get('completed', [])) if marker.exists() else set()
    if not path.exists() or path.stat().st_size != size:
        with path.open('wb') as stream:
            stream.truncate(size)
        completed.clear()
    lock = threading.Lock()
    url = f'https://huggingface.co/{repo_id}/resolve/{revision}/{name}?download=true'

    def piece(part):
        start, end = part
        if start in completed:
            return
        for attempt in range(4):
            try:
                request = urllib.request.Request(url, headers={'Range': f'bytes={start}-{end}'})
                with urllib.request.urlopen(request, timeout=45) as response:
                    if response.status != 206 or response.headers.get('Content-Range') != f'bytes {start}-{end}/{size}':
                        raise RuntimeError('Range response does not match requested bytes')
                    data = response.read()
                if len(data) != end - start + 1:
                    raise RuntimeError('Short range response')
                with path.open('r+b') as stream:
                    stream.seek(start)
                    stream.write(data)
                with lock:
                    completed.add(start)
                    marker.write_text(json.dumps({'completed': sorted(completed)}))
                    if len(completed) % 16 == 0 or len(completed) == len(parts):
                        print('PROGRESS', name, len(completed), '/', len(parts), flush=True)
                return
            except Exception:
                if attempt == 3:
                    raise
                time.sleep(1 + attempt)
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        list(pool.map(piece, parts))
    return path


def main():
    from huggingface_hub import HfApi, hf_hub_download
    ROOT.mkdir(parents=True, exist_ok=True)
    MODELS.mkdir(parents=True, exist_ok=True)
    wanted = [
        ('LocalAI-io/Llama-3-Kimodo-GGML', 'f583f3ee2b093f6d9ec524463b4152622abf11a6', 'text',
         ['Llama-3-Kimodo-Q8_0.gguf', 'tokenizer.gguf'],
         ['LICENSE-META-LLAMA-3.txt', 'NOTICE', 'README.md', 'MANIFEST.json']),
        ('LocalAI-io/Kimodo-SOMA-RP-v1.1-GGML', '65f8b8ab34ee7524161cc87b20ae4cb4f5d3a12d', 'motion',
         ['models/kimodo-soma-rp-v1.1-f32.gguf'],
         ['NOTICE', 'README.md', 'MANIFEST.json']),
    ]
    jobs = []
    provenance = []
    api = HfApi()
    for repo_id, revision, component, weights, notices in wanted:
        info = api.model_info(repo_id, revision=revision)
        if info.gated:
            raise RuntimeError('Unexpected access gate; do not authenticate or substitute silently.')
        directory = MODELS / component
        for name in notices:
            hf_hub_download(repo_id, name, revision=info.sha, local_dir=directory)
        manifest = json.loads((directory / 'MANIFEST.json').read_text())
        available = {entry['path']: entry for entry in manifest['files']}
        for name in weights:
            entry = available[name]
            jobs.append((repo_id, info.sha, name, directory, entry))
        provenance.append({'repository': repo_id, 'revision': info.sha,
                           'manifest': manifest, 'directory': str(directory)})

    missing_bytes = sum(max(0, entry['bytes'] - ((directory / name).stat().st_size
                        if (directory / name).exists() else 0))
                        for _, _, name, directory, entry in jobs)
    if shutil.disk_usage(MODELS).free < missing_bytes + 512 * 2**20:
        raise RuntimeError('Insufficient space for the remaining Kimodo weights; nothing was deleted.')

    def fetch(job):
        repo_id, revision, name, directory, expected = job
        print('DOWNLOAD', name, expected['bytes'], flush=True)
        path = range_download(repo_id, revision, name, directory / name, expected)
        actual = digest(path)
        if path.stat().st_size != expected['bytes'] or actual != expected['sha256']:
            raise RuntimeError('Kimodo weight integrity mismatch: ' + name)
        print('VERIFIED', name, actual, flush=True)
        return {'path': str(path), 'bytes': path.stat().st_size, 'sha256': actual}

    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        verified = list(pool.map(fetch, jobs))
    source = {}
    for name in ['kimodo', 'kimodo-cpp']:
        source[name] = subprocess.check_output(
            ['git', '-C', str(REPO / 'artifacts/tools' / name), 'rev-parse', 'HEAD'],
            text=True).strip()
    record = {'created_unix': time.time(), 'model': 'Kimodo-SOMA-RP-v1.1',
              'runtime': 'kimodo.cpp', 'encoder': 'LLM2Vec Q8_0',
              'repositories': provenance, 'files': verified, 'source': source,
              'paid_calls': 0, 'login_required': False,
              'note': 'SOMA motion weights are F32. Only the text encoder is quantized.'}
    (ROOT / 'model-provenance.json').write_text(json.dumps(record, indent=2))
    print('PUBLIC KIMODO WEIGHTS READY', flush=True)


if __name__ == '__main__':
    main()
