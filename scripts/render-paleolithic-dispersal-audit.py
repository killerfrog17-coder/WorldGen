"""Render regional audit data without loading a game or realising any NPCs."""
import argparse, json, math, pathlib, struct
from PIL import Image, ImageDraw, ImageFont

parser=argparse.ArgumentParser()
parser.add_argument('directory',type=pathlib.Path)
args=parser.parse_args()
root=args.directory
width,height=1024,512
indices=struct.unpack('<'+'i'*(width*height),(root/'regions.i32').read_bytes())
land=(root/'land.rgb').read_bytes()
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',19)
small=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',14)
frames=[]
for year in [0,25000,100000,190000]:
    path=root/f'year-{year}.json'
    if not path.exists(): continue
    rows={r['District']:r for r in json.loads(path.read_text())}
    for mode in ['ancestry','clothing']:
        palette={}
        for k,r in rows.items():
            if r['Population']<1: continue
            if mode=='ancestry':
                t=r['Share']; strength=min(1,math.log1p(r['Population']/r['AreaKm2']*100)/2)
                palette[k]=tuple(round(a*(1-t)+b*t) for a,b in zip((191,138,88),(62,215,195)))
                palette[k]=tuple(round(c*(.4+.6*strength)) for c in palette[k])
            else:
                t=r['Access'][13]
                palette[k]=tuple(round(a*(1-t)+b*t) for a,b in zip((133,110,91),(113,194,255)))
        pixels=bytearray(land)
        for i,k in enumerate(indices):
            if k in palette:pixels[i*3:i*3+3]=bytes(palette[k])
        map_image=Image.frombytes('RGB',(width,height),bytes(pixels))
        image=Image.new('RGB',(width,height+75),(20,27,35));image.paste(map_image,(0,75));draw=ImageDraw.Draw(image)
        draw.text((18,9),f'{root.name} | year {year:,} | '+('Human ancestry and population density' if mode=='ancestry' else 'Fitted clothing: share of bands with access'),font=font,fill='white')
        draw.text((18,40),'Teal: sapiens  /  ochre: other hominins  /  dimmer: lower density' if mode=='ancestry' else 'Brown: no access  /  blue: widespread access  /  grey-green: uninhabited',font=small,fill=(190,201,215))
        image.save(root/f'{mode}-{year}.png')
        if mode=='ancestry':frames.append(image.resize((768,440)))
if frames:
    sheet=Image.new('RGB',(1536,880),(20,27,35))
    for i,im in enumerate(frames):sheet.paste(im,((i%2)*768,(i//2)*440))
    sheet.save(root/'ancestry-contact-sheet.jpg',quality=90)
