"""Concise screenshot report; all inputs live outside disposable artifacts."""
from pathlib import Path
import hashlib,json,sys
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools/reporting/python'))
from reportlab.pdfgen import canvas
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4,landscape
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.utils import ImageReader

IMAGES=ROOT/'docs/reports/tree-bark-images'
DATA=json.loads((IMAGES/'review.json').read_text(encoding='utf-8'))
for name,digest in DATA['images'].items():
    assert hashlib.sha256((IMAGES/name).read_bytes()).hexdigest()==digest,name
OUT=ROOT/'docs/reports/WorldGen - Tree Trunks and Bark.pdf'
for name,file in [('Body','calibri.ttf'),('Bold','calibrib.ttf')]:
    pdfmetrics.registerFont(TTFont(name,str(Path('C:/Windows/Fonts')/file)))
pdfmetrics.registerFontFamily('Body',normal='Body',bold='Bold',italic='Body',boldItalic='Bold')
W,H=landscape(A4);M=34;CW=W-M*2;G=18;COL=(CW-G)/2
INK=colors.HexColor('#26392F');MUTED=colors.HexColor('#617168');GREEN=colors.HexColor('#2B634C');PALE=colors.HexColor('#EEF3ED')
C=canvas.Canvas(str(OUT),pagesize=(W,H),pageCompression=1)
C.setTitle('WorldGen | Tree Trunks and Bark');C.setAuthor('WorldGen')
PAGE=0
def text(s,x,y,w,size=11,bold=False,color=INK):
    p=Paragraph(s,ParagraphStyle('p',fontName='Bold' if bold else 'Body',fontSize=size,leading=size*1.23,textColor=color))
    _,h=p.wrap(w,400);p.drawOn(C,x,y-h);return y-h
def page(title,subtitle):
    global PAGE
    if PAGE:C.showPage()
    PAGE+=1
    C.setFillColor(GREEN);C.rect(0,H-8,W,8,fill=1,stroke=0)
    text(title,M,H-29,CW,25,True)
    text(subtitle,M,H-64,CW,11,color=MUTED)
    C.setStrokeColor(colors.HexColor('#D5DFD5'));C.line(M,24,W-M,24)
    text('WorldGen | 7 November 2026',M,19,CW/2,8,color=MUTED)
    C.setFont('Body',8);C.setFillColor(MUTED);C.drawRightString(W-M,10,str(PAGE))
def image(name,x,y,w,h):
    C.setFillColor(PALE);C.rect(x,y,w,h,fill=1,stroke=0)
    C.drawImage(ImageReader(str(IMAGES/name)),x,y,w,h,preserveAspectRatio=True,anchor='c',mask='auto')
def trunk_crop(name,x,y,w,h):
    # PDF framing only; the preserved screenshot pixels and colours are intact.
    reader=ImageReader(str(IMAGES/name));sw,sh=reader.getSize();scale=w*2.8/sw
    C.saveState();clip=C.beginPath();clip.rect(x,y,w,h);C.clipPath(clip,stroke=0,fill=0)
    C.drawImage(reader,x+w*.5-sw*scale*.5,y+h*.5-sh*scale*.5,sw*scale,sh*scale,mask='auto');C.restoreState()

page('Tree trunks & generated bark','Continuous trunks and a complete set of five image-generated bark materials.')
image('reported-old-trunk.png',M,230,235,270)
trunk_crop('world-bark.png',M+253,230,CW-253,270)
text('Reported in the old shortcut build',M,220,235,10,True)
text('Actual forest in the updated renderer · cropped',M+253,220,CW-253,10,True)
text('Different scene and camera. The close material comparisons on the next page use matched views.',M,196,CW,9,color=MUTED)
text('<b>Trunk seams:</b> retains the preceding continuous-ring trunk fix, smooth taper normals and matching wrap tangents. This revision replaces the remaining scanned bark without changing the tree geometry.',M,169,CW,11)
text('<b>Bark:</b> all five bark types now use image-generated sources: smooth, fissured, scaly, palm-fibrous and peeling. Linear-light exposure retains colour detail; repeat edges and approximate relief normals are prepared offline.',M,122,CW,11)
text('<b>Delivery:</b> the shortcut selects the verified candidate at handoff. An already running old session needs to be relaunched to show the changes.',M,69,CW,10,color=MUTED)

page('Matched material views','The same specimen, camera and production shader: previous scanned materials versus generated replacements.')
text('PREVIOUS SCANNED BARK',M,507,COL,10,True,color=MUTED)
text('UPDATED',M+COL+G,507,COL,10,True,color=GREEN)
image('peeling-before.png',M,283,COL,212)
image('peeling-after.png',M+COL+G,283,COL,212)
text('Peeling bark: fibrous strips and woody grain replace the old mottled scanned surface.',M,275,CW,10)
image('scaly-before.png',M,49,COL,212)
image('scaly-after.png',M+COL+G,49,COL,212)
text('Scaly bark: layered gray-brown pine plates replace the orange-speckled scanned surface.',M,41,CW,9,color=MUTED)

page('Validation & rendering budget','The changes run in authoring/model generation; the foliage shader and its texture-fetch count are unchanged.')
names=['Smooth','Fissured','Scaly','Fibrous','Peeling'];sw=(CW-4*12)/5
for i,name in enumerate(names):
    image('bark-'+str(i)+'.png',M+i*(sw+12),378,sw,133)
    text(name+' · generated',M+i*(sw+12),368,sw,10,True)
cards=[('11 forms × 2 LODs','Wrap normal/tangent checks pass'),('Same triangle counts','Compared with the staged tree build'),('3.33 MiB bark bank','Same five512px BC7 material pairs'),('Native checks pass','Packaged tree and forest shaders')]
for i,(title,caption) in enumerate(cards):
    x=M+i*CW/4;C.setFillColor(PALE);C.roundRect(x,274,CW/4-8,65,5,fill=1,stroke=0)
    text(title,x+10,327,CW/4-28,12,True);text(caption,x+10,304,CW/4-28,9,color=MUTED)
text('Published bark pixels passed <b>330,241</b> boundary, unit-normal and linear-mip checks. Native scratch-buffer checks ensure generated meshes own their data after cached buffers are reused.',M,257,CW,10.5)
text('<b>Performance scope:</b> no new draw passes, texture layers or per-frame processing. A live FPS comparison was not recorded while the existing WorldGen session was running; unchanged budgets are not a global FPS guarantee.',M,216,CW,10.5)
text('<b>Limits:</b> all bark relief is inferred from generated colour, not measured geometry. Branch junction shapes remain the existing procedural architecture. Foliage textures are unchanged; this revision replaces the bark sources only.',M,167,CW,10)
refs=[('Unity — normals at UV seams','https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/mesh/recalculatenormals'),('Adobe — light cancellation, tiling and normal inputs','https://helpx.adobe.com/substance-3d-b2m/desktop/parameters.html'),('Moisan2011 — periodic-plus-smooth image decomposition','https://helios2.mi.parisdescartes.fr/~moisan/papers/2009-11r.pdf'),('Poly Haven — scan reuse terms and attribution','https://polyhaven.com/license')]
for i,(label,url) in enumerate(refs):text('<link href="'+url+'" color="#2B634C">'+label+'</link>',M,120-i*15,CW,9)
text('All five master paths and full prompts: assets/verification/tree-bark-generation-prompts-20261107.json. Built-in image_gen; canonical assets backed up in the verified GitHub asset release.',M,53,CW,8,color=MUTED)
C.save()
print('Created',OUT,'(3 pages)')
