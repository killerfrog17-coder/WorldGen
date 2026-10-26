"""Explicit pigment-only evolutionary art hypotheses, not asserted real taxa.

Rationales describe possible camouflage/contrast pressures. They do not claim
that the selection happened, change physiology, or implement an ecology system.
"""
import numpy as np

def value_noise(x,y):
    ix=np.floor(x);iy=np.floor(y);fx=x-ix;fy=y-iy
    fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy)
    def hashed(a,b):return np.mod(np.sin(a*127.1+b*311.7)*43758.5453,1.)
    return (hashed(ix,iy)*(1-fx)+hashed(ix+1,iy)*fx)*(1-fy)+(hashed(ix,iy+1)*(1-fx)+hashed(ix+1,iy+1)*fx)*fy

def mottling(x,y):
    return value_noise(x,y)*.55+value_noise(x*2.13+13,y*2.13-7)*.28+value_noise(x*4.31-31,y*4.31+11)*.17

def organic_stripes(p,n,count=11,seed=41,horizontal=False,broken=False,uv=None):
    """Static bind-surface pigment: curved, tapered strokes and occasional forks.

    Coordinates are continuous across UV seams. This is an atlas painting cost,
    not a runtime shader or per-frame procedural pattern.
    """
    rng=np.random.default_rng(seed);axis=p[:,2] if horizontal else p[:,1];cross=p[:,1] if horizontal else p[:,2]
    u=(axis-axis.min())/max(float(np.ptp(axis)),1e-5);v=(cross-cross.min())/max(float(np.ptp(cross)),1e-5)
    if uv is not None:u,v=uv
    side=np.tanh(p[:,0]*8);mask=np.zeros(len(p))
    def smooth(a,b,x):
        q=np.clip((x-a)/(b-a),0,1);return q*q*(3-2*q)
    grain=(value_noise(u*47+9,v*61-7)-.5)*.004
    for k in range(count):
        pos=(k+.5+rng.uniform(-.22,.22))/count;phase=rng.uniform(0,6.28);lean=rng.uniform(-.12,.12);bow=rng.uniform(-.13,.13);width=rng.uniform(.13,.34)/count
        curve=pos+lean*(v-.55)+bow*(v-.15)*(v-.85)+.012*np.sin(v*8+phase)+side*.005*np.sin(v*5+phase)+grain
        tip=rng.uniform(.06,.38);taper=smooth(tip,tip+.16,v)*np.clip(1.15-.35*v,.45,1.0);distance=abs(u-curve);stroke=1-smooth(width*taper*.45,width*taper+1e-4,distance)
        if broken and k%3==1:
            gap=rng.uniform(.40,.75);stroke*=smooth(.015,.07,abs(v-gap))
        if k%3==0:
            fork=.030*smooth(.40,.72,v);branch=1-smooth(width*taper*.25,width*taper*.60+1e-4,abs(u-curve-fork));stroke=np.maximum(stroke,branch*smooth(.38,.58,v))
        mask=np.maximum(mask,stroke)
    return np.clip(mask,0,1)

EXTRA={
 'hare':('Faint thicket bars (hypothetical)',[.34,.27,.17],[.69,.64,.51],'Broken side markings under patchy thicket shade.'),
 'rabbit':('Leaf-litter mottled (hypothetical)',[.32,.25,.16],[.66,.60,.46],'Low-contrast mottling against irregular dry leaves.'),
 'red-fox':('Broken russet flank bars (hypothetical)',[.49,.25,.10],[.78,.71,.56],'Subdued broken bands in dense vegetation; existing muzzle and stockings retained.'),
 'wolf':('Broken ash saddle (hypothetical)',[.34,.32,.27],[.69,.66,.57],'Discontinuous guard-hair saddle under woodland shadow.'),
 'horse':('Faint flank bars (hypothetical)',[.45,.36,.23],[.74,.67,.51],'Low-contrast equid banding retained or strengthened in broken cover.'),
 'reindeer':('Thicket-shadow bands (hypothetical)',[.32,.25,.16],[.70,.66,.54],'Faint flank disruption in wooded cover; antler architecture stays cervid.'),
 'moose':('Willow-marsh mottled (hypothetical)',[.25,.21,.14],[.51,.45,.33],'Subtle dark/lighter mottling among willow stems and wetland shade.'),
 'wild-cattle':('Dry-scrub broken brindle (hypothetical)',[.35,.23,.12],[.62,.52,.35],'Muted brindling in a patchy scrub habitat.'),
 'bison':('Dune-brown broken brindle (hypothetical)',[.35,.27,.16],[.60,.49,.31],'Low-contrast coat disruption in dry grass and sandy scrub.'),
 'duck':('Reed-edge cryptic mottling (hypothetical)',[.31,.27,.17],[.66,.61,.46],'Cryptic plumage in reeds; a pigment study on the selected adult build.'),
}

def rationale(label):
    low=label.lower()
    if 'hypothetical' not in low:return None
    for record in EXTRA.values():
        if label==record[0]:return record[3]
    if any(w in low for w in ['dry','sand','pale']):return 'Hypothesized low-contrast pale/warm pigmentation in lighter or dry cover; body form unchanged.'
    if any(w in low for w in ['forest','wood','litter','thicket']):return 'Hypothesized cryptic pigmentation in broken shade or litter; body form unchanged.'
    return 'Conservative pigment/marking contrast hypothesis on the selected body; no new taxon or physiological adaptation asserted.'

def treatment(colour,label,p,n,protect):
    low=label.lower()
    if 'hypothetical' not in low:return colour
    available=np.clip(1-protect,0,1)
    back=np.clip((n[:,2]-.10)/.75,0,1)
    noise=mottling(p[:,1]*9+np.sin(p[:,0]*13)*.5,p[:,2]*11)
    mask=np.zeros(len(p));target=colour*.65
    if any(w in low for w in ['band','bar','brindle']):
        mask=organic_stripes(p,n,count=9,seed=43,broken=True)*np.clip(noise*1.5,0,1)*.34
    elif any(w in low for w in ['mottl','litter','cryptic','broken']):mask=np.clip((noise-.35)/.45,0,1)*.28
    elif any(w in low for w in ['dorsal','saddle','back','bristle']):mask=back*.27
    if 'stripe' in low:
        if 'dorsal' in low:
            centre=(p[:,0].min()+p[:,0].max())*.5+.018*np.sin(p[:,1]*6)+.008*np.sin(p[:,1]*17);mask=np.exp(-((p[:,0]-centre)/.06)**2)*back*.36
        else:mask=organic_stripes(p,n,count=9,seed=83,broken=True)*.34
    mask*=available
    return colour*(1-mask[:,None])+target*mask[:,None]
