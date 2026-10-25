"""Show actual sculpt sockets and old eyes together; never infer visual approval."""
import argparse,json
import numpy as np
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024'
ap=argparse.ArgumentParser();ap.add_argument('--slugs',nargs='*');a=ap.parse_args()
jobs=json.loads((root/'paint-queue.json').read_text())['jobs'];jobs=[j for j in jobs if (not a.slugs or j['slug'] in a.slugs) and (root/j['slug']/'eye-audit/capture.json').exists()]
font=ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf',18);small=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',12)
out=root/'inspection/eye-audit-sheets';out.mkdir(exist_ok=True)
drafts={}
front_species={'owl','snowy-owl','lynx','jaguar','leopard','lion','ocelot','puma','tiger','baboon','capuchin','chimpanzee','gibbon','orangutan','gorilla','guereza','howler-monkey','lemur','macaque','spider-monkey','tamarin'}
def projected(folder,land,side,view):
 camera=json.loads((folder/f'eye-audit/{view}.json').read_text());q=np.linalg.inv(np.asarray(camera['camera_matrix']))@np.r_[land[side+'_eye']['point'],1.]
 scale=camera['scale'];x=(q[0]/scale+.5)*512-.5;y=(.5-q[1]/scale)*512-.5
 radius=land[side+'_eye'].get('radius',json.loads((folder/'coat-bake.json').read_text())['eye_radius'])
 return [round(x),round(y)],[round(radius[0]*1024/scale),round(radius[1]*1024/scale)]
for job in jobs:
 folder=root/job['slug'];land=json.loads((folder/'face-landmarks.json').read_text());state=json.loads((folder/'paint-state.json').read_text());eyes={}
 for side in ['right','left']:
  view='front' if job['slug'] in front_species else side;pixel,size=projected(folder,land,side,view);eyes[side]={'camera':f'eye-audit/{view}.json','pixel':pixel,'size':size}
 drafts[job['slug']]={'geometry_sha256':state['geometry_sha256'],'inspected':False,'fitting_revision':3,'eyes':eyes}
(out/'drafts.json').write_text(json.dumps(drafts,indent=2)+'\n')
for start in range(0,len(jobs),4):
 batch=jobs[start:start+4];canvas=Image.new('RGB',(1536,len(batch)*416),(27,31,31));draw=ImageDraw.Draw(canvas)
 for row,job in enumerate(batch):
  for col,(view,kind) in enumerate([('right','sculpt'),('left','sculpt'),('front','sculpt'),('front','before')]):
   path=root/job['slug']/f'eye-audit/{view}-{kind}.png'
   with Image.open(path) as im:im=im.convert('RGB').resize((384,384));canvas.paste(im,(col*384,row*416))
   if kind=='sculpt':
    folder=root/job['slug'];land=json.loads((folder/'face-landmarks.json').read_text())
    for side in (['right','left'] if view=='front' else [view]):
     pixel,size=projected(folder,land,side,view);px=col*384+pixel[0]*.75;py=row*416+pixel[1]*.75;wx=size[0]*.375;hy=size[1]*.375
     draw.ellipse((px-wx,py-hy,px+wx,py+hy),outline=(75,165,220),width=1);draw.line((px-4,py,px+4,py),fill=(75,165,220));draw.line((px,py-4,px,py+4),fill=(75,165,220))
    for value in [64,128,192,256,320,384,448]:
     xy=int(value*.75);draw.text((col*384+xy+2,row*416+2),str(value),font=small,fill=(155,195,230));draw.text((col*384+2,row*416+xy),str(value),font=small,fill=(155,195,230))
   draw.text((col*384+8,row*416+389),job['slug']+' · '+view+' '+kind,font=font,fill=(240,240,230))
 path=out/f'sockets-{start//4+1:02}.jpg';canvas.save(path,quality=94)
 print(path.name,','.join(j['slug'] for j in batch))
