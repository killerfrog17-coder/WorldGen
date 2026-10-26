"""Finite appearance rebuild/check/render queue. No animation mutation or GUI."""
import argparse,concurrent.futures,hashlib,json,os,subprocess,threading,time
from pathlib import Path
repo=Path(__file__).resolve().parent.parent;ap=argparse.ArgumentParser();ap.add_argument('slugs',nargs='+');ap.add_argument('--workers',type=int,default=2);a=ap.parse_args();base=repo/'artifacts/fauna-anatomy-revision';blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe';python=Path('E:/AI/WorldGen3D/rig-venv/Scripts/python.exe');progress=base/'anatomy-revision-progress.json';lock=threading.Lock();jobs={s:{'stage':'queued','appearance_approved':False} for s in a.slugs};start=time.time()
def update(slug,stage,**extra):
 with lock:
  jobs[slug].update(stage=stage,**extra);progress.write_text(json.dumps({'scope':'Finite appearance-only queue; technical completion never implies appearance approval.','elapsed_seconds':round(time.time()-start),'jobs':jobs},indent=2)+'\n');print(slug,stage,flush=True)

pipeline_files=sorted(set((repo/'scripts').glob('fauna_*.py'))|{repo/'scripts'/n for n in ['finish-selected-fauna-face.py','bake-fauna-projected-face.py','finish-fauna-traits.py','add-selected-fauna-appendages.py','check-fauna-visual-correction.py','render-fauna-eye-results.py']})
def pipeline_digest():
 h=hashlib.sha256()
 for p in pipeline_files:h.update(p.name.encode());h.update(p.read_bytes())
 return h.hexdigest()
source_digest=pipeline_digest()
def build(slug):
 root=base/slug;assert (root/'visual-source.json').exists();env=os.environ.copy();env.update(OMP_NUM_THREADS='2',OPENBLAS_NUM_THREADS='2');log=root/'anatomy-revision-build.log'
 def command(script,args):return [str(blender),'--background','--factory-startup','--threads','2','--python-exit-code','1','--python',str(repo/'scripts'/script),'--','--slug',slug,'--root',str(root),*args]
 stages=[('geometry_and_colour_uv',command('finish-selected-fauna-face.py',[])),('project_current_surface',[str(python),str(repo/'scripts/bake-fauna-projected-face.py'),'--slug',slug,'--root',str(root)]),('pack_current_paint',command('finish-selected-fauna-face.py',[]))]
 if slug in {'buffalo','reindeer','moose'}:stages.append(('fit_horn_variants',command('finish-fauna-traits.py',[])))
 if slug in {'wild-boar','mouflon','wild-goat','bison'}:stages.append(('fit_species_appendages',command('add-selected-fauna-appendages.py',[])))
 full_scale='3.0' if slug=='lynx' else '2.5' if slug in {'hare','rabbit'} else '1.5'
 stages.extend([('native_preservation_check',command('check-fauna-visual-correction.py',[])),('render_heads_and_coats',command('render-fauna-eye-results.py',['--coats'])),('render_full_head',command('render-fauna-eye-results.py',['--camera-scale',full_scale,'--output-root',str(root/'head-overview')]))])
 try:
  with log.open('w') as output:
   for stage,cmd in stages:
    assert pipeline_digest()==source_digest,'Appearance source changed during the queue; rerun from a stable source state'
    update(slug,stage,pipeline_sha256=source_digest);output.write('\nSTAGE '+stage+'\n');output.flush();result=subprocess.run(cmd,cwd=repo,env=env,stdout=output,stderr=subprocess.STDOUT)
    if result.returncode:raise RuntimeError(stage+' exit '+str(result.returncode))
    assert pipeline_digest()==source_digest,'Appearance source changed during a stage; do not publish this mixed-source result'
  proof=json.loads((root/'visual-native-check.json').read_text());assert proof['all_checks_passed'];update(slug,'technically_checked_visual_review_pending',native_sha256=proof['checked_blend_sha256']);return True
 except Exception as error:update(slug,'failed',error=str(error));return False
with concurrent.futures.ThreadPoolExecutor(max_workers=max(1,min(2,a.workers))) as pool:results=list(pool.map(build,a.slugs))
raise SystemExit(0 if all(results) else 1)
