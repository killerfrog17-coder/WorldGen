"""Normalize ground support of supplemental palettes, leaving approved source/bundles untouched."""
from pathlib import Path
import gzip,json,struct,hashlib
import numpy as np
root=Path(__file__).resolve().parents[1];folder=root/'assets/fauna-motion';index=json.loads((folder/'index.json').read_text())
catalogue=json.loads((root/'unity/WorldGen.Unity/Assets/WorldGen/Resources/WorldGenFaunaCatalogue.json').read_text())['animals']
medium={a['slug']:a['medium'] for a in catalogue};changed=0
for bank in index['animals']:
    if medium.get(bank['slug'])!='ground':continue
    path=folder/bank['file'];raw=gzip.decompress(path.read_bytes());bones,frames,js=struct.unpack('<III',raw[40:52]);header=json.loads(raw[52:52+js])
    if header.get('groundSupport'):continue
    base=root/'artifacts/fauna-runtime-20261026/exports'/bank['slug'];model=json.loads((base/'model.json').read_text());mesh=(base/'mesh.bin').read_bytes();n,indices,_,_=struct.unpack('<4i',mesh[8:24]);stride=(len(mesh)-24-indices*4)//n//4
    vertices=np.frombuffer(mesh,dtype='<f4',offset=24,count=n*stride).reshape(n,stride);influences=model['influences'];ids=vertices[:,8:8+influences].astype(int);weights=vertices[:,8+influences:8+2*influences]
    poses=np.frombuffer(raw,dtype='<f4',offset=52+js,count=frames*bones*16).copy().reshape(frames,bones,4,4)
    morph=np.frombuffer(raw,dtype='<f4',offset=52+js+frames*bones*64,count=frames).copy();max_lift=0
    for frame in range(frames):
        xyz=vertices[:,:3].copy()
        if model.get('hasMorph'):xyz+=vertices[:,-3:]*morph[frame]
        p=np.c_[xyz,np.ones(n)];height=sum(np.einsum('nj,nj->n',poses[frame,ids[:,i],1,:],p)*weights[:,i] for i in range(influences))
        lift=max(0,min(.18,-float(height.min())))
        if lift>.001:poses[frame,:,1,3]+=lift;max_lift=max(max_lift,lift)
    header['groundSupport']=True;header['maximumSupportLift']=max_lift;data=json.dumps(header,separators=(',',':')).encode()
    payload=raw[:40]+struct.pack('<III',bones,frames,len(data))+data+poses.astype('<f4').tobytes()+morph.astype('<f4').tobytes()
    path.write_bytes(gzip.compress(payload,compresslevel=6,mtime=0));bank['sha256']=hashlib.sha256(path.read_bytes()).hexdigest();bank['bytes']=path.stat().st_size;bank['maximumSupportLift']=max_lift;changed+=1
    print(bank['slug'],round(max_lift,4),flush=True)
index['policy']='Additional original rig palettes; horizontal root motion removed and bounded vertical ground support normalized. Original model/skin/locomotion bundles unchanged.'
(folder/'index.json').write_text(json.dumps(index,indent=2)+'\n');print('SUPPORTED',changed,flush=True)
