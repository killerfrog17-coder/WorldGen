"""Actual tiny-fauna coat and flight-frame render sheet inputs; no native edits."""
import json,subprocess
from pathlib import Path
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024'
blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
slugs=['butterflies-and-moths-guild','fireflies-derived-lineage','bees-guild','dragonflies-guild','native-detritivorous-flies-guild']
results=[]
for slug in slugs:
    folder=root/slug;proof=json.loads((folder/'paint-procedural-checks.json').read_text())
    for coat in range(proof['coats_per_candidate']):
        view='top' if slug=='butterflies-and-moths-guild' else 'oblique'
        clip='Bind' if slug=='butterflies-and-moths-guild' else 'Flight'
        with (folder/f'colour-{coat}-render.log').open('w',encoding='utf8') as log:
            args=[str(blender),'--background','--threads','4','--python-exit-code','1','--python',str(repo/'scripts/render-fauna-diagnostic.py'),'--','--root',str(folder),'--blend-name','Animal - Painted Species Review.blend','--view',view,'--clip',clip,'--phase','.25','--coat',str(coat),'--size','256','--tag','microfauna']
            r=subprocess.run(args,cwd=repo,stdout=log,stderr=subprocess.STDOUT)
        results.append({'slug':slug,'coat':coat,'returncode':r.returncode,'view':view,'clip':clip,'actually_inspected':False})
        (root/'inspection/microfauna-render-progress.json').write_text(json.dumps(results,indent=2)+'\n')
