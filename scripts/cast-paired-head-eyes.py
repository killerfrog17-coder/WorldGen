"""Fit a bilateral eye pair to actual skull surfaces in a reviewed head plane."""
import argparse,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree
repo=Path(__file__).resolve().parent.parent;ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);folder=repo/'artifacts/fauna-review-revisions'/a.slug;original=repo/'artifacts/fauna-painted-20261024'/a.slug
bpy.ops.wm.open_mainfile(filepath=str(folder/'Animal - Painted Species Review.blend'));entry=json.loads(bpy.context.scene['deer_four_rigs'])[0];body=bpy.data.objects[entry['body']];body.data.calc_loop_triangles();p=np.array([v.co[:] for v in body.data.vertices]);tri=np.array([t.vertices[:] for t in body.data.loop_triangles]);sys.path.insert(0,str(repo/'scripts'));from fauna_surface_components import components
parts=components(p,tri);main=set().union(*[part for part in parts if len(part)>=12]) if a.slug=='pronghorn' else max(parts,key=len);ids=[i for i,t in enumerate(tri) if all(int(v) in main for v in t)];bvh=BVHTree.FromPolygons([Vector(v) for v in p],tri[ids].tolist(),all_triangles=True)
land=json.loads((folder/'face-landmarks.json').read_text());old=json.loads((original/'face-landmarks.json').read_text());plane=json.loads((folder/'head-plane-fit.json').read_text());F=np.array(plane['forward']);S=np.array(plane['side']);up=np.array([0.,0.,1.]);depth=plane['eye_depth'];height=plane['z']-.006;d=plane.get('lateral_center',float(np.array(land['nose']['point'])@S));mid=F*depth+S*d;mid[2]=height;hits=[]
for sign in [1,-1]:
 hit=bvh.ray_cast(Vector(mid+S*sign),Vector(-S*sign),2.)
 if hit[0] is None and plane.get('manual_visual_adjustment',{}).get('open_socket_fallback'):
  side='right' if sign==1 else 'left';point=mid+S*float((np.array(old[side+'_eye']['point'])-mid)@S);nearest=bvh.find_nearest(Vector(point));hit=(Vector(point),nearest[1],nearest[2],0.)
 assert hit[0] is not None;hits.append(hit)
half=np.mean([abs((np.array(hit[0])-mid)@S) for hit in hits]);radii=np.mean([old['right_eye']['radius'],old['left_eye']['radius']],axis=0)
from fauna_paint_recipes import family
from fauna_finish_catalog import SUPPORTED,category
kind=category(a.slug) if a.slug in SUPPORTED else family(a.slug)
facing={'canid':.78,'felid':.86,'primate':.95,'ursid':.74,'bear':.74,'lagomorph':.20,'rodent':.32,'small-carnivore':.60,'beaver':.32,'ungulate':.30,'cervid':.30,'caprine':.30,'bovine':.32,'equine':.28,'suine':.40,'fish':.16,'marine':.18,'reptile':.27,'amphibian':.35,'turtle':.30,'snake':.18,'bird':.35,'avian':.35,'bat':.60}.get(kind,.38)
if a.slug in {'owl','snowy-owl'}:facing=.96
if a.slug in {'hawk','falcon','forest-eagle','fish-eagle'}:facing=.70
manual=plane.get('manual_visual_adjustment',{});facing=manual.get('facing',facing)
if manual.get('recenter_nose'):
 nose=np.array(land['nose']['point']);target=F*float(nose@F)+S*d;target[2]=nose[2];hit=bvh.ray_cast(Vector(target+F*2),Vector(-F),4.)
 if hit[0] is not None:land['nose'].update(point=list(hit[0]),normal=list(hit[1]),triangle=ids[hit[2]])
if kind in {'canid','felid'}:radii[1]*=.80
for (side,sign),hit in zip([('right',1),('left',-1)],hits):
 center=np.array(old[side+'_eye']['point']) if manual.get('independent_socket_centers') else np.array(hit[0]);center[2]=height;normal=F*facing+S*sign*np.sqrt(1-facing**2);horizontal=np.cross(up,normal)
 if manual.get('socket_pixel_picks'):
  pick=manual['socket_pixel_picks'][side];guide=json.loads((folder/'eye-audit'/(side+'.json')).read_text());m=Matrix(guide['camera_matrix']);scale=guide['scale'];width,pixels_high=guide['size'];x,y=pick['pixel'];w,h=pick['size'];direction=(m.to_3x3()@Vector((0,0,-1))).normalized()
  def ray(px,py):
   origin=m@Vector((((px+.5)/width-.5)*scale,(.5-(py+.5)/pixels_high)*scale,0));point=bvh.ray_cast(origin,direction,10)[0];assert point is not None;return np.array(point)
  support=np.mean([ray(x+dx*w*.55,y+dy*h*.55) for dx,dy in [(1,0),(-1,0),(0,1),(0,-1)]],axis=0);origin=np.array(m@Vector((((x+.5)/width-.5)*scale,(.5-(y+.5)/pixels_high)*scale,0)));view_dir=np.array(direction);center=origin+view_dir*((support-origin)@view_dir);radii=np.array([scale*w/width*.5,scale*h/pixels_high*.5]);center[2]=height
 if manual.get('retain_source_socket_frames'):
  center=np.array(old[side+'_eye']['point']);center[2]=height;normal=np.array(old[side+'_eye']['normal']);normal/=np.linalg.norm(normal);horizontal=np.cross(up,normal)
 if horizontal@(np.array(land['nose']['point'])-center)<0:horizontal=-horizontal
 horizontal/=np.linalg.norm(horizontal);vertical=np.cross(normal,horizontal)
 if vertical[2]<0:vertical=-vertical
 land[side+'_eye'].update(point=center.tolist(),surface_point=list(hit[0]),normal=normal.tolist(),radius=radii.tolist(),triangle=ids[hit[2]],fit_frame={'normal':normal.tolist(),'horizontal':horizontal.tolist(),'vertical':vertical.tolist()})
land['front_axis']=F.tolist();land['side_axis']=S.tolist();land['eye_revision']['mean_radius']=radii.tolist();land['paired_eye_revision']={'scope':'Matched bilateral eye positions in fitted skull frame, equal depth/height/aperture and mirrored gaze. Actual paired skull ray hits retained as support evidence.','head_plane':plane,'positions_symmetrical':True,'convex_lens':manual.get('convex_lens',False),'lens_outset':manual.get('lens_outset',0),'agent_visually_checked':False};(folder/'face-landmarks.json').write_text(json.dumps(land,indent=2)+'\n')
# Camera directions must follow the final head frame rather than stale anchors.
target=mid*.88+np.array(land['nose']['point'])*.12
for view,direction in [('front',F),('right',S),('left',-S)]:
 file=folder/'eye-audit'/(view+'.json');guide=json.loads(file.read_text());location=target+direction*4+up*.05;zaxis=location-target;zaxis/=np.linalg.norm(zaxis);xaxis=np.cross(up,zaxis);xaxis/=np.linalg.norm(xaxis);yaxis=np.cross(zaxis,xaxis);matrix=np.eye(4);matrix[:3,:3]=np.stack([xaxis,yaxis,zaxis],axis=1);matrix[:3,3]=location;guide['camera_matrix']=matrix.tolist();file.write_text(json.dumps(guide,indent=2)+'\n')
print('HEAD-SPACE EYE PAIR',a.slug,'spacing',half*2,'radii',radii)
