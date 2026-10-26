"""Bake an inspected AI paint guide into EXISTING UVs, not a replacement mesh.

Guide feature coordinates are explicitly aligned to actual native camera picks.
Only visible front head skin is painted; unseen surfaces and all motion remain.
"""
import argparse,hashlib,importlib.util,json,shutil,sys
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.interpolate import RBFInterpolator
from scipy.ndimage import map_coordinates,distance_transform_edt
repo=Path(__file__).resolve().parent.parent
sys.path.insert(0,str(repo/'scripts'));from fauna_paint_recipes import profiles
ap=argparse.ArgumentParser();ap.add_argument('--slug',choices=['raccoon','red-fox','wolf','hare','rabbit','buffalo'],required=True);ap.add_argument('--root',type=Path);a=ap.parse_args()
root=(a.root or repo/'artifacts/fauna-visual-corrections'/a.slug).resolve()
land=json.loads((root/('projection-landmarks.json' if (root/'projection-landmarks.json').exists() else 'face-landmarks.json')).read_text());guide=json.loads((root/'eye-audit/front.json').read_text());bake=json.loads((root/'coat-bake.json').read_text())
paint_path=root/'front-paint-guide.png';paint=np.asarray(Image.open(paint_path).convert('RGB'),np.float32)/255
anatomy_path=root/('projection-anatomy.npz' if (root/'projection-anatomy.npz').exists() else 'paint-anatomy.npz');d=dict(np.load(anatomy_path));has_source_uv='source_uv' in d
if has_source_uv:
    indices=d['faces'];d['vertices']=d['vertices'][indices].reshape(-1,3);d['normals']=d['normals'][indices].reshape(-1,3);d['faces']=np.arange(len(d['vertices'])).reshape(-1,3);old_uv=d['source_uv'].reshape(-1,2);d['eye_regions']=np.c_[old_uv,np.zeros(len(old_uv))].astype(np.float32)
d['region_weights']=np.zeros((len(d['vertices']),10),np.float32)
spec=importlib.util.spec_from_file_location('raster',repo/'scripts/bake-deer-masked-coat.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
size=bake['size'];maps,occupied,_=module.raster(d,size);iy,ix=np.where(occupied);p=maps[iy,ix,:3];n=maps[iy,ix,3:6];n/=np.maximum(np.linalg.norm(n,axis=1,keepdims=True),1e-8)
M=np.array(guide['camera_matrix']);R=M[:3,:3];position=M[:3,3];scale=guide['scale'];W,H=guide['size']
def project(points):
    camera=(points-position)@R
    return np.stack([(.5+camera[:,0]/scale)*W,(.5-camera[:,1]/scale)*H,-camera[:,2]],axis=1)
screen=project(p);vscreen=project(d['vertices']);depth=np.full((H,W),np.inf,np.float32)
for face in d['faces']:
    tri=vscreen[face];lo=np.maximum(np.floor(tri[:,:2].min(0)).astype(int),0);hi=np.minimum(np.ceil(tri[:,:2].max(0)).astype(int),[W-1,H-1])
    if np.any(lo>hi):continue
    yy,xx=np.mgrid[lo[1]:hi[1]+1,lo[0]:hi[0]+1];A,B,C=tri;den=(B[1]-C[1])*(A[0]-C[0])+(C[0]-B[0])*(A[1]-C[1])
    if abs(den)<1e-9:continue
    w0=((B[1]-C[1])*(xx-C[0])+(C[0]-B[0])*(yy-C[1]))/den;w1=((C[1]-A[1])*(xx-C[0])+(A[0]-C[0])*(yy-C[1]))/den;w2=1-w0-w1
    inside=(w0>=0)&(w1>=0)&(w2>=0);z=w0*A[2]+w1*B[2]+w2*C[2]
    old=depth[yy[inside],xx[inside]];depth[yy[inside],xx[inside]]=np.minimum(old,z[inside])
target=np.array([[193,250],[333,250],[254,335],[260,376],[130,150],[400,145],[265,156],[93,310],[440,310],[0,0],[512,0],[0,512],[512,512]],float)
source=np.array([[510,686],[829,688],[659,935],[650,1035],[337,375],[1012,370],[668,392],[230,775],[1100,775],[0,0],[1254,0],[0,1254],[1254,1254]],float)
if a.slug in {'red-fox','wolf'}:
    target=np.array([[164 if a.slug=='red-fox' else 165,240 if a.slug=='red-fox' else 239],[337 if a.slug=='red-fox' else 335,240 if a.slug=='red-fox' else 239],[255,350],[254,386],[130,90],[390,90],[255,150],[90,310],[435,310],[0,0],[512,0],[0,512],[512,512]],float)
    source=np.array([[434,575],[786,575],[609,915],[609,992],[300,220],[960,220],[625,365],[220,760],[1065,759],[0,0],[1254,0],[0,1254],[1254,1254]],float) if a.slug=='red-fox' else np.array([[439,610],[817,610],[638,1050],[639,1145],[300,220],[960,220],[625,365],[220,760],[1065,759],[0,0],[1254,0],[0,1254],[1254,1254]],float)
views_path=root/'face-paint-views.json';views=json.loads(views_path.read_text()) if views_path.exists() else None
if views:
    for view in views:
        cg=json.loads((root/view.get('camera_directory','eye-audit')/f"{view['camera']}.json").read_text());CM=np.array(cg['camera_matrix'])
        def camera_pixel(point):
            q=(np.array(point)-CM[:3,3])@CM[:3,:3];return ((q[:2]/cg['scale']*[1,-1]+.5)*cg['size']).tolist()
        if view['camera']=='front':
            for index,side in enumerate(['right','left']):view['target_pixels'][index]=camera_pixel(land[side+'_eye']['point'])
            view['target_pixels'][2]=camera_pixel(land['nose']['point']);target=np.array(view['target_pixels'],float);source=np.array(view['guide_pixels'],float)
        else:
            view['target_pixels'][0]=camera_pixel(land[view['camera']+'_eye']['point']);view['target_pixels'][1]=camera_pixel(land['nose']['point'])
warp=RBFInterpolator(target/512,source/1254,kernel='thin_plate_spline',smoothing=0)
xy=warp(screen[:,:2]/512)*np.array([paint.shape[1]-1,paint.shape[0]-1]);sampled=np.stack([map_coordinates(paint[:,:,c],[xy[:,1],xy[:,0]],order=1,mode='nearest') for c in range(3)],axis=1)
def smooth(lo,hi,x):
    t=np.clip((x-lo)/(hi-lo),0,1);return t*t*(3-2*t)
front=np.array(land['front_axis']);eye_mid=(np.array(land['right_eye']['point'])+land['left_eye']['point'])/2
at=np.clip(np.rint(screen[:,:2]).astype(int),[0,0],[W-1,H-1]);visible=np.abs(screen[:,2]-depth[at[:,1],at[:,0]])<.022
relative=p-eye_mid;behind=np.maximum(-(relative@front)-.08,0)/.45;below=np.maximum(-relative[:,2]-.12,0)/.38
head=1-smooth(.65,1.,np.sqrt(behind*behind+below*below))
alpha=smooth(.20,.65,n@front)*head*visible
alpha*=((screen[:,0]>0)&(screen[:,0]<W-1)&(screen[:,1]>0)&(screen[:,1]<H-1))
if views:
    from fauna_face_projection import sample_view
    total=np.zeros(len(p));combined=np.zeros_like(p)
    for view in views:
        colour,weight=sample_view(root,view,d['vertices'],d['faces'],p,n);weight*=head;total+=weight;combined+=colour*weight[:,None]
    sampled=combined/np.maximum(total[:,None],1e-8)
    # View cosine chooses BETWEEN paint guides; it is not paint opacity.
    # Treating it as opacity left old hard ear/face masks visible at angles.
    confidence=total/np.maximum(head,1e-8);alpha=head*smooth(.015,.12,confidence)
elif a.slug in {'hare','rabbit'}:raise ValueError('Explicit calibrated front/profile guide mappings required')
nasal_rel=p-np.array(land['nose']['point']);side_axis=np.array(land['side_axis']);nw,nh=land['physical_eye_revision']['nose_radius']
nasal_scale=1.12 if a.slug in {'red-fox','wolf'} else 1.
nasal_height=.65 if a.slug in {'red-fox','wolf'} else 1.
nasal_q=((nasal_rel@side_axis)/(nw*nasal_scale))**2+(nasal_rel[:,2]/(nh*nasal_scale*nasal_height))**2+((nasal_rel@front)/.075)**2
side_nose=(1-smooth(1.05,1.55,nasal_q))
side_nose*=1-smooth(.45,.80,n@front)
if a.slug=='buffalo':side_nose*=1-alpha
if not land['physical_eye_revision'].get('nasal_disc',True):side_nose*=0
original=root/'unprojected-atlases';original.mkdir(exist_ok=True);palettes=profiles(a.slug)
for ci,coat in enumerate(bake['coats']):
    dst=root/coat['file'];backup=original/dst.name
    if not backup.exists():shutil.copy2(dst,backup)
    original_pixels=np.asarray(Image.open(backup).convert('RGB'),np.float32)/255
    if has_source_uv:
        source_uv=maps[iy,ix,-3:-1];sx=np.clip(source_uv[:,0],0,1)*(original_pixels.shape[1]-1);sy=(1-np.clip(source_uv[:,1],0,1))*(original_pixels.shape[0]-1)
        image=np.zeros((size,size,3),np.float32);image[iy,ix]=np.stack([map_coordinates(original_pixels[:,:,c],[sy,sx],order=1,mode='nearest') for c in range(3)],axis=1)
    else:image=original_pixels.copy()
    colour=sampled.copy()
    if a.slug=='raccoon':
        if ci==1:colour*=np.array([1.08,.97,.85])
        elif ci==2:colour*=np.array([.65,.65,.65])
        elif ci==3:colour=colour*.90+.07
    elif ci:
        _,base,pale,_=palettes[ci];base=np.array(base);pale=np.array(pale);reference_base=np.array(palettes[0][1]);reference_pale=np.array(palettes[0][2])
        # Preserve guide hair detail and natural white cheeks; change only fur
        # pigment. Snow/silver/black coats must never retain orange-red faces.
        lum=colour@np.array([.2126,.7152,.0722]);sat=colour.max(1)-colour.min(1)
        white=smooth(.32,.55,lum)*(1-smooth(.08,.24,sat));dark=1-smooth(.08,.20,lum)
        ratio=base/np.maximum(reference_base,.04)
        tinted=colour*ratio
        if a.slug=='red-fox' and ci in {2,3,6}:
            tinted=lum[:,None]*base/max(float(reference_base@np.array([.2126,.7152,.0722])),.1)
        if a.slug in {'hare','rabbit'} and any(x in palettes[ci][0].lower() for x in ['white','snow']):
            tinted=lum[:,None]*base/max(float(reference_base@np.array([.2126,.7152,.0722])),.1)
        pale_fur=colour*pale/np.maximum(reference_pale,.1)
        colour=tinted*(1-white[:,None])+pale_fur*white[:,None]
        colour=colour*(1-dark[:,None])+sampled*dark[:,None]
    image[iy,ix]=image[iy,ix]*(1-alpha[:,None])+colour*alpha[:,None]
    # A front paint guide must not leave the profile of the nasal disc white.
    # Wrap only the small actual nose volume, preserving the painted front.
    nasal_colour=image[iy,ix];nasal_lum=np.maximum(nasal_colour@np.array([.2126,.7152,.0722]),1e-5)
    nasal_colour=nasal_colour*np.minimum(1.,.035/nasal_lum)[:,None]
    image[iy,ix]=image[iy,ix]*(1-side_nose[:,None])+nasal_colour*side_nose[:,None]
    if land.get('anatomical_orbit',{}).get('stitched') and a.slug in {'wolf','red-fox','raccoon'}:
        # Bind-space nostril pigment goes into the actual selected nasal UVs;
        # separate oval rim objects would project across the muzzle in profile.
        span=nw*(.65 if a.slug=='wolf' else .75);u=(nasal_rel@side_axis)/span;v=nasal_rel[:,2]/(nh*.75);nostril=np.zeros(len(p))
        for sign in [-1,1]:
            q=((u-sign*.52)/.24)**2+((v-.02-sign*.35*(u-sign*.52))/.15)**2
            nostril=np.maximum(nostril,1-smooth(.65,1.25,q))
        nostril*=smooth(-.045,-.018,nasal_rel@front)*(1-smooth(.035,.065,nasal_rel@front))
        image[iy,ix]=image[iy,ix]*(1-nostril[:,None])+np.array([.004,.0035,.003])*nostril[:,None]
    distance,nearest=distance_transform_edt(~occupied,return_indices=True);pad=(~occupied)&(distance<=12);image[pad]=image[nearest[0][pad],nearest[1][pad]]
    Image.fromarray(np.uint8(np.clip(image,0,1)*255)).save(dst)
rough_path=root/'coat-roughness.png';rough=np.asarray(Image.open(rough_path).convert('L'),np.float32)/255
nx,ny=target[2];radius=np.array([26.,18.]) if a.slug=='raccoon' else np.array([32.,23.]);nasal=np.exp(-np.sum(((screen[:,:2]-[nx,ny])/radius)**4,axis=1))*alpha
if not land['physical_eye_revision'].get('nasal_disc',True):nasal*=0
nasal=np.maximum(nasal,side_nose)
if not has_source_uv:
    rough[iy,ix]=rough[iy,ix]*(1-nasal)+.47*nasal;Image.fromarray(np.uint8(np.clip(rough,0,1)*255)).save(rough_path)
report={'slug':a.slug,'guide_sha256':hashlib.sha256(paint_path.read_bytes()).hexdigest(),'target_camera':guide,'target_pixels':target.tolist(),'guide_pixels':source.tolist(),'painted_pixels':int((alpha>.5).sum()),'method':'Visible front head projection of generated paint into original UVs, explicit landmark warp and smooth normal/depth-gated blend; original non-front atlases retained.','limits':'Single front guide only; profile join, fur direction, baked shading and all variants require actual native inspection. Not a replacement render or approved appearance.'}
if views:report.update(method='Calibrated generated front/profile guides fused by surface visibility and view normal into original UVs; mirrored profile guide fitted independently to the other side.',view_configurations=views,limits='Generated paint can retain lighting and fine-fibre stretching; native front/profile/coat inspection and owner review required.')
(root/'face-projection.json').write_text(json.dumps(report,indent=2)+'\n');print('FACE GUIDE BAKED',a.slug,report['painted_pixels'])
