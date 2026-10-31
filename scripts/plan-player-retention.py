"""Inventory obsolete player packages; never archive disposable compiled assets."""
import argparse,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'artifacts/player-retention-20261031'
OUT.mkdir(exist_ok=True)
state=json.loads(subprocess.check_output(['powershell','-NoProfile','-Command',"$s=(New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path (Get-Location) 'WorldGen.exe - Shortcut.lnk')); [ordered]@{selected=$s.TargetPath;running=@(Get-CimInstance Win32_Process -Filter \"Name='WorldGen.exe' OR Name='WorldGen.Cli.exe'\" | ForEach-Object {$_.ExecutablePath})}|ConvertTo-Json -Compress"],cwd=ROOT,text=True))
roots=[ROOT/'artifacts',Path('D:/WorldGenBuildArtifacts'),Path('E:/WorldGen-builds')]
parser=argparse.ArgumentParser();parser.add_argument('--active',action='append',default=[]);args=parser.parse_args()
protected={Path(state['selected']).parent.resolve()}
protected.update((ROOT/'artifacts'/p).resolve() for p in args.active)
protected.update(Path(p).parent.resolve() for p in state['running'] if p)
inputs=[]
for name in ['game-assets.lock.json','legacy-runtime-assets.lock.json']:
    for key,row in json.loads((ROOT/'assets'/name).read_text())['files'].items():
        inputs.append((ROOT/key).resolve())
tracked=subprocess.check_output(['git','ls-files','-z'],cwd=ROOT).decode().split('\0')
inputs.extend((ROOT/p).resolve() for p in tracked if p and not p.endswith('.meta'))
packages={}
for root in roots:
    if not root.exists():continue
    for p in root.iterdir():
        if p.is_dir() and (p/'WorldGen.exe').is_file() and (p/'WorldGen_Data').is_dir():packages[p.resolve()]=p
rows=[]
def linked(p):
    return p.is_symlink() or getattr(p.lstat(),'st_reparse_tag',0) in {0xA0000003,0xA000000C}
for physical,alias in packages.items():
    files=list(physical.rglob('*'));reason=''
    if physical in protected:reason='Selected, running or current build workspace'
    elif any(p.is_relative_to(physical) for p in inputs):reason='Required source or remotely locked dependency inside package'
    elif any(p.is_file() and p.suffix.lower() in {'.blend','.fbx','.safetensors','.onnx'} for p in files):reason='Contains possible source art/model weights'
    elif any(p.is_file() and p.suffix.lower()=='.world' and p.relative_to(physical).as_posix()!='WorldGen_Data/StreamingAssets/world/Earth.world' for p in files):reason='Contains a non-default world'
    elif any((physical/n).is_dir() and any((physical/n).iterdir()) for n in ['worlds','saves','Saves','SaveGames']):reason='Contains possible personal saves'
    targets=[];retained=[]
    if not reason:
        for child in physical.iterdir():
            members=[child]+(list(child.rglob('*')) if child.is_dir() and not linked(child) else [])
            if any(linked(p) for p in members):retained.append(str(child));continue
            targets.append(str(child))
    rows.append({'path':str(physical),'alias':str(alias),'keep':bool(reason),'reason':reason,'targets':targets,'retained_linked_diagnostics':retained,'logical_bytes':sum(p.stat().st_size for p in files if p.is_file())})
report={'policy':'One selected playable player and one reusable workspace; no per-update binary archives','roots':[str(p.resolve()) for p in roots],'protected':[str(p) for p in protected],'packages':rows,'source_commit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()}
(OUT/'plan.json').write_text(json.dumps(report,indent=2))
for row in rows:print('KEEP' if row['keep'] else 'PRUNE',row['path'],round(row['logical_bytes']/2**30,2),'GiB',row['reason'],('linked diagnostics retained' if row['retained_linked_diagnostics'] else ''))
