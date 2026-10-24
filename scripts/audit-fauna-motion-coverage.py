"""Inventory current motion availability without loading or changing any asset."""
import csv,json
from pathlib import Path
from fauna_motion_sources import source_for
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-overnight-20261024'
plan=json.loads((repo/'docs/fauna/asset-plan.json').read_text())['rows'];rows=[]
for animal in plan:
    if animal['asset_status']!='queued':continue
    source=source_for(animal,repo)
    if animal['family']=='microfly':status='Own simple wing/hover motion';clips='Idle / Flight / Perch';bank='own code'
    elif source:
        s=json.loads((source/'source-rig.json').read_text());status='Raw retarget source available; anatomical compatibility awaits review';clips=' / '.join(c['label'] for c in s['clips']);bank=source.name
    else:status='Suitable CC0 motion source still needed';clips='';bank=''
    rows.append([animal['priority_rank'],animal['animal'],animal['family'],status,bank,clips])
with (root/'MOTION_COVERAGE.csv').open('w',newline='',encoding='utf-8-sig') as f:
    w=csv.writer(f);w.writerow(['Rank','Animal','Family','Availability','Source bank','Actual clips']);w.writerows(rows)
missing=[r[1] for r in rows if not r[4]]
(root/'motion-coverage.json').write_text(json.dumps({'visual_animals':len(rows),'source_available':len(rows)-len(missing),'source_missing':missing,'compatibility_is_not_art_approval':True},indent=2))
print(json.dumps({'source_available':len(rows)-len(missing),'source_missing':missing}))
