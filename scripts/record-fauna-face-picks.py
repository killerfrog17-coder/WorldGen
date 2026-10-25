"""Record inspected guide coordinates against exact selected model hashes."""
import argparse,json
from pathlib import Path
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024'
ap=argparse.ArgumentParser();ap.add_argument('table',type=Path);a=ap.parse_args()
for line in a.table.read_text().splitlines():
    if not line.strip() or line.startswith('#'):continue
    slug,rx,ry,lx,ly,nx,ny,axis=line.split()
    folder=root/slug;state=json.loads((folder/'paint-state.json').read_text())
    camera=f"previews/1-Bind-{'left' if axis=='reverse' else axis}-surface.json"
    picks={'model_sha256':state['source_model_sha256'],'cosmetic_head_from_eyes':True,'eyes':{'right':{'camera':camera,'pixel':[int(rx),int(ry)]},'left':{'camera':camera if axis=='front' else 'previews/1-Bind-side-surface.json' if axis=='reverse' else 'previews/1-Bind-left-surface.json','pixel':[int(lx),int(ly)]}},'nose_guide':{'camera':camera,'pixel':[int(nx),int(ny)]}}
    if axis=='front':picks['recast_axis']='front'
    (folder/'face-guide-picks.json').write_text(json.dumps(picks,indent=2)+'\n')
    print('Recorded',slug)
