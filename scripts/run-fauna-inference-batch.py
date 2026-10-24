"""Reuse inference weights within one serial four-candidate animal batch."""
import argparse,importlib.util,json,os,sys,time
from pathlib import Path

ap=argparse.ArgumentParser();ap.add_argument('--manifest',type=Path,required=True);ap.add_argument('--kind',choices=['shape','rig'],required=True)
a=ap.parse_args();manifest=json.loads(a.manifest.resolve().read_text());repo=Path(__file__).resolve().parent.parent
script=repo/'scripts'/('generate-deer-local.py' if a.kind=='shape' else 'deer-skin-ai.py')
spec=importlib.util.spec_from_file_location('fauna_batch_inference',script);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
base=Path(manifest['batch_root']);started=time.perf_counter();initial_dir=Path.cwd()
def paused():
    p=base/'run-control.json'
    if p.exists() and json.loads(p.read_text()).get('mode')=='pause':return True
    legacy=base/'PAUSE'
    if legacy.exists():
        internal=legacy.read_text().strip()=='Safe infrastructure checkpoint; resume immediately with new source dispatch.'
        control=json.loads(p.read_text()) if p.exists() else {}
        if not (internal and control.get('ignore_completed_internal_checkpoint') and control.get('mode')=='run'):return True
    return False
for item in manifest['jobs']:
    if paused():break
    out=Path(item['output'])
    if out.exists():continue
    os.chdir(initial_dir)
    if a.kind=='shape':
        sys.argv=[str(script),'--source',manifest['reference'],'--repo',str(repo/'artifacts/tools/TripoSG'),'--weights','E:/AI/WorldGen3D/models/TripoSG','--output',str(out),'--seed',str(item['seed']),'--steps','50','--depth','8','--resident-gpu','--cache-resident','--infer']
    else:
        sys.argv=[str(script),'--repo',str(repo/'artifacts/tools/SkinTokens'),'--weights','E:/AI/WorldGen3D/models/SkinTokens','--mesh',item['mesh'],'--output',str(out),'--seed',str(item['seed']),'--beams','5','--cache-model']
    print('BATCH',a.kind,item['candidate'],'seed',item['seed'],flush=True);module.main()
os.chdir(initial_dir)
print('Inference batch finished',a.kind,round(time.perf_counter()-started,2),'seconds',flush=True)
