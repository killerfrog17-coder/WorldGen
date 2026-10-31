"""Independent actual-skin/prop review. Uses exported native triangles rather
than the runtime clearance capsule approximation. Reports, never hides failures.
"""
import gzip
import argparse,json
from pathlib import Path
import numpy as np

def xyz(rows):return np.array([[v['x'],v['y'],v['z']] for v in rows],dtype=float)

def intersections(a,b,triangles):
 direction=b-a;e1=triangles[:,1]-triangles[:,0];e2=triangles[:,2]-triangles[:,0]
 h=np.cross(direction,e2);det=np.einsum('ij,ij->i',e1,h);valid=np.abs(det)>1e-10
 inverse=np.zeros_like(det);inverse[valid]=1/det[valid];s=a-triangles[:,0]
 u=inverse*np.einsum('ij,ij->i',s,h);q=np.cross(s,e1);v=inverse*np.einsum('j,ij->i',direction,q);t=inverse*np.einsum('ij,ij->i',e2,q)
 return t[valid&(u>=0)&(v>=0)&(u+v<=1)&(t>.003)&(t<.997)]

def main():
 p=argparse.ArgumentParser();p.add_argument('folder');a=p.parse_args();folder=Path(a.folder);records=[];invalid=[];surface_count=0
 for path in sorted([*folder.glob('*.json'),*folder.glob('*.json.gz')]):
  if path.name.startswith('audit'):continue
  data=json.loads((gzip.decompress(path.read_bytes()) if path.suffix=='.gz' else path.read_bytes()).decode('utf-8-sig'))
  if not isinstance(data,list) or not data or 'BoneNames' not in data[0]:continue
  for frame in data:
   hits=[]
   if frame.get('Vertices') and frame.get('CoreTriangles') and frame.get('PropPoints'):
    surface_count+=1
    vertices=xyz(frame['Vertices'])
    if np.max(np.abs(vertices))>5:
     invalid.append({'file':path.name,'time':frame['Time'],'reason':'Skin vertices are not in native metre coordinates.'});continue
    triangles=vertices[np.array(frame['CoreTriangles']).reshape(-1,3)]
    for label,raw in [('prop',frame['PropPoints']),('ammunition',frame.get('AmmoPoints'))]:
     if not raw:continue
     points=xyz(raw)
     for j,(start,end) in enumerate(zip(points[:-1],points[1:])):
      for t in intersections(start,end,triangles):hits.append({'part':label,'segment':j,'t':float(t),'point':(start+(end-start)*t).tolist()})
   records.append({'file':path.name,'variant':frame['Variant'],'role':frame['Role'],'time':frame['Time'],'item':frame['Item'],
                   'actual_skin_crossings':len(hits),'hits':hits,'capsule_clearance':frame['Clearance'],'grip_error':frame['GripError'],'support_error':frame.get('SupportError',0)})
 result={'frames':len(records),'surface_frames':surface_count,
         'invalid_surface_frames':invalid,
         'crossing_frames':[r for r in records if r['actual_skin_crossings']], 'worst_grip_error':max((r['grip_error'] for r in records),default=0),'records':records,
         'scope':'Native centerline/actual torso-thigh-head skin intersections. Separate image/moving review required; not a universal contact guarantee.'}
 (folder/'audit-actual-skin.json').write_text(json.dumps(result,indent=2))
 print('NATIVE SURFACE AUDIT',result['frames'],'frames;',len(result['crossing_frames']),'crossing frames; worst grip',result['worst_grip_error'])
 if invalid:raise RuntimeError(f'INVALID: {len(invalid)} surface frames have incorrect scale; intersection counts cannot be accepted.')
 for row in result['crossing_frames'][:30]:print(row['variant'],row['role'],row['item'],row['time'],row['hits'])

if __name__=='__main__':main()
