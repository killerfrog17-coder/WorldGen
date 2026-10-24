"""Inventory a concrete owner-requested cleanup; never deletes anything."""
import argparse,datetime,json,os,stat
from pathlib import Path

ap=argparse.ArgumentParser();ap.add_argument('--selected',required=True);a=ap.parse_args()
repo=Path(__file__).resolve().parent.parent;art=repo/'artifacts';external=Path('E:/WorldGenBuilds');work=Path('D:/WorldGenAI')
inventory=json.loads((art/'cleanup-inventory-20261024.json').read_text())
sizes={x['path'].lower():x['bytes'] for root in inventory for x in root['entries']}
selected=Path(a.selected).resolve().parent
builds=[]
for root in (art,external):
    if not root.exists():continue
    for folder in root.iterdir():
        exe=folder/'WorldGen.exe'
        if not exe.is_file() or not (folder/'WorldGen_Data').is_dir():continue
        if folder.lstat().st_file_attributes&stat.FILE_ATTRIBUTE_REPARSE_POINT:raise ValueError(f'Linked build: {folder}')
        when=datetime.datetime.fromtimestamp(exe.stat().st_mtime).astimezone()
        manifest=folder/'build-inputs.json'
        if manifest.exists():
            value=json.loads(manifest.read_text(encoding='utf-8-sig')).get('BuiltUtc')
            if value:when=datetime.datetime.fromisoformat(value.replace('Z','+00:00')).astimezone()
        builds.append({'path':str(folder.resolve()),'timestamp':when.isoformat(),'day':when.date().isoformat(),'bytes':sizes.get(str(folder.resolve()).lower(),0)})
retained={}
for day in sorted({b['day'] for b in builds}):
    # Prefer the local artifact copy for identical timestamps of relocated builds.
    winner=max((b for b in builds if b['day']==day),key=lambda b:(b['timestamp'],str(art).lower() in b['path'].lower(),b['path']))
    retained[winner['path'].lower()]=winner
for b in builds:
    if Path(b['path'])==selected:retained[b['path'].lower()]=b
remove=[]
def add(path,reason):
    path=path.resolve()
    if path.exists():remove.append({'path':str(path),'reason':reason,'bytes':sizes.get(str(path).lower(),0),'directory':path.is_dir()})
for b in builds:
    if b['path'].lower() not in retained:add(Path(b['path']),'Superseded game build; keep one final build per local day across both drives')
keep_roots={'tools','fauna-assets','fauna-ai-review-20261022','fauna-ai-review-additional-20261022','climate-learning','exploration-music','terrain-surface-art-20261024','latest'}
build_paths={b['path'].lower() for b in builds}
for child in art.iterdir():
    if not child.is_dir() or str(child.resolve()).lower() in build_paths or child.name in keep_roots:continue
    add(child,'Superseded generated review/test output; accepted deer and motion inputs consolidated separately')
for name in ['AnyTop','deer-realvis-models','deer-sdxl-models','deer-vae-models','Hunyuan3D-2GP','mocapanything-motion-study','motion-eval-venv','trellis2-models','unimate-motion-study']:
    add(art/'tools'/name,'Unused downloaded models or rejected study tool; not part of current shape/rig/motion pipeline')
for bank in ('fauna-ai-review-20261022','fauna-ai-review-additional-20261022'):
    for name in ('candidates-stereo','jobs'):add(art/bank/name,'Duplicate generation masters/job cache; owner audition WAVs and catalogue retained')
for name in ('deer-four-rigs-20261024','deer-approved-four-rigs-20261024'):
    add(work/name,'Superseded deer gallery; one self-contained accepted prototype retained in fauna-assets/deer')
for child in art.iterdir():
    if child.is_file() and child.name not in {'cleanup-inventory-20261024.json','cleanup-plan-20261024.json','cleanup-report-20261024.json','checkpoint-retention.json'}:
        if child.suffix.lower() in {'.log','.png','.jpg','.pdf','.json','.txt'}:add(child,'Obsolete generated root-level log/capture/report')
protected=[str(selected),str(art/'fauna-assets'),*[str(art/'tools'/name) for name in ['blender-4.5.15','TripoSG','SkinTokens','mesh2motion-cli','Puppeteer','triposg-review-venv','trellis2-venv']],str(work/'trellis2-venv'),*[x['path'] for x in retained.values()]]
plan={'created':datetime.datetime.now().astimezone().isoformat(),'allowed_roots':[str(art.resolve()),str(external.resolve()),str(work.resolve())],
 'rule':'One final playable build per local calendar day, across C and E; selected/running builds protected. No latest-three exception.',
 'retained_builds':list(retained.values()),'protected':protected,'remove':remove,
 'retained_audio':'Both fauna audition folders and their catalogues; only duplicate stereo masters/job caches discarded',
 'retained_deer':'artifacts/fauna-assets/deer/Deer - Prototype.blend, packed maps, reference/fur source, one fresh rig prediction and shared authored motion bank',
 'preserved_user_data':'Repository source/docs, world/, saves outside generated artifacts, sounds/, music/, ecology source and shared AI installations'}
(art/'cleanup-plan-20261024.json').write_text(json.dumps(plan,indent=2))
print(f'Plan: {len(remove)} targets, {len(retained)} daily builds retained.')
print('Root-level bytes represented:',round(sum(x['bytes'] for x in remove)/2**30,2),'GiB; nested model/cache targets counted during apply.')
for b in sorted(retained.values(),key=lambda b:b['timestamp']):print('KEEP',b['day'],b['path'])
