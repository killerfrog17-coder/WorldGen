"""Explicit, reference-directed local head corrections; never edits motion."""
import numpy as np

def apply(body,land,slug):
    if slug not in {'wolf','hare','rabbit'}:return None
    original=np.array([v.co[:] for v in body.data.vertices]);p=original.copy();F=np.array(land['front_axis']);S=np.array(land['side_axis']);nose=np.array(land['nose']['point']);mid=(np.array(land['right_eye']['point'])+land['left_eye']['point'])/2
    def smooth(t):t=np.clip(t,0,1);return t*t*(3-2*t)
    if slug in {'hare','rabbit'}:
        rel=p-mid;f=rel@F;z=rel[:,2];side=rel@S
        head=smooth((f+.34)/.18)*smooth((z+.32)/.10)*smooth((-.02-z)/.065)
        p-=S[None,:]*(side*(.30 if slug=='hare' else .18)*head)[:,None]
        p[:,2]+=(.045 if slug=='hare' else .025)*head*smooth((-.19-z)/.065)
        if slug=='rabbit':p-=F[None,:]*(np.minimum(.068,np.maximum(f-.015,0)*.30)*head)[:,None]
        delta=p-original
        for vertex,point in zip(body.data.vertices,p):vertex.co=point
        if body.data.shape_keys:
            for key in body.data.shape_keys.key_blocks:
                for vertex,shift in zip(key.data,delta):vertex.co=np.array(vertex.co)+shift
        body.data.update();ids=np.where(np.linalg.norm(delta,axis=1)>1e-7)[0]
        return {'method':'Front/profile reference-directed lower-cheek taper, softened jaw depth and rabbit-only bounded muzzle shortening; lateral eye centres retained. Identical displacement across original morph keys.','vertices':ids.tolist(),'positions':{str(i):p[i].tolist() for i in ids},'max_displacement':float(np.linalg.norm(delta,axis=1).max()),'appearance_approved':False}
    rel=p-nose;f=rel@F;z=rel[:,2];side=rel@S
    jaw=smooth((f+.43)/.16)*smooth((-.008-z)/.065)*smooth((z+.24)/.08)
    jaw*=smooth((-.03-f)/.07)
    p[:,2]+=.025*jaw;p-=S[None,:]*(side*.09*jaw)[:,None]
    neighbours=[set() for _ in p];groups={}
    for i,point in enumerate(np.round(original,6)):groups.setdefault(tuple(point),[]).append(i)
    for face in body.data.polygons:
        for i in face.vertices:neighbours[i].update(int(j) for j in face.vertices if j!=i)
    for ids in groups.values():
        connected=set().union(*(neighbours[i] for i in ids))
        for i in ids:neighbours[i]=connected-set(ids)
    weight=smooth((f+.49)/.12)*smooth((-.06-f)/.12)*smooth((p[:,2]-(mid[2]-.27))/.10)*smooth((mid[2]+.08-p[:,2])/.09)
    for key in ['right_eye','left_eye']:
        anchor=land[key];N,H,V=[np.array(anchor['fit_frame'][k]) for k in ['normal','horizontal','vertical']];d=original-np.array(anchor['point']);q=np.sqrt((d@H/.078)**2+(d@V/.064)**2);weight*=smooth((q-1)/.55)
    for _ in range(5):
        q=p.copy()
        for i in np.where(weight>0)[0]:
            if neighbours[i]:q[i]=p[i]+(p[list(neighbours[i])].mean(0)-p[i])*.20*weight[i]
        p=q
    delta=p-original;length=np.linalg.norm(delta,axis=1);delta*=np.minimum(1,.068/np.maximum(length,1e-12))[:,None];p=original+delta
    for vertex,point in zip(body.data.vertices,p):vertex.co=point
    if body.data.shape_keys:
        for key in body.data.shape_keys.key_blocks:
            for vertex,shift in zip(key.data,delta):vertex.co=np.array(vertex.co)+shift
    body.data.update()
    return {'method':'Reference-directed jaw lift/narrowing and bounded cheek-seam relaxation. Original source reset on every build; all shape keys receive identical displacement.','vertices':np.where(length>1e-7)[0].tolist(),'positions':{str(i):p[i].tolist() for i in np.where(length>1e-7)[0]},'max_displacement':float(np.linalg.norm(delta,axis=1).max()),'appearance_approved':False}
