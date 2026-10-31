"""One-time, hash-verified relocation of permanent inputs out of scratch.

The plan is reviewable before --apply. No recursive directory deletion, old
player cloning, blocked-target operation, or authoring file editing is done.
"""
import argparse,hashlib,json,os,shutil,subprocess
from pathlib import Path
from project_layout import ROOT,PREFIXES,relocated

TASK=ROOT/'artifacts/layout-migration'
TASK.mkdir(parents=True,exist_ok=True)
def digest(p):
    h=hashlib.sha256()
    with p.open('rb') as s:
        for block in iter(lambda:s.read(4*1024*1024),b''):h.update(block)
    return h.hexdigest()
def linked(p):return p.is_symlink() or getattr(p.lstat(),'st_reparse_tag',0) in {0xA0000003,0xA000000C}

def plan():
    cached={r['old']:r for r in json.loads((TASK/'plan.json').read_text())['files']} if (TASK/'plan.json').exists() else {}
    needed=set(json.loads((ROOT/'assets/game-assets.lock.json').read_text())['files'])
    tracked=set(subprocess.check_output(['git','ls-files','-z'],cwd=ROOT).decode().split('\0'))
    needed.update(p for p in tracked if p.startswith('artifacts/'))
    needed.add('artifacts/climate-learning/wild-20261002-174123-921/selected.recipe.json')
    # Small, reusable approval/export contracts must not be thrown out with QA.
    for folder in ['human-natural-20261031/package-proof-final8','terrain-boundaries-20261031/package-proof','human-facing-fix/package-proof']:
        needed.update(p.relative_to(ROOT).as_posix() for p in (ROOT/'artifacts'/folder).glob('*.json'))
    # Preserve editable source work, raw motion and its metadata. Exclude camera
    # captures, compiled output, test libraries and tool installations.
    source_ext={'.blend','.fbx','.glb','.gltf','.bvh','.npz','.npy','.f32','.png','.jpg','.jpeg','.webp','.json','.txt','.tsv','.md','.mhmat','.mhclo','.obj'}
    excluded={'previews','renders','captures','screenshots','native','bin','obj','Library','Temp','Logs','__pycache__','.git','node_modules','previous-owner-rejected'}
    for old,new in PREFIXES:
        if not new.startswith('assets-source/') or any(old.startswith(prior+'/') for prior,_ in PREFIXES if len(prior)<len(old) and prior.startswith('artifacts/human-natural-')):continue
        top=ROOT/old
        if not top.is_dir():continue
        for base,dirs,files in os.walk(top,followlinks=False):
            dirs[:]=[d for d in dirs if d not in excluded and not (d.startswith(('native-','native_')) and not d.endswith('-guild')) and not d.startswith(('resumed-','final-capture','rejected-')) and not linked(Path(base)/d)]
            for name in files:
                p=Path(base)/name
                if p.suffix.lower() in source_ext and not name.endswith(('.blend1','.blend2')):needed.add(p.relative_to(ROOT).as_posix())
    # Explicitly requested documents remain available independently of scratch.
    reports={}
    for p in (ROOT/'artifacts').rglob('*.pdf'):
        rel=p.relative_to(ROOT).as_posix()
        if '/tools/' not in rel:
            reports[rel]='docs/reports/'+p.parent.name+'/'+p.name;needed.add(rel)
    rows=[]
    for old in sorted(needed):
        if not old.startswith('artifacts/'):continue
        p=ROOT/old;new=reports.get(old,relocated(old))
        if new==old:
            raise ValueError('Permanent input has no migration rule: '+old)
        if not p.is_file():raise FileNotFoundError('Required input is missing: '+old)
        target=ROOT/new
        if target.exists() and digest(target)!=digest(p):raise ValueError('Destination conflict: '+new)
        size=p.stat().st_size;prior=cached.get(old)
        sha=prior['sha256'] if prior and prior['bytes']==size else digest(p)
        rows.append({'old':old,'new':new,'bytes':size,'sha256':sha,'tracked':old in tracked})
    result={'files':rows,'reports':reports,'bytes':sum(r['bytes'] for r in rows)}
    (TASK/'plan.json').write_text(json.dumps(result,indent=2))
    print(json.dumps({'files':len(rows),'GiB':result['bytes']/2**30,'tracked':sum(r['tracked'] for r in rows),'reports':len(reports)}))

def apply():
    data=json.loads((TASK/'plan.json').read_text());done=[]
    for row in data['files']:
        source=ROOT/row['old'];target=ROOT/row['new']
        if not source.is_file():
            if target.is_file() and digest(target)==row['sha256']:done.append(row['new']);continue
            raise FileNotFoundError(row['old'])
        # Every source is an explicit file beneath the named project scratch
        # tree or its known human-authoring junction, never a blocked path.
        assert source.relative_to(ROOT).as_posix().startswith('artifacts/')
        assert row['new'].startswith(('assets-source/','assets/','docs/reports/'))
        assert not target.resolve().is_relative_to((ROOT/'artifacts').resolve())
        assert digest(source)==row['sha256'],row['old']
        original=source.stat()
        target.parent.mkdir(parents=True,exist_ok=True)
        if source.stat().st_dev==target.parent.stat().st_dev:
            os.replace(source,target)
        else:
            shutil.copyfile(source,target)
            if digest(target)!=row['sha256']:raise ValueError('Copy validation failed; source retained')
            source.unlink()
        after=target.stat()
        if after.st_size!=original.st_size:raise ValueError('Relocation size mismatch')
        # A same-volume rename preserves the file object and all bytes. A
        # cross-volume copy was SHA-verified before its source was removed.
        if original.st_dev==after.st_dev and original.st_ino!=after.st_ino:raise ValueError('Atomic relocation changed file identity')
        done.append(row['new'])
    (TASK/'relocation-proof.json').write_text(json.dumps({'files':len(done),'all_original_bytes_preserved':True,'paths':done},indent=2))
    print('RELOCATED',len(done),'files; every SHA256 unchanged')

parser=argparse.ArgumentParser();parser.add_argument('--apply',action='store_true');args=parser.parse_args()
apply() if args.apply else plan()
