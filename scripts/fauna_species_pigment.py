"""Reference-directed coat markings in bind space, never animated geometry."""
import numpy as np

def prey_soft_features(colour,p,n,land,bake,pale,sampled,slug):
    """Small light chin and thin-fur ear interior, independent of hair hue.

    Spatial boundaries follow the registered muzzle and existing ear frames;
    individual grey guard hairs never become discrete white paint spots.
    """
    def smooth(a,b,x):
        t=np.clip((x-a)/(b-a),0,1);return t*t*(3-2*t)
    F=np.array(land['front_axis']);nose=np.array(land['nose']['point']);d=p-nose
    chin=smooth(-.16,-.065,d@F)*smooth(-.19,-.10,d[:,2])*(1-smooth(-.010,.015,d[:,2]))
    S=np.array(land['side_axis']);nw,nh=land['physical_eye_revision']['nose_radius'];nasal=(d@S/nw)**2+(d[:,2]/nh)**2+(d@F/.025)**2
    chin*=smooth(.65,1.45,nasal)*smooth(.08,.20,sampled@np.array([.2126,.7152,.0722]))
    detail=np.clip((sampled@np.array([.2126,.7152,.0722]))/.60,.65,1.10)
    target=np.array(pale)[None,:]*(.82+.16*detail)[:,None]
    colour=colour*(1-chin[:,None]*.88)+target*chin[:,None]*.88
    inner=np.zeros(len(p))
    for frame in bake['ear_frames']:
        root=np.array(frame['root']);axis=np.array(frame['tip'])-root;length=np.linalg.norm(axis);axis/=length
        across=np.cross(F,axis);across/=np.linalg.norm(across);dd=p-root;t=dd@axis/length;u=dd@across
        gate=smooth(.20,.32,t)*(1-smooth(.80,.96,t))*(1-smooth(.42,.72,abs(u)/frame['width']))
        gate*=smooth(.15,.65,n@F)*(1-smooth(.10,.17,abs(dd@F)))
        inner=np.maximum(inner,gate)
    skin=np.array([.51,.38,.35] if slug=='rabbit' else [.43,.35,.29])
    target=skin[None,:]*(.82+.24*detail)[:,None]
    return colour*(1-inner[:,None]*.80)+target*inner[:,None]*.80

def goat_mature_male(colour,p,n,land,center_x):
    """Connected dark face/neck, dorsal line and foreleg/shoulder marking.

    Mature Bezoar/Cretan references guide this ONE labelled pigment example.
    It is not a rule that every male or every goat must have this coat.
    """
    def smooth(a,b,x):
        t=np.clip((x-a)/(b-a),0,1);return t*t*(3-2*t)
    mid=(np.array(land['right_eye']['point'])+land['left_eye']['point'])/2;F=np.array(land['front_axis']);rel=p-mid;f=rel@F;z=rel[:,2]
    face=smooth(-.13,.04,f)*smooth(-.30,-.18,z)*(1-smooth(.18,.27,z))
    neck=smooth(.27,.39,p[:,1])*smooth(.82,.98,p[:,2])*(1-smooth(1.43,1.55,p[:,2]))
    axis=.16+.12*(p[:,2]-.72);bar=np.exp(-((p[:,1]-axis)/.073)**4)*smooth(.28,.42,p[:,2])*(1-smooth(1.18,1.35,p[:,2]))*smooth(.15,.55,abs(n[:,0]))
    dorsal=np.exp(-((p[:,0]-center_x-.008*np.sin(p[:,1]*7))/.045)**2)*smooth(.15,.65,n[:,2])*(1-smooth(.35,.52,p[:,1]))
    forelegs=smooth(.0,.14,p[:,1])*(1-smooth(.46,.64,p[:,2]))
    mask=np.maximum.reduce([face*.94,neck*.88,bar*.94,dorsal*.94,forelegs*.85]);lum=colour@np.array([.2126,.7152,.0722]);target=np.array([.070,.047,.030])[None,:]*(.30+1.05*np.clip(lum/.45,0,2.0))[:,None]
    return colour*(1-mask[:,None])+np.minimum(colour,target)*mask[:,None]
