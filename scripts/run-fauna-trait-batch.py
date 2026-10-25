"""Finite selected-body appendage finishing/native check/render queue."""
import json,os,subprocess,sys
from datetime import datetime
from pathlib import Path
repo=Path(__file__).resolve().parent.parent;sys.path.insert(0,str(repo/'scripts'))
from fauna_trait_catalog import TRAITS
root=repo/'artifacts/fauna-painted-20261024';blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
lock=root/'traits.worker.lock';fd=os.open(lock,os.O_CREAT|os.O_EXCL|os.O_WRONLY);os.write(fd,str(os.getpid()).encode());os.close(fd)
status={'pid':os.getpid(),'total':len(TRAITS),'completed':[],'errors':{},'current':None}
def save():
    status['updated']=datetime.now().astimezone().isoformat();tmp=root/'traits-progress.tmp';tmp.write_text(json.dumps(status,indent=2)+'\n');tmp.replace(root/'traits-progress.json')
def run(slug,script,args,log):
    with (root/slug/log).open('w',encoding='utf8') as stream:
        r=subprocess.run([str(blender),'--background','--threads','4','--python-exit-code','1','--python',str(repo/'scripts'/script),'--',*args],cwd=repo,stdout=stream,stderr=subprocess.STDOUT)
    if r.returncode:raise RuntimeError(script+' failed: '+log)
try:
    for slug in TRAITS:
        status['current']=slug;save()
        try:
            run(slug,'finish-fauna-traits.py',['--slug',slug],'traits.log')
            run(slug,'check-selected-fauna-paint.py',['--slug',slug],'traits-check.log')
            for view,coat,variant,clip,phase,tag in [('side',0,0,'Bind',.5,'traits'),('front',0,0,'Bind',.5,'traits'),('front',0,1,'Bind',.5,'traits-compact'),('side',0,0,'Walk',.25,'traits-motion')]:
                args=['--root',str(root/slug),'--blend-name','Animal - Painted Species Review.blend','--view',view,'--coat',str(coat),'--extra-variant',str(variant),'--clip',clip,'--phase',str(phase),'--size','448','--tag',tag]
                run(slug,'render-fauna-diagnostic.py',args,f'traits-{view}-{tag}.log')
            status['completed'].append(slug)
        except Exception as exc:status['errors'][slug]=str(exc)
        save()
    status.update(current=None,stage='finished');save()
finally:lock.unlink(missing_ok=True)
