"""CPU mesh controls and UV baking for local Hunyuan multiview painting.

No appearance is invented here: prepare renders geometry controls; bake blends
the corresponding independently generated camera views using visibility and
surface facing. It never projects the original photograph over unseen sides.
"""
import argparse
import json
import math
from pathlib import Path
import numpy as np
from numba import njit
from PIL import Image
import trimesh


@njit(cache=True)
def raster(screen, depth, faces, attributes, size):
    buffer = np.full((size,size),1e20,np.float32)
    output = np.zeros((size,size,attributes.shape[1]),np.float32)
    mask = np.zeros((size,size),np.uint8)
    for indices in faces:
        a,b,c = indices
        x0,y0 = screen[a]
        x1,y1 = screen[b]
        x2,y2 = screen[c]
        denominator = (y1-y2)*(x0-x2)+(x2-x1)*(y0-y2)
        if abs(denominator) < 1e-9:
            continue
        for yy in range(max(0,int(math.floor(min(y0,y1,y2)))),min(size-1,int(math.ceil(max(y0,y1,y2))))+1):
            for xx in range(max(0,int(math.floor(min(x0,x1,x2)))),min(size-1,int(math.ceil(max(x0,x1,x2))))+1):
                w0 = ((y1-y2)*(xx-x2)+(x2-x1)*(yy-y2))/denominator
                w1 = ((y2-y0)*(xx-x2)+(x0-x2)*(yy-y2))/denominator
                w2 = 1-w0-w1
                if min(w0,w1,w2) < -1e-5:
                    continue
                distance = w0*depth[a]+w1*depth[b]+w2*depth[c]
                if distance < buffer[yy,xx]:
                    buffer[yy,xx] = distance
                    mask[yy,xx] = 1
                    for j in range(attributes.shape[1]):
                        output[yy,xx,j] = w0*attributes[a,j]+w1*attributes[b,j]+w2*attributes[c,j]
    return output,buffer,mask


def camera(elevation, azimuth):
    elev,azim = math.radians(-elevation),math.radians(azimuth+90)
    eye = np.array([math.cos(elev)*math.cos(azim),math.cos(elev)*math.sin(azim),math.sin(elev)])*1.45
    look = -eye/np.linalg.norm(eye)
    up = np.array([0.,0.,1.])
    right = np.cross(look,up)
    if np.linalg.norm(right) < 1e-6:
        right = np.array([1.,0.,0.])
    right /= np.linalg.norm(right)
    up = np.cross(right,look)
    rotation = np.stack([right,up,-look])
    return rotation,eye


def project(vertices, rotation, eye, size):
    positions = (vertices-eye)@rotation.T
    screen = np.empty((len(vertices),2),np.float32)
    screen[:,0] = (positions[:,0]/1.2+.5)*size-.5
    screen[:,1] = (.5-positions[:,1]/1.2)*size-.5
    return screen,(-positions[:,2]).astype(np.float32)


def prepare(args):
    import xatlas
    target = args.output/'controls.npz'
    if target.exists():
        raise FileExistsError(target)
    args.output.mkdir(parents=True,exist_ok=True)
    mesh = trimesh.load(args.mesh,force='mesh')
    if len(mesh.faces) > 50000:
        mesh = mesh.simplify_quadric_decimation(face_count=50000)
    vmap,faces,uv = xatlas.parametrize(mesh.vertices.astype(np.float32),mesh.faces.astype(np.uint32))
    raw = mesh.vertices[vmap].astype(np.float32)
    faces = faces.astype(np.int32)
    mesh = trimesh.Trimesh(raw,faces,process=False)
    mesh.visual = trimesh.visual.TextureVisuals(uv=uv)
    mesh.export(args.output/'deer-uv.glb')
    # Right-handed, Z-up painting coordinates. Front camera sees the same side
    # as the reference; the original Y-up geometry is preserved in the export.
    vertices = raw[:,[0,2,1]].copy()
    vertices[:,0] *= -1
    centre = (vertices.min(0)+vertices.max(0))/2
    radius = np.linalg.norm(vertices-centre,axis=1).max()
    vertices = (vertices-centre)*(1.15/(2*radius))
    normals = trimesh.Trimesh(vertices,faces,process=False).vertex_normals.astype(np.float32)
    angles = [(0,0),(0,90),(0,180),(0,270),(90,0),(-90,180)]
    matrices,eyes,depths = [],[],[]
    for index,(elev,azim) in enumerate(angles):
        rotation,eye = camera(elev,azim)
        screen,depth = project(vertices,rotation,eye,args.size)
        maps,depth_map,mask = raster(screen,depth,faces,np.concatenate([normals,vertices],axis=1),args.size)
        normal_image = (maps[:,:,:3]+1)*.5
        position_image = .5-maps[:,:,3:]/1.15
        normal_image[mask==0] = 1
        position_image[mask==0] = 1
        Image.fromarray(np.uint8(np.clip(normal_image,0,1)*255)).save(args.output/f'normal-{index}.png')
        Image.fromarray(np.uint8(np.clip(position_image,0,1)*255)).save(args.output/f'position-{index}.png')
        matrices.append(rotation)
        eyes.append(eye)
        depths.append(depth_map)
        print('Geometry view',index,'ready',flush=True)
    np.savez_compressed(target,raw=raw,vertices=vertices,normals=normals,faces=faces,uv=uv,
        rotations=np.array(matrices),eyes=np.array(eyes),depths=np.array(depths),size=args.size)
    (args.output/'controls.json').write_text(json.dumps({'angles':angles,'camera_info':[12,15,18,21,40,38],
        'vertices':len(raw),'faces':len(faces),'size':args.size,'source':str(args.mesh.resolve())},indent=2))


def bake(args):
    from scipy.spatial import cKDTree
    from scipy.ndimage import distance_transform_edt
    data = np.load(args.output/'controls.npz')
    vertices,normals,faces,uv = [data[n] for n in ('vertices','normals','faces','uv')]
    size = args.texture_size
    uv_screen = np.stack([uv[:,0]*size-.5,(1-uv[:,1])*size-.5],axis=1).astype(np.float32)
    maps,_,uv_mask = raster(uv_screen,np.zeros(len(uv),np.float32),faces,np.concatenate([vertices,normals],axis=1),size)
    rows,cols = np.where(uv_mask)
    points = maps[rows,cols,:3]
    norms = maps[rows,cols,3:]
    norms /= np.maximum(np.linalg.norm(norms,axis=1,keepdims=True),1e-8)
    accumulated = np.zeros_like(points)
    weights = np.zeros(len(points),np.float32)
    image_size = int(data['size'])
    for i,(rotation,eye,depths) in enumerate(zip(data['rotations'],data['eyes'],data['depths'])):
        image = np.asarray(Image.open(args.output/f'paint-{i}.png').convert('RGB').resize((image_size,image_size)),np.float32)/255
        xy,depth = project(points,rotation,eye,image_size)
        xx = np.clip(np.round(xy[:,0]).astype(int),0,image_size-1)
        yy = np.clip(np.round(xy[:,1]).astype(int),0,image_size-1)
        visible = (np.abs(depth-depths[yy,xx])<.008)&(xy[:,0]>=0)&(xy[:,0]<image_size)&(xy[:,1]>=0)&(xy[:,1]<image_size)
        facing = np.clip(norms@rotation[2],0,1)
        weight = np.where(visible& (facing>.15),facing**4,0).astype(np.float32)
        accumulated += image[yy,xx]*weight[:,None]
        weights += weight
    trusted = weights>1e-7
    colours = accumulated/np.maximum(weights[:,None],1e-8)
    # Fill rarely visible creases from physically nearby painted surface, not
    # arbitrary neighbours across unrelated UV islands.
    if (~trusted).any() and trusted.any():
        subset = np.flatnonzero(trusted)[::max(1,int(trusted.sum()/250000))]
        tree = cKDTree(points[subset])
        _,nearest = tree.query(points[~trusted],workers=4)
        colours[~trusted] = colours[subset[nearest]]
    result = np.zeros((size,size,3),np.float32)
    result[rows,cols] = colours
    distance,nearest = distance_transform_edt(uv_mask==0,return_indices=True)
    extend = (uv_mask==0)&(distance<=12)
    result[extend] = result[nearest[0][extend],nearest[1][extend]]
    albedo = Image.fromarray(np.uint8(np.clip(result,0,1)*255))
    albedo.save(args.output/'deer-albedo.png')
    mesh = trimesh.Trimesh(data['raw'],faces,process=False)
    material = trimesh.visual.material.PBRMaterial(baseColorTexture=albedo,roughnessFactor=.88,metallicFactor=0)
    mesh.visual = trimesh.visual.TextureVisuals(uv=uv,material=material)
    mesh.export(args.output/'deer-painted.glb')
    report = {'texture_size':size,'uv_pixels':len(points),'direct_multiview_coverage':float(trusted.mean()),
        'unseen_fill':'nearest painted 3D surface','appearance_source':'Local Hunyuan3D multiview diffusion output'}
    (args.output/'bake.json').write_text(json.dumps(report,indent=2))
    print(report,flush=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('stage',choices=['prepare','bake'])
    ap.add_argument('--mesh',type=Path)
    ap.add_argument('--output',type=Path,required=True)
    ap.add_argument('--size',type=int,default=512)
    ap.add_argument('--texture-size',type=int,default=2048)
    args = ap.parse_args()
    (prepare if args.stage=='prepare' else bake)(args)


if __name__=='__main__':
    main()
