"""Supplementary poses on native bind skeletons; no source mesh, rig or original action edits."""
from pathlib import Path
import gzip,json,struct,hashlib,math
import numpy as np
root=Path(__file__).resolve().parents[1];folder=root/'assets/fauna-motion';index=json.loads((folder/'index.json').read_text());skeletons=json.loads((root/'unity/WorldGen.Unity/Assets/WorldGen/Resources/FaunaPhysicsSkeletons.json').read_text())['species']
def rotation(a,b):
    a=a/np.linalg.norm(a);b=b/np.linalg.norm(b);v=np.cross(a,b);c=float(a@b)
    if c>.999999:return np.eye(3)
    if c<-.99999:
        axis=np.cross(a,np.array([1.,0,0]) if abs(a[0])<.8 else np.array([0.,1,0]));axis/=np.linalg.norm(axis);return 2*np.outer(axis,axis)-np.eye(3)
    K=np.array([[0,-v[2],v[1]],[v[2],0,-v[0]],[-v[1],v[0],0]]);return np.eye(3)+K+K@K/(1+c)
def palette(heads,targets,rotations):
    p=np.tile(np.eye(4),(len(heads),1,1));p[:,:3,:3]=rotations
    p[:,:3,3]=targets-np.einsum('nij,nj->ni',rotations,heads);return p
def support(slug,p):
    base=root/'artifacts/fauna-runtime-20261026/exports'/slug;meta=json.loads((base/'model.json').read_text());raw=(base/'mesh.bin').read_bytes();n,ix,_,_=struct.unpack('<4i',raw[8:24]);stride=(len(raw)-24-ix*4)//n//4;v=np.frombuffer(raw,dtype='<f4',offset=24,count=n*stride).reshape(n,stride);k=meta['influences'];ids=v[:,8:8+k].astype(int);w=v[:,8+k:8+2*k];xyz=np.c_[v[:,:3],np.ones(n)];height=sum(np.einsum('nj,nj->n',p[ids[:,i],1,:],xyz)*w[:,i] for i in range(k));lift=max(0,-float(height.min()));p[:,1,3]+=min(.08,lift);return p
for slug in ['deer','red-kangaroo','wolf']:
    skel=next(s for s in skeletons if s['slug']==slug);heads=np.array([b['head'] for b in skel['bones']]);n=len(heads);targets=heads.copy();rot=np.tile(np.eye(3),(n,1,1));duration=4;frames=[]
    for frame in range(121):
        t=frame/120;breath=math.sin(t*math.pi*2)*.001
        targets=heads.copy();rot=np.tile(np.eye(3),(n,1,1))
        if slug=='deer':
            targets[:,1]-=.23-breath
            # Forelegs fold rearward beneath the chest, with the elbows forward.
            for shoulder,elbow,foot in [(6,7,8),(10,11,12)]:
                sign=-1 if heads[shoulder,0]<0 else 1
                targets[elbow]=[sign*.09,.075,.31];targets[foot]=[sign*.09,.08,.115]
                rot[shoulder]=rotation(heads[elbow]-heads[shoulder],targets[elbow]-targets[shoulder]);rot[elbow]=rotation(heads[foot]-heads[elbow],targets[foot]-targets[elbow]);rot[foot]=rot[elbow]
            # Hind joints retain their opposing folds rather than splaying sideways.
            for hip,knee,hock,ankle,toe in [(23,24,25,26,27),(28,29,30,31,32)]:
                sign=-1 if heads[hip,0]<0 else 1
                targets[knee]=[sign*.08,.21,-.23];targets[hock]=[sign*.08,.055,-.30];targets[ankle]=[sign*.08,.055,-.10];targets[toe]=[sign*.08,.025,-.065]
                for i,j in [(hip,knee),(knee,hock),(hock,ankle),(ankle,toe)]:rot[i]=rotation(heads[j]-heads[i],targets[j]-targets[i])
                rot[toe]=rot[ankle]
            kind='sleep';label='Native fitted bedded rest'
        elif slug=='wolf':
            targets[:,1]-=.255-breath
            for shoulder,elbow,wrist,toe in [(12,13,14,15),(17,18,19,20)]:
                sign=-1 if heads[shoulder,0]<0 else 1
                targets[elbow]=[sign*.09,.045,.24];targets[wrist]=[sign*.09,.035,.415];targets[toe]=[sign*.09,.025,.48]
                for i,j in [(shoulder,elbow),(elbow,wrist),(wrist,toe)]:rot[i]=rotation(heads[j]-heads[i],targets[j]-targets[i])
                rot[toe]=rot[wrist]
            for hip,knee,hock,toe in [(21,22,23,24),(25,26,27,28)]:
                sign=-1 if heads[hip,0]<0 else 1
                targets[knee]=[sign*.1,.075,-.035];targets[hock]=[sign*.11,.04,-.16];targets[toe]=[sign*.11,.025,-.045]
                for i,j in [(hip,knee),(knee,hock),(hock,toe)]:rot[i]=rotation(heads[j]-heads[i],targets[j]-targets[i])
                rot[toe]=rot[hock]
            kind='sleep';label='Native fitted lying rest'
        else:
            theta=math.radians(51+math.sin(t*math.pi*2)*1.2);c,s=math.cos(theta),math.sin(theta);lean=np.array([[1,0,0],[0,c,-s],[0,s,c]])
            pivot=heads[0]
            upper=set([1])
            for i,b in enumerate(skel['bones']):
                if b['parent'] in upper:upper.add(i)
            for i in upper:rot[i]=lean;targets[i]=pivot+lean@(heads[i]-pivot)
            for shoulder,elbow,wrist,finger,tip,fork,forktip in [(18,19,20,21,22,23,24),(25,26,27,28,29,30,31)]:
                start=targets[shoulder].copy();sign=-1 if heads[shoulder,0]<pivot[0] else 1
                goal=np.array([start[0],.06,start[2]+.18]);delta=goal-start;axis=delta/np.linalg.norm(delta);a=np.linalg.norm(heads[elbow]-heads[shoulder]);b=np.linalg.norm(heads[wrist]-heads[elbow]);d=min(np.linalg.norm(delta),(a+b)*.98)
                goal=start+axis*d;hint=np.array([sign*.3,1.,0]);bend=hint-axis*(hint@axis);bend/=np.linalg.norm(bend);along=(a*a+d*d-b*b)/(2*d);height=math.sqrt(max(0,a*a-along*along));targets[elbow]=start+axis*along+bend*height;targets[wrist]=goal
                targets[finger]=goal+np.array([0,-.025,.035]);targets[tip]=targets[finger]+np.array([0,-.005,.045]);targets[fork]=goal;targets[forktip]=targets[finger]+np.array([sign*.015,0,.005])
                for i,j in [(shoulder,elbow),(elbow,wrist),(wrist,finger),(finger,tip),(fork,forktip)]:rot[i]=rotation(heads[j]-heads[i],targets[j]-targets[i])
                rot[tip]=rot[finger];rot[forktip]=rot[fork]
            kind='eating';label='Native grazing stance'
        frames.append(support(slug,palette(heads,targets,rot)))
    path=folder/(slug+'.bin.gz');clips=[];oldframes=[];oldmorph=[]
    if path.exists():
        raw=gzip.decompress(path.read_bytes());bc,nf,js=struct.unpack('<III',raw[40:52]);h=json.loads(raw[52:52+js]);old=np.frombuffer(raw,dtype='<f4',offset=52+js,count=nf*n*16).reshape(nf,n,4,4);om=np.frombuffer(raw,dtype='<f4',offset=52+js+nf*n*64,count=nf)
        for clip in h['clips']:
            if clip['role']==kind:continue
            item=dict(clip,start=len(oldframes));clips.append(item);oldframes.extend(old[clip['start']:clip['start']+clip['frames']]);oldmorph.extend(om[clip['start']:clip['start']+clip['frames']])
    clips.append(dict(name=label,role=kind,start=len(oldframes),frames=len(frames),duration=duration));data=np.array(oldframes+frames,dtype='<f4');morph=np.array(oldmorph+[0]*len(frames),dtype='<f4')
    h=dict(slug=slug,source_sha256=skel['source'],bones=n,frames=len(data),clips=clips,groundSupport=True,supplementalAuthoring='Native bind-joint targets; no foreign skeleton transfer; original source and actions untouched')
    encoded=json.dumps(h,separators=(',',':')).encode();payload=b'WGEXTRA1'+bytes.fromhex(skel['source'])+struct.pack('<III',n,len(data),len(encoded))+encoded+data.tobytes()+morph.tobytes();path.write_bytes(gzip.compress(payload,compresslevel=6,mtime=0))
    record=dict(slug=slug,file=path.name,source_sha256=skel['source'],sha256=hashlib.sha256(path.read_bytes()).hexdigest(),bytes=path.stat().st_size,roles=[c['role'] for c in clips],supplementalAuthoring=h['supplementalAuthoring']);index['animals']=[r for r in index['animals'] if r['slug']!=slug]+[record]
    print(slug,record['bytes'],record['roles'],flush=True)
index['animals'].sort(key=lambda x:x['slug']);index['policy']='Native-rig supplemental motion: original source actions plus documented fitted resting/grazing poses; approved source meshes/rigs/textures/locomotion bundles unchanged.';(folder/'index.json').write_text(json.dumps(index,indent=2)+'\n')
