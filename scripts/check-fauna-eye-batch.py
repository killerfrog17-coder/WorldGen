"""Finite current-eye attachment checks and fresh body/coat views. No native writes."""
import argparse,hashlib,json,subprocess,time,os
from pathlib import Path
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024';out=root/'inspection'
blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
ap=argparse.ArgumentParser();ap.add_argument('--slugs',nargs='*');ap.add_argument('--watch',action='store_true');a=ap.parse_args();jobs=json.loads((root/'paint-queue.json').read_text())['jobs'];jobs=[j for j in jobs if not a.slugs or j['slug'] in a.slugs]
from fauna_eye_profiles import pipeline_signature
expected_pipeline=pipeline_signature(repo)
status={'pid':os.getpid(),'completed':[],'errors':{},'warnings':{},'total':len(jobs)}
def save():(out/'eye-final-qa-progress.json').write_text(json.dumps(status,indent=2)+'\n')
def producer_progress():
 for attempt in range(50):
  try:return json.loads((out/'eye-revision-progress.json').read_text())
  except (PermissionError,json.JSONDecodeError):time.sleep(.02)
 raise RuntimeError('Producer status remained unreadable')
def process(j):
 slug=j['slug'];folder=root/slug;status['current']=slug;save()
 try:
  blend=folder/'Animal - Painted Species Review.blend';digest=hashlib.sha256(blend.read_bytes()).hexdigest();revision=json.loads((folder/'eye-audit/revision.json').read_text());assert revision['blend_sha256']==digest
  prior=folder/'eye-audit/final-check.json'
  if not prior.exists() or json.loads(prior.read_text()).get('blend_sha256')!=digest:
   # The producer already rendered the exact current head. Keep that inspection
   # evidence byte-for-byte while refreshing body/coat views; EEVEE temporal
   # sampling can otherwise change harmless pixels and invalidate a viewed sheet.
   head_paths=[folder/f'eye-audit/{v}-after.png' for v in ['right','left','front']]+[folder/'eye-audit/after-renders.json']
   head_bytes={p:p.read_bytes() for p in head_paths}
   assert json.loads(head_bytes[head_paths[-1]])['source_sha256']==digest
   for script,args in [('check-fauna-finish-motion.py',['--slugs',slug]),('render-fauna-eye-results.py',['--slug',slug,'--bodies'])]:
    with (folder/'eye-audit'/('final-'+script+'.log')).open('w') as stream:
     result=subprocess.run([str(blender),'--background','--threads','2','--python-exit-code','1','--python',str(repo/'scripts'/script),'--',*args],cwd=repo,stdout=stream,stderr=subprocess.STDOUT)
    assert result.returncode==0,script
   assert hashlib.sha256(blend.read_bytes()).hexdigest()==digest
   for path,content in head_bytes.items():path.write_bytes(content)
   revision=json.loads((folder/'eye-audit/revision.json').read_text())
   if not revision['actual_after_vision_pending']:
    final_path=folder/'final-qa.json';final=json.loads(final_path.read_text());final.update(actually_visually_inspected=True,eye_visual_inspection=revision['visual_inspection']);final_path.write_text(json.dumps(final,indent=2)+'\n')
   prior.write_text(json.dumps({'blend_sha256':digest,'motion_checks':'paint-motion-checks.json','body_renders_refreshed':True},indent=2)+'\n')
  motion=json.loads((folder/'paint-motion-checks.json').read_text());assert motion['read_blend_sha256']==digest
  if motion['warnings']:status['warnings'][slug]=motion['warnings']
  if slug not in status['completed']:status['completed'].append(slug)
  status['errors'].pop(slug,None)
 except Exception as exc:status['errors'][slug]=str(exc)
 save()
seen={}
while True:
 worked=False
 for j in jobs:
  slug=j['slug'];folder=root/slug;receipt=folder/'eye-audit/revision.json'
  if a.watch:
   producer=producer_progress()
   if slug in producer.get('active',{}):continue
   if not receipt.exists():continue
   revision=json.loads(receipt.read_text())
   if revision.get('pipeline_sha256')!=expected_pipeline:continue
   digest=revision['blend_sha256']
   if seen.get(slug)==digest:continue
  process(j);worked=True
  if receipt.exists():seen[slug]=json.loads(receipt.read_text())['blend_sha256']
 if not a.watch:break
 producer=producer_progress()
 if producer.get('stage')=='finished' and not producer.get('active') and not worked:break
 if not worked:time.sleep(2)
status.update(current=None,stage='finished');save()
