"""Rebase the installed local TripoSG environment without borrowing scratch data.

Run after relocating the checkout. External AI installations/weights stay in
their existing locations. Use Scripts/python.exe -m pip, not old pip launchers.
"""
from pathlib import Path
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
    text=text.replace(str(ROOT/'artifacts/tools/triposg-review-venv'),str(env))
    path.write_text(text,encoding='utf-8')
print('TripoSG environment uses permanent tool paths; scratch fallback removed.')
