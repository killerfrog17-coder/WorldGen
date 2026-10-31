"""Run the bounded visual-review rerolls and the armed directional movement set."""
import json
from pathlib import Path
import subprocess
import sys

REPO=Path(__file__).resolve().parent.parent
ROOT=REPO/'artifacts/kimodo-human-bank'
jobs=[
    ('crouch_walk',1203,150,'A person walks forward cautiously with their knees slightly bent and their back upright, keeping their head at chest height.'),
    ('crouch_idle',1204,120,'A person stands still in a half crouch, with their knees slightly bent and their back upright.'),
    ('jump',1205,90,'A person performs one small vertical jump on the spot, taking off with both feet together and landing with both feet together.'),
    ('walk_back',935,150,None),('strafe_left',936,150,None),('strafe_right',937,150,None),
]
for name,seed,frames,prompt in jobs:
    command=[sys.executable,str(REPO/'scripts/generate-kimodo-human-bank.py'),'--case',name,'--seed',str(seed),'--frames',str(frames)]
    if prompt:command+=['--prompt',prompt]
    subprocess.run(command,cwd=REPO,check=True)
    (ROOT/'motions'/name/'selected.json').write_text(json.dumps({'take':'seed-'+str(seed)}))
    print('REVIEW TAKE SELECTED',name,seed,flush=True)
