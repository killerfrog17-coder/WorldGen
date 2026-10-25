"""Explicit anatomy/coat recipes for selected-body colour finishing.

These are prototype art palettes, not generated ecology taxa or approval of
one mesh as every named real analogue. Extend one body family at a time.
"""
SUPPORTED={'hare','rabbit','red-fox','wolf','wild-boar','mouflon','wild-goat','bear','bison','duck','pheasant','small-native-freshwater-fish-guild','mouse','squirrel'}

def family(slug):
    assert slug in SUPPORTED
    if slug in {'mouse','squirrel'}:return 'rodent'
    return 'canid' if slug in {'red-fox','wolf'} else 'suine' if slug=='wild-boar' else 'caprine' if slug in {'mouflon','wild-goat'} else 'ursid' if slug=='bear' else 'bovine' if slug=='bison' else 'avian' if slug in {'duck','pheasant'} else 'fish' if slug=='small-native-freshwater-fish-guild' else 'lagomorph'

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
            ('Pale dryland (hypothetical)',[.61,.51,.36],[.84,.80,.70],False)],
        'wild-boar':[
            ('Dark brown grizzled adult',[.245,.18,.12],[.32,.27,.21],False),
            ('Black bristled adult',[.080,.072,.060],[.15,.135,.11],False),
            ('Grey grizzled adult',[.35,.325,.28],[.40,.375,.32],False),
            ('Juvenile stripe pigment study (adult body)',[.41,.285,.16],[.49,.38,.245],False),
            ('Warm dryland brown (hypothetical)',[.41,.285,.17],[.48,.36,.24],False)],
        'mouflon':[
            ('Rufous ram with pale saddle',[.43,.245,.13],[.81,.78,.68],False),
            ('Dark winter ram',[.225,.145,.09],[.82,.80,.72],False),
            ('Tan-brown ewe',[.46,.34,.22],[.80,.76,.66],False),
            ('Plain juvenile pigment study (adult body)',[.42,.315,.22],[.76,.71,.61],False),
            ('Pale dryland (hypothetical)',[.61,.49,.33],[.86,.81,.70],False)],
        'wild-goat':[
            ('Warm brown summer',[.43,.305,.18],[.75,.70,.59],False),
            ('Grey-brown winter',[.36,.345,.29],[.75,.73,.65],False),
            ('Mature male dark markings',[.39,.285,.18],[.76,.71,.62],False),
            ('Lighter female / young palette (adult body)',[.53,.42,.285],[.80,.75,.65],False),
            ('Sandy dryland (hypothetical)',[.64,.52,.35],[.85,.80,.68],False)],
        'bear':[
            ('Near-black pigment study (current body)',[.075,.065,.055],[.32,.26,.20],False),
            ('Cinnamon pigment study (current body)',[.36,.215,.115],[.45,.34,.24],False),
            ('Dark brown',[.225,.16,.10],[.36,.285,.215],False),
            ('Blond',[.60,.49,.33],[.68,.585,.445],False),
            ('Silver-tipped grizzle',[.35,.255,.17],[.46,.37,.28],False),
            ('Pale woodland (hypothetical)',[.53,.50,.415],[.64,.595,.49],False)],
        'bison':[
            ('Dark brown adult',[.235,.16,.105],[.36,.265,.17],False),
            ('Warm brown adult',[.36,.245,.14],[.49,.365,.23],False),
            ('Light-tipped shoulder coat',[.30,.225,.145],[.52,.43,.30],False),
            ('Spring shedding overlay study',[.30,.205,.125],[.47,.365,.235],False),
            ('Pale-brown regional (hypothetical)',[.48,.365,.235],[.60,.49,.34],False)],
        'duck':[
            ('Mallard-like breeding male',[.51,.50,.45],[.77,.75,.66],False),
            ('Mottled brown adult female',[.40,.285,.16],[.67,.54,.35],False),
            ('Brown eclipse male',[.37,.275,.17],[.61,.51,.35],False),
            ('Immature pigment study (adult body)',[.41,.32,.21],[.68,.57,.40],False),
            ('Muted olive-headed male (hypothetical)',[.43,.42,.31],[.68,.65,.48],False),
            ('Pale reedbed mottling (hypothetical)',[.60,.48,.30],[.81,.71,.51],False)],
        'pheasant':[
            ('Copper ring-necked male',[.48,.245,.10],[.72,.57,.33],False),
            ('Brown female pigment study (long-tailed body)',[.40,.30,.18],[.67,.55,.36],False),
            ('Buff immature pigment study (adult body)',[.50,.39,.24],[.74,.63,.44],False),
            ('Reduced-ring male (hypothetical)',[.46,.29,.135],[.69,.55,.34],False),
            ('Dark forest male (hypothetical)',[.24,.16,.10],[.47,.37,.23],False)],
        'small-native-freshwater-fish-guild':[
            ('Silver flank / olive back',[.52,.55,.46],[.79,.81,.74],False),
            ('Dusky lateral stripe',[.43,.48,.37],[.76,.79,.69],False),
            ('Mottled brown pool form',[.35,.31,.22],[.64,.61,.48],False),
            ('Dark-headed breeding pigment study',[.34,.35,.27],[.63,.65,.53],False),
            ('Amber stream form (hypothetical)',[.57,.44,.22],[.78,.71,.48],False)],
        'mouse':[
            ('Golden-brown wood-mouse analogue',[.46,.32,.18],[.79,.75,.63],False),
            ('Grey-brown / two-tone deer-mouse analogue',[.35,.32,.26],[.84,.83,.77],False),
            ('Rufous two-tone',[.47,.255,.12],[.78,.70,.56],False),
            ('Dark wet-cover (hypothetical)',[.145,.125,.095],[.48,.45,.37],False),
            ('Pale sand (hypothetical)',[.63,.51,.32],[.86,.80,.66],False)],
        'squirrel':[
            ('Grey / pale belly',[.40,.39,.35],[.81,.80,.72],False),
            ('Grey / rusty underside study',[.37,.34,.28],[.55,.31,.14],False),
            ('Dark melanistic grey-squirrel analogue',[.085,.078,.065],[.22,.205,.17],False),
            ('Rufous tree-squirrel pigment study',[.47,.245,.10],[.80,.72,.58],False),
            ('Dry-woodland brown (hypothetical)',[.49,.39,.25],[.78,.70,.54],False)]
    }[slug]

def eye_radius(slug):
    return {'hare':(.038,.032),'rabbit':(.040,.034),'red-fox':(.029,.019),'wolf':(.032,.021),'wild-boar':(.027,.018),'mouflon':(.026,.019),'wild-goat':(.026,.019),'bear':(.025,.017),'bison':(.027,.020),'duck':(.021,.018),'pheasant':(.017,.015),'small-native-freshwater-fish-guild':(.043,.039),'mouse':(.035,.031),'squirrel':(.031,.027)}[slug]

def fur_source(slug):
    if slug=='small-native-freshwater-fish-guild':return 'artifacts/fauna-assets/textures/scale-fish-v1.png'
    if slug in {'duck','pheasant'}:return 'artifacts/fauna-assets/textures/feather-contour-v1.png'
    if slug=='bison':return 'artifacts/fauna-assets/textures/fur-wool-shag-v1.png'
    if slug in {'wild-boar','bear'}:return 'artifacts/fauna-assets/textures/fur-coarse-guard-v1.png'
    if slug in {'mouflon','wild-goat'}:return 'artifacts/fauna-assets/deer/fur-source.png'
    return 'artifacts/fauna-assets/deer/fur-source.png' if family(slug)=='canid' else 'artifacts/fauna-assets/textures/fur-dense-fine-v1.png'

def eye_frame(slug,land,side):
    """Same anatomical eye frame for pigment masks and fitted geometry."""
    import numpy as np
    anchor=land[side+'_eye'];n=np.array(anchor['normal']);n/=np.linalg.norm(n)
    if family(slug)!='lagomorph':
        forward,lateral={'canid':(.62,.78),'suine':(.35,.94),'caprine':(.22,.975),'ursid':(.72,.69),'bovine':(.23,.973),'avian':(.20,.98),'fish':(.16,.987),'rodent':(.34,.94)}[family(slug)]
        desired=np.array(land['front_axis'])*forward+np.array(land['side_axis'])*(1 if side=='right' else -1)*lateral
        actual=n.copy();actual[2]=0;actual/=np.linalg.norm(actual)
        n=actual*.55+desired*.45;n[2]=np.clip(anchor['normal'][2],-.12,.12);n/=np.linalg.norm(n)
        horizontal=np.cross([0.,0,1],n);horizontal/=np.linalg.norm(horizontal)
        if horizontal@np.array(land['front_axis'])<0:horizontal=-horizontal
        vertical=np.array([0.,0,1])-n*n[2];vertical/=np.linalg.norm(vertical)
    else:
        horizontal=np.array([0.,1,0]);horizontal-=n*np.dot(horizontal,n);horizontal/=np.linalg.norm(horizontal)
        vertical=np.array([0.,0,1]);vertical-=n*np.dot(vertical,n)+horizontal*np.dot(vertical,horizontal);vertical/=np.linalg.norm(vertical)
    return n,horizontal,vertical
