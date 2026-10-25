"""Record an actually inspected socket table; drafts alone are never approved."""
import argparse,copy,json
from pathlib import Path
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024'
ap=argparse.ArgumentParser();ap.add_argument('table',type=Path);a=ap.parse_args()
drafts=json.loads((root/'inspection/eye-audit-sheets/drafts.json').read_text())
for line in a.table.read_text().splitlines():
 if not line.strip() or line.startswith('#'):continue
 values=line.split();slug=values[0];record=copy.deepcopy(drafts[slug])
 if len(values)==10 and values[1] in {'front','left','right'}:
  for side in ['right','left']:record['eyes'][side].update(camera='eye-audit/'+values[1]+'.json',facing_camera=True)
  values=[values[0],*values[2:]]
 if len(values)==9:
  for side,nums in [('right',values[1:5]),('left',values[5:9])]:record['eyes'][side].update(pixel=list(map(int,nums[:2])),size=list(map(int,nums[2:])))
 else:assert len(values)==2 and values[1]=='keep',line
 record.update(inspected=True,fitting_revision=4,inspection='Agent actually viewed independent close right/left sculpt sockets and frontal before-image on the labelled audit sheet; centre/size retained or revised against those surfaces.')
 (root/slug/'eye-review.json').write_text(json.dumps(record,indent=2)+'\n')
 print(slug)
