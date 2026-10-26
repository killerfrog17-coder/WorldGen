"""Finite two-worker unpublished eye/pattern revisions; source assets preserved."""
import argparse,hashlib,json,os,subprocess,time,threading
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-review-revisions';root.mkdir(exist_ok=True);base=repo/'artifacts/fauna-painted-20261024';python='E:/AI/WorldGen3D/rig-venv/Scripts/python.exe';blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe';ap=argparse.ArgumentParser();ap.add_argument('--slugs',nargs='*');ap.add_argument('--workers',type=int,default=2,choices=[1,2]);a=ap.parse_args();jobs=json.loads((base/'paint-queue.json').read_text())['jobs'];jobs=[j for j in jobs if not a.slugs or j['slug'] in a.slugs];lock=root/'worker.lock';fd=os.open(lock,os.O_CREAT|os.O_EXCL|os.O_WRONLY);os.write(fd,str(os.getpid()).encode());os.close(fd);status={'pid':os.getpid(),'total':len(jobs),'completed':[],'surface_only':[],'errors':{},'active':{}};mutex=threading.RLock();started=time.monotonic()
names=['prepare-paired-fauna-eyes.py','fit-fauna-head-plane.py','cast-paired-head-eyes.py','finish-selected-fauna-face.py','fauna_paint_recipes.py','fauna_hypothetical_coats.py','bake-selected-fauna-coats.py','bake-fauna-finish.py','bake-selected-fauna-bird.py','bake-selected-fauna-fish.py','render-fauna-eye-results.py','check-paired-fauna-revision.py'];code=hashlib.sha256(b''.join((repo/'scripts'/n).read_bytes() for n in names)).hexdigest()
def save():
 with mutex:
  status['elapsed_seconds']=round(time.monotonic()-started,1);p=root/'progress.json';tmp=root/'progress.tmp';tmp.write_text(json.dumps(status,indent=2)+'\n')
  for _ in range(25):
   try:tmp.replace(p);return
   except PermissionError:time.sleep(.02)
def run(slug,script,args,native=False):
 folder=root/slug;folder.mkdir(exist_ok=True);cmd=[str(blender),'--background','--threads','2','--python-exit-code','1','--python',str(repo/'scripts'/script),'--',*args] if native else [python,str(repo/'scripts'/script),*args]
 with mutex:status['active'][slug]=script;save()
 with (folder/(Path(script).stem+'.log')).open('w') as log:r=subprocess.run(cmd,cwd=repo,stdout=log,stderr=subprocess.STDOUT)
 assert r.returncode==0,script+' failed; see log'
def process(job):
 slug=job['slug'];folder=root/slug;folder.mkdir(exist_ok=True)
 try:
  state=json.loads((base/slug/'paint-state.json').read_text())
  if state.get('facial_feature_policy')=='surface_only_arthropod':
   with mutex:status['surface_only'].append(slug);save()
   return
  receipt=folder/'build-receipt.json';native=folder/'Animal - Painted Species Review.blend'
  if receipt.exists():
   old=json.loads(receipt.read_text())
   if old['pipeline_sha256']==code and native.exists() and old['blend_sha256']==hashlib.sha256(native.read_bytes()).hexdigest():
    with mutex:status['completed'].append(slug);save()
    return
  run(slug,'prepare-paired-fauna-eyes.py',['--slugs',slug])
  if slug not in {'red-fox','wolf'} or not (folder/'head-plane-fit.json').exists():run(slug,'fit-fauna-head-plane.py',['--slug',slug])
  run(slug,'cast-paired-head-eyes.py',['--slug',slug],True)
  import sys;sys.path.insert(0,str(repo/'scripts'));from fauna_finish_catalog import SUPPORTED
  size=json.loads((base/slug/'coat-bake.json').read_text())['size'];run(slug,'bake-fauna-finish.py' if slug in SUPPORTED else 'bake-selected-fauna-coats.py',['--slug',slug,'--size',str(size),'--root',str(folder)])
  run(slug,'finish-selected-fauna-face.py',['--slug',slug,'--root',str(folder)],True)
  run(slug,'check-paired-fauna-revision.py',['--slug',slug],True)
  run(slug,'render-fauna-eye-results.py',['--slug',slug,'--root',str(folder)],True)
  run(slug,'render-fauna-eye-results.py',['--slug',slug,'--root',str(folder),'--sculpt'],True)
  receipt.write_text(json.dumps({'pipeline_sha256':code,'blend_sha256':hashlib.sha256(native.read_bytes()).hexdigest(),'actual_visual_review_pending':True},indent=2)+'\n')
  with mutex:status['completed'].append(slug);print('PAIRED REVIEW BUILT',slug,flush=True)
 except Exception as exc:
  with mutex:status['errors'][slug]=str(exc);print('ERROR',slug,str(exc),flush=True)
 finally:
  with mutex:status['active'].pop(slug,None);save()
try:
 with ThreadPoolExecutor(max_workers=a.workers) as pool:list(pool.map(process,jobs))
 status['stage']='finished';save()
finally:lock.unlink(missing_ok=True)
