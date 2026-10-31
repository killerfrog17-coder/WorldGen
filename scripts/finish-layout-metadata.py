"""Rebase active input manifests and preserve metadata hash references."""
import hashlib,json,re,subprocess
from pathlib import Path
from project_layout import ROOT,relocated

task=ROOT/'artifacts/layout-migration';plan=json.loads((task/'plan.json').read_text())
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
old='artifacts/fauna-runtime-20261026/native-inventory.json'
new='assets-source/animals/export-metadata/native-inventory.json'
paths={ROOT/r['new'] for r in plan['files'] if Path(r['new']).suffix in {'.json','.py','.ps1','.txt','.md'}}
paths.update(ROOT/p for p in subprocess.check_output(['git','ls-files','-z'],cwd=ROOT).decode().split('\0') if p and p.endswith(('.json','.py','.cs','.ps1','.md')) and not p.startswith('artifacts/'))
for p in paths:
    if not p.is_file() or p.name in {'project-layout.json','migrate-project-layout.py','rewrite-project-layout.py','finish-layout-metadata.py','AGENTS.md'}:continue
    try:text=p.read_text(encoding='utf-8-sig')
    except UnicodeDecodeError:continue
    changed=text.replace(old,new).replace(old.replace('/','\\\\'),new.replace('/','\\\\'))
    if changed!=text:p.write_text(changed,encoding='utf-8')

# The export plan is an active input, so its paths are portable repo-relative
# locations. Historical commands in provenance remain historical records.
native=json.loads((ROOT/new).read_text())['animals'];sources={r['slug']:r['source'].replace('\\','/') for r in native}
p=ROOT/'assets-source/animals/export-metadata/export-plan.json';export=json.loads(p.read_text())
for row in export['animals']:row['path']=sources[row['slug']]
p.write_text(json.dumps(export,indent=2)+'\n')

# Rebased JSON is new metadata, not new geometry/audio. Update references to
# those metadata hashes, but never change immutable pack/member hashes. The
# publisher will create a small delta pack for changed payloads.
json_paths=[p for p in paths if p.suffix=='.json' and p.is_file() and p.relative_to(ROOT).as_posix() not in {'assets/game-assets.lock.json','assets/legacy-runtime-assets.lock.json','assets/project-layout.json','assets/player-storage-policy.json'} and not p.name.endswith('release-verification.json')]
changed_hashes={}
for iteration in range(8):
    for row in plan['files']:
        if Path(row['new']).suffix=='.json':
            p=ROOT/row['new'];now=sha(p)
            if now!=row['sha256']:changed_hashes[row['sha256']]=now
    changes=0
    pattern=re.compile(r'(?<=["\'])([0-9a-f]{64})(?=["\'])') if changed_hashes else None
    if pattern is None:break
    for p in json_paths:
        try:text=p.read_text(encoding='utf-8-sig')
        except UnicodeDecodeError:continue
        after=pattern.sub(lambda m:changed_hashes.get(m.group(0),m.group(0)),text)
        if after!=text:json.loads(after);p.write_text(after,encoding='utf-8');changes+=1
    if changes==0:break
else:raise RuntimeError('Metadata references did not converge')
(task/'metadata-rebase-proof.json').write_text(json.dumps({'changed_metadata_hashes':len(changed_hashes),'iterations':iteration+1,'geometry_textures_audio_modified':False},indent=2))
print('METADATA REBASED',len(changed_hashes),'hash references;',len(sources),'portable animal sources')
