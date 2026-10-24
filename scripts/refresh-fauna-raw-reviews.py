"""Refresh only unapproved raw night reviews after a shared viewer/demo correction."""
import argparse,json,subprocess,sys
from pathlib import Path
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-overnight-20261024'
ap=argparse.ArgumentParser();ap.add_argument('--all',action='store_true');a=ap.parse_args()
blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
queue=json.loads((root/'queue-state.json').read_text())['animals'];results=[]
for folder in sorted(root.iterdir()):
    meta=folder/'audition.json'
    if not meta.exists() or queue.get(folder.name,{}).get('status')!='ready':continue
    data=json.loads(meta.read_text());entries=data['candidates']
    if data.get('art_approved') or not (folder/'candidate-1/generated-rig.npz').exists():continue
    if not a.all and all(x.get('locomotion_sequence') for x in entries):continue
    assert folder.resolve().parent==root.resolve()
    bank=Path(data.get('motion_bank') or queue[folder.name].get('source') or repo/'artifacts/fauna-motion-sources'/entries[0]['source_family'])
    assert (bank/'source-rig.json').exists()
    result={'animal':data['animal'],'source_bank':str(bank),'status':'running'};results.append(result)
    for script,args,log in [('build-fauna-audition.py',['--root',str(folder),'--motion-root',str(bank),'--animal',data['animal'],'--fur',str(repo/'artifacts/fauna-assets/deer/fur-source.png'),'--rebuild-unreviewed'],'demo-refresh-build.log'),('check-fauna-audition.py',['--root',str(folder)],'demo-refresh-check.log')]:
        with (folder/log).open('w') as stream:
            p=subprocess.run([str(blender),'--background','--factory-startup','--disable-autoexec','--threads','2','--python-exit-code','1','--python',str(repo/'scripts'/script),'--',*args],cwd=repo,stdout=stream,stderr=subprocess.STDOUT)
        if p.returncode:result.update(status='failed',log=str(folder/log));break
    else:result['status']='checked'
    (root/'demo-refresh-results.json').write_text(json.dumps(results,indent=2));print(result['animal'],result['status'],flush=True)
    if result['status']=='failed':raise RuntimeError(result)
subprocess.run([sys.executable,str(repo/'scripts/index-fauna-auditions.py')],cwd=repo,check=True)
print('Unapproved raw demo refresh complete',len(results),flush=True)
