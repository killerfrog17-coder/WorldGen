"""Apply sourced appearance planning without changing raw model/motion scope."""
import csv,json
from pathlib import Path

def apply_refinements(rows,directory):
    path=Path(directory)/'SPECIES_VARIATION_PLANS.json'
    if not path.exists():return rows
    details=json.loads(path.read_text())['animals']
    for row in rows:
        row.pop('actual_model_status',None)  # Live artefact indexes own production status.
        detail=details.get(row['animal'])
        if detail is None:continue
        row['appearance_variation_plan']=detail
        row['texture_plan']['coats']=detail['coat_configurations']
        row['texture_plan']['coat_configuration_count']=len(detail['coat_configurations'])
        row['texture_plan']['reusable_swatch_count']=detail['surface_class_count']
        row['texture_plan']['asset_allocation']=detail['texture_assets']
        row['texture_plan']['anatomical_paint_details']=detail['paint_details']
        row['sex_age_season']=detail['sex_age_season'];row['extras']=detail['extras']
        row['hypothetical_variation']=detail['hypotheses']
        row['second_pass_status']='Targeted appearance/speciation planning with linked sources. Colouring is authorized for owner-selected models; live finishing status is in artifacts/fauna-painted-20261024/paint-queue.json. Motion and appearance approvals remain separate.'
        if row['asset_status']=='audio_existing':
            row['second_pass_status']='Audio-only sex/season/spatial-call/speciation plan refined with sources. No visual asset allocation or playback-bank changes.'
        elif row['asset_status']=='accepted_baseline':
            row['second_pass_status']='Accepted deer model/rig/materials preserved. The concurrent colouring queue applies to other owner-selected new animals.'
        elif row['asset_status']=='deferred':
            row['second_pass_status']='Deferred representation and future anatomy/lifecycle researched; zero current visual allocation. No textures, models or motions generated.'
            row['motion_source_plan']='Deferred: choose a matching body family and source only if this representation is activated; no current motion allocation.'
    return rows

def write_table(rows,path):
    columns=['Rank','Animal','Status','Representation','Triangles','Candidates','Swatches','Coats','Paint regions','Sex / age / season','Extras','Motion sources','Research status','Texture allocation','Plausible hypotheses','Geometry limits','Sources','Audio placement / variation']
    path=Path(path);temporary=path.with_suffix('.csv.tmp')
    with temporary.open('w',newline='',encoding='utf-8-sig') as f:
        w=csv.writer(f);w.writerow(columns)
        for x in rows:
            detail=x.get('appearance_variation_plan',{})
            w.writerow([x['priority_rank'],x['animal'],x['asset_status'],x['representation'],x['triangle_budget'],x['raw_candidates'],x['texture_plan']['reusable_swatch_count'],'; '.join(x['texture_plan']['coats']),'; '.join(x['texture_plan']['paint_zones']),x['sex_age_season'],x['extras'],x['motion_source_plan'],x['second_pass_status'],detail.get('texture_assets',''),x['hypothetical_variation'],detail.get('geometry_limits',''),'; '.join(detail.get('sources',[])),detail.get('acoustic_plan','')])
    temporary.replace(path)

if __name__=='__main__':
    directory=Path(__file__).resolve().parent.parent/'docs/fauna';path=directory/'asset-plan.json';data=json.loads(path.read_text())
    before=[tuple(x[k] for k in ('animal','priority_rank','family','asset_status','triangle_budget','raw_candidates')) for x in data['rows']]
    apply_refinements(data['rows'],directory)
    assert before==[tuple(x[k] for k in ('animal','priority_rank','family','asset_status','triangle_budget','raw_candidates')) for x in data['rows']]
    temporary=path.with_suffix('.json.tmp');temporary.write_text(json.dumps(data,indent=2));temporary.replace(path)
    write_table(data['rows'],directory/'FAUNA_ASSET_PLAN.csv')
    print('Planning refined:',sum('appearance_variation_plan' in x for x in data['rows']),'of',len(data['rows']),'; raw generation scope unchanged')
