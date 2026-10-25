"""Owner-authorized boa body rig from a traced mesh centerline, no neural rig."""
import argparse,heapq,hashlib,json
from pathlib import Path
import cv2,numpy as np
from scipy.ndimage import gaussian_filter1d
from scipy.spatial import cKDTree

p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);a=p.parse_args();root=a.root.resolve()
assert root.name=='boa-constrictor' and root.parent.name=='fauna-overnight-20261024'

def thin(mask):
    img=np.pad(mask.astype(bool),1)
    for iteration in range(300):
        changed=False
        for phase in (0,1):
            z=img[1:-1,1:-1];ns=[img[:-2,1:-1],img[:-2,2:],img[1:-1,2:],img[2:,2:],img[2:,1:-1],img[2:,:-2],img[1:-1,:-2],img[:-2,:-2]]
            count=sum(n.astype(np.uint8) for n in ns);cross=sum((~ns[k]&ns[(k+1)%8]).astype(np.uint8) for k in range(8))
            x,y,w,t=ns[0],ns[2],ns[4],ns[6]
            gate=(~(x&y&w)&~(y&w&t)) if phase==0 else (~(x&y&t)&~(x&w&t))
            erase=z&(count>=2)&(count<=6)&(cross==1)&gate
            if erase.any():z[erase]=False;changed=True
        if not changed:break
    return img[1:-1,1:-1]

def centerline(vertices,faces):
    lo=vertices[:,:2].min(0);span=np.ptp(vertices[:,:2],axis=0);scale=480/max(span);xy=(vertices[:,:2]-lo)*scale+16
    shape=np.ceil(span[::-1]*scale+34).astype(int);mask=np.zeros(shape,np.uint8)
    for tri in faces:cv2.fillConvexPoly(mask,np.rint(xy[tri]).astype(np.int32),1)
    mask=cv2.morphologyEx(mask,cv2.MORPH_CLOSE,np.ones((3,3),np.uint8))
    skeleton=thin(mask);pixels=np.argwhere(skeleton);lookup={tuple(x):i for i,x in enumerate(pixels)};adj=[[] for _ in pixels]
    for i,(y,x) in enumerate(pixels):
        for dy in (-1,0,1):
            for dx in (-1,0,1):
                n=lookup.get((y+dy,x+dx))
                if n is not None and n!=i:adj[i].append((n,float(np.hypot(dx,dy))))
    assert len(pixels)>30
    def far(start):
        dist={start:0.};prev={};heap=[(0.,start)]
        while heap:
            d,n=heapq.heappop(heap)
            if d!=dist[n]:continue
            for child,w in adj[n]:
                if d+w<dist.get(child,float('inf')):dist[child]=d+w;prev[child]=n;heapq.heappush(heap,(d+w,child))
        return max(dist,key=dist.get),prev,dist
    first=max(range(len(adj)),key=lambda i:len(adj[i]));start,_,_=far(first);end,prev,dist=far(start)
    path=[end]
    while path[-1]!=start:path.append(prev[path[-1]])
    points=pixels[path][:,::-1].astype(float)
    distance=cv2.distanceTransform(mask,cv2.DIST_L2,5)
    radii=[float(distance[tuple(pixels[n])]) for n in path]
    if np.mean(radii[:10])<np.mean(radii[-10:]):points=points[::-1]
    points=(points-16)/scale+lo
    arc=np.r_[0,np.cumsum(np.linalg.norm(np.diff(points,axis=0),axis=1))];assert arc[-1]>max(span)*1.2
    t=np.linspace(0,arc[-1],36);out=np.stack([np.interp(t,arc,points[:,k]) for k in (0,1)],axis=1)
    near=cKDTree(vertices[:,:2]).query(out,k=min(16,len(vertices)))[1];z=np.median(vertices[near,2],axis=1)
    joints=np.c_[gaussian_filter1d(out[:,0],.65),gaussian_filter1d(out[:,1],.65),gaussian_filter1d(z,1.)]
    return joints,{'projected_skeleton_pixels':len(pixels),'path_pixels':len(path),'path_length_m':float(arc[-1])}

for number in range(1,5):
    f=root/f'candidate-{number}';old=f/'generated-rig.neural-rejected.npz';old_meta=old.with_suffix('.json');current=f/'generated-rig.npz'
    if not old.exists():
        assert json.loads(current.with_suffix('.json').read_text())['method']=='Local SkinTokens skeleton and skin generation'
        current.rename(old);current.with_suffix('.json').rename(old_meta)
    raw=np.load(old);v=raw['source_vertices'].copy();faces=raw['source_faces'].copy();scale=1.65/float(np.ptp(v,axis=0).max());v*=scale
    joints,details=centerline(v,faces);starts=joints[:-1];vectors=np.diff(joints,axis=0);l2=np.maximum(np.sum(vectors*vectors,axis=1),1e-10)
    t=np.clip(np.sum((v[:,None]-starts)*vectors,axis=2)/l2,0,1);projection=starts+t[:,:,None]*vectors
    segment=np.argmin(np.linalg.norm(v[:,None]-projection,axis=2),axis=1);u=t[np.arange(len(v)),segment]
    skin=np.zeros((len(v),len(joints)),np.float32);skin[np.arange(len(v)),segment]=1-u;skin[np.arange(len(v)),segment+1]=u
    # Adjacent rest-bone influences follow traced arc, never another coil.
    parents=np.arange(len(joints))-1
    np.savez_compressed(current,source_vertices=v,source_faces=faces,joints=joints,parents=parents,sample_points=v,sample_skin=skin)
    meta={'method':'Owner-authorized body centerline rig','mesh':str((f/'model.glb').resolve()),'source_mesh_sha256':hashlib.sha256((f/'model.glb').read_bytes()).hexdigest(),'manual_rig':True,'neural_rig_replaced':True,'joints':len(joints),'uniform_review_scale':scale,'bone_placement':'36 ordered head-to-tail joints along thinned mesh silhouette; visual inspection recorded separately','weights':'At most two adjacent traced body bones; no donor geometry','details':details}
    current.with_suffix('.json').write_text(json.dumps(meta,indent=2));print('Manual body rig',number,details,flush=True)
