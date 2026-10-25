"""Resumable CPU finishing worker: preparation/guides or fitted atlas finishing.

Only selected copies are written. Lock prevents duplicate workers for a phase;
failures are recorded and the remaining independent animals keep progressing.
No owner approval is inferred from a successful stage or elapsed time.
"""
import argparse,json,os,subprocess,sys,time
from datetime import datetime
from pathlib import Path

repo=Path(__file__).resolve().parent.parent
root=repo/'artifacts/fauna-painted-20261024'
blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
python=Path('E:/AI/WorldGen3D/rig-venv/Scripts/python.exe')
ap=argparse.ArgumentParser();ap.add_argument('--phase',choices=['guides','finish'],required=True)
ap.add_argument('--slugs',nargs='*');ap.add_argument('--limit',type=int)
ap.add_argument('--watch',action='store_true',help='Keep accepting inspected face guides until this whole finishing queue is processed')
ap.add_argument('--refresh',action='store_true',help='Re-bake specified working copies after an inspected recipe correction')
a=ap.parse_args()
sys.path.insert(0,str(repo/'scripts'))
from fauna_finish_catalog import SUPPORTED
queue=json.loads((root/'paint-queue.json').read_text())['jobs']
jobs=[j for j in queue if j['slug'] in SUPPORTED and (not a.slugs or j['slug'] in a.slugs)]
if a.limit:jobs=jobs[:a.limit]
lock=root/(a.phase+'.worker.lock')
fd=os.open(lock,os.O_CREAT|os.O_EXCL|os.O_WRONLY);os.write(fd,str(os.getpid()).encode());os.close(fd)
report={'phase':a.phase,'pid':os.getpid(),'started':datetime.now().astimezone().isoformat(),'total':len(jobs),'completed':[],'failed':{},'waiting_for_guides':[]}
status=root/(a.phase+'-progress.json');start=time.monotonic()
def save(slug=None,stage=None):
    report.update(current=slug,stage=stage,elapsed_seconds=round(time.monotonic()-start,1),updated=datetime.now().astimezone().isoformat())
    n=len(report['completed'])+len(report['failed'])
    report['estimated_remaining_seconds']=round(report['elapsed_seconds']/n*(len(jobs)-n)) if n else None
    temp=status.with_suffix('.tmp');temp.write_text(json.dumps(report,indent=2)+'\n');temp.replace(status)
def run(slug,script,args,log,blend=True):
    save(slug,script);folder=root/slug;folder.mkdir(exist_ok=True)
    cmd=[str(blender),'--background','--threads','2','--python-exit-code','1','--python',str(repo/'scripts'/script),'--',*args] if blend else [str(python),str(repo/'scripts'/script),*args]
    with (folder/log).open('w',encoding='utf8') as stream:
        result=subprocess.run(cmd,cwd=repo,stdout=stream,stderr=subprocess.STDOUT)
    if result.returncode:raise RuntimeError(f'{script}: exit {result.returncode}; {folder/log}')
def finished(slug):
    folder=root/slug;blend=folder/'Animal - Painted Species Review.blend';check=folder/'paint-native-checks.json'
    if not blend.exists() or not check.exists():return False
    import hashlib
    return json.loads(check.read_text()).get('checked_blend_sha256')==hashlib.sha256(blend.read_bytes()).hexdigest() and all((folder/f'previews/1-Bind-{view}-painted.png').exists() for view in ['side','front','rear'])

try:
    for job in jobs:
        slug=job['slug'];folder=root/slug
        try:
            if a.phase=='guides':
                if not (folder/'paint-state.json').exists():run(slug,'prepare-selected-fauna-paint.py',['--slug',slug],'prepare.log')
                for view in ['side','left','front']:
                    image=folder/f'previews/1-Bind-{view}-surface.png'
                    if not image.exists():run(slug,'render-fauna-diagnostic.py',['--root',str(folder),'--blend-name','Animal - Painted Species Review.blend','--view',view,'--surface-only','--size','640'],f'guide-{view}.log')
            else:
                if finished(slug) and not a.refresh:
                    report['completed'].append(slug);save();continue
                if not (folder/'face-guide-picks.json').exists():
                    report['waiting_for_guides'].append(slug);save();continue
                run(slug,'fit-selected-fauna-landmarks.py',['--slug',slug],'fit.log')
                run(slug,'bake-fauna-finish.py',['--slug',slug],'bake.log',False)
                run(slug,'finish-selected-fauna-face.py',['--slug',slug],'finish.log')
                run(slug,'check-selected-fauna-paint.py',['--slug',slug],'final-paint-check.log')
                for view in ['side','front','rear']:
                    run(slug,'render-fauna-diagnostic.py',['--root',str(folder),'--blend-name','Animal - Painted Species Review.blend','--view',view,'--size','512','--tag','painted'],f'painted-{view}.log')
            report['completed'].append(slug);print(a.phase,'completed',slug,flush=True)
        except Exception as exc:
            report['failed'][slug]=str(exc);print('FAILED',slug,exc,flush=True)
        save()
    save(stage='finished')
    while a.watch and a.phase=='finish' and report['waiting_for_guides']:
        pending=list(report['waiting_for_guides']);report['waiting_for_guides']=[]
        for slug in pending:
            folder=root/slug
            if not (folder/'face-guide-picks.json').exists():report['waiting_for_guides'].append(slug);continue
            try:
                run(slug,'fit-selected-fauna-landmarks.py',['--slug',slug],'fit.log')
                run(slug,'bake-fauna-finish.py',['--slug',slug],'bake.log',False)
                run(slug,'finish-selected-fauna-face.py',['--slug',slug],'finish.log')
                run(slug,'check-selected-fauna-paint.py',['--slug',slug],'final-paint-check.log')
                for view in ['side','front','rear']:
                    run(slug,'render-fauna-diagnostic.py',['--root',str(folder),'--blend-name','Animal - Painted Species Review.blend','--view',view,'--size','512','--tag','painted'],f'painted-{view}.log')
                report['completed'].append(slug);print('finish completed',slug,flush=True)
            except Exception as exc:report['failed'][slug]=str(exc)
        save(stage='waiting_for_inspected_guides' if report['waiting_for_guides'] else 'finished')
        if report['waiting_for_guides']:time.sleep(2)
finally:
    lock.unlink(missing_ok=True)
