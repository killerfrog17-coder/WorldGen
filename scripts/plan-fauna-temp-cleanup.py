"""Owner-authorized local artifact cleanup; retain current assets and provenance."""
import collections,hashlib,json,os,re
from pathlib import Path

repo=Path(__file__).resolve().parent.parent;root=(repo/'artifacts').resolve()
choices=json.loads((repo/'docs/fauna/OWNER_SELECTIONS.json').read_text())['animals']
protected=set();fingerprints={};targets={}
def protect(path,hash_file=False):
 p=Path(path).resolve()
 if not p.exists():return
 protected.add(str(p))
 if hash_file and str(p) not in fingerprints and p.is_file():fingerprints[str(p)]=hashlib.sha256(p.read_bytes()).hexdigest()
def add(path,reason):
 p=path.resolve()
 assert p.is_relative_to(root) and p!=root,p
 if p.exists():targets[str(p)]=reason

# Every current/native source outside explicitly rejected archives remains.
for folder in ['fauna-overnight-20261024','fauna-painted-20261024','fauna-review-revisions','fauna-anatomy-revision','fauna-visual-corrections','fauna-repairs','fauna-assets','fauna-owner-fixes-20261025']:
 r=root/folder
 for p in r.rglob('*.blend'):
  if not any('previous-owner-rejected' in part or part.startswith('rejected-') for part in p.relative_to(r).parts):protect(p,True)
for folder in ['fauna-assets','fauna-motion-sources','fauna-phone-media','fauna-phone-review/dist']:
 r=root/folder
 for p in r.rglob('*'):
  if p.is_file():protect(p,p.suffix.lower() in {'.png','.jpg','.webp','.mp4','.glb','.npz','.blend'})
# Current saved Blender set and source files are retained verbatim.
current=json.loads((root/'fauna-anatomy-revision/review-index.json').read_text())['ready']
for row in current:protect(row['path'],True)
raw=root/'fauna-overnight-20261024';selected={}
for slug,row in choices.items():
 nums={int(x) for x in row.get('selected_models',[]) if 1<=int(x)<=4}
 if nums:selected[slug]=nums
# Retain any selected number still used by a saved appearance/source baseline.
for folder in ['fauna-painted-20261024','fauna-review-revisions','fauna-anatomy-revision']:
 for p in (root/folder).glob('*/paint-state.json'):
  d=json.loads(p.read_text());n=d.get('source_candidate')
  if isinstance(n,int) and 1<=n<=4:selected.setdefault(p.parent.name,set()).add(n)
for slug,nums in selected.items():
 for n in range(1,5):
  p=raw/slug/f'candidate-{n}'
  if n not in nums:add(p,'Unselected generated alternative; selected candidates and native comparisons retained')
  elif p.exists():
   for q in p.iterdir():
    if q.is_file() and q.name in {'model.glb','generated-rig.npz','generated-rig.json','raw.json'}:protect(q,True)
for p in raw.glob('*/previous-owner-rejected*'):add(p,'Superseded owner-rejected reroll archive')
for p in raw.rglob('*.npz'):
 if any(x in p.name for x in ['rejected-topology-','before-manual-','neural-rejected','before-owner-']):add(p,'Rejected or superseded rig intermediate')
# These are generated contact/preview exports, not paint atlases or source refs.
for folder in ['fauna-painted-20261024','fauna-overnight-20261024']:
 for p in (root/folder).glob('*/previews'):add(p,'Regenerable superseded audition/first-colour screenshots')
# Keep one nightly executable from the existing retention manifest, remove only
# Unity's explicitly non-shipping debug/source backup child, never runtime data.
retained=json.loads((root/'checkpoint-retention.json').read_text())['Retained']
for row in retained:
 build=Path(row['path'])
 for name in ['WorldGen.exe','GameAssembly.dll','UnityPlayer.dll']:protect(build/name,True)
 add(build/'WorldGen_BackUpThisFolder_ButDontShipItWithYourGame','Unity non-shipping symbols and generated C++ backup; nightly executable retained')
add(root/'tools/blender-4.5.15/blender-4.5.15-windows-x64.zip','Installer archive; extracted Blender retained')
add(root/'tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.pdb','Blender debug symbols; application and libraries retained')
add(root/'fauna-phone-review-deploy.tar.gz','Already successfully deployed package; pushed source and current viewer assets retained')
for folder in ['tools','fauna-overnight-20261024','fauna-painted-20261024','fauna-review-revisions','fauna-anatomy-revision']:
 for path,dirs,files in os.walk(root/folder,followlinks=False):
  for n in list(dirs):
   if n=='.git':dirs.remove(n)
   elif n=='__pycache__':add(Path(path)/n,'Regenerable Python bytecode cache');dirs.remove(n)
# Collapse children beneath a planned directory so each byte is counted once.
ordered=sorted(targets,key=lambda x:(len(Path(x).parts),x));accepted=[]
protected_ancestors={str(q).lower() for name in protected for q in [Path(name),*Path(name).parents]}
for name in ordered:
 p=Path(name)
 if any(p.is_relative_to(Path(x)) for x in accepted):continue
 if name.lower() in protected_ancestors:raise RuntimeError('Protected path overlap: '+name)
 accepted.append(name)
rows=[]
for name in accepted:
 p=Path(name);files=[p] if p.is_file() else [x for x in p.rglob('*') if x.is_file()]
 assert not p.is_symlink()
 rows.append({'path':name,'reason':targets[name],'bytes':sum(x.stat().st_size for x in files),'files':len(files)})
plan={'scope':'Owner accepts current placeholders and requests temp/old-iteration cleanup. Preserve all current/source native models, selected raw candidates, relevant textures, source motion, review assets and retained nightly executables. Local artifacts only.','allowed_root':str(root),'protected':sorted(protected),'protected_sha256':fingerprints,'current_animals':len(current),'retained_builds':retained,'remove':rows,'planned_bytes':sum(x['bytes'] for x in rows)}
dest=root/'fauna-cleanup-plan-20261026.json';dest.write_text(json.dumps(plan,indent=2)+'\n')
groups=collections.defaultdict(lambda:[0,0])
for row in rows:groups[row['reason']][0]+=row['bytes'];groups[row['reason']][1]+=1
print(json.dumps({'targets':len(rows),'planned_gib':round(plan['planned_bytes']/2**30,3),'protected_files_hashed':len(fingerprints),'current_animals':len(current),'groups':[{ 'reason':k,'gib':round(v[0]/2**30,3),'targets':v[1]} for k,v in groups.items()]},indent=2))
