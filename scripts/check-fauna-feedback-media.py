"""Check exact revision identity, all paint/extra images and two decoded clips."""
import argparse,hashlib,json
from pathlib import Path
import cv2
from PIL import Image
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-review-revisions';ap=argparse.ArgumentParser();ap.add_argument('--slugs',nargs='*');a=ap.parse_args();report={'checked':[],'pending':{},'errors':{}}
for folder in root.iterdir():
 if not folder.is_dir() or (a.slugs and folder.name not in a.slugs):continue
 plan=folder/'phone-review-plan.json'
 if not plan.exists():continue
 item=json.loads(plan.read_text())['animals'][0];slug=item['slug'];media=root/'phone-media'/slug;receipt=media/'receipt.json'
 try:
  if not receipt.exists():report['pending'][slug]='Missing media';continue
  r=json.loads(receipt.read_text());native=repo/item['source'];digest=hashlib.sha256(native.read_bytes()).hexdigest()
  if not r.get('complete') or r['source_sha256']!=digest or item['source_sha256']!=digest:report['pending'][slug]='Stale or incomplete native/media identity';continue
  assert len(r['videos'])==2 and len(r['stills'])==3+len(item['coats'])+len(item['extras'])
  for still in r['stills']:
   file=media/still['file'];assert hashlib.sha256(file.read_bytes()).hexdigest()==still['sha256']
   with Image.open(file) as image:assert image.size==(768,576);image.verify()
  decoded=0
  for clip in r['videos']:
   file=media/clip['file'];assert hashlib.sha256(file.read_bytes()).hexdigest()==clip['sha256'];cap=cv2.VideoCapture(str(file));assert cap.isOpened();fps=cap.get(cv2.CAP_PROP_FPS);count=int(cap.get(cv2.CAP_PROP_FRAME_COUNT));assert abs(fps-24)<.01 and abs(count/24-clip['seconds'])<.05
   for phase in [0,.17,.43,.79,.96]:
    cap.set(cv2.CAP_PROP_POS_FRAMES,min(count-1,int(count*phase)));ok,frame=cap.read();assert ok and frame.shape[:2]==(480,640);decoded+=1
   cap.release()
  (media/'media-check.json').write_text(json.dumps({'source_sha256':digest,'stills':len(r['stills']),'clips':2,'decoded_motion_frames':decoded,'all_checks_passed':True},indent=2)+'\n');report['checked'].append(slug)
 except Exception as exc:report['errors'][slug]=str(exc)
(root/'feedback-media-check.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({**report,'checked':len(report['checked'])}));assert not report['errors']
