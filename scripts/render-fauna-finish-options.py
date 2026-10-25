"""Render selected hypothetical/sex trait and bounded-morph combinations."""
import hashlib,json,subprocess
from pathlib import Path
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024'
blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
cases=[(slug,-1,2 if slug in {'hare','rabbit','red-fox','wolf'} else 0,'lineage') for slug in ['hare','rabbit','red-fox','wolf','horse','reindeer','moose','wild-cattle','bison','duck']]
cases += [('moose',0,2,'female'),('reindeer',0,2,'female')]
evidence=[]
for slug,coat,extra,tag in cases:
    folder=root/slug;state=json.loads((folder/'paint-state.json').read_text());coat=coat%len(state['coats'])
    blend=folder/'Animal - Painted Species Review.blend';digest=hashlib.sha256(blend.read_bytes()).hexdigest()
    for view in ['side','front']:
        args=[str(blender),'--background','--threads','4','--python-exit-code','1','--python',str(repo/'scripts/render-fauna-diagnostic.py'),'--','--root',str(folder),'--blend-name',blend.name,'--coat',str(coat),'--extra-variant',str(extra),'--view',view,'--size','448','--tag',tag]
        with (folder/f'{tag}-{view}.log').open('w',encoding='utf8') as log:r=subprocess.run(args,cwd=repo,stdout=log,stderr=subprocess.STDOUT)
        suffix=(f'-coat{coat}' if coat else '')+(f'-extra{extra}' if extra else '')+'-'+tag
        path=folder/'previews'/f'1-Bind-{view}{suffix}.png'
        evidence.append({'slug':slug,'coat':coat,'label':state['coats'][coat]['label'],'extra_variant':extra,'view':view,'path':str(path.relative_to(repo)),'blend_sha256':digest,'render_sha256':hashlib.sha256(path.read_bytes()).hexdigest() if r.returncode==0 else None,'returncode':r.returncode,'actually_inspected':False})
        (root/'inspection/lineage-preview-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
