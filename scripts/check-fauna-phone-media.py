"""Decode real motion samples, verify full coverage/identities and native preservation."""
import argparse,hashlib,json
from pathlib import Path
import cv2,numpy as np
from PIL import Image,ImageDraw,ImageFont
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-phone-media';plan=json.loads((root/'plan.json').read_text());out=root/'checks';out.mkdir(exist_ok=True)
ap=argparse.ArgumentParser();ap.add_argument('--partial',action='store_true');a=ap.parse_args();report={'total':177,'completed':[],'errors':{},'edge_warnings':[],'clips_checked':0,'stills_checked':0};thumbs=[];font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',17)
check_signature=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
for item in plan['animals']:
 slug=item['slug'];folder=root/slug;rp=folder/'receipt.json'
 if not rp.exists() or not json.loads(rp.read_text()).get('complete'):
  if a.partial:continue
  report['errors'][slug]='Media incomplete';continue
 try:
  r=json.loads(rp.read_text());assert r['source_sha256']==item['source_sha256']==hashlib.sha256((repo/item['source']).read_bytes()).hexdigest()
  assert len(r['videos'])==len(item['clips']);assert len(r['stills'])==3+int(item['procedural'])+len(item['coats'])+len(item['extras'])
  evidence=folder/'media-check.json';receipt_signature=hashlib.sha256(rp.read_bytes()).hexdigest();cached=json.loads(evidence.read_text()) if evidence.exists() else {}
  if cached.get('checker_sha256')==check_signature and cached.get('receipt_sha256')==receipt_signature:
   report['stills_checked']+=cached['stills'];report['clips_checked']+=cached['clips'];report['edge_warnings']+=cached['edge_warnings'];report['completed'].append(slug)
   for thumb in cached.get('motion_thumbnails',[]):
    with Image.open(folder/thumb['file']) as image:thumbs.append({'slug':slug,'label':thumb['label'],'frame':np.asarray(image.convert('RGB')).copy()})
   continue
  warning_start=len(report['edge_warnings'])
  animal_thumbs=[]
  for im in r['stills']:
   path=folder/im['file'];assert hashlib.sha256(path.read_bytes()).hexdigest()==im['sha256']
   with Image.open(path) as pic:assert pic.size==(768,576);pic.verify()
   report['stills_checked']+=1
  for clip in r['videos']:
   path=folder/clip['file'];assert hashlib.sha256(path.read_bytes()).hexdigest()==clip['sha256'];cap=cv2.VideoCapture(str(path));assert cap.isOpened();fps=cap.get(cv2.CAP_PROP_FPS);count=int(cap.get(cv2.CAP_PROP_FRAME_COUNT));assert abs(fps-24)<.01 and abs(count/24-clip['seconds'])<.05
   for phase in [0,.17,.43,.79,.96]:
    cap.set(cv2.CAP_PROP_POS_FRAMES,min(count-1,int(count*phase)));ok,frame=cap.read();assert ok and frame.shape[:2]==(480,640)
    # Background is flat; inspect the largest animal component, ignoring retained
    # tiny raw debris. Border contact is a framing flag, not gait acceptance.
    background=np.median(np.vstack([frame[0],frame[-1],frame[:,0],frame[:,-1]]),axis=0);mask=(np.linalg.norm(frame.astype(float)-background,axis=2)>18).astype(np.uint8);n,labels,stats,centroids=cv2.connectedComponentsWithStats(mask)
    if n>1:
     x,y,w,h,area=stats[1+np.argmax(stats[1:,4])]
     if not item['accepted'] and area>1000 and (x<=1 or y<=1 or x+w>=639 or y+h>=479):report['edge_warnings'].append({'slug':slug,'clip':clip['label'],'phase':phase})
    if phase==.43 and (clip['label'] in ['Walk','Run','Flight','Flap','Glide','Slither','Side winding','Sidewinding'] or clip['label'].startswith('Swim')) and len(animal_thumbs)<2:
     frame_rgb=cv2.cvtColor(frame,cv2.COLOR_BGR2RGB);thumbname='checked-motion-'+clip['label'].lower()+'.jpg';Image.fromarray(frame_rgb).save(folder/thumbname,quality=93);animal_thumbs.append({'file':thumbname,'label':clip['label']});thumbs.append({'slug':slug,'label':clip['label'],'frame':frame_rgb})
   cap.release();report['clips_checked']+=1
  report['completed'].append(slug)
  evidence.write_text(json.dumps({'checker_sha256':check_signature,'receipt_sha256':receipt_signature,'source_sha256':item['source_sha256'],'stills':len(r['stills']),'clips':len(r['videos']),'edge_warnings':report['edge_warnings'][warning_start:],'motion_thumbnails':animal_thumbs,'native_unchanged':True},indent=2)+'\n')
 except Exception as exc:report['errors'][slug]=str(exc)
for start in range(0,len(thumbs),12):
 batch=thumbs[start:start+12];canvas=Image.new('RGB',(1280,((len(batch)+3)//4)*272),(25,30,30));draw=ImageDraw.Draw(canvas)
 for i,t in enumerate(batch):x=i%4*320;y=i//4*272;canvas.paste(Image.fromarray(t['frame']).resize((320,240)),(x,y));draw.text((x+6,y+244),t['slug']+' / '+t['label'],font=font,fill='white')
 canvas.save(out/f'motion-{start//12+1:02}.jpg',quality=93)
(out/'media-check.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({**report,'completed':len(report['completed'])}));assert not report['errors']
