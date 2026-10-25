"""CPU anatomical feather atlas for explicitly inspected selected bird bodies.

Duck and pheasant. Uses two shared feather sources, linked plumage patterns
and real wing skin membership. Does not change geometry, skin or motion.
"""
import argparse,hashlib,importlib.util,json,sys,time
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter,map_coordinates,distance_transform_edt

repo=Path(__file__).resolve().parent.parent
sys.path.insert(0,str(repo/'scripts'))
from fauna_paint_recipes import profiles,eye_radius,eye_frame
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--size',type=int,default=2048)
a=ap.parse_args();assert a.slug in {'duck','pheasant','grouse','quail','turkey','junglefowl'},'No inspected feather masks for this bird yet'
root=repo/'artifacts/fauna-painted-20261024'/a.slug;started=time.perf_counter()
state=json.loads((root/'paint-state.json').read_text());assert not state['appearance_approved']
land=json.loads((root/'face-landmarks.json').read_text());d=dict(np.load(root/'paint-anatomy.npz'))
raw=json.loads((root/'selected-source-entry.json').read_text()) if (root/'selected-source-entry.json').exists() else json.loads((repo/'artifacts/fauna-overnight-20261024'/a.slug/'audition.json').read_text())['candidates'][state['source_candidate']-1]
groups=np.asarray(raw['groups']);d['region_weights']=np.zeros((len(d['vertices']),10),np.float32)
for channel,kind in [(0,'wing'),(1,'tail'),(2,'neck'),(3,'hind')]:d['region_weights'][:,channel]=d['weights'][:,groups==kind].sum(1)
spec=importlib.util.spec_from_file_location('coat_raster',repo/'scripts/bake-deer-masked-coat.py');raster=importlib.util.module_from_spec(spec);spec.loader.exec_module(raster)
maps,occupied,_=raster.raster(d,a.size);iy,ix=np.where(occupied);p=maps[iy,ix,:3];n=maps[iy,ix,3:6];n/=np.maximum(np.linalg.norm(n,axis=1,keepdims=True),1e-8)

def smooth(lo,hi,x):
    t=np.clip((x-lo)/(hi-lo),0,1);return t*t*(3-2*t)

sources=[]
def source(filename):
    path=repo/'artifacts/fauna-assets/textures'/filename
    data=np.asarray(Image.open(path).convert('RGB').resize((768,768)),np.float32)/255
    lum=data@np.array([.2126,.7152,.0722]);detail=lum-gaussian_filter(lum,14,mode='wrap');detail/=max(float(np.std(detail))*3,1e-5)
    sources.append({'path':str(path.relative_to(repo)),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
    def sample(u,v):return map_coordinates(detail,[np.mod(v,1)*767,np.mod(u,1)*767],order=1,mode='wrap')
    return sample

contour=source('feather-contour-v1.png');flight=source('feather-flight-v1.png')
blend=np.abs(n)**6;blend/=np.maximum(blend.sum(1,keepdims=True),1e-8)
feathers=contour(p[:,2]*2.8,-p[:,1]*2.8)*blend[:,0]+contour(p[:,0]*2.8,p[:,2]*2.8)*blend[:,1]+contour(p[:,0]*2.8,-p[:,1]*2.8)*blend[:,2]
cx=state['center_x'];head=np.array(state['head_seed']);nose=np.array(land['nose']['point'])
eyes=(np.array(land['right_eye']['point'])+np.array(land['left_eye']['point']))/2
front=np.array(land['front_axis']);side=np.array(land['side_axis']);forward=(p-eyes)@front;lateral=(p-eyes)@side
head_mask=smooth(1.17,1.25,p[:,2])*smooth(.28,.43,p[:,1])
if a.slug=='pheasant':head_mask=smooth(1.30,1.38,p[:,2])*smooth(1.00,1.10,p[:,1])
groundbird=a.slug in {'grouse','quail','turkey','junglefowl'}
if groundbird:head_mask=smooth(eyes[2]-.18,eyes[2]-.05,p[:,2])*smooth(head[1]-.18,head[1]-.02,p[:,1])
head_detail=contour(p[:,1]*5.6,p[:,2]*5.6)*blend[:,0]+contour(p[:,0]*5.6,p[:,2]*5.6)*blend[:,1]+contour(p[:,0]*5.6,p[:,1]*5.6)*blend[:,2]
feathers=feathers*(1-head_mask)+head_detail*head_mask
bill=smooth(.045,.085,forward)*(1-smooth(1.40,1.46,p[:,2]))*smooth(1.20,1.25,p[:,2])
feet=1-smooth(.20,.28,p[:,2])
if a.slug=='pheasant':
    bill=smooth(.055,.078,forward)*(1-smooth(1.53,1.56,p[:,2]))*smooth(1.43,1.47,p[:,2])
    feet=1-smooth(.29,.36,p[:,2])
if groundbird:
    bill=smooth(.030,.075,forward)*(1-smooth(eyes[2]+.01,eyes[2]+.06,p[:,2]))*smooth(eyes[2]-.17,eyes[2]-.10,p[:,2])
    feet=1-smooth(.17,.25,p[:,2])
wing=smooth(.28,.60,maps[iy,ix,6])*smooth(.16,.27,np.abs(p[:,0]-cx))
if a.slug=='pheasant':wing=smooth(.12,.24,np.abs(p[:,0]-cx))*smooth(.04,.25,p[:,1])*(1-smooth(.85,1.05,p[:,1]))*smooth(.40,.60,p[:,2])*(1-smooth(.95,1.11,p[:,2]))
wing_span=np.abs(p[:,0]-cx)
flight_mask=wing*smooth(.40,.62,wing_span)
long_detail=flight(p[:,1]*2.1,wing_span*.9)
if a.slug=='pheasant':
    flight_mask=wing*(1-smooth(.25,.55,p[:,1]))
    long_detail=flight(p[:,0]*3,-p[:,1]*1.5)
feathers=feathers*(1-flight_mask)+long_detail*flight_mask
tail=smooth(.28,.65,maps[iy,ix,7])
if a.slug=='pheasant':tail=smooth(.06,-.25,p[:,1])
feathers=feathers*(1-tail)+flight(p[:,0]*2.5,-p[:,1]*1.2)*tail
# Secondary band belongs only to the actual upper wing, never the flank.
secondary=wing*smooth(.32,.43,wing_span)*(1-smooth(.72,.84,wing_span))*smooth(-.10,.13,n[:,2])
band_center=.31;band_distance=np.abs(p[:,1]-band_center)
speculum=secondary*(1-smooth(.050,.074,band_distance))
white_bars=secondary*smooth(.060,.078,band_distance)*(1-smooth(.090,.108,band_distance))
collar=(1-smooth(.009,.027,np.abs(p[:,2]-1.20)))*smooth(.30,.42,p[:,1])*(1-wing)
breast=smooth(.12,.32,p[:,1])*(1-smooth(1.12,1.19,p[:,2]))*smooth(.53,.67,p[:,2])*(1-wing)
if a.slug=='pheasant':
    secondary*=0;speculum*=0;white_bars*=0
    collar=(1-smooth(.010,.030,np.abs(p[:,2]-1.29)))*smooth(.95,1.10,p[:,1])
    breast=smooth(.65,.95,p[:,1])*(1-smooth(1.20,1.29,p[:,2]))*smooth(.56,.72,p[:,2])
eye_surround=np.zeros(len(p));bare=np.zeros(len(p));rx,rz=eye_radius(a.slug)
for key in ['right_eye','left_eye']:
    normal,horizontal,vertical=eye_frame(a.slug,land,key.split('_')[0]);delta=p-np.array(land[key]['point'])
    radius=np.sqrt(((delta@horizontal)/rx)**2+((delta@vertical)/rz)**2)
    gate=(1-smooth(.020,.050,np.abs(delta@normal)))*smooth(.05,.65,n@normal)
    eye_surround=np.maximum(eye_surround,(1-smooth(1.12,1.65,radius))*gate)
    bare=np.maximum(bare,(1-smooth(.70,.90,radius))*gate)
nostril=np.zeros(len(p));nostril_anchors=[]
for sign in ([-1,1] if a.slug=='duck' else []):
    target=nose-front*.10+side*sign*.040+np.array([0,0,.033])
    candidates=(d['vertices'][:,1]>.75)&(d['vertices'][:,1]<.92)&(d['vertices'][:,2]>1.28)&(d['vertices'][:,2]<1.43)&(d['normals'][:,2]>.20)
    points=d['vertices'][candidates];assert len(points)>8
    anchor=points[np.argmin(np.sum(((points-target)/[.4,1,.8])**2,axis=1))];delta=p-anchor
    q=((delta@front)/.015)**2+((delta@side)/.018)**2+(delta[:,2]/.007)**2
    nostril=np.maximum(nostril,(1-smooth(.60,1.25,q))*bill)
    nostril_anchors.append({'side':sign,'point':anchor.tolist(),'pixels':int((((1-smooth(.60,1.25,q))*bill)>.5).sum())})
height=feathers*.00009*(1-bill)*(1-feet)*(1-bare)
scale_lines=.5+.5*np.sin(p[:,2]*260+np.sin(p[:,0]*45)*.3)
distance,nearest=distance_transform_edt(~occupied,return_indices=True);pad=(~occupied)&(distance<=12)
def extend(array):array[pad]=array[nearest[0][pad],nearest[1][pad]];return array
coat_records=[]
for index,(label,base,pale,_) in enumerate(profiles(a.slug)):
    base=np.asarray(base);pale=np.asarray(pale);colour=np.tile(base,(len(p),1))
    def paint(mask,target):return colour*(1-mask[:,None])+np.asarray(target)*mask[:,None]
    male=index in {0,4};eclipse=index==2
    if male:
        green=[.045,.205,.105] if index==0 else [.16,.205,.105]
        colour=paint(head_mask,green)
        colour=paint(breast,[.30,.12,.065] if index==0 else [.29,.22,.11])
        colour=paint(collar,[.83,.81,.72])
        rump=smooth(-.38,-.63,p[:,1])*(1-wing)
        colour=paint(rump,[.045,.041,.034]);colour=paint(tail*.65,pale)
        # Fine flank vermiculation is pigment, not displacement.
        verm=(.5+.5*np.sin(p[:,2]*200+np.sin(p[:,1]*31)*1.3))*.10*(1-head_mask)*(1-wing)*(1-breast)
        colour=paint(verm,base*.50)
    else:
        mottling=smooth(-.10,.65,feathers)*.72*(1-bill)*(1-feet)
        colour=paint(mottling,base*.36)
        underside=(1-smooth(.64,.94,p[:,2]))*(1-feet)*(1-wing)
        colour=paint(underside*.26,pale)
        eyebrow=(1-smooth(.008,.025,np.abs(p[:,2]-eyes[2]-.039)))*head_mask
        colour=paint(eyebrow*.65,pale)
        if eclipse:
            residual=head_mask*smooth(eyes[2]+.015,eyes[2]+.10,p[:,2])*.30
            colour=paint(residual,[.10,.17,.095]);colour=paint(tail*.50,[.80,.78,.68])
    colour*=np.clip(1+feathers[:,None]*.20,.72,1.28)
    colour=paint(wing*.20,base*.75)
    colour=paint(white_bars,[.82,.80,.70]);colour=paint(speculum,[.12,.16,.37])
    colour=paint(feet,np.array([.68,.30,.07])[None,:]*(.87+.13*scale_lines[:,None]))
    bill_colour=[.64,.51,.105] if male or eclipse else [.55,.32,.16]
    colour=paint(bill,bill_colour)
    if not male and not eclipse:
        flecks=bill*smooth(.16,.65,np.sin(p[:,1]*135+np.sin(p[:,0]*190)))*.70
        colour=paint(flecks,[.15,.105,.06])
    nail=bill*smooth(nose@front-.022,nose@front-.006,p@front)
    colour=paint(nail*.80,[.17,.13,.08]);colour=paint(nostril,[.045,.035,.025])
    if a.slug=='pheasant':
        # Species-specific ground-bird masks override the duck's palette work
        # above. The shared feather/UV processing is retained.
        male=index in {0,3,4};colour=np.tile(base,(len(p),1))
        markings=smooth(.05,.70,feathers)*(.30 if male else .64)*(1-bill)*(1-feet)
        colour=paint(markings,base*.28)
        colour*=np.clip(1+feathers[:,None]*.18,.75,1.25)
        colour=paint(wing*.36,pale)
        tail_bars=smooth(.25,.68,np.sin(p[:,1]*42+np.sin(p[:,0]*12)*.15))*tail
        colour=paint(tail*.75,pale*.85);colour=paint(tail_bars*.82,base*.28)
        if male:
            colour=paint(breast*.68,base*.90+np.array([.045,.005,0]))
            colour=paint(head_mask,[.035,.13,.095] if index!=4 else [.025,.075,.055])
            colour=paint(collar*(.22 if index==3 else .88),[.81,.79,.69])
            red_face=np.zeros(len(p))
            for key in ['right_eye','left_eye']:
                normal,horizontal,vertical=eye_frame(a.slug,land,key.split('_')[0]);delta=p-np.array(land[key]['point'])
                q=((delta@horizontal)/.055)**2+((delta@vertical)/.043)**2
                gate=(1-smooth(.025,.065,np.abs(delta@normal)))*smooth(.03,.55,n@normal)
                red_face=np.maximum(red_face,(1-smooth(.65,1.20,q))*gate)
            colour=paint(red_face,[.58,.065,.035])
        colour=paint(feet,np.array([.43,.36,.24])[None,:]*(.88+.12*scale_lines[:,None]))
        colour=paint(bill,[.56,.48,.32])
    if groundbird:
        colour=np.tile(base,(len(p),1));male='male' in label and 'female' not in label
        body=(1-bill)*(1-feet)
        mottling=smooth(-.05,.65,feathers)*.38*body
        colour=paint(mottling,base*.32)
        row=np.floor(p[:,2]/.044);fu=np.mod(p[:,1]/.063+row*.5,1)-.5;fv=np.mod(p[:,2]/.044,1)
        feather_arc=np.sqrt((fu/.52)**2+((fv-.12)/.88)**2)
        scallop=smooth(.76,.84,feather_arc)*(1-smooth(.92,1.04,feather_arc))
        breaks=.55+.45*np.sin(p[:,1]*37+p[:,2]*27)**2
        bars=scallop*breaks*body*(1-head_mask)
        colour=paint(bars*.17,pale*.78)
        underside=(1-smooth(eyes[2]-.65,eyes[2]-.25,p[:,2]))*(1-feet)
        colour=paint(underside*.25,pale)
        if a.slug=='quail':
            stripe=(1-smooth(.008,.022,np.abs(p[:,2]-eyes[2]-.032)))*head_mask
            colour=paint(stripe*.85,pale)
            throat=head_mask*(1-smooth(eyes[2]-.08,eyes[2]-.025,p[:,2]))
            colour=paint(throat*.6,[.84,.81,.70] if male else pale)
        elif a.slug=='junglefowl' and male:
            colour=paint(head_mask*.90,pale)
            breast=smooth(head[1]-.40,head[1]-.10,p[:,1])*(1-smooth(eyes[2]-.24,eyes[2]-.12,p[:,2]))*(1-feet)
            colour=paint(breast*.9,[.045,.060,.039]);colour=paint(tail*.9,[.025,.070,.046])
            crown=head_mask*smooth(eyes[2]+.06,eyes[2]+.12,p[:,2])
            colour=paint(crown*.8,[.52,.055,.032])
        elif a.slug=='turkey':
            barehead=head_mask*smooth(eyes[2]-.13,eyes[2]-.04,p[:,2])
            colour=paint(barehead*.85,[.33,.17,.16] if male else [.34,.29,.27])
            facepatch=barehead*smooth(-.05,.04,forward)
            colour=paint(facepatch*.7,[.37,.065,.05] if male else [.28,.20,.18])
        colour*=np.clip(1+feathers[:,None]*.16,.78,1.22)
        colour=paint(feet,np.array([.41,.34,.23])[None,:]*(.88+.12*scale_lines[:,None]))
        colour=paint(bill,[.42,.34,.20])
    from fauna_hypothetical_coats import treatment
    colour=treatment(colour,label,p,n,np.maximum.reduce([head_mask,bill,feet,bare]))
    colour=paint(eye_surround*.25,base*.70);colour=paint(bare,[.055,.042,.030])
    image=np.zeros((a.size,a.size,3),np.float32);image[iy,ix]=np.clip(colour,0,1);extend(image)
    filename=f'coat-{index}-colour.png';Image.fromarray(np.uint8(np.clip(image,0,1)*255)).save(root/filename)
    coat_records.append({'label':label,'file':filename})
rough=np.clip(.75-bill*.25-feet*.10-bare*.28-head_mask*.10,.30,.80)
rough_image=np.ones((a.size,a.size),np.float32)*.75;rough_image[iy,ix]=rough;extend(rough_image)
Image.fromarray(np.uint8(rough_image*255)).save(root/'coat-roughness.png')
height_image=np.zeros((a.size,a.size),np.float32);height_image[iy,ix]=height;extend(height_image)
dy,dx=np.gradient(gaussian_filter(height_image,.55));physical_pixel=2.6/a.size
normal=np.dstack([-dx/physical_pixel,dy/physical_pixel,np.ones_like(dx)]);normal/=np.linalg.norm(normal,axis=2,keepdims=True)
Image.fromarray(np.uint8(np.clip(normal*.5+.5,0,1)*255)).resize((512,512),Image.Resampling.LANCZOS).save(root/'coat-normal.png')
report={'method':'CPU continuous rest-surface feather mapping, separate contour/flight sources and linked sex/season plumage; no animal photo projection','size':a.size,'normal_size':512,'coats':coat_records,'eye_radius':[rx,rz],'uv_coverage_fraction':float(occupied.mean()),'mask_pixels':{key:int((value>.5).sum()) for key,value in [('nose',bill),('bill',bill),('feet',feet),('wing',wing),('speculum',speculum),('bare_lids',bare),('nostril',nostril)]},'feather_sources':sources,'seconds':round(time.perf_counter()-started,2),'sources':['https://www.allaboutbirds.org/guide/Mallard/id'],'limits':'One selected adult body, no duckling proportions or swimming controller. Small relief is artistic feather detail, not measured PBR. Speculum is tied to existing wing membership; owner appearance/motion approval pending.'}
(root/'coat-bake.json').write_text(json.dumps(report,indent=2)+'\n')
report['nostril_anchors']=nostril_anchors
if a.slug=='pheasant':
    report['sources']=['https://mdc.mo.gov/discover-nature/field-guide/ring-necked-pheasant']
    report['limits']='Selected long-tailed adult body; female and immature are pigment studies without shortened female/juvenile tail or body proportions. Folded-wing Idle/Walk only; flight/takeoff remain missing. Feather detail is artistic, not measured PBR; owner art and motion approval pending.'
    report['folded_wing_mask_method']='Inspected rest-surface bounds; raw ground-bird skeleton has no separate wing/tail groups. No new wing or tail bones added.'
if groundbird:
    plan=json.loads((repo/'docs/fauna/SPECIES_VARIATION_PLANS.json').read_text())['animals'][{'grouse':'Grouse','quail':'Quail','turkey':'Turkey','junglefowl':'Junglefowl'}[a.slug]]
    report['sources']=plan.get('sources',[])
    report['limits']='Selected adult silhouette; sex labels describe pigment only. No new age proportions, ornaments, population rules or additional motions. Artistic feather relief; owner appearance review pending.'
(root/'coat-bake.json').write_text(json.dumps(report,indent=2)+'\n')
print('BIRD COAT ATLAS BAKED',a.slug,len(coat_records),a.size,report['seconds'],report['mask_pixels'],flush=True)
