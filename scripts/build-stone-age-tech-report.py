"""Typeset the owner-requested Stone Age technology design report as a PDF."""
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

OUT=ROOT/'docs/reports/WorldGen - Stone Age Technology Guide.pdf'
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
    cells=[[label,''],[p('STARTING TOOLKIT · ~200,000 YEARS AGO','label'),p('LATER STONE AGE UNLOCKS','label')],
           [p(start,'card'),p(later+cite(*refs),'card')]]
    table=Table(cells,colWidths=[255,255],hAlign='LEFT')
    table.setStyle(TableStyle([('SPAN',(0,0),(1,0)),('BACKGROUND',(0,0),(-1,0),PALE),
        ('BACKGROUND',(0,1),(0,-1),colors.white),('BACKGROUND',(1,1),(1,-1),SAND),
        ('BOX',(0,0),(-1,-1),.6,LINE),('LINEBEFORE',(1,1),(1,-1),.5,LINE),
        ('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),10),('RIGHTPADDING',(0,0),(-1,-1),10),
        ('TOPPADDING',(0,0),(-1,0),6),('BOTTOMPADDING',(0,0),(-1,0),6),
        ('TOPPADDING',(0,1),(-1,1),6),('BOTTOMPADDING',(0,1),(-1,1),1),
        ('TOPPADDING',(0,2),(-1,2),2),('BOTTOMPADDING',(0,2),(-1,2),9)]))
    return KeepTogether([table,Spacer(1,10)])
def chrome(c,doc):
    c.setFillColor(GREEN);c.rect(0,782,612,10,fill=1,stroke=0)
    c.setFont('Bold',9);c.setFillColor(GREEN);c.drawString(51,761,'WORLDGEN  /  TECHNOLOGY DESIGN')
    c.setStrokeColor(LINE);c.setLineWidth(.6);c.line(51,43,561,43)
    c.setFont('Body',8.5);c.setFillColor(MUTED);c.drawString(51,29,'Research brief · 24 October 2026 · Stone Age scope')
    c.drawRightString(561,29,f'{doc.page} / 5')

story=[]
story.extend([p('Stone Age\ntechnology paths'.replace('\n','<br/>'),'title'),
 p('A practical crafting scope: from our ~200,000-year starting point through the Neolithic. <b>No wheels or metalworking.</b>'),
 p('<b>How to read this report.</b> “Starting toolkit” means historically appropriate knowledge, not equipment everyone spawns with. Regional traditions, materials and proficiency still matter. Later finds provide evidence of availability, not exact invention dates.'),
 p('<b>† Plausible reconstruction:</b> suitable for gameplay consideration, but not securely demonstrated at the stated starting date. The Neolithic is the final part of the Stone Age; its dates vary by region.','small')])
categories=[
 (1,'Weapons','Clubs, handheld stone cutting tools, wooden spears; stone-tipped spear technology.',
  'Bows and arrows, spear-throwers, improved spearheads and stone-headed maces. Slings and wooden/hide shields† are optional branches.',(1,3)),
 (2,'Hunting, trapping & fishing','Hunting spears, digging implements and butchery tools. Simple capture barriers†.',
  'Bone harpoons, fishhooks and line, nets, basket traps and fish weirs. Snares and deadfalls† fit progression after reliable bindings.',(8,)),
 (3,'Medicine & injury care','No securely reconstructable standardized medical kit. Supporting an injured person, protecting wounds and immobilizing injuries are plausible practices†.',
  'Better dressings, splints, locally learned plant preparations† and specialist treatment tools. Some Stone Age surgery and dentistry are evidenced; these remain exceptional practices.',(13,15)),
 (4,'Clothing & body protection','Hide scraping and preparation; simple draped skins or wraps†.',
  'Pierced and sewn garments, bone needles, fitted cold-weather clothing, footwear, belts, hats and woven plant-fibre clothing. Protective hide garments or padding† offer modest protection.',(1,9)),
 (5,'Carrying, containers & storage','Simple hide bundles and useful natural containers†.',
  'Sewn bags, baskets, bark containers, wooden bowls, ceramic pots and storage jars. Back-carriers†, storage pits and granaries provide larger upgrades.',()),
 (6,'Food preparation & preservation','Butchery, cracking and pounding foods, and cooking over fire.',
  'Specialized grinding, boiling and stewing, ovens, flatbread, fermentation and dairy processing. Drying, smoking and salt-curing fit practical progression; their earliest origins are difficult to date.',(10,16)),
 (7,'Fire, lighting & heating','Hearths, maintaining fire and carrying embers†. Deliberate ignition where technique and suitable materials are known.',
  'Friction-fire kits, torches, fat lamps, improved hearths, earth ovens and clay ovens. Fire supports material crafts as well as cooking.',(2,)),
 (8,'Tools & material processing','Hammerstones, flakes, handaxes, scrapers, worked wood and basic bone implements.',
  'Specialized blades, awls, drills, saw-like tools, hafted axes and adzes, polished cutting edges, wedges and advanced carpentry tools.',(1,4,11)),
 (9,'Fibre, adhesives & ceramics','Basic woodworking and material selection; some composite-tool technology exists. Do not assume every group has a sophisticated adhesive recipe.',
  'Twisted cord, rope, braiding, matting, basketry, spinning, weaving, improved adhesives and sealants, pottery and controlled firing. A major enabling branch.',(7,9)),
 (10,'Shelter & construction','Windbreaks, simple branch shelters and rough shelter frames†; using and improving natural shelters.',
  'Hide tents, thatched huts, earth-covered shelters, wattle-and-daub, mudbrick and stone houses, substantial timber buildings, fences, platforms and modest footbridges†.',(5,11,12)),
 (11,'Furniture & camp fittings','Grass bedding; conveniently placed logs or stones for sitting†.',
  'Mats, hide bedding, raised sleeping platforms†, racks, pegs, shelves, storage bins and built-in stone beds. Wooden stools, tables and framed beds† become plausible with joinery.',(6,12)),
 (12,'Travel, hauling & watercraft','Walking with carried loads; improvised hauling arrangements†.',
  'Drag sledges, carrying frames†, dugout canoes, paddles and regional watercraft. Animal-assisted hauling can follow suitable domestication. No wheeled vehicles.',(8,)),
 (13,'Water & camp sanitation','Collecting water from available sources. Separating waste from sleeping areas†.',
  'Water-carrying vessels, settling/straining†, boiling with suitable containers, rain collection†, dug and lined wells, drainage and organized waste disposal.',(11,)),
 (14,'Cultivation & animal keeping','Wild-food gathering and digging tools—not established farming or livestock.',
  'Plant tending, sowing, harvesting, seed selection, crop storage, livestock, pens and dairying. Dogs belong to later Stone Age progression, but their domestication predates farming.',(11,16)),
]
for item in categories[:4]:story.append(card(*item))
story.extend([PageBreak(),p('Crafting, food & materials','heading'),p('These processes enable several kinds of equipment. They should not be hidden inside a single enormous “tools” menu.','small')])
for item in categories[4:9]:story.append(card(*item))
story.extend([PageBreak(),p('Camp, movement & settlement','heading'),p('Building capability grows with materials, preparation, labour and knowledge. Agriculture is not a prerequisite for every advanced hunter-gatherer craft.','small')])
for item in categories[9:]:story.append(card(*item))
story.extend([PageBreak(),p('What can we actually build?','heading'),p('Stone tools do not limit us to crude huts. Substantial houses belong near the top of this scope; preparing and transporting their materials should be the challenge.')])
buildings=[
 ('Branch-and-leaf lean-to','Starting option†. Modest protection and limited durability.'),
 ('Conical pole-and-hide tent','Include after adequate hide preparation and bindings. Use a generic description rather than claiming the specific historical tipi design for every prehistoric group.'),
 ('Thatch or reed hut','Strong later option: poles, bindings and locally suitable roofing plants.'),
 ('Wattle-and-daub house','Within the Neolithic ceiling: woven framework covered with an earth mixture.'),
 ('Earth-covered or turf shelter','Regional construction family†, dependent on drainage, climate and materials.'),
 ('Stone house','Definitely within scope by the Neolithic. Skara Brae preserves stone houses and built-in furniture. [12]'),
 ('Substantial timber house','Within scope. Neolithic wells demonstrate substantial worked timbers and sophisticated joints; metal nails are not required. [11]'),
 ('Log-built structure','Credible later option. Preserved log well constructions establish capability, without proving every modern cabin design existed. [11]'),
 ('Classic block-built igloo','Optional cold-climate invention†, not a 200,000-year baseline. The documented Arctic tradition is much later. Simple snow windbreaks or excavated snow shelters† are easier early allowances. [14]'),
]
rows=[[p('BUILDING','label'),p('RECOMMENDATION','label')]]+[[p(a,'card'),p(b,'card')] for a,b in buildings]
t=Table(rows,colWidths=[146,364]);t.setStyle(TableStyle([('VALIGN',(0,0),(-1,-1),'TOP'),('BACKGROUND',(0,0),(-1,0),PALE),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,SAND]),('LINEBELOW',(0,0),(-1,0),.7,LINE),('LEFTPADDING',(0,0),(-1,-1),9),('RIGHTPADDING',(0,0),(-1,-1),9),('TOPPADDING',(0,0),(-1,-1),7),('BOTTOMPADDING',(0,0),(-1,-1),7)]));story.extend([t,Spacer(1,12)])
story.append(p('<b>Beds are a clear inclusion.</b> Grass bedding is evidenced around 200,000 years ago. Neolithic Skara Brae had stone box-beds, storage furniture and hearths. Elaborate backed chairs are a less secure default than simple seats, stools or built-in benches. [6, 12]'))
story.append(p('<b>Medicine needs restraint.</b> Exact preparations and effectiveness are rarely recoverable. Even the beeswax dental filling is interpreted cautiously by its researchers. Avoid interchangeable herb-based healing potions. [13]'))
story.extend([PageBreak(),p('Design the unlocks as processes','heading'),p('Keep three constraints separate: <b>technology</b> is knowing the process; <b>ability</b> is performing it well; <b>materials</b> determine what is possible locally. Culture affects transmission and established practice.')])
processes=[('Cordage','Stronger bindings, carrying straps, net-making and some construction.'),('Hide preparation & sewing','Fitted clothing, bags, coverings and bedding.'),('Controlled clay firing','Pots, cooking vessels and larger storage containers.'),('Wood joinery','Better buildings, furniture, wells and wooden structures.'),('Grinding & food processing','New foods, independently of whether ingredients are cultivated.')]
for name,text in processes:story.append(p('<b>'+name+':</b> '+text,'small'))
story.append(p('Knowing how to make a bow is technology; making a dependable bow and shooting accurately are different skills. Recipes should use properties such as suitable fibres, workable timber and knappable stone—not require particular real-world species names.','small'))
story.extend([Spacer(1,6),p('Evidence & further reading','heading')])
sources=[
 ('Smithsonian — Stone tools','https://humanorigins.si.edu/evidence/behavior/stone-tools'),
 ('Nature — Fire-making evidence around 400,000 years ago','https://www.nature.com/articles/s41586-025-09855-6'),
 ('PLOS ONE — Early stone-tipped projectiles','https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0078092'),
 ('Nature — Systematic bone tools at 1.5 million years','https://www.nature.com/articles/s41586-025-08652-5'),
 ('Nature — Structural wood use at Kalambo Falls','https://www.nature.com/articles/s41586-023-06557-9'),
 ('Science / SUNY — Grass bedding around 200,000 years ago','https://researchconnect.suny.edu/en/publications/fire-and-grass-bedding-construction-200-thousand-years-ago-at-bor/'),
 ('Scientific Reports — Neanderthal fibre technology','https://www.nature.com/articles/s41598-020-61839-w'),
 ('National Museum of Denmark — Mesolithic coastal equipment','https://en.natmus.dk/historical-knowledge/denmark/prehistoric-period-until-1050-ad/the-mesolithic-period/the-dugout-boat-from-broksoe/coastal-settlements/'),
 ('Natural History Museum Vienna — Textile equipment','https://www.nhm.at/en/research/prehistory/research/textile_research/textile_equipment'),
 ('PNAS — Bread-like foods before agriculture','https://doi.org/10.1073/PNAS.1801071115'),
 ('PLOS ONE — Neolithic wooden wells and carpentry','https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0051374'),
 ('Historic Environment Scotland — Skara Brae houses and furniture','https://www.historicenvironment.scot/visit/all/skara-brae/history-and-stories/'),
 ('PLOS ONE — Possible Neolithic beeswax dental filling','https://pmc.ncbi.nlm.nih.gov/articles/PMC3446997/'),
 ('Library and Archives Canada — Inuit snow-house tradition','https://epe.lac-bac.gc.ca/100/205/301/ic/cdc/world_around_me/world/vr1/pop-ups/19881069.htm?nodisclaimer=1'),
 ('Scientific Reports — Neolithic cranial surgery','https://www.nature.com/articles/s41598-018-23914-1'),
 ('Nature — Neolithic cheese-making evidence','https://doi.org/10.1038/nature11698'),
]
for index,(title,url) in enumerate(sources,1):
    story.append(p(f'<font color="#526560">[{index:02}]</font> <link href="{url.replace("&","&amp;")}" color="#245F4C">{title}</link>','source'));story.append(Spacer(1,4))
story.append(Spacer(1,5));story.append(p('Evidence establishes particular populations and practices, not universal knowledge. † entries are explicit design inferences; missing organic remains do not demonstrate that a technology was absent.','small'))
doc=SimpleDocTemplate(str(OUT),pagesize=(612,792),rightMargin=51,leftMargin=51,topMargin=51,bottomMargin=54,
    title='WorldGen — Stone Age Technology Paths',author='WorldGen design research',subject='Starting toolkit and later Stone Age crafting through the Neolithic')
doc.build(story,onFirstPage=chrome,onLaterPages=chrome)
print(OUT)
