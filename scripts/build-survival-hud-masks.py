"""Convert the approved SVG silhouettes into four runtime distance fields.

No AI redraw or baked HUD screenshot: only fixed shape geometry is rasterized.
All fills, bars, colours and thermal falloff remain live shader inputs.
"""
import json,math,re,uuid
from pathlib import Path
import xml.etree.ElementTree as ET
import numpy as np
from scipy.ndimage import distance_transform_edt
from PIL import Image,ImageDraw

ROOT=Path(__file__).resolve().parent.parent
SOURCE=ROOT/'assets/survival-hud'
DEST=ROOT/'unity/WorldGen.Unity/Assets/WorldGen/Resources/SurvivalHud'
SIZE=1024;SCALE=SIZE/600
cfg=json.loads((SOURCE/'HUD_Configuration.json').read_text(encoding='utf-8'))
svg=ET.parse(SOURCE/'HUD_Neutral_Reference.svg').getroot();ns={'s':'http://www.w3.org/2000/svg'}
food=svg.find("s:g[@id='stomach']/s:path",ns).get('d')
numbers=[float(n) for n in re.findall(r'-?\d+(?:\.\d+)?',food)]
food_points=[(332+(x-430)*.14*cfg['lockedLayout']['stomach']['scale'][0],301+(y-360)*.14*cfg['lockedLayout']['stomach']['scale'][1]) for x,y in zip(numbers[::2],numbers[1::2])]
def cubic(a,b,c,d):
    return [(a[0]*(1-t)**3+3*b[0]*t*(1-t)**2+3*c[0]*t*t*(1-t)+d[0]*t**3,
             a[1]*(1-t)**3+3*b[1]*t*(1-t)**2+3*c[1]*t*t*(1-t)+d[1]*t**3) for t in np.linspace(0,1,129)]
water=cubic((0,-51),(-9,-29),(-39,4),(-39,29))
water += [(39*math.cos(t),29+39*math.sin(t)) for t in np.linspace(math.pi,0,181)]
water += cubic((39,29),(39,4),(9,-29),(0,-51))
water_points=[(238+x*cfg['lockedLayout']['water']['scale'][0],261+y*cfg['lockedLayout']['water']['scale'][1]) for x,y in water]
def canvas():return Image.new('L',(SIZE,SIZE),0)
def polygon(points):
    im=canvas();ImageDraw.Draw(im).polygon([(round(x*SCALE),round(y*SCALE)) for x,y in points],fill=255);return im
def line(draw,a,b,width=10):
    a=tuple(v*SCALE for v in a);b=tuple(v*SCALE for v in b);draw.line((a,b),fill=255,width=round(width*SCALE))
    radius=width*SCALE/2
    for x,y in (a,b):draw.ellipse((x-radius,y-radius,x+radius,y+radius),fill=255)
sun=canvas();draw=ImageDraw.Draw(sun)
draw.ellipse(tuple(v*SCALE for v in (135,135,465,465)),fill=255)
draw.ellipse(tuple(v*SCALE for v in (145,145,455,455)),fill=0)
snow=canvas();sd=ImageDraw.Draw(snow)
for i in range(6):
    t=i*math.pi/3
    def point(x,y):return (300+x*math.cos(t)-y*math.sin(t),300+x*math.sin(t)+y*math.cos(t))
    line(draw,point(0,-160),point(0,-200))
    for a,b in [((0,0),(0,-200)),((-28,-172),(0,-200)),((0,-200),(28,-172)),((-20,-150),(0,-170)),((0,-170),(20,-150))]:line(sd,point(*a),point(*b))
def sdf(im):
    inside=np.asarray(im)>127
    # Pixel centres place the geometric edge halfway between opposite mask cells.
    d=(distance_transform_edt(~inside)-distance_transform_edt(inside))/SCALE
    d-=np.sign(d)*(.5/SCALE)
    return np.rint(np.clip(.5+d/32,0,1)*255).astype(np.uint8)
DEST.mkdir(parents=True,exist_ok=True)
im=Image.fromarray(np.stack([sdf(polygon(water_points)),sdf(polygon(food_points)),sdf(sun),sdf(snow)],axis=2))
im.save(DEST/'Shapes.png')
for folder in [DEST]:
    meta=folder.with_suffix('.meta')
    if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
print('HUD mask atlas:',DEST/'Shapes.png')
print('Water bounds:',np.min(water_points,axis=0),np.max(water_points,axis=0))
print('Stomach bounds:',np.min(food_points,axis=0),np.max(food_points,axis=0))
