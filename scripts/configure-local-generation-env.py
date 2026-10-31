"""Rebase the installed local TripoSG environment without borrowing scratch data.

Run after relocating the checkout. External AI installations/weights stay in
their existing locations. Use Scripts/python.exe -m pip, not old pip launchers.
"""
import re
from project_layout import ROOT

env=ROOT/'tools/triposg-review-venv'
pth=env/'Lib/site-packages/owned-dependencies.pth'
if not pth.is_file():raise SystemExit('Install the local TripoSG environment first.')
lines=pth.read_text(encoding='utf-8').splitlines()
kept=[line for line in lines if 'artifacts' not in line.lower()]
pth.write_text('\n'.join(kept)+'\n',encoding='utf-8')
for name in ['activate','activate.bat','pyvenv.cfg']:
    path=env/('Scripts/'+name if name!='pyvenv.cfg' else name)
    text=path.read_text(encoding='utf-8')
    if name=='activate':
        text=re.sub(r'(?m)^VIRTUAL_ENV=.*$',lambda _:f'VIRTUAL_ENV="{env}"',text)
    elif name=='activate.bat':
        text=re.sub(r'(?m)^set VIRTUAL_ENV=.*$',lambda _:f'set VIRTUAL_ENV={env}',text)
    else:
        text=re.sub(r'(?m)^(command = .* -m venv ).*$',lambda m:m[1]+str(env),text)
    path.write_text(text,encoding='utf-8')
print('TripoSG environment uses permanent tool paths; scratch fallback removed.')
