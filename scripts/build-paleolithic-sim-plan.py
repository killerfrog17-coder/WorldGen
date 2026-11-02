"""Generate the concise owner-facing architecture report; no scratch inputs required."""
from pathlib import Path
import sys
root=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root/'tools/reporting/python'))
from reportlab.pdfgen import canvas
from reportlab.lib.colors import HexColor,white
from reportlab.platypus import Paragraph
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
for name,file in [('Body','calibri.ttf'),('Bold','calibrib.ttf')]:
    pdfmetrics.registerFont(TTFont(name,'C:/Windows/Fonts/'+file))
pdfmetrics.registerFontFamily('Body',normal='Body',bold='Bold')
output=root/'docs/reports/WorldGen - Paleolithic Simulation Plan.pdf'
output.parent.mkdir(parents=True,exist_ok=True)
c=canvas.Canvas(str(output),pagesize=(595,842))
c.setTitle('WorldGen | Paleolithic simulation foundation')
ink=HexColor('#183c38');muted=HexColor('#516864');green=HexColor('#267d6a');pale=HexColor('#eef4ef');gold=HexColor('#9c6824')
def text(value,x,y,width=507,size=10.5,color=ink):
    p=Paragraph(value,ParagraphStyle('body',fontName='Body',fontSize=size,leading=size*1.28,textColor=color))
    _,height=p.wrap(width,2000)
    assert y-height>35,(value,y,height)
    p.drawOn(c,x,y-height)
    return y-height

def page(number,title,subtitle):
    if number>1:c.showPage()
    c.setFillColor(ink);c.rect(0,830,595,12,fill=1,stroke=0)
    text('WORLDGEN / PALEOLITHIC FOUNDATION',44,805,size=9,color=green)
    text(title,44,776,size=25)
    text(subtitle,44,736,size=10.5,color=muted)
    c.setFillColor(muted);c.setFont('Body',8)
    c.drawString(44,20,f'REBUILD ARCHITECTURE  /  02 NOV 2026                                                {number} / 2')
def block(title,body,y):
    y=text('<b>'+title+'</b>',44,y,size=12)
    return text(body,44,y-6,size=10.5)-18

page(1,'A world with a history','Established humans at the start. Changing opportunities, cultures and populations afterward.')
y=block('The starting point','World Year 0 represents established <b>Homo sapiens</b> around the equivalent of 200,000 years ago. Other hominin lineages have their own connected ancestral dispersal, isolation, physical traits and ancestry. There are no invented states or universal calendar-based technology unlocks.',699)
rows=[
('Climate and ecology','Stable ecological boundaries; changing temperature, rain, ice, vegetation potential and local species presence. Wet corridors, refugia and real shelf depths affect movement.'),
('People and resources','Age structure, births, deaths, food, winter protection, mating contacts and local shocks. Populations can retreat or disappear. Splitting a population record creates no extra food access.'),
('Technology','43 techniques with separate familiarity, trained prevalence and actual use. Prerequisites, materials, teachers, innovation and forgetting matter. Useful technologies spread through real contact.'),
('Language','Inherited words and grammar change at different rates. Borrowing, bilingual contact, local varieties and recognisable families are separate from deep common ancestry.'),
('Culture and belief','Distinct social norms, exchange relationships and residence patterns. Local rites, sacred places/animals, mortuary practices and spiritual themes vary and spread without churches or states.'),
('Human lineages','Different body/face profiles, cultural capacities, geographic histories and mixed ancestry. Competition and survival depend on actual conditions; extinction is not scheduled by date.'),
('Local inhabitants','Stable bands, households, birthdays, parents, seasonal camps and contacts. Samples contain actual proficient makers, individual language knowledge and separate routine/winter equipment.'),
]
y=text('<b>What the model tracks</b>',44,y,size=13)-12
for i,(title,body) in enumerate(rows):
    height=65
    if i%2==0:c.setFillColor(pale);c.roundRect(40,y-height+4,515,height,5,fill=1,stroke=0)
    text('<b>'+title+'</b>',49,y-4,width=116,size=10.5,color=green)
    text(body,173,y-4,width=373,size=10)
    y-=height

page(2,'How this reaches the game','The simulation produces local evidence for the next supervised NPC implementation.')
y=block('From a district to a camp','A local query supplies a band, related people, appearance, spoken languages, capable craft specialists, equipment practices and a seasonal itinerary. The NPC host must retain instantiated people and player edits. Camp candidates still need physical ground, water and collision validation.',699)
y=block('Observe the actual differences','Language/family, cultural identity, spiritual tradition, toolkit and human-species layers read different data. Political territories remain empty. Climate/ecology maps follow the dated world. Technology inspection distinguishes knowing a method, being able to make it and routinely using it.',y)
y=block('World continuity and performance','Small demographic reviews and slower network/ecology reviews share one saved timeline. Long histories remain computationally expensive. Nearby queries use district/contact indexes. Terrain workers read the exact planet without duplicating the population archive; dated local-map colours stay with the player.',y)
y=block('Generate a new world for this revision','New worlds use <b>foragers-4</b>: additive resource claims and teaching from actual capable makers. Rare experts are distinct from population-wide adoption. Existing saves retain their older model. Compact saves preserve historical fields; earlier formats stay readable. Git and asset packs provide rollback.',y)
y=block('Validation and scope','Check several seeds and dates, expansion <i>and</i> retreat, local technique differences, real vocabulary divergence, household consistency, save/reload, live map playback and entering gameplay. Physical NPC camps/dialogue, new hominin art, moving shorelines/ice-sheet terrain and post-Paleolithic progression are separate implementation stages.',y)
y=text('<b>Selected research behind the mechanisms</b>',44,y,size=12)-12
refs=[
('Structured African ancestry and connected populations','https://www.nature.com/articles/s41586-023-06055-y'),
('Climate, habitats and hominin distributions','https://www.nature.com/articles/s41586-022-04600-9'),
('Forager networks and cumulative culture','https://pmc.ncbi.nlm.nih.gov/articles/PMC7048420/'),
('Language change: unequal rates across features','https://pmc.ncbi.nlm.nih.gov/articles/PMC5651730/'),
('Forager belief systems and their variation','https://pmc.ncbi.nlm.nih.gov/articles/PMC4958132/'),
('Culture-linked competition and assimilation','https://pubmed.ncbi.nlm.nih.gov/29032037/'),
]
for title,url in refs:
    y=text(f'<link href="{url}" color="#267d6a">{title}</link>',44,y,size=9)-8
c.save();print(output)
