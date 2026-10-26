"""Arrange actual native head renders for manual visual audit; no approval."""
import argparse,json
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-review-revisions';ap=argparse.ArgumentParser();ap.add_argument('--slugs',nargs='*');a=ap.parse_args();queue=json.loads((repo/'artifacts/fauna-painted-20261024/paint-queue.json').read_text())['jobs'];rows=[]
for job in queue:
 slug=job['slug'];folder=root/slug
 if a.slugs and slug not in a.slugs:continue
 files=[folder/'eye-audit'/f'{view}-{kind}.png' for view in ['front','right','left'] for kind in ['after','sculpt']]
 if all(p.exists() for p in files):rows.append((slug,files))
dest=root/'visual-sheets';dest.mkdir(exist_ok=True);font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',16);manifest=[]
for start in range(0,len(rows),4):
 group=rows[start:start+4];sheet=Image.new('RGB',(1536,len(group)*286),(23,27,31));draw=ImageDraw.Draw(sheet)
 for row,(slug,files) in enumerate(group):
  for col,path in enumerate(files):
   with Image.open(path) as im:sheet.paste(im.convert('RGB').resize((256,256)),(col*256,row*286+27))
   draw.text((col*256+5,row*286+4),slug+' / '+path.stem,font=font,fill='white')
 path=dest/f'heads-{start//4+1:02}.jpg';sheet.save(path,quality=94);manifest.append({'file':str(path.relative_to(repo)),'slugs':[slug for slug,files in group]})
(dest/'index.json').write_text(json.dumps(manifest,indent=2)+'\n');print('VISUAL SHEETS',len(rows),len(manifest))
