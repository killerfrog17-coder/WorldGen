"""Small morning review index; no game launch or inference."""
import argparse,csv,json
from pathlib import Path
from fauna_motion_sources import source_for
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,default=repo/'artifacts/fauna-overnight-20261024')
a=ap.parse_args();root=a.root.resolve();plan=json.loads((repo/'docs/fauna/asset-plan.json').read_text())['rows']
ready=[];pending=[];rows=[];raw_motion_pending=[]
queue_path=root/'queue-state.json';queue=json.loads(queue_path.read_text()).get('animals',{}) if queue_path.exists() else {}
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
            q=queue.get(slug,{})
            if q.get('status')=='failed':status='generation_failed';notes=q.get('error','')
            elif q.get('status')=='models_rigged_motion_pending':
                status='models_rigged_motion_pending';notes='Four raw meshes/rigs available; animations and Blender motion review still missing';raw_motion_pending.append(slug)
            elif item['family']!='microfly' and source_for(item,repo) is None:status='waiting_for_motion_source';notes='Suitable CC0 motion still needed; see MOTION_COVERAGE.csv'
    elif status=='accepted_baseline':path=str(repo/'artifacts/fauna-assets/deer/Deer - Prototype.blend')
    rows.append([item['priority_rank'],item['animal'],status,path,notes])
index={'ready':ready,'pending':pending,'raw_motion_pending':raw_motion_pending,'counts':{'catalogue':len(plan),'new_reviews_ready':len(ready),'new_candidates_ready':sum(x['candidates'] for x in ready),'models_rigged_motion_pending':len(raw_motion_pending),'accepted_deer_baseline':1,'unpainted':True},'notes':'File/controls pass is not visual acceptance. Motion-pending raw assets do not count as animated reviews. Individual motions may be missing; see per-animal audition.json.'}
(root/'review-index.json').write_text(json.dumps(index,indent=2))
with (root/'REVIEW_INDEX.csv').open('w',newline='',encoding='utf-8-sig') as f:
    w=csv.writer(f);w.writerow(['Rank','Animal','Status','Blender file','Motion gaps']);w.writerows(rows)
print(json.dumps(index['counts']))
