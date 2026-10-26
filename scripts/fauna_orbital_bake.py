"""Bake continuous orbital fur atlases across disconnected original body UVs.

Samples the retained body pigment per texel, avoiding coarse vertex-colour lids
and interpolation between unrelated body UV islands. No lighting is baked.
"""
import numpy as np

def bake(patches,coats,atlases,support_sample,load_image,root=None,slug=None):
    W,H=256,128;positions=np.zeros((H,W,3));occupied=np.zeros((H,W),bool);source_uv=np.zeros((H,W,2));clone_uv=np.zeros((H,W,2));clone_weight=np.zeros((H,W))
    for record in patches:
        ob=record['object'];mesh=ob.data;mesh.calc_loop_triangles();points=np.array([v.co[:] for v in mesh.vertices]);uv=mesh.uv_layers['Orbital bake UV'];clone_attr=mesh.attributes.get('Fitted clone blend');weights=np.array([v.value for v in clone_attr.data]) if clone_attr else None
        for face in mesh.loop_triangles:
            triangle=points[list(face.vertices)];xy=np.array([uv.data[i].uv[:] for i in face.loops])*[W,H]-.5
            lo=np.maximum(np.ceil(xy.min(0)).astype(int),0);hi=np.minimum(np.floor(xy.max(0)).astype(int),[W-1,H-1])
            if np.any(lo>hi):continue
            A,B,C=xy;den=(B[1]-C[1])*(A[0]-C[0])+(C[0]-B[0])*(A[1]-C[1])
            if abs(den)<1e-10:continue
            yy,xx=np.mgrid[lo[1]:hi[1]+1,lo[0]:hi[0]+1];a=((B[1]-C[1])*(xx-C[0])+(C[0]-B[0])*(yy-C[1]))/den;b=((C[1]-A[1])*(xx-C[0])+(A[0]-C[0])*(yy-C[1]))/den;c=1-a-b
            inside=(a>=-1e-6)&(b>=-1e-6)&(c>=-1e-6);y,x=yy[inside],xx[inside];p=a[inside,None]*triangle[0]+b[inside,None]*triangle[1]+c[inside,None]*triangle[2]
            positions[y,x]=p;occupied[y,x]=True
            if weights is not None:clone_weight[y,x]=a[inside]*weights[face.vertices[0]]+b[inside]*weights[face.vertices[1]]+c[inside]*weights[face.vertices[2]]
        # Each eye occupies one half; query body UV per texel, not per vertex.
        half=record['half'];ys,xs=np.where(occupied & (np.indices(occupied.shape)[1]//128==half))
        for y,x in zip(ys,xs):
            rel=positions[y,x]-record['center'];u=float(rel@record['horizontal']);v=float(rel@record['vertical']);rho=np.sqrt((u/record['rx'])**2+(v/(record['radius']*.60))**2)
            target=record['center']+record['horizontal']*u+record['vertical']*v
            source_point=positions[y,x] if weights is not None else target
            minimum=record.get('source_fur_radius',0.)
            if minimum and rho<minimum:
                # Paint guides deliberately contain grey placeholder eyes.
                # Do not clone that removed eye pigment onto the new fur lid.
                # The narrow bare margin is a separate shader attribute.
                factor=minimum/max(rho,.05);source_point=record['center']+record['horizontal']*u*factor+record['vertical']*v*factor
            source_uv[y,x]=support_sample(source_point)[1]
            # Clone nearby upper/lower fur at its original scale. Collapsing
            # inner rings onto one sampled radius creates radial starbursts.
            direction=-1 if 'raccoon' in ob.name else 1
            clone_uv[y,x]=support_sample(target+record['vertical']*record['radius']*1.7*direction)[1]
            if weights is None:
                t=np.clip((rho-1.15)/1.15,0,1);clone_weight[y,x]=1-t*t*(3-2*t)
    ys,xs=np.where(occupied);uv=np.mod(source_uv[ys,xs],1);other_uv=np.mod(clone_uv[ys,xs],1);blend=clone_weight[ys,xs,None]
    direct=None;base_values=None
    import json
    direct_config=json.loads((root/'face-landmarks.json').read_text()).get('anatomical_orbit',{}) if root is not None else {}
    if root is not None and (slug in {'lion','tiger','jaguar','leopard','puma','ocelot'} or direct_config.get('direct_registered_fur')):
        # New feline lids have no original UV surface. Sample their registered
        # pigmentation directly rather than jumping between nearest coarse
        # skull triangles, which produced visible radial colour wedges.
        import hashlib
        query=positions[ys,xs].copy();normals=np.zeros_like(query)
        for record in patches:
            mask=(xs//128)==record['half'];normals[mask]=record['normal']
        import json
        dependency=hashlib.sha256((root/'face-paint-views.json').read_bytes());dependency.update((root/'face-landmarks.json').read_bytes())
        for config in json.loads((root/'face-paint-views.json').read_text()):
            dependency.update((root/config['image']).read_bytes());dependency.update((root/config.get('camera_directory','eye-audit')/(config['camera']+'.json')).read_bytes())
        signature=hashlib.sha256(query.tobytes()+normals.tobytes()+dependency.digest()).hexdigest()
        np.savez_compressed(root/'orbital-projection-input.npz',points=query,normals=normals,signature=signature,ys=ys,xs=xs)
        cache=root/'orbital-projection-colour.npz'
        if cache.exists():
            result=np.load(cache)
            if str(result['signature'])==signature:direct=result['colour']
    for coat,atlas in zip(coats,atlases):
        image=load_image(coat['file']);pixels=np.empty(len(image.pixels),np.float32);image.pixels.foreach_get(pixels);pixels=pixels.reshape(image.size[1],image.size[0],4)
        def sample(coords):
            xy=coords*[image.size[0]-1,image.size[1]-1];lo=xy.astype(int);hi=np.minimum(lo+1,[image.size[0]-1,image.size[1]-1]);t=xy-lo
            return pixels[lo[:,1],lo[:,0]]*(1-t[:,0:1])*(1-t[:,1:2])+pixels[lo[:,1],hi[:,0]]*t[:,0:1]*(1-t[:,1:2])+pixels[hi[:,1],lo[:,0]]*(1-t[:,0:1])*t[:,1:2]+pixels[hi[:,1],hi[:,0]]*t[:,0:1]*t[:,1:2]
        values=sample(uv)*(1-blend)+sample(other_uv)*blend
        if direct is not None:
            if base_values is None:base_values=values[:,:3].copy()
            ratio=np.clip(values[:,:3]/np.maximum(base_values,.018),.16,3.)
            valid=np.isfinite(direct).all(1);values[valid,:3]=direct[valid]*ratio[valid]
        result=np.ones((H,W,4),np.float32);result[:,:,:3]=0;result[ys,xs]=values;known=occupied.copy()
        for _ in range(9):
            summed=np.zeros_like(result);count=np.zeros((H,W),np.float32)
            for axis,shift in [(0,1),(0,-1),(1,1),(1,-1)]:
                mask=np.roll(known,shift,axis);summed+=np.roll(result,shift,axis)*mask[:,:,None];count+=mask
            fill=(~known)&(count>0);result[fill]=summed[fill]/count[fill,None];known[fill]=True
        result[:,:,3]=1;atlas.pixels.foreach_set(result.ravel());atlas.update();atlas.pack()
    return int(occupied.sum())
