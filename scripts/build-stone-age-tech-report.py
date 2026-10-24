"""Typeset the owner-requested Paleolithic technology design report as a PDF."""
from pathlib import Path
import sys
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'artifacts/report-tools/python'))
from reportlab.pdfgen import canvas
from reportlab.platypus import SimpleDocTemplate,Paragraph,Spacer,Table,TableStyle,PageBreak,KeepTogether
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib import colors
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont

OUT=ROOT/'docs/reports/WorldGen - Paleolithic Technology Guide.pdf'
OUT.parent.mkdir(parents=True,exist_ok=True)
for name,file in [('Body','calibri.ttf'),('Bold','calibrib.ttf'),('Italic','calibrii.ttf'),('Light','calibril.ttf')]:
    pdfmetrics.registerFont(TTFont(name,str(Path('C:/Windows/Fonts')/file)))
pdfmetrics.registerFontFamily('Body',normal='Body',bold='Bold',italic='Italic',boldItalic='Bold')
INK=colors.HexColor('#203331');GREEN=colors.HexColor('#245F4C');MUTED=colors.HexColor('#526560')
PALE=colors.HexColor('#EEF4EF');SAND=colors.HexColor('#F6F2E9');LINE=colors.HexColor('#D3DED7')
styles={
 'title':ParagraphStyle('title',fontName='Light',fontSize=29,leading=32,textColor=INK,spaceAfter=12),
 'heading':ParagraphStyle('heading',fontName='Bold',fontSize=19,leading=23,textColor=INK,spaceAfter=10),
 'body':ParagraphStyle('body',fontName='Body',fontSize=11.2,leading=14.5,textColor=INK,spaceAfter=8),
 'small':ParagraphStyle('small',fontName='Body',fontSize=9.5,leading=12.2,textColor=MUTED,spaceAfter=6),
 'card':ParagraphStyle('card',fontName='Body',fontSize=10.5,leading=13.2,textColor=INK),
 'label':ParagraphStyle('label',fontName='Bold',fontSize=9,leading=11,textColor=GREEN,spaceAfter=4),
 'cat':ParagraphStyle('cat',fontName='Bold',fontSize=12,leading=15,textColor=GREEN),
 'source':ParagraphStyle('source',fontName='Body',fontSize=9.2,leading=11.6,textColor=INK),
}
def p(text,style='body'):return Paragraph(text,styles[style])
def cite(*ids):return ' <font color="#526560" size="8.5">'+', '.join(f'[{n:02}]' for n in ids)+'</font>'
def card(number,title,start,later,refs=()):
    label=p(f'{number:02}  {title}','cat')
    cells=[[label,''],[p('STARTING TOOLKIT · ~200,000 YEARS AGO','label'),p('LATER PALEOLITHIC UNLOCKS','label')],
           [p(start,'card'),p(later+cite(*refs),'card')]]
    table=Table(cells,colWidths=[255,255],hAlign='LEFT')
    table.setStyle(TableStyle([('SPAN',(0,0),(1,0)),('BACKGROUND',(0,0),(-1,0),PALE),
        ('BACKGROUND',(0,1),(0,-1),colors.white),('BACKGROUND',(1,1),(1,-1),SAND),
        ('BOX',(0,0),(-1,-1),.6,LINE),('LINEBEFORE',(1,1),(1,-1),.5,LINE),
        ('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),10),('RIGHTPADDING',(0,0),(-1,-1),10),
        ('TOPPADDING',(0,0),(-1,0),6),('BOTTOMPADDING',(0,0),(-1,0),6),
        ('TOPPADDING',(0,1),(-1,1),6),('BOTTOMPADDING',(0,1),(-1,1),1),
        ('TOPPADDING',(0,2),(-1,2),2),('BOTTOMPADDING',(0,2),(-1,2),7)]))
    return KeepTogether([table,Spacer(1,8)])
def chrome(c,doc):
    c.setFillColor(GREEN);c.rect(0,782,612,10,fill=1,stroke=0)
    c.setFont('Bold',9);c.setFillColor(GREEN);c.drawString(51,761,'WORLDGEN  /  TECHNOLOGY DESIGN')
    c.setStrokeColor(LINE);c.setLineWidth(.6);c.line(51,43,561,43)
    c.setFont('Body',8.5);c.setFillColor(MUTED);c.drawString(51,29,'Research brief · 24 October 2026 · Paleolithic scope')
    c.drawRightString(561,29,f'{doc.page} / 5')

story=[]
story.extend([p('Paleolithic\ntechnology paths'.replace('\n','<br/>'),'title'),
 p('From our ~200,000-year starting point to the end of the last Ice Age, roughly 11,700 years ago. <b>No Neolithic progression.</b>'),
 p('<b>How to read this report.</b> “Starting toolkit” means appropriate knowledge, not spawn equipment. Regional traditions and materials matter. Finds establish availability, not exact invention dates.'),
 p('<b>† Plausible reconstruction:</b> a design allowance, not a securely demonstrated practice at the stated date. Regional period names vary; pre-cutoff Epipaleolithic examples are included.','small')])
categories=[
 (1,'Weapons','Handheld stone cutting tools, wooden spears and stone-tipped spear technology. Simple clubs†.',
  'Bows and arrows, spear-throwers and specialized stone or bone points. Slings and wooden/hide shields† are optional inventions, not an established standard kit.',(1,3)),
 (2,'Hunting, trapping & fishing','Hunting spears, digging implements and butchery tools. Simple capture barriers†.',
  'Barbed fishing points, harpoons and hooks; hooks survive from about 23,000 years ago. Netting is supported, with some uses inferred. Basket traps, weirs, snares and deadfalls† are optional reconstructions.',(7,15)),
 (3,'Medicine & injury care','No securely reconstructable standardized medical kit. Supporting an injured person, protecting wounds and immobilizing injuries are plausible practices†.',
  'Dressings, splints and locally learned plant preparations†. A roughly 14,000-year-old tooth shows cavity scraping; this supports exceptional intervention, not routine dentistry or proven herbal cures.',(13,)),
 (4,'Clothing & body protection','Hide scraping and preparation; simple draped skins or wraps†.',
  'Awls, eyed needles and fitted sewn hide garments. Footwear, belts, head coverings and hide padding† are sensible design options; exact early forms are often reconstructed. No default loom-made linen industry.',(8,)),
 (5,'Carrying, containers & storage','Simple hide bundles and useful natural containers†.',
  'Hide bags, baskets and bark/wood vessels†; portable forms are often poorly preserved. Simple pottery is a regional late unlock, roughly 20,000–16,000 years ago. Framed carriers and food caches†; no farming granaries.',(11,15)),
 (6,'Food preparation & preservation','Butchery, cracking and pounding foods, and cooking over fire.',
  'Grinding and wild-plant processing; bread-like foods occur about 14,400 years ago. Drying, smoking, hot-stone boiling and earth-oven cooking† are practical branches with uncertain early recipes. No dairying.',(9,12)),
 (7,'Fire, lighting & heating','Hearths, maintaining fire and carrying embers†. Deliberate ignition where technique and suitable materials are known.',
  'Improved hearths, torches and fat lamps. Friction-fire kits† are a reasonable craft allowance, with uncertain method-specific early dates. Distinguish maintaining fire from reliably starting it.',(2,)),
 (8,'Tools & material processing','Hammerstones, flakes, handaxes, scrapers, worked wood and basic bone implements.',
  'Specialized blades, awls, drills and bone/antler tools. Ground-edge axes occur regionally in Australia by about 44,000–49,000 years ago; this does not imply universal Neolithic-style carpentry.',(1,10,16)),
 (9,'Fibre, adhesives & ceramics','Basic woodworking and material selection; some composite-tool technology exists. Do not assume every group has a sophisticated adhesive recipe.',
  'Twisted cord, rope, plaiting, twining and netting; fibre impressions survive from the Upper Paleolithic. Better hafting adhesives and regional clay firing expand the toolkit. Pottery need not imply agriculture.',(6,11,15)),
 (10,'Shelter & construction','Windbreaks, simple branch shelters and rough shelter frames†; using and improving natural shelters.',
  'Brushwood huts with hearths and bedding are evidenced. Pole-and-hide tents and earth-covered shelters† fit progression; village architecture is outside scope.',(4,12)),
 (11,'Furniture & camp fittings','Grass bedding; conveniently placed logs or stones for sitting†.',
  'Prepared plant bedding, hides, mats†, simple racks, pegs and sleeping platforms†. Formal chairs and fitted stone beds are not established defaults.',(5,12)),
 (12,'Travel, hauling & watercraft','Walking with carried loads; improvised hauling arrangements†.',
  'Drag skids, carrying frames and simple watercraft†. Ancient sea crossings do not reveal an exact boat design. No livestock traction, dog-sled teams or wheeled vehicles.',()),
 (13,'Water & camp sanitation','Collecting water from available sources. Separating waste from sleeping areas†.',
  'Water containers†, settling/straining† and hot-stone boiling†. Camp waste areas are evidenced. No lined village wells; straining alone does not disinfect water.',(12,)),
 (14,'Wild resources & animal relationships','Wild-food gathering, digging and knowledge of local plants and animal behaviour.',
  'Wild-seed processing, seasonal harvesting and caches†. Dogs are present by about 14,200 years ago. Farming, livestock herds and milking are outside scope.',(12,14)),
]
for item in categories[:4]:story.append(card(*item))
story.extend([PageBreak(),p('Crafting, food & materials','heading'),p('These processes enable several kinds of equipment. They should not be hidden inside a single enormous “tools” menu.','small')])
for item in categories[4:9]:story.append(card(*item))
story.extend([PageBreak(),p('Camp, movement & wild resources','heading'),p('Mobile hunter-gatherers can have sophisticated crafts and well-organized homes. This scope does not assume the architecture or economy of later farming villages.','small')])
for item in categories[9:]:story.append(card(*item))
story.extend([PageBreak(),p('What can we actually build?','heading'),p('Build around shelters, hearths and camp fittings. Complexity can grow substantially without importing Neolithic houses. Entries marked † are deliberate gameplay reconstructions.')])
buildings=[
 ('Branch-and-leaf lean-to','Starting option†. Modest protection and limited durability.'),
 ('Conical pole-and-hide tent','Include after adequate hide preparation and bindings. Use a generic description rather than claiming the specific historical tipi design for every prehistoric group.'),
 ('Brushwood hut','Clear inclusion. Ohalo II preserves roughly 23,000-year-old huts, hearths, working areas and bedding. [12]'),
 ('Reed-roofed pole shelter','Reasonable regional design† with suitable poles, bindings and roofing plants. Exact form and weather resistance need testing.'),
 ('Earth-covered shelter','Optional design†. Drainage, roof loads and available timber limit practicality; not a universal prehistoric house type.'),
 ('Bone-supported shelter','Possible late regional branch†. Do not interpret every mammoth-bone ring as a complete dwelling; function can be disputed.'),
 ('Stone windbreak / low wall','Modest camp improvement†. This is not evidence for importing later stone-built village houses or fitted stone furniture.'),
 ('Timber cabin / longhouse','Exclude as a standard unlock. Earlier worked or joined wood proves woodworking capability, not a particular log cabin or Neolithic house plan. [4]'),
 ('Snow shelter / classic igloo','Simple snow windbreaks or dug shelters† may be design allowances. Do not call the much later documented block-built Inuit igloo an established Paleolithic technology.'),
]
rows=[[p('BUILDING','label'),p('RECOMMENDATION','label')]]+[[p(a,'card'),p(b,'card')] for a,b in buildings]
t=Table(rows,colWidths=[146,364]);t.setStyle(TableStyle([('VALIGN',(0,0),(-1,-1),'TOP'),('BACKGROUND',(0,0),(-1,0),PALE),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,SAND]),('LINEBELOW',(0,0),(-1,0),.7,LINE),('LEFTPADDING',(0,0),(-1,-1),9),('RIGHTPADDING',(0,0),(-1,-1),9),('TOPPADDING',(0,0),(-1,-1),7),('BOTTOMPADDING',(0,0),(-1,-1),7)]));story.extend([t,Spacer(1,12)])
story.append(p('<b>Beds are a clear inclusion.</b> Prepared grass bedding is evidenced around 200,000 years ago. Upgrade its warmth, dryness and parasite control before adding framed furniture†. A bed does not require a modern bed frame. [5]'))
story.append(p('<b>Medicine needs restraint.</b> Care and wound protection† make useful mechanics. Exact ancient treatments and their effectiveness are rarely recoverable; isolated dental intervention does not justify generic healing potions. [13]'))
story.extend([PageBreak(),p('Design the unlocks as processes','heading'),p('Keep three constraints separate: <b>technology</b> is knowing the process; <b>ability</b> is performing it well; <b>materials</b> determine what is possible locally. Culture affects transmission and established practice.')])
processes=[('Cordage','Stronger bindings, carrying straps, net-making and shelter frames.'),('Hide preparation & sewing','Fitted clothing, bags, coverings and bedding.'),('Woodworking & hafting','Better tools, spears and shelter components; sophistication need not mean cabins.'),('Grinding & controlled heating','Wild-food processing and cooking; regional pottery is a late branch, not a farming prerequisite.')]
for name,text in processes:story.append(p('<b>'+name+':</b> '+text,'small'))
story.append(p('Knowing how to make a bow is technology; making a dependable bow and shooting accurately are different skills. Recipes should use properties such as suitable fibres, workable timber and knappable stone—not require particular real-world species names.','small'))
story.append(p('<b>Recommended scope:</b> start with stone/wood tools, spears, fire handling, hide preparation and plant bedding. Add cordage, sewn clothing, specialized hunting/fishing and better shelters. Reserve pottery and dogs for regional late progression; omit farming, dairying, village infrastructure, wheels and metalworking.','small'))
story.extend([Spacer(1,6),p('Evidence & further reading','heading')])
sources=[
 ('Smithsonian — Stone tools','https://humanorigins.si.edu/evidence/behavior/stone-tools'),
 ('Nature — Fire-making evidence around 400,000 years ago','https://www.nature.com/articles/s41586-025-09855-6'),
 ('PLOS ONE — Early stone-tipped projectiles','https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0078092'),
 ('Nature — Structural wood use at Kalambo Falls','https://www.nature.com/articles/s41586-023-06557-9'),
 ('Science / SUNY — Grass bedding around 200,000 years ago','https://researchconnect.suny.edu/en/publications/fire-and-grass-bedding-construction-200-thousand-years-ago-at-bor/'),
 ('Scientific Reports — Neanderthal fibre technology','https://www.nature.com/articles/s41598-020-61839-w'),
 ('PNAS — Paleolithic shell fishhooks, Okinawa','https://doi.org/10.1073/pnas.1607857113'),
 ('Smithsonian — Making clothing','https://humanorigins.si.edu/evidence/behavior/making-clothing'),
 ('PNAS — Bread-like foods before agriculture, 14,400 years ago','https://doi.org/10.1073/PNAS.1801071115'),
 ('Australian Archaeology — Ground-edge axes, 44,000–49,000 years ago','https://doi.org/10.1080/03122417.2016.1164379'),
 ('Science — Early pottery in China; earliest dating remains debated','https://pubmed.ncbi.nlm.nih.gov/22745428/'),
 ('Journal of Lithic Studies — Ohalo II huts, tools and food processing','https://journals.ed.ac.uk/lithicstudies/article/view/1646'),
 ('Scientific Reports — Upper Paleolithic dental intervention','https://www.nature.com/articles/srep12150'),
 ('Journal of Archaeological Science — Bonn-Oberkassel dogs','https://doi.org/10.1016/j.jas.2018.01.004'),
 ('PLOS ONE — Upper Paleolithic fishing and netting evidence','https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0311302'),
 ('Nature — Systematic bone tools at 1.5 million years','https://www.nature.com/articles/s41586-025-08652-5'),
]
for index,(title,url) in enumerate(sources,1):
    story.append(p(f'<font color="#526560">[{index:02}]</font> <link href="{url.replace("&","&amp;")}" color="#245F4C">{title}</link>','source'));story.append(Spacer(1,4))
story.append(Spacer(1,5));story.append(p('Evidence establishes particular populations and practices, not universal knowledge. † entries are explicit design inferences; missing organic remains do not demonstrate that a technology was absent.','small'))
doc=SimpleDocTemplate(str(OUT),pagesize=(612,792),rightMargin=51,leftMargin=51,topMargin=51,bottomMargin=54,
    title='WorldGen — Paleolithic Technology Paths',author='WorldGen design research',subject='Starting toolkit and later Paleolithic crafting; no Neolithic progression')
doc.build(story,onFirstPage=chrome,onLaterPages=chrome)
print(OUT)
