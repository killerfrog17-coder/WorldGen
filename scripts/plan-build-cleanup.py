"""Read-only build inventory and historical asset audit before package cleanup."""
import argparse
import concurrent.futures
import hashlib
import json
import re
import subprocess
from pathlib import Path

from game_asset_inventory import ROOT,sha


def asset_files(package):
    data=package/'WorldGen_Data'
    if not data.is_dir():
        return []
    files=[]
    for p in data.rglob('*'):
        if not p.is_file():
            continue
        relative=p.relative_to(data).as_posix()
        if relative.startswith('StreamingAssets/'):
            if p.suffix.lower() not in {'.dll','.exe','.pdb','.key','.pem'}:
                files.append(p)
        elif p.parent==data and (p.suffix.lower() in {'.assets','.ress','.resource','.unity3d'}):
            files.append(p)
    return files


parser=argparse.ArgumentParser()
parser.add_argument('--keep',type=Path,action='append',default=[])
parser.add_argument('--output',type=Path,required=True)
parser.add_argument('--legacy-inventory',type=Path,required=True)
args=parser.parse_args()
args.output.parent.mkdir(parents=True,exist_ok=True)
state=json.loads(subprocess.check_output(['powershell','-NoProfile','-Command',"$s=(New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path (Get-Location) 'WorldGen.exe - Shortcut.lnk')); [ordered]@{selected=$s.TargetPath; running=@(Get-CimInstance Win32_Process -Filter \"Name='WorldGen.exe'\" | ForEach-Object {$_.ExecutablePath})} | ConvertTo-Json -Compress"],cwd=ROOT,text=True))
protected={p.resolve() for p in args.keep}
if state['selected']:
    protected.add(Path(state['selected']).parent.resolve())
for p in state['running']:
    if p:
        protected.add(Path(p).parent.resolve())
manifest=json.loads((ROOT/'assets/game-assets.lock.json').read_text())
inputs=[(ROOT/p).resolve() for p in manifest['files']]
roots=[ROOT/'artifacts',Path('E:/WorldGen-builds'),Path('C:/WorldGenBuildArtifacts')]
packages={}
discovered=[]
for root in roots:
    if not root.is_dir():
        continue
    result=subprocess.run(['rg','--files','--hidden','--no-ignore','--follow','-g','WorldGen.exe','-g','!**/Library/**','-g','!**/tools/**','-g','!**/bin/**','-g','!**/obj/**','-g','!**/.git/**',str(root)],capture_output=True,text=True)
    discovered.extend(Path(line).parent for line in result.stdout.splitlines() if line.strip())
for directory in discovered:
        if not (directory/'WorldGen.exe').is_file() or not (directory/'WorldGen_Data').is_dir():
            continue
        physical=directory.resolve()
        if physical in [p.resolve() for p in roots] or not any(physical.is_relative_to(p.resolve()) for p in roots):
            continue
        entry=packages.setdefault(str(physical),{'path':str(physical),'aliases':[],'name':directory.name})
        if str(directory) not in entry['aliases']:
            entry['aliases'].append(str(directory))
rows=[]
legacy=[]
snapshots={}
for entry in packages.values():
    directory=Path(entry['path'])
    reason=''
    if directory in protected:
        reason='Selected, running or active work'
    elif any(p.is_relative_to(directory) for p in inputs):
        reason='Contains required asset input'
    else:
        for p in directory.rglob('*.world'):
            if p.relative_to(directory).as_posix()!='WorldGen_Data/StreamingAssets/world/Earth.world':
                reason='Contains a non-default world/save';break
        for name in ['world','worlds','saves','Saves','SaveGames']:
            if (directory/name).is_dir() and any((directory/name).iterdir()):
                reason='Contains a possible world/save library'
    entry['keep']=bool(reason);entry['reason']=reason
    entry['bytes']=sum(p.stat().st_size for p in directory.rglob('*') if p.is_file())
    if reason:
        rows.append(entry);continue
    assets=asset_files(directory)
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        digests=list(pool.map(sha,assets))
    entries=sorted([(p.relative_to(directory/'WorldGen_Data').as_posix(),p,digest) for p,digest in zip(assets,digests)])
    snapshot=hashlib.sha256('\n'.join(path+'|'+digest for path,_,digest in entries).encode()).hexdigest()
    if snapshot not in snapshots:
        snapshots[snapshot]={'packages':[],'files':len(entries),'bytes':sum(p.stat().st_size for _,p,_ in entries)}
        for relative,p,digest in entries:
            legacy.append({'path':'asset-history/'+snapshot+'/'+relative,'source_path':str(p),'group':'legacy-runtime-assets','bytes':p.stat().st_size,'sha256':digest,'tracked':False})
    snapshots[snapshot]['packages'].append(entry['name'])
    entry['asset_snapshot']=snapshot
    metadata=directory/'build-inputs.json'
    if metadata.is_file():
        entry['build']=json.loads(metadata.read_text(encoding='utf-8-sig'))
    entry['source_commits']=[]
    for candidate in [directory/'WorldGen_Data/Managed/WorldGen.Core.dll',directory/'WorldGen_BackUpThisFolder_ButDontShipItWithYourGame/Managed/WorldGen.Core.dll',directory/'WorldGen_Data/il2cpp_data/Metadata/global-metadata.dat']:
        if candidate.is_file():
            entry['source_commits']=sorted({m.decode() for m in re.findall(rb'1\.0\.0\+([0-9a-f]{40})',candidate.read_bytes())})
            if entry['source_commits']:
                break
    rows.append(entry)
report={'selected':state['selected'],'protected':[str(p) for p in sorted(protected)],'roots':[str(p) for p in roots],'packages':rows,'historical_asset_snapshots':snapshots,'deletable_packages':sum(not r['keep'] for r in rows),'deletable_bytes':sum(r['bytes'] for r in rows if not r['keep'])}
args.output.write_text(json.dumps(report,indent=2))
args.legacy_inventory.write_text(json.dumps(legacy,indent=2))
print(json.dumps({'packages':len(rows),'deletable_packages':report['deletable_packages'],'deletable_GiB':report['deletable_bytes']/2**30,'historical_asset_snapshots':len(snapshots),'historical_files':len(legacy),'historical_unique_payloads':len({r['sha256'] for r in legacy})}))
