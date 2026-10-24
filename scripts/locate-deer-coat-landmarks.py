"""Pick surface landmarks from inspected orthographic guide pixels."""
import json
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path(sys.argv[sys.argv.index('--')+1]).resolve()
d=np.load(root/'anatomy.npz');basis=d['basis'];tree=BVHTree.FromPolygons(d['vertices'],d['faces'],all_triangles=True)
cameras=json.loads((root/'anatomical-cameras.json').read_text())
landmarks={}
for name,view,pixel in [('eye_right','head-right',(401,392)),('eye_left','head-left',(534,404)),
    ('nose','head-front',(489,541)),('lip','head-front',(488,587))]:
    camera=cameras[view];m=np.array(camera['camera_matrix']);w,h=camera['size'];span=camera['scale']
    local=np.array([((pixel[0]+.5)/w-.5)*span,(.5-(pixel[1]+.5)/h)*span*h/w,0])
    origin=basis@(m[:3,:3]@local+m[:3,3]);direction=basis@(-m[:3,2])
    hit,normal,face,distance=tree.ray_cast(Vector(origin),Vector(direction),10)
    assert hit is not None,(name,pixel)
    if normal.dot(-Vector(direction))<0:normal=-normal
    landmarks[name]=dict(point=list(hit),normal=list(normal),face=int(face),guide=view,pixel=pixel)
(root/'landmarks.json').write_text(json.dumps(landmarks,indent=2))
print(json.dumps(landmarks,indent=2))
