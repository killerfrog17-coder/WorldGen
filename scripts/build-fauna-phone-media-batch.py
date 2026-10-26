"""Finite resumable selected-animal media rendering; no native asset mutations."""
import argparse,hashlib,json,os,subprocess,threading,time
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-phone-media';plan=json.loads((root/'plan.json').read_text());blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
ap=argparse.ArgumentParser();ap.add_argument('--slugs',nargs='*');ap.add_argument('--workers',type=int,default=2,choices=[1,2]);a=ap.parse_args();jobs=[x for x in plan['animals'] if not a.slugs or x['slug'] in a.slugs]
lock=root/'worker.lock';fd=os.open(lock,os.O_CREAT|os.O_EXCL|os.O_WRONLY);os.write(fd,str(os.getpid()).encode());os.close(fd)
status={'pid':os.getpid(),'total':len(jobs),'completed':[],'errors':{},'active':{},'expected_clips':sum(len(x['clips']) for x in jobs)};mutex=threading.RLock();started=time.monotonic()
signature=hashlib.sha256((repo/'scripts/render-fauna-phone-animal.py').read_bytes()).hexdigest()
def save():
 with mutex:
  status['elapsed_seconds']=round(time.monotonic()-started,1);p=root/'progress.json';temp=root/'progress.tmp';temp.write_text(json.dumps(status,indent=2)+'\n')
  for _ in range(25):
   try:temp.replace(p);return
   except PermissionError:time.sleep(.02)
def process(item):
 slug=item['slug'];folder=root/slug;folder.mkdir(exist_ok=True)
 with mutex:status['active'][slug]='rendering';save()
 try:
  receipt=folder/'receipt.json';ready=False
  if receipt.exists():
   r=json.loads(receipt.read_text());ready=r.get('complete') and r['source_sha256']==item['source_sha256'] and r['renderer_sha256']==signature
   if ready:ready=all((folder/v['file']).exists() and hashlib.sha256((folder/v['file']).read_bytes()).hexdigest()==v['sha256'] for v in r['stills']+r['videos'])
  if not ready:
   with (folder/'render.log').open('w') as stream:
    result=subprocess.run([str(blender),'--background','--threads','2','--python-exit-code','1','--python',str(repo/'scripts/render-fauna-phone-animal.py'),'--','--slug',slug],cwd=repo,stdout=stream,stderr=subprocess.STDOUT)
   assert result.returncode==0,'native rendering failed; see render.log'
   assert json.loads(receipt.read_text())['complete'],'incomplete media receipt'
  assert hashlib.sha256((repo/item['source']).read_bytes()).hexdigest()==item['source_sha256'],'native source changed'
  with mutex:status['completed'].append(slug);print('ANIMAL MEDIA READY',slug,flush=True)
 except Exception as exc:
  with mutex:status['errors'][slug]=str(exc);print('ERROR',slug,str(exc),flush=True)
 finally:
  with mutex:status['active'].pop(slug,None);save()
try:
 with ThreadPoolExecutor(max_workers=a.workers) as executor:list(executor.map(process,jobs))
 status['stage']='finished';save()
finally:lock.unlink(missing_ok=True)
