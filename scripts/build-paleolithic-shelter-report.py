"""Illustrated archaeological/design catalogue; does not modify the game."""
from pathlib import Path
import json, sys, math
from html import escape
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'artifacts/report-tools/python'))
from reportlab.pdfgen import canvas
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4,landscape
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import Paragraph,Table,TableStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.utils import ImageReader

OUT=ROOT/'docs/reports/WorldGen - Paleolithic Shelters.pdf'
EVIDENCE=ROOT/'artifacts/shelter-research-20261028';EVIDENCE.mkdir(parents=True,exist_ok=True)
for name,file in [('Body','calibri.ttf'),('Bold','calibrib.ttf'),('Italic','calibrii.ttf')]:
    pdfmetrics.registerFont(TTFont(name,str(Path('C:/Windows/Fonts')/file)))
pdfmetrics.registerFontFamily('Body',normal='Body',bold='Bold',italic='Italic',boldItalic='Bold')
W,H=landscape(A4);M=34;CW=W-2*M;PAGE=0;TOTAL=12;LAYOUT=[]
INK=colors.HexColor('#19392F');MUTED=colors.HexColor('#536B63');GREEN=colors.HexColor('#28634E')
PALE=colors.HexColor('#EDF3ED');SAND=colors.HexColor('#F5F0E5');LINE=colors.HexColor('#C9D6CC');GOLD=colors.HexColor('#986526')
C=canvas.Canvas(str(OUT),pagesize=(W,H),pageCompression=1)
C.setTitle('WorldGen | Paleolithic Shelters: Evidence, Examples and Dynamic Building')
C.setAuthor('WorldGen design research');C.setSubject('Late Paleolithic / Epipaleolithic shelter catalogue and implementation requirements')
IMAGES={m['id']:m for m in json.loads((ROOT/'docs/reports/shelter-images/credits.json').read_text(encoding='utf-8'))}
USED=set()
SOURCES=[
 ('MNHN — Abri Pataud collection','https://www.mnhn.fr/en/abri-pataud-collection','Upper Paleolithic rock-shelter occupation; modern museum features in the photograph are not prehistoric architecture.'),
 ('French Ministry of Culture — Étiolles dwellings','https://archeologie.culture.gouv.fr/etiolles/fr/les-habitations-1','Circular dwelling layouts, stone arrangements and the explicit hypothesis of a Siberian yaranga-like shelter.'),
 ('French Ministry of Culture — Life inside the tents','https://archeologie.culture.gouv.fr/etiolles/fr/la-vie-linterieur-des-tentes','The Étiolles camp around 15,000 years ago; substantial differences between contemporary dwelling units.'),
 ('Steiner et al. 2022 — Ohalo II','https://doi.org/10.1371/journal.pone.0262434','Six brush huts, calibrated age and Hut 1 floor dimensions/depth. Open-access plans, not preserved standing roofs.'),
 ('Nadel et al. 2004 — Ohalo bedding','https://pmc.ncbi.nlm.nih.gov/articles/PMC404215/','Plant bedding and deliberately arranged domestic floor space; bedding is separate from a roof.'),
 ('Maher et al. 2012 — Kharaneh IV huts','https://doi.org/10.1371/journal.pone.0031447','Early Epipaleolithic hut footprints, shallow depression and burnt construction remains. Open-access figures.'),
 ('Ramsey et al. 2018 — Sheltered by reeds, settled on sedges','https://doi.org/10.1016/j.jaa.2018.03.003','Phytolith study of Kharaneh IV construction and floor materials. Distinguishes inferred building plants from a preserved roof.'),
 ('Hearst Museum — Experimental thatched construction','https://hearstmuseum.berkeley.edu/exhibit/experimental-archaeology-thatched-hut-construction/','Research-led hut reconstruction; expressly uses later ethnographic analogues without making them ancient photographs.'),
 ('Chu, Shydlovskyi & Maier 2025 — Mezhyrich chronology','https://doi.org/10.12688/openreseurope.20112.1','Version 1 dates MBS 4/context to approximately 18,248–17,764 cal BP; structural function remains debated.'),
 ('Pryor et al. 2020 — Kostenki 11','https://doi.org/10.15184/aqy.2020.7','Mammoth-bone circle chronology and function: a bone ring is not automatically a roofed house.'),
 ('Edwards 2015 — Wadi Hammeh 27','https://www.antiquity.ac.uk/projgall/edwards347','Early Natufian substantial huts on stone foundations, c. 12,500–12,000 cal BC; pre-Neolithic architecture.'),
 ('Chile nomination via UNESCO — Monte Verde','https://whc.unesco.org/en/tentativelists/1873/','Traditional interpretation: timber bases, stakes, uprights and organic coverings. A state-party nomination, not UNESCO certification of every interpretation.'),
 ('Surovell et al. 2026 — Monte Verde chronological challenge','https://doi.org/10.1126/science.adw9217','Recent critique proposes a much younger depositional age. Treat Monte Verde as a debated example, not a secure date for this roof form.'),
 ('Dillehay et al. 2026 — Monte Verde response','https://doi.org/10.1080/20555563.2026.2699017','Defence of the roughly 14,500-year chronology; paired with the critique rather than silently choosing one side.'),
 ('Canadian Museum of History — Snow knife','https://www.historymuseum.ca/collections/artifact/539514','Later Dorset/Inuit comparison; does not establish Paleolithic snow-block igloos.'),
 ('US Forest Service — Wetland wood construction','https://www.fs.usda.gov/t-d/pubs/pdfpubs/pdf07232804/pdf07232804dpi300.pdf','Modern material guidance for peeling, drying and decay. Used for physical design reasoning, not evidence for an ancient shelter style.'),
]
def ref(n):
    title,url,_=SOURCES[n-1];return f'<link href="{escape(url,quote=True)}" color="#28634E">[{n}]</link>'
def para(text,x,y,width,size=10.5,leading=None,color=INK,font='Body'):
    p=Paragraph(text,ParagraphStyle('p',fontName=font,fontSize=size,leading=leading or size*1.25,textColor=color,spaceAfter=0))
    _,h=p.wrap(width,2000);p.drawOn(C,x,y-h);LAYOUT.append((PAGE,x,y-h,width,h))
    if y-h<32:raise RuntimeError(f'Page {PAGE} overflow: {text[:70]}')
    return y-h
def box(x,y,w,h,fill=PALE):
    C.setFillColor(fill);C.setStrokeColor(LINE);C.setLineWidth(.6);C.roundRect(x,y,w,h,7,fill=1,stroke=1)
def label(text,x,y,color=GREEN,size=9):
    C.setFillColor(color);C.setFont('Bold',size);C.drawString(x,y,text)
def page(title,subtitle):
    global PAGE
    if PAGE:C.showPage()
    PAGE+=1;C.setFillColor(colors.white);C.rect(0,0,W,H,fill=1,stroke=0)
    C.setFillColor(GREEN);C.rect(0,H-9,W,9,fill=1,stroke=0)
    label('WORLDGEN  /  SHELTER RESEARCH',M,H-30,size=8.5)
    para(title,M,H-43,CW,23,26,font='Bold')
    para(subtitle,M,H-77,CW,10,color=MUTED)
    C.setStrokeColor(LINE);C.line(M,30,W-M,30)
    C.setFont('Body',8);C.setFillColor(MUTED);C.drawString(M,17,'Research and design proposal • 28 October 2026 • no game changes')
    C.drawRightString(W-M,17,f'{PAGE} / {TOTAL}')
    C.bookmarkPage(f'p{PAGE}');C.addOutlineEntry(title,f'p{PAGE}',0)
def image(key,x,y,w,h,caption,size=8.5):
    m=IMAGES[key];USED.add(key);path=ROOT/m['file'];C.setFillColor(SAND);C.roundRect(x,y,w,h,5,fill=1,stroke=0)
    C.drawImage(str(path),x,y,w,h,preserveAspectRatio=True,anchor='c',mask='auto')
    LAYOUT.append((PAGE,x,y,w,h))
    credit=f'{caption} <font color="#536B63">Photo/figure: {escape(m["artist"])}; {escape(m["license"])}.</font>'
    return para(credit,x,y-7,w,size)
def table(headers,rows,widths,y,size=9.2):
    def cell(t,bold=False):return Paragraph(t,ParagraphStyle('cell',fontName='Bold' if bold else 'Body',fontSize=size,leading=size*1.22,textColor=colors.white if bold else INK))
    data=[[cell(h,True) for h in headers]]+[[cell(s) for s in row] for row in rows]
    t=Table(data,colWidths=widths,hAlign='LEFT')
    t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),GREEN),('VALIGN',(0,0),(-1,-1),'TOP'),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,PALE]),('LINEBELOW',(0,0),(-1,0),.6,GREEN),('LINEBELOW',(0,1),(-1,-1),.4,LINE),('LEFTPADDING',(0,0),(-1,-1),7),('RIGHTPADDING',(0,0),(-1,-1),7),('TOPPADDING',(0,0),(-1,-1),7),('BOTTOMPADDING',(0,0),(-1,-1),7)]))
    _,h=t.wrap(sum(widths),1000);t.drawOn(C,M,y-h);LAYOUT.append((PAGE,M,y-h,sum(widths),h))
    if y-h<36:raise RuntimeError(f'Table overflow page {PAGE}: {h}')
    return y-h
def note(text,y=65):
    return para(text,M,y,CW,9,color=MUTED)

CATALOGUE=[
 ['01 • Natural cave / rock overhang<br/><b>Documented</b>',f'Abri Pataud, France; Upper Paleolithic occupations. {ref(1)}','Use an existing dry recess/entrance. Bedding and a screen can improve it; no artificial roof is necessary.','Occupy natural geometry; add ground bedding or a wind-facing screen.','Actual overhang/cave geometry, collision and directional weather shelter.','Rock geometry route'],
 ['02 • Boulders / tree-backed windbreak<br/><b>Plausible form</b>','Physical/ethnographic reconstruction; no securely dated exact arrangement claimed.','Branches, brush or hide against an existing obstacle; stones can anchor a lower edge.','Place a partial wall; its benefit depends on wind direction and gaps.','World-object attachments, ballast and wind screening.','First pass'],
 ['03 • Single lean-to<br/><b>Plausible form</b>','Your forked-post design; not presented as a preserved Paleolithic blueprint.','Two forked posts, ridgepole, sloping rafters, crosspieces and layered covering.','Plant/drive posts, rest beam in forks, lash selected joints, lean and cover.','Ground embedment, bearing contacts, lashings and roof coverage.','First target'],
 ['04 • A-frame / double lean-to<br/><b>Plausible form</b>','A feasible frame configuration; exact early roof geometry generally does not survive.','Ridge supports with rafters on both sides; brush, hide or thatch over it.','Extend the same ridge construction to a second roof slope; close an end if desired.','Same parts as lean-to; support both slopes and leave a doorway.','First pass'],
 ['05 • Low brush hut / dome<br/><b>Hut evidence; roof inferred</b>',f'Ohalo II, Israel; about 23,000 cal BP, slightly earlier than our core window. {ref(4)}','Brushwood enclosing a small oval/circular floor; specific above-ground frame reconstructed.','Flexible arches or short poles with brush laid into/over the frame.','Flexible green wood, retained curvature and interwoven infill.','After rigid frames'],
 ['06 • Reed / grass-covered hut<br/><b>Plant evidence; form inferred</b>',f'Kharaneh IV, Jordan; about 20,000–19,000 cal BP. {ref(6)} {ref(7)}','Local construction plants, bundles/thatch and small structural members.','Add overlapping reed bundles or mat-like sections to a supporting frame.','Gather construction-sized bundles; bind/weave; account for overlap and thickness.','After coverings'],
 ['07 • Pole-and-hide tent<br/><b>Layouts documented; roof inferred</b>',f'Étiolles, France; around 15,000 years ago. Siberian-style forms are hypotheses. {ref(2)} {ref(3)}','Wooden supports, sufficiently large prepared hides, ties and optional stone weights.','Tripod/conical, dome or ridge arrangements; stretch/sew skins between supports.','Flexible sheet surfaces, seams, anchors and reusable tent components.','After sheet support'],
 ['08 • Stone-weighted tent / hybrid<br/><b>Stone layouts documented</b>',f'Étiolles has some stone-rich and some stone-poor units. {ref(2)}','Organic frame/cover with stones securing edges or forming low perimeter arrangements.','Weight a skin edge or brace a base; do not require a stone ring for every tent.','Meaningful movable rocks, edge anchoring and compatible mixed materials.','Early extension'],
 ['09 • Large timber / hide shelter<br/><b>Debated example</b>',f'Monte Verde, Chile: traditionally about 14,500 cal BP; renewed chronology dispute in 2026. {ref(12)} {ref(13)} {ref(14)}','Traditional interpretation uses timber bases, stakes, uprights, ties and organic covers.','Join repeated small bays into a longer shelter with internal divisions.','Long timber supply/carrying, repeated frame connections and partitions.','Later scale-up'],
 ['10 • Mammoth-bone hybrid dwelling<br/><b>Bone structures documented</b>',f'Mezhyrich, Ukraine; MBS 4/context around 18,250–17,760 cal BP in 2025 study. {ref(9)}','Large skulls/jaws/long bones/tusks; organic roof or additional wooden members reconstructed.','Set heavy bones as bases/supports; combine shaped members with a covering.','Distinct large bone anatomy, scavenged bone supply, mass/transport and bearing rules.','Later material set'],
 ['11 • Shallow sunken-floor hut<br/><b>Documented floor feature</b>',f'Ohalo II and Kharaneh IV include shallow depressed floors. {ref(4)} {ref(6)}','A shallow excavated floor beneath an organic shelter; not necessarily a deep cellar.','Excavate a basin, retain safe edges, then erect an ordinary roof/frame.','Persistent terrain/collision edits, drainage and entry access.','After terrain edits'],
 ['12 • Stone-founded round hut<br/><b>Documented; late regional case</b>',f'Natufian Wadi Hammeh 27, Jordan; c. 14,450–13,950 cal BP. {ref(11)}','Substantial stone foundations/low walls with an organic upper structure inferred.','Arrange stable stone courses, support posts/roof and retain an opening.','Stone placement/support, heavier work and organic-to-stone connections.','Late-window extension'],
 ['13 • Bark-clad shelter<br/><b>Plausible material variant</b>','Later documented analogues support feasibility; no exact Late Paleolithic bark-house tradition asserted here.','Suitable bark sheets held against an existing frame; seams/overlap matter.','Clamp or tie sheets onto poles; replace individual damaged sections.','Harvest intact bark sheets, sheet stiffness, fastening and lap joints.','Covering extension'],
 ['14 • Snow drift / snow-pit refuge<br/><b>Plausible; unverified here</b>',f'No direct Paleolithic case established by this research. Later snow knives are not proof of Ice Age igloos. {ref(15)}','Excavate or shape appropriate compact snow; maintain an opening and breathable air.','Dig a refuge or combine a snow bank with an organic roof.','Actual snow volume, excavation, stability, ventilation and melting.','Optional later system'],
]
(EVIDENCE/'catalogue.json').write_text(json.dumps(CATALOGUE,ensure_ascii=False,indent=2),encoding='utf-8')

page('Paleolithic shelters: what can we actually build?', 'An illustrated catalogue of 14 shelter families and material variants, with evidence and game-system requirements.')
image('tent',M,244,365,245,'<b>Modern reconstruction:</b> a Paleolithic tent interpretation at Asnapio. It illustrates a possible form, not a surviving ancient tent.')
image('mammoth',W-M-365,244,365,245,'<b>Museum reconstruction:</b> a Mezhyrich-style bone dwelling. The completed upper silhouette is interpretive.')
box(M,51,CW,124)
para('<b>Scope:</b> mainly the late Upper Paleolithic / Epipaleolithic, roughly 20,000–12,000 years ago. Ohalo II provides slightly older context. These are worldwide possibilities where resources and knowledge permit—not a universal list used by every group.',M+13,164,CW-26,11)
para('<b>What the evidence supports:</b> temporary organic shelters, repeated-use huts, natural rock shelters, bone-built structures and, late in some regions, substantial stone-founded dwellings. Exact roofs are much less certain than floors and foundations.',M+13,117,CW-26,11)
para('<b>Design recommendation:</b> build a shared kit of posts, joints, sheets, infill and anchors. Let several shelter forms emerge from the same actions. Start with the forked-post lean-to.',M+13,76,CW-26,10.5)

page('Catalogue A • from natural shelter to portable tents','“Documented” describes surviving archaeological features; it does not automatically authenticate the illustrated roof shape.')
widths=[104,137,148,148,151,CW-688]
y=table(['Shelter / evidence','Real example / context','Materials and real construction','How we assemble it in game','What the system needs','Order'],CATALOGUE[:7],widths,H-108,9)
note('Entries overlap: a dome can be covered in brush, reeds, bark or hides. A lean-to is a frame arrangement, not a special wood material.',min(y-13,61))

page('Catalogue B • stone, bone, earth and other coverings','Dates are approximate calendar years before present (cal BP, where present = AD 1950), unless otherwise stated.')
y=table(['Shelter / evidence','Real example / context','Materials and real construction','How we assemble it in game','What the system needs','Order'],CATALOGUE[7:],widths,H-108,9)
note('These are major useful combinations, not an exhaustive list of every excavated site. No archaeological record can supply all ephemeral shelter forms.',min(y-13,61))

def lean_diagram(x,y,w,h,stage=4):
    # Original game-construction schematic; deliberately not an archaeological reconstruction.
    def p(a,b):return x+a*w,y+b*h
    C.setStrokeColor(colors.HexColor('#9D7651'));C.setLineCap(1)
    def ln(a,b,width=4,color=None):
        C.setLineWidth(width);C.setStrokeColor(color or colors.HexColor('#9D7651'));C.line(*p(*a),*p(*b))
    ln((.07,.14),(.91,.14),1,LINE)
    # Two front forked uprights.
    for a,b in [(.24,.15),(.77,.26)]:
        ln((a,b-.06),(a,b+.46));ln((a,b+.46),(a-.035,b+.52),3);ln((a,b+.46),(a+.035,b+.52),3)
        if stage>=2:ln((a-.023,b+.48),(a+.023,b+.49),2,GOLD)
    if stage>=2:ln((.23,.64),(.79,.76),5)
    if stage>=3:
        for i in range(6):
            t=i/5;ln((.23+.56*t,.64+.12*t),(.12+.55*t,.17+.04*t),3.1)
    if stage>=4:
        for j in range(1,5):
            t=j/5;ln((.23*(1-t)+.12*t,.64*(1-t)+.17*t),(.79*(1-t)+.67*t,.76*(1-t)+.21*t),2)
        # Thin overlapping roofing courses leave rafters visible.
        C.setFillColor(colors.HexColor('#7D9567'));C.setStrokeColor(colors.HexColor('#607B52'))
        for j in range(4):
            a=j/4;b=(j+1.25)/4
            def roof(u,v):return p(.23+.56*u-.11*v,.64+.12*u-.47*v)
            poly=C.beginPath();poly.moveTo(*roof(0,min(b,1)));poly.lineTo(*roof(1,min(b,1)));poly.lineTo(*roof(1,a));poly.lineTo(*roof(0,a));poly.close();C.setLineWidth(.7);C.drawPath(poly,fill=1,stroke=1)
    label(str(stage),x+5,y+h-12,size=10)

page('The first build • a fork-supported lean-to','A proposed gameplay assembly. Its engineering is plausible; this is not a claim that one exact prehistoric template is known.')
steps=[('Plant and drive','Keep useful forks. Depth, ground and member size affect support.'),('Seat the ridgepole','A fork carries weight; a tie can restrain rolling or uplift.'),('Lean the rafters','Place parallel sloping members. Add bracing where the frame needs it.'),('Cover and secure','Crosspieces retain overlapping brush, reeds, bark or hides.')]
gap=14;sw=(CW-3*gap)/4
for i,(title,text) in enumerate(steps):
    x=M+i*(sw+gap);box(x,267,sw,222);lean_diagram(x+4,339,sw-8,139,i+1);para('<b>'+title+'</b>',x+9,331,sw-18,12);para(text,x+9,308,sw-18,10)
table(['Player choice','Useful consequence','Avoid this artificial rule'],[
 ['Trim selectively','Remove snagging stubs where fitting/covering requires it; retain a useful Y.','Every structural member must first become a generic straight pole.'],
 ['Drive or brace','Embedment, load direction and ground affect support; planting trades exposed height for anchorage.','Deep enough means unlimited strength regardless of member size.'],
 ['Lash where helpful','Ties restrain movement; bearing contacts still carry weight.','Touching any two sticks automatically welds them together.'],
 ['Use bark deliberately',f'Peeling can help drying and longer-term durability; a brief camp may not justify the work. {ref(16)}','Debarking instantly increases the wood’s bending strength.'],
], [150,319,CW-469],239,10)

page('Brush, reeds and domes • evidence versus analogy','These give us distinct cover and frame mechanics without requiring a separate recipe for every hut shape.')
image('wickiup',M,257,345,228,'<b>Later analogy, not Paleolithic:</b> Apache wickiup photographed in 1903. Useful for seeing organic cover and a low hut silhouette; not evidence of a direct cultural lineage.',8.5)
image('kharaneh',M+365,257,CW-365,228,'<b>Actual excavations:</b> Kharaneh IV hut remains, Maher et al. 2012, Fig. 5. Surviving traces do not preserve a standing roof.',8.5)
table(['Evidence / construction point','What follows for our game'],[
 [f'Kharaneh IV preserves small hut remains; subsequent phytolith research addresses construction plants. {ref(6)} {ref(7)}','Reed bundles and grass cover should be physical construction materials with area, thickness and fastening—not a single decorative roof prop.'],
 [f'Research-led reconstruction explicitly compares later organic houses. {ref(8)}','We can offer plausible forms while labelling historical certainty correctly. Modern images are references, not proof.'],
 ['Arched frames require bendable members; dry crooked branches are different material stock.','Add green/flexible wood and supported curvature. A stable shaped member can remain static; it does not need continuous soft-body simulation.'],
], [300,CW-300],190,10)

page('Floors tell us more than roofs','Preserve that distinction when choosing what the game treats as historical knowledge.')
image('ohalo',M,180,372,307,'<b>Ohalo II plan:</b> location, camp layout and Hut 1. Published by Steiner et al. 2022, Fig. 1. This is a plan, not an above-ground reconstruction.',8.5)
image('kharaneh-plan',M+395,180,CW-395,307,'<b>Kharaneh IV plan:</b> Structures 1 and 2, Maher et al. 2012, Fig. 3. The figure itself distinguishes inferred boundaries from surviving ones.',8.5)
box(M,49,CW,87)
para(f'<b>Two useful anchors:</b> Ohalo II has six brush-hut remains at roughly 23,000 cal BP; Hut 1 is about 4.5 × 3 m and has a shallow depressed floor. Kharaneh IV adds small hut floors around 20,000 years ago. {ref(4)} {ref(6)}',M+12,125,CW-24,10.5)
para(f'<b>Separate systems:</b> ground bedding, floor preparation, roof cover and wind screens should contribute independently. Plant bedding is archaeologically attested; a roof alone should not magically insulate a sleeper from cold or wet ground. {ref(5)}',M+12,81,CW-24,10.5)

page('Hide tents and bone-built hybrids','Organic coverings can be portable; a heavy foundation usually changes the camp’s mobility and labour requirements.')
image('tent',M,239,350,245,'<b>Reconstruction:</b> Asnapio tent. The photo makes poles/skin enclosure tangible, but is not proof of a unique ancient conical design.',8.5)
image('mammoth',M+373,239,CW-373,245,'<b>Reconstruction:</b> Mezhyrich-style mammoth-bone dwelling. Original bone arrangements support interpretation; complete roof design remains uncertain.',8.5)
table(['Real-life lesson','New game requirement'],[
 [f'Étiolles has circular layouts and variable stone arrangements; an iaranga-like reconstruction is explicitly a hypothesis. {ref(2)}','Support multiple frame layouts. Sew/stretch skins over supported boundaries; anchor edges with ties, pegs or stones.'],
 [f'Mezhyrich MBS 4/context dates to about 18.25–17.76 thousand cal BP in the 2025 study. Some construction bones came from older accumulations. {ref(9)}','Large bones should be a distinct salvage supply. One deer “long bone” cannot stand in for a mammoth skull, tusk or heavy supporting bone.'],
 [f'Not every big mammoth-bone circle is securely a house; function is debated, including at Kostenki 11. {ref(10)}','Permit bone/wood/stone hybrids; do not encode an unsupported rule that every circular bone arrangement generates a complete roof.'],
], [348,CW-348],185,10)

page('Rock, stone and shallow earth • useful, but different problems','Natural shelter, constructed walls and excavation should not all be implemented as cosmetic roof pieces.')
image('pataud-excavation',M,280,382,205,'<b>Actual rock-shelter excavation:</b> Abri Pataud, with modern visitor walkways. Those metal/wood installations are not Paleolithic construction.',8.5)
x=M+407
para(f'<b>Natural shelters</b><br/>Rock overhangs and cave entrances provide existing roof geometry. Abri Pataud is a documented Upper Paleolithic occupation site. {ref(1)}',x,481,CW-407,11)
para(f'<b>Stone-founded huts are not automatically Neolithic.</b><br/>Wadi Hammeh 27 has substantial pre-Neolithic huts on stone foundations around 14.45–13.95 thousand cal BP. The upper roof is less certain than the surviving stonework. {ref(11)}',x,384,CW-407,11)
table(['Form','What must become real in the world','Suggested scope'],[
 ['Natural cave / overhang','Actual non-heightfield geometry and collision, light/air access, rockfall-independent shelter detection.','Major terrain/rock feature; add independently of stick construction.'],
 ['Stone-founded hut / low wall','Stable stones/courses and contact supports. A roof can still use the same organic frame and cover systems.','Build after simple ballast/anchors; no blanket requirement for mortar.'],
 ['Shallow sunken floor','Editable terrain/collider and persistent ground levels; water drainage and an accessible entrance.','Later than posts: a post can store embedment without first implementing terrain excavation.'],
], [158,390,CW-548],212,10)

page('The reusable building kit','Implement relationships between pieces. Many shelter appearances then become combinations of the same mechanics.')
table(['Capability','Why we need it','What can stay simple / what is new'],[
 ['Wood geometry and provenance','Length/thickness already vary; preserve forks, stubs, curvature, species and condition.','Existing dimensions are a start. Add structural shape/contact points and relevant wood material metadata.'],
 ['Placement and embedment','Place a piece anywhere sensible; drive its end into appropriate ground.','Store depth/orientation and ground suitability. No visible terrain hole is necessary for the first post system.'],
 ['Bearing supports','Forks, notches, ledges and ground carry load through actual contacts.','A cheap support graph can represent support direction and capacity. Avoid floating contacts created by proximity alone.'],
 ['Ties, pegs, braces and ballast','Restrain sliding/uplift and stabilise joints; hold coverings down.','Use existing cordage/rawhide/sinew types where suitable. Tension connections and support are different relationships.'],
 ['Layered brush / reed cover','Sparse brush gives shade; dense, overlapped cover sheds rain better and adds insulation.','Construction bundles need area/thickness/moisture. Combine bundles into static render sections rather than endless loose pieces.'],
 ['Hide / bark sheets','Span between supports, overlap, fasten, sew and replace panels.','Generate a supported sheet mesh. Skin size, seams and bark stiffness are needed beyond a folded inventory icon.'],
 ['Flexible arches','Create domes and bent frames without every curve being a new fixed model.','Green wood and bend limits; store a curve and build its mesh. Full-time soft-body physics is unnecessary.'],
 ['Large bone components','A skull, tusk and long bone play different structural roles.','New anatomy-specific resources/models, sensible handling and salvage. Retain identity/shape rather than generic “bone points”.'],
 ['Local shelter effects','A structure should block particular rain/wind directions and cover a real sleeping area.','Evaluate roof/side coverage near actors; floor bedding, wetness and warmth stay separate. Fire/smoke come with the fire system.'],
 ['Persistence and performance','Moving/breaking a support must affect attached pieces, including after saving and streaming.','Save pieces/joints in planetary coordinates. Re-evaluate changed structures; keep stable parts static and simulate failures locally.'],
], [151,275,CW-426],H-109,10)

page('Recommended implementation order','This is software order—not a claim that every society invented shelters in this sequence.')
table(['Pass','Player-visible result','What counts as a useful first version'],[
 ['1 • Rigid pieces and supports','Plant/drive straight or forked wood; rest a beam; lean rafters; tie a joint.','One coherent lean-to frame. Preserve chosen forks and length. It must save/reload without floating or shifting.'],
 ['2 • Organic cover and bedding','Add brush/reed bundles and hides; overlap and anchor them; add a sleeping layer.','The same frame can offer different rain, wind and ground protection. Missing cover remains a real opening.'],
 ['3 • Reconfiguration and portability','Add an opposite roof slope/end wall, make a small tent, dismantle and recover materials.','Lean-to, A-frame and small hide shelter emerge without complete-building recipes.'],
 ['4 • New material behaviours','Bend green poles; add sheet bark and low stone arrangements.','Domes, reed huts and hybrid shelters reuse the existing connections. Larger stonework can follow separately.'],
 ['5 • Heavy and terrain-dependent shelter','Add large bone salvage, bigger timbers, shallow excavated floors and real overhangs/caves.','Bone hybrids and lasting camps become possible without forcing costly terrain/cave work into the first build.'],
], [155,281,CW-436],H-109,10.5)
box(M,50,CW,96,SAND)
para('<b>Useful acceptance checks:</b> remove a critical support and the dependent roof loses support; an intact fork carries a seated beam; a lashing does not make thin wood infinitely strong; gaps admit weather; dismantling returns recoverable physical materials; saving preserves every connection.',M+13,135,CW-26,11)
para('<b>Keep this tactile:</b> aim/place, rotate, seat, drive, tie and layer materials. Offer sensible local snapping/contact previews, not a compulsory finished-hut blueprint or arbitrary “must craft pole” gate.',M+13,83,CW-26,10.5)

page('Evidence boundaries and research references','Clickable sources support the archaeological claims. Construction-system proposals are our design inferences.')
table(['Do not silently treat this as established','How the report handles it'],[
 ['Every modern bushcraft shape has a known Paleolithic date','Lean-to, A-frame, bark variants and snow refuges are marked plausible where direct confirmation was not established.'],
 ['All ice-age cold shelters were snow-block igloos','Later Dorset/Inuit evidence is not evidence for a Paleolithic igloo tradition. Do not make igloos a default historically certified unlock.'],
 ['The mammoth skeleton reconstruction proves its complete roof','The arrangement is real evidence; covering and roof engineering are partly reconstructed. Different bone circles may have different functions.'],
 ['Monte Verde provides an uncontested dated timber-house template','Traditional architecture interpretation is included, but the new 2026 age dispute and response are linked. No confident date gate relies on it.'],
 ['A full log cabin, mud-brick village or stone tower belongs in this first pass','These are not default templates here. Technical feasibility and evidence for a particular building tradition are separate questions.'],
], [310,CW-310],H-110,9.8)
label('CORE ARCHAEOLOGY / CONTEXT',M,230)
left=M;right=M+CW/2+10;width=CW/2-17
y1=y2=213
for i in range(8):
    n=i+1;title,url,desc=SOURCES[i]
    y1=para(f'<b>{n}.</b> <link href="{escape(url,quote=True)}" color="#28634E">{escape(title)}</link>',left,y1,width,8.6)-8
for i in range(8,16):
    n=i+1;title,url,desc=SOURCES[i]
    y2=para(f'<b>{n}.</b> <link href="{escape(url,quote=True)}" color="#28634E">{escape(title)}</link>',right,y2,width,8.6)-8

page('Picture credits and reading notes','All photographs are genuine site/exhibit/ethnographic photographs. The lean-to diagram is an original game-design schematic.')
credit_rows=[]
for key in ['tent','mammoth','wickiup','pataud-excavation','ohalo','kharaneh','kharaneh-plan']:
    m=IMAGES[key]
    kind={'tent':'Modern reconstruction at Asnapio','mammoth':'Museum bone-dwelling reconstruction','wickiup':'Later Apache wickiup, 1903','pataud-excavation':'Abri Pataud excavation with modern walkways','ohalo':'Ohalo II published site/hut plans','kharaneh':'Kharaneh IV published excavation figure','kharaneh-plan':'Kharaneh IV published hut plans'}[key]
    licence=escape(m['license'])
    if m.get('license_url'):licence=f'<link href="{escape(m["license_url"],quote=True)}" color="#28634E">{licence}</link>'
    credit_rows.append([kind,escape(m['artist']),licence,f'<link href="{escape(m["source"],quote=True)}" color="#28634E">Original file / figure and full attribution</link>'])
table(['Picture','Creator / publication','Reuse statement','Source'],credit_rows,[246,159,135,CW-540],H-110,10)
box(M,53,CW,104,SAND)
para('<b>Picture handling:</b> reference images were proportionally resized and converted to JPEG for this report; no architectural features were invented or added. Public-domain and Creative Commons images retain their individual reuse terms. These research images are not proposed as distributable game textures or models.',M+12,146,CW-24,10.5)
para('<b>Reading dates:</b> BP is measured from AD 1950; cal BP uses calibrated ages. Old radiocarbon-only ages should not be mixed directly with calibrated dates. Regional “Paleolithic,” “Epipaleolithic” and “Neolithic” boundaries differ; late Natufian hunter-gatherer buildings are included intentionally.',M+12,96,CW-24,10.5)

assert PAGE==TOTAL,(PAGE,TOTAL)
C.save();(EVIDENCE/'layout.json').write_text(json.dumps(LAYOUT),encoding='utf-8')
(EVIDENCE/'sources.json').write_text(json.dumps(SOURCES,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Created {OUT}: {PAGE} pages, {len(USED)} reference images')
