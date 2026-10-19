"""Read-only verification of the local generated library and its source provenance."""
import csv
import hashlib
import json
from pathlib import Path

import numpy as np
import soundfile as sf

root = Path(__file__).resolve().parents[1]
rows = list(csv.DictReader((root/'scripts/weather-sounds.csv').open(encoding='utf-8')))
catalog = json.loads((root/'artifacts/weather-sounds/catalog.json').read_text())
results = json.loads((root/'artifacts/weather-sounds/results.json').read_text())
assert len(rows) == len(results) == 17
assert catalog['model'] == 'stabilityai/stable-audio-3-medium' and catalog['steps'] == 8
assert any(d['type'] == 'cuda' for d in catalog['devices'])
fingerprints = set()
report = []
for row in rows:
    record = results[row['id']]
    assert record['brief'] == row['brief'] and record['group'] == row['group']
    assert record['prompt'].startswith('TrackType: SFX.')
    assert 'trees, leaves, foliage' in record['prompt'] and 'animals, birds, insects' in record['prompt']
    path = root/'sounds/weather'/(row['id']+'.wav')
    assert hashlib.sha256(path.read_bytes()).hexdigest() == record['file_sha256']
    pcm, rate = sf.read(path, dtype='float32', always_2d=True)
    assert rate == 44100 and np.isfinite(pcm).all()
    assert pcm.shape[1] == (1 if row['group'] == 'thunder' else 2)
    peak = float(np.max(np.abs(pcm)))
    rms = float(np.sqrt(np.mean(pcm.astype('float64')**2)))
    assert .002 < rms < .15 and peak <= .701
    assert np.max(np.abs(pcm[0])) < .0001 and np.max(np.abs(pcm[-1])) < .0001
    assert len(pcm)/rate >= (6 if row['group'] == 'thunder' else 25)
    signature = hashlib.sha256(path.read_bytes()).hexdigest()
    assert signature not in fingerprints
    fingerprints.add(signature)
    report.append(dict(id=row['id'], seconds=len(pcm)/rate, channels=pcm.shape[1], rms=rms, peak=peak))
sources = list((root/'unity/WorldGen.Unity/Assets/WorldGen/Runtime').glob('*.cs'))
assert not any('AudioClip.Create(' in p.read_text(encoding='utf-8-sig') for p in sources)
assert not (root/'src/WorldGen.Presentation/WindSynth.cs').exists()
assert not (root/'src/WorldGen.Presentation/RainSynth.cs').exists()
(root/'artifacts/weather-sounds/verification.json').write_text(json.dumps(report, indent=2))
print('PASS: 17 CUDA SA3 effects, provenance, PCM readback, distinctness, boundaries and no runtime synthesizers.')
print('These checks do not certify timbre, biological accuracy or absence of unwanted audible sources; audition remains necessary.')
