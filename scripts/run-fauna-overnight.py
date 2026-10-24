"""Serial, resumable GPU queue. Plans/references are prepared by the agent.

No source painting or game integration. Keeps four low-poly candidates plus one
review, discards only its own high-poly/intermediate files after native reload.
"""
import argparse,datetime,json,os,subprocess,sys,time
from pathlib import Path

repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,default=repo/'artifacts/fauna-overnight-20261024');ap.add_argument('--watch',action='store_true');ap.add_argument('--only');ap.add_argument('--no-cached-inference',action='store_true')
a=ap.parse_args();root=a.root.resolve();root.mkdir(parents=True,exist_ok=True)
plan=json.loads((repo/'docs/fauna/asset-plan.json').read_text())['rows']
blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe';shape_python=repo/'artifacts/tools/triposg-review-venv/Scripts/python.exe';rig_python=Path('E:/AI/WorldGen3D/rig-venv/Scripts/python.exe')
env=os.environ.copy();temp=root/'temp';temp.mkdir(exist_ok=True);env['TMP']=str(temp);env['TEMP']=str(temp);env['OMP_NUM_THREADS']='3'
lock=root/'worker.lock';fd=os.open(lock,os.O_CREAT|os.O_EXCL|os.O_WRONLY);os.write(fd,str(os.getpid()).encode());os.close(fd)
state={'pid':os.getpid(),'started':datetime.datetime.now().astimezone().isoformat(),'animals':{},'current':None,'inference_serial':True,'painted':False}
status_path=root/'queue-state.json'
if status_path.exists():state['animals']=json.loads(status_path.read_text()).get('animals',{})
def pause_requested():
    control=root/'run-control.json'
    mode=json.loads(control.read_text()) if control.exists() else {}
    if mode.get('mode')=='pause':return True
    legacy=root/'PAUSE'
    if not legacy.exists():return False
    # Preserve the blocked-to-delete internal checkpoint as a record. This
    # exception never ignores an owner's empty/new PAUSE marker or JSON pause.
    internal=legacy.read_text().strip()=='Safe infrastructure checkpoint; resume immediately with new source dispatch.'
    return not (internal and mode.get('mode')=='run' and mode.get('ignore_completed_internal_checkpoint') is True)
def save():
    state['updated']=datetime.datetime.now().astimezone().isoformat();status_path.write_text(json.dumps(state,indent=2))
def run(command,log):
    state['phase']=log.stem;save()
    tool_names={Path(str(x)).name for x in command}
    minimum=5.8 if 'generate-deer-local.py' in tool_names or ('run-fauna-inference-batch.py' in tool_names and 'shape' in command) else 3.8 if 'deer-skin-ai.py' in tool_names or 'run-fauna-inference-batch.py' in tool_names else None
    if minimum:
        while True:
            free=int(subprocess.check_output(['nvidia-smi','--query-gpu=memory.free','--format=csv,noheader,nounits'],text=True).strip().splitlines()[0])/1024
            if free>=minimum:break
            state['phase']='waiting_for_gpu_memory';state['free_gpu_gib']=round(free,2);save()
            if pause_requested():raise RuntimeError('Owner-requested safe pause while waiting for GPU')
            time.sleep(10)
    print('STEP',state['current'],log.name,flush=True)
    with log.open('w') as f:r=subprocess.run([str(x) for x in command],cwd=repo,stdout=f,stderr=subprocess.STDOUT,env=env)
    if r.returncode:raise RuntimeError(f'Step failed {r.returncode}: {log}')
def native(script,args,log):run([blender,'--background','--factory-startup','--threads','3','--python-exit-code','1','--python',repo/'scripts'/script,'--',*args],log)
def source_for(entry):
    slug=entry['slug'];fam=entry['family']
    if entry['animal']=='Coastal squid (guild)':return None
    if slug in ('hare','rabbit'):return repo/'artifacts/fauna-motion-sources/rabbit'
    if slug=='squirrel':return repo/'artifacts/fauna-motion-sources/squirrel'
    if entry['animal'] in {'Mouse','Bank vole','Cotton rat','Grass rat','Lemming','Shrew','Jerboa','Agouti','Paca','Pika','Ground squirrel','Marmot','Nutria','Beaver'}:return repo/'artifacts/fauna-motion-sources/rat'
    key='rat' if fam in ('armoured_mammal','spiny_mammal') else 'fox' if fam in ('canid','felid','mammal','bear') else 'horse' if fam in ('ungulate','thickskin') else 'bird' if fam=='bat' else 'crocodile' if fam=='reptile' else fam
    p=repo/'artifacts/fauna-motion-sources'/key
    return p if (p/'source-rig.json').exists() else None
try:
    while not pause_requested():
        did=False
        for entry in plan:
            slug=entry['slug']
            if a.only and slug!=a.only:continue
            if entry['asset_status']!='queued' or entry['family']=='microfly':continue
            folder=root/slug;reference=root/'references'/f'{slug}.png';source=source_for(entry)
            previous=state['animals'].get(slug,{})
            if previous.get('status') in ('ready','failed'):continue
            if (folder/'native-checks.json').exists():state['animals'][slug]={'status':'ready','animal':entry['animal'],'review':str(folder/'Animal - Raw Audition.blend')};save();continue
            if not reference.exists() or source is None:continue
            if pause_requested():break
            # Space guard stops new work, never erases retained/user assets.
            import shutil
            if shutil.disk_usage(root).free<8*2**30:state['current']='disk_space_guard';save();raise RuntimeError('Less than8GiB free; paused without deleting retained work')
            did=True;state['current']=slug;folder.mkdir(exist_ok=True)
            state['animals'][slug]={'status':'working','animal':entry['animal'],'source':str(source),'started':datetime.datetime.now().astimezone().isoformat()};save()
            try:
                if not a.no_cached_inference:
                    jobs=[]
                    for i in range(1,5):
                        candidate=folder/f'candidate-{i}';candidate.mkdir(exist_ok=True);seed=735000+entry['priority_rank']*100+i
                        if not (candidate/'raw.glb').exists() and not (candidate/'model.glb').exists():jobs.append({'candidate':i,'output':str(candidate/'raw.glb'),'seed':seed})
                    if jobs:
                        manifest=folder/'shape-batch.json';manifest.write_text(json.dumps({'batch_root':str(root),'reference':str(reference),'jobs':jobs},indent=2))
                        run([shape_python,repo/'scripts/run-fauna-inference-batch.py','--manifest',manifest,'--kind','shape'],folder/'shape-batch.log')
                    if pause_requested():break
                    for i in range(1,5):
                        candidate=folder/f'candidate-{i}'
                        if not (candidate/'model.glb').exists():native('prepare-fauna-candidate.py',['--root',candidate,'--triangles',entry['triangle_budget']],candidate/'prepare.log')
                    jobs=[]
                    for i in range(1,5):
                        candidate=folder/f'candidate-{i}';seed=735000+entry['priority_rank']*100+i
                        if not (candidate/'generated-rig.npz').exists():jobs.append({'candidate':i,'output':str(candidate/'generated-rig.npz'),'mesh':str(candidate/'model.glb'),'seed':seed+100000})
                    if jobs:
                        manifest=folder/'rig-batch.json';manifest.write_text(json.dumps({'batch_root':str(root),'jobs':jobs},indent=2))
                        run([rig_python,repo/'scripts/run-fauna-inference-batch.py','--manifest',manifest,'--kind','rig'],folder/'rig-batch.log')
                    if pause_requested():break
                for i in range(1,5):
                    candidate=folder/f'candidate-{i}';candidate.mkdir(exist_ok=True);seed=735000+entry['priority_rank']*100+i
                    if not (candidate/'raw.glb').exists() and not (candidate/'model.glb').exists():
                        run([shape_python,repo/'scripts/generate-deer-local.py','--source',reference,'--repo',repo/'artifacts/tools/TripoSG','--weights','E:/AI/WorldGen3D/models/TripoSG','--output',candidate/'raw.glb','--seed',seed,'--steps','50','--depth','8','--resident-gpu','--infer'],candidate/'shape.log')
                    if not (candidate/'model.glb').exists():native('prepare-fauna-candidate.py',['--root',candidate,'--triangles',entry['triangle_budget']],candidate/'prepare.log')
                    if not (candidate/'generated-rig.npz').exists():run([rig_python,repo/'scripts/deer-skin-ai.py','--repo',repo/'artifacts/tools/SkinTokens','--weights','E:/AI/WorldGen3D/models/SkinTokens','--mesh',candidate/'model.glb','--output',candidate/'generated-rig.npz','--beams','5','--seed',seed+100000],candidate/'rig.log')
                    state['animals'][slug]['completed_raw_candidates']=i;save()
                    if pause_requested():break
                if pause_requested():break
                if not (folder/'Animal - Raw Audition.blend').exists():native('build-fauna-audition.py',['--root',folder,'--motion-root',source,'--animal',entry['animal'],'--fur',repo/'artifacts/fauna-assets/deer/fur-source.png'],folder/'assemble.log')
                native('check-fauna-audition.py',['--root',folder],folder/'check.log')
                # Only exact intermediate names created by this worker; no
                # recursive deletion or broad cleanup inside generated assets.
                for i in range(1,5):
                    for name in ('raw.glb','raw.latents.pt','reference-prepared.png'):
                        p=folder/f'candidate-{i}'/name
                        if p.exists():p.unlink()
                state['animals'][slug].update(status='ready',review=str(folder/'Animal - Raw Audition.blend'),finished=datetime.datetime.now().astimezone().isoformat());save();print('READY',slug,flush=True)
                subprocess.run([sys.executable,str(repo/'scripts/index-fauna-auditions.py'),'--root',str(root)],cwd=repo,env=env,check=True)
            except Exception as exc:
                state['animals'][slug].update(status='failed',error=str(exc));save();print('FAILED',slug,str(exc),flush=True)
        state['current']=None;save()
        if not a.watch or a.only:break
        if not did:time.sleep(10)
finally:
    state['current']=None;state['worker_exited']=True;save();lock.unlink(missing_ok=True)
