"""Read current native assets into a complete phone-review media inventory."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024';out=repo/'artifacts/fauna-phone-media';out.mkdir(exist_ok=True)
rows=[{'slug':'deer','name':'Deer','path':str(repo/'artifacts/fauna-assets/deer/Deer - Prototype.blend'),'accepted':True}]+json.loads((root/'review-index.json').read_text())['ready']
plan=[]
for number,row in enumerate(rows,1):
 source=Path(row['path']);digest=hashlib.sha256(source.read_bytes()).hexdigest();bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0]
 fps=s.render.fps/s.render.fps_base;clips=[]
 demos=set(e.get('demos',{}).values())
 for label,name in e['actions'].items():
  if name in demos or label.lower() in {'showcase','locomotion','demo','showcase loop','locomotion loop'}:continue
  action=bpy.data.actions[name];first,last=map(float,action.frame_range)
  clips.append({'label':label,'action':name,'first':first,'last':last,'native_seconds':(last-first)/fps,'seconds':max(2.,(last-first)/fps)})
 coats=e.get('painting_coats') or e['approved_art']['coats'];variants=[];seen=set()
 if row['slug']=='deer':
  for i,rack in enumerate(e['approved_art']['antlers']):variants.append({'coat':0,'rack':i,'label':rack.get('label',rack.get('name',f'Antler rack {i+1}'))})
  variants.append({'coat':0,'rack':-1,'label':'No antlers'})
 else:
  for ci,coat in enumerate(coats):
   options=coat.get('appendage_variants') or [{'label':coat.get('trait_label','Selected form'),'objects':coat.get('visible_extras',[])}]
   for vi,option in enumerate(options):
    signature=json.dumps({'objects':sorted(option.get('objects',[])),'morphs':option.get('body_morphs',{})},sort_keys=True)
    if signature in seen:continue
    seen.add(signature)
    if option.get('objects') or option.get('body_morphs') or len(options)>1:variants.append({'coat':ci,'variant':vi,'label':option.get('label','Appendage variation')})
 item={'number':number,'slug':row['slug'],'name':row['name'],'source':str(source.relative_to(repo)),'source_sha256':digest,'procedural':bool(row.get('procedural')),'accepted':bool(row.get('accepted')),'entry':0,'source_fps':fps,'coats':[{'index':i,'label':c['label'],'hypothetical':bool(c.get('hypothetical') or 'hypothetical' in c['label'].lower())} for i,c in enumerate(coats)],'extras':variants,'clips':clips}
 plan.append(item)
 assert hashlib.sha256(source.read_bytes()).hexdigest()==digest
(out/'plan.json').write_text(json.dumps({'animals':plan,'count':len(plan),'coats':sum(len(x['coats']) for x in plan),'extra_examples':sum(len(x['extras']) for x in plan),'clips':sum(len(x['clips']) for x in plan),'video_seconds':sum(c['seconds'] for x in plan for c in x['clips'])},indent=2)+'\n')
print('PHONE PLAN',len(plan),'animals',sum(len(x['clips']) for x in plan),'clips',sum(len(x['coats']) for x in plan),'coats',flush=True)
