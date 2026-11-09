"""Install the pinned optional NVIDIA backend; never change the live AI profile."""
import hashlib
import json
import os
from pathlib import Path
import ssl
import urllib.request
import zipfile

root = Path(__file__).resolve().parents[1]
record = json.loads((root / 'assets/npc-ai/runtime.json').read_text())['cuda_runtime']
cache = Path(os.environ['LOCALAPPDATA']) / 'WorldGen/NpcAI'
target = cache / ('llama-' + record['version'] + '-cuda')
target.mkdir(parents=True, exist_ok=True)
receipt = target / 'install-receipt.json'
if receipt.exists() and json.loads(receipt.read_text()) == record and (target / 'llama-server.exe').exists():
    print('CUDA backend already installed: ' + str(target))
else:
    try:
        import certifi
        context = ssl.create_default_context(cafile=certifi.where())
    except ImportError:
        context = ssl.create_default_context()
    for package in record['packages']:
        archive = cache / package['file']
        sha = hashlib.sha256()
        count = 0
        with urllib.request.urlopen(package['url'], timeout=90, context=context) as source, archive.open('wb') as output:
            while True:
                chunk = source.read(4 * 1024 * 1024)
                if not chunk:
                    break
                count += len(chunk)
                sha.update(chunk)
                output.write(chunk)
        if count != package['bytes'] or sha.hexdigest() != package['sha256']:
            raise RuntimeError('CUDA package verification failed: ' + archive.name)
        with zipfile.ZipFile(archive) as bundle:
            for member in bundle.infolist():
                if not (target / member.filename).resolve().is_relative_to(target.resolve()):
                    raise RuntimeError('Unsafe archive path')
            bundle.extractall(target)
        print('Verified and extracted ' + archive.name, flush=True)
        if not archive.resolve().is_relative_to(cache.resolve()):
            raise RuntimeError('Archive outside shared runtime cache')
        archive.unlink()
    if not (target / 'llama-server.exe').exists():
        raise RuntimeError('CUDA server missing after extraction')
    receipt.write_text(json.dumps(record, indent=2) + '\n')
    print('CUDA backend ready: ' + str(target), flush=True)
