"""Bounded CPU before/after views for explicitly selected repairs."""
import argparse,concurrent.futures,json,subprocess,sys
from pathlib import Path
from PIL import Image,ImageDraw
repo=Path(__file__).resolve().parent.parent
blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
ap=argparse.ArgumentParser();ap.add_argument('--slugs',nargs='+',required=True);a=ap.parse_args()
def work(slug):
    root=repo/'artifacts/fauna-overnight-20261024'/slug;proof=json.loads((root/'owner-fix.json').read_text());number=proof['candidate'];logs=repo/'artifacts/fauna-owner-fixes-20261025'/slug;logs.mkdir(exist_ok=True,parents=True)
    with (logs/'edge-diagnostics.log').open('w') as log:
        subprocess.run([str(blender),'--background','--threads','2','--python-exit-code','1','--python',str(repo/'scripts/measure-fauna-owner-repair.py'),'--','--slug',slug],stdout=log,stderr=subprocess.STDOUT,check=True)
    data=json.loads((logs/'edge-diagnostics.json').read_text());clip=data['inspection_clip'];phase=data['inspection_phase'];images=[]
    for view in ('side','front'):
        for version,blend in [('before','Animal - Raw Audition.blend'),('after','Animal - Owner Corrected.blend')]:
            with (logs/f'render-{view}-{version}.log').open('w') as log:
                subprocess.run([str(blender),'--background','--threads','2','--python-exit-code','1','--python',str(repo/'scripts/render-fauna-diagnostic.py'),'--','--root',str(root),'--model',str(number),'--clip',clip,'--phase',str(phase),'--view',view,'--size','448','--blend-name',blend,'--tag','owner-'+version],stdout=log,stderr=subprocess.STDOUT,check=True)
            images.append((root/'previews'/f'{number}-{clip}-{view}-owner-{version}.png',f'{slug} {clip} phase {phase:.3f} {view} {version}'))
    canvas=Image.new('RGB',(896,952),'#20252c');draw=ImageDraw.Draw(canvas)
    for i,(path,label) in enumerate(images):
        x=(i%2)*448;y=(i//2)*476;canvas.paste(Image.open(path),(x,y));draw.text((x+8,y+452),label,fill='white')
    canvas.save(logs/'repair-contact.png');print('CONTACT READY',slug,flush=True)
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
    for result in pool.map(work,a.slugs):pass
