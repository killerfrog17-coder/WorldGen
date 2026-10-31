"""Fail when any permanent dependency leaks into disposable artifacts."""
import argparse,json,os,subprocess
from pathlib import Path
from project_layout import ROOT,LAYOUT

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--report',type=Path);args=parser.parse_args()
    scratch=ROOT/LAYOUT['scratch'];errors=[];checked=0
    scratch_roots={scratch.resolve()}
    # Scratch itself has historically contained junctions to other drives.
    # A permanent path must not share one of those output directories either.
    for base,dirs,_ in os.walk(scratch,followlinks=False):
        for name in dirs[:]:
            path=Path(base)/name
            if path.is_symlink() or getattr(path.lstat(),'st_reparse_tag',0) in {0xA0000003,0xA000000C}:
                scratch_roots.add(path.resolve());dirs.remove(name)
    def in_scratch(path):
        physical=path.resolve()
        return any(physical.is_relative_to(p) for p in scratch_roots)
    tracked=subprocess.check_output(['git','ls-files','-z'],cwd=ROOT).decode().split('\0')
    leaks=[p for p in tracked if p.startswith('artifacts/')]
    if leaks:errors.append('Tracked permanent files remain in scratch: '+str(leaks[:5]))
    lock=json.loads((ROOT/'assets/game-assets.lock.json').read_text())
    for name in lock['files']:
        checked+=1;target=ROOT/name
        if name.startswith('artifacts/') or in_scratch(target):errors.append('Scratch dependency: '+name)
        elif not target.is_file():errors.append('Missing permanent input: '+name)
    inventory=ROOT/'assets-source/animals/export-metadata/native-inventory.json'
    animals=json.loads(inventory.read_text())['animals']
    for animal in animals:
        name=animal['source'].replace('\\','/');checked+=1
        if name.startswith('artifacts/') or not (ROOT/name).is_file():errors.append('Animal source is not permanent: '+name)
    bank=json.loads((ROOT/'assets/human-motion-kimodo/bank.json').read_text())
    for person in bank['people']:
        for clip in person['clips']:
            folder=clip['source'].replace('\\','/');checked+=1
            if folder.startswith('artifacts/') or not all((ROOT/folder/f).is_file() for f in ['generation.json','local_rotations_xyzw.f32','root_positions.f32']):errors.append('Missing permanent animation source: '+folder)
    for slot in LAYOUT['build_slots']:
        target=ROOT/LAYOUT['builds']/slot;checked+=1
        if in_scratch(target):errors.append('Player slot points into scratch: '+slot)
    for tool in ['blender-4.5.15','mpfb2','kimodo','kimodo-cpp','mesh2motion-cli']:
        checked+=1
        target=ROOT/LAYOUT['tools']/tool
        if not target.is_dir():errors.append('Required authoring tool missing: '+tool)
        elif in_scratch(target):errors.append('Tool is stored in scratch: '+tool)
    # Borrowed Python packages can silently recreate the same dependency leak.
    # These installations are local tools, outside the game-media asset lock.
    for path in (ROOT/LAYOUT['tools']).rglob('*.pth'):
        if 'site-packages' not in path.parts:continue
        checked+=1
        for line in path.read_text(encoding='utf-8-sig').splitlines():
            if 'artifacts' in line.lower():errors.append('Tool borrows packages from scratch: '+str(path.relative_to(ROOT)))
    report={'passed':not errors,'checks':checked,'locked_files':len(lock['files']),'animal_sources':len(animals),'errors':errors}
    if args.report:args.report.parent.mkdir(parents=True,exist_ok=True);args.report.write_text(json.dumps(report,indent=2))
    print(json.dumps(report if len(errors)<10 else {**report,'errors':errors[:10]},indent=2))
    return int(bool(errors))
if __name__=='__main__':raise SystemExit(main())
