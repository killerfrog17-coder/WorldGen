"""Package every selected animal's views, all coats/extras and source motion clips."""
import argparse,hashlib,json,shutil
from pathlib import Path
from fauna_phone_clips import review_clip_labels
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-phone-media';site=repo/'artifacts/fauna-phone-review/dist'
ap=argparse.ArgumentParser();ap.add_argument('--partial',action='store_true');a=ap.parse_args();plan=json.loads((root/'plan.json').read_text());animals=[];ledger=[];complete=0
def copy(source,target,digest):
 assert source.exists() and hashlib.sha256(source.read_bytes()).hexdigest()==digest,source
 target.parent.mkdir(exist_ok=True,parents=True)
 if not target.exists() or hashlib.sha256(target.read_bytes()).hexdigest()!=digest:shutil.copy2(source,target)
 ledger.append({'source':str(source.relative_to(repo)),'file':str(target.relative_to(site)),'sha256':digest})
for item in plan['animals']:
 folder=root/item['slug'];receipt=folder/'receipt.json';ready=False
 if receipt.exists():
  r=json.loads(receipt.read_text());ready=r.get('complete') and r['source_sha256']==item['source_sha256']
 row={k:item[k] for k in ['number','slug','name','accepted']};row.update(asset_revision=item['source_sha256'],pictures=[],variations=[],clips=[],ready=bool(ready))
 if ready:
  assert hashlib.sha256((repo/item['source']).read_bytes()).hexdigest()==item['source_sha256'];complete+=1
  for im in r['stills']:
   target=site/'animals'/item['slug']/im['file'];copy(folder/im['file'],target,im['sha256']);record={'label':im['label'],'url':str(target.relative_to(site)).replace('\\','/')}
   if im['view'] in ['coat','extra']:
    record['type']='paint' if im['view']=='coat' else 'extra';record['caption']='Paint job' if im['view']=='coat' else 'Extra shape · '+item['coats'][im['coat']]['label'];row['variations'].append(record)
   else:row['pictures'].append(record)
  if item['slug']=='deer':row['pictures'].append({'label':'Face & eyes','url':'media/D-face.webp'})
  elif not item['procedural']:
   for view,label in [('front','Face — front'),('right','Face — right'),('left','Face — left')]:
    path=site/f"eyes/{item['slug']}-{view}-after.webp";assert path.exists();row['pictures'].append({'label':label,'url':str(path.relative_to(site)).replace('\\','/')})
  chosen=review_clip_labels([clip['label'] for clip in r['videos']])
  for label in chosen:
   clip=next(clip for clip in r['videos'] if clip['label']==label)
   target=site/'animals'/item['slug']/clip['file'];copy(folder/clip['file'],target,clip['sha256']);row['clips'].append({'label':clip['label'],'url':str(target.relative_to(site)).replace('\\','/'),'seconds':clip['seconds']})
  assert len([v for v in row['variations'] if v['type']=='paint'])==len(item['coats'])
  assert len([v for v in row['variations'] if v['type']=='extra'])==len(item['extras'])
  assert len(row['clips'])==2
  # Prune only obsolete published previews; native actions and render masters stay.
  published_folder=(site/'animals'/item['slug']).resolve();allowed=(site/'animals').resolve()
  assert published_folder.is_relative_to(allowed)
  keep={Path(clip['url']).name for clip in row['clips']}
  for old in published_folder.glob('clip-*.mp4'):
   assert old.resolve().is_relative_to(allowed) and old.is_file()
   if old.name not in keep:old.unlink()
 else:assert a.partial,'Not all animal media is ready: '+item['slug']
 animals.append(row)
assert len(animals)==177
dest=site/'animals';dest.mkdir(exist_ok=True);(dest/'index.json').write_text(json.dumps({'animals':animals,'count':177,'ready':complete,'coats':sum(sum(v['type']=='paint' for v in r['variations']) for r in animals),'extras':sum(sum(v['type']=='extra' for v in r['variations']) for r in animals),'clips':sum(len(r['clips']) for r in animals)},ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8')
if not (site/'deer.html').exists():shutil.copy2(site/'index.html',site/'deer.html')
for name in ['index.html','viewer.js','viewer.css']:shutil.copy2(repo/'scripts/fauna-phone-viewer'/name,site/name)
(site.parent/'checks/viewer-inputs.json').write_text(json.dumps({'ready':complete,'files':ledger},indent=2)+'\n')
for name in ['overview.html','eyes.html']:
 path=site/name;text=path.read_text(encoding='utf-8')
 if 'Full animal viewer' not in text:text=text.replace('<main>','<main><p><a href="index.html">← Full animal viewer</a></p>',1)
 path.write_text(text,encoding='utf-8')
print(json.dumps({'animals':177,'ready':complete,'media_files':len(ledger),'partial':a.partial}))
