"""Screenshot report; originals stay unaltered, JPEG used only for PDF export."""
from pathlib import Path
import hashlib,io,json,sys
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools/reporting/python'))
from PIL import Image
from reportlab.pdfgen import canvas
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4,landscape
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.utils import ImageReader

DIR=ROOT/'docs/reports/ground-textures-20261107'
DATA=json.loads((DIR/'review.json').read_text(encoding='utf-8'))
SOURCES=json.loads((ROOT/'assets/verification/generated-ground-sources-20261107.json').read_text(encoding='utf-8'))
for name,digest in DATA['images'].items():assert hashlib.sha256((DIR/name).read_bytes()).hexdigest()==digest,name
for row in SOURCES['materials']:assert hashlib.sha256((ROOT/row['master']).read_bytes()).hexdigest()==row['master_sha256']
OUT=ROOT/'docs/reports/WorldGen - Generated Ground Textures.pdf'
for name,file in [('Body','calibri.ttf'),('Bold','calibrib.ttf')]:pdfmetrics.registerFont(TTFont(name,str(Path('C:/Windows/Fonts')/file)))
pdfmetrics.registerFontFamily('Body',normal='Body',bold='Bold',italic='Body',boldItalic='Bold')
W,H=landscape(A4);M=34;CW=W-2*M;G=18;COL=(CW-G)/2
INK=colors.HexColor('#293D31');MUTED=colors.HexColor('#637065');GREEN=colors.HexColor('#30604A');PALE=colors.HexColor('#EFF2EA')
C=canvas.Canvas(str(OUT),pagesize=(W,H),pageCompression=1);C.setTitle('WorldGen | Generated Ground Textures');C.setAuthor('WorldGen');PAGE=0
def text(s,x,y,w,size=11,bold=False,color=INK):
    p=Paragraph(s,ParagraphStyle('p',fontName='Bold' if bold else 'Body',fontSize=size,leading=size*1.22,textColor=color));_,h=p.wrap(w,500);p.drawOn(C,x,y-h);return y-h
def page(title,subtitle):
    global PAGE
    if PAGE:C.showPage()
    PAGE+=1;C.setFillColor(GREEN);C.rect(0,H-8,W,8,fill=1,stroke=0)
    text(title,M,H-29,CW,24,True);text(subtitle,M,H-64,CW,10.5,color=MUTED)
    C.setStrokeColor(colors.HexColor('#D6DFD4'));C.line(M,24,W-M,24)
    text('WorldGen | 7 November 2026',M,19,CW/2,8,color=MUTED);C.setFillColor(MUTED);C.setFont('Body',8);C.drawRightString(W-M,10,str(PAGE))
def image(path,x,y,w,h):
    C.setFillColor(PALE);C.rect(x,y,w,h,fill=1,stroke=0)
    # Ordinary document encoding; canonical source/screenshot PNGs are unchanged.
    raw=Image.open(path).convert('RGB');raw.thumbnail((max(256,round(w*2.5)),max(256,round(h*2.5))),Image.Resampling.LANCZOS)
    buf=io.BytesIO();raw.save(buf,format='JPEG',quality=95,subsampling=0,optimize=True);buf.seek(0)
    C.drawImage(ImageReader(buf),x,y,w,h,preserveAspectRatio=True,anchor='c')
def shot(name,x,y,w,h):image(DIR/name,x,y,w,h)

page('Ground textures — all generated','19 source materials replace the terrain and low ground-cover textures; placement and terrain geometry are unchanged.')
text('PREVIOUS FOREST FLOOR',M,505,COL,10,True,color=MUTED);text('GENERATED REPLACEMENT',M+COL+G,505,COL,10,True,color=GREEN)
shot('forest-before.png',M,276,COL,216);shot('forest-after.png',M+COL+G,276,COL,216)
text('Actual world, same site, ground camera and Medium vegetation. Existing ecological colours and material blending are retained.',M,263,CW,10,color=MUTED)
shot('grass-probe.png',M,69,246,164)
text('Grass detail',M+266,231,CW-266,17,True)
text('The generated grass surface has readable blades and thatch. This image is an isolated flat plane using the production terrain shader; physical grass geometry is unchanged.',M+266,204,CW-266,11)
text('<b>Scope:</b> soil, sand, gravel, clay, organic earth, salt and ice; six rock families; three forest floors; grass, moss and lichen. The existing generic rock slot shares granite.',M+266,145,CW-266,11)
text('<b>Delivery:</b> the shortcut selects the verified new build. Relaunch an already running older session to see the new materials.',M,54,CW,9,color=MUTED)

page('In the world','Actual generated landscapes, Medium vegetation. These views check material integration, not an uncontended FPS benchmark.')
views=[('sandsea-ground.png','Sand: fine grains and gentle ripple detail'),('savanna-ground.png','Wooded savanna: grass/soil detail under existing plants'),('limestoneupland-ground.png','Limestone upland: local rock and ground blends'),('forest-wide.png','Forest: generated floor within the existing vegetation')]
for i,(name,caption) in enumerate(views):
    x=M+(i%2)*(COL+G);y=286 if i<2 else 59
    shot(name,x,y,COL,200);text(caption,x,y-7,COL,10,color=MUTED)

page('The material set','Generated base-colour masters, before neutral mean preparation and the game’s ecological/geological tint.')
labels={'soil':'Mineral soil','sand':'Sand','gravel':'Gravel','clay':'Clay','granite':'Granite / generic rock','basalt':'Basalt','sandstone':'Sandstone','limestone':'Limestone','mudstone':'Mudstone','metamorphic':'Metamorphic rock','organic':'Organic earth','ice':'Ice / firn','broadleaf':'Broadleaf litter','needles':'Needle litter','damp':'Damp mixed litter','salt':'Salt crust','grass':'Grass surface','moss':'Moss cushions','lichen':'Lichen crust'}
cell=CW/5
for i,row in enumerate(SOURCES['materials']):
    x=M+(i%5)*cell;y=H-100-(i//5)*100-82
    image(ROOT/row['master'],x,y,cell-10,82);text(labels[row['key']],x,y-5,cell-10,9.5,True)
text('<b>Verified:</b> 462,697 layout, repeat-mip, unit-normal, linear-light and production-shader checks. All 17 terrain slices are replaced; the terrain texture payload remains <b>28.69 MiB</b> (1024 / 512 / 128 BC7 arrays). No shader passes or samples added. The 256px lichen map replaces its procedural RGBA32 counterpart with BC7.',M,87,CW,9)
text('<b>Limits:</b> relief, occlusion and roughness are conservative authoring approximations derived from colour, not measured PBR. No displacement or new terrain shapes. No FPS claim while the owner’s separate game session was running.',M,51,CW,8.5)
C.save();print('Created',OUT,'(3 pages)')
