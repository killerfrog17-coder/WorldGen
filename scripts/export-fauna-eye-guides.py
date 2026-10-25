"""Back up curated socket picks and their cameras, without model/image binaries."""
import json
from pathlib import Path
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024'
jobs=json.loads((root/'paint-queue.json').read_text())['jobs'];records={}
for job in jobs:
 slug=job['slug'];folder=root/slug;review=json.loads((folder/'eye-review.json').read_text());cameras={}
 for spec in review['eyes'].values():
  camera=spec['camera'];guide=json.loads((folder/camera).read_text())
  cameras[camera]={key:guide[key] for key in ['camera_matrix','scale','size']}
 records[slug]={'review':review,'cameras':cameras}
out=repo/'docs/fauna/EYE_SOCKET_GUIDES.json';out.write_text(json.dumps({'scope':'Manually inspected selected-body socket picks. Geometry SHA gates reuse. Do not apply these camera pixels to a regenerated body or assume owner appearance approval.','animals':records},indent=2)+'\n')
print('Curated socket guides',len(records))
