"""Index technically checked colour passes without choosing or approving art."""
import hashlib,json
from datetime import datetime
from pathlib import Path

repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024'
choices_path=repo/'docs/fauna/OWNER_SELECTIONS.json';choices=json.loads(choices_path.read_text())
plan=json.loads((repo/'docs/fauna/asset-plan.json').read_text())['rows'];rank={x['slug']:x['priority_rank'] for x in plan}
queue=json.loads((root/'paint-queue.json').read_text());ready=[]
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
    if bake.get('mask_pixels',{}).get('nose',1)<=0:continue
    check=json.loads(check_path.read_text());blend=folder/'Animal - Painted Species Review.blend'
    if check.get('checked_blend_sha256')!=hashlib.sha256(blend.read_bytes()).hexdigest():continue
    assert check['source_bind_geometry_skin_rig_exact'] and check['source_actions_exact']
    if not state.get('facial_objects'):continue
    ready.append({'slug':slug,'name':job['animal'],'priority':rank[slug],'path':str(blend),'candidates':1,'selected_raw_candidate':state['source_candidate'],'coats':len(state['coats']),'appearance_scope':state.get('appearance_scope','Selected body only; no age-proportion or ecology implementation inferred.'),'appearance_approved':False,'motion_approved':False})
now=datetime.now().astimezone().isoformat();queue['updated']=now
queue['counts']={'selected':len(queue['jobs']),'painting_started':sum((root/x['slug']/'paint-state.json').exists() for x in queue['jobs']),'checked_first_passes':len(ready),'appearance_approved':sum(bool(x.get('appearance_approved')) for x in queue['jobs'])}
index={'updated':now,'ready':sorted(ready,key=lambda x:x['priority']),'notes':'First colour passes on owner-selected bodies only. Body/rig/source-motion preservation checked. Appearance and natural motion remain unapproved; raw debris and raw gait defects are retained.'}
for path,value in [(root/'paint-queue.json',queue),(root/'review-index.json',index),(choices_path,choices)]:
    temporary=path.with_suffix(path.suffix+'.tmp');temporary.write_text(json.dumps(value,indent=2)+'\n');temporary.replace(path)
print(json.dumps(queue['counts']))
