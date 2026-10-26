"""Separate actual UV overlaps for colour painting; retain original UV layers."""
import json,math,hashlib
import numpy as np

def area(p):
    return abs(sum(float(np.cross(p[i],p[(i+1)%len(p)])) for i in range(len(p))))*.5 if len(p)>=3 else 0.

def intersection(first,second):
    polygon=[p.copy() for p in first];sign=1 if np.cross(second[1]-second[0],second[2]-second[0])>=0 else -1
    for i,A in enumerate(second):
        B=second[(i+1)%3];edge=B-A;output=[]
        for j,P in enumerate(polygon):
            Q=polygon[(j+1)%len(polygon)];dp=sign*np.cross(edge,P-A);dq=sign*np.cross(edge,Q-A)
            if dp>=-1e-12:output.append(P)
            if (dp>=0)!=(dq>=0):output.append(P+(Q-P)*(dp/(dp-dq)))
        polygon=output
        if not polygon:return 0.
    return area(polygon)

def prepare(body,root,size=2048):
    mesh=body.data;mesh.calc_loop_triangles();original=mesh.uv_layers['UVMap'];triangles=np.array([[original.data[i].uv[:] for i in f.loops] for f in mesh.loop_triangles]);assert all(len(f.vertices)==3 for f in mesh.polygons)
    uv_hash=hashlib.sha256(triangles.astype(np.float32).tobytes()).hexdigest();cache_path=root/'paint-uv-correction.json';cache=json.loads(cache_path.read_text()) if cache_path.exists() else {}
    cached=cache.get('source_uv_sha256')==uv_hash and cache.get('algorithm_version')==1
    lo=triangles.min(1);hi=triangles.max(1);bad=set(cache['repaired_triangle_indices']) if cached else {i for i,t in enumerate(triangles) if area(t)<1e-12 or lo[i].min()<0 or hi[i].max()>1};buckets={};overlaps=cache['overlap_pairs'] if cached else [];grid=64
    for i in ([] if cached else range(len(triangles))):
        low=np.maximum(np.floor(lo[i]*grid).astype(int),0);high=np.minimum(np.floor(hi[i]*grid).astype(int),grid-1)
        candidates=set()
        for y in range(low[1],high[1]+1):
            for x in range(low[0],high[0]+1):candidates.update(buckets.setdefault((x,y),[]))
        for j in candidates:
            if np.any(np.minimum(hi[i],hi[j])-np.maximum(lo[i],lo[j])<=1e-10):continue
            if intersection(triangles[i],triangles[j])>1e-10:bad.update([i,j]);overlaps.append([j,i])
        for y in range(low[1],high[1]+1):
            for x in range(low[0],high[0]+1):buckets[(x,y)].append(i)
    uv=mesh.uv_layers.new(name='Fauna Paint UV');band=min(.40,max(.08,len(bad)/max(1,len(triangles))*1.6)) if bad else 0.;coords=triangles.copy();coords[:,:,1]=coords[:,:,1]*(1-band)+band
    columns=max(1,math.ceil(math.sqrt(len(bad)/max(band,1e-8))));rows=max(1,math.ceil(len(bad)/columns));cell=np.array([1/columns,band/rows]);padding=np.minimum(cell*.20,np.maximum(3/size,cell*.05))
    for index,face in enumerate(sorted(bad)):
        lower=np.array([index%columns*cell[0],index//columns*cell[1]])+padding;upper=lower+cell-2*padding;coords[face]=[lower,[upper[0],lower[1]],[lower[0],upper[1]]]
    for tri,face in zip(mesh.loop_triangles,coords):
        for loop,point in zip(tri.loops,face):uv.data[loop].uv=point
    mesh.uv_layers.active=original;original.active_render=True
    body['fauna_colour_uv_signature']=uv_hash
    report={'method':'Original UVMap retained. Positive-area overlapping/degenerate/out-of-tile colour faces receive unique cells in a reserved atlas strip; other colour UVs receive one common vertical affine transform.','source_uv_sha256':uv_hash,'algorithm_version':1,'original_uv':'UVMap','colour_uv':'Fauna Paint UV','overlap_pairs':overlaps,'repaired_triangle_indices':sorted(bad),'repaired_faces':len(bad),'total_faces':len(triangles),'reserved_fraction':band,'grid':[columns,rows],'atlas_size':size,'appearance_approved':False}
    (root/'paint-uv-correction.json').write_text(json.dumps(report,indent=2)+'\n');return report
