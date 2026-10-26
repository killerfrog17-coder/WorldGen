"""Reference-directed coat markings in bind space, never animated geometry."""
import numpy as np

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
