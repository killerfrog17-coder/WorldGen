"""Initial silhouette registration of inspected body pigment guides.

No appearance acceptance is implied. Actual native front/profile views must
be checked because a generative guide can change anatomy within its silhouette.
"""
import argparse,json
from pathlib import Path
import numpy as np
from PIL import Image
from fauna_face_projection import neutral_exterior_coverage
ap=argparse.ArgumentParser();ap.add_argument('slugs',nargs='+');a=ap.parse_args()
base=Path(__file__).resolve().parent.parent/'artifacts/fauna-anatomy-revision'
for slug in a.slugs:
 root=base/slug;views=[]
 for view in ['right','front']:
  def sections(path):
   rgb=np.asarray(Image.open(path).convert('RGB'),np.float32)/255;mask=neutral_exterior_coverage(rgb)>0.9
   if view=='right':mask=mask.T
   yy,xx=np.where(mask);lo,hi=yy.min(),yy.max();points=[]
   for f in [.025,.10,.20,.32,.45,.58,.70,.81,.90,.975]:
    y=int(round(lo+(hi-lo)*f));xs=np.where(mask[y])[0];l,r=np.quantile(xs,[.02,.98])
    for t in [.2,.5,.8]:
     x=float(l+(r-l)*t);points.append([float(y),x] if view=='right' else [x,float(y)])
   return points,rgb.shape
  target,ts=sections(root/f'body-paint-source/{view}-clay.png');guide,gs=sections(root/f'body-{view}-paint-guide.png')
  target.extend([[0,0],[ts[1],0],[0,ts[0]],[ts[1],ts[0]]]);guide.extend([[0,0],[gs[1],0],[0,gs[0]],[gs[1],gs[0]]])
  config={'camera':view,'camera_directory':'body-paint-source','image':f'body-{view}-paint-guide.png','target_pixels':target,'guide_pixels':guide,'two_sided':False,'fade_border':.025,'weight':1.,'reject_neutral_exterior':True,'exterior_inset_pixels':10.,'registration_method':'Initial silhouette cross sections; actual native anatomy and pattern continuity remain pending.'};views.append(config)
  if view=='right':
   mirror=dict(config);mirror.update(camera='left',target_pixels=[[ts[1]-x,y] for x,y in target],guide_pixels=[[gs[1]-x,y] for x,y in guide],flip_x=True);views.append(mirror)
 (root/'body-paint-views.json').write_text(json.dumps(views,indent=2)+'\n');print(slug,'initial body registration installed')
