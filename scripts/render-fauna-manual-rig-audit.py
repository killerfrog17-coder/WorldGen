"""Multiple CPU poses for single selected manually rebuilt animal rigs."""
import argparse,concurrent.futures,hashlib,json,subprocess
from pathlib import Path
from PIL import Image,ImageDraw
repo=Path(__file__).resolve().parent.parent;blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
p=argparse.ArgumentParser();p.add_argument('--slugs',nargs='+',required=True);a=p.parse_args()
def work(slug):
    root=repo/'artifacts/fauna-overnight-20261024'/slug;workroot=repo/'artifacts/fauna-owner-fixes-20261025'/slug;proof=json.loads((root/'owner-fix.json').read_text());number=proof.get('review_model_number',proof['candidate']);digest=hashlib.sha256((root/'Animal - Owner Corrected.blend').read_bytes()).hexdigest()
    clip='Swim Vertical' if slug=='whale' else 'Walk';images=[]
    for y,phase in enumerate([0,.25,.5,.75]):
        for x,view in enumerate(['side','front']):
            tag=f'manual-rig-{int(phase*100)}'
            with (workroot/f'audit-{view}-{tag}.log').open('w') as stream:
                subprocess.run([str(blender),'--background','--threads','2','--python-exit-code','1','--python',str(repo/'scripts/render-fauna-diagnostic.py'),'--','--root',str(root),'--model',str(number),'--clip',clip,'--phase',str(phase),'--view',view,'--size','384','--blend-name','Animal - Owner Corrected.blend','--surface-only','--tag',tag],stdout=stream,stderr=subprocess.STDOUT,check=True)
            path=root/'previews'/f'{number}-{clip}-{view}-surface-{tag}.png';images.append((x,y,path,f'{slug} {clip} {phase:.2f} {view}'))
    assert hashlib.sha256((root/'Animal - Owner Corrected.blend').read_bytes()).hexdigest()==digest
    canvas=Image.new('RGB',(768,1640),'#20252c');draw=ImageDraw.Draw(canvas)
    for x,y,path,label in images:canvas.paste(Image.open(path),(x*384,y*410));draw.text((x*384+6,y*410+388),label,fill='white')
    canvas.save(workroot/'manual-rig-contact.png');(workroot/'manual-rig-render-evidence.json').write_text(json.dumps({'checked_blend_sha256':digest,'inspection_pending':True,'images':[str(path.relative_to(root)) for _,_,path,_ in images]},indent=2)+'\n');print('MANUAL RIG CONTACT READY',slug,flush=True)
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
    for _ in pool.map(work,a.slugs):pass
