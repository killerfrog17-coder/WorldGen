"""Register inspected overhead pigment guides by actual silhouette cross sections."""
import argparse,json,sys
from pathlib import Path
import numpy as np
from PIL import Image
from fauna_face_projection import neutral_exterior_coverage

ap=argparse.ArgumentParser();ap.add_argument('slugs',nargs='+');a=ap.parse_args()
base=Path(__file__).resolve().parent.parent/'artifacts/fauna-anatomy-revision'
for slug in a.slugs:
 root=base/slug
 def sections(path):
  rgb=np.asarray(Image.open(path).convert('RGB'),np.float32)/255
  mask=neutral_exterior_coverage(rgb)>0.9;ys,xs=np.where(mask);lo,hi=ys.min(),ys.max();points=[]
  for fraction in [.035,.12,.22,.33,.45,.57,.69,.79,.88,.96]:
   y=int(round(lo+(hi-lo)*fraction));xx=np.where(mask[y])[0]
   if not len(xx):raise ValueError((slug,path,y))
   left,right=np.quantile(xx,[.02,.98])
   for t in [.18,.5,.82]:points.append([float(left+(right-left)*t),float(y)])
  return points,rgb.shape
 target,ts=sections(root/'body-paint-source/top-clay.png');guide,gs=sections(root/'body-top-paint-guide.png')
 target.extend([[0,0],[ts[1],0],[0,ts[0]],[ts[1],ts[0]]]);guide.extend([[0,0],[gs[1],0],[0,gs[0]],[gs[1],gs[0]]])
 config={'camera':'top','camera_directory':'body-paint-source','image':'body-top-paint-guide.png','target_pixels':target,'guide_pixels':guide,'two_sided':False,'fade_border':.025,'weight':1.3,'reject_neutral_exterior':True,'exterior_inset_pixels':10.,'registration_method':'Inspected overhead silhouette cross sections at ten longitudinal fractions; native result still requires actual inspection.'}
 path=root/'body-paint-views.json';views=json.loads(path.read_text());views=[v for v in views if v['camera']!='top'];views.append(config);path.write_text(json.dumps(views,indent=2)+'\n');print(slug,'top registration installed')
