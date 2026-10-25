"""Fit independently inspected socket centres/sizes; never level or mirror eyes."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024'
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
folder=root/a.slug;review=json.loads((folder/'eye-review.json').read_text());state=json.loads((folder/'paint-state.json').read_text())
assert review['geometry_sha256']==state['geometry_sha256']
bpy.ops.wm.open_mainfile(filepath=str(folder/'Animal - Painted Species Review.blend'))
entry=json.loads(bpy.context.scene['deer_four_rigs'])[0];body=bpy.data.objects[entry['body']]
p=np.asarray([v.co[:] for v in body.data.vertices]);body.data.calc_loop_triangles();tri=np.asarray([t.vertices[:] for t in body.data.loop_triangles])
sys.path.insert(0,str(repo/'scripts'));from fauna_surface_components import components
main=max(components(p,tri),key=len);face_ids=[i for i,t in enumerate(tri) if all(int(v) in main for v in t)]
bvh=BVHTree.FromPolygons([Vector(v) for v in p],tri[face_ids].tolist(),all_triangles=True)
landpath=folder/'face-landmarks.json';land=json.loads(landpath.read_text())
for side in ['right','left']:
 spec=review['eyes'][side];camera=json.loads((folder/spec['camera']).read_text());m=Matrix(camera['camera_matrix']);inverse=body.matrix_world.inverted();scale=camera['scale'];width,height=camera['size']
 def cast(pixel,reference=None):
  local=Vector((((pixel[0]+.5)/width-.5)*scale,(.5-(pixel[1]+.5)/height)*scale,0))
  origin=inverse@(m@local);direction=inverse.to_3x3()@(m.to_3x3()@Vector((0,0,-1)))
  point,normal,index,distance=bvh.ray_cast(origin,direction.normalized())
  if point is None and reference is not None:
   # An inspected rim can cross the silhouette of an oblique skull. Use the
   # nearest local surface at the centre's ray depth, not an opposite-side hit.
   plane=origin+direction.normalized()*((Vector(reference)-origin).dot(direction.normalized()))
   point,normal,index,distance=bvh.find_nearest(plane)
   assert distance<camera['scale']*.18,(a.slug,side,'rim too far from skull')
  assert point is not None,(a.slug,side,pixel)
  return np.asarray(point),np.asarray(normal),face_ids[index]
 x,y=spec['pixel'];w,h=spec['size']
 if spec.get('open_socket_center'):
  # Some retained sculpts contain an actual opening rather than a socket floor.
  # Preserve the inspected screen-space centre; infer depth only from its rim.
  rim_hits=[cast((x+dx*w*.6,y+dy*h*.6)) for dx,dy in [(1,0),(-1,0),(0,1),(0,-1)]]
  local=Vector((((x+.5)/width-.5)*scale,(.5-(y+.5)/height)*scale,0))
  origin=np.asarray(inverse@(m@local));direction=np.asarray(inverse.to_3x3()@(m.to_3x3()@Vector((0,0,-1))));direction/=np.linalg.norm(direction)
  mid=np.mean([r[0] for r in rim_hits],axis=0);center=origin+direction*((mid-origin)@direction)
  normal=np.mean([r[1] for r in rim_hits],axis=0);face=rim_hits[0][2]
 else:center,normal,face=cast(spec['pixel'])
 rim=[cast((x+dx*w*.5,y+dy*h*.5),center) for dx,dy in [(1,0),(-1,0),(0,1),(0,-1)]]
 points=np.asarray([r[0] for r in rim]);mid=points.mean(0)
 # Averaging actual rim normals avoids fitting to a single faceted socket floor.
 n=np.mean([r[1] for r in rim],axis=0);n/=max(np.linalg.norm(n),1e-8)
 toward=np.asarray(inverse.to_3x3()@(m.to_3x3()@Vector((0,0,1))))
 if n@toward<0:n=-n
 from fauna_finish_catalog import SUPPORTED as extended,category
 from fauna_paint_recipes import family
 kind=category(a.slug) if a.slug in extended else family(a.slug)
 frontal=spec.get('facing_camera') or spec['camera'].endswith('/front.json')
 f,l=({'canid':(.62,.78),'felid':(.78,.63),'primate':(.97,.24),'bird':(.45,.89),'avian':(.35,.94),'ursid':(.65,.76),'small-carnivore':(.55,.835)}.get(kind,(.30,.954)))
 desired=np.asarray(land['front_axis'])*f+np.asarray(land['side_axis'])*(1 if side=='right' else -1)*l
 if frontal:desired=toward/np.linalg.norm(toward)
 surface_weight=float(spec.get('surface_normal_weight',.75))
 n=n*surface_weight+desired*(1-surface_weight);n/=np.linalg.norm(n)
 # A near-frontal normal makes a projected nose direction numerically unstable
 # and can rotate an almond into a vertical ribbon. Keep the opening level in
 # the head's upright plane, with its inner corner directed toward the muzzle.
 hu=np.cross(np.array([0.,0.,1.]),n);hu/=max(np.linalg.norm(hu),1e-8)
 if hu@(np.asarray(land['nose']['point'])-center)<0:hu=-hu
 vu=np.cross(n,hu)
 if vu[2]<0:vu=-vu
 # Width from opposite rim points; retain the actual opening's local vertical.
 pixel_rx=scale*w/width*.5;pixel_rz=scale*h/height*.5
 # Rim rays can jump onto the cheek across a sculpt crease. Never let their
 # depth difference turn a modest visible opening into an enormous eyeball.
 rx=float(np.clip(np.linalg.norm(points[0]-points[1])*.5,pixel_rx,pixel_rx*1.18))
 rz=float(np.clip(np.linalg.norm(points[2]-points[3])*.5,pixel_rz,pixel_rz*1.12))
 raw=center.copy();lift=np.clip((mid-center)@n,0,min(rx,rz)*.15);center+=n*lift
 land[side+'_eye']={'point':center.tolist(),'surface_point':raw.tolist(),'normal':n.tolist(),'triangle':face,'radius':[float(rx),float(rz)],'fit_frame':{'normal':n.tolist(),'horizontal':hu.tolist(),'vertical':vu.tolist()},'socket_rim_points':points.tolist(),'inspected_pixel':spec['pixel'],'inspected_size':spec['size'],'independently_fitted':True}
land['eye_revision']={'method':'Independently inspected actual socket centre and four rim rays; no forced world-height equality, mirroring or family-wide radius.','geometry_sha256':state['geometry_sha256'],'review':review,'mean_radius':np.mean([land[s+'_eye']['radius'] for s in ['right','left']],axis=0).tolist()}
landpath.write_text(json.dumps(land,indent=2)+'\n')
print('SOCKET FIT',a.slug,json.dumps({s:land[s+'_eye']['radius'] for s in ['right','left']}),flush=True)
