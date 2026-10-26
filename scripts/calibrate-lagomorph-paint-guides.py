"""Explicit measured guide landmarks, including independent mirrored profile fit."""
import json
from pathlib import Path
import numpy as np
repo=Path(__file__).resolve().parent.parent
for slug in ['hare','rabbit']:
 root=repo/'artifacts/fauna-visual-corrections'/slug;land=json.loads((root/'face-landmarks.json').read_text())
 def pixel(point,view):
  guide=json.loads((root/'eye-audit'/f'{view}.json').read_text());M=np.array(guide['camera_matrix']);p=(np.array(point)-M[:3,3])@M[:3,:3]
  return ((p[:2]/guide['scale']*[1,-1]+.5)*guide['size']).tolist()
 def with_corners(target,source):return target+[[0,0],[512,0],[0,512],[512,512]],source+[[0,0],[1254,0],[0,1254],[1254,1254]]
 config=[]
 target=[pixel(land[side+'_eye']['point'],'front') for side in ['right','left']]+[pixel(land['nose']['point'],'front')]
 source=[[414,450],[890,475],[642,904]] if slug=='hare' else [[383,515],[868,518],[640,930]]
 target,source=with_corners(target,source);config.append({'camera':'front','image':'front-paint-guide.png','target_pixels':target,'guide_pixels':source})
 eye,nose=([485,455],[951,863]) if slug=='hare' else ([520,485],[974,882])
 for side in ['right','left']:
  target=[pixel(land[side+'_eye']['point'],side),pixel(land['nose']['point'],side)]
  source=[eye,nose] if side=='right' else [[1253-eye[0],eye[1]],[1253-nose[0],nose[1]]]
  target,source=with_corners(target,source)
  config.append({'camera':side,'image':'right-paint-guide.png','flip_x':side=='left','target_pixels':target,'guide_pixels':source})
 (root/'face-paint-views.json').write_text(json.dumps(config,indent=2)+'\n');print('EXPLICIT FRONT/PROFILE GUIDE FIT',slug)
