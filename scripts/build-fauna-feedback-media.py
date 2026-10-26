"""Finite two-worker media refresh for exact checked revision natives.

Does not approve art or publish. Manual visual evidence remains a separate gate.
"""
import argparse,hashlib,json,os,subprocess,time,threading
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
from PIL import Image
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-review-revisions';blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe';ap=argparse.ArgumentParser();ap.add_argument('--slugs',nargs='*');ap.add_argument('--exclude',nargs='*',default=[]);a=ap.parse_args();queue=json.loads((repo/'artifacts/fauna-painted-20261024/paint-queue.json').read_text())['jobs'];jobs=[]
for row in queue:
 slug=row['slug'];folder=root/slug;check=folder/'revision-native-check.json';native=folder/'Animal - Painted Species Review.blend'
 if slug in a.exclude or (a.slugs and slug not in a.slugs) or not check.exists():continue
 proof=json.loads(check.read_text())
 if proof.get('all_checks_passed') and proof['checked_blend_sha256']==hashlib.sha256(native.read_bytes()).hexdigest():jobs.append(slug)
lock=root/'media-worker.lock';fd=os.open(lock,os.O_CREAT|os.O_EXCL|os.O_WRONLY);os.write(fd,str(os.getpid()).encode());os.close(fd);status={'pid':os.getpid(),'total':len(jobs),'completed':[],'errors':{},'active':{}};mutex=threading.RLock();started=time.monotonic()
def save():
 with mutex:
  status['elapsed_seconds']=round(time.monotonic()-started,1);temp=root/'media-progress.tmp';temp.write_text(json.dumps(status,indent=2)+'\n');temp.replace(root/'media-progress.json')
def call(slug,script,args):
 with mutex:status['active'][slug]=script;save()
 with (root/slug/(Path(script).stem+'-phone.log')).open('w') as log:r=subprocess.run([str(blender),'--background','--threads','2','--python-exit-code','1','--python',str(repo/'scripts'/script),'--',*args],cwd=repo,stdout=log,stderr=subprocess.STDOUT)
 assert r.returncode==0,script+' failed'
def process(slug):
 try:
  folder=root/slug;native=folder/'Animal - Painted Species Review.blend';digest=hashlib.sha256(native.read_bytes()).hexdigest()
  for view in ['front','right','left']:
   png=folder/'eye-audit'/(view+'-after.png');Image.open(png).save(png.with_suffix('.webp'),quality=92,method=6)
  plan=folder/'phone-review-plan.json';call(slug,'prepare-fauna-feedback-plan.py',['--slug',slug,'--blend',str(native),'--out',str(plan),'--face-root',str(folder/'eye-audit')]);call(slug,'render-fauna-phone-animal.py',['--slug',slug,'--plan',str(plan),'--output-root',str(root/'phone-media')]);assert hashlib.sha256(native.read_bytes()).hexdigest()==digest
  with mutex:status['completed'].append(slug);print('REVISION MEDIA READY',slug,flush=True)
 except Exception as exc:
  with mutex:status['errors'][slug]=str(exc);print('ERROR',slug,str(exc),flush=True)
 finally:
  with mutex:status['active'].pop(slug,None);save()
try:
 with ThreadPoolExecutor(max_workers=2) as pool:list(pool.map(process,jobs))
 status['stage']='finished';save()
finally:lock.unlink(missing_ok=True)
