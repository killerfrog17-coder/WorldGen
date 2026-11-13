from pathlib import Path
import json, sys
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "tools/reporting/python"))
from reportlab.pdfgen import canvas
from reportlab.lib.pagesizes import landscape, A4
from reportlab.lib.colors import HexColor
from reportlab.platypus import Paragraph
from reportlab.lib.styles import ParagraphStyle

root=Path(__file__).resolve().parent
r=json.loads((root/"evidence/final-review.json").read_text())
pdf=root/"WorldGen - Forest Placement Repair.pdf"
c=canvas.Canvas(str(pdf),pagesize=landscape(A4));w,h=landscape(A4)
c.setTitle("WorldGen - Forest Placement Repair")
c.setFillColor(HexColor("#163b30"));c.rect(0,h-84,w,84,fill=1,stroke=0)
c.setFillColor(HexColor("#ffffff"));c.setFont("Helvetica-Bold",23);c.drawString(30,h-36,"Forest placement repair")
c.setFont("Helvetica",11);c.drawString(30,h-58,"Inland rainforest | same location and camera | playable checkpoint, remaining work noted below")
image_width=(w-76)/2;image_height=image_width/1.6;y=h-108-image_height
for x,file,label in [(30,"before-rows.png","Before: straight gaps between compressed planting blocks"),(46+image_width,"forest-first.png","Now: mixed species across the correct planting footprint")]:
 c.drawImage(str(root/"evidence"/file),x,y,width=image_width,height=image_height)
 c.setFillColor(HexColor("#344b42"));c.setFont("Helvetica",9);c.drawString(x,y-16,label)
style=ParagraphStyle("body",fontName="Helvetica",fontSize=10.5,leading=15,textColor=HexColor("#263b32"))
text=[
 "<b>Placement.</b> Shared near/far tree identities, locally suitable per-tree species, and corrected planting footprints remove the straight rows. All 551 forest fields were present at first reveal and unchanged 20 seconds later.",
 f"<b>Loading.</b> Same-site entry fell from 48.3s to {r['entrySeconds']:.1f}s after terrain setup. Reused physical support and resolved pond candidates eliminate repeated terrain queries. World reading and worker startup are additional; this is still too slow.",
 "<b>Checks.</b> Actual CPU/GPU rendered positions agree within 0.05 pixels; the 600m/s route had no ground-coverage gaps. Camera-turn and shadow visibility checks pass. The recurring one-second vegetation rebuild fell from 12 occurrences to zero in a 12-second turn.",
 "<b>Performance.</b> 1080p, maximum terrain, Medium vegetation, 6km view: about 118-122 FPS average; worst frames 19-28ms. Another Codex was running. No reliable overall FPS gain or 100-FPS floor is claimed. Movement/GC spikes remain."
]
top=y-34
for text in text:
 p=Paragraph(text,style);_,ph=p.wrap(w-60,1000);p.drawOn(c,30,top-ph);top-=ph+10
c.setStrokeColor(HexColor("#d4ded8"));c.line(30,30,w-30,30)
c.setFillColor(HexColor("#617469"));c.setFont("Helvetica",8)
c.drawString(30,18,"6km view | terrain 1/4, vegetation Medium | 300m above ground | site >=20km from ocean coast")
c.drawRightString(w-30,18,"Build "+r["entrySource"][:8]+" | receipts and original captures alongside this report")
c.showPage()
c.setFillColor(HexColor("#163b30"));c.rect(0,h-68,w,68,fill=1,stroke=0)
c.setFillColor(HexColor("#ffffff"));c.setFont("Helvetica-Bold",23);c.drawString(30,h-36,"Distant forest review")
c.drawImage(str(root/"evidence/horizon.png"),50,100,width=w-100,height=(w-100)*864/1536)
notes="<b>1,500m above ground, 20km view.</b> The owner moved during this scenic review; this image is a visual example, not a stationary comparison. Beyond about 6km, forest uses canopy-colored terrain without tree silhouettes. Improving that distant representation, faster entry and remaining frame spikes are deferred."
p=Paragraph(notes,style);_,ph=p.wrap(w-60,1000);p.drawOn(c,30,86-ph)
c.setFillColor(HexColor("#617469"));c.setFont("Helvetica",8);c.drawString(30,18,"Candidate "+r["candidateSource"]+" | Mono, non-development | normal root shortcut updated")
c.save();print(pdf)
