"""CPU anatomical atlas painter for the remaining selected fauna.

Shared generated surface swatches supply fine detail; explicit recipes supply
anatomical pigment. Every mask uses continuous bind-surface coordinates rather
than a projected animal photograph. Geometry/skin/motion are read-only inputs.
"""
import argparse,hashlib,importlib.util,json,sys,time
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter,map_coordinates,distance_transform_edt
from scipy.spatial import cKDTree

repo=Path(__file__).resolve().parent.parent;sys.path.insert(0,str(repo/'scripts'))
from fauna_finish_catalog import SUPPORTED,PALETTES,category,profiles,radius,ROWS
from fauna_paint_recipes import eye_frame
from fauna_hypothetical_coats import treatment,rationale
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--size',type=int,default=1024)
a=ap.parse_args();assert a.slug in SUPPORTED;started=time.monotonic()
root=repo/'artifacts/fauna-painted-20261024'/a.slug
state=json.loads((root/'paint-state.json').read_text());land=json.loads((root/'face-landmarks.json').read_text())
d=dict(np.load(root/'paint-anatomy.npz'));entry=json.loads((root/'selected-source-entry.json').read_text())
groups=np.asarray(entry['groups']);weights=d['weights'];assert weights.shape[1]==len(groups)
kind=category(a.slug)
if kind=='snake':
    heads=d['joint_heads'];parents=d['parents'];arc=np.zeros(len(heads));tangents=np.zeros_like(heads)
    for j,parent in enumerate(parents):
        if parent>=0:arc[j]=arc[parent]+np.linalg.norm(heads[j]-heads[parent])
        children=np.where(parents==j)[0]
        tangents[j]=heads[children[0]]-heads[j] if len(children) else heads[j]-heads[parent] if parent>=0 else [0,1,0]
    vertex_tangent=weights@tangents;vertex_tangent/=np.maximum(np.linalg.norm(vertex_tangent,axis=1,keepdims=True),1e-8)
    across=np.cross(vertex_tangent,[0,0,1]);bad=np.linalg.norm(across,axis=1)<.01;across[bad]=np.cross(vertex_tangent[bad],[0,1,0]);across/=np.maximum(np.linalg.norm(across,axis=1,keepdims=True),1e-8)
    radial_up=np.cross(across,vertex_tangent)
    d['eye_regions']=np.stack([weights@arc,np.sum(d['normals']*radial_up,axis=1),np.sum(d['normals']*across,axis=1)],axis=1)
d['region_weights']=np.stack([weights[:,groups==g].sum(1) for g in ['spine','neck','tail','wing','fore','hind','head','ear','jaw','other']],axis=1)
spec=importlib.util.spec_from_file_location('coat_raster',repo/'scripts/bake-deer-masked-coat.py');raster=importlib.util.module_from_spec(spec);spec.loader.exec_module(raster)
maps,occupied,_=raster.raster(d,a.size);iy,ix=np.where(occupied);p=maps[iy,ix,:3];n=maps[iy,ix,3:6];n/=np.maximum(np.linalg.norm(n,axis=1,keepdims=True),1e-8)
pattern=PALETTES[a.slug][2];sources=[]
if kind=='snake':snake_arc=maps[iy,ix,16];snake_angle=np.arctan2(maps[iy,ix,18],maps[iy,ix,17])
def smooth(lo,hi,x):
    t=np.clip((x-lo)/(hi-lo),0,1);return t*t*(3-2*t)
def detail_source(path,scale=3):
    path=repo/path;data=np.asarray(Image.open(path).convert('RGB').resize((512,512)),np.float32)/255
    lum=data@np.array([.2126,.7152,.0722]);hi=lum-gaussian_filter(lum,14,mode='wrap');hi/=max(float(hi.std())*3,1e-5)
    blend=np.abs(n)**5;blend/=np.maximum(blend.sum(1,keepdims=True),1e-8)
    def tex(u,v):return map_coordinates(hi,[np.mod(v,1)*511,np.mod(u,1)*511],order=1,mode='wrap')
    sampled=tex(p[:,1]*scale,p[:,2]*scale)*blend[:,0]+tex(p[:,0]*scale,p[:,2]*scale)*blend[:,1]+tex(p[:,0]*scale,p[:,1]*scale)*blend[:,2]
    sources.append({'path':str(path.relative_to(repo)),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
    return sampled
detail=detail_source(state['fur_source'],3.4 if kind=='bird' else 2.8)
tail_weight=np.clip(maps[iy,ix,8],0,1);wing_weight=np.clip(maps[iy,ix,9],0,1)
eye_mid=(np.array(land['right_eye']['point'])+land['left_eye']['point'])/2
front=np.array(land['front_axis']);side=np.array(land['side_axis']);nose=np.array(land['nose']['point'])
rel=p-eye_mid;forward=rel@front;lateral=rel@side;eye_z=eye_mid[2]
eye_gap=np.linalg.norm(np.array(land['right_eye']['point'])-land['left_eye']['point'])
headscale=float(np.clip(eye_gap,.12,.5));headnear=np.exp(-np.sum((rel/[headscale*1.8,headscale*2.5,headscale*2.5])**2,axis=1))
face=smooth(-headscale*1.8,-headscale*.5,forward)*smooth(eye_z-headscale*1.9,eye_z-headscale*.7,p[:,2])
bodywidth=max(float(np.quantile(np.abs(d['vertices'][:,0]-state['center_x']),.8)),.12)
zlo,zhi=np.quantile(d['vertices'][:,2],[.05,.94]);span=max(float(zhi-zlo),.2)
belly=(1-smooth(zlo+span*.35,zlo+span*.65,p[:,2]))*(.25+.75*smooth(.25,-.65,n[:,2]))
belly*=1-face*.7
if kind=='bird':belly=np.maximum(belly,smooth(.12,.65,n@front)*(1-face)*.8)
throat=face*(1-smooth(eye_z-.10,eye_z+.02,p[:,2]))*smooth(-.25,.02,forward)
foot=1-smooth(float(zlo+.035),float(zlo+.14),p[:,2])
back=smooth(.05,.75,n[:,2])*(1-face)
wing=np.maximum(wing_weight,smooth(bodywidth*.72,bodywidth*1.1,np.abs(p[:,0]-state['center_x']))*(1-face)) if kind=='bird' else wing_weight
if kind=='bird':
    flight=detail_source('artifacts/fauna-assets/textures/feather-flight-v1.png',2.1)
    detail=detail*(1-wing*.8)+flight*wing*.8
if kind=='bat':
    fur=detail_source('artifacts/fauna-assets/textures/fur-dense-fine-v1.png',3.2)
    membrane=np.maximum(wing_weight,smooth(bodywidth*.5,bodywidth*.9,np.abs(p[:,0]-state['center_x'])))
    detail=detail*membrane+fur*(1-membrane)

surround=np.zeros(len(p));bare=np.zeros(len(p));eye_patches=[];rx,rz=radius(a.slug)
for key in ['right_eye','left_eye']:
    normal,horizontal,vertical=eye_frame(a.slug,land,key.split('_')[0]);delta=p-np.array(land[key]['point'])
    q=np.sqrt(((delta@horizontal)/rx)**2+((delta@vertical)/rz)**2)
    gate=(1-smooth(.025,.065,np.abs(delta@normal)))*smooth(.02,.65,n@normal)
    surround=np.maximum(surround,(1-smooth(1.3,2.0,q))*gate)
    bare=np.maximum(bare,(1-smooth(.78,.97,q))*gate)
    patch=(1-smooth(2.,3.6,q))*gate;eye_patches.append(patch)
nn=np.array(land['nose']['normal']);nn/=np.linalg.norm(nn);across=np.cross(nn,[0.,0,1.])
if np.linalg.norm(across)<1e-5:across=side.copy()
across/=np.linalg.norm(across);up=np.cross(across,nn);delta=p-nose
nosew=.055 if kind in {'ungulate','canid','bear','felid','marine','thickskin'} else .035
noseq=((delta@across)/nosew)**2+((delta@up)/(.035 if kind!='thickskin' else .06))**2+((delta@nn)/.045)**2
nosemask=(1-smooth(.6,1.3,noseq))*smooth(.02,.65,n@nn)
if a.slug=='elephant':nosemask[:]=0  # Trunk tip is skin, not a pig-like black nose disc.
mammal=kind in {'mammal','canid','ungulate','bear','felid','bat','hopper_marsupial','armoured_mammal','primate','spiny_mammal','thickskin'} or a.slug=='sea-otter'
if kind=='bird':
    bill=smooth(.035,.080,forward)*(1-smooth(eye_z+.01,eye_z+.07,p[:,2]))*smooth(eye_z-.22,eye_z-.10,p[:,2])*face
    bill_axis=nose-eye_mid;bill_length=max(float(bill_axis@bill_axis),1e-6)
    along=(rel@bill_axis)/bill_length;off=rel-along[:,None]*bill_axis
    tube=(1-smooth(headscale*.35,headscale*.65,np.linalg.norm(off,axis=1)))*smooth(.12,.25,along)*(1-smooth(1.05,1.20,along))
    bill=np.maximum(np.maximum(bill,nosemask),tube)
else:bill=np.zeros(len(p))
ear=np.zeros(len(p))
if mammal:
    ear=smooth(eye_z+.08,eye_z+.19,p[:,2])*(1-smooth(.025,.14,forward))*smooth(-.38,-.16,forward)*smooth(.04,.12,np.abs(lateral))
    ear*=smooth(.05,.7,n@front)
keratin=foot if kind=='ungulate' else np.zeros(len(p))
if kind=='turtle':
    shell=smooth(zlo+span*.25,zlo+span*.50,p[:,2])*(1-face)
    scutes=.5+.5*np.sin(p[:,1]*25+np.sin(p[:,0]*31)*2)*np.sin(p[:,0]*29)
else:shell=np.zeros(len(p))
scale_belly=(1-smooth(-.1,.3,n[:,2]))*.7 if kind in {'snake','reptile','amphibian','fish'} else belly
rough=np.full(len(p),.78 if mammal else .68 if kind=='bird' else .53 if kind in {'reptile','snake','turtle'} else .39 if kind in {'marine','fish','squid','amphibian'} else .57)
rough-=nosemask*(.43 if mammal else 0)+bare*.23+bill*.15+keratin*.2
rough=np.clip(rough,.22,.9)
height=detail*(.00012 if mammal or kind=='bird' else .00009)*(1-bare)*(1-nosemask*.9)*(1-bill)
if kind=='bat':rough=rough*(1-membrane)+.51*membrane
distance,nearest=distance_transform_edt(~occupied,return_indices=True);pad=(~occupied)&(distance<=12)
def extend(array):array[pad]=array[nearest[0][pad],nearest[1][pad]];return array

# Stable, irregular bind-space marks. No per-frame random noise or light bake.
u=p[:,1]*13;v=p[:,2]*17;cell=np.floor(u)*173+np.floor(v)*317
h=(np.sin(cell)*43758.5453)%1;j=(np.sin(cell+91)*22573.173)%1
du=(u%1)-.15-.7*h;dv=(v%1)-.15-.7*j;rr=np.sqrt(du*du+dv*dv)
spots=(1-smooth(.12,.24,rr))*(h>.28)*(1-face)
rosettes=smooth(.12,.19,rr)*(1-smooth(.25,.34,rr))*(h>.2)*(1-face)
if pattern in {'rosettes','ocelot'}:
    # Jittered seed cells, irregular broken rings and varying orientation/size.
    # No rigid grid of identical circular stamps.
    rng_marks=np.random.default_rng(29453)
    by,bz=np.mgrid[p[:,1].min()-.2:p[:,1].max()+.2:.135,p[:,2].min()-.2:p[:,2].max()+.2:.12]
    seeds=np.stack([by.ravel(),bz.ravel()],axis=1)+rng_marks.uniform(-.045,.045,(by.size,2))
    warped=np.stack([p[:,1]+.009*np.sin(p[:,2]*29),p[:,2]+.008*np.sin(p[:,1]*31)],axis=1)
    _,near=cKDTree(seeds).query(warped,k=3);delta=warped[:,None,:]-seeds[near]
    phase=(np.sin(near*37.15)*4567)%1*(2*np.pi)
    aspect=1.65 if pattern=='ocelot' else 1.
    du=delta[:,:,0]/aspect;dv=delta[:,:,1]*aspect
    theta=np.arctan2(dv,du);rad=.037+.016*((np.sin(near*19.13)*2378)%1)
    rr2=np.sqrt(du*du+dv*dv)/rad/(1+.10*np.sin(theta*3+phase)+.045*np.cos(theta*5-phase))
    rings=smooth(.58,.76,rr2)*(1-smooth(.97,1.11,rr2))*(1-smooth(.82,.98,np.cos(theta-phase)))
    rosettes=rings.max(1)*(1-face)
    face_dots=(1-smooth(.16,.31,rr2.min(1)))*face
    if a.slug=='jaguar':rosettes=np.maximum(rosettes,(1-smooth(.12,.25,rr2.min(1)))*(1-face)*.8)
flank=smooth(.15,.65,np.abs(n[:,0]))
bars=smooth(.35,.8,np.sin(p[:,2]*145+np.sin(p[:,1]*39)*2.6+np.sin(p[:,0]*47)*1.7))
rng=np.random.default_rng(3517)
noise=gaussian_filter(rng.normal(size=(24,24,24)),.8,mode='wrap');noise=(noise-noise.min())/(noise.max()-noise.min())
organic=map_coordinates(noise,[np.mod(p[:,0]*5+12,24),np.mod(p[:,1]*5+12,24),np.mod(p[:,2]*5+12,24)],order=1,mode='wrap')
coats=[]
for index,(label,base,pale,_) in enumerate(profiles(a.slug)):
    base=np.array(base);pale=np.array(pale);colour=np.tile(base,(len(p),1));low=label.lower()
    def paint(mask,target):
        return colour*(1-np.clip(mask,0,1)[:,None])+np.asarray(target)*np.clip(mask,0,1)[:,None]
    dark=np.array([.055,.045,.028]);white=np.array([.82,.80,.72])
    if pattern=='zebra' and ('brown' in low or 'foal' in low):dark=np.array([.24,.135,.065])
    colour=paint(belly*.65,pale)
    if kind in {'snake','reptile','amphibian','fish'}:colour=paint(scale_belly*.85,pale)
    if kind=='bird':
        colour=paint(bars*.15*(1-face)*(1-bill)*(1-foot),base*.45)
        colour=paint(wing*.25,base*.65)
    if pattern in {'grizzle','sloth','spines'}:
        colour=paint(smooth(.12,.78,detail)*(.35 if pattern!='spines' else .6),pale)
    mark_strength=.55 if 'hypothetical' in low and any(w in low for w in ['muted','subdued','weak']) else 1.
    if pattern in {'rosettes','ocelot'}:
        colour=paint(rosettes*.92*flank*mark_strength,dark);colour=paint(spots*.6*(1-flank)*(1-face)*mark_strength,dark)
        colour=paint(face_dots*.85,dark)
    if pattern in {'hyena','civet','monitor','pike'}:colour=paint(spots*.82,dark)
    if pattern in {'paca','quoll'}:
        marks=spots*.95*flank
        if pattern=='paca':marks*=smooth(.15,.65,.5+.5*np.cos(p[:,2]*43))
        colour=paint(marks,white)
    if pattern in {'tiger','zebra','ass','iguana'}:
        wave=p[:,1]*(23 if pattern=='tiger' else 35)+np.sin(p[:,2]*8)*1.7+np.sin(p[:,0]*10)*.7
        stripes=smooth(.27,.60,np.sin(wave))*flank
        if pattern=='ass':stripes*=foot*.75
        colour=paint(stripes*(.95 if pattern!='iguana' else .45),dark)
        if pattern=='zebra':colour=paint(face*smooth(.30,.65,np.sin(forward*74+lateral*19))*.95,dark)
    if pattern=='giraffe':
        # Irregular cells with fine pale corridors, not regular polka dots.
        seeds=rng.uniform(d['vertices'].min(0)-.1,d['vertices'].max(0)+.1,size=(650,3))
        distances,indices=cKDTree(seeds).query(p,k=2)
        patch=smooth(.012,.025,distances[:,1]-distances[:,0])*(1-foot*.7)
        phase=(np.sin(indices[:,0]*47)*137)%1
        colour=paint(patch*.9,np.array([.39,.25,.11])[None,:]*(.90+.10*phase[:,None]))
    if pattern in {'gazelle','oryx','pronghorn'}:
        colour=paint(belly,pale)
        stripe=np.exp(-((p[:,2]-(zlo+span*.52))/.065)**2)*flank*(1-face)
        if pattern!='pronghorn':colour=paint(stripe*(.10 if a.slug=='impala' else .8)*mark_strength,dark)
        colour=paint(face*.45,pale)
        if pattern=='oryx':
            colour=paint(surround*.9,dark)
            if 'scimitar' in low:
                colour=np.tile([.78,.76,.67],(len(p),1))
                neck=smooth(-headscale*3.,-headscale*.55,forward)*(1-smooth(-.03,.06,forward))*smooth(eye_z-.65,eye_z-.28,p[:,2])
                colour=paint(neck*.85,[.48,.25,.10])
        if a.slug=='impala':
            rump=smooth(.35,.75,-n[:,1])*smooth(-.05,.40,p[:,2]-zlo)*(1-face)
            marks=np.maximum(np.exp(-((np.abs(p[:,0]-state['center_x'])-.12)/.028)**2)*rump,tail_weight*.75)
            colour=paint(marks*.8,dark)
        if pattern=='pronghorn':
            colour=paint(np.maximum(tail_weight*.95,belly*.9),white)
            colour=paint(throat*.9,white)
            if 'male' in low:colour=paint(face*np.exp(-((forward+.035)/.06)**2)*.75,dark)
    if pattern=='collar':colour=paint(np.exp(-((forward+headscale*2)/.10)**2)*(1-foot)*.85,pale)
    if pattern=='saddle':colour=paint(back*.6,pale)
    if pattern=='painted-dog':
        patches=organic
        colour=paint(smooth(.55,.72,patches)*.95,dark)
        colour=paint((1-smooth(.17,.30,patches))*.85,white)
        colour=paint(face*.80,dark);colour=paint(tail_weight*smooth(-.8,-1.2,p[:,1]),white)
    if pattern in {'skunk','wolverine','anteater','tapir','guereza','panda'}:
        if pattern=='skunk':
            stripe=np.exp(-((np.abs(p[:,0]-state['center_x'])-.16)/.075)**2)*smooth(.25,.75,n[:,2])
            colour=paint(np.maximum(stripe,tail_weight*.65),white)
        elif pattern=='wolverine':colour=paint(np.exp(-((p[:,2]-(zlo+span*.65))/.13)**2)*flank*.8,pale)
        elif pattern=='anteater':
            stripe=np.exp(-((p[:,2]-(zlo+span*.4+(p[:,1]+.3)*.35))/.15)**2)*flank
            colour=paint(stripe*.95,dark)
        elif pattern=='tapir' and any(x in low for x in ('saddle','malay')):
            colour=paint(smooth(-.55,-.35,p[:,1])*(1-smooth(.20,.40,p[:,1]))*.95,pale)
        elif pattern=='tapir' and 'calf' in low:
            stripes=smooth(.35,.75,np.sin(p[:,2]*46+np.sin(p[:,1]*11)*.7))*flank*(1-face)
            broken=smooth(.14,.40,organic)
            colour=paint(np.maximum(stripes*broken,spots*.45)*.9,[.76,.69,.51])
        elif pattern=='guereza':colour=paint(np.maximum(flank*(1-face)*.8,tail_weight),white)
        elif pattern=='panda':
            colour=paint(foot*.97,dark)
            limbs=np.maximum(maps[iy,ix,10],maps[iy,ix,11]);colour=paint(smooth(.20,.65,limbs)*.98,dark)
            for patch in eye_patches:colour=paint(patch*.98,dark)
            colour=paint(ear*.98,dark)
    if kind=='primate':
        face_skin=smooth(-.06,.05,forward)*headnear*(1-smooth(eye_z+.13,eye_z+.25,p[:,2]))
        skin=[.31,.25,.19] if pattern not in {'orangutan','gorilla','capuchin'} else [.24,.19,.14] if pattern=='orangutan' else [.18,.18,.15] if pattern=='gorilla' else [.68,.58,.42]
        colour=paint(face_skin*.85,skin)
        colour=paint(foot*.70,np.array(skin)*.8)
        if pattern in {'capuchin','tamarin'}:colour=paint(throat*.75,pale)
    if pattern=='sloth':
        colour=paint(face*.7,pale)
        for patch in eye_patches:colour=paint(patch*.85,dark)
    if pattern=='otter':colour=paint(face*.6,pale)
    if a.slug in {'walrus','elephant'}:
        ivory=smooth(.06,.10,np.abs(lateral))*(1-smooth(.32,.42,np.abs(lateral)))*smooth(-.02,.06,forward)*(1-smooth(eye_z-.12,eye_z-.05,p[:,2]))
        if a.slug=='elephant':
            # The selected body's checked tusk/mouth region, excluding ears.
            ivory=smooth(.82,.91,p[:,1])*smooth(.06,.09,np.abs(p[:,0]))*(1-smooth(.34,.40,np.abs(p[:,0])))*smooth(.65,.70,p[:,2])*(1-smooth(1.08,1.15,p[:,2]))
        colour=paint(ivory*.95,[.78,.73,.60])
    if a.slug=='rhinoceros':
        horn=smooth(eye_z+.06,eye_z+.17,p[:,2])*smooth(-.02,.05,forward)
        colour=paint(horn*.85,[.32,.28,.20])
    if pattern=='lion':
        mane=np.exp(-((forward+.18)/.32)**2)*smooth(eye_z-.65,eye_z-.40,p[:,2])*(1-smooth(-.03,.12,forward))
        colour=paint(mane*.9,[.24,.16,.075] if any(x in low for x in ('dark','black','mature')) else [.47,.32,.14])
    if pattern=='gorilla' and 'silver' in low:colour=paint(back*.7,[.47,.47,.42])
    if pattern in {'koala','lemur','meerkat'}:
        colour=paint(throat*.75,pale)
        for patch in eye_patches:colour=paint(patch*(.8 if pattern!='koala' else .35),dark)
        if pattern=='lemur':colour=paint(tail_weight*smooth(.1,.65,np.sin(p[:,1]*31))*.95,white)
        if pattern=='meerkat':colour=paint(bars*back*.35,dark)
    if pattern in {'boa','python','viper'}:
        saddles=smooth(.20,.65,np.sin(snake_arc*22+np.cos(snake_angle*2)*1.3))*(1-scale_belly)
        colour=paint(saddles*.8,dark)
        colour=paint(scale_belly*.8,pale)
        if pattern=='viper':colour=paint((1-smooth(.14,.34,np.abs(snake_angle-.43*np.sin(snake_arc*26))))*(1-scale_belly)*.8,dark)
    if pattern in {'lizard','crocodile','salamander','monitor','iguana'}:
        colour=paint(spots*.35*(1-scale_belly),dark)
        if pattern=='salamander':colour=paint(spots*.80,[.71,.59,.22])
        if pattern=='crocodile':colour=paint(back*.35,base*.6)
    if kind=='turtle':
        colour=paint(shell*scutes*.35,base*.45)
        colour=paint((1-shell)*.6,pale*.65)
        colour=paint(belly*.85,pale)
    if pattern=='shark':
        gillregion=np.exp(-((p[:,2]-eye_z+.08)/.16)**2)*flank
        cuts=np.zeros(len(p))
        for offset in [.17,.21,.25,.29,.33]:cuts=np.maximum(cuts,np.exp(-((forward+offset+(p[:,2]-eye_z)*.25)/.006)**2))
        colour=paint(cuts*gillregion*.55,base*.42)
    if pattern=='armour':
        grooves=.5+.5*np.cos(p[:,1]*72+np.sin(p[:,0]*25))
        colour=paint(grooves*.20*(1-belly),base*.55)
    if pattern in {'dolphin','shark','fish','pike','whale','seal','orca','penguin','puffin'}:
        counter=(1-smooth(zlo+span*.42,zlo+span*.64,p[:,2]))
        if pattern=='penguin':
            colour=np.tile(base,(len(p),1));counter=smooth(.05,.55,n@front)*(1-smooth(eye_z-.20,eye_z-.08,p[:,2]))
        colour=paint(counter*.95,pale)
        if pattern=='seal':colour=paint(spots*.28,base*.55)
        if pattern=='orca':
            colour=paint(counter,white)
            for side_name in ['right','left']:
                anchor=np.array(land[side_name+'_eye']['point'])-front*.12+np.array([0,0,.07])
                delta=p-anchor;q=((delta@front)/.12)**2+(delta[:,2]/.055)**2
                patch=(1-smooth(.65,1.15,q))*smooth(.18,.55,np.abs(n@side))
                colour=paint(patch,white)
        if pattern=='puffin':colour=paint(face*.80,pale)
    if kind=='bird':
        wingtip=wing*smooth(bodywidth*.9,bodywidth*1.25,np.abs(p[:,0]-state['center_x']))
        colour=paint(wingtip*.7,dark)
        if pattern in {'owl','snowy-owl'}:
            colour=paint(face*.65,pale);colour=paint(bars*.12*(1-bill)*(1-face),base*.45)
            for patch in eye_patches:colour=paint(patch*.35,base*.45)
        if pattern in {'eagle','stork','swan','gull','albatross','pelican','tern'}:colour=paint(face*.95,white)
        if pattern=='raptor':colour=paint(belly*.65,pale);colour=paint(bars*belly*.25,dark)
        if pattern in {'crane','heron','goose','secretary'}:
            colour=paint(throat*.7,pale);colour=paint(wingtip*.95,dark)
        if pattern=='flamingo':colour=paint(wingtip*.95,dark)
        if pattern=='ostrich':colour=paint(wing*.85,white);colour=paint(face*.70,[.58,.51,.40])
        if pattern=='emu':colour=paint(face*.8,[.20,.23,.20])
        if pattern=='cassowary':colour=paint(face*.92,[.13,.29,.38]);colour=paint(throat*.7,[.44,.10,.075])
        if pattern in {'hornbill','toucan'}:colour=paint(throat*.9,[.71,.62,.22]);colour=paint(bill,[.66,.48,.15])
        if pattern in {'kingfisher','hummingbird','peafowl','parrot','turaco'}:
            if pattern=='kingfisher':colour=paint(belly*.95,[.65,.34,.12])
            if pattern=='hummingbird':colour=paint(throat*.85,[.54,.12,.095])
            if pattern=='peafowl':
                female='female' in low
                colour=paint(face*.95,[.30,.28,.18] if female else [.065,.22,.35]);colour=paint(wing*.50,[.22,.31,.13])
                train=np.maximum(tail_weight,smooth(-.25,-.55,p[:,1])*smooth(.05,.55,n[:,2]))
                colour=paint(train*.85,[.25,.25,.12] if female else [.18,.29,.12])
                if not female:
                    tu=(p[:,1]*8)%1-.5;tv=(p[:,0]*13+.12*np.sin(p[:,1]*7))%1-.5
                    q=(tu/.36)**2+(tv/.28)**2
                    for limit,target in [(1.,[.48,.36,.12]),(.70,[.11,.32,.23]),(.41,[.065,.22,.37]),(.17,[.025,.055,.055])]:
                        colour=paint((1-smooth(limit*.85,limit,q))*train,target)
            if pattern=='parrot':colour=paint(wingtip*.65,[.10,.28,.42]);colour=paint(face*smooth(eye_z+.06,eye_z+.17,p[:,2])*.6,[.55,.10,.07])
            if pattern=='turaco':colour=paint(wingtip*.7,[.48,.07,.05])
        if pattern=='cockatoo':colour=paint(face*smooth(eye_z+.12,eye_z+.25,p[:,2]),[.70,.60,.18])
        if pattern=='woodpecker':
            colour=paint(spots*wing*.9,white);colour=paint(belly*.90,white)
            colour=paint(face*smooth(eye_z+.06,eye_z+.15,p[:,2])*.9,[.50,.045,.03])
        if pattern=='pigeon':colour=paint(throat*.55,[.18,.29,.23]);colour=paint(wing*bars*.25,dark)
        if pattern in {'puffin','penguin'}:colour=paint(face*.6,pale)
        footcol=[.60,.22,.08] if pattern in {'puffin','penguin','goose','swan','stork'} else [.60,.43,.16] if pattern in {'raptor','eagle','gull','tern'} else [.29,.25,.18]
        billcol=[.66,.41,.12] if pattern in {'puffin','gull','tern','eagle'} else [.51,.13,.075] if pattern=='stork' else [.19,.16,.11]
        if pattern in {'hornbill','toucan'}:billcol=[.69,.51,.16]
        colour=paint(foot,footcol);colour=paint(bill,billcol)
        if pattern=='flamingo':colour=paint(foot,[.64,.29,.25]);colour=paint(bill,[.18,.14,.12])
    if pattern=='platypus':
        duckbill=smooth(.04,.09,forward)*(1-smooth(eye_z+.01,eye_z+.08,p[:,2]))*smooth(eye_z-.20,eye_z-.10,p[:,2])
        colour=paint(duckbill,[.25,.23,.18]);colour=paint(foot*.8,[.24,.20,.14])
    if kind=='bat':colour=paint(membrane*.85,[.19,.13,.085])
    if kind in {'ground_insect','spider'}:
        colour=paint(spots*.4,base*.4)
        colour=paint(bars*.25,pale)
    if mammal:
        colour=paint(ear*.6,base*.65+[.035,.02,.01]);colour=paint(keratin,[.09,.075,.055])
        colour=paint(nosemask,[.075,.057,.038])
    colour*=np.clip(1+detail[:,None]*(.17 if mammal else .10),.75,1.25)
    colour=treatment(colour,label,p,n,np.maximum.reduce([face,foot,bill,bare]))
    colour=paint(bare,[.06,.045,.03])
    image=np.zeros((a.size,a.size,3),np.float32);image[iy,ix]=np.clip(colour,0,1);extend(image)
    filename=f'coat-{index}-colour.png';Image.fromarray(np.uint8(np.clip(image,0,1)*255)).save(root/filename)
    coats.append({'label':label,'file':filename})

roughmap=np.ones((a.size,a.size),np.float32)*.8;roughmap[iy,ix]=rough;extend(roughmap)
Image.fromarray(np.uint8(roughmap*255)).save(root/'coat-roughness.png')
heightmap=np.zeros((a.size,a.size),np.float32);heightmap[iy,ix]=height;extend(heightmap)
dy,dx=np.gradient(gaussian_filter(heightmap,.55));pixel=2.6/a.size
normal=np.dstack([-dx/pixel,dy/pixel,np.ones_like(dx)]);normal/=np.linalg.norm(normal,axis=2,keepdims=True)
Image.fromarray(np.uint8(np.clip(normal*.5+.5,0,1)*255)).resize((512,512),Image.Resampling.LANCZOS).save(root/'coat-normal.png')
report={'method':'Continuous bind-surface anatomical masks and shared generated surface detail; explicit species recipe','slug':a.slug,'category':kind,'pattern':pattern,'size':a.size,'normal_size':512,'coats':coats,'eye_radius':[rx,rz],'uv_coverage_fraction':float(occupied.mean()),'mask_pixels':{key:int((value>.5).sum()) for key,value in [('nose',nosemask),('bill',bill),('bare_lids',bare),('belly',belly),('ear',ear)]},'required_masks':[] if kind in {'ground_insect','spider'} else ['bare_lids'],'surface_sources':sources,'seconds':round(time.monotonic()-started,2),'appearance_approved':False,'limits':ROWS[a.slug].get('geometry_limits','Selected approved silhouette and motions; pigment studies do not add age/sex body proportions or missing behaviours.')}
(root/'coat-bake.json').write_text(json.dumps(report,indent=2)+'\n')
print('ANATOMICAL ATLAS',a.slug,len(coats),'coats',report['seconds'],'seconds',report['mask_pixels'],flush=True)
