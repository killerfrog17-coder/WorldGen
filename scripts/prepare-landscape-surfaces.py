"""Fetch free CC0 scans and prepare neutral PBR swatches for existing slots.

Source files are permanent authoring inputs; all runtime processing is offline.
No extra terrain layers, shader passes or runtime texture dimensions are added.
"""
import hashlib
import json
from pathlib import Path
import urllib.request

import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'assets-source/terrain/surfaces/cc0-regions'
OUTPUT=ROOT/'unity/WorldGen.Unity/Assets/WorldGen/Editor/TerrainSurfaceArtV1'
SOURCE.mkdir(parents=True,exist_ok=True)
OUTPUT.mkdir(parents=True,exist_ok=True)
def request(url):
    return urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'WorldGen/1.0 (CC0 game asset preparation)'}),timeout=60)
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def linear(x):return np.where(x<=.04045,x/12.92,((x+.055)/1.055)**2.4)
def srgb(x):return np.where(x<=.0031308,x*12.92,1.055*np.maximum(x,0)**(1/2.4)-.055)
def save(path,data):Image.fromarray(np.uint8(np.clip(data,0,1)*255+.5)).save(path)
materials=[]
for material,asset in [('sand','sand_01'),('gravel','gravel_stones'),('clay','mud_cracked_dry_03')]:
    folder=SOURCE/asset;folder.mkdir(parents=True,exist_ok=True)
    with request('https://api.polyhaven.com/files/'+asset) as response:files=json.load(response)
    with request('https://api.polyhaven.com/info/'+asset) as response:info=json.load(response)
    inputs={}
    for channel in ['Diffuse','nor_gl','arm']:
        metadata=files[channel]['1k']['jpg'];path=folder/(channel+'.jpg')
        if not path.exists() or hashlib.md5(path.read_bytes()).hexdigest()!=metadata['md5']:
            with request(metadata['url']) as response:path.write_bytes(response.read())
        if hashlib.md5(path.read_bytes()).hexdigest()!=metadata['md5']:raise ValueError('Source checksum mismatch: '+str(path))
        inputs[channel]={'path':path.relative_to(ROOT).as_posix(),'url':metadata['url'],'md5':metadata['md5'],'sha256':sha(path),'bytes':path.stat().st_size}
    (folder/'source-metadata.json').write_text(json.dumps({'info':info,'files':inputs,'license':'CC0-1.0','license_url':'https://polyhaven.com/license'},indent=2)+'\n',encoding='utf-8')
    diffuse=np.asarray(Image.open(folder/'Diffuse.jpg').convert('RGB').resize((512,512),Image.Resampling.LANCZOS),dtype=np.float64)/255
    grey=linear(diffuse)@np.array([.2126,.7152,.0722]);target=float(linear(np.array(.5)))
    grey=np.clip(grey*(target/max(.001,grey.mean())),.004,.8);grey*=target/grey.mean()
    albedo=np.repeat(srgb(grey)[:,:,None],3,axis=2)
    normal=np.asarray(Image.open(folder/'nor_gl.jpg').convert('RGB').resize((256,256),Image.Resampling.LANCZOS),dtype=np.float64)/255*2-1
    normal/=np.maximum(.001,np.linalg.norm(normal,axis=2,keepdims=True));normal=normal*.5+.5
    arm=np.asarray(Image.open(folder/'arm.jpg').convert('RGB').resize((128,128),Image.Resampling.LANCZOS),dtype=np.float64)/255
    mask=np.stack([arm[:,:,0],arm[:,:,1],np.full((128,128),.5),arm[:,:,2]],axis=2)
    outputs={}
    for channel,data in [('albedo',albedo),('normal',normal),('mask',mask)]:
        path=OUTPUT/(material+'-'+channel+'.png');save(path,data);outputs[channel]={'path':path.relative_to(ROOT).as_posix(),'sha256':sha(path)}
    if material=='clay':
        # A desiccation-crust pattern, deliberately neutral: the terrain's
        # saline material tint supplies the white mineral colour separately.
        for channel,data in [('albedo',.5+(albedo-.5)*.65),('normal',.5+(normal-.5)*.65),('mask',mask)]:
            path=OUTPUT/('crust-'+channel+'.png');save(path,data)
    materials.append({'material':material,'asset':asset,'url':'https://polyhaven.com/a/'+asset,'license':'CC0-1.0','authors':info.get('authors'),
        'inputs':inputs,'outputs':outputs,'bake':'Neutral linear luminance mean, registered scanned OpenGL normals, AO/roughness/metal masks; 512/256/128 pixels.'})
report={'license_url':'https://polyhaven.com/license','materials':materials,'layers':{'sand':1,'gravel':2,'clay':3,'crust':6},
    'crust_derivative':'Muted cracked-mud desiccation pattern; saline colour comes from the existing ground model, not a claimed salt scan.',
    'runtime_budget':'Existing 14-layer BC7 arrays, unchanged 512/256/128 dimensions and sampling budget.'}
path=ROOT/'assets/verification/landscape-surface-sources-20261103.json';path.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'materials':len(materials),'raw_files':9,'output_swatches':12,'report':str(path)},indent=2))
