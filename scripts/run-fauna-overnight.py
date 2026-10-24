"""Serial, resumable GPU queue. Plans/references are prepared by the agent.

No source painting or game integration. Keeps four low-poly candidates plus one
review, discards only its own high-poly/intermediate files after native reload.
"""
import argparse,datetime,json,os,subprocess,sys,time
from pathlib import Path
from fauna_motion_sources import source_for as resolve_source
from fauna_rig_topology import minimum_leaves,topology,quarantine

repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,default=repo/'artifacts/fauna-overnight-20261024');ap.add_argument('--watch',action='store_true');ap.add_argument('--only');ap.add_argument('--no-cached-inference',action='store_true');ap.add_argument('--motion-root',type=Path)
a=ap.parse_args();root=a.root.resolve();root.mkdir(parents=True,exist_ok=True)
if a.motion_root:
    if not a.only:ap.error('--motion-root is a single-animal technical trial and requires --only')
    a.motion_root=a.motion_root.resolve()
    assert a.motion_root.is_relative_to(repo/'artifacts/fauna-motion-sources')
    assert all((a.motion_root/n).exists() for n in ('source-rig.json','source-motions.npz'))
plan=json.loads((repo/'docs/fauna/asset-plan.json').read_text())['rows']
# Finish motion-backed auditions before spending GPU time on source-gap animals.
# Those still receive independent raw meshes/rigs, but never a false "animated"
# completion label or permission to paint.
plan.sort(key=lambda row:(row['asset_status']=='queued' and row['family']!='microfly' and resolve_source(row,repo) is None,row['priority_rank']))
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

def prepare_candidate(candidate,entry):
    if not (candidate/'mesh-component-cleanup.json').exists():run([rig_python,repo/'scripts/clean-fauna-raw-components.py','--root',candidate,'--triangles',entry['triangle_budget']],candidate/'component-cleanup.log')
    native('prepare-fauna-candidate.py',['--root',candidate,'--triangles',entry['triangle_budget']],candidate/'prepare.log')
def source_for(entry):
    return resolve_source(entry,repo)

def candidate_seeds(entry,number,candidate):
    seed=735000+entry['priority_rank']*100+number;override=candidate/'seed-overrides.json'
    values=json.loads(override.read_text()) if override.exists() else {}
    shape=int(values.get('shape_seed',seed));rig=int(values.get('rig_seed',shape+100000))
    return shape,rig
try:
    while not pause_requested():
        did=False
        for entry in plan:
            slug=entry['slug']
            if a.only and slug!=a.only:continue
            if entry['asset_status']!='queued' or entry['family']=='microfly':continue
            folder=root/slug;reference=root/'references'/f'{slug}.png';source=a.motion_root or source_for(entry)
            rebuild_marker=folder/'review-rebuild.json'
            trial_marker=folder/'source-trial-pending.json'
            previous=state['animals'].get(slug,{})
            if trial_marker.exists() and not a.motion_root:continue
            if previous.get('status')=='failed':
                # An explicit agent-prepared recovery can be queued without
                # racing this producer's in-memory/JSON state. Only this worker
                # consumes it, after verifying every repaired cached mesh.
                recovery=folder/'retry-failed.json'
                if not recovery.exists():continue
                import hashlib
                request=json.loads(recovery.read_text())
                assert request.get('reason') and len(request['prepared_models'])==4
                for number,digest in enumerate(request['prepared_models'],1):
                    mesh=folder/f'candidate-{number}'/'model.glb'
                    assert hashlib.sha256(mesh.read_bytes()).hexdigest()==digest,'Recovery mesh changed after request'
                request['previous_failure']=previous
                archive=folder/('recovery-consumed-'+str(time.time_ns())+'.json')
                archive.write_text(json.dumps(request,indent=2));recovery.unlink()
            if previous.get('status')=='ready' and not rebuild_marker.exists():continue
            if previous.get('status')=='models_rigged_motion_pending' and source is None:continue
            if (folder/'native-checks.json').exists() and not rebuild_marker.exists():state['animals'][slug]={'status':'ready','animal':entry['animal'],'review':str(folder/'Animal - Raw Audition.blend')};save();continue
            if not reference.exists():continue
            if pause_requested():break
            # Space guard stops new work, never erases retained/user assets.
            import shutil
            if shutil.disk_usage(root).free<8*2**30:state['current']='disk_space_guard';save();raise RuntimeError('Less than8GiB free; paused without deleting retained work')
            did=True;state['current']=slug;folder.mkdir(exist_ok=True)
            if a.motion_root:
                assert not (folder/'native-checks.json').exists(),'Do not overwrite an existing review with a new source trial'
                trial_marker.write_text(json.dumps({'animal':entry['animal'],'motion_source':str(source),'technical_source_review_pending':True,'art_approved':False},indent=2))
            state['animals'][slug]={'status':'working','animal':entry['animal'],'source':str(source) if source else None,'started':datetime.datetime.now().astimezone().isoformat()};save()
            try:
                if not a.no_cached_inference:
                    jobs=[]
                    for i in range(1,5):
                        candidate=folder/f'candidate-{i}';candidate.mkdir(exist_ok=True);seed,rig_seed=candidate_seeds(entry,i,candidate)
                        if not (candidate/'raw.glb').exists() and not (candidate/'model.glb').exists():jobs.append({'candidate':i,'output':str(candidate/'raw.glb'),'seed':seed})
                    if jobs:
                        manifest=folder/'shape-batch.json';manifest.write_text(json.dumps({'batch_root':str(root),'reference':str(reference),'jobs':jobs},indent=2))
                        run([shape_python,repo/'scripts/run-fauna-inference-batch.py','--manifest',manifest,'--kind','shape'],folder/'shape-batch.log')
                    if pause_requested():break
                    for i in range(1,5):
                        candidate=folder/f'candidate-{i}'
                        if not (candidate/'model.glb').exists():prepare_candidate(candidate,entry)
                    jobs=[]
                    for i in range(1,5):
                        candidate=folder/f'candidate-{i}';seed,rig_seed=candidate_seeds(entry,i,candidate)
                        rig_path=candidate/'generated-rig.npz';required=minimum_leaves(entry)
                        if rig_path.exists() and not topology(rig_path,required,entry['family'])['pass']:quarantine(rig_path,root)
                        if not rig_path.exists():jobs.append({'candidate':i,'output':str(rig_path),'mesh':str(candidate/'model.glb'),'seed':rig_seed,'minimum_leaves':required,'family':entry['family']})
                    if jobs:
                        manifest=folder/'rig-batch.json';manifest.write_text(json.dumps({'batch_root':str(root),'jobs':jobs},indent=2))
                        run([rig_python,repo/'scripts/run-fauna-inference-batch.py','--manifest',manifest,'--kind','rig'],folder/'rig-batch.log')
                    if pause_requested():break
                for i in range(1,5):
                    candidate=folder/f'candidate-{i}';candidate.mkdir(exist_ok=True);seed,rig_seed=candidate_seeds(entry,i,candidate)
                    if not (candidate/'raw.glb').exists() and not (candidate/'model.glb').exists():
                        run([shape_python,repo/'scripts/generate-deer-local.py','--source',reference,'--repo',repo/'artifacts/tools/TripoSG','--weights','E:/AI/WorldGen3D/models/TripoSG','--output',candidate/'raw.glb','--seed',seed,'--steps','50','--depth','8','--resident-gpu','--infer'],candidate/'shape.log')
                    if not (candidate/'model.glb').exists():prepare_candidate(candidate,entry)
                    if not (candidate/'generated-rig.npz').exists():run([rig_python,repo/'scripts/deer-skin-ai.py','--repo',repo/'artifacts/tools/SkinTokens','--weights','E:/AI/WorldGen3D/models/SkinTokens','--mesh',candidate/'model.glb','--output',candidate/'generated-rig.npz','--beams','5','--seed',rig_seed],candidate/'rig.log')
                    state['animals'][slug]['completed_raw_candidates']=i;save()
                    if pause_requested():break
                if pause_requested():break
                if source is None:
                    report={'animal':entry['animal'],'status':'models_rigged_motion_pending','animated_review_ready':False,'painted':False,
                      'candidates':[{'number':i,'mesh':str(folder/f'candidate-{i}'/'model.glb'),'rig':str(folder/f'candidate-{i}'/'generated-rig.npz'),'topology':topology(folder/f'candidate-{i}'/'generated-rig.npz',minimum_leaves(entry),entry['family'])} for i in range(1,5)],
                      'missing':'Suitable CC0 motion source; model approval/painting still waits for motion review'}
                    (folder/'raw-motion-pending.json').write_text(json.dumps(report,indent=2))
                    for i in range(1,5):
                        for name in ('raw.glb','raw-clean.glb','raw.latents.pt','reference-prepared.png'):
                            p=folder/f'candidate-{i}'/name
                            if p.exists():p.unlink()
                    state['animals'][slug].update(status='models_rigged_motion_pending',finished=datetime.datetime.now().astimezone().isoformat());save()
                    print('RAW MODELS/RIGS READY; MOTION STILL MISSING',slug,flush=True)
                    subprocess.run([sys.executable,str(repo/'scripts/index-fauna-auditions.py'),'--root',str(root)],cwd=repo,env=env,check=True)
                    continue
                if not (folder/'Animal - Raw Audition.blend').exists() or rebuild_marker.exists():native('build-fauna-audition.py',['--root',folder,'--motion-root',source,'--animal',entry['animal'],'--fur',repo/'artifacts/fauna-assets/deer/fur-source.png',*(['--rebuild-unreviewed'] if (folder/'Animal - Raw Audition.blend').exists() else [])],folder/'assemble.log')
                native('check-fauna-audition.py',['--root',folder],folder/'check.log')
                # Only exact intermediate names created by this worker; no
                # recursive deletion or broad cleanup inside generated assets.
                for i in range(1,5):
                    for name in ('raw.glb','raw-clean.glb','raw.latents.pt','reference-prepared.png'):
                        p=folder/f'candidate-{i}'/name
                        if p.exists():p.unlink()
                if rebuild_marker.exists():rebuild_marker.replace(folder/'mesh-preparation-rebuild-completed.json')
                final_status='experimental_motion_trial_pending' if a.motion_root else 'ready'
                state['animals'][slug].update(status=final_status,review=str(folder/'Animal - Raw Audition.blend'),finished=datetime.datetime.now().astimezone().isoformat());save();print('TRIAL NEEDS TECHNICAL REVIEW' if a.motion_root else 'READY',slug,flush=True)
                subprocess.run([sys.executable,str(repo/'scripts/index-fauna-auditions.py'),'--root',str(root)],cwd=repo,env=env,check=True)
            except Exception as exc:
                state['animals'][slug].update(status='failed',error=str(exc));save();print('FAILED',slug,str(exc),flush=True)
        state['current']=None;save()
        if not a.watch or a.only:break
        if not did:time.sleep(10)
finally:
    state['current']=None;state['worker_exited']=True;save();lock.unlink(missing_ok=True)
