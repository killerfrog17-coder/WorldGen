"""Curated library/provenance checks. Classification does not replace listening."""
import hashlib
import json
import csv
from pathlib import Path

import numpy as np
import soundfile as sf

root = Path(__file__).resolve().parents[1]
folder = root/'sounds/weather-isolated'
release = json.loads((root/'artifacts/weather-isolated/release.json').read_text())
selected = {r['id']:r for r in csv.DictReader((root/'scripts/weather-selected.csv').open(encoding='utf-8'))}
audit = json.loads((root/'artifacts/weather-sound-review/final-audit.json').read_text())
screened = dict(line.split('\t') for line in (folder/'screened.tsv').read_text().splitlines() if not line.startswith('#'))
release = {k:v for k,v in release.items() if not k.startswith('wind')}
assert len(release) == len(screened) == 7
assert set(p.name for p in folder.glob('*.wav')) == set(screened)
assert not any(name.startswith('wind') for name in screened)
fingerprints = set()
report = []
for name, record in release.items():
    filename = name+'.wav'
    assert not audit['files'][filename]['screen_rejections']
    assert record['prompt'].startswith('TrackType: SFX.')
    assert record['checkpoint'] in ('stable_audio_3_small_sfx.safetensors','stable_audio_3_medium.safetensors')
    assert record['checkpoint'] == selected[name]['checkpoint'] and record['actual_seed'] == int(selected[name]['seed'])
    assert record['brief'] == selected[name]['brief'] and record['file_sha256'] == selected[name]['file_sha256']
    path = folder/filename
    assert hashlib.sha256(path.read_bytes()).hexdigest() == record['file_sha256'] == screened[filename]
    pcm, rate = sf.read(path, dtype='float32', always_2d=True)
    assert rate == 44100 and np.isfinite(pcm).all()
    assert pcm.shape[1] == (1 if name.startswith('thunder') else 2)
    peak = float(np.max(np.abs(pcm)))
    rms = float(np.sqrt(np.mean(pcm.astype('float64')**2)))
    assert .001 < rms < .15 and peak <= .701
    assert np.max(np.abs(pcm[0])) < .0001 and np.max(np.abs(pcm[-1])) < .0001
    assert len(pcm)/rate >= (4 if name.startswith(('wind','thunder')) else 18)
    if name.startswith('wind'):assert record['gain_db'] <= 0
    signature = hashlib.sha256(path.read_bytes()).hexdigest()
    assert signature not in fingerprints
    fingerprints.add(signature)
    report.append(dict(id=name, seconds=len(pcm)/rate, channels=pcm.shape[1], rms=rms, peak=peak))
sources = list((root/'unity/WorldGen.Unity/Assets/WorldGen/Runtime').glob('*.cs'))
assert not any('AudioClip.Create(' in p.read_text(encoding='utf-8-sig') for p in sources)
assert not (root/'src/WorldGen.Presentation/WindSynth.cs').exists()
assert not (root/'src/WorldGen.Presentation/RainSynth.cs').exists()
(root/'artifacts/weather-isolated/verification.json').write_text(json.dumps(report, indent=2))
print('PASS: 7 rain/thunder effects, no wind assets, provenance, PCM and no runtime synthesis.')
print('These checks do not certify timbre, biological accuracy or absence of unwanted audible sources; audition remains necessary.')
