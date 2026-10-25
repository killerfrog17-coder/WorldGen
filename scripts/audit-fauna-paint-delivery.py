"""Inventory checked selected-body colour copies and make actual-render review sheets.

This does not approve appearance or motion. Sheets are composed from saved CPU
renders; the audit records their current file hashes for subsequent inspection.
"""
import hashlib,json
from datetime import datetime
from pathlib import Path
from PIL import Image,ImageDraw

repo=Path(__file__).resolve().parent.parent
root=repo/'artifacts/fauna-painted-20261024'
queue=json.loads((root/'paint-queue.json').read_text())
out=root/'inspection';out.mkdir(exist_ok=True)
rows=[]
for job in queue['jobs']:
    slug=job['slug'];folder=root/slug;blend=folder/'Animal - Painted Species Review.blend'
    check=folder/'paint-native-checks.json'
    row={'slug':slug,'appearance_approved':False,'second_pass_inspected':False,'errors':[]}
    if not blend.exists() or not check.exists():row['errors'].append('native copy/check missing')
    else:
        proof=json.loads(check.read_text());digest=hashlib.sha256(blend.read_bytes()).hexdigest()
        row['blend_sha256']=digest
        if proof.get('checked_blend_sha256')!=digest:row['errors'].append('native check SHA mismatch')
        if not proof.get('source_bind_geometry_skin_rig_exact'):row['errors'].append('source identity proof missing')
        if not proof.get('source_actions_exact'):row['errors'].append('action identity proof missing')
    row['renders']={}
    for view in ['side','front','rear']:
        path=folder/f'previews/1-Bind-{view}-painted.png'
        if path.exists():row['renders'][view]={'path':str(path.relative_to(repo)),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
        else:row['errors'].append(view+' render missing')
    rows.append(row)
sheets=[]
for start in range(0,len(rows),24):
    batch=rows[start:start+24]
    sheet=Image.new('RGB',(1440,((len(batch)+5)//6)*260),(27,29,32));draw=ImageDraw.Draw(sheet)
    for i,row in enumerate(batch):
        x=(i%6)*240;y=(i//6)*260
        if 'side' in row['renders']:
            with Image.open(repo/row['renders']['side']['path']) as im:
                im=im.convert('RGB');im.thumbnail((240,236));sheet.paste(im,(x+(240-im.width)//2,y))
        draw.text((x+5,y+238),row['slug'][:32],fill=(245,245,245))
        if row['errors']:draw.text((x+5,y+251),'CHECK: '+str(len(row['errors'])),fill=(255,140,90))
    path=out/f'colour-side-sheet-{start//24+1:02}.jpg';sheet.save(path,quality=88)
    sheets.append({'path':str(path.relative_to(repo)),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'slugs':[r['slug'] for r in batch],'actually_inspected':False})
report={'updated':datetime.now().astimezone().isoformat(),'selected':len(rows),'native_and_three_views_ready':sum(not r['errors'] for r in rows),'rows':rows,'sheets':sheets,'limits':'Technical inventory only. No image is marked inspected by generating a sheet. Appearance, motion, species extras and engine delivery remain separate.'}
(out/'delivery-audit.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({k:report[k] for k in ['selected','native_and_three_views_ready']}))
print('Issues:',[(r['slug'],r['errors']) for r in rows if r['errors']])
