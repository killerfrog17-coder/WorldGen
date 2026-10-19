"""Publish selected AI recordings; quarantine owned rejects recoverably.

Never deletes music, user recordings or model files. Provenance stays in artifacts.
"""
import hashlib
import json
import shutil
from pathlib import Path

root = Path(__file__).resolve().parents[1]
active = root/'sounds/weather-isolated'
evidence = root/'artifacts/weather-isolated'
review = root/'artifacts/weather-sound-review'
medium = json.loads((evidence/'results.json').read_text())
audit = json.loads((review/'replacement-audit.json').read_text())['files']
selected = ['rain-light-01', 'rain-light-02', 'rain-medium-01',
            'rain-medium-02', 'rain-heavy-01', 'thunder-03', 'thunder-04']
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
for name in selected:
    assert not audit[name+'.wav']['screen_rejections'], (name, audit[name+'.wav'])
    assert sha(active/(name+'.wav')) == medium[name]['file_sha256']

quarantine = review/'rejected-medium-v2'
quarantine.mkdir(exist_ok=True)
for name, record in medium.items():
    if name in selected:
        continue
    source = active/(name+'.wav')
    if not source.exists():
        continue
    assert sha(source) == record['file_sha256'], 'Refusing to move changed/unmanaged '+str(source)
    destination = quarantine/source.name
    assert not destination.exists(), 'Refusing to overwrite '+str(destination)
    shutil.move(str(source), str(destination))
records = {name: medium[name] for name in selected}
for name, record in records.items():
    record['path'] = str(active/(name+'.wav'))
(evidence/'release.json').write_text(json.dumps(records, indent=2))
print('Published 7 rain/thunder recordings; wind is intentionally excluded')
