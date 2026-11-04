"""Prepare a photographed, two-metre CC0 grass floor; no runtime synthesis."""
from pathlib import Path
import hashlib
import json
import requests
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'assets-source/terrain/surfaces/cc0-grass/leafy_grass'
OUTPUT = ROOT / 'unity/WorldGen.Unity/Assets/WorldGen/Resources/GroundGrassV2.png'

def fetch(url):
    response = requests.get(url, timeout=60)
    response.raise_for_status()
    return response.content

def main():
    SOURCE.mkdir(parents=True, exist_ok=True)
    info = json.loads(fetch('https://api.polyhaven.com/info/leafy_grass'))
    files = json.loads(fetch('https://api.polyhaven.com/files/leafy_grass'))
    remote = files['Diffuse']['1k']['jpg']
    source = SOURCE / 'Diffuse.jpg'
    if not source.exists() or hashlib.md5(source.read_bytes()).hexdigest() != remote['md5']:
        source.write_bytes(fetch(remote['url']))
    assert hashlib.md5(source.read_bytes()).hexdigest() == remote['md5']
    rgb = np.asarray(Image.open(source).convert('RGB'), dtype=float) / 255
    rgb = np.where(rgb <= .04045, rgb / 12.92, ((rgb + .055) / 1.055)**2.4)
    # The biome supplies the broad hue. Keep the actual scanned blade/litter
    # colours and contrast around a neutral .5 sRGB mean, rather than noise.
    target = ((.5 + .055) / 1.055)**2.4
    rgb *= target / np.maximum(.001, rgb.mean(axis=(0, 1)))
    rgb = np.clip(rgb, 0, 1)
    rgb = np.where(rgb <= .0031308, rgb * 12.92, 1.055 * rgb**(1/2.4) - .055)
    Image.fromarray(np.uint8(rgb * 255 + .5)).save(OUTPUT)
    report = {'asset': 'leafy_grass', 'page': 'https://polyhaven.com/a/leafy_grass',
              'license': 'CC0-1.0', 'license_url': 'https://polyhaven.com/license',
              'info': info, 'download': remote, 'repeat_metres': 2,
              'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
              'output_sha256': hashlib.sha256(OUTPUT.read_bytes()).hexdigest(),
              'processing': 'Per-channel linear mean normalization only; photographed 1024px surface, 2m repeat. sRGB BC7 mipmapped runtime import.'}
    (SOURCE / 'source.json').write_text(json.dumps(report, indent=2) + '\n')
    (ROOT / 'assets/verification/ground-grass-source-20261104.json').write_text(json.dumps(report, indent=2) + '\n')
    print('Prepared photographed grass:', OUTPUT)

if __name__ == '__main__':
    main()
