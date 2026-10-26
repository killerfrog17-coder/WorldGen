"""Report exact native face IDs under inspected diagnostic pixels; read-only."""
import argparse,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree
repo=Path(__file__).resolve().parent.parent
sys.path.insert(0,str(repo/'scripts'));from fauna_surface_components import components
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--root',type=Path);ap.add_argument('--view',choices=['front','right','left'],default='front');ap.add_argument('--pixels',type=float,nargs='+',required=True);ap.add_argument('--camera-json',type=Path);ap.add_argument('--output-name');a=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
root=(a.root or repo/'artifacts/fauna-visual-corrections'/a.slug).resolve();guide=json.loads((a.camera_json or root/'eye-audit'/f'{a.view}.json').read_text());M=Matrix(guide['camera_matrix']);W,H=guide['size'];scale=guide['scale']
bpy.ops.wm.open_mainfile(filepath=str(root/'Animal - Painted Species Review.blend'));e=json.loads(bpy.context.scene['deer_four_rigs'])[0];body=bpy.data.objects[e['body']];mesh=body.data
bvh=BVHTree.FromPolygons([v.co for v in mesh.vertices],[f.vertices[:] for f in mesh.polygons]);records=[]
mesh.calc_loop_triangles();parts=components(np.array([v.co[:] for v in mesh.vertices]),np.array([f.vertices[:] for f in mesh.loop_triangles]))
for x,y in np.array(a.pixels).reshape(-1,2):
    origin=M@Vector(((x/W-.5)*scale,(.5-y/H)*scale,0));direction=-(M.to_3x3()@Vector((0,0,1)));point,normal,face,d=bvh.ray_cast(origin,direction,10)
    if point is None:continue
    poly=mesh.polygons[face];vs=np.array([mesh.vertices[i].co[:] for i in poly.vertices])
    part=next(part for part in parts if poly.vertices[0] in part)
    triangle=vs[:3];bc=np.linalg.lstsq(np.stack([triangle[1]-triangle[0],triangle[2]-triangle[0]],axis=1),np.array(point)-triangle[0],rcond=None)[0];bary=np.array([1-bc.sum(),*bc]);uv=bary@np.array([mesh.uv_layers.active.data[i].uv[:] for i in list(poly.loop_indices)[:3]])
    face_uv=np.array([mesh.uv_layers.active.data[i].uv[:] for i in poly.loop_indices]);uv_area=float(abs(np.linalg.det(np.array([face_uv[1]-face_uv[0],face_uv[2]-face_uv[0]])))*.5)
    records.append({'pixel':[x,y],'point':list(point),'uv':uv.tolist(),'face_uv':face_uv.tolist(),'uv_area':uv_area,'normal':list(normal),'face':face,'vertices':list(poly.vertices),'positions':vs.tolist(),'area':poly.area,'component_size':len(part),'component_vertices':sorted(part) if len(part)<100 else None})
(root/(a.output_name or f'inspected-face-picks-{a.view}.json')).write_text(json.dumps(records,indent=2)+'\n')
if a.view=='front' and not a.output_name:(root/'inspected-face-picks.json').write_text(json.dumps(records,indent=2)+'\n')
print(json.dumps(records))
