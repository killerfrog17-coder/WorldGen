"""Verify admitted locally generated contacts, not subjective listening quality."""
from pathlib import Path
import hashlib
import math
import struct
import wave

root = Path(__file__).resolve().parents[1] / 'sounds' / 'footsteps'
entries = dict(line.split('\t') for line in (root / 'screened.tsv').read_text().splitlines() if line and not line.startswith('#'))
expected = {f'{material}-{i:02}.wav' for material in ('soil', 'sand', 'gravel', 'stone', 'mud', 'organic', 'snow') for i in range(1, 5)}
assert set(entries) == expected, 'Need four admitted variants per material'
seen = set()
for name, digest in sorted(entries.items()):
    path = root / name
    actual = hashlib.sha256(path.read_bytes()).hexdigest()
    assert actual == digest and digest not in seen, name
    seen.add(digest)
    with wave.open(str(path), 'rb') as source:
        assert source.getnchannels() == 1 and source.getsampwidth() == 2 and source.getframerate() == 44100, name
        samples = struct.unpack('<' + 'h' * source.getnframes(), source.readframes(source.getnframes()))
    duration = len(samples) / 44100
    rms = math.sqrt(sum(v*v for v in samples) / len(samples)) / 32768
    assert .12 <= duration <= 1.2 and .0001 < rms <= .081, name
    assert max(abs(v) for v in samples) / 32768 <= .481, name
    assert abs(samples[0]) < 3 and abs(samples[-1]) < 3, name
print('PASS28 unique mono AI contacts, admission hashes, duration, restrained levels and boundary fades')
