"""Measure source texture statistics for runtime pigment recolouring.

Reads the licensed textures, writes numeric metadata only. No texture is edited.
"""
import json
from pathlib import Path
from PIL import Image
import numpy as np
ROOT=Path(__file__).resolve().parents[1]
rows={}
def measure(source,target):
    image=Image.open(source).convert('RGBA').resize((128,128));pixels=np.asarray(image,dtype=np.float32)/255
    rgb=pixels[:,:,:3];rgb=np.where(rgb<=.04045,rgb/12.92,((rgb+.055)/1.055)**2.4)
    luma=rgb@np.array([.2126,.7152,.0722]);valid=(pixels[:,:,3]>.8)&(luma>.002)
    rows[target]=max(.003,float(np.median(luma[valid]))) if valid.any() else .12
for letter in 'abcdef':
    folder=ROOT/f'assets-source/humans/appearance-runtime/exports/human-{letter}'
    for p in folder.glob('*.png'):measure(p,f'Assets/WorldGen/GeneratedHumanAppearance/human-{letter}/{p.name}')
for row in json.loads((ROOT/'assets-source/humans/phenotypes/exports/manifest.json').read_text()):
    for hair in row['hair']:
        p=Path(hair['texture']);measure(ROOT/p,'Assets/WorldGen/GeneratedHumanPhenotypes/'+p.parent.name+'-'+p.name)
output=ROOT/'assets-source/humans/phenotypes/pigments.json'
output.write_text(json.dumps({'rows':[{'path':k,'luminance':v} for k,v in sorted(rows.items())]},indent=2))
print('Measured',len(rows),'source-texture pigment references; no pixel files changed')
