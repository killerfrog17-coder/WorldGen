"""Small morning review index; no game launch or inference."""
import argparse,csv,json
from pathlib import Path
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,default=repo/'artifacts/fauna-overnight-20261024')
a=ap.parse_args();root=a.root.resolve();plan=json.loads((repo/'docs/fauna/asset-plan.json').read_text())['rows']
ready=[];pending=[];rows=[]
for item in plan:
    slug=item['slug'];folder=root/slug;meta=folder/'audition.json';check=folder/'native-checks.json'
    status=item['asset_status'];path='';notes=''
    if status=='queued':
        if meta.exists() and check.exists():
            data=json.loads(meta.read_text());path=str(folder/'Animal - Raw Audition.blend');status='ready_for_owner_review'
            ready.append({'slug':slug,'name':item['animal'],'priority':item['priority_rank'],'path':path,'candidates':len(data['candidates'])})
            notes='; '.join(data.get('motion_gaps',[]))
        else:
            status='in_progress_or_waiting';pending.append(slug)
    elif status=='accepted_baseline':path=str(repo/'artifacts/fauna-assets/deer/Deer - Prototype.blend')
    rows.append([item['priority_rank'],item['animal'],status,path,notes])
index={'ready':ready,'pending':pending,'counts':{'catalogue':len(plan),'new_reviews_ready':len(ready),'new_candidates_ready':sum(x['candidates'] for x in ready),'accepted_deer_baseline':1,'unpainted':True},'notes':'File/controls pass is not visual acceptance. Individual motions may be missing; see per-animal audition.json.'}
(root/'review-index.json').write_text(json.dumps(index,indent=2))
with (root/'REVIEW_INDEX.csv').open('w',newline='',encoding='utf-8-sig') as f:
    w=csv.writer(f);w.writerow(['Rank','Animal','Status','Blender file','Motion gaps']);w.writerows(rows)
print(json.dumps(index['counts']))
