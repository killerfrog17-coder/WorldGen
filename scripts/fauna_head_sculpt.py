"""Explicit, reference-directed local head corrections; never edits motion."""
import numpy as np

def apply(body,land,slug):
    if slug not in {'wolf','red-fox','raccoon','hare','rabbit','buffalo'}:return None
    original=np.array([v.co[:] for v in body.data.vertices]);p=original.copy();F=np.array(land['front_axis']);S=np.array(land['side_axis']);nose=np.array(land['nose']['point']);mid=(np.array(land['right_eye']['point'])+land['left_eye']['point'])/2
    def smooth(t):t=np.clip(t,0,1);return t*t*(3-2*t)
    if slug=='buffalo':
        rel=p-mid;f=rel@F;z=rel[:,2];side=rel@S;head=smooth((f-.04)/.14)*smooth((-.025-z)/.09)*smooth((z+.44)/.12)
        p+=S[None,:]*(side*.20*head)[:,None];p-=F[None,:]*(np.minimum(.026,np.maximum(f-.10,0)*.10)*head)[:,None];p[:,2]-=.018*head*smooth((-.16-z)/.075)
        ear=smooth((np.abs(side)-np.linalg.norm(np.array(land['right_eye']['point'])-land['left_eye']['point'])*.60)/.07)*(1-smooth((f+.07)/.08))*smooth((f+.45)/.12)*smooth((z+.12)/.07)*smooth((.25-z)/.08)
        p[:,2]-=(.06+.035*smooth((np.abs(side)-.15)/.18))*ear
        delta=p-original
        for vertex,point in zip(body.data.vertices,p):vertex.co=point
        if body.data.shape_keys:
            for key in body.data.shape_keys.key_blocks:
                for vertex,shift in zip(key.data,delta):vertex.co=np.array(vertex.co)+shift
        body.data.update();ids=np.where(np.linalg.norm(delta,axis=1)>1e-7)[0]
        return {'method':'Reference-directed bounded muzzle widening, shortened tip, fuller lower nasal/jaw volume and lateral ear droop; eye centres and motion retained.','vertices':ids.tolist(),'positions':{str(i):p[i].tolist() for i in ids},'max_displacement':float(np.linalg.norm(delta,axis=1).max()),'appearance_approved':False}
    if slug in {'hare','rabbit'}:
        rel=p-mid;f=rel@F;z=rel[:,2];side=rel@S
        head=smooth((f+.34)/.18)*smooth((z+.32)/.10)*smooth((-.02-z)/.065)
        p-=S[None,:]*(side*(.30 if slug=='hare' else .18)*head)[:,None]
        p[:,2]+=(.045 if slug=='hare' else .025)*head*smooth((-.19-z)/.065)
        if slug=='rabbit':p-=F[None,:]*(np.minimum(.068,np.maximum(f-.015,0)*.30)*head)[:,None]
        neighbours=[set() for _ in p];groups={}
        for i,point in enumerate(np.round(original,6)):groups.setdefault(tuple(point),[]).append(i)
        for face in body.data.polygons:
            for i in face.vertices:neighbours[i].update(int(j) for j in face.vertices if j!=i)
        for ids in groups.values():
            connected=set().union(*(neighbours[i] for i in ids))
            for i in ids:neighbours[i]=connected-set(ids)
        relax=smooth((f+.34)/.18)*smooth((z+.32)/.10)*smooth((.18-z)/.10)
        for key in ['right_eye','left_eye']:
            anchor=land[key];N,H,V=[np.array(anchor['fit_frame'][k]) for k in ['normal','horizontal','vertical']];d=original-np.array(anchor['point']);q=np.sqrt((d@H/.045)**2+(d@V/.04)**2);relax*=smooth((q-1.)/.65)
        for _ in range(6):
            q=p.copy()
            for i in np.where(relax>0)[0]:
                if neighbours[i]:q[i]=p[i]+(p[list(neighbours[i])].mean(0)-p[i])*.25*relax[i]
            p=q
        delta=p-original;delta*=np.minimum(1,.085/np.maximum(np.linalg.norm(delta,axis=1),1e-12))[:,None];p=original+delta
        for vertex,point in zip(body.data.vertices,p):vertex.co=point
        if body.data.shape_keys:
            for key in body.data.shape_keys.key_blocks:
                for vertex,shift in zip(key.data,delta):vertex.co=np.array(vertex.co)+shift
        body.data.update();ids=np.where(np.linalg.norm(delta,axis=1)>1e-7)[0]
        return {'method':'Front/profile reference-directed lower-cheek taper, softened jaw depth and rabbit-only bounded muzzle shortening; lateral eye centres retained. Identical displacement across original morph keys.','vertices':ids.tolist(),'positions':{str(i):p[i].tolist() for i in ids},'max_displacement':float(np.linalg.norm(delta,axis=1).max()),'appearance_approved':False}
    rel=p-nose;f=rel@F;z=rel[:,2];side=rel@S
    jaw=smooth((f+.44)/.16)*smooth((-.008-z)/.065)*smooth((z+.30)/.09)*smooth((-.025-f)/.07)
    taper,lift=(.20,.035) if slug=='red-fox' else (.18,.028) if slug=='raccoon' else (.15,.040)
    p[:,2]+=lift*jaw;p-=S[None,:]*(side*taper*jaw)[:,None]
    neighbours=[set() for _ in p];groups={}
    for i,point in enumerate(np.round(original,6)):groups.setdefault(tuple(point),[]).append(i)
    for face in body.data.polygons:
        for i in face.vertices:neighbours[i].update(int(j) for j in face.vertices if j!=i)
    for ids in groups.values():
        connected=set().union(*(neighbours[i] for i in ids))
        for i in ids:neighbours[i]=connected-set(ids)
    relative=original-mid;forward=relative@F;height=relative[:,2]
    weight=smooth((forward+.34)/.15)*smooth((.30-forward)/.12)*smooth((height+.49)/.12)*smooth((.035-height)/.10)
    nasal=original-nose;protect=np.sqrt((nasal@F/.11)**2+(nasal@S/.095)**2+(nasal[:,2]/.075)**2);weight*=smooth((protect-.85)/.70)
    for key in ['right_eye','left_eye']:
        anchor=land[key];N,H,V=[np.array(anchor['fit_frame'][k]) for k in ['normal','horizontal','vertical']];d=original-np.array(anchor['point']);q=np.sqrt((d@H/.070)**2+(d@V/.060)**2);weight*=smooth((q-1)/.55)
    for _ in range(9):
        for factor in [.32,-.20]:
            q=p.copy()
            for i in np.where(weight>0)[0]:
                if neighbours[i]:q[i]=p[i]+(p[list(neighbours[i])].mean(0)-p[i])*factor*weight[i]
            p=q
    if slug=='wolf':
        # Match the approved deer method's LOCAL socket-bed relaxation before
        # cutting the lid opening. Subdivision alone retains angular pit walls.
        orbital=np.zeros(len(p))
        for key in ['right_eye','left_eye']:
            anchor=land[key];N,H,V=[np.array(anchor['fit_frame'][k]) for k in ['normal','horizontal','vertical']];d=original-np.array(anchor['point']);R=anchor['radius'][0];q=np.sqrt((d@H/(R*2.8))**2+(d@V/(R*2.6))**2);w=smooth((1.1-q)/.35)*smooth((R*2.7-abs(d@N))/(R*.7));orbital=np.maximum(orbital,w)
        for _ in range(3):
            q=p.copy()
            for i in np.where(orbital>0)[0]:
                if neighbours[i]:q[i]=p[i]+(p[list(neighbours[i])].mean(0)-p[i])*.20*orbital[i]
            p=q
    delta=p-original;length=np.linalg.norm(delta,axis=1);delta*=np.minimum(1,.090/np.maximum(length,1e-12))[:,None];p=original+delta
    for vertex,point in zip(body.data.vertices,p):vertex.co=point
    if body.data.shape_keys:
        for key in body.data.shape_keys.key_blocks:
            for vertex,shift in zip(key.data,delta):vertex.co=np.array(vertex.co)+shift
    body.data.update();ids=np.where(np.linalg.norm(delta,axis=1)>1e-7)[0]
    return {'method':'Reference-directed lower cheek/jaw taper and lift, bounded Taubin relaxation through cheek-to-neck folds, protected eye and nasal anchors. Source reset each build; identical displacement on every morph.','vertices':ids.tolist(),'positions':{str(i):p[i].tolist() for i in ids},'max_displacement':float(np.linalg.norm(delta,axis=1).max()),'appearance_approved':False}
