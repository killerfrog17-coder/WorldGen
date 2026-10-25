"""Run a finite owner-requested redo list before resuming the full serial queue."""
import argparse,datetime,json,subprocess,sys
from pathlib import Path
repo=Path(__file__).resolve().parent.parent;batch=repo/'artifacts/fauna-overnight-20261024'
p=argparse.ArgumentParser();p.add_argument('--jobs',type=Path,required=True);p.add_argument('--resume-full',action='store_true');a=p.parse_args()
request=json.loads(a.jobs.read_text());assert not (batch/'worker.lock').exists()
for slug in request['slugs']:
    control=json.loads((batch/'run-control.json').read_text())
    if control.get('mode')=='pause':print('Owner pause respected; priority queue stopped.',flush=True);sys.exit(0)
    request['current']=slug;request['updated']=datetime.datetime.now().astimezone().isoformat();a.jobs.write_text(json.dumps(request,indent=2))
    proc=subprocess.run([sys.executable,str(repo/'scripts/run-fauna-overnight.py'),'--only',slug],cwd=repo)
    assert not (batch/'worker.lock').exists(),'Wait for actual producer termination'
    state=json.loads((batch/'queue-state.json').read_text());request.setdefault('results',{})[slug]=state['animals'].get(slug,{'status':'not_started'})
    a.jobs.write_text(json.dumps(request,indent=2))
    if proc.returncode:print('Priority worker terminal error',slug,proc.returncode,flush=True)
request['current']=None;request['priority_pass_finished']=True;a.jobs.write_text(json.dumps(request,indent=2))
if a.resume_full and json.loads((batch/'run-control.json').read_text()).get('mode')=='run':
    print('Priority pass finished; resuming full raw roster.',flush=True)
    subprocess.run([sys.executable,str(repo/'scripts/run-fauna-overnight.py'),'--watch'],cwd=repo,check=True)
