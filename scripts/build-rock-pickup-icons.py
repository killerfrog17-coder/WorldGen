"""Rasterize original white rock glyphs; keep the editable vector sources.

No generated geometry/material data or external image service is required.
The taller existing FlakeableStonePickupV1 remains the suitability marker.
"""
from pathlib import Path
import uuid
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'assets-source/items/icon-masks'
TARGET=ROOT/'unity/WorldGen.Unity/Assets/WorldGen/Resources'
SHAPES={
 'PickupPebbleV1':[
  [('M',24,88),('C',12,85,11,72,20,65),('C',33,50,62,49,89,57),('C',107,64,118,80,108,87),('C',92,98,44,99,24,88),('Z',)],
  [('M',32,63),('C',50,57,74,59,91,68),('C',70,62,48,62,32,66),('Z',)]],
 'PickupSmallRockV1':[
  [('M',12,91),('L',8,72),('L',24,49),('C',34,41,43,38,52,36),('L',83,39),('L',111,62),('L',119,83),('C',115,90,106,96,99,98),('L',39,101),('Z',)],
  [('M',25,55),('L',49,66),('L',89,60),('L',50,71),('Z',)],
  [('M',50,74),('L',46,93),('L',52,82),('Z',)]],
 'PickupLargeRockV1':[
  [('M',8,94),('C',4,85,5,73,8,62),('L',29,35),('L',50,27),('L',82,32),('L',106,49),('L',120,74),('C',124,88,119,98,106,103),('L',38,106),('Z',)],
  [('M',31,42),('L',45,60),('L',86,55),('L',47,66),('Z',)],
  [('M',50,70),('L',36,96),('L',44,73),('Z',)],
  [('M',91,63),('L',109,82),('L',105,69),('Z',)]]
}

def flatten(commands):
    points=[];current=(0,0)
    for command in commands:
        op,*v=command
        if op in ('M','L'):current=tuple(v);points.append(current)
        elif op=='C':
            start=current
            for i in range(1,41):
                t=i/40;s=1-t
                points.append(tuple(s**3*start[k]+3*s*s*t*v[k]+3*s*t*t*v[k+2]+t**3*v[k+4] for k in (0,1)))
            current=tuple(v[4:])
    return points

SOURCE.mkdir(parents=True,exist_ok=True)
for name,paths in SHAPES.items():
    data=' '.join(' '.join(str(x) for x in command) for path in paths for command in path)
    svg=f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128"><path fill="white" fill-rule="evenodd" d="{data}"/></svg>\n'
    (SOURCE/(name+'.svg')).write_text(svg,encoding='utf-8')
    factor=8;mask=Image.new('L',(128*factor,128*factor),0);draw=ImageDraw.Draw(mask)
    for i,path in enumerate(paths):draw.polygon([(round(x*factor),round(y*factor)) for x,y in flatten(path)],fill=255 if i==0 else 0)
    mask=mask.resize((256,256),Image.Resampling.LANCZOS)
    image=Image.new('RGBA',mask.size,(255,255,255,0));image.putalpha(mask);image.save(TARGET/(name+'.png'))
    meta=TARGET/(name+'.png.meta')
    if not meta.exists():
        template=(TARGET/'FlakeableStonePickupV1.png.meta').read_text()
        lines=template.splitlines();lines=[('guid: '+uuid.uuid5(uuid.NAMESPACE_URL,'worldgen-icons/'+name).hex) if line.startswith('guid: ') else line for line in lines]
        meta.write_text('\n'.join(lines)+'\n')
    box=mask.getbbox();assert box[2]-box[0]>box[3]-box[1],(name,'Ordinary rock glyph must be wider than tall')
    print(name,box)
