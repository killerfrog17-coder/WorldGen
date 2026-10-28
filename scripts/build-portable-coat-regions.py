"""Read-only torso UV analysis; writes numeric sampling rectangles, no repainted fauna."""
from pathlib import Path
import json,struct,hashlib
import numpy as np
from PIL import Image,ImageDraw
from scipy.ndimage import distance_transform_cdt
root=Path(__file__).resolve().parents[1];exports=root/'artifacts/fauna-runtime-20261026/exports'
rows=[]
for folder in sorted(exports.iterdir()):
    if not (folder/'model.json').exists():continue
    meta=json.loads((folder/'model.json').read_text());data=(folder/'mesh.bin').read_bytes();n,nt,bones,parts=struct.unpack_from('<4i',data,8)
    stride=8+meta['influences']*2+1+(3 if meta.get('hasMorph') else 0)
    v=np.frombuffer(data,dtype='<f4',count=n*stride,offset=24).reshape(n,stride);tri=np.frombuffer(data,dtype='<i4',count=nt,offset=24+n*stride*4).reshape(-1,3)
    pos=v[:,:3];uv=v[:,6:8];low=pos.min(axis=0);size=np.maximum(pos.max(axis=0)-low,1e-5);p=(pos-low)/size
    along=2 if size[2]>=size[0] else 0;across=0 if along==2 else 2
    torso=(p[:,1]>.35)&(p[:,1]<.8)&(p[:,along]>.22)&(p[:,along]<.76)&(np.abs(p[:,across]-.5)>.16)
    selected=tri[np.all(torso[tri],axis=1)]
    if len(selected)<3:selected=tri[np.all((p[:,1]>.25)[tri]&(p[:,1]<.85)[tri],axis=1)]
    mask=Image.new('L',(256,256));draw=ImageDraw.Draw(mask)
    for t in selected:draw.polygon([(float(uv[i,0])*255,float(uv[i,1])*255) for i in t],fill=1)
    distance=distance_transform_cdt(np.pad(np.asarray(mask,dtype=np.uint8),1),metric='chessboard')[1:-1,1:-1];y,x=np.unravel_index(distance.argmax(),distance.shape);r=max(1,int(distance[y,x]*.8))
    rect=[max(0,(x-r)/255),max(0,(y-r)/255),min(1,(x+r)/255),min(1,(y+r)/255)]
    rows.append({'slug':meta['slug'],'rect':rect,'coats':len(meta['coats']),'mesh_sha256':hashlib.sha256(data).hexdigest()})
out=root/'unity/WorldGen.Unity/Assets/WorldGen/Resources/PortableCoatRegions.json';out.write_text(json.dumps({'regions':rows},separators=(',',':')))
print('Wrote numeric torso UV regions for',len(rows),'existing source atlases')
