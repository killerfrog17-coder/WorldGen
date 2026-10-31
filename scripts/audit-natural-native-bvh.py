"""Independent triangle-level collision audit of actual native body/prop exports.
Run in Blender. BVH narrows candidate pairs; segment/triangle tests confirm them.
No claim about unsampled frames or an artist's visual acceptance.
"""
import argparse,json,sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

def vectors(rows):return [Vector((v['x'],v['y'],v['z'])) for v in rows]
def faces(indices):return [tuple(indices[i:i+3]) for i in range(0,len(indices),3)]

def edge_hit(a,b,triangle):
 c,d,e=triangle;direction=b-a;e1=d-c;e2=e-c;h=direction.cross(e2);det=e1.dot(h)
 if abs(det)<1e-10:return False
 inv=1/det;s=a-c;u=inv*s.dot(h)
 if u<0 or u>1:return False
 q=s.cross(e1);v=inv*direction.dot(q)
 if v<0 or u+v>1:return False
 t=inv*e2.dot(q)
 return .0001<t<.9999

def triangle_hit(a,b):
 return any(edge_hit(a[i],a[(i+1)%3],b) or edge_hit(b[i],b[(i+1)%3],a) for i in range(3))

def main():
 p=argparse.ArgumentParser();p.add_argument('folder');a=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
 folder=Path(a.folder);bad=[];frames=0;parts=0;invalid=[]
 for path in sorted(folder.glob('*.json')):
  if path.name.startswith('audit'):continue
  data=json.loads(path.read_text(encoding='utf-8-sig'))
  if not isinstance(data,list):continue
  for row in data:
   if not isinstance(row,dict) or not row.get('Vertices'):continue
   body=vectors(row['Vertices']);tri=faces(row['CoreTriangles']);frames+=1
   if max(abs(x) for v in body for x in v)>5:invalid.append(path.name);continue
   tree=BVHTree.FromPolygons(body,tri,all_triangles=True)
   for label,vertices,indices in [('prop',row.get('PropVertices'),row.get('PropTriangles')),('ammunition',row.get('AmmoVertices'),row.get('AmmoTriangles'))]:
    if not vertices or not indices:continue
    points=vectors(vertices);f=faces(indices);other=BVHTree.FromPolygons(points,f,all_triangles=True);hits=[];parts+=1
    for i,j in tree.overlap(other):
     x=tuple(body[k] for k in tri[i]);y=tuple(points[k] for k in f[j])
     if triangle_hit(x,y):
      center=sum(y,Vector())/3;hits.append({'skin_triangle':i,'prop_triangle':j,'near':list(center)})
    if hits:bad.append({'file':path.name,'variant':row['Variant'],'role':row['Role'],'time':row['Time'],'part':label,'pairs':len(hits),'examples':hits[:8]})
 result={'surface_frames':frames,'actual_prop_meshes':parts,'invalid_scale_frames':invalid,'penetrating_frames':bad,
         'scope':'Actual exported body-core/prop/ammunition triangle crossings in sampled native poses. Body core excludes intended hand grips. Separate moving/visual review required.'}
 (folder/'audit-triangles.json').write_text(json.dumps(result,indent=2))
 print('ACTUAL NATIVE TRIANGLE AUDIT',frames,'surface frames;',parts,'prop meshes;',len(bad),'penetrating part frames;',len(invalid),'invalid scale frames',flush=True)
 for b in bad[:40]:print(b['variant'],b['role'],b['time'],b['part'],b['pairs'],b['examples'][0],flush=True)
 if invalid or not frames or not parts:raise RuntimeError('Triangle evidence incomplete/invalid')
 if bad:raise RuntimeError('Actual native body/prop triangle crossings require review/correction')

if __name__=='__main__':main()
