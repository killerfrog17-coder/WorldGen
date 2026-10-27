"""Read-only ray audit of the exact meshes exported for the game."""
import argparse,json,math,struct,sys
from pathlib import Path
import numpy as np
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree

REPO=Path(__file__).resolve().parent.parent
ROOT=REPO/'artifacts/fauna-eye-closure-repair'
ROOT.mkdir(exist_ok=True)
ap=argparse.ArgumentParser();ap.add_argument('--slugs',nargs='*');args=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
runtime=REPO/'artifacts/fauna-runtime-20261026'
inventory={r['slug']:r for r in json.loads((runtime/'native-inventory.json').read_text())['animals']}
plan=json.loads((runtime/'export-plan.json').read_text())['animals']
rows=[]
for item in plan:
    slug=item['slug']
    if args.slugs and slug not in args.slugs:continue
    folder=Path(item['path']).parent;landfile=folder/'face-landmarks.json'
    if not landfile.exists():continue
    land=json.loads(landfile.read_text());entry=inventory[slug];model=json.loads((runtime/'exports'/slug/'model.json').read_text())
    raw=(runtime/'exports'/slug/'mesh.bin').read_bytes();vc,ic,bc,pc=struct.unpack_from('<4i',raw,8)
    width=9+model['influences']*2+(3 if model.get('hasMorph') else 0)
    vertices=np.frombuffer(raw,dtype='<f4',offset=24,count=vc*width).reshape(vc,width)
    faces=np.frombuffer(raw,dtype='<i4',offset=24+vc*width*4,count=ic).reshape(-1,3)
    body=next(m for m in entry['meshes'] if m['name']==entry['entry']['body'])
    low=np.array(body['bounds'][0]);high=np.array(body['bounds'][1]);span=max(high-low)
    forward=Vector(item['forward']);yaw=math.atan2(forward.x,forward.y)
    axis=Matrix(((1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)))
    C=axis@Matrix.Rotation(yaw,4,'Z')@Matrix.Scale(1/span,4)@Matrix.Translation(Vector((-(low[0]+high[0])*.5,-(low[1]+high[1])*.5,-low[2])))
    if 'source_to_runtime' in model:C=Matrix(model['source_to_runtime'])
    W=Matrix(body['matrix']);M=C@W
    bvh=BVHTree.FromPolygons(vertices[:,:3].tolist(),faces.tolist(),all_triangles=True)
    eyes=[]
    for side in ['right','left']:
        if side+'_eye' not in land:continue
        eye=land[side+'_eye'];frame=eye.get('fit_frame')
        if not frame:continue
        center=np.array(M@Vector(eye['point']));radius=float(eye.get('radius',[land.get('eye_revision',{}).get('mean_radius',[.03])[0]])[0])
        R=(M.to_3x3()@Vector(frame['horizontal'])*radius).length
        n=np.array((M.to_3x3()@Vector(frame['normal'])).normalized());h=np.array((M.to_3x3()@Vector(frame['horizontal'])).normalized());v=np.array((M.to_3x3()@Vector(frame['vertical'])).normalized())
        failures=[];total=0
        directions=[n,(n*.8+h*.6),(n*.8-h*.6),(n*.8+v*.6),(n*.8-v*.6)]
        for di,direction in enumerate(directions):
            direction=direction/np.linalg.norm(direction)
            for radius in [0,.45,.8,1.,1.2,1.4]:
                for angle in np.linspace(0,math.tau,1 if radius==0 else 32,endpoint=False):
                    target=center+h*(R*radius*math.cos(angle))+v*(R*.75*radius*math.sin(angle))
                    origin=target+direction*R*5
                    hit,_,_,distance=bvh.ray_cast(Vector(origin),Vector(-direction),R*10)
                    total+=1
                    if hit is None or distance>R*7:
                        failures.append({'direction':di,'radius':radius,'angle':float(angle),'miss':hit is None,'distance_r':float(distance/R) if distance is not None else None,'target':target.tolist()})
        eyes.append({'side':side,'rays':total,'suspect_rays':len(failures),'failures':failures,'radius_normalized':R,'center':center.tolist()})
    rows.append({'slug':slug,'source':item['path'],'source_sha256':item['source_sha256'],'stitched':(folder/'eye-retopology.json').exists(),'eyes':eyes,'suspect_rays':sum(e['suspect_rays'] for e in eyes)})
    print('EYE_CLOSURE_AUDIT',slug,rows[-1]['suspect_rays'],flush=True)
    (ROOT/'runtime-ray-audit.json').write_text(json.dumps({'scope':'Bind-pose geometric runtime-mesh ray coverage, independent of material alpha. Suspect rays require inspection; outer silhouette rays can be false positives. Use opacity validation to detect the confirmed missing-lid export defect.','animals':rows},indent=2))
print('EYE_CLOSURE_AUDIT_DONE',len(rows),sum(r['suspect_rays']>0 for r in rows),flush=True)
