"""One explicit, visually chosen camera ray on the immutable selected head."""
import argparse,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--root',type=Path,required=True);ap.add_argument('--side',choices=['left','right'],required=True);ap.add_argument('--view',choices=['left','right','front'],required=True);ap.add_argument('--camera-directory',default='eye-audit');ap.add_argument('--pixel',nargs=2,type=float,required=True);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
meta=json.loads((root/'visual-source.json').read_text());bpy.ops.wm.open_mainfile(filepath=str(repo/meta['source']));e=json.loads(bpy.context.scene['deer_four_rigs'])[0];mesh=bpy.data.objects[e['body']].data
bvh=BVHTree.FromPolygons([v.co for v in mesh.vertices],[f.vertices[:] for f in mesh.polygons]);g=json.loads((root/a.camera_directory/f'{a.view}.json').read_text());M=Matrix(g['camera_matrix']);W,H=g['size'];x,y=a.pixel
origin=M@Vector(((x/W-.5)*g['scale'],(.5-y/H)*g['scale'],0));direction=-(M.to_3x3()@Vector((0,0,1)));q,n,face,_=bvh.ray_cast(origin,direction,10);assert q is not None
land_path=root/'face-landmarks.json';land=json.loads(land_path.read_text());eye=land[a.side+'_eye'];previous=eye['point'];eye.update(point=list(q),surface_point=list(q),actual_surface_normal=list(n),explicit_surface_pick={'camera_directory':a.camera_directory,'view':a.view,'pixel':a.pixel,'camera':g,'source_face':face,'previous_point':previous,'actually_visually_inspected':True})
land['paired_eye_revision']['positions_symmetrical']=False;land_path.write_text(json.dumps(land,indent=2)+'\n');print('FITTED',a.slug,a.side,list(q))
