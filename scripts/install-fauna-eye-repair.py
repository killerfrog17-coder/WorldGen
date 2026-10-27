"""Install verified animal data into canonical assets and selected existing players."""
import argparse,hashlib,json,os,shutil,subprocess
from pathlib import Path

repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-eye-closure-repair'
ap=argparse.ArgumentParser();ap.add_argument('--players',nargs='+',required=True);args=ap.parse_args()
validation=json.loads((root/'export-repair-validation.json').read_text());bundles=json.loads((root/'bundle-validation.json').read_text())
assert validation['count']==bundles['count']==177
command="Get-CimInstance Win32_Process | Where-Object {$_.Name -eq 'WorldGen.exe' -or ($_.Name -eq 'Unity.exe' -and $_.CommandLine.Replace('\\','/') -like '*WorldGen/unity/WorldGen.Unity*')} | Select-Object ProcessId,Name,CommandLine | ConvertTo-Json -Compress"
active=subprocess.check_output(['powershell','-NoProfile','-Command',command],text=True).strip()
assert not active,('Leave running player/editor alone. Install only after it exits.',active)
canonical=repo/'unity/WorldGen.Unity/Assets/StreamingAssets/fauna'
players=[]
for name in args.players:
    player=(repo/'artifacts'/name).resolve()
    assert player.is_relative_to((repo/'artifacts').resolve()) and player.name==name
    assert (player/'WorldGen.exe').is_file() and (player/'WorldGen_Data').is_dir(),player
    players.append(player)
stages=[]
for player in players:
    build=player/'build-inputs.json'
    if not build.exists():continue
    name=json.loads(build.read_text(encoding='utf-8-sig')).get('StagingDirectory')
    if not name:continue
    stage=Path(name).resolve()
    if stage.is_relative_to(Path('E:/AI/WorldGen3D').resolve()) and stage.name.startswith('build-stage-') and (stage/'WorldGen_Data/StreamingAssets/fauna').is_dir() and stage not in stages:stages.append(stage)
before={}
for player in players:before[str(player)]=hashlib.sha256((player/'WorldGen.exe').read_bytes()).hexdigest()
def replace(source,destination):
    destination.parent.mkdir(parents=True,exist_ok=True)
    if destination.is_file() and hashlib.sha256(destination.read_bytes()).digest()==hashlib.sha256(source.read_bytes()).digest():return
    temporary=destination.with_name(destination.name+'.eye-repair-tmp')
    shutil.copy2(source,temporary);assert hashlib.sha256(temporary.read_bytes()).digest()==hashlib.sha256(source.read_bytes()).digest()
    os.replace(temporary,destination)
rows=[]
for animal in validation['animals']:
    slug=animal['slug'];source=root/'bundles'/(slug+'.fauna');assert source.is_file(),slug
    sha=hashlib.sha256(source.read_bytes()).hexdigest()
    replace(source,canonical/source.name)
    sidecar=source.with_suffix(source.suffix+'.manifest')
    if sidecar.exists():replace(sidecar,canonical/sidecar.name)
    for player in [*players,*stages]:replace(source,player/'WorldGen_Data/StreamingAssets/fauna'/source.name)
    rows.append({'slug':slug,'bundle_sha256':sha,'source_native_sha256':animal['source_sha256']})
for source_name,destination_name in [('bundles','fauna'),('bundles.manifest','fauna.manifest')]:
    source=root/'bundles'/source_name
    if source.exists():replace(source,canonical/destination_name)
subprocess.run([__import__('sys').executable,str(repo/'scripts/package-fauna-runtime.py')],check=True)
for player in [*players,*stages]:
    dest=player/'WorldGen_Data/StreamingAssets/fauna'
    for name in ['assets.json','ATTRIBUTION.txt']:replace(canonical/name,dest/name)
    for animal in rows:assert hashlib.sha256((dest/(animal['slug']+'.fauna')).read_bytes()).hexdigest()==animal['bundle_sha256']
    if player in players:assert before[str(player)]==hashlib.sha256((player/'WorldGen.exe').read_bytes()).hexdigest(),'Player executable changed'
report={'count':len(rows),'canonical':str(canonical),'players':[str(p) for p in players],'existing_build_stages':[str(p) for p in stages],'assets_manifest_sha256':hashlib.sha256((canonical/'assets.json').read_bytes()).hexdigest(),'player_executables_unchanged':True,'animals':rows}
(root/'installation.json').write_text(json.dumps(report,indent=2))
print('FAUNA_EYE_REPAIR_INSTALLED',len(rows),*[p.name for p in players])
