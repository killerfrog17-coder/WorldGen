"""Fetch only selected CC0 MPFB assets using ZIP byte ranges, avoiding whole packs."""
import concurrent.futures
import hashlib
import io
import json
from pathlib import Path
import threading
import zipfile

import requests

ROOT = Path(__file__).resolve().parent.parent / 'artifacts/human-builder-demo'
DEST = ROOT / 'mpfb-assets'


class RemoteZip(io.RawIOBase):
    def __init__(self, url):
        self.url = url
        r = requests.head(url, timeout=20)
        r.raise_for_status()
        self.length = int(r.headers['Content-Length'])
        self.position = 0

    def seekable(self):
        return True

    def tell(self):
        return self.position

    def seek(self, offset, whence=0):
        self.position = offset if whence == 0 else self.position + offset if whence == 1 else self.length + offset
        return self.position

    def read(self, count=-1):
        if count < 0:
            count = self.length - self.position
        if not count:
            return b''
        start = self.position
        self.position += count
        r = requests.get(self.url, headers={'Range': f'bytes={start}-{start+count-1}'}, timeout=(15, 60))
        r.raise_for_status()
        if r.status_code != 206 or len(r.content) != count:
            raise RuntimeError(f'Invalid byte range: {r.status_code}, {len(r.content)}, expected {count}')
        return r.content


def main():
    url = 'https://files.makehumancommunity.org/asset_packs/makehuman_system_assets/makehuman_system_assets_cc0.zip'
    with zipfile.ZipFile(RemoteZip(url)) as archive:
        names = archive.namelist()
    (ROOT / 'mpfb-system-pack-files.json').write_text(json.dumps(names, indent=2))
    prefixes = [
        'eyes/', 'eyebrows/eyebrow001/', 'eyebrows/eyebrow005/',
        'eyelashes/eyelashes01/', 'teeth/teeth_base/',
        'hair/afro01/', 'hair/short02/', 'hair/short03/', 'hair/bob01/', 'hair/ponytail01/',
        'clothes/male_casualsuit01/', 'clothes/female_sportsuit01/',
        'skins/young_african_female/', 'skins/young_asian_male/',
        'skins/young_caucasian_female/', 'skins/middleage_caucasian_male/',
        'skins/young_asian_female/', 'skins/old_caucasian_male/',
    ]
    selected = [name for name in names if any(name.startswith(prefix) for prefix in prefixes) and not name.endswith('/')]
    local = threading.local()

    def fetch(name):
        destination = (DEST / name).resolve()
        assert destination.is_relative_to(DEST.resolve())
        if not destination.is_file():
            if not hasattr(local, 'archive'):
                local.archive = zipfile.ZipFile(RemoteZip(url))
            content = local.archive.read(name)
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(content)
        content = destination.read_bytes()
        print('fetched', name, len(content), flush=True)
        return {'path': name, 'bytes': len(content), 'sha256': hashlib.sha256(content).hexdigest()}

    with concurrent.futures.ThreadPoolExecutor(10) as executor:
        result = list(executor.map(fetch, selected))
    (ROOT / 'mpfb-pack-provenance.json').write_text(json.dumps({'url': url, 'licence': 'CC0', 'files': result}, indent=2))
    print('COMPLETE', len(result), sum(x['bytes'] for x in result), flush=True)


if __name__ == '__main__':
    main()
