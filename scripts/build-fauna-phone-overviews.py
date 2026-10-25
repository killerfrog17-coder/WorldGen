"""Package labelled current animal renders for a phone-readable whole-roster review."""
import hashlib,json,textwrap
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont

repo=Path(__file__).resolve().parent.parent
root=repo/'artifacts/fauna-painted-20261024'
site=repo/'artifacts/fauna-phone-review/dist'
out=site/'overview';out.mkdir(exist_ok=True)
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18)
bold=ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf',20)
rows=[{'slug':'deer','name':'Deer (approved)','procedural':False,'deer':True}]
rows+=json.loads((root/'review-index.json').read_text())['ready']
assert len(rows)==177 and len({r['slug'] for r in rows})==177
ledger=[]

def image_for(row,mode):
    if row.get('deer'):
        name={'side':'A-side','front':'B-front','alternate':'F-striped'}[mode]
        return site/'media'/f'{name}.webp','Approved sample' if mode!='alternate' else 'Hypothetical striped coat'
    folder=root/row['slug']
    if row.get('procedural'):
        coats=json.loads((folder/'paint-state.json').read_text())['coats']
        index=len(coats)-1 if mode=='alternate' else 0
        suffix=f'-coat{index}' if index else ''
        view='Bind-top' if row['slug']=='butterflies-and-moths-guild' else 'Flight-oblique'
        return folder/f'previews/1-{view}{suffix}-microfauna.png','Tiny fauna: top / flight view'
    if mode=='alternate':
        qa=json.loads((folder/'final-qa.json').read_text())
        path=repo/qa['lineage_render']
        assert hashlib.sha256(path.read_bytes()).hexdigest()==qa['lineage_render_sha256']
        return path,qa['lineage_label']
    return folder/f'previews/1-Bind-{mode}-painted.png','Default coat'

def compose(batch,mode,path,cols,width=320):
    height=width+88
    im=Image.new('RGB',(cols*width,((len(batch)+cols-1)//cols)*height),(30,35,35))
    draw=ImageDraw.Draw(im);sources=[]
    for cell,row in enumerate(batch):
        number=rows.index(row)+1;x=cell%cols*width;y=cell//cols*height
        source,label=image_for(row,mode);assert source.exists(),source
        with Image.open(source) as original:
            thumb=original.convert('RGB');thumb.thumbnail((width,width))
            draw.rectangle((x,y,x+width-1,y+width-1),fill=(94,102,107))
            im.paste(thumb,(x+(width-thumb.width)//2,y+(width-thumb.height)//2))
        display=row['name'].replace(' (guild)','').replace(' (derived lineage)','')
        title=textwrap.wrap(f'{number:03d}  {display}',width=29)
        for line,text in enumerate(title[:2]):draw.text((x+10,y+width+5+line*23),text,font=bold,fill=(244,244,232))
        caption=textwrap.shorten(label,width=39,placeholder='…')
        draw.text((x+10,y+width+57),caption,font=font,fill=(180,203,186))
        sources.append({'number':number,'slug':row['slug'],'name':row['name'],'path':str(source.relative_to(repo)),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'label':label})
    im.save(path,quality=91,optimize=True)
    ledger.append({'file':path.name,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'mode':mode,'inputs':sources})

manifest={'count':len(rows),'modes':{},'notes':'One retained model per animal/group. Alternate pages show one additional coat, not all palettes. Tiny fauna repeat their top/flight view in Front mode.'}
for mode in ['side','front','alternate']:
    master=out/f'all-{mode}.jpg';compose(rows,mode,master,cols=12)
    pages=[]
    for start in range(0,len(rows),24):
        batch=rows[start:start+24];path=out/f'{mode}-{start//24+1:02}.jpg'
        compose(batch,mode,path,cols=6)
        pages.append({'file':path.name,'first':start+1,'last':start+len(batch),'names':[r['name'] for r in batch]})
    manifest['modes'][mode]={'master':master.name,'pages':pages}
assert all(len([i for row in ledger if row['mode']==mode and row['file'].startswith('all-') for i in row['inputs']])==177 for mode in manifest['modes'])
(out/'index.json').write_text(json.dumps(manifest,indent=2)+'\n')
(repo/'artifacts/fauna-phone-review/checks/overview-inputs.json').write_text(json.dumps(ledger,indent=2)+'\n')
print(json.dumps({'animals':len(rows),'images':len(ledger),'bytes':sum(p.stat().st_size for p in out.glob('*.jpg'))}))
