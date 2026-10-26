"""Align diagnostic cameras to actual paired eyes and fitted nasal tip.

Changes camera/landmark metadata only, never the selected model or animation.
"""
import argparse,hashlib,json,shutil
from pathlib import Path
import numpy as np
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('slugs',nargs='+');a=ap.parse_args()
for slug in a.slugs:
 root=repo/'artifacts/fauna-anatomy-revision'/slug;path=root/'face-landmarks.json';land=json.loads(path.read_text());mid=(np.array(land['right_eye']['point'])+land['left_eye']['point'])/2;F=np.array(land['nose']['point'])-mid;F[2]=0;F/=np.linalg.norm(F);S=np.cross(F,[0.,0.,1.]);old=land['front_axis'];backup=root/'legacy-eye-cameras';backup.mkdir(exist_ok=True)
 source=json.loads((root/'visual-source.json').read_text())
 for view,direction in [('front',F),('right',S),('left',-S)]:
  guide_path=root/'eye-audit'/f'{view}.json'
  if not (backup/guide_path.name).exists():shutil.copy2(guide_path,backup/guide_path.name)
  guide=json.loads(guide_path.read_text());D=direction+np.array([0.,0.,.0125]);D/=np.linalg.norm(D);X=np.cross([0.,0.,1.],D);X/=np.linalg.norm(X);Y=np.cross(D,X);M=np.eye(4);M[:3,:3]=np.stack([X,Y,D],axis=1);M[:3,3]=mid+np.array([0.,0.,.012])+D*3.
  guide.update(camera_matrix=M.tolist(),source_sha256=source['source_sha256'],actual_head_frame_reoriented=True,scope='Read-only frontal/profile camera alignment to current eye midpoint and actual fitted nose; model pose unchanged.');guide_path.write_text(json.dumps(guide,indent=2)+'\n')
 land['front_axis']=F.tolist();land['side_axis']=S.tolist();land['frontal_axis_revision']={'previous':old,'current':F.tolist(),'reason':'Paired socket correction had left a stale head axis. Camera axis now follows actual eye midpoint to fitted nasal tip. No mesh rotation.','reference_comparison_pending':True};path.write_text(json.dumps(land,indent=2)+'\n');print('ALIGNED',slug,'axis angle',round(float(np.degrees(np.arccos(np.clip(np.array(old)@F,-1,1)))),2))
