"""Small native front/side/rest/moving contact sheets for requested rerolls."""
import argparse,concurrent.futures,hashlib,json,subprocess
from pathlib import Path
from PIL import Image,ImageDraw
repo=Path(__file__).resolve().parent.parent;batch=repo/'artifacts/fauna-overnight-20261024'
blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
p=argparse.ArgumentParser();p.add_argument('--slugs',nargs='+',required=True)
p.add_argument('--motion-revision',action='store_true',help='Inspect Walk and Run at four phases on each revised candidate')
p.add_argument('--snake-revision',action='store_true',help='Inspect Slither and Side winding at four phases from above')
p.add_argument('--bird-revision',action='store_true',help='Inspect four Walk and Flap phases plus Glide')
a=p.parse_args()
def work(slug):
    root=batch/slug;revised=a.motion_revision or a.snake_revision or a.bird_revision;source=root/('Animal - Motion Revision.blend' if revised else 'Animal - Raw Audition.blend');digest=hashlib.sha256(source.read_bytes()).hexdigest()
    check=json.loads((root/('motion-native-checks.json' if a.bird_revision else 'snake-deformation-checks.json' if a.snake_revision else 'rodent-deformation-checks.json' if a.motion_revision else 'native-checks.json')).read_text());assert check.get('checked_blend_sha256')==digest
    meta=json.loads((root/'audition.json').read_text());sheets=[];evidence=[]
    for number,e in enumerate(meta['candidates'],1):
        clip=next((key for key in ['Run','Walk','Swim','Swim Vertical','Side winding','Flap'] if key in e['actions']),'Idle')
        cases=[(label,'top',phase) for label in ('Slither','Side winding') for phase in (0,.25,.5,.75)] if a.snake_revision else [(label,'side',phase) for label in ('Walk','Run') for phase in (0,.25,.5,.75)] if a.motion_revision else [('Bind','front',.5),('Bind','side',.5),(clip,'side',.5)]
        if a.bird_revision:cases=[(label,'oblique',phase) for label in ('Walk','Flap') for phase in (0,.25,.5,.75)]+[('Glide','front',.5)]
        for row,(label,view,phase) in enumerate(cases):
            tag=f'motion-audit-{int(phase*100)}' if revised else 'alpha-audit'
            log=root/f'owner-audit-{number}-{row}.log'
            with log.open('w') as stream:
                subprocess.run([str(blender),'--background','--threads','2','--python-exit-code','1','--python',str(repo/'scripts/render-fauna-diagnostic.py'),'--','--root',str(root),'--model',str(number),'--clip',label,'--phase',str(phase),'--view',view,'--size','384','--surface-only','--tag',tag,'--blend-name',source.name],stdout=stream,stderr=subprocess.STDOUT,check=True)
            path=root/'previews'/f'{number}-{label}-{view}-surface-{tag}.png';sheets.append((number-1,row,path,f'{slug} {number} {label} {phase:.2f} {view}'))
            evidence.append({'image':str(path.relative_to(root)),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
    assert hashlib.sha256(source.read_bytes()).hexdigest()==digest,'Do not combine images from different native builds'
    canvas=Image.new('RGB',(1536,410*(9 if a.bird_revision else 8 if revised else 3)),'#20252c');draw=ImageDraw.Draw(canvas)
    for x,y,path,label in sheets:canvas.paste(Image.open(path),(x*384,y*410));draw.text((x*384+6,y*410+388),label,fill='white')
    suffix='owner-motion' if revised else 'owner-alpha'
    canvas.save(root/f'previews/{suffix}-reroll-audit.png')
    (root/f'{suffix}-render-evidence.json').write_text(json.dumps({'checked_blend_sha256':digest,'images':evidence,'inspection_pending':True,'art_approved':False},indent=2)+'\n');print('REROLL CONTACT READY',slug,flush=True)
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
    for _ in pool.map(work,a.slugs):pass
