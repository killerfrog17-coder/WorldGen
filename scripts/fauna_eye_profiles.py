"""Socket-specific values shared by atlas masks and fitted eye geometry."""

def pipeline_signature(repo):
    import hashlib
    names=['fit-fauna-reviewed-eyes.py','finish-selected-fauna-face.py','fauna_eye_profiles.py','fauna_paint_recipes.py','bake-fauna-finish.py','bake-selected-fauna-coats.py','bake-selected-fauna-bird.py','bake-selected-fauna-fish.py','render-fauna-eye-results.py']
    h=hashlib.sha256()
    for name in names:h.update(name.encode());h.update((repo/'scripts'/name).read_bytes())
    return h.hexdigest()
def dimensions(land,default,side=None):
    if side:return land[side+'_eye'].get('radius',default)
    return land.get('eye_revision',{}).get('mean_radius',default)

def contour(t,kind):
    import math
    c=math.cos(t);s=math.sin(t)
    if kind in {'fish','scaled'}:return c,s
    # Forward tear corner; upper lid arch and shallower lower lid.
    return c*(1+.12*max(c,0)),s*(1.08 if s>0 else .82)
