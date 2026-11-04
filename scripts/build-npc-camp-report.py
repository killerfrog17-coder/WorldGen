"""Build the illustrated NPC review from curated, preserved game captures."""
from pathlib import Path
import json, sys
from html import escape

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools/reporting/python'))
from reportlab.pdfgen import canvas
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import Paragraph
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.utils import ImageReader

FOLDER=ROOT/'docs/reports/npc-camp-images'
REVIEW=json.loads((FOLDER/'review.json').read_text(encoding='utf-8'))
OUT=ROOT/'docs/reports/WorldGen - NPC Camps and Daily Life.pdf'
for name,file in [('Body','calibri.ttf'),('Bold','calibrib.ttf')]:
    pdfmetrics.registerFont(TTFont(name,str(Path('C:/Windows/Fonts')/file)))
pdfmetrics.registerFontFamily('Body',normal='Body',bold='Bold',italic='Body',boldItalic='Bold')
W,H=A4;M=34;CW=W-2*M;GAP=15;COL=(CW-GAP)/2
INK=colors.HexColor('#213A33');MUTED=colors.HexColor('#52665F')
GREEN=colors.HexColor('#285C49');PALE=colors.HexColor('#EDF3EE')
GOLD=colors.HexColor('#B78C4A');LINE=colors.HexColor('#D5DFD7')
C=canvas.Canvas(str(OUT),pagesize=A4,pageCompression=1)
C.setTitle('WorldGen | NPC Camps and Daily Life')
C.setAuthor('WorldGen');C.setSubject('Illustrated review of the current NPC foundation, including visual test findings')
PAGE=0;LAYOUT=[]

def para(text,x,y,w,size=10.5,bold=False,color=INK):
    p=Paragraph(text,ParagraphStyle('p',fontName='Bold' if bold else 'Body',fontSize=size,leading=size*1.3,textColor=color))
    _,h=p.wrap(w,1500);p.drawOn(C,x,y-h)
    if y-h<36:raise RuntimeError(f'Page {PAGE} overflow: {text[:70]}')
    LAYOUT.append({'page':PAGE,'x':x,'bottom':y-h,'width':w,'height':h})
    return y-h

def page(title,subtitle):
    global PAGE
    if PAGE:C.showPage()
    PAGE+=1
    C.setFillColor(colors.white);C.rect(0,0,W,H,fill=1,stroke=0)
    C.setFillColor(GREEN);C.rect(0,H-8,W,8,fill=1,stroke=0)
    para('WORLDGEN  /  NPC FIELD REVIEW',M,H-29,CW,8.5,bold=True,color=GREEN)
    para(title,M,H-49,CW,25,bold=True)
    para(subtitle,M,H-86,CW,10,color=MUTED)
    C.setStrokeColor(LINE);C.line(M,30,W-M,30)
    C.setFont('Body',8);C.setFillColor(MUTED)
    C.drawString(M,17,'Actual game captures  |  '+REVIEW['date']+'  |  NPC update only')
    C.drawRightString(W-M,17,str(PAGE)+' / 5')
    C.bookmarkPage('page'+str(PAGE));C.addOutlineEntry(title,'page'+str(PAGE),0)
    return H-119

def image(key,x,y,w,caption=None):
    entry=REVIEW['images'][key];path=FOLDER/entry['file'];r=ImageReader(str(path));iw,ih=r.getSize();h=w*ih/iw
    C.drawImage(r,x,y-h,w,h,mask='auto')
    bottom=y-h
    if caption is not None:
        bottom=para(caption,x,bottom-7,w,9.5)
    return bottom

def gallery(keys,y):
    for row in range(2):
        bottom=y
        for col in range(2):
            key=keys[row*2+col]
            bottom=min(bottom,image(key,M+col*(COL+GAP),y,COL,REVIEW['images'][key]['caption']))
        y=bottom-21
    return y

def heading(text,y):return para(text,M,y,CW,12,bold=True,color=GREEN)-8

y=page('People, camps and daily life','What is implemented, what the screenshots show, and what still needs work.')
y=image('hero',M,y,CW,REVIEW['images']['hero']['caption'])-19
y=heading('A community foundation',y)
y=para('Human groups now appear from the world simulation, with named residents, households, camps and daily routines. Their location and way of life come from regional data; they are not a crowd spawned around the player.',M,y,CW)-18
for i,(title,body) in enumerate([
 ('World-derived','Species, culture, language, belief and known techniques inform local communities.'),
 ('Local detail','Lightweight group tracking extends to 50 km; bodies, homes and activities appear nearby.'),
 ('Repeatable','Seeded identities and itineraries reproduce unvisited groups. Consequential local changes are saved.'),
 ('First pass','This is a procedural scheduling foundation. Dialogue, NPC hunting and a complete economy remain deferred.')]):
    col=i%2;row=i//2;x=M+col*(COL+GAP);top=y-row*94
    para(title,x,top,COL,11,bold=True,color=GREEN)
    para(body,x,top-19,COL,10)

y=page('Different camps, different lives','Shelter form, covering and residence patterns respond to the community and its environment.')
y=gallery(['camp_wet','camp_collector','camp_cold','camp_evening'],y)
y=heading('More than a village template',y)
y=para('Camps combine household shelters, hearths and bedding using the same material-backed objects as player construction. Footing surveys avoid cliffs, water, trunks and boulders; occupied floors clear grass. Seasonal bases, mobile camps and dependable waterside bases have different stays and journeys.',M,y,CW)-15
para('A whole community need not be standing at home: work parties can be away. The population listed in captions is the community total, not a requirement for everyone to crowd the screenshot.',M,y,CW,10,color=MUTED)

y=page('A day of work','Schedules choose activities; residents move between home, work areas and short gathering trips.')
y=gallery(['work_craft','work_repair','work_food','work_travel'],y)
y=heading('What changes the routine?',y)
y=para('Local daylight, weather, age, skills, family care and culture influence the schedule. It includes fuel and plant gathering, water trips, craft work, shelter maintenance, food preparation and learning. Hot conditions can prompt rest; heavy rain can redirect work toward shelter.',M,y,CW)-15
para('<b>Current limit:</b> these are movement, pose and prop routines, not a complete production/consumption simulation. A task label can describe someone heading to work; it does not prove that the pictured action has already started.',M,y,CW,10,color=MUTED)

y=page('Families and shared time','Residents have persistent identities and household relationships rather than interchangeable villager roles.')
y=gallery(['family_care','family_carried','family_play','family_social'],y)
y=heading('People behind the crowd',y)
y=para('Names are derived from the group\'s language. Household records include parent, partner and child links; ages and skills affect responsibilities. Children use age-specific bodies, infants have carers and carrying arrangements, and adults can share care or teaching duties. Work is not universally locked to one sex or a single permanent chief.',M,y,CW)-15
para('Evening routines can gather households around shared hearths. Rest and sleep follow daylight, individual variation and age, rather than sending everyone to bed at the same instant.',M,y,CW,10,color=MUTED)

y=page('Moving camps, remembering people','The current foundation supports return visits; the screenshots also expose unfinished visual work.')
y=image('rest',M,y,CW,REVIEW['images']['rest']['caption'])-17
y=heading('Movement and persistence',y)
y=para('A dated itinerary covers occupation, packing, travel and setup. Packing removes shelter components; the next camp begins partially assembled. Old hearths and bedding can remain as decaying traces. Reliable waterside bases can stay occupied. Families retain their identities through these moves; wounds, aggression/trust and edited camp objects survive current-version saves.',M,y,CW)-15
y=heading('What this screenshot pass found',y)
y=para(REVIEW['findings'],M,y,CW,10)-14
y=heading('Try it in the game',y)
y=para('<b>/camps</b> lists nearby communities. <b>/camps N</b> inspects one; <b>/camps visit N</b> visits it. F3 inspection identifies residents and their scheduled task.',M,y,CW,10)-13
para(REVIEW['verification_note'],M,y,CW,9,color=MUTED)

C.save()
if PAGE!=5:raise RuntimeError('Unexpected page count')
scratch=ROOT/'artifacts/npc-report-20261104';scratch.mkdir(parents=True,exist_ok=True)
(scratch/'pdf-layout.json').write_text(json.dumps(LAYOUT,indent=2)+'\n')
print(OUT)
