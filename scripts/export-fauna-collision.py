"""Derive read-only collision skins from the exact accepted runtime exports.

The shipped render bundles deliberately discard CPU mesh data. Keep a compact,
independently streamed copy for hit tests rather than changing those bundles.
"""
from pathlib import Path
import gzip, hashlib, json, struct

root = Path(__file__).resolve().parents[1]
exports = root / 'artifacts/fauna-runtime-20261026/exports'
output = root / 'unity/WorldGen.Unity/Assets/StreamingAssets/fauna-collision'
output.mkdir(parents=True, exist_ok=True)
total = 0
for directory in sorted(exports.iterdir()):
    if not directory.is_dir():
        continue
    meta = json.loads((directory / 'model.json').read_text(encoding='utf-8'))
    raw = (directory / 'mesh.bin').read_bytes()
    assert raw[:8] == b'WGFAUNA1'
    count, indices, bones, parts = struct.unpack_from('<4i', raw, 8)
    assert (count, indices, bones, parts) == (meta['vertices'], meta['indices'], meta['bones'], meta['parts'])
    influences = meta.get('influences', 4)
    morph = bool(meta.get('hasMorph', False))
    stride = (9 + influences * 2 + (3 if morph else 0)) * 4
    assert len(raw) == 24 + count * stride + indices * 4
    # Preserve original bytes, but omit unused normals. Runtime parser handles
    # both influence widths and the optional source shape deformation.
    data = bytearray(b'WGHITS01')
    data.extend(bytes.fromhex(meta['source_sha256']))
    data.extend(struct.pack('<6i', count, indices, bones, parts, influences, int(morph)))
    for vertex in range(count):
        start = 24 + vertex * stride
        data.extend(raw[start:start + 12])
        data.extend(raw[start + 24:start + stride])
    data.extend(raw[24 + count * stride:])
    compressed = gzip.compress(data, compresslevel=6, mtime=0)
    (output / (directory.name + '.bin.gz')).write_bytes(compressed)
    total += len(compressed)
print(f'Collision skins: {len(list(output.glob("*.bin.gz")))} / {total / 1048576:.1f} MiB; original bundles untouched')
