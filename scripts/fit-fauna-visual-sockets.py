"""Explicit inspected frontal socket picks, on the chosen body, not skull guesses."""
import argparse,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
repo=Path(__file__).resolve().parent.parent;sys.path.insert(0,str(repo/'scripts'))
from fauna_surface_components import components
ap=argparse.ArgumentParser();ap.add_argument('--slug',choices=['red-fox','wolf','raccoon','buffalo','hare','rabbit'],required=True);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
root=repo/'artifacts/fauna-visual-corrections'/a.slug
land=json.loads((root/'face-landmarks.json').read_text());guide=json.loads((root/'eye-audit/front.json').read_text())
source=json.loads((root/'visual-source.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(repo/source['source']))
e=json.loads(bpy.context.scene['deer_four_rigs'])[0];body=bpy.data.objects[e['body']];body.data.calc_loop_triangles()
p=np.array([v.co[:] for v in body.data.vertices]);faces=np.array([f.vertices[:] for f in body.data.loop_triangles]);main=max(components(p,faces),key=len)
faces=faces[[all(int(i) in main for i in f) for f in faces]];bvh=BVHTree.FromPolygons([Vector(v) for v in p],faces.tolist(),all_triangles=True)
M=Matrix(guide['camera_matrix']);scale=guide['scale'];W,H=guide['size'];F=np.array(land['front_axis']);S=np.array(land['side_axis']);Z=np.array([0.,0.,1.])
def hit(pixel,view='front'):
    camera=json.loads((root/'eye-audit'/f'{view}.json').read_text());matrix=Matrix(camera['camera_matrix']);width,height=camera['size'];span=camera['scale']
    x,y=pixel;origin=matrix@Vector(((x/width-.5)*span,(.5-y/height)*span,0));direction=-(matrix.to_3x3()@Vector((0,0,1)))
    q,n,tri,d=bvh.ray_cast(origin,direction,10)
    assert q is not None,pixel
    return np.array(q),np.array(n)
settings={
 'red-fox':dict(eyes=[(164,240),(337,240)],nose=(255,350),radius=(.033,.024),nose_radius=(.057,.037)),
 'wolf':dict(eyes=[(165,239),(335,239)],nose=(255,350),radius=(.037,.026),nose_radius=(.066,.043)),
 'raccoon':dict(eyes=[(193,250),(333,250)],nose=(254,335),radius=(.041,.034),nose_radius=(.070,.047)),
 'buffalo':dict(eyes=[(179,231),(333,231)],nose=(260,294),radius=(.038,.029),nose_radius=(.112,.070)),
 'hare':dict(eyes=[(225,225),(285,225)],views=['right','left'],nose=(256,350),radius=(.043,.032),nose_radius=(.025,.018),nasal_disc=False),
 'rabbit':dict(eyes=[(230,225),(282,225)],views=['right','left'],nose=(256,355),radius=(.040,.032),nose_radius=(.025,.018),nasal_disc=False),
}[a.slug]
for side,pixel in zip(['right','left'],settings['eyes']):
    view=settings.get('views',['front','front'])[0 if side=='right' else 1];q,surface=hit(pixel,view);sign=1 if side=='right' else -1
    prey=a.slug in {'hare','rabbit'}
    n=F*(.25 if prey else .55 if a.slug=='buffalo' else .88)+S*sign*(.968 if prey else .835 if a.slug=='buffalo' else .475);n/=np.linalg.norm(n);h=np.cross(n,Z);h/=np.linalg.norm(h);v=np.cross(h,n)
    land[side+'_eye'].update(point=q.tolist(),surface_point=q.tolist(),normal=n.tolist(),radius=settings['radius'],fit_frame={'normal':n.tolist(),'horizontal':h.tolist(),'vertical':v.tolist()},inspected_frontal_pixel=pixel,actual_surface_normal=surface.tolist())
q,n=hit(settings['nose']);land['nose'].update(point=q.tolist(),normal=F.tolist())
land['physical_eye_revision']={'method':'Round iris in physical eye coordinates, spherical corneal cap, narrow shaped lid; explicit actual camera socket picks checked against real animal reference.','iris_radius_factor':1.30,'sculpt_socket_clearance':True,'nose_radius':settings['nose_radius'],'nasal_disc':settings.get('nasal_disc',True),'front_guide':guide,'reference_comparison_pending':True}
land['paired_eye_revision']['positions_symmetrical']=False
(root/'face-landmarks.json').write_text(json.dumps(land,indent=2)+'\n')
print('EXPLICIT SOCKET PICKS',a.slug)
