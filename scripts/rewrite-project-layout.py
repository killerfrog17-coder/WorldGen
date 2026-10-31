"""Update repository-owned path references after a verified source relocation."""
import ast,json,re,subprocess
from pathlib import Path
from project_layout import ROOT,PREFIXES

task=ROOT/'artifacts/layout-migration'
plan=json.loads((task/'plan.json').read_text())
exact={r['old']:r['new'] for r in plan['files']}
prefixes=list(PREFIXES)+list(plan['reports'].items())
prefixes.sort(key=lambda row:-len(row[0]))
diagnostic=r'(?:native[-_/]|previews/|renders/|captures/|screenshots/)'
def rewrite(text):
    # Full paths and embedded JSON/Windows-escaped path strings. Longest prefixes
    # first prevent an export subtree being mistaken for generic metadata.
    for old,new in prefixes:
        for slash in ['/', '\\', '\\\\']:
            a=old.replace('/',slash);b=new.replace('/',slash)
            def replacement(m):
                tail=text[m.end():].replace('\\','/').lstrip('/');component=re.split(r'[/\"\'\s]',tail,1)[0]
                if component.startswith(('native-','native_','previews','renders','captures','screenshots','resumed-')) and not component.endswith('-guild') and not new.startswith('assets/verification/'):
                    return m.group(0)
                return b
            text=re.sub(re.escape(a)+r'(?![A-Za-z0-9_-])',replacement,text)
    # pathlib code sometimes splits the repository-relative prefix in two.
    for old,new in prefixes:
        parts=old.split('/')
        if len(parts)!=2:continue
        text=re.sub(r"(['\"])artifacts\1\s*/\s*(['\"])"+re.escape(parts[1])+r"\2",repr(new),text)
    return text

tracked=subprocess.check_output(['git','ls-files','-z'],cwd=ROOT).decode().split('\0')
paths={ROOT/p for p in tracked if p and not p.startswith('artifacts/')}
paths.update(ROOT/r['new'] for r in plan['files'] if Path(r['new']).suffix in {'.json','.py','.ps1','.txt','.md','.tsv'})
paths.update((ROOT/'scripts').glob('*human*physique*.py'))
paths.update((ROOT/'scripts').glob('*human*hair*.py'))
skip={'AGENTS.md','assets/project-layout.json','assets/player-storage-policy.json','assets/legacy-runtime-assets.lock.json','scripts/project_layout.py','scripts/migrate-project-layout.py','scripts/rewrite-project-layout.py','scripts/deer-review-controls.py','scripts/prepare-deer-review.py'}
extensions={'.cs','.py','.ps1','.mjs','.js','.ts','.tsx','.jsx','.json','.md','.html','.txt','.tsv'}
changed=[]
for path in sorted(paths):
    rel=path.relative_to(ROOT).as_posix()
    if rel in skip or path.suffix not in extensions or not path.is_file():continue
    try:before=path.read_text(encoding='utf-8-sig')
    except UnicodeDecodeError:continue
    after=rewrite(before)
    # Historical runtime evidence remains a historical scratch reference. Only
    # the installed player location changes in old release certificates.
    if rel.startswith('assets/') and rel.endswith('release-verification.json'):
        after=before.replace('artifacts/player-human-natural-review','builds/slot-a').replace('artifacts/player-world-feedback','builds/slot-b')
    if before!=after:
        if path.suffix=='.py':ast.parse(after,filename=rel)
        if path.suffix=='.json':json.loads(after)
        path.write_text(after,encoding='utf-8');changed.append(rel)
(task/'rewritten-files.json').write_text(json.dumps(changed,indent=2))
print('UPDATED REFERENCES',len(changed))
