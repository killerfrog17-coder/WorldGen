"""Bake reusable fur and anatomical masks onto the unchanged deer UVs, CPU only.

The generated swatch supplies fur detail, never eye/nose placement. Surface
anchors come from inspected geometry. Original mesh/skin/actions are not edited.
"""
import argparse
import hashlib
import json
from pathlib import Path
import time
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter,distance_transform_edt,binary_erosion,map_coordinates


def smooth(a,b,x):
    t=np.clip((x-a)/(b-a),0,1);return t*t*(3-2*t)


def raster(d,size):
    """Triangle-corner UV raster; no re-unwrapping or topology mutation."""
    channels=['body','head','neck','ear_L','ear_R','tail','ForeHoof_L','ForeHoof_R','HindHoof_L','HindHoof_R']
    if 'region_weights' in d:weights=d['region_weights']
    else:
        names=list(d['bone_names']);w=d['weights']
        def group(parts):return w[:,[i for i,n in enumerate(names) if n in parts]].sum(1)
        weights=np.stack([group(['Pelvis','Spine1','Spine2','Chest']),group(['Head','Jaw','Muzzle_L','Muzzle_R']),
            group(['Neck1','Neck2','Neck3']),group(['Ear_L']),group(['Ear_R']),group(['Tail1','Tail2']),
            *[group([n]) for n in channels[6:]]],axis=1)
    attributes=np.c_[d['vertices'],d['normals'],weights].astype(np.float32)
    out=np.zeros((size,size,attributes.shape[1]),np.float32);occupied=np.zeros((size,size),bool)
    for face,uv in zip(d['faces'],d['uv']):
        xy=np.c_[uv[:,0]*size-.5,(1-uv[:,1])*size-.5]
        lo=np.maximum(np.ceil(xy.min(0)).astype(int),0);hi=np.minimum(np.floor(xy.max(0)).astype(int),size-1)
        if np.any(lo>hi):continue
        x0,y0=xy[0];x1,y1=xy[1];x2,y2=xy[2]
        den=(y1-y2)*(x0-x2)+(x2-x1)*(y0-y2)
        if abs(den)<1e-9:continue
        yy,xx=np.mgrid[lo[1]:hi[1]+1,lo[0]:hi[0]+1]
        b0=((y1-y2)*(xx-x2)+(x2-x1)*(yy-y2))/den
        b1=((y2-y0)*(xx-x2)+(x0-x2)*(yy-y2))/den;b2=1-b0-b1
        inside=(b0>=-1e-5)&(b1>=-1e-5)&(b2>=-1e-5)
        y,x=yy[inside],xx[inside]
        out[y,x]=b0[inside,None]*attributes[face[0]]+b1[inside,None]*attributes[face[1]]+b2[inside,None]*attributes[face[2]]
        occupied[y,x]=True
    return out,occupied,channels


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
    ap.add_argument('--size',type=int,default=2048)
    ap.add_argument('--preset',type=int,default=0)
    a=ap.parse_args();root=a.root.resolve();start=time.perf_counter()
    def progress(stage):
        p=root/'progress.json';tmp=root/'progress.tmp';tmp.write_text(json.dumps(dict(stage=stage,ready=False)));tmp.replace(p)
        print(stage,flush=True)
    progress('Baking anatomical masks and fur')
    d=np.load(root/'anatomy.npz');anchors=json.loads((root/'anchors.json').read_text());land=json.loads((root/'landmarks.json').read_text())
    size=a.size;maps,coverage,channels=raster(d,size);iy,ix=np.where(coverage)
    p=maps[iy,ix,:3];n=maps[iy,ix,3:6];n/=np.maximum(np.linalg.norm(n,axis=1,keepdims=True),1e-6)
    weights=maps[iy,ix,6:];weight={name:weights[:,i] for i,name in enumerate(channels)}
    settings=json.loads((root/'coat-settings.json').read_text()) if (root/'coat-settings.json').exists() else {}
    palettes=[([.38,.215,.105],[.36,.265,.18],[.67,.62,.50]),
              ([.43,.235,.105],[.37,.265,.17],[.71,.67,.55]),
              ([.33,.30,.255],[.34,.31,.265],[.65,.62,.55]),
              ([.68,.66,.60],[.66,.65,.59],[.79,.77,.70]),
              ([.23,.155,.095],[.29,.22,.155],[.53,.48,.39]),
              ([.34,.19,.085],[.34,.24,.15],[.67,.62,.51]),
              ([.42,.265,.13],[.37,.27,.17],[.68,.62,.51]),
              ([.48,.36,.22],[.45,.35,.24],[.73,.67,.55])]
    body_colour,face_colour,pale_colour=palettes[a.preset%len(palettes)]
    count=len(p);colour=np.tile(body_colour,(count,1)).astype(np.float32)
    rough=np.full(count,.8,np.float32);height=np.zeros(count,np.float32);masks={}
    fur=np.asarray(Image.open(root/'fur-source.png').convert('RGB').resize((384,384),Image.Resampling.LANCZOS),np.float32)/255
    lum=fur@np.array([.2126,.7152,.0722]);detail=lum-gaussian_filter(lum,9,mode='wrap')
    # Repeat only small-scale hair variation. Broad shade/pattern comes from
    # surface masks, preventing baked illumination or swatch colour seams.
    detail=gaussian_filter(detail,.7,mode='wrap')
    def sample(u,v):return map_coordinates(detail,[np.mod(v,1)*383,np.mod(u,1)*383],order=1,mode='wrap')
    side=sample((p[:,1]*-.35+p[:,2]*.94)/.29,(p[:,1]*-.94-p[:,2]*.35)/.29)
    top=sample(p[:,0]/.29,-p[:,1]/.29)
    front=sample(p[:,0]/.22,-p[:,2]/.22)
    blend=np.abs(n)**6;blend/=np.maximum(blend.sum(1,keepdims=True),1e-8)
    hair=side*blend[:,0]+front*blend[:,1]+top*blend[:,2]
    # Legs/face have shorter directional hairs rather than long body strands.
    leg=smooth(.9,.55,p[:,2])*(1-weight['body'])*(1-weight['neck'])
    short=sample(p[:,0]/.12,-p[:,2]/.12)
    hair=hair*(1-leg)+short*leg
    if settings.get('fresh_model',False):
        head_hair=sample((p[:,1]*.6+p[:,2]*.8)/.14,(p[:,1]*.8-p[:,2]*.6)/.14)
        hw=smooth(.1,.8,weight['head']);hair=hair*(1-hw)+head_hair*hw
    colour*=np.clip(1+hair[:,None]*2.2,.72,1.28);height=hair*.000045
    def apply(name,mask,rgb,roughness=None):
        nonlocal colour,rough
        mask=np.clip(mask,0,1).astype(np.float32);masks[name]=mask
        target=np.asarray(rgb,np.float32)
        colour=colour*(1-mask[:,None])+target*mask[:,None]
        if roughness is not None:rough=rough*(1-mask)+roughness*mask
    body=smooth(.1,.75,weight['body'])
    belly=smooth(1.02,.84,p[:,2])*smooth(.2,-.5,n[:,2])*body
    pale=np.array(pale_colour)*(1+hair[:,None]*1.1)
    apply('belly',belly,pale)
    dorsal=smooth(1.04,1.22,p[:,2])*body*smooth(.25,.75,n[:,2])*.38
    apply('dorsal',dorsal,np.array([.29,.19,.105])*(1+hair[:,None]))
    # Throat follows the front of the neck, not a fixed world-space strip.
    neck=smooth(.1,.65,weight['neck'])
    throat=neck*smooth(.1,.75,n[:,1])*smooth(.18,.02,np.abs(p[:,0]-settings.get('center_x',-.23)))*.7
    apply('throat',throat,pale)
    face=smooth(.15,.6,weight['head'])
    apply('face',face,np.array(face_colour)*(1+hair[:,None]*1.7))
    if a.preset==1:
        rng=np.random.default_rng(732123);spots=np.zeros(count,np.float32)
        for y,z,ry,rz in zip(rng.uniform(-.75,.48,70),rng.uniform(.87,1.20,70),rng.uniform(.014,.025,70),rng.uniform(.012,.023,70)):
            spots=np.maximum(spots,1-smooth(.5,1.2,((p[:,1]-y)/ry)**2+((p[:,2]-z)/rz)**2))
        apply('coat_spots',spots*body*smooth(.1,.65,np.abs(n[:,0])),pale)
    if a.preset in (5,6):
        phase=p[:,1]/(.125 if a.preset==5 else .14)+.065*np.sin(p[:,2]*12)+.035*(p[:,0]/.18)**2
        stripe=1-smooth(.035,.115,np.abs(np.sin(phase*np.pi)))
        habitat_pattern=body*smooth(.80,.91,p[:,2])*(1-smooth(1.19,1.28,p[:,2]))
        if a.preset==5:habitat_pattern*=smooth(.12,.65,np.abs(n[:,0]))
        target=pale if a.preset==5 else np.array([.15,.105,.067])*(1+hair[:,None])
        apply('forest_stripes',stripe*habitat_pattern,target)
    # Pale muzzle hairs surround a separate bounded rhinarium.
    nose=np.array(land['nose']['point'],dtype=float);nn=np.array(land['nose']['normal'],dtype=float);nn/=np.linalg.norm(nn)
    across=np.cross(nn,[0,0,1]);across/=np.linalg.norm(across);up=np.cross(across,nn)
    delta=p-nose;u=delta@across;v=delta@up;depth=delta@nn
    muzzle=np.exp(-((u/.085)**2+(v/.065)**2+(depth/.105)**2)*1.5)*face
    apply('muzzle_fur',muzzle*.9,np.array([.66,.61,.51])*(1+hair[:,None]))
    eye_mid=(np.array(land['eye_right']['point'])+np.array(land['eye_left']['point']))*.5
    snout=nose-eye_mid;snout_length=np.linalg.norm(snout);snout/=snout_length
    snout_across=np.cross(snout,[0,0,1]);snout_across/=np.linalg.norm(snout_across)
    snout_up=np.cross(snout_across,snout);sp=p-eye_mid
    along=sp@snout/snout_length
    bridge=np.exp(-((sp@snout_across)/.038)**2)*smooth(-.25,.05,along)*(1-smooth(.85,1.1,along))*smooth(.0,.035,sp@snout_up)*face
    apply('nose_bridge',bridge*.72,np.array([.24,.20,.16])*(1+hair[:,None]*1.5))
    for name in ('eye_right','eye_left'):
        eye=np.array(land[name]['point']);normal=np.array(land[name]['normal']);normal/=np.linalg.norm(normal)
        horizontal=np.cross([0,0,1],normal);horizontal/=np.linalg.norm(horizontal)
        vertical=np.cross(normal,horizontal);dp=p-eye
        ex=dp@horizontal;ey=dp@vertical;ez=dp@normal
        eye_rx=settings.get('eye_rx',.027);eye_ry=settings.get('eye_ry',.010)
        q=(ex/eye_rx)**2+(ey/eye_ry)**2
        gate=(np.abs(ez)<.025)*smooth(.0,.4,n@normal)
        surround=(1-smooth(1.6,7.0,q))*gate
        apply(name+'_surround',surround*.55,np.array([.245,.185,.125])*(1+hair[:,None]))
        brow=np.exp(-((ex/.039)**2+((ey-.019)/.009)**2))*gate
        apply(name+'_brow',brow*.65,np.array([.60,.50,.36])*(1+hair[:,None]*1.4))
        half_height=eye_ry*np.maximum(0,1-(np.abs(ex)/eye_rx)**1.5)**.7
        almond=np.maximum(np.abs(ex)/eye_rx,np.abs(ey)/np.maximum(half_height,.0001))
        rim=(1-smooth(1.0,1.13 if settings.get('fresh_model',False) else 1.4,almond))*gate
        apply(name+'_rim',rim,[.11,.075,.045],.48)
        if settings.get('fresh_model',False):
            fold=np.exp(-((np.sqrt(q)-1.55)/.22)**2)*gate*smooth(-.006,.006,ey)
            height+=fold*.00010
        eye_mask=(1-smooth(.85,1.05,almond))*gate
        iris_r=np.sqrt((ex/.0105)**2+(ey/.0105)**2)
        angle=np.arctan2(ey,ex)
        streak=.5+.5*np.sin(angle*53+np.sin(angle*17)*2+iris_r*13)
        iris=np.tile([.16,.092,.036],(count,1))*(.7+.35*streak[:,None])
        iris*=np.clip(1.25-iris_r[:,None]*.4,.5,1)
        iris_ring=(1-smooth(.72,1.0,iris_r))[:,None]
        eye_colour=np.array([.045,.032,.024])*(1-iris_ring)+iris*iris_ring
        pupil=1-smooth(.8,1.1,(ex/.0085)**2+(ey/.0027)**2)
        eye_colour=eye_colour*(1-pupil[:,None])+np.array([.009,.010,.008])*pupil[:,None]
        if not settings.get('separate_eyes',False):apply(name,eye_mask,eye_colour,.10)
        else:masks[name]=eye_mask
        height*=1-eye_mask
    nose_width=np.clip(.046+v*.35,.032,.058)
    nq=(u/nose_width)**2+(v/settings.get('nose_ry',.032))**2+((depth+.018)/.052)**2
    nose_mask=(1-smooth(.85,1.18,nq))*face
    if settings.get('rhinarium_revision',0)>=2:
        # A small rounded trapezoid across the muzzle tip, not an ellipsoid
        # stretching up the sloping bridge. Its depth gate wraps the nostrils.
        nose_width=np.clip(.028+v*.16,.024,.031)
        nq=(np.abs(u)/nose_width)**4+(np.abs(v)/.0155)**4
        nose_mask=(1-smooth(.90,1.18,nq))*smooth(-.060,-.035,depth)*face
    pores=np.sin(p[:,0]*6300)*np.sin(p[:,2]*5300+p[:,1]*1200)
    nose_colour=np.array([.075,.060,.047] if settings.get('rhinarium_revision',0)>=2 else [.10,.085,.070])*(1+pores[:,None]*.08)
    apply('nose',nose_mask,nose_colour,.55);height=height*(1-nose_mask)+pores*.00006*nose_mask
    for sign in (-1,1):
        revision=settings.get('rhinarium_revision',0)>=2
        nu=(u-sign*(.0185 if revision else .028));nv=v-(.002 if revision else .001)+sign*nu*.35
        nostril=(1-smooth(.55,1.1,(nu/(.0085 if revision else .013))**2+(nv/(.0038 if revision else .006))**2))*nose_mask
        apply('nostril_'+str(sign),nostril,[.018,.015,.013],.35)
        if revision:height-=nostril*.00018
    lip=np.array(land['lip']['point']);ld=p-lip
    lip_mask=(1-smooth(.8,1.2,((ld@across)/.058)**2+((ld@up)/.0028)**2+((ld@nn)/.042)**2))*face
    apply('lip',lip_mask,[.09,.065,.045],.42)
    # Narrow dark border and sparse warm-grey inner ear. The mask is constrained
    # to its ear's skin influence and to the forward-facing surface.
    for side_name in ('L','R'):
        ew=weight['ear_'+side_name];root_ear=np.array(anchors['Ear_'+side_name]['head'])
        if 'region_weights' in d:points=d['vertices'][d['region_weights'][:,channels.index('ear_'+side_name)]>.35]
        else:points=d['vertices'][d['weights'][:,list(d['bone_names']).index('Ear_'+side_name)]>.35]
        frame=settings.get('ear_frames',{}).get(side_name)
        if frame:root_ear=np.array(frame['root'])
        axis=(np.array(frame['tip']) if frame else points[np.argmax(points[:,2])])-root_ear;length=np.linalg.norm(axis);axis/=length
        lateral=np.cross(axis,[0,1,0]);lateral/=np.linalg.norm(lateral)
        dd=p-root_ear;t=dd@axis;ee=dd@lateral
        radius=max(np.quantile(np.abs((points-root_ear)@lateral),.96),.025)
        eq=((t-length*.50)/(length*.43))**2+(ee/(radius*.68))**2
        inner=(1-smooth(.85,1.25,eq))*smooth(.15,.6,ew)*smooth(-.05,.35,n[:,1])
        inside=np.array([.49,.405,.34])*(1+short[:,None]*1.2)
        if frame:
            fraction=np.clip(t/length,0,1)
            width=.061*np.sin(np.pi*fraction)**.72+.022*(1-fraction)**4
            across=ee/np.maximum(width,.003)
            gate=smooth(.08,.3,fraction)*(1-smooth(.88,.98,fraction))*smooth(.15,.65,ew)*smooth(.02,.55,n[:,1])
            inner=(1-smooth(.60,.84,np.abs(across)))*gate
            ear_hair=sample(ee/.065,-t/.11)
            hair_edge=smooth(.10,.65,np.abs(across))
            tip_hair=smooth(.55,.88,fraction)
            hair_cover=np.maximum(hair_edge,tip_hair)
            inside=np.array([.47,.365,.31])*(1-hair_cover[:,None])+np.array([.66,.60,.52])*hair_cover[:,None]
            inside*=(1+ear_hair[:,None]*2.5)*(0.91+.09*fraction[:,None])
            edge=smooth(.82,.97,np.abs(across))*gate
            apply('ear_edge_'+side_name,edge*.7,np.array(body_colour)*.65*(1+ear_hair[:,None]))
        apply('inner_ear_'+side_name,inner,inside,.9)
    tail=smooth(.15,.65,weight['tail'])
    if settings.get('fresh_model',False):
        tail=smooth(-.63,-.68,p[:,1])*smooth(.065,.035,np.abs(p[:,0]))*smooth(1.075,.99,p[:,2])*smooth(.80,.86,p[:,2])
    apply('tail_under',tail*smooth(.3,-.5,n[:,2]),pale)
    if settings.get('fresh_model',False) and a.preset in (2,4):
        tip=tail*smooth(.96,.90,p[:,2])
        apply('tail_tip',tip,np.array([.12,.095,.065])*(1+hair[:,None]))
    for name in channels[6:]:
        hoof_head=np.array(anchors[name]['head']);hw=weight[name]
        hm=smooth(.25,.65,hw)*smooth(hoof_head[2]-.025,hoof_head[2]-.045,p[:,2])
        groove=np.sin(p[:,0]*1750+p[:,1]*600)*.025
        hoof_colour=np.array([.23,.21,.185])*(1+groove[:,None])
        apply(name,hm,hoof_colour,.43);height*=1-hm
    colour=np.clip(colour,0,1)
    # Conservative tangent detail from metric-scaled height derivatives. Both
    # seams and unoccupied texels receive neutral detail normals.
    colour_image=np.zeros((size,size,3),np.float32);colour_image[iy,ix]=colour
    rough_image=np.full((size,size),.8,np.float32);rough_image[iy,ix]=rough
    hmap=np.zeros((size,size),np.float32);hmap[iy,ix]=height
    hx=np.gradient(hmap,axis=1);hy=np.gradient(hmap,axis=0)
    px=np.gradient(maps[:,:,:3],axis=1);py=np.gradient(maps[:,:,:3],axis=0)
    nx=-hx/np.maximum(np.linalg.norm(px,axis=2),.0001)
    ny=hy/np.maximum(np.linalg.norm(py,axis=2),.0001)
    live=binary_erosion(coverage,iterations=2)
    normal=np.dstack([np.clip(nx,-.12,.12)*live,np.clip(ny,-.12,.12)*live,np.ones_like(nx)])
    normal/=np.linalg.norm(normal,axis=2,keepdims=True)
    # The accepted normal bake contains geometric high-to-low detail, not old
    # coat colour. Preserve it and add only restrained fur relief.
    base_path=root/'geometry-normal.png' if settings.get('fresh_model',False) else Path(__file__).resolve().parent.parent/'artifacts/deer-realvis-20261023/deer-normal.png'
    base=np.asarray(Image.open(base_path).convert('RGB').resize((size,size),Image.Resampling.BILINEAR),np.float32)/127.5-1 if base_path.exists() else np.dstack([np.zeros_like(nx),np.zeros_like(nx),np.ones_like(nx)])
    base/=np.maximum(np.linalg.norm(base,axis=2,keepdims=True),1e-6)
    normal=np.dstack([base[:,:,0]+normal[:,:,0],base[:,:,1]+normal[:,:,1],base[:,:,2]*normal[:,:,2]])
    normal/=np.maximum(np.linalg.norm(normal,axis=2,keepdims=True),1e-6)
    distance,nearest=distance_transform_edt(~coverage,return_indices=True)
    pad=(~coverage)&(distance<=16)
    colour_image[pad]=colour_image[nearest[0][pad],nearest[1][pad]]
    rough_image[pad]=rough_image[nearest[0][pad],nearest[1][pad]]
    # Save all maps before advancing the live-view marker.
    for name,image in [('coat-colour.png',colour_image),('coat-roughness.png',rough_image),('coat-normal.png',normal*.5+.5)]:
        path=root/name;tmp=root/(path.stem+'-writing.png')
        Image.fromarray(np.uint8(np.clip(image,0,1)*255+.5)).save(tmp);tmp.replace(path)
    # A readable UV region atlas is evidence, not the runtime material.
    label=np.zeros((size,size,3),np.uint8)
    for name,c in [('eye_right',(40,180,255)),('eye_left',(40,180,255)),('nose',(255,65,90)),('lip',(170,30,60)),
                   ('inner_ear_L',(200,120,180)),('inner_ear_R',(200,120,180))]:
        ids=masks[name]>.5;label[iy[ids],ix[ids]]=c
    Image.fromarray(label).save(root/'facial-mask-atlas.png')
    report=dict(seconds=time.perf_counter()-start,texture_size=size,occupied_pixels=int(coverage.sum()),
        coverage_fraction=float(coverage.mean()),face_anchors=land,
        mask_pixels={name:int((m>.5).sum()) for name,m in masks.items()},
        fur_source_sha256=hashlib.sha256((root/'fur-source.png').read_bytes()).hexdigest(),
        old_colour_pixels_used=False,geometry_changed=False,local_gpu_inference=False,
        method='Generated reusable fur detail with geometry-anchored anatomical colour/roughness masks')
    report['preset']=a.preset
    (root/'bake-report.json').write_text(json.dumps(report,indent=2))
    marker=root/'material-ready.json';tmp=root/'material-ready.tmp';tmp.write_text(json.dumps(report));tmp.replace(marker)
    progress('First coat bake ready - checking face and seams')
    print('Baked masked deer coat.',flush=True)


if __name__=='__main__':main()
