"""CPU UV coat bake for the first selected hare/rabbit finishing pass.

Uses shared generated fibre detail with anatomical masks, never a whole-animal
photo projection. Pigment masks are not converted into bump height.
"""
import argparse,hashlib,importlib.util,json,time
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter,map_coordinates,distance_transform_edt

repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--size',type=int,default=1024)
a=ap.parse_args();assert a.slug in {'hare','rabbit'}
root=repo/'artifacts/fauna-painted-20261024'/a.slug;started=time.perf_counter()
state=json.loads((root/'paint-state.json').read_text());land=json.loads((root/'face-landmarks.json').read_text())
d=dict(np.load(root/'paint-anatomy.npz'));d['region_weights']=np.zeros((len(d['vertices']),10),np.float32)
raw=json.loads((repo/'artifacts/fauna-overnight-20261024'/a.slug/'audition.json').read_text())['candidates'][state['source_candidate']-1]
groups=np.asarray(raw['groups']);weights=d['weights']
for channel,kind in [(0,'spine'),(1,'neck'),(2,'neck'),(5,'tail')]:d['region_weights'][:,channel]=weights[:,groups==kind].sum(1)
spec=importlib.util.spec_from_file_location('coat_raster',repo/'scripts/bake-deer-masked-coat.py');raster=importlib.util.module_from_spec(spec);spec.loader.exec_module(raster)
maps,occupied,_=raster.raster(d,a.size);iy,ix=np.where(occupied);p=maps[iy,ix,:3];n=maps[iy,ix,3:6];n/=np.maximum(np.linalg.norm(n,axis=1,keepdims=True),1e-8)
body_weight=np.clip(maps[iy,ix,6],0,1);tail_weight=np.clip(maps[iy,ix,11],0,1)

def smooth(lo,hi,x):
    t=np.clip((x-lo)/(hi-lo),0,1);return t*t*(3-2*t)

fur_path=repo/state['fur_source'];fur=np.asarray(Image.open(fur_path).convert('RGB').resize((768,768)),np.float32)/255
lum=fur@np.array([.2126,.7152,.0722]);detail=lum-gaussian_filter(lum,15,mode='wrap')
detail/=max(float(np.std(detail))*3,1e-4)
def sample(u,v):return map_coordinates(detail,[np.mod(v,1)*767,np.mod(u,1)*767],order=1,mode='wrap')
blend=np.abs(n)**6;blend/=np.maximum(blend.sum(1,keepdims=True),1e-8)
hair=(sample(p[:,1]*2.1,p[:,2]*2.1)*blend[:,0]+sample(p[:,0]*2.1,p[:,2]*2.1)*blend[:,1]+sample(p[:,0]*2.1,p[:,1]*2.1)*blend[:,2])
head=np.array(state['head_seed']);cx=state['center_x'];face=smooth(head[1]-.15,head[1]+.04,p[:,1])*smooth(head[2]-.32,head[2]-.12,p[:,2])
belly=(1-smooth(state['belly_z']+.06,state['belly_z']+.38,p[:,2]))*(.4+.6*smooth(.25,-.65,n[:,2]))*(.28+.72*smooth(.1,.65,body_weight))
chin=face*smooth(head[2]+.04,head[2]-.17,p[:,2])*smooth(.4,-.5,n[:,2])*.85
tail=tail_weight*smooth(.15,-.7,n[:,2]);tail_top=tail_weight*smooth(.15,.7,n[:,2])
inner=np.zeros(len(p));tips=np.zeros(len(p));frames=[]
for side in [-1,1]:
    points=d['vertices'][(d['vertices'][:,2]>state['ear_base']-.035)&(d['vertices'][:,1]<head[1]+.06)&((d['vertices'][:,0]-cx)*side>0)]
    assert len(points)>8,'No independent ear region found'
    root_point=points[points[:,2]<=np.quantile(points[:,2],.12)].mean(0);tip_point=points[points[:,2]>=np.quantile(points[:,2],.97)].mean(0)
    axis=tip_point-root_point;length=np.linalg.norm(axis);axis/=length
    across=np.cross([0,1,0],axis);across/=np.linalg.norm(across)
    rel=p-root_point;frac=rel@axis/length;u=rel@across
    width=max(float(np.quantile(np.abs((points-root_point)@across),.92)),.025)
    side_gate=smooth(-.005,.015,(p[:,0]-cx)*side);spatial=smooth(state['ear_base']-.06,state['ear_base']+.06,p[:,2])*(1-smooth(head[1]+.01,head[1]+.13,p[:,1]))*side_gate
    ear_inner=spatial*smooth(.08,.25,frac)*(1-smooth(.82,.98,frac))*(1-smooth(.48,.83,np.abs(u)/width))*smooth(.12,.65,n[:,1])
    inner=np.maximum(inner,ear_inner);tips=np.maximum(tips,spatial*smooth(.76,.96,frac)*(1-ear_inner*.7))
    frames.append({'side':side,'root':root_point.tolist(),'tip':tip_point.tolist(),'width':width})
nose=np.array(land['nose']['point']);nn=np.array(land['nose']['normal']);nn/=np.linalg.norm(nn)
up=np.array([0.,0.,1.]);across=np.cross(nn,up);across/=np.linalg.norm(across);up=np.cross(across,nn)
delta=p-nose;nu=delta@across;nv=delta@up;nd=delta@nn
width=.035*(.68+.32*np.clip(nv/.025+1,0,1));q=(nu/width)**2+(nv/.025)**2+(nd/.045)**2
nose_mask=(1-smooth(.7,1.2,q))*face*smooth(.05,.65,n@nn)
eye_surround=np.zeros(len(p));bare=np.zeros(len(p))
eye_radius=(.038,.032) if a.slug=='hare' else (.040,.034)
for key in ['right_eye','left_eye']:
    point=np.array(land[key]['point']);normal=np.array(land[key]['normal']);normal/=np.linalg.norm(normal)
    horizontal=np.array([0.,1.,0.]);horizontal-=normal*np.dot(horizontal,normal);horizontal/=np.linalg.norm(horizontal)
    vertical=np.array([0.,0.,1.]);vertical-=normal*np.dot(vertical,normal)+horizontal*np.dot(vertical,horizontal);vertical/=np.linalg.norm(vertical)
    rel=p-point;u=rel@horizontal;v=rel@vertical;depth=rel@normal
    radius=np.sqrt((u/eye_radius[0])**2+(v/eye_radius[1])**2)
    gate=(1-smooth(.025,.065,np.abs(depth)))*smooth(.05,.65,n@normal)
    eye_surround=np.maximum(eye_surround,(1-smooth(1.25,1.85,radius))*gate)
    bare=np.maximum(bare,(1-smooth(.98,1.16,radius))*gate)
fur_weight=np.clip(1-nose_mask-inner*.55-bare,0,1)
rough=np.clip(.84-nose_mask*.40-inner*.10-bare*.30,.3,.88)
height=hair*.00012*fur_weight

profiles=([('European brown',[.40,.30,.18],[.79,.76,.66],True),('European grey-brown',[.32,.30,.25],[.77,.76,.69],True),('Snowshoe summer',[.39,.26,.16],[.78,.75,.66],True),('Snowshoe winter',[.82,.82,.76],[.89,.89,.83],True),('Sandy dryland (hypothetical)',[.57,.46,.30],[.83,.78,.67],True),('Dark wet-cover (hypothetical)',[.20,.18,.14],[.58,.56,.48],True)] if a.slug=='hare' else
          [('Grey-brown agouti',[.34,.29,.22],[.79,.78,.72],False),('Warm brown agouti',[.43,.30,.19],[.81,.77,.67],False),('Dark melanistic',[.10,.085,.07],[.27,.25,.22],False),('Pale sandy (hypothetical)',[.58,.47,.32],[.85,.80,.69],False),('Litter-mottled (hypothetical)',[.31,.25,.17],[.76,.72,.62],False)])
distance,nearest=distance_transform_edt(~occupied,return_indices=True);pad=(~occupied)&(distance<=12)
def extend(array):
    array[pad]=array[nearest[0][pad],nearest[1][pad]];return array
for index,(label,base,pale,dark_tips) in enumerate(profiles):
    base=np.array(base);pale=np.array(pale);colour=np.tile(base,(len(p),1))
    def paint(mask,target):return colour*(1-mask[:,None])+np.asarray(target)*mask[:,None]
    colour=paint(belly,pale);colour=paint(chin,pale);colour=paint(tail,pale)
    if a.slug=='hare':colour=paint(tail_top*.9,[.055,.05,.045])
    colour=paint(eye_surround*.5,base*.55+pale*.45)
    colour*=np.clip(1+hair[:,None]*.32,.64,1.34)
    colour=paint(inner,[.47,.35,.31])
    if dark_tips:colour=paint(tips*.94,[.045,.042,.037])
    colour=paint(bare,[.075,.058,.045]);colour=paint(nose_mask,[.12,.09,.08] if a.slug=='hare' else [.23,.15,.14])
    if 'mottled' in label:
        m=(.5+.5*np.sin(p[:,1]*21+np.sin(p[:,2]*15)*2))*smooth(.1,.7,n[:,2])*.2
        colour=paint(m,[.13,.105,.075])
    image=np.zeros((a.size,a.size,3),np.float32);image[iy,ix]=np.clip(colour,0,1);extend(image)
    Image.fromarray(np.uint8(np.clip(image,0,1)*255)).save(root/f'coat-{index}-colour.png')
rough_image=np.ones((a.size,a.size),np.float32)*.84;rough_image[iy,ix]=rough;extend(rough_image)
Image.fromarray(np.uint8(rough_image*255)).save(root/'coat-roughness.png')
height_image=np.zeros((a.size,a.size),np.float32);height_image[iy,ix]=height;extend(height_image)
dy,dx=np.gradient(gaussian_filter(height_image,.55));physical_pixel=2.1/a.size
normal=np.dstack([-dx/physical_pixel,dy/physical_pixel,np.ones_like(dx)]);normal/=np.linalg.norm(normal,axis=2,keepdims=True)
Image.fromarray(np.uint8(np.clip(normal*.5+.5,0,1)*255)).resize((512,512),Image.Resampling.LANCZOS).save(root/'coat-normal.png')
summary={'method':'CPU rest-surface UV raster, shared generated fibre detail, separate anatomical pigment/roughness masks; no whole-animal photo projection','size':a.size,'normal_size':512,'coats':[{'label':x[0],'file':f'coat-{i}-colour.png'} for i,x in enumerate(profiles)],'uv_coverage_fraction':float(occupied.mean()),'mask_pixels':{key:int((value>.5).sum()) for key,value in [('belly',belly),('inner_ear',inner),('ear_tips',tips),('nose',nose_mask),('bare_lids',bare)]},'ear_frames':frames,'eye_radius':eye_radius,'fur_sha256':hashlib.sha256(fur_path.read_bytes()).hexdigest(),'seconds':round(time.perf_counter()-started,2),'limits':'Normal relief is restrained artistic fibre detail, not measured PBR. Face geometry and owner appearance review remain pending.'}
(root/'coat-bake.json').write_text(json.dumps(summary,indent=2)+'\n')
print('COAT ATLAS BAKED',a.slug,len(profiles),'coats',a.size,'pixels',summary['seconds'],'seconds',summary['mask_pixels'],flush=True)
