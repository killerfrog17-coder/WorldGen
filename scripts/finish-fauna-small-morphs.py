"""Bounded ear/tail pigment-lineage morphs, preserving the approved basis/rig.

Ear frames came from previously inspected surface fits. Tail selection requires
both the original tail role and actual rearward anatomy, so leg weights cannot
silently become a tail morph. No new bones or motion tracks are authored here.
"""
import argparse,json,sys
from pathlib import Path
import bpy,numpy as np
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slug',choices=['hare','rabbit','red-fox','wolf'],required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=repo/'artifacts/fauna-painted-20261024'/a.slug
state=json.loads((root/'paint-state.json').read_text());assert not state['appearance_approved']
bake=json.loads((root/'coat-bake.json').read_text());land=json.loads((root/'face-landmarks.json').read_text());d=dict(np.load(root/'paint-anatomy.npz'))
bpy.ops.wm.open_mainfile(filepath=str(root/'Animal - Painted Species Review.blend'));entry=json.loads(bpy.context.scene['deer_four_rigs'])[0]
body=bpy.data.objects[entry['body']];p=np.array([v.co[:] for v in body.data.vertices]);delta=np.zeros_like(p)
def smooth(lo,hi,x):
    t=np.clip((x-lo)/(hi-lo),0,1);return t*t*(3-2*t)
head=np.array(land['head_seed']);side=np.array(land['side_axis']);compact=a.slug in {'red-fox','wolf'}
split=(p-head)@side if compact else p[:,0]-state['center_x']
ear_mask=np.zeros(len(p))
for frame in bake['ear_frames']:
    root_point=np.array(frame['root']);tip=np.array(frame['tip']);axis=tip-root_point;length=np.linalg.norm(axis);axis/=length
    rel=p-root_point;along=rel@axis;radial=np.linalg.norm(rel-along[:,None]*axis,axis=1)
    mask=smooth(.005,.055,along)*smooth(state['ear_base']-.025,state['ear_base']+.055,p[:,2])
    mask*=(split*frame['side']>0)*(p[:,1]<head[1]+.06)*(1-smooth(frame['width']*1.4,frame['width']*2.2,radial))
    # Localised root-to-tip change; skull and facial vertices stay fixed.
    delta+=axis[None,:]*(along*.085*mask)[:,None];ear_mask=np.maximum(ear_mask,mask)
assert (ear_mask>.5).sum()>8
if not body.data.shape_keys:body.shape_key_add(name='Basis')
traits={}
name='Lineage ear length'
key=body.data.shape_keys.key_blocks.get(name) or body.shape_key_add(name=name)
key.data.foreach_set('co',(p+delta).astype(np.float32).ravel());key.slider_min=-1;key.slider_max=1;key.value=0
traits['ear_length']={'shape_key':name,'bounds':[-1,1],'maximum_tip_fraction':.085,'selected_vertices':int((ear_mask>.1).sum())}
source_path=root/'selected-source-entry.json'
raw=json.loads(source_path.read_text()) if source_path.exists() else json.loads((repo/'artifacts/fauna-overnight-20261024'/a.slug/'audition.json').read_text())['candidates'][state['source_candidate']-1]
groups=np.array(raw['groups']);tail=d['weights'][:,groups=='tail'].sum(1)
rear=(p[:,1]<np.quantile(p[:,1],.30))
mask=smooth(.32,.78,tail)*rear
if (mask>.5).sum()>8:
    selected=p[mask>.5];threshold=np.quantile(selected[:,1],.85)
    root_point=selected[selected[:,1]>=threshold].mean(0)
    delta=(p-root_point)*.10*mask[:,None];name='Lineage tail length'
    key=body.data.shape_keys.key_blocks.get(name) or body.shape_key_add(name=name)
    key.data.foreach_set('co',(p+delta).astype(np.float32).ravel());key.slider_min=-1;key.slider_max=1;key.value=0
    traits['tail_length']={'shape_key':name,'bounds':[-1,1],'maximum_length_fraction':.10,'selected_vertices':int((mask>.1).sum())}
variants=[{'label':'Selected adult proportions','objects':[],'body_morphs':{'ear_length':0.,'tail_length':0.},'hypothetical':False},
          {'label':'Hypothetical compact appendages in cold or dense cover','objects':[],'body_morphs':{'ear_length':-.9,'tail_length':-.7},'hypothetical':True},
          {'label':'Hypothetical slightly longer ears/tail; same adult build','objects':[],'body_morphs':{'ear_length':.9,'tail_length':.8},'hypothetical':True}]
for coat in entry['painting_coats']:coat.update(appendage_variants=variants,visible_extras=[],trait_label=variants[0]['label'])
entry['body_morph_traits']=traits;bpy.context.scene['deer_four_rigs']=json.dumps([entry])
assert np.array_equal(p,np.array([v.co[:] for v in body.data.vertices]))
state.update(coats=entry['painting_coats'],body_morph_traits=traits)
(root/'paint-state.json').write_text(json.dumps(state,indent=2)+'\n')
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(root/'Animal - Painted Species Review.blend'))
print('Bounded basis-preserving ear/tail morphs',a.slug,traits)
