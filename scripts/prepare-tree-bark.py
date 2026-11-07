"""Compile generated bark into the existing five-layer art bank.

Authoring only: linear-light exposure, periodic boundaries and unit normals.
All layers use luminance-based approximate relief, not measured scan geometry.
"""
from pathlib import Path
import hashlib,json
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'assets-source/vegetation/tree-surfaces'
OUTPUT=ROOT/'unity/WorldGen.Unity/Assets/WorldGen/Editor/TreeSurfaceArtV1'
NAMES=['smooth','fissured','scaly','fibrous','peeling']
MASTERS=[SOURCE/('ai/fissured-bark-20261107/albedo.png' if name=='fissured' else 'ai/bark-set-20261107/'+name+'.png') for name in NAMES]
SIZE=512

def linear(x):return np.where(x<=.04045,x/12.92,((x+.055)/1.055)**2.4)
def srgb(x):return np.where(x<=.0031308,x*12.92,1.055*np.maximum(x,0)**(1/2.4)-.055)
def unit(x):return x/np.maximum(1e-9,np.linalg.norm(x,axis=2,keepdims=True))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def periodic(image):
    # Periodic-plus-smooth decomposition (Moisan2011). Only an offline boundary
    # correction, no extra shader lookups or stochastic runtime texture blending.
    h,w=image.shape[:2];v=np.zeros_like(image)
    v[0]=image[-1]-image[0];v[-1]=-v[0]
    v[:,0]+=image[:,-1]-image[:,0];v[:,-1]-=image[:,-1]-image[:,0]
    denom=2*np.cos(2*np.pi*np.arange(h)/h)[:,None]+2*np.cos(2*np.pi*np.arange(w)/w)[None,:]-4
    denom[0,0]=1
    spectrum=np.fft.fft2(v,axes=(0,1))/denom[:,:,None];spectrum[0,0]=0
    return image-np.fft.ifft2(spectrum,axes=(0,1)).real

def close_edges(a):
    # Small residual correction after decomposition, so the actual repeated
    # first/last samples match exactly without a broad blurred crossfade stripe.
    a=a.copy();band=12
    for axis in [0,1]:
        b=np.swapaxes(a,0,axis);delta=(b[-1]-b[0])*.5
        for j in range(band):
            t=1-j/band;t=t*t*(3-2*t)
            b[j]+=delta*t;b[-1-j]-=delta*t
    return a

def read(p):return np.asarray(Image.open(p).convert('RGB').resize((SIZE,SIZE),Image.Resampling.LANCZOS),dtype=float)/255
def encode(a,p):Image.fromarray(np.uint8(np.clip(a,0,1)*255+.5)).save(p)

def build():
    OUTPUT.mkdir(parents=True,exist_ok=True);records=[]
    for layer,(name,source) in enumerate(zip(NAMES,MASTERS)):
        before=read(source);colour=linear(before)
        luminance=colour@np.array([.2126,.7152,.0722])
        light=gaussian_filter(luminance,72,mode='wrap')
        colour*=np.clip(np.mean(luminance)/np.maximum(.01,light),.75,1.3)[:,:,None]
        target=[.24,.17,.20,.19,.26][layer]
        colour*=min(target/max(.01,float(np.mean(colour@np.array([.2126,.7152,.0722])))),.9/max(.01,float(np.quantile(colour,.9999))))
        colour=close_edges(periodic(colour));colour=np.clip(colour,.001,.98)
        h=np.log(np.maximum(.01,colour@np.array([.2126,.7152,.0722])))
        h=gaussian_filter(h,.7,mode='wrap')-gaussian_filter(h,10,mode='wrap')
        h*=[.0025,.008,.006,.0035,.006][layer]/max(.1,float(np.std(h)))
        dx=(np.roll(h,-1,1)-np.roll(h,1,1))*SIZE*.5
        dy=(np.roll(h,-1,0)-np.roll(h,1,0))*SIZE*.5
        normal=unit(np.stack([-dx,dy,np.ones_like(dx)],axis=2))
        normal=unit(close_edges(normal));c=srgb(colour);n=normal*.5+.5
        paths=[]
        for channel,array in [('albedo',c),('normal',n)]:
            p=OUTPUT/f'bark-{layer:02d}-{channel}.png';encode(array,p);paths.append({'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p)})
        encoded=np.asarray(Image.open(OUTPUT/f'bark-{layer:02d}-albedo.png'))
        if not np.array_equal(encoded[0],encoded[-1]) or not np.array_equal(encoded[:,0],encoded[:,-1]):raise AssertionError('Bark repeat boundary mismatch')
        if not np.isfinite(normal).all() or np.max(np.abs(np.linalg.norm(normal,axis=2)-1))>1e-10:raise AssertionError('Non-unit normal')
        records.append({'layer':layer,'name':name,'source':source.relative_to(ROOT).as_posix(),'source_sha256':sha(source),'generated':True,'mean_linear_rgb':colour.mean(axis=(0,1)).tolist(),'clipped_white_fraction':float(np.mean(encoded>=254)),'outputs':paths})
    result={'version':'tree-bark-3-all-generated','size':SIZE,'layers':5,'records':records,'processing':'Linear-light scalar luminance exposure; original chroma retained; periodic-plus-smooth and a narrow residual boundary correction; approximate luminance-derived relief for all five generated layers. No runtime processing.',
        'research':[{'url':'https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/mesh/recalculatenormals','application':'UV duplicates need matching normals and tangents; recalculating triangle normals alone does not guarantee a smooth wrap.'},{'url':'https://helpx.adobe.com/substance-3d-b2m/desktop/parameters.html','application':'Separate base colour from lighting, make it tile, normalize surface vectors; luminance-derived relief is an approximation.'},{'url':'https://helios2.mi.parisdescartes.fr/~moisan/papers/2009-11r.pdf','application':'Offline periodic-plus-smooth boundary correction; independently implemented from the published equations.'}],
        'generation':{'tool':'built-in image_gen','masters':[p.relative_to(ROOT).as_posix() for p in MASTERS],'prompts':['assets-source/vegetation/tree-surfaces/ai/fissured-bark-20261107/source.json','assets-source/vegetation/tree-surfaces/ai/bark-set-20261107/sources.json']}}
    (ROOT/'assets/verification/tree-bark-sources-20261107.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print('PASS five512px bark pairs; exact repeat edges, unit normals and retained source colours')
    return records

if __name__=='__main__':build()
