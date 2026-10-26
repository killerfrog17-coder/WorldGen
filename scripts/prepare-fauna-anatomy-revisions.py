"""Prepare isolated, source-pinned anatomy work. Never selects/approves bodies."""
import argparse,hashlib,json,shutil
from pathlib import Path
import numpy as np

repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('slugs',nargs='+');a=ap.parse_args()
feedback=json.loads((repo/'docs/fauna/PHONE_REVIEW_FEEDBACK.json').read_text())
overrides={row['slug']:row['repair_review']['blend'] for row in feedback['items'] if row.get('repair_review')}
settings={
 'wild-boar':((.095,.065),.30,.67),
 'mouflon':((.055,.035),.65,None),
 'wild-goat':((.045,.030),.65,None),
 'bear':((.080,.050),.25,.63),
 'bison':((.095,.055),.30,None),
 'horse':((.085,.060),.22,None),
 'reindeer':((.075,.045),.35,None),
 'moose':((.100,.060),.28,None),
 'lynx':((.050,.034),.55,.45),
 'badger':((.060,.038),.25,.70),
 'otter':((.070,.045),.23,.70),
 'mouse':((.018,.012),.20,.78),
 'squirrel':((.028,.018),.20,.77),
 'beaver':((.055,.037),.23,.75),
 'lion':((.075,.048),.70,.46),
 'tiger':((.065,.042),.78,.44),
 'jaguar':((.066,.043),.72,.45),
 'leopard':((.055,.036),.72,.44),
 'puma':((.057,.038),.70,.46),
 'ocelot':((.040,.027),.70,None),
}
for slug in a.slugs:
 assert slug in settings,slug
 src=repo/'artifacts/fauna-review-revisions'/slug;dest=repo/'artifacts/fauna-anatomy-revision'/slug
 assert not (dest/'visual-source.json').exists(),'Already prepared: '+slug
 dest.mkdir(parents=True,exist_ok=True)
 for name in ['paint-state.json','coat-bake.json','face-landmarks.json','paint-anatomy.npz','coat-roughness.png','coat-normal.png']:
  shutil.copy2(src/name,dest/name)
 bake=json.loads((dest/'coat-bake.json').read_text())
 for coat in bake['coats']:shutil.copy2(src/coat['file'],dest/coat['file'])
 (dest/'eye-audit').mkdir(exist_ok=True)
 for path in (src/'eye-audit').iterdir():
  if path.suffix in {'.json','.png','.webp'}:shutil.copy2(path,dest/'eye-audit'/path.name)
 native=repo/overrides.get(slug,str((src/'Animal - Painted Species Review.blend').relative_to(repo)))
 shutil.copy2(native,dest/'Animal - Painted Species Review.blend')
 provenance={'slug':slug,'source':str(native.relative_to(repo)),'source_sha256':hashlib.sha256(native.read_bytes()).hexdigest(),'scope':'Local reference-directed appearance work; body choice and original rig/actions retained. Independent visual inspection required.','appearance_approved':False}
 for name in ['visual-source.json','revision-source.json']:(dest/name).write_text(json.dumps(provenance,indent=2)+'\n')
 land=json.loads((dest/'face-landmarks.json').read_text());nose_radius,iris_scale,pupil=settings[slug]
 for side in ['right','left']:
  eye=land[side+'_eye'];N=np.array(eye['fit_frame']['normal']);N/=np.linalg.norm(N);H=np.cross(N,[0.,0.,1.]);H/=np.linalg.norm(H);V=np.cross(H,N)
  eye['fit_frame']={'normal':N.tolist(),'horizontal':H.tolist(),'vertical':V.tolist()}
 land['physical_eye_revision']={'method':'Buried physical iris and anatomically shaped stitched lids. Existing centres are provisional until front/profile photo comparison.','iris_radius_factor':1.3,'sculpt_socket_clearance':True,'nose_radius':nose_radius,'nasal_disc':True,'iris_scale':iris_scale,'round_pupil_radius':pupil,'reference_comparison_pending':True}
 land['anatomical_orbit']={'globe_radius_factor':1.,'burial':.82,'iris_radius':.68,'upper_aperture':.52,'lower_aperture':.55,'outer_skin_radius':2.65,'globe_recess_front':0.,'stitched':True}
 if slug=='ocelot':land['physical_eye_revision']['pupil_shape']='vertical'
 (dest/'face-landmarks.json').write_text(json.dumps(land,indent=2)+'\n');print('PREPARED',slug,flush=True)
