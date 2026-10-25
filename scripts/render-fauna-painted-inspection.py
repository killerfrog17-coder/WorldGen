"""Render missing selected colour views and two moving poses for visual inspection.

Runs without changing native models or asserting art approval. This is a finite
CPU queue; errors are recorded per view and never interpreted as a pass.
"""
import hashlib,json,os,subprocess,time
from pathlib import Path
from datetime import datetime

repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024'
blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
out=root/'inspection';out.mkdir(exist_ok=True)
lock=out/'render.worker.lock';fd=os.open(lock,os.O_CREAT|os.O_EXCL|os.O_WRONLY)
os.write(fd,str(os.getpid()).encode());os.close(fd)
jobs=json.loads((root/'review-index.json').read_text())['ready']
report={'pid':os.getpid(),'total':len(jobs),'completed':[],'errors':{},'current':None,'appearance_approved':False}
def save():
    report['updated']=datetime.now().astimezone().isoformat()
    temp=out/'render-progress.tmp';temp.write_text(json.dumps(report,indent=2)+'\n');temp.replace(out/'render-progress.json')
try:
    for job in jobs:
        slug=job['slug'];folder=root/slug;report['current']=slug;save()
        source_path=folder/'selected-source-entry.json'
        if source_path.exists():source=json.loads(source_path.read_text())
        else:
            state=json.loads((folder/'paint-state.json').read_text())
            raw=json.loads((repo/'artifacts/fauna-overnight-20261024'/slug/'audition.json').read_text())
            source=next(e for e in raw['candidates'] if e['number']==state['source_candidate'])
        actions=source['actions'];clip=next((c for c in ['Run','Walk','Swim','Flight','Flap','Slither','Idle'] if c in actions),next(iter(actions)))
        blend=folder/'Animal - Painted Species Review.blend'
        digest=hashlib.sha256(blend.read_bytes()).hexdigest()
        evidence=[]
        poses=[('Bind',.5,v,'painted') for v in ['side','front','rear']]
        poses += [(clip,.25,'side','motion-quarter'),(clip,.75,'front','motion-threequarter')]
        for motion,phase,view,tag in poses:
            pattern=f'1-{motion}-{view}-{tag}.png'
            matches=list((folder/'previews').glob(pattern))
            if not matches:
                args=[str(blender),'--background','--threads','4','--python-exit-code','1','--python',str(repo/'scripts/render-fauna-diagnostic.py'),'--','--root',str(folder),'--blend-name',blend.name,'--view',view,'--clip',motion,'--phase',str(phase),'--size','384','--tag',tag]
                log=folder/f'inspection-{motion}-{view}-{tag}.log'
                with log.open('w',encoding='utf8') as stream:r=subprocess.run(args,cwd=repo,stdout=stream,stderr=subprocess.STDOUT)
                if r.returncode:report['errors'].setdefault(slug,[]).append(str(log.relative_to(repo)));save();continue
                matches=list((folder/'previews').glob(pattern))
            for image in matches:evidence.append({'path':str(image.relative_to(repo)),'sha256':hashlib.sha256(image.read_bytes()).hexdigest(),'clip':motion,'phase':phase,'view':view})
        (folder/'paint-inspection-frames.json').write_text(json.dumps({'blend_sha256':digest,'renders':evidence,'actually_inspected':False,'limits':'Images generated for inspection; no natural-motion/collision/contact or art acceptance inferred.'},indent=2)+'\n')
        report['completed'].append(slug);save()
    report['current']=None;report['stage']='finished';save()
finally:lock.unlink(missing_ok=True)
