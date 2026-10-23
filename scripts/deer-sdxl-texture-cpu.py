"""Geometry-guided multiview painting controls and visibility-aware UV bake.

Only consumes our unpainted mesh and new SDXL views. Old Hunyuan pixels never
enter this pipeline. Extra head views improve useful atlas detail without 4K.
"""
import argparse
import importlib.util
import json
import math
import sys
from pathlib import Path
import numpy as np
from PIL import Image
from numba import njit


spec=importlib.util.spec_from_file_location('deer_raster',Path(__file__).with_name('deer-texture-cpu.py'))
module=importlib.util.module_from_spec(spec)
sys.modules[spec.name]=module
spec.loader.exec_module(module)
raster=njit(cache=False)(module.raster.py_func)


def project(v,rotation,centre,span,size):
    p=(v-centre)@rotation.T
    screen=np.stack([(p[:,0]/span+.5)*size-.5,(.5-p[:,1]/span)*size-.5],axis=1)
    return screen.astype(np.float32),(-p[:,2]+5).astype(np.float32)


def prepare(args):
    import xatlas
    import trimesh
    data=np.load(args.root/'game-mesh.npz')
    vertices,faces=data['vertices'],data['faces']
    smooth_normals=trimesh.Trimesh(vertices,faces,process=False).vertex_normals.astype(np.float32)
    atlas=xatlas.Atlas()
    atlas.add_mesh(vertices,faces.astype(np.uint32))
    pack=xatlas.PackOptions()
    pack.resolution=args.texture_size
    pack.padding=12
    pack.bruteForce=False
    atlas.generate(pack_options=pack)
    vmap,faces,uv=atlas[0]
    vertices=vertices[vmap]
    weights=data['weights'][vmap]
    np.savez_compressed(args.root/'uv-mesh.npz',vertices=vertices,faces=faces,uv=uv,
        weights=weights,groups=data['groups'])
    normals=smooth_normals[vmap]
    forward=np.array([-.91514678,-.40312079,0])
    right=np.array([.40312079,-.91514678,0])
    centre=(vertices.min(0)+vertices.max(0))/2
    span=max(np.ptp(vertices@right),np.ptp(vertices@forward),np.ptp(vertices[:,2]))*1.12
    group_names=list(data['groups'])
    head_ids=[group_names.index(n) for n in ('Head','Ear_L','Ear_R','Jaw','Muzzle_R','Muzzle_L')]
    head_weight=weights[:,head_ids].sum(1)
    head_points=vertices[head_weight>.5]
    head_centre=(head_points.min(0)+head_points.max(0))/2
    head_span=float(max(np.ptp(head_points,axis=0))*1.3)
    # A neutral anatomical colour guide, not an old generated texture. Shape
    # control comes from exact rendered depth, not estimated photo depth.
    colours=np.tile(np.array([.49,.32,.19]),(len(vertices),1))
    belly=np.clip((-normals[:,2]-.15)*1.5,0,1)*(vertices[:,2]>.55)*(vertices[:,2]<1.1)
    colours=colours*(1-belly[:,None]*.65)+np.array([.73,.64,.50])*belly[:,None]*.65
    hoof=np.clip((.135-vertices[:,2])/.06,0,1)
    colours=colours*(1-hoof[:,None])+np.array([.12,.10,.085])*hoof[:,None]
    # Very soft geometry shading supplies volume to img2img but is not baked
    # as the final material; the model is asked for diffuse/albedo-like colour.
    rotations=[]
    centres=[]
    spans=[]
    depths=[]
    masks=[]
    specs=[]
    angles=[(0,0),(180,0),(90,0),(270,0),(45,15),(135,15),(225,15),(315,15),(0,65),(180,-55)]
    angles += [(0,10),(180,10),(90,0)]
    for i,(azimuth,elevation) in enumerate(angles):
        a,e=math.radians(azimuth),math.radians(elevation)
        axis=(right*math.cos(a)+forward*math.sin(a))*math.cos(e)+np.array([0,0,math.sin(e)])
        screen_right=np.cross(np.array([0,0,1.]),axis)
        screen_right/=np.linalg.norm(screen_right)
        up=np.cross(axis,screen_right)
        rotation=np.stack([screen_right,up,axis])
        is_head=i>=10
        c=head_centre if is_head else centre
        s=head_span if is_head else span
        xy,depth=project(vertices,rotation,c,s,args.size)
        maps,zbuffer,mask=raster(xy,depth,faces,np.concatenate([vertices,normals,colours],axis=1),args.size)
        valid=mask>0
        normalized_depth=np.zeros((args.size,args.size),np.float32)
        near,far=zbuffer[valid].min(),zbuffer[valid].max()
        normalized_depth[valid]=.25+.7*(far-zbuffer[valid])/max(float(far-near),1e-5)
        Image.fromarray(np.uint8(normalized_depth*255)).convert('RGB').save(args.root/f'depth-{i}.png')
        light=np.clip(maps[:,:,3:6]@axis,.15,1)
        initial=np.ones((args.size,args.size,3),np.float32)*.87
        initial[valid]=maps[:,:,6:9][valid]*(.8+.2*light[valid,None])
        Image.fromarray(np.uint8(np.clip(initial,0,1)*255)).save(args.root/f'guide-{i}.png')
        Image.fromarray(mask*255).save(args.root/f'mask-{i}.png')
        rotations.append(rotation)
        centres.append(c)
        spans.append(s)
        depths.append(zbuffer)
        masks.append(mask)
        title=('close-up head and ears' if is_head else 'whole body')
        view=('front' if azimuth==90 else 'rear' if azimuth==270 else 'side profile')
        specs.append(dict(index=i,azimuth=azimuth,elevation=elevation,head=is_head,
            description=f'{title}, {view} view'))
        print('Control',i,title,flush=True)
    np.savez_compressed(args.root/'view-controls.npz',vertices=vertices,normals=normals,faces=faces,uv=uv,
        rotations=rotations,centres=centres,spans=spans,depths=depths,masks=masks,size=args.size,
        head_weight=head_weight)
    (args.root/'views.json').write_text(json.dumps(specs,indent=2))


def bake(args):
    from scipy.ndimage import distance_transform_edt,gaussian_filter,binary_erosion
    from scipy.spatial import cKDTree
    data=np.load(args.root/'view-controls.npz')
    v,normals,faces,uv=[data[n] for n in ('vertices','normals','faces','uv')]
    size=args.texture_size
    uvscreen=np.stack([uv[:,0]*size-.5,(1-uv[:,1])*size-.5],axis=1).astype(np.float32)
    maps,_,mask=raster(uvscreen,np.zeros(len(v),np.float32),faces,
        np.concatenate([v,normals,data['head_weight'][:,None]],axis=1),size)
    yy,xx=np.where(mask)
    points,norms,head=maps[yy,xx,:3],maps[yy,xx,3:6],maps[yy,xx,6]
    norms/=np.maximum(np.linalg.norm(norms,axis=1,keepdims=True),1e-8)
    best=np.zeros(len(points),np.float32)
    best_colours=np.zeros_like(points)
    best_low_colours=np.zeros_like(points)
    total=np.zeros_like(points)
    low_samples=[]
    broad_weights=[]
    sums=np.zeros(len(points),np.float32)
    image_size=int(data['size'])
    specs=json.loads((args.root/'views.json').read_text())
    used_views=[]
    for i,(rotation,centre,span,depths) in enumerate(zip(data['rotations'],data['centres'],data['spans'],data['depths'])):
        path=args.root/f'albedo-view-{i}.png' if args.delighted else args.root/f'paint-{i}.png'
        if not path.exists():
            continue
        used_views.append(i)
        image=np.asarray(Image.open(path).convert('RGB').resize((image_size,image_size)),np.float32)/255
        # Diffusion follows depth closely but not to the exact pixel silhouette.
        # Reject its plain studio background; otherwise it becomes grey/white
        # streaks on the mesh where the generated outline shifted inward.
        silhouette_image=np.asarray(Image.open(args.root/f'paint-{i}.png').convert('RGB').resize((image_size,image_size)),np.float32)/255
        corners=np.concatenate([silhouette_image[:24,:24].reshape(-1,3),silhouette_image[:24,-24:].reshape(-1,3),
            silhouette_image[-24:,:24].reshape(-1,3),silhouette_image[-24:,-24:].reshape(-1,3)])
        background=np.median(corners,axis=0)
        foreground=np.linalg.norm(silhouette_image-background,axis=2)>.12
        foreground=binary_erosion(foreground,iterations=2)
        xy,depth=project(points,rotation,centre,span,image_size)
        px=np.clip(np.round(xy[:,0]).astype(int),0,image_size-1)
        py=np.clip(np.round(xy[:,1]).astype(int),0,image_size-1)
        visible=(np.abs(depth-depths[py,px])<.012)&(xy[:,0]>=1)&(xy[:,0]<image_size-1)&(xy[:,1]>=1)&(xy[:,1]<image_size-1)
        visible &= foreground[py,px]
        facing=np.clip(norms@rotation[2],0,1)
        weight=np.where(visible&(facing>.2),facing**12,0).astype(np.float32)
        broad=np.where(visible&(facing>.2),facing**2,0).astype(np.float32)
        if specs[i]['head']:
            priority=np.clip((head-.4)*4,0,1)*5
            weight*=priority
            broad*=priority
        colour=image[py,px]
        low_colour=gaussian_filter(image,(4,4,0))[py,px]
        wins=weight>best
        best_colours[wins]=colour[wins]
        best_low_colours[wins]=low_colour[wins]
        best[wins]=weight[wins]
        total+=colour*weight[:,None]
        low_samples.append(low_colour)
        broad_weights.append(broad)
        sums+=weight
    trusted=sums>1e-8
    # Blend broad body colour continuously, but keep fur high frequencies from
    # the best-facing view. This avoids both a hard colour seam and blur from
    # averaging unrelated hair strands. Facial features favour one close view.
    average=total/np.maximum(sums[:,None],1e-8)
    # A broad-facing weighted median rejects a single view's invented white
    # marking or exposure outlier. Fine detail still uses the sharp primary.
    broad_weights=np.stack(broad_weights,axis=1)
    low_samples=np.stack(low_samples,axis=1)
    low_average=np.empty_like(points)
    rows=np.arange(len(points))
    half_weight=broad_weights.sum(1)*.5
    for channel in range(3):
        order=np.argsort(low_samples[:,:,channel],axis=1)
        sorted_weights=np.take_along_axis(broad_weights,order,axis=1)
        at=(np.cumsum(sorted_weights,axis=1)>=half_weight[:,None]).argmax(1)
        selected=order[rows,at]
        low_average[:,channel]=low_samples[rows,selected,channel]
    body=low_average+.8*(best_colours-best_low_colours)+.2*(average-low_average)
    face=best_colours*.8+average*.2
    facial=np.clip((head-.15)*2,0,1)[:,None]
    colours=body*(1-facial)+face*facial
    if not trusted.any():
        raise ValueError('No painted surface')
    if (~trusted).any():
        subset=np.flatnonzero(trusted)[::max(1,int(trusted.sum()/250000))]
        _,nearest=cKDTree(points[subset]).query(points[~trusted],workers=4)
        colours[~trusted]=colours[subset[nearest]]
    image=np.zeros((size,size,3),np.float32)
    image[yy,xx]=colours
    distance,nearest=distance_transform_edt(mask==0,return_indices=True)
    padding=(mask==0)&(distance<=16)
    image[padding]=image[nearest[0][padding],nearest[1][padding]]
    Image.fromarray(np.uint8(np.clip(image,0,1)*255)).save(args.root/'deer-albedo.png')
    (args.root/'bake.json').write_text(json.dumps(dict(texture_size=size,occupied_pixels=len(points),
        painted_coverage=float(trusted.mean()),planned_view_count=len(specs),used_views=used_views,
        blend='Robust broad colour consensus; primary-view fine detail; primary facial view',
        estimated_delighting=args.delighted,old_hunyuan_pixels=False),indent=2))
    print('Baked',float(trusted.mean()),flush=True)


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('stage',choices=['prepare','bake'])
    ap.add_argument('--root',type=Path,required=True)
    ap.add_argument('--size',type=int,default=1024)
    ap.add_argument('--texture-size',type=int,default=2048)
    ap.add_argument('--delighted',action='store_true',help='Use reviewed Marigold albedo views instead of photos')
    args=ap.parse_args()
    (prepare if args.stage=='prepare' else bake)(args)


if __name__=='__main__':
    main()
