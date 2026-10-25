"""Linked adult appendage options; hypothetical forms are explicit art proposals.

Numerical sizes are bounded art choices at review scale, not biological samples.
Sex labels describe appendages on the same chosen adult body, not sex morphs.
"""
TRAITS={
 'reindeer':('antler',.68,'both'),
 'moose':('palmate',.62,'male'),
 'wild-cattle':('lyre',.37,'both'),
 'buffalo':('buffalo',.43,'both'),
 'gazelle':('lyre',.35,'both'),
 'ibex':('scimitar',.68,'both'),
 'impala':('lyre',.50,'male'),
 'musk-ox':('muskox',.40,'both'),
 'oryx':('spear',.85,'both'),
 'pronghorn':('pronghorn',.34,'both-small-female'),
 'wildebeest':('wildebeest',.35,'both'),
 'yak':('lyre',.53,'both'),
 'duiker':('spike',.13,'lineage'),
 'forest-hog':('tusk',.065,'both'),
 'warthog':('tusk',.21,'both'),
 'collared-peccary':('tusk',.035,'both'),
}
SOURCES={
 'antlers':'https://www.nps.gov/articles/hornsvantlers.htm',
 'reindeer':'https://www.fda.gov/animal-veterinary/animal-health-literacy/fun-facts-about-reindeer-and-caribou',
 'moose':'https://www.adfg.alaska.gov/static/regulations/wildliferegulations/pdfs/mooseid.pdf',
 'oryx':'https://animals.sandiegozoo.org/animals/oryx',
 'pronghorn':'https://www.nps.gov/yell/learn/nature/pronghorn.htm',
 'muskox':'https://www.adfg.alaska.gov/index.cfm?adfg=muskoxidentification.main',
 'wild_cattle':'https://animals.sandiegozoo.org/animals/wild-cattle',
}

def configurations(slug):
    shape,size,sex=TRAITS[slug]
    variants=[dict(id='adult',label='Adult male appendage proportions; selected adult body',shape=shape,size=size,radius=1.,sex='male',hypothetical=False),
              dict(id='compact',label='Compact adult male proportions; selected adult body',shape=shape,size=size*.76,radius=.85,sex='male',hypothetical=False)]
    if sex in {'both','both-small-female'}:
        female_scale=.38 if slug=='reindeer' else .20 if slug=='pronghorn' else .65 if slug=='ibex' else .88
        variants.append(dict(id='female',label='Adult female appendage proportions; selected adult body',shape='spike' if slug=='pronghorn' else shape,size=size*female_scale,radius=.58,sex='female',hypothetical=False))
        if slug=='oryx':variants[-1].update(size=size*1.10,radius=.62)
    elif sex=='male':
        variants.append(dict(id='female',label='Antlerless female / seasonally shed male' if slug=='moose' else 'Hornless adult female',shape='none',size=0.,radius=0.,sex='female',hypothetical=False))
    else:
        variants.append(dict(id='female',label='Hornless female-lineage example; selected adult body',shape='none',size=0.,radius=0.,sex='female',hypothetical=False))
    if slug in {'reindeer','moose'}:
        variants.append(dict(id='shed',label='Seasonally shed antlers; adult body',shape='none',size=0.,radius=0.,sex='seasonal',hypothetical=False))
    if slug=='pronghorn':variants.append(dict(id='female-hornless',label='Hornless female example',shape='none',size=0.,radius=0.,sex='female',hypothetical=False))
    variants.append(dict(id='hyp-compact',label='Hypothetical dense-cover lineage: compact, less projecting appendages',shape=shape,size=size*.67,radius=.92,sex='lineage-dependent',hypothetical=True))
    variants.append(dict(id='hyp-open',label='Hypothetical open-habitat lineage: broader display within the same structure',shape=shape,size=size*1.08,radius=.95,sex='lineage-dependent',hypothetical=True))
    if slug=='oryx':variants.append(dict(id='scimitar',label='Scimitar-like curved horn analogue; same selected body',shape='scimitar',size=.80,radius=.75,sex='both',hypothetical=False))
    if slug=='buffalo':variants.append(dict(id='water-sweep',label='Water-buffalo sweeping horn analogue; selected adult body',shape='water-buffalo',size=.62,radius=.75,sex='both',hypothetical=False))
    return variants
