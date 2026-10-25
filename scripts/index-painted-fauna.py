"""Index technically checked colour passes without choosing or approving art."""
import hashlib,json
from datetime import datetime
from pathlib import Path

repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024'
choices_path=repo/'docs/fauna/OWNER_SELECTIONS.json';choices=json.loads(choices_path.read_text())
plan=json.loads((repo/'docs/fauna/asset-plan.json').read_text())['rows'];rank={x['slug']:x['priority_rank'] for x in plan}
queue=json.loads((root/'paint-queue.json').read_text());ready=[]
# Incorporate later owner choices without discarding existing finishing state.
jobs={job['slug']:job for job in queue['jobs']}
for row in plan:
    slug=row['slug'];chosen=choices['animals'].get(slug,{})
    if not chosen.get('model_selected') or not chosen.get('model_sha256'):continue
    job=jobs.setdefault(slug,{'slug':slug,'animal':row.get('animal',row.get('name',slug)),'status':'queued_for_colouring','appearance_approved':False})
    job.update(priority=row['priority_rank'],selected_models=chosen['selected_models'],model_sha256=chosen['model_sha256'],rig_sha256=chosen['rig_sha256'])
queue['jobs']=sorted(jobs.values(),key=lambda job:job['priority'])
for job in queue['jobs']:
    slug=job['slug'];folder=root/slug;state_path=folder/'paint-state.json';check_path=folder/'paint-native-checks.json'
    if not state_path.exists():continue
    state=json.loads(state_path.read_text());chosen=choices['animals'][slug]
    assert state['source_candidate'] in chosen['selected_models']
    assert state['source_model_sha256']==chosen['model_sha256'] and state['source_rig_sha256']==chosen['rig_sha256']
    approved=bool(state.get('appearance_approved') or chosen.get('appearance_approved'))
    job['status']=state['status'];job['appearance_approved']=approved
    if approved:continue  # Indexing never revokes a later owner decision.
    chosen.update(painting_started=True,painting_status=state['status'],paint_review=str((folder/'Animal - Painted Species Review.blend').relative_to(repo)))
    if not check_path.exists() or not state.get('uv_atlases_installed'):continue
    bake_path=folder/'coat-bake.json'
    if not bake_path.exists():continue
    bake=json.loads(bake_path.read_text())
    required_masks=bake.get('required_masks',['nose'])
    if any(bake.get('mask_pixels',{}).get(key,1)<=0 for key in required_masks):continue
    check=json.loads(check_path.read_text());blend=folder/'Animal - Painted Species Review.blend'
    if check.get('checked_blend_sha256')!=hashlib.sha256(blend.read_bytes()).hexdigest():continue
    assert check['source_bind_geometry_skin_rig_exact'] and check['source_actions_exact']
    if not state.get('facial_objects') and state.get('facial_feature_policy')!='surface_only_arthropod':continue
    ready.append({'slug':slug,'name':job['animal'],'priority':rank[slug],'path':str(blend),'candidates':1,'selected_raw_candidate':state['source_candidate'],'coats':len(state['coats']),'appearance_scope':state.get('appearance_scope','Selected body only; no age-proportion or ecology implementation inferred.'),'appearance_approved':False,'motion_approved':False})
    final_path=folder/'final-qa.json'
    if final_path.exists():
        final=json.loads(final_path.read_text())
        ready[-1]['appearance_review_ready']=bool(final.get('checked_blend_sha256')==check['checked_blend_sha256'] and final.get('all_sampled_source_clips_finite') and not final.get('motion_warnings') and final.get('actually_visually_inspected'))
now=datetime.now().astimezone().isoformat();queue['updated']=now
queue['counts']={'selected':len(queue['jobs']),'painting_started':sum((root/x['slug']/'paint-state.json').exists() for x in queue['jobs']),'checked_first_passes':len(ready),'appearance_approved':sum(bool(x.get('appearance_approved')) for x in queue['jobs'])}
procedural_ready=[]
for slug,revision in choices.get('procedural_revisions',{}).items():
    folder=root/slug;proof_path=folder/'paint-procedural-checks.json';blend=folder/'Animal - Painted Species Review.blend'
    if not proof_path.exists() or not blend.exists():continue
    proof=json.loads(proof_path.read_text())
    if proof['checked_blend_sha256']!=hashlib.sha256(blend.read_bytes()).hexdigest():continue
    if not proof.get('source_bind_geometry_skin_rig_actions_exact') or not proof.get('all_coats_reachable'):continue
    raw=repo/'artifacts/fauna-overnight-20261024'/slug/'Animal - Raw Audition.blend'
    if hashlib.sha256(raw.read_bytes()).hexdigest()!=proof['source_blend_sha256']:continue
    row=next(r for r in plan if r['slug']==slug)
    procedural_ready.append({'slug':slug,'name':row.get('animal',slug),'priority':rank[slug],'path':str(blend),'candidates':proof['candidate_count'],'coats':proof['coats_per_candidate'],'procedural':True,'appearance_approved':False,'motion_approved':False,'appearance_scope':proof['scope']})
    procedural_ready[-1]['appearance_review_ready']=bool(proof.get('actually_visually_inspected'))
    revision.update(painting_started=True,painting_status='procedural_coloured_working',paint_review=str(blend.relative_to(repo)),paint_native_sha256=proof['checked_blend_sha256'])
queue['counts']['procedural_checked_first_passes']=len(procedural_ready)
queue['counts']['appearance_reviews_ready']=sum(bool(row.get('appearance_review_ready')) for row in ready+procedural_ready)
queue['started']=queue['counts']['painting_started']
queue['limits']='Separate selected-body colour copies. Checked first passes still need owner appearance approval; raw motion acceptance is independent.'
index={'updated':now,'ready':sorted(ready+procedural_ready,key=lambda x:x['priority']),'notes':'First colour passes on owner-selected bodies and accepted procedural tiny fauna. Body/rig/source-motion preservation checked. Appearance and natural motion remain unapproved; raw debris and raw gait defects are retained.'}
for path,value in [(root/'paint-queue.json',queue),(root/'review-index.json',index),(choices_path,choices)]:
    temporary=path.with_suffix(path.suffix+'.tmp');temporary.write_text(json.dumps(value,indent=2)+'\n');temporary.replace(path)
print(json.dumps(queue['counts']))
