"""Track every visual animal without inheriting rejected appearance pass flags."""
import json
from pathlib import Path
repo=Path(__file__).resolve().parent.parent
base=json.loads((repo/'artifacts/fauna-phone-media/plan.json').read_text())
path=repo/'docs/fauna/VISUAL_CORRECTIONS.json'
previous=json.loads(path.read_text()) if path.exists() else {'animals':[]}
retained={x['slug']:x for x in previous['animals']}
rows=[]
for animal in base['animals']:
    slug=animal['slug'];row=retained.get(slug,{'number':animal['number'],'slug':slug,'name':animal['name'],'status':'queued','reference_comparison_completed':False,'appearance_approved':False})
    if slug=='deer':row.update(status='protected_owner_approved_deer',appearance_approved=True)
    root=repo/'artifacts/fauna-visual-corrections'/slug
    if (root/'visual-source.json').exists() and row['status']=='queued':row['status']='working_copy_not_published'
    rows.append(row)
data={'scope':'Owner asks every animal be reconsidered assuming appearance is bad. No inherited technical or lenient visual pass constitutes appearance approval. Existing phone selections/Done/notes retained.','animation_policy':'Existing animations accepted for now. Do not repair/rebake; future long pass ordered by owner, using AI video.','animals':rows}
path.write_text(json.dumps(data,indent=2)+'\n');print('Visual roster:',len(rows),'working:',sum(x['status']=='working_copy_not_published' for x in rows),'queued:',sum(x['status']=='queued' for x in rows))
