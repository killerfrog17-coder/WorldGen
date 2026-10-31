"""Repository locations. Artifacts is output-only and never an input library."""
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parent.parent
LAYOUT=json.loads((ROOT/'assets/project-layout.json').read_text())
PREFIXES=sorted(LAYOUT['migration_prefixes'].items(),key=lambda row:-len(row[0]))

def relocated(relative):
    text=str(relative).replace('\\','/')
    absolute=text.lower().startswith(ROOT.as_posix().lower()+'/')
    if absolute:text=text[len(ROOT.as_posix())+1:]
    for old,new in PREFIXES:
        if text==old or text.startswith(old+'/'):
            mapped=new+text[len(old):]
            return (ROOT/mapped).as_posix() if absolute else mapped
    return (ROOT/text).as_posix() if absolute else text

def source_path(relative):
    """Resolve historical provenance paths without restoring old directories."""
    text=relocated(relative)
    target=ROOT/text
    if text.startswith('artifacts/') or target.resolve().is_relative_to((ROOT/LAYOUT['scratch']).resolve()):
        raise ValueError('No permanent input is registered for scratch path: '+text)
    return target

def scratch_path(relative):return ROOT/LAYOUT['scratch']/relative
def build_path(slot):
    if slot not in LAYOUT['build_slots']:raise ValueError('Unknown fixed player slot')
    return ROOT/LAYOUT['builds']/slot
