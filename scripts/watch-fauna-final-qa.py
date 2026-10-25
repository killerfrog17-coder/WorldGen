"""Consume finished colour refreshes for motion checks and current lineage views.

Read-only with respect to native assets. A changed native SHA invalidates an old
QA receipt. Actual visual inspection remains a distinct manual step.
"""
import hashlib,json,os,subprocess,time
from datetime import datetime
from pathlib import Path
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024';out=root/'inspection'
blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
jobs=json.loads((root/'paint-queue.json').read_text())['jobs'];lock=out/'final-qa.worker.lock'
fd=os.open(lock,os.O_CREAT|os.O_EXCL|os.O_WRONLY);os.write(fd,str(os.getpid()).encode());os.close(fd)
status={'pid':os.getpid(),'total':len(jobs),'completed':[],'errors':{},'warnings':{},'current':None}
seen={}
def save():
    status['updated']=datetime.now().astimezone().isoformat();temp=out/'final-qa-progress.tmp';temp.write_text(json.dumps(status,indent=2)+'\n');temp.replace(out/'final-qa-progress.json')
def run(folder,script,args,log):
    with (folder/log).open('w',encoding='utf8') as stream:
        result=subprocess.run([str(blender),'--background','--threads','2','--python-exit-code','1','--python',str(repo/'scripts'/script),'--',*args],cwd=repo,stdout=stream,stderr=subprocess.STDOUT)
    if result.returncode:raise RuntimeError(script+' failed; '+log)
try:
    while True:
        paint=json.loads((root/'finish-progress.json').read_text());done=set(paint.get('completed',[]));worked=False
        for job in jobs:
            slug=job['slug'];folder=root/slug;blend=folder/'Animal - Painted Species Review.blend'
            if slug not in done or slug==paint.get('current'):continue
            stamp=(blend.stat().st_mtime_ns,blend.stat().st_size)
            if seen.get(slug)==stamp:continue
            proof=json.loads((folder/'paint-native-checks.json').read_text());digest=hashlib.sha256(blend.read_bytes()).hexdigest()
            if proof.get('checked_blend_sha256')!=digest:continue
            status['current']=slug;save();worked=True
            try:
                run(folder,'check-fauna-finish-motion.py',['--slugs',slug],'final-motion-qa.log')
                motion=json.loads((folder/'paint-motion-checks.json').read_text())
                if motion['warnings']:status['warnings'][slug]=motion['warnings']
                else:status['warnings'].pop(slug,None)
                state=json.loads((folder/'paint-state.json').read_text());coat=next((i for i,c in reversed(list(enumerate(state['coats']))) if 'hypothetical' in c['label'].lower()),len(state['coats'])-1)
                run(folder,'render-fauna-diagnostic.py',['--root',str(folder),'--blend-name',blend.name,'--coat',str(coat),'--view','side','--size','384','--tag','final-lineage'],'final-lineage-qa.log')
                suffix=f'-coat{coat}' if coat else '';path=folder/'previews'/f'1-Bind-side{suffix}-final-lineage.png'
                report={'checked_blend_sha256':digest,'all_sampled_source_clips_finite':motion['all_sampled_surfaces_finite'],'motion_warnings':motion['warnings'],'lineage_coat':coat,'lineage_label':state['coats'][coat]['label'],'lineage_render':str(path.relative_to(repo)),'lineage_render_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'actually_visually_inspected':False,'appearance_approved':False}
                (folder/'final-qa.json').write_text(json.dumps(report,indent=2)+'\n')
                if slug not in status['completed']:status['completed'].append(slug)
                status['errors'].pop(slug,None)
            except Exception as exc:status['errors'][slug]=str(exc)
            seen[slug]=stamp;save()
        if paint.get('stage')=='finished' and paint.get('current') is None and not worked:break
        if not worked:time.sleep(2)
    status.update(current=None,stage='finished');save()
finally:lock.unlink(missing_ok=True)
