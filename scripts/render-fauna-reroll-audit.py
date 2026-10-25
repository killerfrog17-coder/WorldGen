"""Small native front/side/rest/moving contact sheets for requested rerolls."""
import argparse,concurrent.futures,hashlib,json,subprocess
from pathlib import Path
from PIL import Image,ImageDraw
repo=Path(__file__).resolve().parent.parent;batch=repo/'artifacts/fauna-overnight-20261024'
blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
p=argparse.ArgumentParser();p.add_argument('--slugs',nargs='+',required=True);a=p.parse_args()
def work(slug):
    root=batch/slug;source=root/'Animal - Raw Audition.blend';digest=hashlib.sha256(source.read_bytes()).hexdigest()
    check=json.loads((root/'native-checks.json').read_text());assert check.get('checked_blend_sha256')==digest
    meta=json.loads((root/'audition.json').read_text());sheets=[];evidence=[]
    for number,e in enumerate(meta['candidates'],1):
        clip=next((key for key in ['Run','Walk','Swim','Swim Vertical','Side winding','Flap'] if key in e['actions']),'Idle')
        for row,(label,view) in enumerate([('Bind','front'),('Bind','side'),(clip,'side')]):
            log=root/f'owner-audit-{number}-{row}.log'
            with log.open('w') as stream:
                subprocess.run([str(blender),'--background','--threads','2','--python-exit-code','1','--python',str(repo/'scripts/render-fauna-diagnostic.py'),'--','--root',str(root),'--model',str(number),'--clip',label,'--phase','.5','--view',view,'--size','384','--surface-only','--tag','alpha-audit'],stdout=stream,stderr=subprocess.STDOUT,check=True)
            path=root/'previews'/f'{number}-{label}-{view}-surface-alpha-audit.png';sheets.append((number-1,row,path,f'{slug} {number} {label} {view}'))
            evidence.append({'image':str(path.relative_to(root)),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
    assert hashlib.sha256(source.read_bytes()).hexdigest()==digest,'Do not combine images from different native builds'
    canvas=Image.new('RGB',(1536,1230),'#20252c');draw=ImageDraw.Draw(canvas)
    for x,y,path,label in sheets:canvas.paste(Image.open(path),(x*384,y*410));draw.text((x*384+6,y*410+388),label,fill='white')
    canvas.save(root/'previews/owner-alpha-reroll-audit.png')
    (root/'owner-alpha-render-evidence.json').write_text(json.dumps({'checked_blend_sha256':digest,'images':evidence,'inspection_pending':True,'art_approved':False},indent=2)+'\n');print('REROLL CONTACT READY',slug,flush=True)
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
    for _ in pool.map(work,a.slugs):pass
