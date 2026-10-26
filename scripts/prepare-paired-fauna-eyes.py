"""Prepare an unpublished paired-eye correction; preserve selected source assets."""
import argparse,copy,hashlib,json,shutil
from pathlib import Path
import numpy as np
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slugs',nargs='+',required=True);a=ap.parse_args()
for slug in a.slugs:
 source=repo/'artifacts/fauna-painted-20261024'/slug;out=repo/'artifacts/fauna-review-revisions'/slug;out.mkdir(parents=True,exist_ok=True)
 assert out.resolve().is_relative_to((repo/'artifacts/fauna-review-revisions').resolve())
 for name in ['Animal - Painted Species Review.blend','paint-state.json','face-landmarks.json','paint-anatomy.npz','selected-source-entry.json','coat-bake.json']:
  path=source/name
  if path.exists() and not (out/name).exists():shutil.copy2(path,out/name)
 land=json.loads((source/'face-landmarks.json').read_text());original=copy.deepcopy(land);right=land['right_eye'];left=land['left_eye'];mid=(np.array(right['point'])+np.array(left['point']))*.5;F=np.array(land['nose']['point'])-mid;F[2]=0;F/=np.linalg.norm(F);S=np.array([F[1],-F[0],0.]);up=np.array([0.,0.,1.]);land['front_axis']=F.tolist();land['side_axis']=S.tolist()
 R=np.array(right['point']);L=np.array(left['point']);height=(R[2]+L[2])*.5
 # Respect each actual socket's depth, while matching their head-space height,
 # opening dimensions and mirrored facing. Body asymmetry is measured, not hidden.
 normal_r=np.array(right['normal']);normal_l=np.array(left['normal']);f=(normal_r@F+normal_l@F)*.5;l=(abs(normal_r@S)+abs(normal_l@S))*.5;z=(normal_r[2]+normal_l[2])*.5
 radius=np.mean([right['radius'],left['radius']],axis=0)
 if slug in {'red-fox','wolf'}:radius[1]*=.86
 for side,sign in [('right',1),('left',-1)]:
  anchor=land[side+'_eye'];point=np.array(anchor['point']);point[2]=height;normal=F*max(f,.05)+S*l*sign+up*z;normal/=np.linalg.norm(normal);horizontal=np.cross(up,normal)
  if horizontal@(np.array(land['nose']['point'])-point)<0:horizontal=-horizontal
  horizontal/=np.linalg.norm(horizontal);vertical=np.cross(normal,horizontal)
  if vertical[2]<0:vertical=-vertical
  anchor.update(point=point.tolist(),normal=normal.tolist(),radius=radius.tolist(),fit_frame={'normal':normal.tolist(),'horizontal':horizontal.tolist(),'vertical':vertical.tolist()})
 land['eye_revision']['mean_radius']=radius.tolist();land['paired_eye_revision']={'scope':'Matched paired aperture, head-space height and mirrored gaze. Retained independently fitted socket depths; further placement inspection required.','source_landmarks_sha256':hashlib.sha256((source/'face-landmarks.json').read_bytes()).hexdigest(),'agent_visually_checked':False}
 target=mid*.88+np.array(land['nose']['point'])*.12;guides=out/'eye-audit';guides.mkdir(exist_ok=True)
 for view,direction in [('front',F),('right',S),('left',-S)]:
  guide=json.loads((source/'eye-audit'/(view+'.json')).read_text());location=target+direction*4+np.array([0,0,.05]);zaxis=location-target;zaxis/=np.linalg.norm(zaxis);xaxis=np.cross(up,zaxis);xaxis/=np.linalg.norm(xaxis);yaxis=np.cross(zaxis,xaxis);matrix=np.eye(4);matrix[:3,:3]=np.stack([xaxis,yaxis,zaxis],axis=1);matrix[:3,3]=location;guide['camera_matrix']=matrix.tolist();guide['head_axis_refreshed_after_socket_fit']=True;(guides/(view+'.json')).write_text(json.dumps(guide,indent=2)+'\n')
 (out/'face-landmarks.json').write_text(json.dumps(land,indent=2)+'\n');(out/'source-landmarks.json').write_text(json.dumps(original,indent=2)+'\n');print('PAIRED EYE WORKING COPY',slug)
