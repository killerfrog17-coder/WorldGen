"""CPU UV coat bake for explicitly supported selected mammal bodies.

Uses shared generated fibre detail with anatomical masks, never a whole-animal
photo projection. Pigment masks are not converted into bump height.
"""
import argparse,hashlib,importlib.util,json,time,sys
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter,map_coordinates,distance_transform_edt

repo=Path(__file__).resolve().parent.parent
sys.path.insert(0,str(repo/'scripts'))
from fauna_paint_recipes import SUPPORTED, family, profiles as coat_profiles, eye_radius as get_eye_radius, eye_frame
from fauna_hypothetical_coats import treatment
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--size',type=int,default=1024)
a=ap.parse_args();assert a.slug in SUPPORTED
if family(a.slug) in {'avian','fish'}:
    import runpy
    script='bake-selected-fauna-fish.py' if family(a.slug)=='fish' else 'bake-selected-fauna-bird.py'
    runpy.run_path(str(repo/'scripts'/script),run_name='__main__')
    raise SystemExit
kind=family(a.slug);canid=kind=='canid';compact=kind!='lagomorph'
root=repo/'artifacts/fauna-painted-20261024'/a.slug;started=time.perf_counter()
state=json.loads((root/'paint-state.json').read_text());land=json.loads((root/'face-landmarks.json').read_text())
d=dict(np.load(root/'paint-anatomy.npz'));d['region_weights']=np.zeros((len(d['vertices']),10),np.float32)
raw=json.loads((root/'selected-source-entry.json').read_text()) if (root/'selected-source-entry.json').exists() else json.loads((repo/'artifacts/fauna-overnight-20261024'/a.slug/'audition.json').read_text())['candidates'][state['source_candidate']-1]
groups=np.asarray(raw['groups']);weights=d['weights']
for channel,group_kind in [(0,'spine'),(1,'neck'),(2,'neck'),(5,'tail')]:d['region_weights'][:,channel]=weights[:,groups==group_kind].sum(1)
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
extra_fur_sources=[];shag=np.zeros(len(p))
if a.slug=='squirrel':
    # Follow the selected generated tail chain instead of using world-up hair.
    tail_path=repo/'artifacts/fauna-assets/textures/fur-coarse-guard-v1.png'
    tuft=np.asarray(Image.open(tail_path).convert('RGB').resize((768,768)),np.float32)/255
    tl=tuft@np.array([.2126,.7152,.0722]);td=tl-gaussian_filter(tl,15,mode='wrap');td/=max(float(np.std(td))*3,1e-5)
    nearest=np.full(len(p),np.inf);arc=np.zeros(len(p));theta=np.zeros(len(p));length=0.;segments=0
    heads=d['joint_heads'];parents=d['parents']
    for i in range(len(groups)):
        parent=int(parents[i])
        if groups[i]!='tail' or parent<0 or groups[parent]!='tail':continue
        start=heads[parent];delta=heads[i]-start;seglen=np.linalg.norm(delta)
        if seglen<1e-5:continue
        t=np.clip(((p-start)@delta)/(seglen*seglen),0,1);q=start+t[:,None]*delta;rel=p-q;dist=np.sum(rel*rel,axis=1);mask=dist<nearest
        tangent=delta/seglen;across=np.array([1.,0,0]);across-=tangent*(across@tangent);across/=np.linalg.norm(across);up=np.cross(tangent,across)
        nearest[mask]=dist[mask];arc[mask]=length+t[mask]*seglen;theta[mask]=np.arctan2(rel[mask]@up,rel[mask]@across)
        length+=seglen;segments+=1
    assert segments>=3,'No usable selected tail chain'
    tail_hair=map_coordinates(td,[np.mod(arc*2.4,1)*767,np.mod(theta/(2*np.pi)*2,1)*767],order=1,mode='wrap')
    weight=smooth(.25,.75,tail_weight);hair=hair*(1-weight)+tail_hair*weight
    extra_fur_sources.append({'path':str(tail_path.relative_to(repo)),'sha256':hashlib.sha256(tail_path.read_bytes()).hexdigest(),'region':'Tail fibres aligned by nearest selected tail-chain segment','segments':segments})
if a.slug=='bison':
    # Forequarter shag and shorter hindquarter fibres share continuous surface
    # coordinates. The blending boundary never follows a UV island or neck seam.
    shag=smooth(-.28,.20,p[:,1])*smooth(.38,.72,p[:,2])
    short_path=repo/'artifacts/fauna-assets/deer/fur-source.png'
    short=np.asarray(Image.open(short_path).convert('RGB').resize((768,768)),np.float32)/255
    short_lum=short@np.array([.2126,.7152,.0722]);short_detail=short_lum-gaussian_filter(short_lum,15,mode='wrap')
    short_detail/=max(float(np.std(short_detail))*3,1e-4)
    def sample_short(u,v):return map_coordinates(short_detail,[np.mod(v,1)*767,np.mod(u,1)*767],order=1,mode='wrap')
    short_hair=(sample_short(p[:,1]*2.8,p[:,2]*2.8)*blend[:,0]+sample_short(p[:,0]*2.8,p[:,2]*2.8)*blend[:,1]+sample_short(p[:,0]*2.8,p[:,1]*2.8)*blend[:,2])
    hair=hair*shag+short_hair*(1-shag)
    extra_fur_sources.append({'path':str(short_path.relative_to(repo)),'sha256':hashlib.sha256(short_path.read_bytes()).hexdigest(),'region':'Short hindquarter coat; smooth forequarter transition'})
head=np.array(state['head_seed']);cx=state['center_x'];face=smooth(head[1]-.15,head[1]+.04,p[:,1])*smooth(head[2]-.32,head[2]-.12,p[:,2])
belly=(1-smooth(state['belly_z']+.06,state['belly_z']+.38,p[:,2]))*(.4+.6*smooth(.25,-.65,n[:,2]))*(.28+.72*smooth(.1,.65,body_weight))
chin=face*smooth(head[2]+.04,head[2]-.17,p[:,2])*smooth(.4,-.5,n[:,2])*.85
tail=tail_weight*smooth(.15,-.7,n[:,2]);tail_top=tail_weight*smooth(.15,.7,n[:,2])
inner=np.zeros(len(p));tips=np.zeros(len(p));frames=[]
head_side=np.array(land['side_axis']);head_front=np.array(land['front_axis'])
for side in [-1,1]:
    split=(d['vertices']-head)@head_side if compact else d['vertices'][:,0]-cx
    ear_gate=(d['vertices'][:,2]>state['ear_base']-.035)&(d['vertices'][:,1]<head[1]+.06)&(split*side>0)
    if kind in {'small-carnivore','beaver'}:ear_gate &= (d['vertices'][:,1]>head[1]-.27)&(d['vertices'][:,1]<head[1]+.12)
    if kind in {'suine','caprine','equine'}:ear_gate &= d['vertices'][:,1]>head[1]-.24
    if kind=='ursid':ear_gate=(d['vertices'][:,2]>state['ear_base']-.035)&(d['vertices'][:,1]>head[1]-.10)&(d['vertices'][:,1]<head[1]+.27)&(split*side>.12)
    if a.slug=='wild-cattle':ear_gate=(d['vertices'][:,2]>1.26)&(d['vertices'][:,2]<1.48)&(d['vertices'][:,1]>.78)&(d['vertices'][:,1]<1.12)&(split*side>.12)
    if a.slug=='bison':ear_gate=(d['vertices'][:,2]>1.26)&(d['vertices'][:,2]<1.47)&(d['vertices'][:,1]>.55)&(d['vertices'][:,1]<.90)&(split*side>.10)
    if kind=='rodent':
        y0,y1,z0,lateral=(.72,1.24,1.30,.14) if a.slug=='mouse' else (1.10,1.65,1.36,.07)
        ear_gate=(d['vertices'][:,2]>z0)&(d['vertices'][:,1]>y0)&(d['vertices'][:,1]<y1)&(split*side>lateral)
    points=d['vertices'][ear_gate]
    assert len(points)>8,'No independent ear region found'
    root_point=points[points[:,2]<=np.quantile(points[:,2],.12)].mean(0);tip_point=points[points[:,2]>=np.quantile(points[:,2],.97)].mean(0)
    axis=tip_point-root_point;length=np.linalg.norm(axis);axis/=length
    across=np.cross([0,1,0],axis);across/=np.linalg.norm(across)
    rel=p-root_point;frac=rel@axis/length;u=rel@across
    width=max(float(np.quantile(np.abs((points-root_point)@across),.92)),.025)
    side_gate=smooth(-.005,.015,((p-head)@head_side if compact else p[:,0]-cx)*side);spatial=smooth(state['ear_base']-.06,state['ear_base']+.06,p[:,2])*(1-smooth(head[1]+.01,head[1]+.13,p[:,1]))*side_gate
    if kind in {'small-carnivore','beaver'}:spatial*=smooth(head[1]-.28,head[1]-.20,p[:,1])
    if kind in {'suine','caprine','equine'}:spatial*=smooth(head[1]-.24,head[1]-.17,p[:,1])
    if kind=='ursid':spatial=smooth(state['ear_base']-.06,state['ear_base']+.06,p[:,2])*smooth(head[1]-.10,head[1]-.04,p[:,1])*(1-smooth(head[1]+.20,head[1]+.30,p[:,1]))*smooth(.10,.15,((p-head)@head_side)*side)
    if a.slug=='wild-cattle':spatial=smooth(1.24,1.29,p[:,2])*(1-smooth(1.44,1.49,p[:,2]))*smooth(.76,.83,p[:,1])*(1-smooth(1.08,1.14,p[:,1]))*smooth(.10,.15,((p-head)@head_side)*side)
    if a.slug=='bison':spatial=smooth(1.24,1.29,p[:,2])*(1-smooth(1.43,1.49,p[:,2]))*smooth(.53,.60,p[:,1])*(1-smooth(.87,.94,p[:,1]))*smooth(.10,.15,((p-head)@head_side)*side)
    if kind=='rodent':spatial=smooth(z0-.025,z0+.02,p[:,2])*smooth(y0-.025,y0+.025,p[:,1])*(1-smooth(y1-.02,y1+.04,p[:,1]))*smooth(lateral-.02,lateral+.02,((p-head)@head_side)*side)
    ear_inner=spatial*smooth(.08,.25,frac)*(1-smooth(.82,.98,frac))*(1-smooth(.48,.83,np.abs(u)/width))*smooth(.12,.65,n@head_front if compact else n[:,1])
    if a.slug=='wild-cattle':ear_inner=spatial*smooth(.08,.25,frac)*(1-smooth(.82,.98,frac))*(1-smooth(.48,.83,np.abs(u)/width))*smooth(.08,.60,n@head_side*side)
    inner=np.maximum(inner,ear_inner);tips=np.maximum(tips,spatial*smooth(.76,.96,frac)*(1-ear_inner*.7))
    frames.append({'side':side,'root':root_point.tolist(),'tip':tip_point.tolist(),'width':width})
nose=np.array(land['nose']['point']);nn=np.array(land['nose']['normal']);nn/=np.linalg.norm(nn)
up=np.array([0.,0.,1.]);across=np.cross(nn,up);across/=np.linalg.norm(across);up=np.cross(across,nn)
delta=p-nose;nu=delta@across;nv=delta@up;nd=delta@nn
nw,nh=(.085,.053) if kind=='bovine' else (.075,.055) if kind in {'suine','ursid'} else (.040,.028) if kind=='caprine' else (.049,.033) if canid else (.035,.025)
if kind=='rodent':nw,nh=(.041,.030) if a.slug=='mouse' else (.042,.030)
width=nw*(.68+.32*np.clip(nv/nh+1,0,1));q=(nu/width)**2+(nv/nh)**2+(nd/(.060 if compact else .045))**2
nose_mask=(1-smooth(.7,1.2,q))*face*smooth(.05,.65,n@nn)
if kind in {'suine','ursid','bovine','rodent'}:nose_mask=(1-smooth(.7,1.2,q))*smooth(.0,.60,n@nn)
eye_surround=np.zeros(len(p));bare=np.zeros(len(p))
eye_radius=get_eye_radius(a.slug)
for key in ['right_eye','left_eye']:
    point=np.array(land[key]['point']);normal,horizontal,vertical=eye_frame(a.slug,land,key.split('_')[0])
    rel=p-point;u=rel@horizontal;v=rel@vertical;depth=rel@normal
    radius=np.sqrt((u/eye_radius[0])**2+(v/eye_radius[1])**2)
    gate=(1-smooth(.025,.065,np.abs(depth)))*smooth(.05,.65,n@normal)
    eye_surround=np.maximum(eye_surround,(1-smooth(1.25,1.85,radius))*gate)
    bare=np.maximum(bare,(1-smooth(.70 if compact else .98,.90 if compact else 1.16,radius))*gate)
fur_weight=np.clip(1-nose_mask-inner*(.25 if kind in {'ursid','bovine'} else .55)-bare,0,1)
hoof=np.zeros(len(p))
if kind in {'suine','caprine','bovine','equine','cervid'}:hoof=1-smooth(.10 if kind in {'caprine','bovine'} else .15,.19 if kind in {'caprine','bovine'} else .25,p[:,2])
fur_weight*=1-hoof
pads=(1-smooth(.045,.12,p[:,2]))*smooth(-.05,-.65,n[:,2]) if kind=='ursid' else np.zeros(len(p))
rough=np.clip(.84-nose_mask*(.58 if canid else .52 if kind=='ursid' else .40)-inner*.10-bare*.30-hoof*.30-pads*.15,.24,.88)
height=hair*((.00010+.00025*shag) if kind=='bovine' else .00030 if kind=='ursid' else .00012)*fur_weight

profiles=coat_profiles(a.slug)
if kind in {'ursid','bovine','equine'}:belly*=0;chin*=0;tail*=0
if canid:
    tail_weight=smooth(-.48,-.67,p[:,1])
    tail=np.zeros(len(p));tail_top=np.zeros(len(p))
    throat=smooth(.25,.70,p[:,1])*(1-smooth(1.10,1.30,p[:,2]))*smooth(.0,.65,n[:,1])
    belly=np.maximum(belly,throat*.88)
    eye_z=(land['right_eye']['point'][2]+land['left_eye']['point'][2])/2
    cheek=face*(1-smooth(eye_z-.09,eye_z-.015,p[:,2]))
    stockings=1-smooth(.35,.55,p[:,2]);tail_tip=tail_weight*smooth(-.80,-.94,p[:,1])
    ear_outer=np.clip(smooth(state['ear_base']-.03,state['ear_base']+.08,p[:,2])*(1-smooth(head[1]+.01,head[1]+.12,p[:,1]))-inner,0,1)
    back=smooth(.05,.70,n[:,2])*smooth(.88,1.13,p[:,2])*(1-smooth(head[1]-.2,head[1]+.02,p[:,1]))
    forehead=face*smooth(eye_z-.01,eye_z+.13,p[:,2])*(1-smooth(head[1]+.09,head[1]+.20,p[:,1]))
distance,nearest=distance_transform_edt(~occupied,return_indices=True);pad=(~occupied)&(distance<=12)
def extend(array):
    array[pad]=array[nearest[0][pad],nearest[1][pad]];return array
for index,(label,base,pale,dark_tips) in enumerate(profiles):
    base=np.array(base);pale=np.array(pale);colour=np.tile(base,(len(p),1))
    def paint(mask,target):return colour*(1-mask[:,None])+np.asarray(target)*mask[:,None]
    colour=paint(belly,pale);colour=paint(chin,pale);colour=paint(tail,pale)
    if kind=='suine':
        # Adult underside stays subtly pigmented, never a domestic pink patch.
        colour=paint(tail_weight*.7,base*.70)
        guard=smooth(.25,.85,hair)*smooth(.65,1.0,p[:,2])*.24
        colour=paint(guard,np.minimum(base*1.65+.045,.65))
        if 'Juvenile' in label:
            region=smooth(-1.0,-.76,p[:,1])*(1-smooth(.30,.55,p[:,1]))*smooth(.50,.72,p[:,2])*(1-smooth(1.38,1.52,p[:,2]))*smooth(.25,.65,np.abs(n[:,0]))
            stripe=smooth(.20,.72,np.sin((p[:,2]-.68)*42+np.sin(p[:,1]*3)*.45))*region
            colour=paint(stripe,[.72,.59,.36])
    elif kind=='cervid':
        legs=(1-smooth(.32,.78,p[:,2]))*(1-hoof)
        throat=face*(1-smooth(head[2]-.12,head[2]+.10,p[:,2]))
        colour=paint(legs*.75,pale);colour=paint(throat*.65,pale)
        colour=paint(tail_weight*.65,pale)
    elif kind=='equine':
        # Continuous body-space coat with separate mane/tail and hoof pigment.
        dark=np.array([.055,.040,.027])
        mane=np.exp(-((p[:,0]-cx)/.065)**2)*smooth(.5,.85,p[:,1])*smooth(head[2]-.35,head[2]+.05,p[:,2])
        points=(1-smooth(.30,.67,p[:,2]))*(1-hoof)
        if label in {'Bay','Dark bay','Dun'}:
            colour=paint(points*.90,dark);colour=paint(np.maximum(mane,tail_weight)*.95,dark)
        elif label=='Chestnut':colour=paint(np.maximum(mane,tail_weight)*.70,[.34,.13,.045])
        if label=='Dun':
            dorsal=np.exp(-((p[:,0]-cx)/.032)**2)*smooth(.35,.8,n[:,2])
            colour=paint(dorsal*.8,dark)
    elif kind=='caprine':
        legs=(1-smooth(.30,.65,p[:,2]))*(1-hoof)
        colour=paint(legs*.90,pale)
        flank=np.sqrt(((p[:,1]+.22)/.37)**2+((p[:,2]-1.05)/.20)**2)
        saddle=(1-smooth(.65,1.1,flank))*smooth(.20,.65,np.abs(n[:,0]))
        strength=1 if 'ram' in label else .20 if 'hypothetical' in label else 0
        colour=paint(saddle*strength,pale)
        dorsal=np.exp(-((p[:,0]-cx)/.075)**2)*smooth(.3,.75,n[:,2])*(1-smooth(.3,.5,p[:,1]))
        colour=paint(dorsal*.65,base*.35)
        if 'ram' in label:
            neck=smooth(.0,.35,p[:,1])*(1-smooth(head[1]-.05,head[1]+.10,p[:,1]))*.65
            colour=paint(neck,base*.55)
        if a.slug=='wild-goat':
            colour=paint(legs*.78,base*.36)
            male='Mature male' in label
            shoulder=np.exp(-((p[:,1]-.20)/.13)**2)*smooth(.65,.92,p[:,2])*(1-smooth(1.25,1.42,p[:,2]))
            colour=paint(shoulder*(.82 if male else .24),base*.20)
            neck=smooth(.10,.37,p[:,1])*(1-smooth(head[1]-.05,head[1]+.10,p[:,1]))
            colour=paint(neck*(.70 if male else .15),base*.30)
    elif kind=='ursid':
        eye_z=(land['right_eye']['point'][2]+land['left_eye']['point'][2])/2
        muzzle=smooth(-.32,-.12,(p-nose)@head_front)*(1-smooth(eye_z-.06,eye_z+.03,p[:,2]))
        colour=paint(muzzle*.88,pale)
        guards=smooth(.15,.75,hair)*.22*(1-muzzle)*(1-nose_mask)
        colour=paint(guards,np.minimum(base*2+.035,.85))
        if 'grizzle' in label:colour=paint(smooth(.30,.85,hair)*.48,[.68,.64,.53])
        colour=paint(pads,[.09,.072,.055])
    elif kind=='bovine':
        colour=paint(shag*.70,base*.66)
        legs=(1-smooth(.27,.50,p[:,2]))*(1-hoof)
        colour=paint(legs*.25,base*.60)
        colour=paint(tail_weight*.65,base*.45)
        guards=smooth(.15,.75,hair)*shag*(.60 if 'Light-tipped' in label else .18)
        colour=paint(guards,pale)
        if 'shedding' in label:
            # Colour-only seasonal overlay, not a separate inherited species.
            patches=smooth(.0,.60,np.sin(p[:,1]*19+np.sin(p[:,0]*23)*1.6+np.sin(p[:,2]*15)))*smooth(-.65,-.10,p[:,1])*(1-smooth(.28,.65,p[:,1]))*smooth(.50,.80,p[:,2])
            colour=paint(patches*.40,pale)
    elif kind in {'small-carnivore','beaver'}:
        eye_mid=(np.array(land['right_eye']['point'])+np.array(land['left_eye']['point']))/2
        ahead=(p-eye_mid)@head_front;sidepos=(p-eye_mid)@head_side
        muzzle=smooth(-.05,.14,ahead)*(1-smooth(eye_mid[2]-.03,eye_mid[2]+.06,p[:,2]))
        paws=1-smooth(.07,.16,p[:,2]);colour=paint(paws*.75,base*.45)
        if a.slug=='raccoon':
            facegate=smooth(head[1]-.05,head[1]+.10,p[:,1])*smooth(eye_mid[2]-.20,eye_mid[2]-.08,p[:,2])
            colour=paint(facegate*.75,pale)
            mask=np.exp(-((p[:,2]-eye_mid[2])/.047)**2)*smooth(-.14,-.03,ahead)*(1-smooth(.11,.20,ahead))
            colour=paint(mask*.95,[.045,.038,.030])
            rings=smooth(.15,.65,np.sin(p[:,1]*31))*tail_weight
            colour=paint(tail_weight*.85,pale*.70);colour=paint(rings*.90,base*.25)
        elif a.slug=='badger':
            facegate=smooth(head[1]-.18,head[1]+.04,p[:,1])*smooth(eye_mid[2]-.23,eye_mid[2]-.10,p[:,2])
            colour=paint(facegate*.95,pale)
            stripe=np.exp(-((np.abs(sidepos)-.13)/.065)**2)*facegate
            colour=paint(stripe*.98,[.035,.030,.025]);colour=paint(paws*.95,[.055,.047,.037])
        elif a.slug=='lynx':
            u=p[:,1]*15;v=p[:,2]*17;cells=np.floor(u)*173+np.floor(v)*317
            jitter=(np.sin(cells)*43758.5453)%1;second=(np.sin(cells+91)*22573.173)%1
            du=(u%1)-.15-.70*jitter;dv=(v%1)-.15-.70*second
            radius=.08+.12*((jitter*13)%1)
            spots=(1-smooth(radius,radius+.07,np.sqrt(du*du+dv*dv)))*(jitter>.34)*(1-face)*smooth(.18,.4,np.abs(n[:,0]))
            colour=paint(spots*(.60 if 'Strong' in label else .28),base*.30)
            colour=paint(muzzle*.8,pale);colour=paint(tail_weight*smooth(-.55,-.85,p[:,1]),[.035,.030,.025])
            colour=paint(tips*.8,[.040,.033,.025])
        elif a.slug=='otter':
            throat=face*(1-smooth(eye_mid[2]-.12,eye_mid[2]+.02,p[:,2]))
            colour=paint(np.maximum(muzzle,throat)*(.95 if 'Broad' in label else .72),pale)
        else:
            tail_scales=(.5+.5*np.cos(p[:,1]*110)*np.cos(p[:,0]*110))*.12
            colour=paint(tail_weight*.96,np.array([.13,.105,.075])[None,:]*(.88+tail_scales[:,None]))
    elif kind=='rodent':
        paws=(1-smooth(.085,.16,p[:,2]))*(1-tail_weight)
        if a.slug=='mouse':
            # Fine-coated mouse tails remain two-toned; no universal bare-rat
            # tail or arbitrary sex-linked colour swap.
            colour=paint(tail_weight*.55,base*.80)
            colour=paint(tail,pale)
            foot_colour=pale*.86+np.array([.045,.015,.005])
            colour=paint(paws*.78,foot_colour)
        else:
            guards=smooth(.25,.80,hair)*tail_weight*.35
            colour=paint(guards,np.minimum(pale*.88+.045,.87))
            colour=paint(paws*.48,base*.64)
            if 'rusty' in label:colour=paint(inner*.5,[.46,.25,.12])
    if canid:
        colour=paint(cheek*.85,pale)
        if a.slug=='red-fox':
            colour=paint(stockings*.98,[.050,.041,.032]);colour=paint(ear_outer*.95,[.055,.043,.032])
            if 'cross' in label:
                cross=np.exp(-((p[:,1]-.35)/.17)**2);spine=np.exp(-((p[:,0]-cx)/.10)**2)
                colour=paint(back*np.maximum(cross,spine)*.85,[.065,.050,.037])
            if 'Silver' in label:
                guards=smooth(.25,.75,hair)*smooth(.40,1.00,p[:,2])*.70
                colour=paint(guards,[.64,.63,.58])
            colour=paint(tail_tip,[.85,.83,.76])
        else:
            strength=.12 if 'Ivory' in label else .55
            colour=paint(back*strength,base*.42)
            colour=paint(forehead*.28,base*.55)
            colour=paint(tail_weight*.30,base*.55)
    if a.slug=='hare':colour=paint(tail_top*.9,[.055,.05,.045])
    colour=paint(eye_surround*(.65 if kind=='caprine' else .20 if kind=='ursid' else .5),base*.55+pale*.45)
    colour*=np.clip(1+hair[:,None]*(.24 if canid else .32),.64,1.34)
    colour=paint(inner,base*.65+[.025,.018,.012] if kind in {'ursid','bovine','small-carnivore','beaver'} else [.47,.35,.31])
    if kind=='rodent':colour=paint(inner,base*.6+[.10,.065,.05])
    if dark_tips:colour=paint(tips*.94,[.045,.042,.037])
    if a.slug in {'raccoon','badger'}:colour=paint(eye_surround*.92,[.045,.036,.026])
    colour=paint(hoof,[.065,.054,.044])
    colour=paint(bare,[.075,.058,.045]);colour=paint(nose_mask,[.16,.115,.09] if kind=='suine' else [.055,.043,.034] if kind=='caprine' else [.040,.032,.025] if kind in {'ursid','bovine'} else [.045,.035,.029] if canid else [.045,.034,.025] if kind in {'small-carnivore','beaver'} else [.12,.09,.08] if a.slug=='hare' else [.23,.15,.14])
    if kind=='rodent':colour=paint(nose_mask,[.29,.17,.145] if a.slug=='mouse' else [.075,.052,.037])
    if 'mottled' in label:
        m=(.5+.5*np.sin(p[:,1]*21+np.sin(p[:,2]*15)*2))*smooth(.1,.7,n[:,2])*.2
        colour=paint(m,[.13,.105,.075])
    colour=treatment(colour,label,p,n,np.maximum.reduce([bare,nose_mask,hoof,inner,tips]))
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
if extra_fur_sources:
    summary['extra_fur_sources']=extra_fur_sources
    if kind=='bovine':summary['forequarter_shag_pixels']=int((shag>.5).sum())
    (root/'coat-bake.json').write_text(json.dumps(summary,indent=2)+'\n')
print('COAT ATLAS BAKED',a.slug,len(profiles),'coats',a.size,'pixels',summary['seconds'],'seconds',summary['mask_pixels'],flush=True)
