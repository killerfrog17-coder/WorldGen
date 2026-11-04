"""Render the regional-update showcase from preserved, hash-verified captures."""
from pathlib import Path
import json,sys

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

IMAGES=ROOT/'docs/reports/regional-landscape-images'
DATA=json.loads((IMAGES/'review.json').read_text(encoding='utf-8'))
OUT=ROOT/'docs/reports/WorldGen - Regional Landscapes.pdf'
for name,file in [('Body','calibri.ttf'),('Bold','calibrib.ttf')]:
    pdfmetrics.registerFont(TTFont(name,str(Path('C:/Windows/Fonts')/file)))
pdfmetrics.registerFontFamily('Body',normal='Body',bold='Bold',italic='Body',boldItalic='Bold')
W,H=A4;M=34;CW=W-2*M;GAP=15;COL=(CW-GAP)/2
INK=colors.HexColor('#213A33');MUTED=colors.HexColor('#52665F')
GREEN=colors.HexColor('#285C49');LINE=colors.HexColor('#D5DFD7')
C=canvas.Canvas(str(OUT),pagesize=A4,pageCompression=1)
C.setTitle('WorldGen | Update 2: Regional Landscapes');C.setAuthor('WorldGen')
C.setSubject('Illustrated regional terrain, wetlands and small-water update showcase')
PAGE=0;LAYOUT=[]

def para(text,x,y,w,size=10.5,bold=False,color=INK):
    p=Paragraph(text,ParagraphStyle('p',fontName='Bold' if bold else 'Body',fontSize=size,leading=size*1.3,textColor=color))
    _,h=p.wrap(w,1500);p.drawOn(C,x,y-h)
    if y-h<37:raise RuntimeError(f'Page {PAGE} overflow: {text[:80]}')
    LAYOUT.append({'page':PAGE,'x':x,'bottom':y-h,'width':w,'height':h})
    return y-h

def page(title,subtitle):
    global PAGE
    if PAGE:C.showPage()
    PAGE+=1;C.setFillColor(colors.white);C.rect(0,0,W,H,fill=1,stroke=0)
    C.setFillColor(GREEN);C.rect(0,H-8,W,8,fill=1,stroke=0)
    para('WORLDGEN  /  UPDATE 2: REGIONAL LANDSCAPES',M,H-29,CW,8.5,bold=True,color=GREEN)
    para(title,M,H-49,CW,25,bold=True)
    para(subtitle,M,H-86,CW,10,color=MUTED)
    C.setStrokeColor(LINE);C.line(M,30,W-M,30)
    C.setFont('Body',8);C.setFillColor(MUTED)
    C.drawString(M,17,'Regional checkpoint captures  |  Before update 3 tree artwork')
    C.drawRightString(W-M,17,str(PAGE)+' / 5')
    C.bookmarkPage('page'+str(PAGE));C.addOutlineEntry(title,'page'+str(PAGE),0)
    return H-120

def feature(kind,title,body,y):
    y=para(title,M,y,CW,12,bold=True,color=GREEN)-5
    y=para(body,M,y,CW,10)-10
    bottom=y
    for col,view in enumerate(['wide','ground']):
        entry=DATA['images'][kind+'-'+view];r=ImageReader(str(IMAGES/entry['file']));iw,ih=r.getSize();height=COL*ih/iw;x=M+col*(COL+GAP)
        C.drawImage(r,x,y-height,COL,height,mask='auto')
        caption='Wide view' if view=='wide' else 'Ground detail'+(' (crop)' if entry['crop'] else '')
        bottom=min(bottom,para(caption,x,y-height-5,COL,8.5,color=MUTED))
    return bottom-23

def note(title,text,y):
    y=para(title,M,y,CW,11,bold=True,color=GREEN)-6
    return para(text,M,y,CW,10)-15

y=page('Regional landscapes','The same broad biome can now contain different ground, relief, vegetation and water settings.')
y=para('Update 2 adds local landscape character inside the existing world: sandy and gravelly deserts, dry basins, different wetlands, rocky uplands and enclosed ponds. The twenty images below are real wide/ground captures of ten sampled environments.',M,y,CW,10)-17
y=feature('SandSea','Sand seas','Loose sand supports dune relief and a sandy surface, with sparse dryland plants instead of a grass lawn. Relief looks modest in this midday view; ripples and surface texture are more apparent up close.',y)
y=feature('GravelDesert','Gravel deserts','A firmer gravelly surface carries scattered coarse stones and sparse scrub. This is a different substrate from mobile sand, rather than the same desert ground with a different label.',y)
note('How regions are chosen','Soil, rock, sediment, moisture, drainage, canopy and cold-ground conditions feed blended local controls. This changes physical ground and plant placement, not just the displayed biome name.',y)

y=page('Dry basins: clay and salt','Flat dry landscapes now differ in their surface material and ability to support plants.')
y=feature('ClayPan','Clay pans','Fine, dry basin ground uses a darker cracked-mud surface and sparse scrub. The cracking is small-scale material detail; the wide view remains a broad, relatively flat basin.',y)
y=feature('SaltFlat','Salt flats','Pale crust and fine drying cracks contrast with the clay pan. Cover is sparse on the flat, with more vegetation toward the less saline margin; this example is not a white mirror-like salt lake.',y)
y=note('More than these four dryland examples','Related controls also cover sand sheets, exposed rock deserts, loess hills and dissected soft ground. These ten sampled scenes are not an exhaustive showcase of every landscape label or every environment on Earth.',y)
note('New ground surfaces','Photographed sand, gravel and cracked mud replace four existing surface slots. The ground texture arrays retain the same fourteen-layer size and GPU payload; there are no extra ground-rendering passes for these surfaces.',y)

y=page('Open marsh to wooded wetland','Wet ground should not always turn into either an ordinary forest or a uniform open lawn.')
y=feature('FreshwaterMarsh','Freshwater marsh','Open sedge/rush cover surrounds shallow pools, with very little woody cover at this site. The neighbourhood contains three pools within 250 m; twenty nearby reed plants are rooted in shallow water.',y)
y=feature('WoodedSwamp','Wooded wetland','Suitable saturated ground can retain a forest around standing water. This site has four nearby pools and thirty-six reeds rooted in water. It is an actual wooded/water setting, rather than a moist upland forest renamed as swamp.',y)
y=note('Vegetation follows the surface','Wetness and rooting conditions affect tree admission. Reed-form plants can occupy shallow, slow water; ordinary shrubs stay on land. Cold, saline, shallow-soil and persistently flooded sites retain their exclusions.',y)
note('What still looks basic','The cover mix is functional, but much of the wetland vegetation still reads as a green grass sward in these captures. Plant artwork and pool-bank detail are not equally distinctive in every setting.',y)

y=page('Peatland and small ponds','Small standing water now has a real basin, not just a decorative water plane laid over dry ground.')
y=feature('Peatland','Peatland','Organic wet-ground controls favour moss/sedge cover and low woody density. This site contains five nearby pools. Its current appearance is close to open marsh; the distinction is stronger in the ground and cover data than in the artwork.',y)
y=feature('pond','Enclosed ponds','Candidate depressions are surveyed for a gentle, enclosed rim and given an actual depressed bed. This example has a roughly 14.6 m radius and is 1.71 m deep at the sampled centre. Drinking, swimming and visible water use the same basin.',y)
y=note('A small-water mosaic','Sparse deeper ponds are supplemented by finer shallow pools in suitable wetlands. Basin collars cannot overlap; dry banks, beds and water surfaces remain consistent across terrain chunks. Many outlines still look fairly regular in these examples.',y)
note('Static water bodies for now','Seasonal drying, freezing/thawing and a local water budget are not implemented. Pond density and shape remain tunable game parameters, rather than a claim to reproduce Earth\'s exact distribution.',y)

y=page('Uplands and cold country','Rock type, exposed substrate and cold-ground history add variation beyond the desert and wetland cases.')
y=feature('LimestoneUpland','Limestone uplands','Broken upland relief and exposed substrate contrast with soil-bearing, vegetated gullies. This illustrates regional ground character; it does not add caves, limestone towers or a separate landmark catalogue.',y)
y=feature('GlacialLowland','Glacial lowlands','Open, low-relief ground carries coarse stones and short patchy cover. This is exposed glacially influenced ground, not active ice cover. Permafrost alone no longer labels a region as a glacial landscape.',y)
y=note('Scope and cost','The broad world generator is retained. Local terrain/material controls and bounded pond caches add detail on demand; there is no worldwide per-pond tick simulation. Other controls include rocky/alpine ground, tundra, icefields and tidal wet settings. Waterfalls, volcanoes and other landmark features remain outside this update.',y)
para('Capture record: ten native cases / 84 checks at the verified regional checkpoint (3242861), before the later tree-art update. These are reused, hash-checked captures, not new tests of the current build. Source record: landscape-regions-20261103.json. No gameplay changes were made for this PDF.',M,y,CW,9,color=MUTED)

C.save()
assert PAGE==5
scratch=ROOT/'artifacts/region-report-20261104';scratch.mkdir(parents=True,exist_ok=True)
(scratch/'pdf-layout.json').write_text(json.dumps(LAYOUT,indent=2)+'\n')
print(OUT)
