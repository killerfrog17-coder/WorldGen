"""Pin and pack a CC0 photographed moss mat into the existing one-fetch shader layout."""
from pathlib import Path
import hashlib, io, json, zipfile
import requests
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'assets-source/terrain/surfaces/cc0-moss/Moss001'
OUTPUT=ROOT/'unity/WorldGen.Unity/Assets/WorldGen/Resources/GroundMossV1.png'
URL='https://ambientcg.com/get?file=Moss001_1K-JPG.zip'
def main():
    SOURCE.mkdir(parents=True,exist_ok=True)
    color=SOURCE/'Moss001_1K-JPG_Color.jpg';normal=SOURCE/'Moss001_1K-JPG_NormalGL.jpg'
    if not color.exists() or not normal.exists():
        response=requests.get(URL,timeout=90);response.raise_for_status()
        with zipfile.ZipFile(io.BytesIO(response.content)) as archive:
            for destination in (color,normal):destination.write_bytes(archive.read(destination.name))
    rgb=np.asarray(Image.open(color).convert('RGB'),dtype=float)/255
    linear=np.where(rgb<=.04045,rgb/12.92,((rgb+.055)/1.055)**2.4)
    luminance=linear@np.array([.2126,.7152,.0722]);luminance=np.clip(luminance*.5/luminance.mean(),0,1)
    normals=np.asarray(Image.open(normal).convert('RGB'),dtype=float)/255
    packed=np.dstack([luminance,normals[:,:,:2],np.ones(luminance.shape)])
    Image.fromarray(np.uint8(packed*255+.5)).save(OUTPUT)
    record={'asset':'Moss001','page':'https://ambientcg.com/view?id=Moss001','license':'CC0-1.0','license_url':'https://docs.ambientcg.com/license/','download':URL,'repeat_metres':.45,'processing':'R mean-normalized linear photographed luminance; G/B OpenGL tangent normal X/Y; A=1. Existing one-fetch mat layout, linear BC7 mipmapped 1024px.', 'files':{str(p.relative_to(ROOT)).replace(chr(92),'/'):{'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in (color,normal,OUTPUT)}}
    (SOURCE/'source.json').write_text(json.dumps(record,indent=2)+'\n')
    (ROOT/'assets/verification/ground-moss-source-20261106.json').write_text(json.dumps(record,indent=2)+'\n')
    print('Prepared',OUTPUT)
if __name__=='__main__':main()
