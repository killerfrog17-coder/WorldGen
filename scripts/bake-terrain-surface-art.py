"""Offline periodic detail swatches for the existing terrain material budget.

Input art comes from imagegen. Height/normals are conservative luminance-derived
approximations, not inferred physical PBR geometry. No runtime image processing.
"""
import argparse,hashlib,json
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter

def linear(x):return np.where(x<=.04045,x/12.92,((x+.055)/1.055)**2.4)
def srgb(x):return np.where(x<=.0031308,x*12.92,1.055*np.maximum(x,0)**(1/2.4)-.055)
def periodic(image):
    h,w=image.shape;boundary=np.zeros_like(image)
    boundary[0]=image[-1]-image[0];boundary[-1]=-boundary[0]
    delta=image[:,-1]-image[:,0];boundary[:,0]+=delta;boundary[:,-1]-=delta
    denom=2*np.cos(2*np.pi*np.arange(h)/h)[:,None]+2*np.cos(2*np.pi*np.arange(w)/w)[None,:]-4
    denom[0,0]=1;spectrum=np.fft.fft2(boundary)/denom;spectrum[0,0]=0
    return image-np.fft.ifft2(spectrum).real
def resize(x,size):return np.asarray(Image.fromarray(x.astype(np.float32)).resize((size,size),Image.Resampling.LANCZOS),dtype=np.float64)
def save(path,x):Image.fromarray(np.uint8(np.clip(x,0,1)*255+.5)).save(path)
def seam_ratio(x):
    edge=np.mean(abs(x[:,0]-x[:,-1]))+np.mean(abs(x[0]-x[-1]))
    interior=np.mean(abs(np.diff(x,axis=0)))+np.mean(abs(np.diff(x,axis=1)))
    return float(edge/max(interior,1e-8))
ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True);ap.add_argument('--output',type=Path,required=True)
a=ap.parse_args();a.output.mkdir(parents=True,exist_ok=True);reports=[]
for name,relief,rough in [('soil',.0015,.92),('granite',.003,.86)]:
    source=a.root/(name+'-source.png');image=np.asarray(Image.open(source).convert('RGB'),dtype=np.float64)/255
    luminance=linear(image)@np.array([.2126,.7152,.0722]);tile=periodic(resize(luminance,512))
    # Remove broad baked illumination while retaining medium-scale material.
    tile*=np.mean(tile)/np.maximum(gaussian_filter(tile,48,mode='wrap'),.005)
    mean=float(linear(np.array(.5)));tile=np.clip(tile,.005,1)
    tile=np.clip(tile*(mean/tile.mean()),.025,.65);tile*=mean/tile.mean()
    colour=srgb(tile);save(a.output/(name+'-albedo.png'),np.repeat(colour[:,:,None],3,axis=2))
    height=resize(tile,256);height=gaussian_filter(height,.7,mode='wrap')-gaussian_filter(height,14,mode='wrap')
    height*=relief/max(height.std(),.001)
    dx=(np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))/(4/256)
    dy=(np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))/(4/256)
    normal=np.stack([-dx,dy,np.ones_like(dx)],axis=2);normal/=np.linalg.norm(normal,axis=2,keepdims=True)
    save(a.output/(name+'-normal.png'),normal*.5+.5)
    mask=np.zeros((128,128,4));mask[:,:,0]=.98;mask[:,:,1]=rough
    h=resize(height,128);mask[:,:,2]=np.clip(.5+h/(relief*8),0,1)
    save(a.output/(name+'-mask.png'),mask)
    preview=Image.fromarray(np.uint8(np.repeat(colour[:,:,None],3,axis=2)*255+.5));repeat=Image.new('RGB',(1024,1024))
    for x in (0,512):
        for y in (0,512):repeat.paste(preview,(x,y))
    repeat.save(a.root/(name+'-repeat-preview.png'))
    reports.append({'material':name,'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'linear_mean':float(tile.mean()),'target_mean':mean,
                    'albedo_size':512,'normal_size':256,'mask_size':128,'edge_gradient_ratio':seam_ratio(colour),
                    'normal_method':'Conservative periodic luminance relief; no measured/inferred geometry','relief_std_m':relief})
report={'generator':'Built-in imagegen','offline_bake':'Periodic-plus-smooth boundary removal, broad-light correction, neutral linear mean, shallow normal derivation',
        'repeat_metres':2,'replacement_layers':{'soil':[0],'granite':[4,8]},'materials':reports,'runtime_shader_changes':False}
(a.output/'provenance.json').write_text(json.dumps(report,indent=2));(a.root/'bake-report.json').write_text(json.dumps(report,indent=2))
(a.output/'prompts.json').write_bytes((a.root/'prompts.json').read_bytes())
print(json.dumps(report,indent=2))
