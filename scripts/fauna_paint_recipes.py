"""Explicit anatomy/coat recipes for selected-body colour finishing.

These are prototype art palettes, not generated ecology taxa or approval of
one mesh as every named real analogue. Extend one body family at a time.
"""
SUPPORTED={'hare','rabbit','red-fox','wolf'}

def family(slug):
    assert slug in SUPPORTED
    return 'canid' if slug in {'red-fox','wolf'} else 'lagomorph'

def profiles(slug):
    assert slug in SUPPORTED
    return {
        'hare':[
            ('European brown',[.40,.30,.18],[.79,.76,.66],True),
            ('European grey-brown',[.32,.30,.25],[.77,.76,.69],True),
            ('Snowshoe summer',[.39,.26,.16],[.78,.75,.66],True),
            ('Snowshoe winter',[.82,.82,.76],[.89,.89,.83],True),
            ('Sandy dryland (hypothetical)',[.57,.46,.30],[.83,.78,.67],True),
            ('Dark wet-cover (hypothetical)',[.20,.18,.14],[.58,.56,.48],True)],
        'rabbit':[
            ('Grey-brown agouti',[.34,.29,.22],[.79,.78,.72],False),
            ('Warm brown agouti',[.43,.30,.19],[.81,.77,.67],False),
            ('Dark melanistic',[.10,.085,.07],[.27,.25,.22],False),
            ('Pale sandy (hypothetical)',[.58,.47,.32],[.85,.80,.69],False),
            ('Litter-mottled (hypothetical)',[.31,.25,.17],[.76,.72,.62],False)],
        'red-fox':[
            ('Red woodland',[.57,.26,.105],[.84,.81,.73],False),
            ('Dark cross phase',[.42,.21,.115],[.76,.73,.67],False),
            ('Silver-tipped dark phase',[.12,.115,.105],[.66,.65,.60],False),
            ('Black phase',[.070,.064,.058],[.39,.38,.34],False),
            ('Pale dryland red (hypothetical)',[.67,.43,.235],[.87,.83,.73],False)],
        'wolf':[
            ('Grey agouti',[.40,.38,.325],[.75,.73,.65],False),
            ('Warm tawny-brown',[.48,.36,.23],[.79,.74,.63],False),
            ('Dark brown',[.255,.20,.14],[.59,.54,.45],False),
            ('Black guard-hair',[.085,.080,.069],[.30,.285,.25],False),
            ('Ivory northern analogue',[.79,.79,.735],[.88,.87,.81],False),
            ('Pale dryland (hypothetical)',[.61,.51,.36],[.84,.80,.70],False)]
    }[slug]

def eye_radius(slug):
    return {'hare':(.038,.032),'rabbit':(.040,.034),'red-fox':(.029,.019),'wolf':(.032,.021)}[slug]

def fur_source(slug):
    return 'artifacts/fauna-assets/deer/fur-source.png' if family(slug)=='canid' else 'artifacts/fauna-assets/textures/fur-dense-fine-v1.png'

def eye_frame(slug,land,side):
    """Same anatomical eye frame for pigment masks and fitted geometry."""
    import numpy as np
    anchor=land[side+'_eye'];n=np.array(anchor['normal']);n/=np.linalg.norm(n)
    if family(slug)=='canid':
        desired=np.array(land['front_axis'])*.62+np.array(land['side_axis'])*(1 if side=='right' else -1)*.78
        actual=n.copy();actual[2]=0;actual/=np.linalg.norm(actual)
        n=actual*.55+desired*.45;n[2]=np.clip(anchor['normal'][2],-.12,.12);n/=np.linalg.norm(n)
        horizontal=np.cross([0.,0,1],n);horizontal/=np.linalg.norm(horizontal)
        if horizontal@np.array(land['front_axis'])<0:horizontal=-horizontal
        vertical=np.array([0.,0,1])-n*n[2];vertical/=np.linalg.norm(vertical)
    else:
        horizontal=np.array([0.,1,0]);horizontal-=n*np.dot(horizontal,n);horizontal/=np.linalg.norm(horizontal)
        vertical=np.array([0.,0,1]);vertical-=n*np.dot(vertical,n)+horizontal*np.dot(vertical,horizontal);vertical/=np.linalg.norm(vertical)
    return n,horizontal,vertical
