"""Compose current saved review renders with source-image hashes, not approvals."""
import hashlib,json
from pathlib import Path
from PIL import Image,ImageDraw
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024';out=root/'inspection'
jobs=json.loads((root/'paint-queue.json').read_text())['jobs'];ledger=[]
def sheet(rows,path,cols=6,width=240):
    im=Image.new('RGB',(cols*width,((len(rows)+cols-1)//cols)*(width+28)),(27,29,32));draw=ImageDraw.Draw(im)
    sources=[]
    for i,(slug,label,path0) in enumerate(rows):
        x=i%cols*width;y=i//cols*(width+28)
        if path0 and path0.exists():
            with Image.open(path0) as opened:
                small=opened.convert('RGB');small.thumbnail((width,width));im.paste(small,(x+(width-small.width)//2,y))
            sources.append({'slug':slug,'path':str(path0.relative_to(repo)),'sha256':hashlib.sha256(path0.read_bytes()).hexdigest()})
        draw.text((x+4,y+width+1),slug[:32],fill='white');draw.text((x+4,y+width+13),label[:38],fill=(205,205,205))
    im.save(path,quality=90)
    ledger.append({'path':str(path.relative_to(repo)),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'inputs':sources,'actually_inspected':False})
for mode in ['default','lineage']:
    for start in range(0,len(jobs),24):
        rows=[]
        for job in jobs[start:start+24]:
            slug=job['slug'];folder=root/slug
            if mode=='lineage':
                qa=folder/'final-qa.json'
                if qa.exists():
                    q=json.loads(qa.read_text());path=repo/q['lineage_render'];label=q['lineage_label']
                else:path=None;label='Final render pending'
            else:path=folder/'previews/1-Bind-side-painted.png';label='Default adult coat'
            rows.append((slug,label,path))
        sheet(rows,out/f'final-{mode}-sheet-{start//24+1:02}.jpg')
evidence=out/'lineage-preview-evidence.json'
if evidence.exists():
    rows=json.loads(evidence.read_text())
    for view in ['side','front']:
        subset=[(r['slug'],r['label'],repo/r['path']) for r in rows if r['view']==view and r['returncode']==0]
        sheet(subset,out/f'bounded-traits-{view}-sheet.jpg',cols=4,width=320)
slugs=['butterflies-and-moths-guild','fireflies-derived-lineage','bees-guild','dragonflies-guild','native-detritivorous-flies-guild']
rows=[]
for slug in slugs:
    state=json.loads((root/slug/'paint-state.json').read_text())
    for i,coat in enumerate(state['coats']):
        suffix=f'-coat{i}' if i else ''
        name=f'1-Bind-top{suffix}-microfauna.png' if slug==slugs[0] else f'1-Flight-oblique{suffix}-microfauna.png'
        rows.append((slug,coat['label'],root/slug/'previews'/name))
sheet(rows,out/'final-microfauna-coats.jpg',cols=6,width=256)
(out/'finish-sheet-inputs.json').write_text(json.dumps(ledger,indent=2)+'\n')
print('Saved',len(ledger),'review sheets; inspection remains explicit')
