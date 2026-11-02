"""Rebuild the concise owner-facing Paleolithic simulation plan."""
from pathlib import Path
import sys,math
R=Path(__file__).resolve().parents[1];sys.path.insert(0,str(R/'tools/reporting/python'))
from reportlab.pdfgen import canvas
from reportlab.lib.colors import HexColor,white
from reportlab.platypus import Paragraph
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
for name,file in [('Body','calibri.ttf'),('Bold','calibrib.ttf')]:pdfmetrics.registerFont(TTFont(name,'C:/Windows/Fonts/'+file))
pdfmetrics.registerFontFamily('Body',normal='Body',bold='Bold')
O=R/'docs/reports/WorldGen - Paleolithic Simulation Plan.pdf';O.parent.mkdir(parents=True,exist_ok=True)
C=canvas.Canvas(str(O),pagesize=(595,842));C.setTitle('WorldGen | Paleolithic simulation plan')
ink=HexColor('#183c38');muted=HexColor('#516864');green=HexColor('#267d6a');pale=HexColor('#eef4ef');gold=HexColor('#a66a26')
def text(s,x,y,w=507,size=11,color=ink):
 p=Paragraph(s,ParagraphStyle('x',fontName='Body',fontSize=size,leading=size*1.3,textColor=color));_,h=p.wrap(w,2000);assert y-h>35,(s,y,h);p.drawOn(C,x,y-h);return y-h
def page(n,title,subtitle):
 if n>1:C.showPage()
 C.setFillColor(ink);C.rect(0,830,595,12,fill=1,stroke=0)
 text('WORLDGEN / DESIGN & IMPLEMENTATION',44,803,size=9,color=green)
 text(title,44,774,size=26);text(subtitle,44,732,size=11,color=muted)
 C.setFont('Body',8);C.setFillColor(muted);C.drawString(44,20,f'PALEOLITHIC FOUNDATION   /   02 NOV 2026                                     {n} / 3')
def block(title,body,y):
 y=text('<b>'+title+'</b>',44,y,size=13);return text(body,44,y-7,size=11)-22
page(1,'A living Paleolithic world','Start about 200,000 years ago. Stop technological progression before the Mesolithic.')
y=block('The starting world', 'One culturally simulated sapient species: <b>Homo sapiens</b>. Separate lightweight warm- and cold-adapted archaic lineages can share ecological patches with later sapiens. Choose a warm, productive, well-watered ancestral area using the existing ecological geography; seed a small connected population of established foraging bands, not a single newly evolved couple. Geography can keep them on one continent. Other sapient profiles can be added later.',695)
y=block('Basic inherited knowledge', 'Flake cutting, prepared-core stoneworking, scrapers, wooden spears, basic spearhead hafting, fire tending and elementary hide working. Communities inherit different levels of practice. More specialized clothing, composite adhesives, bone tools, boats and projectile technologies remain local developments, not global calendar unlocks.',y)
y=block('Ice ages change opportunities', 'Keep region IDs and terrain fixed. Smooth glacial/interglacial forcing changes regional temperature, moisture, forage capacity and cold exposure. Shelves become migration corridors only when their actual depth permits it; lower sea level never turns a deep ocean into a land bridge. Clothing, fire and shelter affect winter survival. Expansion can stall, retreat or resume.',y)
# Small schematic: climate -> local survival -> routes -> expansion.
for i,(a,b) in enumerate([('CLIMATE','cold / moisture'),('HABITABILITY','food / warmth'),('CONNECTIVITY','passes / shelves'),('POPULATION','spread / retreat')]):
 x=44+i*129;C.setFillColor(pale);C.roundRect(x,246,120,68,5,fill=1,stroke=0);text('<b>'+a+'</b>',x+9,299,104,9);text(b,x+9,278,104,9)
text('<b>In game:</b> the same coordinates keep their identity, but their communities and knowledge can change across generations. This pass exposes climate/ecology state for gameplay; it does not promise moving ice-sheet terrain or a complete visual vegetation replacement system.',44,219,size=11)
text('Basis: African population structure [1], glacial forcing [2]. The seeded origin and numerical rates are explicit world-generation priors.',44,110,size=9,color=muted)
page(2,'Knowledge travels with people','Regional percentages describe adoption; persistent local identities decide who actually knows what.')
y=block('Discovery → learning → established practice','Each technique has prerequisites, experimentation and teaching. Local familiarity grows gradually; isolation, lost practitioners and resource shortages can cause decline. Knowing how and having the required material are separate. A hide wrap, fitted garment and insulated outfit are different capabilities.',695)
y=block('90% does not mean a reroll at every encounter','Store local adoption and stable community identities. A spatially coherent, seeded community sample gives the same band the same access when revisited. Neighbouring bands tend to share traditions. Local discovery and contact spread practice within and across regions; there is no whole-region on/off switch.',y)
# coherent adoption example
for i in range(20):
 x=48+(i%10)*49;y0=424-(i//10)*29;C.setFillColor(green if i<18 else HexColor('#d8dfdc'));C.roundRect(x,y0,38,17,4,fill=1,stroke=0)
text('Example: 18 of 20 local community samples know hide clothing. Their positions are stable; grey communities do not randomly swap on reload.',44,368,size=9,color=muted)
y=block('Languages exist from the beginning','Reuse the existing sound and word generator. Save language families, local varieties, inherited vocabulary, contact borrowing and divergence. Add separate gradual capacities for expressive vocabulary, sequencing, displaced reference and clause combination; future dialogue can ask what a speaker can express, rather than counting modern words.',317)
y=block('A deliberate historical abstraction','Early expressive limits are a configurable game hypothesis, not a reconstructed 200,000-year-old grammar. Development is local and continuous, not an intelligence ladder between living peoples. No writing, literacy or automatic modern-language date unlock. Later communities can reach fully expressive speech while retaining a simple material toolkit.',y)
text('Basis: multicamp learning networks [3]; language-origin hypothesis [4]; hide working [5]. The language generator is a foundation, not finished voiced dialogue.',44,100,size=9,color=muted)
page(3,'Build the foundation, then connect it','A versioned replacement for new simulations; old saves retain their original rules.')
y=block('1 / Preserve the escape hatch','Push a named Git baseline before changing simulation rules. Add a separate Paleolithic state version, copied and hashed with saves. Existing historical saves remain loadable without silent conversion. New worlds use the new model; later ages are disabled, not merely hidden from the menu.',695)
y=block('2 / Implement the shared simulation','Use stable ecological patches and sparse occupied community records. Bound migration and contact by actual routes. Track continuous population, local technique adoption, language development and climate. Give the existing calendar a deterministic work budget; fast-forward and ordinary gameplay advance the same state.',y)
y=block('3 / Connect the observer and gameplay','Use the existing in-world Year 0 calendar. Culture, language/dialect, technology and human-species maps show their own records; state and territory layers stay empty before institutions exist. Inspection shows adoption shares. A stable band query returns identity, language, capabilities and appearance for future NPC spawns. Existing NPCs are not silently respawned with new equipment. Preserve the current day/year conversion rather than adding a second competing clock.',y)
y=block('4 / Prove it with focused checks','Check smooth adoption, local coherence, no ocean teleportation, cold exposure, advancing/receding corridors, no post-Paleolithic unlocks, deterministic stepping, save/reload continuation and old-save compatibility. Time a long run and real gameplay updates. Build in the inactive reusable slot, inspect the actual map, then update the existing shortcut.',y)
y=block('What this delivery does not claim','No new hominin bodies, complete NPC tribes/dialogue, genetically detailed speciation, planetary ice-sheet physics or later ages. The new foundations make those additions possible without treating every band as an independent full-world simulation.',y)
text('<b>Research links</b>',44,224,size=12)
refs=[('1  African origins / connected ancestral populations','https://www.nature.com/articles/s41586-023-06055-y'),('2  Glacial cycles and global temperature','https://www.nature.com/articles/nature19798'),('3  Hunter-gatherer networks and cumulative culture','https://pubmed.ncbi.nlm.nih.gov/32158935/'),('4  Language capacity: a proposed early bound, not a consensus date','https://doi.org/10.3389/fpsyg.2025.1503900'),('5  Specialized hide-working tools, 120–90 ka','https://doi.org/10.1016/j.isci.2021.102988')]
for i,(title,url) in enumerate(refs):text(f'<link href="{url}" color="#267d6a">{title}</link>',44,198-i*22,size=9)
C.save();print(O)
