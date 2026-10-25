"""Small morning review index; no game launch or inference."""
import argparse,csv,json,datetime
from pathlib import Path
from fauna_motion_sources import source_for
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,default=repo/'artifacts/fauna-overnight-20261024')
a=ap.parse_args();root=a.root.resolve();plan=json.loads((repo/'docs/fauna/asset-plan.json').read_text())['rows']
ready=[];pending=[];rows=[];raw_motion_pending=[];motion_trials_pending=[]
choices=json.loads((repo/'docs/fauna/OWNER_SELECTIONS.json').read_text())
queue_path=root/'queue-state.json';queue=json.loads(queue_path.read_text()).get('animals',{}) if queue_path.exists() else {}
for item in plan:
    slug=item['slug'];folder=root/slug;meta=folder/'audition.json';check=folder/'native-checks.json'
    status=item['asset_status'];path='';notes=''
    if status=='queued':
        revision=choices.get('pending_revisions',{}).get(slug,{})
        redo=revision.get('explicit_repeat_review') and not revision.get('status','').startswith('manual_')
        request=folder/'owner-reroll-request-current.json';fresh_redo=True
        if redo:
            fresh_redo=False
            if request.exists() and check.exists():
                requested=datetime.datetime.fromisoformat(json.loads(request.read_text())['time']).timestamp()
                fresh_redo=check.stat().st_mtime>=requested
        if fresh_redo and meta.exists() and check.exists() and not (folder/'review-rebuild.json').exists() and not (folder/'source-trial-pending.json').exists():
            data=json.loads(meta.read_text());path=str(folder/'Animal - Raw Audition.blend');status='ready_for_owner_review'
            selected=choices.get('animals',{}).get(slug,{})
            if selected.get('manual_fix_blend'):
                fixed=repo/selected['manual_fix_blend']
                if fixed.exists():path=str(fixed)
            ready.append({'slug':slug,'name':item['animal'],'priority':item['priority_rank'],'path':path,'candidates':len(data['candidates'])})
            notes='; '.join(data.get('motion_gaps',[]))
            warning_file=folder/'visual-warnings.json'
            if warning_file.exists():
                screening=json.loads(warning_file.read_text());warnings=screening.get('warnings',[])
                ready[-1]['visual_warnings']=warnings
                ready[-1]['screened_out_candidates']=screening.get('candidate_recommendation_exclusions',[])
                warning_text='; '.join('Candidate '+str(w['candidate'])+' '+w.get('clip','')+': '+w['reason'] for w in warnings)
                notes='; '.join(x for x in (notes,warning_text) if x)
        else:
            status='in_progress_or_waiting';pending.append(slug)
            q=queue.get(slug,{})
            if redo and not fresh_redo:status='owner_requested_reference_redo';notes=revision.get('reason','Fresh owner-requested four-model set pending')
            elif q.get('status')=='failed':status='generation_failed';notes=q.get('error','')
            elif (folder/'source-trial-pending.json').exists():status='experimental_motion_trial_pending';notes='Source trial awaits technical inspection; excluded from animation-ready count';motion_trials_pending.append(slug)
            elif (folder/'review-rebuild.json').exists():
                status='refreshing_raw_review'
                rebuilding=json.loads((folder/'review-rebuild.json').read_text())
                notes='Previous unapproved review retained; rebuilding: '+rebuilding.get('reason','raw preparation correction')
            elif q.get('status')=='models_rigged_motion_pending':
                status='models_rigged_motion_pending';notes='Four raw meshes/rigs available; animations and Blender motion review still missing';raw_motion_pending.append(slug)
                rejection=folder/'motion-trial-rejection.json'
                if rejection.exists():notes+='; '+json.loads(rejection.read_text())['reason']
            elif item['family']!='microfly' and source_for(item,repo) is None:status='waiting_for_motion_source';notes='Suitable CC0 motion still needed; see MOTION_COVERAGE.csv'
    elif status=='accepted_baseline':path=str(repo/'artifacts/fauna-assets/deer/Deer - Prototype.blend')
    rows.append([item['priority_rank'],item['animal'],status,path,notes])
index={'ready':ready,'pending':pending,'raw_motion_pending':raw_motion_pending,'motion_trials_pending':motion_trials_pending,'counts':{'catalogue':len(plan),'new_reviews_ready':len(ready),'new_candidates_ready':sum(x['candidates'] for x in ready),'models_rigged_motion_pending':len(raw_motion_pending),'motion_trials_pending':len(motion_trials_pending),'accepted_deer_baseline':1,'unpainted':True},'notes':'File/controls pass is not visual acceptance. Motion-pending raw assets and uninspected source trials do not count as animated reviews. Individual motions may be missing; see per-animal audition.json.'}
(root/'review-index.json').write_text(json.dumps(index,indent=2))
with (root/'REVIEW_INDEX.csv').open('w',newline='',encoding='utf-8-sig') as f:
    w=csv.writer(f);w.writerow(['Rank','Animal','Status','Blender file','Motion gaps']);w.writerows(rows)
print(json.dumps(index['counts']))
