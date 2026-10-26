"""Inventory one corrected native for the existing two-clip phone reviewer."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy
repo=Path(__file__).resolve().parent.parent;sys.path.insert(0,str(repo/'scripts'));from fauna_phone_clips import review_clip_labels
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--blend',type=Path,required=True);ap.add_argument('--out',type=Path,required=True);ap.add_argument('--face-root',type=Path);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);source=a.blend.resolve();assert source.is_relative_to(repo);digest=hashlib.sha256(source.read_bytes()).hexdigest();item=next(x for x in json.loads((repo/'artifacts/fauna-phone-media/plan.json').read_text())['animals'] if x['slug']==a.slug)
bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0];fps=s.render.fps/s.render.fps_base;clips=[]
for label in review_clip_labels(e['actions']):
 action=bpy.data.actions[e['actions'][label]];first,last=map(float,action.frame_range);clips.append({'label':label,'action':action.name,'first':first,'last':last,'native_seconds':(last-first)/fps,'seconds':max(2.,(last-first)/fps)})
item.update(source=str(source.relative_to(repo)),source_sha256=digest,source_fps=fps,clips=clips,coats=[{'index':i,'label':c['label'],'hypothetical':bool(c.get('hypothetical') or 'hypothetical' in c['label'].lower())} for i,c in enumerate(e['painting_coats'])])
if a.face_root:
 item['face_views']=[]
 for view in ['front','right','left']:
  target=a.face_root.resolve()/(view+'-after.webp');assert target.exists();item['face_views'].append({'label':'Face — '+view,'file':str(target.relative_to(repo)),'sha256':hashlib.sha256(target.read_bytes()).hexdigest()})
a.out.parent.mkdir(parents=True,exist_ok=True);a.out.write_text(json.dumps({'animals':[item]},indent=2)+'\n');assert hashlib.sha256(source.read_bytes()).hexdigest()==digest;print('FEEDBACK PLAN',a.slug,len(item['coats']),len(clips))
