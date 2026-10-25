"""Finite resumable eye corrections only after independently inspected socket picks."""
import argparse,hashlib,json,os,subprocess,time,threading
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024'
from fauna_eye_profiles import pipeline_signature
code_signature=pipeline_signature(repo)
blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe';python='E:/AI/WorldGen3D/rig-venv/Scripts/python.exe'
ap=argparse.ArgumentParser();ap.add_argument('--slugs',nargs='*');ap.add_argument('--workers',type=int,choices=[1,2],default=1);a=ap.parse_args()
jobs=json.loads((root/'paint-queue.json').read_text())['jobs'];jobs=[j for j in jobs if not a.slugs or j['slug'] in a.slugs]
lock=root/'eye.worker.lock';fd=os.open(lock,os.O_CREAT|os.O_EXCL|os.O_WRONLY);os.write(fd,str(os.getpid()).encode());os.close(fd)
progress=root/'inspection/eye-revision-progress.json';status={'pid':os.getpid(),'total':len(jobs),'completed':[],'waiting':[],'errors':{},'active':{}};start=time.monotonic();mutex=threading.RLock()
def save():
 with mutex:
  status['elapsed_seconds']=round(time.monotonic()-start,1);status['current']=list(status['active']);temp=progress.with_suffix('.tmp');temp.write_text(json.dumps(status,indent=2)+'\n')
  # Windows readers can briefly deny replacement. Reporting must never cancel
  # a valid per-animal job or leave the rest of a finite batch unprocessed.
  for attempt in range(25):
   try:temp.replace(progress);return
   except PermissionError:time.sleep(.02)
  print('Progress temporarily locked; continuing asset work',flush=True)
def run(folder,script,args,native=True):
 with mutex:status['active'][folder.name]=script;save()
 cmd=[str(blender),'--background','--threads','2','--python-exit-code','1','--python',str(repo/'scripts'/script),'--',*args] if native else [python,str(repo/'scripts'/script),*args]
 with (folder/'eye-audit'/f'{Path(script).stem}.log').open('w') as stream:result=subprocess.run(cmd,cwd=repo,stdout=stream,stderr=subprocess.STDOUT)
 assert result.returncode==0,f'{script} exit {result.returncode}'
def process(job):
  slug=job['slug'];folder=root/slug
  with mutex:status['active'][slug]='starting';save()
  try:
   config=folder/'eye-review.json'
   if not config.exists():status['waiting'].append(slug);return
   review=json.loads(config.read_text());assert review.get('inspected')
   signature=hashlib.sha256(config.read_bytes()).hexdigest();receipt=folder/'eye-audit/revision.json';blend=folder/'Animal - Painted Species Review.blend'
   if receipt.exists():
    prior=json.loads(receipt.read_text())
    if prior.get('pipeline_sha256')==code_signature and prior.get('review_sha256')==signature and prior.get('blend_sha256')==hashlib.sha256(blend.read_bytes()).hexdigest():status['completed'].append(slug);return
   run(folder,'fit-fauna-reviewed-eyes.py',['--slug',slug])
   import sys;sys.path.insert(0,str(repo/'scripts'));from fauna_finish_catalog import SUPPORTED
   size=json.loads((folder/'coat-bake.json').read_text())['size']
   run(folder,'bake-fauna-finish.py' if slug in SUPPORTED else 'bake-selected-fauna-coats.py',['--slug',slug,'--size',str(size)],False)
   run(folder,'finish-selected-fauna-face.py',['--slug',slug])
   run(folder,'check-selected-fauna-paint.py',['--slug',slug])
   run(folder,'render-fauna-eye-results.py',['--slug',slug])
   receipt.write_text(json.dumps({'pipeline_sha256':code_signature,'review_sha256':signature,'blend_sha256':hashlib.sha256(blend.read_bytes()).hexdigest(),'native_checks':'pass','actual_after_vision_pending':True},indent=2)+'\n')
   status['completed'].append(slug);print('EYE REVISION',slug,flush=True)
  except Exception as exc:status['errors'][slug]=str(exc);print('ERROR',slug,repr(exc),flush=True)
  finally:
   with mutex:status['active'].pop(slug,None);save()
try:
 with ThreadPoolExecutor(max_workers=a.workers) as executor:list(executor.map(process,jobs))
 status['stage']='finished';save()
finally:lock.unlink(missing_ok=True)
