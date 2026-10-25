"""CPU scale/fin atlases on the inspected owner-selected freshwater fish.

Colour and shallow scale/ray relief only. No geometry, rig or motion edits;
fin colour is opaque in this initial inexpensive preview.
"""
import argparse,hashlib,importlib.util,json,sys,time
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter,map_coordinates,distance_transform_edt

repo=Path(__file__).resolve().parent.parent;sys.path.insert(0,str(repo/'scripts'))
from fauna_paint_recipes import profiles,eye_frame,eye_radius
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--size',type=int,default=1024)
a=ap.parse_args();assert a.slug=='small-native-freshwater-fish-guild'
root=repo/'artifacts/fauna-painted-20261024'/a.slug;started=time.perf_counter()
state=json.loads((root/'paint-state.json').read_text());assert not state['appearance_approved']
land=json.loads((root/'face-landmarks.json').read_text());d=dict(np.load(root/'paint-anatomy.npz'))
raw=json.loads((repo/'artifacts/fauna-overnight-20261024'/a.slug/'audition.json').read_text())['candidates'][state['source_candidate']-1]
groups=np.array(raw['groups']);d['region_weights']=np.zeros((len(d['vertices']),10),np.float32)
for channel,kind in [(0,'fin'),(1,'extra'),(2,'tail')]:d['region_weights'][:,channel]=d['weights'][:,groups==kind].sum(1)
spec=importlib.util.spec_from_file_location('coat_raster',repo/'scripts/bake-deer-masked-coat.py');raster=importlib.util.module_from_spec(spec);spec.loader.exec_module(raster)
maps,occupied,_=raster.raster(d,a.size);iy,ix=np.where(occupied);p=maps[iy,ix,:3];n=maps[iy,ix,3:6];n/=np.maximum(np.linalg.norm(n,axis=1,keepdims=True),1e-8)

def smooth(lo,hi,x):
    t=np.clip((x-lo)/(hi-lo),0,1);return t*t*(3-2*t)

path=repo/state['fur_source'];source=np.asarray(Image.open(path).convert('RGB').resize((768,768)),np.float32)/255
lum=source@np.array([.2126,.7152,.0722]);detail=lum-gaussian_filter(lum,16,mode='wrap');detail/=max(float(np.std(detail))*3,1e-6)
def sample(u,v):return map_coordinates(detail,[np.mod(v,1)*767,np.mod(u,1)*767],order=1,mode='wrap')
blend=np.abs(n)**6;blend/=np.maximum(blend.sum(1,keepdims=True),1e-8)
scales=sample(p[:,2]*2.7,-p[:,1]*2.1)*blend[:,0]+sample(p[:,0]*2.7,-p[:,2]*2.1)*blend[:,1]+sample(p[:,0]*2.7,-p[:,1]*2.1)*blend[:,2]
cx=state['center_x'];head=smooth(1.28,1.73,p[:,1]);dorsal=smooth(.80,1.24,p[:,2]);belly=1-smooth(.39,.74,p[:,2])
inferred=smooth(.40,.75,maps[iy,ix,6]+maps[iy,ix,7])
edge=np.maximum.reduce([1-smooth(.23,.42,p[:,2]),smooth(.27,.37,np.abs(p[:,0]-cx)),smooth(1.25,1.42,p[:,2])])
tail_fin=smooth(-1.38,-1.72,p[:,1]);top_fin=smooth(1.26,1.43,p[:,2])*(1-smooth(.08,.14,np.abs(p[:,0]-cx)))
fin=np.maximum.reduce([inferred*edge,tail_fin,top_fin])
tail_angle=np.arctan2(p[:,2]-.77,np.maximum(-p[:,1]-1.22,.02))
top_angle=np.arctan2(p[:,1]+.25,np.maximum(p[:,2]-1.15,.02))
side_angle=np.arctan2(np.abs(p[:,0]-cx),p[:,1]-.65)
phase=side_angle*22*(1-tail_fin-top_fin)+tail_angle*27*tail_fin+top_angle*19*top_fin
rays=(.5+.5*np.cos(phase))**12
gill_y=1.39+.11*((p[:,2]-.79)/.44)**2
gill=np.exp(-((p[:,1]-gill_y)/.026)**2)*smooth(.30,.45,p[:,2])*(1-smooth(1.16,1.29,p[:,2]))*smooth(.20,.65,np.abs(n[:,0]))*(1-fin)
nose=np.array(land['nose']['point']);mouth=smooth(2.08,2.20,p[:,1])*(1-smooth(.035,.075,np.abs(p[:,2]-nose[2])))*(1-fin)
from fauna_eye_profiles import dimensions
eye_rim=np.zeros(len(p));bare=np.zeros(len(p));rx,rz=dimensions(land,eye_radius(a.slug))
for key in ['right_eye','left_eye']:
    rx,rz=dimensions(land,eye_radius(a.slug),key.split('_')[0])
    normal,horizontal,vertical=eye_frame(a.slug,land,key.split('_')[0]);delta=p-np.array(land[key]['point'])
    q=np.sqrt(((delta@horizontal)/rx)**2+((delta@vertical)/rz)**2)
    gate=(1-smooth(.025,.065,np.abs(delta@normal)))*smooth(.04,.65,n@normal)
    eye_rim=np.maximum(eye_rim,(1-smooth(1.15,1.70,q))*gate)
    bare=np.maximum(bare,(1-smooth(.70,.90,q))*gate)
height=scales*.000065*(1-head*.8)*(1-fin)*(1-bare)+rays*.00004*fin
rough=np.clip(.30+fin*.12+head*.025+gill*.035-bare*.09,.20,.48)
distance,nearest=distance_transform_edt(~occupied,return_indices=True);pad=(~occupied)&(distance<=10)
def extend(image):image[pad]=image[nearest[0][pad],nearest[1][pad]];return image
coats=[]
for index,(label,base,pale,_) in enumerate(profiles(a.slug)):
    base=np.array(base);pale=np.array(pale);colour=np.tile(base,(len(p),1))
    def paint(mask,target):return colour*(1-mask[:,None])+np.asarray(target)*mask[:,None]
    colour=paint(dorsal,[base[0]*.48,base[1]*.56,base[2]*.47]);colour=paint(belly,pale)
    flank=(1-dorsal)*(1-belly)*(1-fin)*(1-head*.7)
    colour=paint(flank*.45,np.minimum(pale*.95,1))
    colour*=np.clip(1+scales[:,None]*.14,.83,1.17)
    if index==1:
        stripe=(1-smooth(.035,.072,np.abs(p[:,2]-(.80+.025*np.sin(p[:,1]*1.8)))))*(1-fin)*(1-head*.7)
        colour=paint(stripe*.82,base*.28)
    if index==2:
        m=smooth(.32,.75,.5+.25*np.sin(p[:,1]*17+np.sin(p[:,2]*23))+.20*np.sin(p[:,2]*13+p[:,1]*7))*(1-fin)*(1-belly*.6)
        colour=paint(m*.55,base*.34)
    if index==3:colour=paint(head*.85,[.095,.105,.070])
    fin_colour=base*.68+np.array([.085,.085,.055])
    colour=paint(fin,fin_colour[None,:]*(.87+.13*rays[:,None]))
    colour=paint(gill*.48,base*.48)
    colour=paint(mouth*.40,base*.40)
    colour=paint(eye_rim*.45,pale*.65);colour=paint(bare,[.16,.15,.095])
    image=np.zeros((a.size,a.size,3),np.float32);image[iy,ix]=np.clip(colour,0,1);extend(image)
    filename=f'coat-{index}-colour.png';Image.fromarray(np.uint8(np.clip(image,0,1)*255)).save(root/filename);coats.append({'label':label,'file':filename})
image=np.ones((a.size,a.size),np.float32)*.3;image[iy,ix]=rough;extend(image);Image.fromarray(np.uint8(image*255)).save(root/'coat-roughness.png')
image=np.zeros((a.size,a.size),np.float32);image[iy,ix]=height;extend(image);dy,dx=np.gradient(gaussian_filter(image,.55));pixel=4.7/a.size
normal=np.dstack([-dx/pixel,dy/pixel,np.ones_like(dx)]);normal/=np.linalg.norm(normal,axis=2,keepdims=True)
Image.fromarray(np.uint8(np.clip(normal*.5+.5,0,1)*255)).resize((512,512),Image.Resampling.LANCZOS).save(root/'coat-normal.png')
report={'method':'CPU continuous rest-surface scale mapping, anatomical countershading, gill/lip and fin masks; deterministic fan-ray pigment. No whole-fish photo projection or emissive silver.','size':a.size,'normal_size':512,'coats':coats,'eye_radius':[rx,rz],'uv_coverage_fraction':float(occupied.mean()),'required_masks':['mouth','gill','fin','eye_rim'],'mask_pixels':{key:int((value>.5).sum()) for key,value in [('mouth',mouth),('gill',gill),('fin',fin),('eye_rim',eye_rim)]},'scale_source':str(path.relative_to(repo)),'scale_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'fin_rays':'Project-authored bounded fan-angle cosine pigment and shallow relief baked into the same opaque atlas','seconds':round(time.perf_counter()-started,2),'limits':'Selected generic fish silhouette only, no exact taxon or new fin topology. Opaque low-cost fin approximation; no true membrane transmission, age/body morphs or school AI. Fixed ocular rims, no mobile fish eyelids. Original rig/motions and owner approval remain separate.'}
(root/'coat-bake.json').write_text(json.dumps(report,indent=2)+'\n');print('FISH COAT ATLAS BAKED',len(coats),a.size,report['seconds'],report['mask_pixels'],flush=True)
