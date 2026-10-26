"""Read-only final review sheets from the exact packaged phone assets.

No native rendering or automatic art approval. Keeps compact contact sheets,
not hundreds of temporary decoded images, and records hashes for manual review.
"""
import hashlib,json,math
from pathlib import Path
import cv2
from PIL import Image,ImageDraw,ImageFont,ImageOps
repo=Path(__file__).resolve().parent.parent;site=repo/'artifacts/fauna-phone-review/dist';out=repo/'artifacts/fauna-review-revisions/final-sweep';out.mkdir(exist_ok=True)
data=json.loads((site/'animals/index.json').read_text(encoding='utf-8'));font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',15);manifest={'source_manifest_sha256':hashlib.sha256((site/'animals/index.json').read_bytes()).hexdigest(),'sheets':[],'animals':[],'actually_inspected':False,'owner_approved':False}
def tile(sheet,im,x,y,w,h):
 im=ImageOps.contain(im.convert('RGB'),(w,h));sheet.paste(im,(x+(w-im.width)//2,y+(h-im.height)//2))
for start in range(0,len(data['animals']),6):
 group=data['animals'][start:start+6];heads=Image.new('RGB',(1280,228*len(group)),(25,29,34));motion=Image.new('RGB',(1408,160*len(group)),(25,29,34));hd=ImageDraw.Draw(heads);md=ImageDraw.Draw(motion)
 for row,animal in enumerate(group):
  slug=animal['slug'];pics=animal['pictures'];faces=[p for p in pics if 'Face' in p['label']];variants=[p for p in animal['variations'] if p['type']=='paint'];selected=(faces+pics)[:3]+[variants[0],variants[-1]]
  for col,picture in enumerate(selected):
   path=site/picture['url'];tile(heads,Image.open(path),col*256,row*228+24,256,204);hd.text((col*256+4,row*228+3),slug[:24]+' / '+picture['label'][:22],font=font,fill='white')
  clips=[]
  for clip_index,clip in enumerate(animal['clips']):
   path=site/clip['url'];cap=cv2.VideoCapture(str(path));assert cap.isOpened(),path;count=int(cap.get(cv2.CAP_PROP_FRAME_COUNT));fps=cap.get(cv2.CAP_PROP_FPS);frames=[]
   for phase_index,phase in enumerate([.09,.31,.57,.81]):
    frame_index=min(count-1,int(count*phase));cap.set(cv2.CAP_PROP_POS_FRAMES,frame_index);ok,frame=cap.read();assert ok,(slug,clip['label'],phase);im=Image.fromarray(cv2.cvtColor(frame,cv2.COLOR_BGR2RGB));x=(clip_index*4+phase_index)*176;y=row*160;tile(motion,im,x,y+24,176,132);md.text((x+3,y+3),slug[:17]+' '+clip['label'][:9]+' '+str(round(frame_index/fps,2)),font=font,fill='white');frames.append(frame_index)
   cap.release();clips.append({'label':clip['label'],'file':clip['url'],'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'sampled_frames':frames})
  manifest['animals'].append({'slug':slug,'asset_revision':animal['asset_revision'],'clips':clips,'issues':[],'actually_inspected':False})
 for kind,sheet in [('heads-coats',heads),('motion',motion)]:
  path=out/f'{kind}-{start//6+1:02}.jpg';sheet.save(path,quality=94);manifest['sheets'].append({'kind':kind,'file':str(path.relative_to(repo)),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'slugs':[r['slug'] for r in group],'actually_inspected':False})
 print('FINAL SWEEP SHEETS',start+len(group),'/',len(data['animals']),flush=True)
(out/'review.json').write_text(json.dumps(manifest,indent=2)+'\n');print('READ-ONLY SWEEP READY',len(manifest['animals']),len(manifest['sheets']))
