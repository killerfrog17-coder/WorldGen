"""Record only explicitly viewed result sheets whose native and PNG hashes match."""
import argparse,hashlib,json
from pathlib import Path
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024'
ap=argparse.ArgumentParser();ap.add_argument('--pages',nargs='+',type=int,required=True);a=ap.parse_args()
for page in a.pages:
 path=root/f'inspection/eye-result-sheets/eyes-{page:02}.json';sheet=json.loads(path.read_text());groups={}
 for view in sheet['views']:groups.setdefault(view['slug'],[]).append(view)
 for slug,views in groups.items():
  assert len(views)==3,(slug,'missing views');folder=root/slug;digest=hashlib.sha256((folder/'Animal - Painted Species Review.blend').read_bytes()).hexdigest()
  for v in views:
   assert v['source_sha256']==digest,(slug,'stale native')
   assert hashlib.sha256((folder/f"eye-audit/{v['view']}-after.png").read_bytes()).hexdigest()==v['sha256'],(slug,'stale image')
  evidence={'sheet':str(path.with_suffix('.jpg').relative_to(repo)),'sheet_sha256':hashlib.sha256(path.with_suffix('.jpg').read_bytes()).hexdigest(),'views':views,'scope':'Agent actually inspected the revised front and both side close-ups against the earlier neutral socket audit. Eye placement, opening size, surrounding rims and attachment appearance only; owner art approval remains pending.'}
  receipt=folder/'eye-audit/revision.json';value=json.loads(receipt.read_text());assert value['blend_sha256']==digest
  assert value['review_sha256']==hashlib.sha256((folder/'eye-review.json').read_bytes()).hexdigest(),(slug,'new fit pending')
  value.update(actual_after_vision_pending=False,visual_inspection=evidence);receipt.write_text(json.dumps(value,indent=2)+'\n')
  report=folder/'eye-audit/after-renders.json';value=json.loads(report.read_text());value.update(actual_vision_pending=False,visual_inspection=evidence);report.write_text(json.dumps(value,indent=2)+'\n')
  final=folder/'final-qa.json'
  if final.exists():
   value=json.loads(final.read_text())
   if value['checked_blend_sha256']==digest:value.update(actually_visually_inspected=True,eye_visual_inspection=evidence);final.write_text(json.dumps(value,indent=2)+'\n')
  print('Eye views inspected',slug)
 sheet['actually_inspected']=True;path.write_text(json.dumps(sheet,indent=2)+'\n')
