"""Compact current eye views, with exact native/render identities for inspection."""
import argparse,hashlib,json
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024'
ap=argparse.ArgumentParser();ap.add_argument('--pages',nargs='*',type=int);a=ap.parse_args()
jobs=json.loads((root/'paint-queue.json').read_text())['jobs'];out=root/'inspection/eye-result-sheets';out.mkdir(exist_ok=True)
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18)
for start in range(0,len(jobs),4):
 page=start//4+1
 if a.pages and page not in a.pages:continue
 batch=jobs[start:start+4];canvas=Image.new('RGB',(1152,len(batch)*416),(25,30,30));draw=ImageDraw.Draw(canvas);ledger=[]
 for row,j in enumerate(batch):
  folder=root/j['slug'];receipt=folder/'eye-audit/after-renders.json'
  for col,view in enumerate(['right','left','front']):
   path=folder/f'eye-audit/{view}-after.png'
   if path.exists():
    with Image.open(path) as im:canvas.paste(im.convert('RGB').resize((384,384)),(col*384,row*416))
    ledger.append({'slug':j['slug'],'view':view,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'source_sha256':json.loads(receipt.read_text())['source_sha256']})
   draw.text((col*384+6,row*416+389),j['slug']+' / '+view,font=font,fill='white')
 path=out/f'eyes-{page:02}.jpg';canvas.save(path,quality=94)
 path.with_suffix('.json').write_text(json.dumps({'views':ledger,'actually_inspected':False},indent=2)+'\n')
 print(page,','.join(j['slug'] for j in batch))
