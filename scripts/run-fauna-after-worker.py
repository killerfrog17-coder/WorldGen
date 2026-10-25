"""Start one finite prepared queue only after the identified parent really exits."""
import argparse,json,subprocess,sys,time
from pathlib import Path
import psutil
repo=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser();p.add_argument('--after-pid',type=int,required=True);p.add_argument('--jobs',type=Path,required=True);a=p.parse_args()
parent=psutil.Process(a.after_pid);identity=parent.create_time();command=' '.join(parent.cmdline())
assert 'run-fauna-priority-queue.py' in command and 'owner-alpha-redos-20261025.json' in command
print('Waiting for the actual finite queue parent',a.after_pid,flush=True)
while psutil.pid_exists(a.after_pid):
    try:
        if psutil.Process(a.after_pid).create_time()!=identity:break
    except psutil.NoSuchProcess:break
    time.sleep(3)
batch=repo/'artifacts/fauna-overnight-20261024'
assert not (batch/'worker.lock').exists(),'A producer remains; never launch another'
if json.loads((batch/'run-control.json').read_text()).get('mode')!='run':
    print('Control is no longer RUN; pending queue left untouched',flush=True);sys.exit(0)
print('Prior queue exited. Starting the prepared additional owner rerolls.',flush=True)
sys.exit(subprocess.call([sys.executable,str(repo/'scripts/run-fauna-priority-queue.py'),'--jobs',str(a.jobs)],cwd=repo))
