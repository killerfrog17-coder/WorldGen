"""Resumable, bounded CPU workers; each native gets an isolated Blender process."""
import argparse,concurrent.futures,hashlib,json,os,subprocess,time
from pathlib import Path
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-runtime-20261026'
ap=argparse.ArgumentParser();ap.add_argument('--workers',type=int,default=3);ap.add_argument('--slugs',nargs='*');ap.add_argument('--reuse-bakes',action='store_true');a=ap.parse_args()
plan=json.loads((root/'export-plan.json').read_text())['animals'];script=repo/'scripts/export-fauna-runtime.py'
surface=repo/'scripts/fauna_runtime_surface.py'
uv=repo/'scripts/fauna_runtime_uv.py'
signature=hashlib.sha256(script.read_bytes()+surface.read_bytes()+uv.read_bytes()).hexdigest();exe=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
env=dict(os.environ,OMP_NUM_THREADS='2',OPENBLAS_NUM_THREADS='2',MKL_NUM_THREADS='2',OPENIMAGEIO_THREADS='2')
def run(row):
    slug=row['slug'];folder=root/'exports'/slug;folder.mkdir(parents=True,exist_ok=True)
    assert hashlib.sha256(script.read_bytes()+surface.read_bytes()+uv.read_bytes()).hexdigest()==signature,'Exporter changed during queue'
    previous=folder/'receipt.json'
    reuse=bool(a.reuse_bakes and previous.exists() and json.loads(previous.read_text()).get('exporter_sha256')==signature)
    for attempt in range(2):
        with (folder/'export.log').open('w') as log:
            proc=subprocess.run([str(exe),'--background','--factory-startup','--disable-autoexec','--threads','2','--python-exit-code','1','--python',str(script),'--','--slugs',slug,*(['--reuse-bakes'] if reuse else [])],cwd=repo,stdout=log,stderr=subprocess.STDOUT,env=env)
        if proc.returncode==0:break
        print('RETRY',slug,proc.returncode,flush=True)
    receipt=folder/'receipt.json'
    ok=proc.returncode==0 and receipt.exists() and json.loads(receipt.read_text()).get('exporter_sha256')==signature
    print('READY' if ok else 'FAILED',slug,flush=True)
    return {'slug':slug,'ok':ok,'exit':proc.returncode}
rows=[r for r in plan if not a.slugs or r['slug'] in a.slugs];results=[]
with concurrent.futures.ThreadPoolExecutor(max_workers=max(1,min(4,a.workers))) as pool:
    for result in pool.map(run,rows):
        results.append(result);(root/'export-status.json').write_text(json.dumps({'results':results,'total':len(rows)},indent=2)+'\n')
assert all(r['ok'] for r in results),[r['slug'] for r in results if not r['ok']]
assert hashlib.sha256(script.read_bytes()+surface.read_bytes()+uv.read_bytes()).hexdigest()==signature
print('ALL RUNTIME EXPORTS READY',len(results),flush=True)
