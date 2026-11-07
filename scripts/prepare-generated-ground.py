"""Compile image-generated ground into existing terrain/cover material layouts.

No runtime synthesis, extra layers or sampling. Relief/roughness are authoring
approximations derived from generated colour, not physically measured scans.
"""
from pathlib import Path
import json,hashlib,importlib.util
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'unity/WorldGen.Unity/Assets/WorldGen/Editor/TerrainSurfaceArtV1'
RES=ROOT/'unity/WorldGen.Unity/Assets/WorldGen/Resources'
PROMPTS=ROOT/'assets/verification/ground-generation-prompts-20261107.json'
spec=importlib.util.spec_from_file_location('bark_authoring',ROOT/'scripts/prepare-tree-bark.py')
util=importlib.util.module_from_spec(spec);spec.loader.exec_module(util)
linear,srgb,periodic,close_edges,unit=util.linear,util.srgb,util.periodic,util.close_edges,util.unit

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def save(p,a):
    p.parent.mkdir(parents=True,exist_ok=True)
    Image.fromarray(np.uint8(np.clip(a,0,1)*255+.5)).save(p)
def resize(a,size):
    return np.asarray(Image.fromarray(np.float32(a)).resize((size,size),Image.Resampling.BOX),dtype=float)
def colour(source):
    a=linear(np.asarray(Image.open(source).convert('RGB').resize((1024,1024),Image.Resampling.LANCZOS),dtype=float)/255)
    lum=a@np.array([.2126,.7152,.0722]);light=gaussian_filter(lum,120,mode='wrap')
    a*=np.clip(np.mean(lum)/np.maximum(.005,light),.85,1.18)[:,:,None]
    # Macro/ecological colour is supplied by existing terrain data. Neutralize
    # only the mean, retaining each pixel's relative mineral/leaf variation.
    target=float(linear(np.array(.5)))
    for _ in range(8):
        a*=target/np.maximum(.001,a.mean(axis=(0,1)))
        a=np.where(a<.7,a,.7+.27*(1-np.exp(-np.maximum(0,a-.7)/.27)))
    return np.clip(close_edges(periodic(a)),.001,.98)
def relief(c,size,metres,amount):
    h=resize(c@np.array([.2126,.7152,.0722]),size)
    h=np.log(np.maximum(.005,h));h=gaussian_filter(h,.8,mode='wrap')-gaussian_filter(h,12,mode='wrap')
    h*=amount/max(.08,float(np.std(h)))
    dx=(np.roll(h,-1,1)-np.roll(h,1,1))*size/(2*metres)
    dy=(np.roll(h,-1,0)-np.roll(h,1,0))*size/(2*metres)
    # Terrain's existing xz projection maps texture v toward world z.
    n=unit(close_edges(unit(np.stack([-dx,-dy,np.ones_like(dx)],axis=2))))
    return h,n
def validate(p):
    a=np.asarray(Image.open(p))
    assert np.array_equal(a[0],a[-1]) and np.array_equal(a[:,0],a[:,-1]),str(p)
    return {'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p),'size':list(a.shape[:2])}
def build(only=None):
    OUT.mkdir(parents=True,exist_ok=True);records=[]
    strengths={'sand':.0015,'gravel':.006,'clay':.0025,'granite':.003,'basalt':.003,'sandstone':.0025,'limestone':.003,'mudstone':.003,'metamorphic':.003,'broadleaf':.0035,'needles':.002,'damp':.003,'moss':.00055,'lichen':.0007,'ice':.0015,'salt':.001}
    for source in json.loads(PROMPTS.read_text(encoding='utf-8'))['assets']:
        if only is not None and source['key']!=only:continue
        key=source['key'];master=ROOT/source['master'];c=colour(master);outputs=[]
        for layer in source['layers']:
            h,n=relief(c,512,source['repeat_metres'],strengths.get(key,.0018))
            small=resize(h,128);v=np.clip(small/max(.001,float(np.std(small))),-2,2)
            rough=.65 if key=='ice' else .93 if key in ['sand','salt','organic'] else .88
            mask=np.stack([np.clip(.94+v*.015,.85,1),np.clip(rough-v*.015,.5,.99),np.clip(.5+v*.1,0,1),np.zeros_like(v)],axis=2)
            arrays={'albedo':srgb(c),'normal':n*.5+.5,'mask':close_edges(mask)}
            for channel,a in arrays.items():
                p=OUT/f'layer-{layer:02d}-{channel}.png';save(p,a);outputs.append(validate(p))
            assert np.max(np.abs(np.linalg.norm(n,axis=2)-1))<1e-9
        if source['cover']=='grass':
            p=RES/'GroundGrassV2.png';save(p,srgb(c));outputs.append(validate(p))
        elif source['cover'] in ['moss','lichen']:
            size=1024 if key=='moss' else 256
            h,n=relief(c,size,source['repeat_metres'],strengths[key])
            lum=resize(c@np.array([.2126,.7152,.0722]),size)
            lum=np.clip(.5+(lum/lum.mean()-1)*.26,.12,.88)
            packed=np.stack([lum,n[:,:,0]*.5+.5,n[:,:,1]*.5+.5,np.ones_like(lum)],axis=2)
            p=RES/('GroundMossV1.png' if key=='moss' else 'GroundLichenV1.png');save(p,close_edges(packed));outputs.append(validate(p))
        records.append({**{k:source[k] for k in ['key','layers','cover','repeat_metres','master']},'master_sha256':sha(master),'generated':True,'outputs':outputs})
    if only is not None:
        previous=json.loads((ROOT/'assets/verification/generated-ground-sources-20261107.json').read_text(encoding='utf-8'))['materials']
        updated={row['key']:row for row in previous};updated.update({row['key']:row for row in records})
        records=[updated[row['key']] for row in json.loads(PROMPTS.read_text(encoding='utf-8'))['assets']]
        for row in records:
            assert sha(ROOT/row['master'])==row['master_sha256'],row['key']
            for output in row['outputs']:assert sha(ROOT/output['path'])==output['sha256'],output['path']
    assert len(records)==19 and sorted(layer for r in records for layer in r['layers'])==list(range(17))
    result={'version':'generated-ground-1','generator':'built-in image_gen','materials':records,'normal_mask_note':'Approximate luminance-derived relief, conservative dry roughness and nonmetallic masks. No measured PBR claim.','processing':'Neutral linear RGB means retain existing ecological/geological macro tints; low-frequency light compensation, periodic-plus-smooth boundaries, narrow exact edge closure.','budget':'Existing17 BC7 terrain layers at1024/512/128; grass1024 and packed moss1024 unchanged, packed lichen256 BC7 replaces procedural256 RGBA32. No extra shader read/pass.','full_prompts':PROMPTS.relative_to(ROOT).as_posix()}
    (ROOT/'assets/verification/generated-ground-sources-20261107.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print('PASS19 generated masters,17 layer triples and3 cover textures; exact repeat edges and unit relief normals.')
if __name__=='__main__':
    import argparse
    parser=argparse.ArgumentParser();parser.add_argument('--only',help='Recompile one material while retaining the verified published set.');args=parser.parse_args()
    if args.only and args.only not in [r['key'] for r in json.loads(PROMPTS.read_text(encoding='utf-8'))['assets']]:parser.error('Unknown material')
    build(args.only)
