"""Owner-requested research roadmap. Generates vector PDF, no game changes."""
from pathlib import Path
import sys, math
from urllib.parse import quote
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'artifacts/report-tools/python'))
from reportlab.pdfgen import canvas
from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import Paragraph, Table, TableStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont

OUT=ROOT/'docs/reports/WorldGen - Survival Crafting Roadmap.pdf'
OUT.parent.mkdir(parents=True,exist_ok=True)
for name,file in [('Body','calibri.ttf'),('Bold','calibrib.ttf'),('Italic','calibrii.ttf'),('Light','calibril.ttf')]:
    pdfmetrics.registerFont(TTFont(name,str(Path('C:/Windows/Fonts')/file)))
pdfmetrics.registerFontFamily('Body',normal='Body',bold='Bold',italic='Italic',boldItalic='Bold')
INK=colors.HexColor('#203632'); GREEN=colors.HexColor('#25644E'); MUTED=colors.HexColor('#596C65')
LINE=colors.HexColor('#CBDAD2'); PALE=colors.HexColor('#EFF5F0'); SAND=colors.HexColor('#F8F2E7'); GOLD=colors.HexColor('#A7722A')
W,H=612,792; PAGE=0; TOTAL=10
C=canvas.Canvas(str(OUT),pagesize=(W,H),pageCompression=1)
C.setTitle('WorldGen | Survival Crafting Roadmap'); C.setAuthor('WorldGen design research')
C.setSubject('Implementation order, dependencies and research briefs; Paleolithic solo survival')
LAYOUT=[]
def para(text,x,y,width,size=11,leading=None,color=INK,font='Body'):
    obj=Paragraph(text,ParagraphStyle('p',fontName=font,fontSize=size,leading=leading or size*1.28,textColor=color))
    _,height=obj.wrap(width,2000); obj.drawOn(C,x,y-height); LAYOUT.append((PAGE,x,y-height,width,height))
    if y-height<48: raise RuntimeError(f'Page {PAGE} overflow: {text[:65]} -> {y-height}')
    return y-height
def label(text,x,y,color=GREEN,size=9):
    C.setFillColor(color);C.setFont('Bold',size);C.drawString(x,y,text)
def box(x,y,w,h,fill=PALE,stroke=LINE,r=8):
    C.setFillColor(fill);C.setStrokeColor(stroke);C.setLineWidth(.65);C.roundRect(x,y,w,h,r,fill=1,stroke=1)
def page(title,subtitle='',landscape=False):
    global PAGE,W,H
    if PAGE:C.showPage()
    PAGE+=1;W,H=(792,612) if landscape else (612,792);C.setPageSize((W,H))
    C.setFillColor(GREEN);C.rect(0,H-9,W,9,fill=1,stroke=0)
    label('WORLDGEN  /  SURVIVAL & CRAFTING ROADMAP',44,H-34)
    C.setFont('Body',8);C.setFillColor(MUTED);C.drawRightString(W-44,H-34,'27 OCTOBER 2026')
    C.setStrokeColor(LINE);C.line(44,43,W-44,43)
    C.setFont('Body',8);C.setFillColor(MUTED);C.drawString(44,28,'Design proposal | Software order, not a universal invention timeline')
    C.drawRightString(W-44,28,f'{PAGE:02} / {TOTAL:02}')
    y=para(title,44,H-65,W-88,size=26,leading=29,font='Light')
    if subtitle:y=para(subtitle,44,y-9,W-88,size=10.5,color=MUTED)
    return y
def refs(ids):
    return ' '.join(f'<link href="#sources" color="#25644E">[{s}]</link>' for s in ids)
def note(text,x,y,w,fill=SAND):
    obj=Paragraph(text,ParagraphStyle('note',fontName='Body',fontSize=10.5,leading=13.5,textColor=INK));_,h=obj.wrap(w-24,1000)
    if y-h-22<48: raise RuntimeError(f'Page {PAGE} note overflow')
    box(x,y-h-22,w,h+22,fill);obj.drawOn(C,x+12,y-h-11);return y-h-22
def arrow(points,dashed=False):
    C.saveState();C.setStrokeColor(GOLD if dashed else GREEN);C.setFillColor(GOLD if dashed else GREEN);C.setLineWidth(1.1)
    if dashed:C.setDash(4,3)
    p=C.beginPath();p.moveTo(*points[0])
    for pt in points[1:]:p.lineTo(*pt)
    C.drawPath(p);a,b=points[-2],points[-1];angle=math.atan2(b[1]-a[1],b[0]-a[0]);l=5
    p=C.beginPath();p.moveTo(*b);p.lineTo(b[0]-l*math.cos(angle-.5),b[1]-l*math.sin(angle-.5));p.lineTo(b[0]-l*math.cos(angle+.5),b[1]-l*math.sin(angle+.5));p.close();C.drawPath(p,fill=1,stroke=0);C.restoreState()

SOURCES=[
('S1','NPS: Ten Essentials','https://www.nps.gov/articles/10essentials.htm','Modern safety guidance supports shelter, exposure and hydration priorities; it does not reconstruct an ancient daily schedule.'),
('S2','Smithsonian: Early Stone Age tools','https://humanorigins.si.edu/evidence/behavior/stone-tools/early-stone-age-tools','Hammerstones, cores and flakes: usable cutting edges can precede a handled knife or elaborate point.'),
('S3','Venditti et al., 2022: Woodworking traces at Schöningen','https://www.nature.com/articles/s41598-022-24769-3','Use-wear and residues support stone-tool working of wood. The site date is not used as a gameplay unlock.'),
('S4','Nadel et al., 2004: Bedding and brush huts at Ohalo II','https://pubmed.ncbi.nlm.nih.gov/15090648/','A 23,000-year-old camp preserves huts and grass bedding. This does not establish every proposed shelter form.'),
('S5','Sorensen et al., 2018: Fire-making technology','https://www.nature.com/articles/s41598-018-28342-9','Microwear and experiments support flint/pyrite fire-making inference. Specific friction-kit histories are less securely preserved.'),
('S6','Hardy et al., 2020: Fibre technology','https://www.nature.com/articles/s41598-020-61839-w','Direct evidence for twisted cordage. Exact basket, bag and knot designs still require their own support.'),
('S7','Smithsonian: Making clothing','https://humanorigins.si.edu/evidence/behavior/making-clothing','Awls, perforators and needles support clothing production. An eyed needle is not the only possible route to stitching.'),
('S8','Lombard, 2005: Hunting and hafting at Sibudu','https://pure.uj.ac.za/en/publications/evidence-of-hunting-and-hafting-during-the-middle-stone-age-at-si/','Residues/use-wear support hafted points, probable plant binding and possible resin use; not a universal spear recipe.'),
('S9','Wadley, 2010: Compound-adhesive replication','https://www.journals.uchicago.edu/doi/10.1086/649836','Experiments distinguish collection, preparation and final hafting. Replication demonstrates feasibility, not a unique ancient recipe.'),
('S10','Fujita et al., 2016: Maritime adaptation and shell hooks','https://doi.org/10.1073/pnas.1607857113','Late Pleistocene Okinawan fishing evidence supports a regional branch, not a universal starting kit.'),
('S11','Kozowyk et al., 2017: Birch-tar production experiments','https://www.nature.com/articles/s41598-017-08106-7','Alternative production methods demonstrate method-specific material, heating and labor requirements.'),
('S12','NOAA repository: Land Safety & Survival, 2003','https://repository.library.noaa.gov/view/noaa/34660/noaa_34660_DS1.pdf','Modern educational guidance: fire requires work and must be balanced against shelter and other needs.'),
]

# ID, title, build scope, prerequisites, completion check, research, search terms, evidence, sources.
MILESTONES=[
('M1','World resources & immediate needs',
'Reachable water and edible resources; useful stones, branches, fibres and tinder. Retain actual size, shape, mass, material, moisture and stable identity.',
'Existing inventory/needs and persistent ownership. Geology and plant habitat govern availability; bare hands provide no hidden storage.',
'A naked player can obtain water/food, choose materials, put them down and save/reload without duplication.',
'Which sources work in the chosen biome and season? What needs processing? How do branch forks, rock suitability and moisture affect use?',
'primitive material selection; local toolstone identification; wild plant processing',
'Selection of suitable materials is evidenced. Spawn density, gathering time and interaction range are gameplay decisions.', ['S1','S2']),
('M2','Stone percussion & usable edges',
'Select hammer/support stones, work a suitable core and retain useful flakes and the altered core. Start with one visible, persistent percussion process.',
'M1 materials and shared work actions. Require suitable fracture properties; a handled knife, cordage or fire is not a prerequisite.',
'Make a cutting flake, pause/resume work, and use its edge on another material. Outputs retain their identity and geometry.',
'Core/flake versus shaped-point production; hard-hammer and supported methods; local rock; edge wear; whether direct aiming adds meaningful control.',
'experimental archaeology hard hammer flake production; unhafted cutting flake',
'Cores, hammerstones and cutting flakes are well supported. Exact fracture physics and precision minigames are optional design choices.', ['S2']),
('M3','Emergency cover, bedding & sleep',
'Use a natural refuge or arrange supports, poles and vegetation for a windbreak/roof. Add insulating bedding, dryness, cover and rest effects.',
'M1 materials, placement and saved pieces. Trees/forks/weighted contact can support a first shelter; rope and an axe are not universal gates.',
'Arrange a functional sleeping space with observable wind/rain protection and rest benefits. Pieces persist and can be rearranged.',
'Match supports and roofing to local plants. How much coverage, bedding and maintenance are needed? Which exact forms are reconstructions?',
'primitive shelter no cordage forked supports; debris bedding experimental archaeology',
'Brush huts and prepared bedding are evidenced. Each lean-to arrangement and weather rating requires explicit reconstruction choices.', ['S1','S4']),
('M4','Fire-making, fuel & cooking heat',
'One researched ignition method, dry tinder, graduated fuel and a maintained hearth. Include material/condition failures and preserving embers.',
'M1 materials. Friction routes may need M2 woodworking; mineral percussion needs appropriate minerals. A bow-drill route also needs cordage.',
'Establish, feed, extinguish and save a fire; its heat affects exposure and its fuel is consumed.',
'Choose one method first. What preparation, dryness, exertion and tinder are required? Separate ignition, maintenance, cooking and water treatment.',
'friction fire primitive stone tools hand drill; flint pyrite experimental archaeology',
'Mineral-percussion evidence exists. Do not give every community every method, or use arbitrary rocks as fire starters.', ['S5','S12']),
('M5','Plant ties, cordage & carrying',
'Simple plant ties first; process fibres into stronger cordage. Add a researched carrying bundle/sling or bag, then a genuinely water-holding container route.',
'M1 plants; M2 where processing needs an edge. Natural ties can precede high-quality twisted cord. Waterproofing depends on the container method.',
'Crafted equipment replaces the test bag, grants real capacity/attachments and can be set down loaded while retaining contents.',
'Immediate-use versus retted/dried fibres; splicing; wet strength; container closure; carrying versus waterproof storage; realistic pack access.',
'natural fibre cordage bast processing; carrying sling; bark water container',
'Twisted fibre technology is evidenced. Each exact bag/basket needs regional or experimental support; a basket is not automatically a water vessel.', ['S6']),
('M6','Wooden utility tools & first weapons',
'Trim, scrape, split, abrade and sharpen suitable wood into a digging stick, useful pole and wooden spear. Throwing-stick designs are an additional option.',
'M1 wood and M2 cutting capability or a suitable abrasive route. Large pieces can rest on the ground. No stone point or adhesive is required.',
'Dimensions affect handling/use. One tool improves gathering and a wooden spear functions as a weapon; both can wear and be reworked.',
'Wood selection, natural shape, green/dry working, point preparation, balance, straightening, maintenance and realistic effort.',
'wooden spear manufacture stone flakes; digging stick experimental archaeology',
'Stone-tool woodworking is evidenced. Fire treatment is a separate method to investigate, not a mandatory automatic strength upgrade.', ['S3']),
('M7','Animal acquisition, butchery & food use',
'Damage, animal death/ragdoll, persistent carcasses, staged butchery and scaled yields. Meat/fat feed the player; hides, bone and sinew feed later crafts.',
'A viable acquisition route; M6 is one option, not the only one. M2 edges enable butchery. M4 enables heat-based cooking, not all food acquisition.',
'A catch becomes physical resources that must be processed, carried and stored. Eating has appropriate hunger/energy effects.',
'Anatomy and yields; cutting versus disarticulation; spoilage; transport; cooking without a pot; accessible small foods before large-game hunting.',
'flake butchery experimental archaeology; carcass processing; primitive cooking',
'Stone edges used for food processing are established. Success, spoilage speed and yields require research and game balance.', ['S2','S12']),
('M8','Hide preparation, clothing & bone tools',
'Simple coverings before complex garments; scrape/process hides, then add closures, footwear and bone/antler tools. Awls can support stitching before eyed needles.',
'M7 or another valid animal-material source; M2 cutting/scraping; M5 where ties/thread are needed. Some methods need heat, time or other materials.',
'A covering changes exposure by coverage, wetness and condition. Saved processing progress prevents instant conversion to finished leather.',
'Rawhide, fur and softened hide are different outputs. Compare drying/softening, regional treatments, closures, awl stitching and fitted garments.',
'stone scraper hide processing; bone awl sewing hide clothing',
'Clothing tools are evidenced. Exact treatments and garment forms need separate support; a wrap and a tailored coat are different projects.', ['S7']),
('M9','Composite tools, hafting & repair',
'Join a shaped point/edge to a prepared shaft/handle using one researched fixation method. Preserve partial assembly and replaceable components.',
'M2 head, M6 shaft, and suitable fixation: binding, adhesive or a validated combination. Heat is required only by methods that need it.',
'Assemble in stages with free hands/worksite support. Attachment state affects reliability; replace damaged heads or bindings while retaining the shaft.',
'Point base; seat/socket/split; wrapping/tension; adhesive preparation and setting; moisture/heat response; fit, failure, recovery and repair.',
'stone point hafting natural materials; sinew plant cord resin; split shaft spear',
'Hafting and varied adhesives/bindings are evidenced. Audit modern substitutions before treating a demo as a from-nothing process.', ['S8','S9','S11']),
('M10','A maintained camp & regional equipment',
'Improve shelter, bedding, storage and work areas; add preservation and fishing/trapping branches. Later add appropriate projectile systems and cooperative work.',
'Earlier capabilities as each design requires. Hooks/nets, bows/throwers, drying/smoking and water treatment have separate knowledge/material needs.',
'The camp supports repeated work and maintenance. Improved gear has measurable benefits; NPCs share material/labor rules, with distant work aggregated.',
'Choose a regional tradition. Research one food method and one shelter improvement at a time; assess labor, seasonality, upkeep and passive-catch limits.',
'Upper Paleolithic fishing; primitive food drying smoking; brush hut construction',
'Hooks, shelters and clothing are evidenced regionally. Complex traps, boats, projectiles and shelters need individual briefs; avoid universal kits.', ['S4','S7','S10']),
]

# 1. Implementation spine.
y=page('From nothing to a working camp','A researched feature roadmap for a knowledgeable Homo sapiens, roughly 20,000–12,000 years ago, starting alone with no equipment.')
y=para('<b>Purpose.</b> Build a complete survival loop, then deepen its crafts. These are software priorities, not rigid player steps or invention dates. Weather can make shelter/fire urgent; a skilled character already knows the relevant local techniques.',44,y-18,524)
rows=[['ORDER','FEATURE MILESTONE','RESULT FOR THE PLAYER']]
short=[('M1','World resources & needs','Water, food and useful raw materials'),('M2','Stone percussion & edges','A cutting tool, without a handled knife'),('M3','Emergency shelter & bedding','A protected sleeping place'),('M4','Fire & fuel','Warmth, cooking heat and kept embers'),('M5','Fibre & carrying','Bindings and capacity beyond two hands'),('M6','Wooden implements','Gathering tools and a wooden spear'),('M7','Animals & processing','Food, carcasses and animal materials'),('M8','Hide & body protection','Coverings, footwear and sewing branches'),('M9','Hafting & repair','Equipment with replaceable components'),('M10','An established camp','Storage, preservation and regional gear')]
rows.extend(short)
style=ParagraphStyle('table',fontName='Body',fontSize=10.2,leading=12.8,textColor=INK)
t=Table([[Paragraph(v,style) for v in row] for row in rows],colWidths=[48,200,276])
t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),PALE),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,colors.HexColor('#F8FAF8')]),('VALIGN',(0,0),(-1,-1),'TOP'),('LINEBELOW',(0,0),(-1,0),.7,LINE),('LINEBELOW',(0,1),(-1,-1),.3,LINE),('LEFTPADDING',(0,0),(-1,-1),8),('RIGHTPADDING',(0,0),(-1,-1),8),('TOPPADDING',(0,0),(-1,-1),8),('BOTTOMPADDING',(0,0),(-1,-1),8)]))
_,th=t.wrap(524,1000);t.drawOn(C,44,y-18-th);y-=18+th
if y<110:raise RuntimeError('Overview table overflow')
y=note('<b>First complete checkpoint: M1–M6.</b> Obtain water/food, make an edge, establish a sleeping place and fire, carry supplies and prepare wood. Hunting is an additional route, not the only way to survive. Develop branches in parallel where dependencies permit.',44,y-18,524)
para('<b>Before M1:</b> retain accepted inventory, needs/HUD and controls. Build saved workpieces, contextual operations and construction ownership alongside the first real craft. Research cards distinguish evidence from proposed game abstractions.',44,y-12,524,size=9.5,color=MUTED)

# 2. Major dependencies. Complete prerequisite lists are on the cards.
page('The dependency web','Solid arrows = required capability/material route. Dashed arrows = an optional route or improvement. Pages 3–7 give the complete conditions.',True)
box(44,452,704,38,PALE)
para('<b>Shared foundation:</b> physical items + saved workpieces + accessible materials + known procedures + tool capabilities. No generic workbench or handled-knife gate.',56,480,680,size=10)
def node(title,sub,x,top,w=150,h=61):
    box(x,top-h,w,h);para(title,x+9,top-9,w-18,size=12,font='Bold');para(sub,x+9,top-29,w-18,size=9.1,leading=11.1,color=MUTED)
arrow([(194,410),(210,410),(210,101),(224,101)])
for yy in [410,307,204]:arrow([(210,yy),(224,yy)])
arrow([(374,410),(402,410)]);arrow([(552,410),(580,410)])
arrow([(374,397),(386,397),(386,307),(402,307)])
arrow([(374,101),(402,101)],True)
arrow([(477,379),(477,338)],True);arrow([(477,277),(477,235)])
arrow([(552,307),(580,307)],True);arrow([(552,204),(580,204)])
arrow([(374,204),(389,204),(389,116),(402,116)],True)
arrow([(552,101),(580,101)])
node('M1  Materials','Water, food, stone, wood, fibres and fuel',44,440)
node('M2  Working edges','Flakes, hammerstones; abrasive alternatives',224,440)
node('M3  Cover & bedding','Natural supports can avoid early cordage',224,337)
node('M4  Fire','Method-specific tools, tinder and fuel',224,234)
node('M5  Ties & carrying','Natural ties / processed fibres; containers',224,145)
node('M6  Wood tools','Shafts, digging tools and wooden spears',402,440)
node('M7  Animal resources','Acquisition + cutting + processing',402,337)
node('M8  Hide preparation','Coverings, bone tools and sewing branches',402,234)
node('M10  Better camp','Build on M3; add storage and upkeep',402,145)
node('M9  Composite tools','Shaft + head + fixation; expanded on page 8',580,440)
node('Specialized food gear','Fishing/trapping; also fibre/wood inputs',580,337)
node('Fitted protection','Suitable hide, closures, thread and piercing tool',580,234)
node('NPC camp work','Same operations/costs; distant work aggregated',580,145)
para('M1 supplies every branch; its trunk is drawn once. M3 can start alongside M2. M4 does not always require M5: ignition method determines that. M6 → M7 is one hunting route, not a gate to all animal resources. Some supporting inputs are omitted here for readability.',44,74,704,size=8.7,leading=10.6,color=MUTED)

# 3–7. Two research cards per page.
def milestone_card(item,top):
    key,title,build,needs,ready,research,search,evidence,ids=item
    x=44;w=524;h=294;bottom=top-h;box(x,bottom,w,h,colors.white)
    C.setFillColor(PALE);C.roundRect(x,bottom+h-37,w,37,8,fill=1,stroke=0)
    label(key,x+12,top-23,size=12);para(title,x+52,top-11,w-66,size=17,leading=20,font='Bold')
    y=top-49
    for name,text in [('BUILD',build),('NEEDS',needs),('READY WHEN',ready),('RESEARCH NEXT',research),('SEARCH TERMS',search)]:
        if name=='SEARCH TERMS':text=f'<link href="https://www.youtube.com/results?search_query={quote(text)}" color="#25644E">{text}</link>'
        y=para(f'<font color="#25644E"><b>{name}</b></font>  {text}',x+12,y,w-24,size=10,leading=12.5)-7
    y=para('<b>Evidence / inference:</b> '+evidence+' '+refs(ids),x+12,y,w-24,size=9.3,leading=11.6,color=MUTED)
    if y<bottom+10:raise RuntimeError(f'{key} card overflow: {y} < {bottom+10}')
for n,title in enumerate(['Materials before recipes','Get through the first night','Recover carrying and working tools','Turn catches into useful resources','Improve equipment and camp']):
    page(title,'Research briefs • Each feature package has a playable completion check and questions to resolve before implementation.')
    milestone_card(MILESTONES[n*2],674);milestone_card(MILESTONES[n*2+1],365)

# 8. The user's multi-component spear example.
y=page('One spear, several workstreams','Hands limit handling, not the number of components a persistent project may contain.')
for x,title,text in [(44,'HEAD  /  M2','Suitable stone → point blank → shaped point. Keep base geometry, edge condition and identity.'),(224,'SHAFT  /  M6','Suitable wood → prepared shaft → matching seat. Size, straightness and fit matter.'),(404,'FIXATION  /  M5 + M9','Choose one complete method: binding, adhesive or a supported combination. Research preparation and setting.')]:
    box(x,521,164,111);para(title,x+10,620,144,size=11,font='Bold');para(text,x+10,597,144,size=10,leading=13)
arrow([(126,521),(126,488),(253,488),(253,464)])
arrow([(306,521),(306,464)])
box(172,390,268,74);para('<b>Dry-fitted / partial workpiece</b><br/>Point and shaft retain identity. If they cannot stay together, leave them arranged at the worksite until temporarily secured.',184,452,244,size=10.5)
arrow([(486,521),(486,337),(440,337)])
arrow([(306,390),(306,373)])
box(172,298,268,75,PALE);para('<b>Apply the selected fixation process</b><br/>Work, bind and/or set adhesive as the method requires. Track progress and attachment condition; allow interruption and resumption.',184,361,244,size=10.5)
arrow([(306,298),(306,277)])
box(172,222,268,55,SAND);para('<b>Usable spear → wear → repair</b><br/>Replace a point or binding while retaining sound components.',184,266,244,size=10.5)
y=para('<b>Research before committing to a recipe</b>',44,199,524,size=14,font='Bold')
y=para('Compare point-base and shaft-seat designs separately from fixation materials. Record tools, wrapping technique, adhesive preparation, setting conditions, moisture response, failure and repair. Commercial glue, synthetic sinew or metal tools can hide major prerequisites in a demonstration. '+refs(['S8','S9','S11']),44,y-10,524,size=10.5)
note('<b>Implementation rule:</b> keep components or their provenance, expose missing capabilities, and let partial assemblies behave according to their state. A generic pile of ingredients should not become an unrelated finished object. This is a design choice, not an archaeological claim.',44,y-14,524)

# 9. Reusable implementation/research worksheet.
y=page('Research each craft, then choose the abstraction','Click a research card’s search terms to open YouTube. Use AI for questions and evidence; demonstrations show the work.')
y=para('<b>Shared mechanics alongside M1–M2</b>',44,y-16,524,size=15,font='Bold')
for text in [
'<b>Materials:</b> dimensions, mass, shape and local suitability. Branch forks, moisture and toolstone properties affect use.',
'<b>Actions:</b> known operation + capable tool + suitable workpiece + required support/free hands. Explain missing requirements.',
'<b>Jobs:</b> save preparation, attached parts, progress and condition. Work takes visible effort; inventory transfers stay immediate.',
'<b>Construction:</b> place/join real pieces; judge support, coverage, anchoring and bedding. Archetypes are examples, not compulsory menu blueprints.',
'<b>Knowledge:</b> methods come from the character/community; skill affects execution. Culture, materials and conditions determine viable routes.',
'<b>NPCs:</b> use the same input/output and labor rules; aggregate distant work instead of animating every historical action.'
]:y=para(text,44,y-9,524,size=10.5)
y=para('<b>One research sheet per technique</b>',44,y-20,524,size=15,font='Bold')
for text in [
'1. Which place, period and tradition? Separate direct evidence, ethnographic comparison, experimental replication and speculation.',
'2. List every raw material, tool, intermediate, support and heat source. Include preparation before the camera started.',
'3. Record operations, interruptions, waste, failure and recoverable parts. Separate active work from drying/setting time.',
'4. Audit modern blades, glue, synthetic cord, pre-tanned hide and pre-cut blanks. How would these be obtained in our game?',
'5. Choose the interaction and completion check. What needs direct input, what repeats while held, and what can advance as a persistent process?'
]:y=para(text,44,y-8,524,size=10.2)
y=note('<b>Reusable AI prompt</b><br/>“Research [technique] for a knowledgeable person in [biome/tradition], around 20–12 ka, without equipment. Separate direct evidence from reconstruction. Give alternative methods, all tools/materials/intermediates, dependencies, failure and repair. Cite primary sources and flag modern shortcuts. Propose the smallest believable game implementation.”',44,y-17,524)
para('Choose a method before implementation. Save your selected reference/demo and rejected alternatives. Add a regional branch when it changes meaningful materials, effort or performance. No fixed hour estimates or guaranteed solo-survival outcome are asserted.',44,y-10,524,size=9.4,color=MUTED)

# 10. Annotated, clickable sources.
y=page('Evidence and further reading','Checked 27 October 2026 • Finds establish particular practices, not universal knowledge or exact invention dates.')
C.bookmarkPage('sources')
for key,title,url,annotation in SOURCES:
    y=para(f'<font color="#25644E"><b>[{key}]</b></font> <link href="{url.replace("&","&amp;")}" color="#25644E"><b>{title}</b></link>',44,y-10,524,size=10.2,leading=12.5)
    y=para(annotation,44,y-3,524,size=9.5,leading=11.6,color=MUTED)
    C.linkURL(url,(44,y,W-44,y+28),relative=0,thickness=0)
para('Modern survival guidance informs priorities; it does not prove reliable naked solo survival. Organic technologies preserve poorly. Regional pottery, watercraft, advanced projectiles and complex shelters need additional briefs; farming, metalworking and log-cabin progression are outside this first roadmap.',44,y-14,524,size=9.4,color=MUTED)
assert PAGE==TOTAL,(PAGE,TOTAL)
C.save()
print(OUT)
print(f'{PAGE} pages; {len(LAYOUT)} text blocks')
